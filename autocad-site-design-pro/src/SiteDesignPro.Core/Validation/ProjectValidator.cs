using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Validation
{
    /// <summary>Uniform grid for neighbourhood queries on plan bounding boxes (O(n) average for large projects).</summary>
    public sealed class SpatialGrid<T>
    {
        private readonly double cell;
        private readonly Dictionary<(long, long), List<(Box2 Box, T Item)>> cells = new Dictionary<(long, long), List<(Box2, T)>>();

        public SpatialGrid(double cellSize)
        {
            cell = Math.Max(0.5, cellSize);
        }

        public void Add(Box2 box, T item)
        {
            foreach ((long, long) key in Keys(box))
            {
                if (!cells.TryGetValue(key, out List<(Box2, T)>? list))
                {
                    list = new List<(Box2, T)>();
                    cells[key] = list;
                }

                list.Add((box, item));
            }
        }

        public IEnumerable<T> Query(Box2 box)
        {
            var seen = new HashSet<T>();
            foreach ((long, long) key in Keys(box))
            {
                if (!cells.TryGetValue(key, out List<(Box2 Box, T Item)>? list))
                {
                    continue;
                }

                foreach ((Box2 b, T item) in list)
                {
                    if (b.Intersects(box) && seen.Add(item))
                    {
                        yield return item;
                    }
                }
            }
        }

        private IEnumerable<(long, long)> Keys(Box2 b)
        {
            long x0 = (long)Math.Floor(b.MinX / cell), x1 = (long)Math.Floor(b.MaxX / cell);
            long y0 = (long)Math.Floor(b.MinY / cell), y1 = (long)Math.Floor(b.MaxY / cell);
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > 100000)
            {
                throw new InvalidOperationException("Raspon elementa je prevelik za prostorni indeks; provjerite jedinice.");
            }

            for (long x = x0; x <= x1; x++)
            {
                for (long y = y0; y <= y1; y++)
                {
                    yield return (x, y);
                }
            }
        }
    }

    /// <summary>
    /// Validates managed elements (package frame: metres). Distinguishes definite geometric/data errors,
    /// potential design conflicts and items that require professional review. No check is a compliance check.
    /// </summary>
    public sealed class ProjectValidator
    {
        private static readonly HashSet<string> SurfaceBuilders = new HashSet<string>(StringComparer.Ordinal)
        {
            "slab", "planting_area", "pool", "basin", "planter", "steps", "ramp", "massing",
        };

        private static readonly HashSet<string> RectangleBuilders = new HashSet<string>(StringComparer.Ordinal)
        {
            "pergola", "steps", "ramp", "stripes",
        };

        /// <summary>Builders whose footprints are fixed obstacles that also conflict with ground markings.</summary>
        private static readonly HashSet<string> ObstacleBuilders = new HashSet<string>(StringComparer.Ordinal)
        {
            "tree", "pole_mounted", "bench", "table", "shelter", "swing", "fountain", "boulder", "sign_post", "underground_container", "bike_stand",
        };

        private readonly ElementCatalog catalog;
        private readonly CheckSettings checks;

        public ProjectValidator(ElementCatalog catalog, CheckSettings? checks = null)
        {
            this.catalog = catalog;
            this.checks = checks ?? new CheckSettings();
        }

        /// <summary>Fills parameters missing from an element with the type defaults (in place).</summary>
        public static void FillDefaults(SiteElement e, ElementType type)
        {
            foreach (ParameterDefinition p in type.Parameters)
            {
                if (!e.Parameters.Contains(p.Key))
                {
                    e.Parameters[p.Key] = p.Default;
                }
            }
        }

        public ValidationReport Validate(IReadOnlyList<SiteElement> elements)
        {
            var report = new ValidationReport { ElementCount = elements.Count };
            var ids = new Dictionary<string, SiteElement>(StringComparer.Ordinal);
            var valid = new List<(SiteElement E, ElementType T)>();

            foreach (SiteElement e in elements)
            {
                int before = report.Issues.Count;
                if (!SiteElement.IsValidId(e.Id))
                {
                    report.Add(Severity.Error, IssueKind.DataError, "SDP-D001", null, "Element tipa '" + e.TypeId + "' nema ispravan identifikator.", "Pokrenite SDPAUDIT da dobije novi stalni identifikator.");
                    continue;
                }

                if (ids.ContainsKey(e.Id))
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D002", e.Id, "Dupli identifikator elementa (obično objekat kopiran zajedno s podacima).", "Pokrenite SDPAUDIT; kopije dobijaju nove identifikatore, a original zadržava svoj.") { Label = e.Label });
                    continue;
                }

                ids[e.Id] = e;
                ElementType? type = catalog.FindType(e.TypeId);
                if (type == null)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D003", e.Id, "Nepodržan tip elementa '" + e.TypeId + "'. Ne može se rekonstruisati u 3D.", "Instalirajte korisnički katalog koji definiše ovaj tip ili promijenite tip elementa.") { Label = e.Label });
                    continue;
                }

                ValidateGeometry(e, type, report);
                ValidateParameters(e, type, report);
                ValidateLogic(e, type, report);
                foreach (ValidationIssue issue in report.Issues.Skip(before))
                {
                    issue.Label ??= e.Label;
                }

                if (!report.Issues.Skip(before).Any(i => i.Severity == Severity.Error))
                {
                    valid.Add((e, type));
                }
            }

            ValidateLabels(elements, report);
            CheckPointCollisions(valid, report);
            if (checks.CheckSurfaceOverlaps)
            {
                CheckSurfaceOverlaps(valid, report);
            }

            if (checks.CheckCanopyClearance)
            {
                CheckCanopies(valid, report);
            }

            foreach (ValidationIssue issue in report.Issues.Where(i => i.Label == null && i.ElementId != null && ids.ContainsKey(i.ElementId)))
            {
                issue.Label = ids[issue.ElementId!].Label;
            }

            return report;
        }

        private void ValidateGeometry(SiteElement e, ElementType type, ValidationReport report)
        {
            if (e.Geometry.Kind != type.Geometry)
            {
                report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G001", e.Id,
                    "Geometrija je " + KindBs(e.Geometry.Kind) + ", a tip '" + type.Name + "' zahtijeva " + KindBs(type.Geometry) + ".",
                    type.Geometry == GeometryKind.Polygon ? "Koristite zatvorenu poliliniju granice." : type.Geometry == GeometryKind.Path ? "Koristite poliliniju trase." : "Koristite tačku umetanja.");
                return;
            }

            switch (e.Geometry)
            {
                case PointGeometry p:
                    if (!p.Position.IsFinite || !Compat.IsFinite(p.Rotation))
                    {
                        report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G002", e.Id, "Tačka umetanja ili rotacija nije konačan broj.", "Pomjerite ili ponovo umetnite element.");
                    }
                    else
                    {
                        CheckElevation(e, p.Position.Z, report);
                    }

                    break;
                case PathGeometry path:
                    {
                        if (path.Ring.Vertices.Any(v => !v.IsFinite))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G002", e.Id, "Trasa sadrži neispravne koordinate.", "Ponovo nacrtajte trasu.");
                            break;
                        }

                        Ring cleaned = GeometryMath.RemoveDuplicateVertices(path.Ring, path.Closed);
                        if (cleaned.Count < 2 || GeometryMath.Length(cleaned, path.Closed) < 1e-4)
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G003", e.Id, "Trasa ima dužinu nula.", "Ponovo nacrtajte trasu s najmanje dva različita tjemena.");
                            break;
                        }

                        if (cleaned.Count != path.Ring.Count)
                        {
                            report.Add(Severity.Warning, IssueKind.GeometricError, "SDP-G004", e.Id, "Trasa sadrži uzastopna dupla tjemena.", "Uklonite ih naredbom SDPAUDIT ili PEDIT; pri izvozu se zanemaruju.");
                        }

                        CheckElevation(e, path.Ring.Vertices.Average(v => v.Z), report);
                        break;
                    }

                case PolygonGeometry poly:
                    {
                        if (poly.Outer.Vertices.Any(v => !v.IsFinite))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G002", e.Id, "Granica sadrži neispravne koordinate.", "Ponovo nacrtajte granicu.");
                            break;
                        }

                        Ring outer = GeometryMath.RemoveDuplicateVertices(poly.Outer, true);
                        if (outer.Count < 3 && !(outer.Count == 2 && outer.HasArcs))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G005", e.Id, "Granica ima manje od tri različita tjemena.", "Nacrtajte površinu kao zatvorenu poliliniju s najmanje tri tjemena.");
                            break;
                        }

                        if (GeometryMath.IsSelfIntersecting(outer))
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G007", e.Id, "Granica siječe samu sebe.", "Ispravite granicu tako da se stranice ne sijeku (oblik osmice podijelite na zasebne površine).");
                            break;
                        }

                        double area = Math.Abs(GeometryMath.SignedArea(outer));
                        if (area < 1e-6)
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G006", e.Id, "Granica ne zatvara površinu.", "Ponovo nacrtajte granicu; tjemena ne smiju ležati na jednom pravcu.");
                            break;
                        }

                        List<Vec2> outerPts = GeometryMath.Tessellate(outer, true).Select(v => v.XY).ToList();
                        foreach (Ring hole in poly.Holes)
                        {
                            List<Vec2> holePts = GeometryMath.Tessellate(hole, true).Select(v => v.XY).ToList();
                            Vec2? ip = GeometryMath.InteriorPoint(holePts);
                            if (holePts.Count < 3 || ip == null || !GeometryMath.PointInPolygon(ip.Value, outerPts))
                            {
                                report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G008", e.Id, "Otvor leži izvan granice ili je neispravan.", "Nacrtajte otvor unutar vanjske granice.");
                            }
                        }

                        if (RectangleBuilders.Contains(type.Builder.Id) && !GeometryMath.IsRectangle(outer))
                        {
                            report.Add(Severity.Info, IssueKind.GeometricError, "SDP-G009", e.Id,
                                type.Name + " se generiše na orijentisanom pravougaoniku oko granice koja nije pravougaona.",
                                "Ako je tačan oblik važan, nacrtajte pravougaonik čija prva stranica određuje orijentaciju.");
                        }

                        if (type.Builder.Id == "sail" && outer.Count != 3 && outer.Count != 4)
                        {
                            report.Add(Severity.Error, IssueKind.GeometricError, "SDP-G011", e.Id, "Jedro za sjenu treba tačno 3 ili 4 ugaone tačke (pronađeno " + outer.Count + ").", "Nacrtajte konturu jedra s 3 ili 4 tjemena.");
                        }

                        CheckElevation(e, outer.Vertices.Average(v => v.Z), report);
                        break;
                    }
            }
        }

        private void CheckElevation(SiteElement e, double z, ValidationReport report)
        {
            if (Math.Abs(z) > checks.ElevationRange)
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-G012", e.Id,
                    Bs.T("Visina {0} m u odnosu na ishodište projekta je izvan ±{1} m.", Bs.Num(z, 3), Bs.Num(checks.ElevationRange, 0)),
                    "Provjerite visinu ishodišta projekta i jedinice crteža (crtež u milimetrima izvezen kao metri daje 1000 puta veće vrijednosti).");
            }
        }

        private void ValidateParameters(SiteElement e, ElementType type, ValidationReport report)
        {
            foreach (ParameterDefinition def in type.Parameters)
            {
                ParamValue v = e.Parameters.Contains(def.Key) ? e.Parameters[def.Key] : def.Default;
                if (v.IsNull)
                {
                    if (def.Required)
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P001", e.Id, "Obavezni parametar '" + def.Label + "' nije unesen.", "Unesite vrijednost naredbom SDPEDIT ili u paleti svojstava.");
                    }

                    continue;
                }

                switch (def.Type)
                {
                    case ParameterType.Length:
                    case ParameterType.Number:
                    case ParameterType.Integer:
                    case ParameterType.Angle:
                        {
                            double d = v.AsNumber();
                            if (v.Kind != ParamKind.Number || !Compat.IsFinite(d))
                            {
                                report.Add(Severity.Error, IssueKind.DataError, "SDP-P002", e.Id, "Parametar '" + def.Label + "' mora biti broj (uneseno '" + v.AsText() + "').", "Unesite brojčanu vrijednost.");
                                continue;
                            }

                            if (def.Type == ParameterType.Integer && Math.Abs(d - Math.Round(d)) > 1e-9)
                            {
                                report.Add(Severity.Error, IssueKind.DataError, "SDP-P003", e.Id, "Parametar '" + def.Label + "' mora biti cijeli broj.", "Unesite cijeli broj.");
                            }

                            if ((def.Min.HasValue && d < def.Min.Value - 1e-12) || (def.Max.HasValue && d > def.Max.Value + 1e-12))
                            {
                                report.Add(Severity.Error, IssueKind.DataError, "SDP-P004", e.Id,
                                    Bs.T("Parametar '{0}' = {1} {2} je izvan dozvoljenog raspona {3} do {4}.", def.Label, Bs.Num(d, 3), def.DisplayUnit, def.Min.HasValue ? Bs.Num(def.Min.Value, 3) : "-∞", def.Max.HasValue ? Bs.Num(def.Max.Value, 3) : "+∞"),
                                    "Unesite vrijednost unutar raspona. Ako je vrijednost u milimetrima, pretvorite je u metre.");
                            }

                            break;
                        }

                    case ParameterType.Bool:
                        if (v.Kind != ParamKind.Bool)
                        {
                            report.Add(Severity.Error, IssueKind.DataError, "SDP-P005", e.Id, "Parametar '" + def.Label + "' mora biti Da ili Ne.", "Odaberite Da ili Ne.");
                        }

                        break;
                    case ParameterType.Enum:
                        if (!def.Options.Contains(v.AsText()))
                        {
                            report.Add(Severity.Error, IssueKind.DataError, "SDP-P006", e.Id, "Parametar '" + def.Label + "' ima neispravnu opciju '" + v.AsText() + "'. Dozvoljeno: " + string.Join(", ", def.Options.Select(catalog.OptionLabel)) + ".", "Odaberite jednu od navedenih opcija.");
                        }

                        break;
                    case ParameterType.Material:
                        if (catalog.FindMaterial(v.AsText()) == null)
                        {
                            report.Add(Severity.Warning, IssueKind.DataError, "SDP-P007", e.Id, "Materijal '" + v.AsText() + "' nije u biblioteci materijala; koristi se zadani materijal.", "Odaberite materijal iz biblioteke ili ga dodajte u korisničku datoteku materijala.");
                        }

                        break;
                }
            }

            foreach (string key in e.Parameters.Keys.Where(k => type.Parameter(k) == null))
            {
                report.Add(Severity.Info, IssueKind.DataError, "SDP-P008", e.Id, "Parametar '" + key + "' nije definisan za tip '" + type.Name + "' i zanemaruje se.", "Vrijednosti specifične za projekat premjestite u korisnička polja.");
            }

            foreach (string req in type.ReviewRequired)
            {
                bool missing;
                string label;
                if (req.StartsWith("prop:", StringComparison.Ordinal))
                {
                    string key = req.Substring(5);
                    missing = string.IsNullOrWhiteSpace(e.Property(key));
                    PropertyDefinition? pd = catalog.CommonProperties.FirstOrDefault(p => p.Key == key);
                    label = pd?.Label ?? key;
                }
                else
                {
                    ParamValue v = e.Parameters.Contains(req) ? e.Parameters[req] : type.Parameter(req)?.Default ?? ParamValue.Null;
                    missing = v.IsNull || (v.Kind == ParamKind.Text && string.IsNullOrWhiteSpace(v.AsText()));
                    label = type.Parameter(req)?.Label ?? req;
                }

                if (missing)
                {
                    report.Add(Severity.Review, IssueKind.ProfessionalReview, "SDP-R001", e.Id,
                        "'" + label + "' nije definisano za " + type.Name + ". Zadane dimenzije i razmaci su samo orijentacioni.",
                        "Unesite vrijednost prema proizvođaču, botaničkom izvoru ili primjenjivom standardu i zabilježite izvor.");
                }
            }
        }

        private void ValidateLogic(SiteElement e, ElementType type, ValidationReport report)
        {
            ParameterSet p = e.Parameters;
            double N(string k) => p.Contains(k) ? p.Number(k) : type.Parameter(k)?.Default.AsNumber() ?? double.NaN;
            bool B(string k) => p.Contains(k) ? p.Bool(k) : type.Parameter(k)?.Default.AsBool() ?? false;
            switch (type.Builder.Id)
            {
                case "linear_slab":
                    {
                        double req = N("requiredClearWidth");
                        if (Compat.IsFinite(req) && N("width") + 1e-9 < req)
                        {
                            report.Add(Severity.Warning, IssueKind.DesignConflict, "SDP-C010", e.Id,
                                Bs.T("Širina {0} m je manja od zadane potrebne svijetle širine {1} m.", Bs.Num(N("width"), 3), Bs.Num(req, 3)),
                                "Proširite trasu ili potvrdite zahtjev. Upoređuju se samo vrijednosti projekta; ovo nije provjera propisa.");
                        }

                        break;
                    }

                case "ramp":
                    if (e.Geometry is PolygonGeometry rp && Compat.IsFinite(N("maxSlope")))
                    {
                        OrientedRect r = GeometryMath.OrientedBoundingRect(rp.Outer);
                        double slope = r.LengthV > 1e-9 ? N("rise") / r.LengthV * 100 : double.PositiveInfinity;
                        if (slope > N("maxSlope") + 1e-9)
                        {
                            report.Add(Severity.Warning, IssueKind.DesignConflict, "SDP-C011", e.Id,
                                Bs.T("Nagib rampe {0} % prelazi zadani najveći nagib {1} %.", Bs.Num(slope, 1), Bs.Num(N("maxSlope"), 1)),
                                "Produžite rampu ili smanjite visinsku razliku. Provjerite prema važećem propisu o pristupačnosti.");
                        }
                    }

                    break;
                case "pool":
                    if (N("waterLevelOffset") >= N("depth"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P010", e.Id, "Nivo vode je na dnu bazena ili ispod njega.", "Smanjite 'Nivo vode ispod ruba' ili povećajte dubinu.");
                    }

                    break;
                case "chamber":
                    if (N("coverSize") > N("innerSize"))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P011", e.Id, "Otvor poklopca je veći od unutrašnje dimenzije okna.", "Provjerite dimenzije okna i poklopca.");
                    }

                    break;
                case "underground_container":
                    if (p.Text("installation", type.Parameter("installation")?.Default.AsText() ?? "underground") == "underground" && N("columnDiameter") > Math.Min(N("bodyLength"), N("bodyWidth")))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P012", e.Id, "Stub za ubacivanje je širi od tijela kontejnera.", "Provjerite dimenzije kontejnera prema podacima proizvođača.");
                    }

                    break;
                case "tree":
                    if (N("clearTrunkHeight") >= N("height"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P013", e.Id, "Visina čistog debla nije manja od visine drveta.", "Smanjite visinu čistog debla ili povećajte visinu.");
                    }

                    if (Compat.IsFinite(N("matureHeight")) && N("matureHeight") < N("height"))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P014", e.Id, "Visina u zrelosti je manja od visine pri sadnji.", "Provjerite izvor podataka o rastu.");
                    }

                    break;
                case "bench":
                    if (B("backrest") && N("backHeight") <= N("seatHeight"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P015", e.Id, "Visina naslona nije iznad visine sjedišta.", "Povećajte ukupnu visinu s naslonom.");
                    }

                    break;
                case "fence":
                    if (N("postSpacing") <= N("postSize"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P016", e.Id, "Razmak stubova mora biti veći od dimenzije stuba.", "Povećajte razmak stubova.");
                    }

                    break;
                case "wall":
                    if (N("footingWidth") > 0 && N("footingDepth") <= 0)
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P017", e.Id, "Širina temelja je zadana, a dubina temelja je nula.", "Unesite dubinu temelja ili postavite širinu temelja na 0.");
                    }

                    break;
                case "basin":
                    if (N("waterDepth") > N("depth"))
                    {
                        report.Add(Severity.Error, IssueKind.DataError, "SDP-P018", e.Id, "Projektovana dubina vode prelazi dubinu bazena.", "Smanjite dubinu vode ili produbite bazen.");
                    }

                    break;
                case "sail":
                    if (N("highHeight") < N("lowHeight"))
                    {
                        report.Add(Severity.Warning, IssueKind.DataError, "SDP-P019", e.Id, "Visina visokog ugla je manja od visine niskog ugla.", "Zamijenite vrijednosti.");
                    }

                    break;
            }
        }

        private static void ValidateLabels(IReadOnlyList<SiteElement> elements, ValidationReport report)
        {
            foreach (IGrouping<string, SiteElement> g in elements.Where(e => !string.IsNullOrWhiteSpace(e.Label)).GroupBy(e => e.Label!, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                foreach (SiteElement e in g.Skip(1))
                {
                    report.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-D010", e.Id, "Oznaku '" + g.Key + "' koristi " + Bs.Elements(g.Count()) + ".", "Prenumerišite naredbom SDPNUMBER ili izmijenite oznaku.") { Label = e.Label, RelatedElementId = g.First().Id });
                }
            }
        }

        private void CheckPointCollisions(List<(SiteElement E, ElementType T)> valid, ValidationReport report)
        {
            var items = new List<(SiteElement E, ElementType T, Footprint F)>();
            foreach ((SiteElement e, ElementType t) in valid)
            {
                if (e.Geometry is PointGeometry pg)
                {
                    foreach (Footprint f in Footprints.For(t.Builder.Id, pg, e.Parameters))
                    {
                        items.Add((e, t, f));
                    }
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            var grid = new SpatialGrid<int>(5.0);
            for (int i = 0; i < items.Count; i++)
            {
                grid.Add(items[i].F.Bounds(), i);
            }

            var reported = new HashSet<(string, string)>();
            for (int i = 0; i < items.Count; i++)
            {
                (SiteElement a, ElementType ta, Footprint fa) = items[i];
                foreach (int j in grid.Query(fa.Bounds().Inflate(checks.CollisionTolerance)))
                {
                    if (j <= i)
                    {
                        continue;
                    }

                    (SiteElement b, ElementType tb, Footprint fb) = items[j];
                    if (a.Id == b.Id || !ZonesInteract(fa.Zone, ta, fb.Zone, tb))
                    {
                        continue;
                    }

                    if (fa.Inflate(checks.CollisionTolerance / 2).Intersects(fb.Inflate(checks.CollisionTolerance / 2)))
                    {
                        (string, string) key = string.CompareOrdinal(a.Id, b.Id) < 0 ? (a.Id, b.Id) : (b.Id, a.Id);
                        if (reported.Add(key))
                        {
                            report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C001", a.Id,
                                "Moguća kolizija: " + Describe(a, ta) + " i " + Describe(b, tb) + (fa.Zone == FootprintZone.Underground ? " (podzemno)." : "."),
                                "Pomjerite jedan od elemenata ili potvrdite da je preklapanje namjerno.") { RelatedElementId = b.Id });
                        }
                    }
                }
            }

            // Clearance envelopes (service / safety / lifting) entered by the user.
            foreach ((SiteElement e, ElementType t) in valid)
            {
                if (!(e.Geometry is PointGeometry pg))
                {
                    continue;
                }

                foreach (string key in new[] { "serviceClearance", "safetyClearance", "liftingClearance" })
                {
                    double clearance = e.Parameters.Number(key, double.NaN);
                    if (!Compat.IsFinite(clearance) || clearance <= 0)
                    {
                        continue;
                    }

                    foreach (Footprint own in Footprints.For(t.Builder.Id, pg, e.Parameters).Where(f => f.Zone != FootprintZone.Underground))
                    {
                        Footprint zone = own.Inflate(clearance);
                        foreach (int j in grid.Query(zone.Bounds()))
                        {
                            (SiteElement o, ElementType to, Footprint fo) = items[j];
                            if (o.Id == e.Id || fo.Zone == FootprintZone.Underground || fo.Zone == FootprintZone.Marking)
                            {
                                continue;
                            }

                            if (zone.Intersects(fo))
                            {
                                (string, string) pair = (e.Id + "#" + key, o.Id);
                                if (reported.Add(pair))
                                {
                                    report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C002", e.Id,
                                        Bs.T("{0} leži unutar {2} od {1} m elementa {3}.", Describe(o, to), Bs.Num(clearance, 2), Humanize(key), Describe(e, t)),
                                        "Premjestite element; razmak smanjite samo ako to dozvoljavaju izvorni podaci.") { RelatedElementId = o.Id });
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool ZonesInteract(FootprintZone a, ElementType ta, FootprintZone b, ElementType tb)
        {
            if (a == FootprintZone.Underground || b == FootprintZone.Underground)
            {
                return a == b;
            }

            if (a == FootprintZone.Marking && b == FootprintZone.Marking)
            {
                return true;
            }

            if (a == FootprintZone.Marking)
            {
                return ObstacleBuilders.Contains(tb.Builder.Id);
            }

            if (b == FootprintZone.Marking)
            {
                return ObstacleBuilders.Contains(ta.Builder.Id);
            }

            return true;
        }

        private static string Humanize(string key) => key switch
        {
            "serviceClearance" => "servisnog razmaka",
            "safetyClearance" => "sigurnosnog razmaka",
            "liftingClearance" => "prostora za podizanje",
            _ => key,
        };

        private static string KindBs(GeometryKind k) => k == GeometryKind.Point ? "tačka" : (k == GeometryKind.Path ? "trasa" : "površina");

        private static string Describe(SiteElement e, ElementType t) => (string.IsNullOrEmpty(e.Label) ? t.Name : e.Label + " (" + t.Name + ")");

        private void CheckSurfaceOverlaps(List<(SiteElement E, ElementType T)> valid, ValidationReport report)
        {
            var polys = new List<(SiteElement E, ElementType T, IReadOnlyList<IReadOnlyList<Vec2>> Rings, Box2 Box)>();
            foreach ((SiteElement e, ElementType t) in valid)
            {
                if (e.Geometry is PolygonGeometry pg && SurfaceBuilders.Contains(t.Builder.Id))
                {
                    var rings = new List<IReadOnlyList<Vec2>> { GeometryMath.PlanOutline(pg, 0.05) };
                    rings.AddRange(pg.Holes.Select(h => (IReadOnlyList<Vec2>)GeometryMath.Tessellate(h, true, 0.05).Select(v => v.XY).ToList()));
                    polys.Add((e, t, rings, GeometryMath.BoundsOf(rings[0])));
                }
            }

            var grid = new SpatialGrid<int>(20.0);
            for (int i = 0; i < polys.Count; i++)
            {
                grid.Add(polys[i].Box, i);
            }

            for (int i = 0; i < polys.Count; i++)
            {
                foreach (int j in grid.Query(polys[i].Box))
                {
                    if (j <= i)
                    {
                        continue;
                    }

                    if (GeometryMath.AreasOverlap(polys[i].Rings, polys[j].Rings))
                    {
                        bool building = polys[i].T.Builder.Id == "massing" || polys[j].T.Builder.Id == "massing";
                        report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, building ? "SDP-C004" : "SDP-C003", polys[i].E.Id,
                            (building ? "Površina preklapa tlocrt objekta: " : "Površine se preklapaju: ") + Describe(polys[i].E, polys[i].T) + " i " + Describe(polys[j].E, polys[j].T) + ".",
                            "Skratite jednu granicu tako da površine dijele ivice bez preklapanja ili potvrdite da je slojevitost namjerna (npr. podna obloga preko druge površine).") { RelatedElementId = polys[j].E.Id });
                    }
                }
            }
        }

        private void CheckCanopies(List<(SiteElement E, ElementType T)> valid, ValidationReport report)
        {
            var trees = valid.Where(v => v.T.Builder.Id == "tree" && v.E.Geometry is PointGeometry).ToList();
            if (trees.Count == 0)
            {
                return;
            }

            var buildings = valid.Where(v => v.T.Builder.Id == "massing" && v.E.Geometry is PolygonGeometry)
                .Select(v => (v.E, v.T, Pts: GeometryMath.PlanOutline((PolygonGeometry)v.E.Geometry, 0.05)))
                .ToList();
            foreach ((SiteElement tree, ElementType tt) in trees)
            {
                double mature = tree.Parameters.Number("matureCanopyDiameter", double.NaN);
                if (!Compat.IsFinite(mature))
                {
                    continue;
                }

                Vec2 pos = ((PointGeometry)tree.Geometry).Position.XY;
                double r = mature / 2;
                foreach ((SiteElement b, ElementType bt, IReadOnlyList<Vec2> pts) in buildings)
                {
                    bool inside = GeometryMath.PointInPolygon(pos, pts);
                    double d = GeometryMath.DistanceToBoundary(pos, pts);
                    if (inside || d < r)
                    {
                        report.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-C005", tree.Id,
                            Bs.T("Krošnja u zrelosti (prečnik {0} m) elementa {1} dopire do {2} (udaljenost {3} m).", Bs.Num(mature, 1), Describe(tree, tt), Describe(b, bt), Bs.Num(inside ? 0 : d, 2)),
                            "Pomjerite drvo ili odaberite vrstu s manjom krošnjom u zrelosti.") { RelatedElementId = b.Id });
                    }
                }
            }
        }
    }
}
