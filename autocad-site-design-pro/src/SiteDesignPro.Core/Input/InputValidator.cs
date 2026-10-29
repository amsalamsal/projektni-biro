using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geo;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Text;
using SiteDesignPro.Core.Units;
using SiteDesignPro.Core.Validation;

namespace SiteDesignPro.Core.Input
{
    public enum SourceKind
    {
        Polyline,
        Line,
        Arc,
        Circle,
        Text,
        Point,
        Block,
        Other,
    }

    /// <summary>A host-neutral snapshot of one DWG object (coordinates in drawing units).</summary>
    public sealed class SourceObject
    {
        public string Handle { get; set; } = string.Empty;

        public string Layer { get; set; } = string.Empty;

        public SourceKind Kind { get; set; }

        public bool Closed { get; set; }

        /// <summary>Vertices for curves (bulges in <see cref="Bulges"/>), insertion point for texts, points and blocks.</summary>
        public List<Vec3> Vertices { get; set; } = new List<Vec3>();

        public List<double>? Bulges { get; set; }

        public string Text { get; set; } = string.Empty;

        public string BlockName { get; set; } = string.Empty;

        /// <summary>True when the curve is not in the world XY plane (non-Z normal).</summary>
        public bool NonPlanar { get; set; }

        /// <summary>Leader end for labels with leaders.</summary>
        public Vec3? Target { get; set; }

        public Ring ToRing() => new Ring(Vertices, Bulges);
    }

    public sealed class InputSettings
    {
        public LengthUnit DrawingUnit { get; set; } = LengthUnit.Millimeter;

        /// <summary>Gap (metres) below which an open boundary is reported as "almost closed".</summary>
        public double CloseGapTolerance { get; set; } = 0.05;

        public string Crs { get; set; } = string.Empty;

        public ElevationDatum Datum { get; set; } = new ElevationDatum();
    }

    /// <summary>Statistics of the analysed drawing, shown with the report.</summary>
    public sealed class InputSummary
    {
        public Dictionary<SourceRole, int> ObjectsPerRole { get; } = new Dictionary<SourceRole, int>();

        public List<string> UnmappedLayers { get; } = new List<string>();

        public int ParcelCount { get; set; }

        public int BuildingCount { get; set; }

        public int MarkerCount { get; set; }

        public int ElevationLabelCount { get; set; }

        public int UsableElevationLabels { get; set; }

        public int TerrainPointsWithZ { get; set; }

        public double ParcelAreaM2 { get; set; }

        public string? CoordinateNote { get; set; }
    }

    /// <summary>
    /// Validates a source DWG against a layer mapping before anything is built. All messages are Bosnian. The
    /// validator reports - it never invents geometry, elevations or coordinates.
    /// </summary>
    public static class InputValidator
    {
        public static (ValidationReport Report, InputSummary Summary) Validate(IReadOnlyList<SourceObject> objects, LayerMapping mapping, InputSettings settings)
        {
            var r = new ValidationReport();
            var sum = new InputSummary();
            double k = UnitConversion.MetersPerUnit(settings.DrawingUnit);

            List<string> layers = objects.Select(o => o.Layer).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
            var byRole = new Dictionary<SourceRole, List<SourceObject>>();
            foreach (SourceRole role in SourceRoles.All)
            {
                byRole[role] = new List<SourceObject>();
            }

            // --- mapping problems
            foreach (SourceRole role in SourceRoles.All)
            {
                foreach (string pattern in mapping.Patterns(role))
                {
                    if (!layers.Any(l => LayerMapping.Matches(pattern, l)))
                    {
                        r.Add(Severity.Warning, IssueKind.DataError, "SDP-I001", null,
                            Bs.T("Sloj \"{0}\" mapiran kao \"{1}\" ne postoji u crtežu ili je prazan.", pattern, SourceRoles.Name(role)),
                            Bs.T("Uklonite ga iz mapiranja ili provjerite naziv sloja."));
                    }
                }
            }

            foreach (string layer in layers)
            {
                List<SourceRole> roles = mapping.RolesOf(layer);
                if (roles.Count > 1 && !(roles.Count == 2 && roles.Contains(SourceRole.BuildingFootprint) && roles.Contains(SourceRole.BuildingMarker)))
                {
                    r.Add(Severity.Warning, IssueKind.DataError, "SDP-I002", null,
                        Bs.T("Sloj \"{0}\" je dodijeljen u više uloga: {1}.", layer, string.Join(", ", roles.Select(SourceRoles.Name))),
                        Bs.T("Svaki sloj treba imati jednu ulogu; objekti se inače obrađuju više puta."));
                }
                else if (roles.Count == 0)
                {
                    sum.UnmappedLayers.Add(layer);
                }
            }

            foreach (SourceObject o in objects)
            {
                foreach (SourceRole role in mapping.RolesOf(o.Layer))
                {
                    byRole[role].Add(o);
                }
            }

            foreach (SourceRole role in SourceRoles.All)
            {
                sum.ObjectsPerRole[role] = byRole[role].Count;
            }

            // --- parcel
            List<SourceObject> parcels = byRole[SourceRole.ParcelBoundary].Where(IsCurve).ToList();
            sum.ParcelCount = parcels.Count(p => p.Closed);
            if (mapping.Patterns(SourceRole.ParcelBoundary).Count == 0)
            {
                r.Add(Severity.Error, IssueKind.DataError, "SDP-I010", null, Bs.T("Nijedan sloj nije mapiran kao parcelna granica."), Bs.T("U mapiranju slojeva odaberite sloj s granicom parcele."));
            }
            else if (parcels.Count == 0)
            {
                r.Add(Severity.Error, IssueKind.GeometricError, "SDP-I011", null, Bs.T("Na slojevima parcelne granice nema linija ni polilinija."), Bs.T("Nacrtajte ili učitajte granicu parcele kao zatvorenu poliliniju."));
            }

            CheckClosedCurves(r, parcels, SourceRoles.Name(SourceRole.ParcelBoundary), settings, k);
            var parcelAreas = new List<(SourceObject Obj, List<Vec2> Poly)>();
            foreach (SourceObject p in parcels.Where(p => p.Closed && p.Vertices.Count >= 3))
            {
                List<Vec2> poly = Plan(p, k);
                parcelAreas.Add((p, poly));
                sum.ParcelAreaM2 += Math.Abs(Area(poly));
            }

            if (parcelAreas.Count > 0)
            {
                if (sum.ParcelAreaM2 < 20)
                {
                    r.Add(Severity.Warning, IssueKind.DataError, "SDP-I012", null,
                        Bs.T("Površina parcele je samo {0}. Jedinice crteža ({1}) vjerovatno nisu ispravne.", Bs.SquareMeters(sum.ParcelAreaM2), UnitConversion.Code(settings.DrawingUnit)),
                        Bs.T("Provjerite jedinicu crteža (INSUNITS) i postavke projekta."));
                }
                else if (sum.ParcelAreaM2 > 5_000_000)
                {
                    r.Add(Severity.Warning, IssueKind.DataError, "SDP-I013", null,
                        Bs.T("Površina parcele je {0}. Jedinice crteža ({1}) vjerovatno nisu ispravne.", Bs.SquareMeters(sum.ParcelAreaM2, 0), UnitConversion.Code(settings.DrawingUnit)),
                        Bs.T("Provjerite jedinicu crteža (INSUNITS) i postavke projekta."));
                }
            }

            ReportDuplicates(r, parcels, SourceRoles.Name(SourceRole.ParcelBoundary), k);

            // --- buildings
            List<SourceObject> buildingCurves = byRole[SourceRole.BuildingFootprint].Where(IsCurve).ToList();
            List<SourceObject> markers = byRole[SourceRole.BuildingMarker].Concat(byRole[SourceRole.BuildingFootprint].Where(o => o.Kind == SourceKind.Block || o.Kind == SourceKind.Point)).Distinct().ToList();
            sum.BuildingCount = buildingCurves.Count(b => b.Closed);
            sum.MarkerCount = markers.Count;
            if (buildingCurves.Count == 0 && markers.Count == 0)
            {
                r.Add(Severity.Error, IssueKind.DataError, "SDP-I020", null, Bs.T("U crtežu nije pronađen tlocrt objekta ni oznaka položaja objekta."),
                    Bs.T("Mapirajte sloj s tlocrtom objekta. Program ne pretpostavlja položaj objekta."));
            }
            else if (sum.BuildingCount == 0 && markers.Count > 0)
            {
                r.Add(mapping.AllowMarkerWorkflow ? Severity.Warning : Severity.Error, IssueKind.DataError, "SDP-I021", null,
                    Bs.T("Crtež sadrži samo oznake položaja objekta ({0}), bez stvarnog tlocrta.", markers.Count),
                    mapping.AllowMarkerWorkflow
                        ? Bs.T("Uključen je rad s oznakama: dimenzije objekta morate potvrditi ručno za svaku oznaku.")
                        : Bs.T("Označite stvarnu geometriju tlocrta ili uključite rad s oznakama u mapiranju. Simbol se nikad ne tretira automatski kao tlocrt."));
            }

            CheckClosedCurves(r, buildingCurves, SourceRoles.Name(SourceRole.BuildingFootprint), settings, k);
            ReportDuplicates(r, buildingCurves, SourceRoles.Name(SourceRole.BuildingFootprint), k);
            if (parcelAreas.Count > 0)
            {
                foreach (SourceObject b in buildingCurves.Where(b => b.Closed && b.Vertices.Count >= 3))
                {
                    List<Vec2> poly = Plan(b, k);
                    if (!poly.All(v => parcelAreas.Any(pa => GeometryMath.PointInPolygon(v, pa.Poly) || GeometryMath.DistanceToBoundary(v, pa.Poly) < 0.01)))
                    {
                        r.Add(new ValidationIssue(Severity.Warning, IssueKind.DesignConflict, "SDP-I022", b.Handle,
                            Bs.T("Tlocrt objekta (sloj {0}) izlazi izvan parcelne granice.", b.Layer), Bs.T("Provjerite položaj objekta i granicu parcele.")));
                    }
                }
            }

            // --- roads and parterre
            if (byRole[SourceRole.Roads].Count == 0)
            {
                r.Add(Severity.Warning, IssueKind.DataError, "SDP-I030", null, Bs.T("Nisu pronađene saobraćajnice prema regulacionom planu."), Bs.T("Mapirajte sloj saobraćajnica ako postoji u crtežu."));
            }

            List<SourceObject> parterre = byRole[SourceRole.ParterreLine].Where(IsCurve).ToList();
            if (parterre.Count == 0)
            {
                r.Add(Severity.Warning, IssueKind.DataError, "SDP-I031", null, Bs.T("Linija partera nije pronađena."), Bs.T("Mapirajte sloj s linijom partera (preporučeno na posebnom sloju)."));
            }

            CheckClosedCurves(r, parterre.Where(p => !p.Closed && Gap(p, k) <= settings.CloseGapTolerance).ToList(), SourceRoles.Name(SourceRole.ParterreLine), settings, k);

            // --- elevations
            List<SourceObject> labels = byRole[SourceRole.ElevationLabels].Where(o => o.Kind == SourceKind.Text).ToList();
            sum.ElevationLabelCount = labels.Count;
            var parsedAt = new List<(SourceObject Obj, ParsedElevation P)>();
            foreach (SourceObject l in labels)
            {
                ParsedElevation p = ElevationLabelParser.Parse(l.Text);
                parsedAt.Add((l, p));
                if (p.Kind == LabelKind.Invalid)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-I040", l.Handle, Bs.T("Oznaka kote \"{0}\" nije čitljiva: {1}", l.Text, p.Problem ?? string.Empty), Bs.T("Ispravite tekst ili ga uklonite sa sloja kota.")));
                }
                else if (p.Kind == LabelKind.Ambiguous)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-I041", l.Handle, Bs.T("Oznaka kote \"{0}\": {1}", l.Text, p.Problem ?? string.Empty), Bs.T("Pri dodjeli kota odaberite vrstu kote ručno.")));
                }
                else
                {
                    sum.UsableElevationLabels++;
                    if (p.Absolute.HasValue && p.Relative.HasValue && settings.Datum.IsDefined && Math.Abs(settings.Datum.ToRelative(p.Absolute.Value) - p.Relative.Value) > 0.005)
                    {
                        r.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-I042", l.Handle,
                            Bs.T("Oznaka \"{0}\" nije u skladu s nultom kotom {1}.", l.Text, ElevationDatum.FormatAbsolute(settings.Datum.BuildingZeroAbsolute!.Value)),
                            Bs.T("Provjerite nultu kotu objekta ili tekst oznake.")));
                    }
                }
            }

            // same place, different values
            for (int i = 0; i < parsedAt.Count; i++)
            {
                for (int j = i + 1; j < parsedAt.Count; j++)
                {
                    (SourceObject a, ParsedElevation pa) = parsedAt[i];
                    (SourceObject b, ParsedElevation pb) = parsedAt[j];
                    if (a.Vertices.Count == 0 || b.Vertices.Count == 0 || (a.Vertices[0].XY - b.Vertices[0].XY).Length * k > 0.2)
                    {
                        continue;
                    }

                    double? va = pa.Relative ?? pa.Absolute, vb = pb.Relative ?? pb.Absolute;
                    if (va.HasValue && vb.HasValue && pa.Kind == pb.Kind && Math.Abs(va.Value - vb.Value) > 0.005)
                    {
                        r.Add(new ValidationIssue(Severity.Warning, IssueKind.DataError, "SDP-I043", a.Handle,
                            Bs.T("Dvije oznake kota na istom mjestu imaju različite vrijednosti (\"{0}\" i \"{1}\").", a.Text, b.Text), Bs.T("Uklonite zastarjelu oznaku.")) { RelatedElementId = b.Handle });
                    }
                }
            }

            List<SourceObject> terrainPts = byRole[SourceRole.TerrainPoints].Where(o => o.Kind == SourceKind.Point || o.Kind == SourceKind.Block).ToList();
            sum.TerrainPointsWithZ = terrainPts.Count(o => o.Vertices.Count > 0 && Math.Abs(o.Vertices[0].Z) > 1e-9);
            if (terrainPts.Count > 0 && sum.TerrainPointsWithZ == 0)
            {
                r.Add(Severity.Warning, IssueKind.DataError, "SDP-I044", null, Bs.T("Tačke terena ({0}) nemaju Z visinu (sve su na Z = 0).", terrainPts.Count),
                    Bs.T("Visine su možda samo u tekstu pored tačaka; koristite dodjelu kota ili učitajte 3D tačke."));
            }

            List<SourceObject> contours = byRole[SourceRole.Contours].Where(IsCurve).ToList();
            int flatContours = contours.Count(c => c.Vertices.All(v => Math.Abs(v.Z) < 1e-9));
            if (flatContours > 0)
            {
                r.Add(Severity.Warning, IssueKind.DataError, "SDP-I045", null, Bs.T("{0} izohipsi nema visinu (Z = 0).", flatContours), Bs.T("Postavite elevation izohipsi ili koristite dodjelu kota izohipsama."));
            }

            if (sum.UsableElevationLabels == 0 && sum.TerrainPointsWithZ == 0 && contours.Count - flatContours == 0)
            {
                r.Add(Severity.Error, IssueKind.DataError, "SDP-I046", null, Bs.T("U crtežu nema upotrebljivih podataka o visinama (kote, tačke terena ili izohipse)."),
                    Bs.T("Bez visina se teren i nivelacija ne mogu odrediti; program ih ne izmišlja. Dodajte kote ili geodetski snimak."));
            }

            if (!settings.Datum.IsDefined)
            {
                r.Add(Severity.Warning, IssueKind.DataError, "SDP-I047", null, Bs.T("Apsolutna kota nulte kote objekta (±0,00) nije definisana."),
                    Bs.T("Definišite nultu kotu naredbom SDPDATUM prije dodjele kota."));
            }

            // --- geometry sanity of all mapped curves
            foreach (SourceObject o in objects.Where(o => IsCurve(o) && mapping.RolesOf(o.Layer).Count > 0))
            {
                if (o.NonPlanar)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.GeometricError, "SDP-I050", o.Handle, Bs.T("Objekat na sloju {0} nije u ravni XY.", o.Layer), Bs.T("Projektujte ga u ravan XY (FLATTEN) ili koristite 3D poliliniju.")));
                }
            }

            // --- coordinates
            List<Vec2> allPts = objects.Where(o => mapping.RolesOf(o.Layer).Count > 0).SelectMany(o => o.Vertices.Select(v => new Vec2(v.X * k, v.Y * k))).ToList();
            if (allPts.Count == 0)
            {
                allPts = objects.SelectMany(o => o.Vertices.Select(v => new Vec2(v.X * k, v.Y * k))).ToList(); // nothing mapped yet
            }
            if (allPts.Count > 0)
            {
                sum.CoordinateNote = GeoreferenceSolver.DescribeCoordinateMagnitude(GeometryMath.BoundsOf(allPts));
                bool local = sum.CoordinateNote != null && sum.CoordinateNote.Contains("lokalne");
                if (string.IsNullOrWhiteSpace(settings.Crs))
                {
                    r.Add(local ? Severity.Info : Severity.Warning, IssueKind.DataError, "SDP-I060", null,
                        Bs.T("Koordinatni sistem projekta nije naveden. {0}", sum.CoordinateNote ?? string.Empty),
                        Bs.T("Navedite koordinatni sistem (npr. EPSG:3908) ili georeferencirajte crtež kontrolnim tačkama."));
                }
            }

            r.ElementCount = objects.Count;
            return (r, sum);
        }

        private static bool IsCurve(SourceObject o) => o.Kind == SourceKind.Polyline || o.Kind == SourceKind.Line || o.Kind == SourceKind.Arc || o.Kind == SourceKind.Circle;

        private static double Gap(SourceObject o, double k) => o.Vertices.Count < 2 ? double.MaxValue : (o.Vertices[0].XY - o.Vertices[o.Vertices.Count - 1].XY).Length * k;

        private static List<Vec2> Plan(SourceObject o, double k) =>
            GeometryMath.Tessellate(o.ToRing(), true, 0.01 / k).Select(v => new Vec2(v.X * k, v.Y * k)).ToList();

        private static double Area(List<Vec2> p)
        {
            double a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                a += (p[i].X * p[(i + 1) % p.Count].Y) - (p[(i + 1) % p.Count].X * p[i].Y);
            }

            return a / 2;
        }

        private static void CheckClosedCurves(ValidationReport r, List<SourceObject> curves, string roleName, InputSettings s, double k)
        {
            foreach (SourceObject c in curves)
            {
                if (c.Kind == SourceKind.Line || c.Kind == SourceKind.Arc)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.GeometricError, "SDP-I003", c.Handle,
                        Bs.T("{0} je nacrtana pojedinačnim linijama ili lukovima (sloj {1}).", roleName, c.Layer), Bs.T("Spojite segmente u zatvorenu poliliniju (JOIN ili PEDIT).")));
                    continue;
                }

                if (c.Closed)
                {
                    if (c.Vertices.Count >= 3 && GeometryMath.IsSelfIntersecting(c.ToRing()))
                    {
                        r.Add(new ValidationIssue(Severity.Error, IssueKind.GeometricError, "SDP-I004", c.Handle,
                            Bs.T("{0} (sloj {1}) siječe samu sebe.", roleName, c.Layer), Bs.T("Ispravite poliliniju tako da se njene stranice ne sijeku.")));
                    }

                    continue;
                }

                double gap = Gap(c, k);
                if (gap <= 1e-6)
                {
                    r.Add(new ValidationIssue(Severity.Info, IssueKind.GeometricError, "SDP-I005", c.Handle,
                        Bs.T("{0} (sloj {1}) ima isti početak i kraj, ali nije označena kao zatvorena.", roleName, c.Layer), Bs.T("Uključite svojstvo Closed; program je tretira kao zatvorenu.")));
                }
                else if (gap <= s.CloseGapTolerance)
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.GeometricError, "SDP-I006", c.Handle,
                        Bs.T("{0} (sloj {1}) nije zatvorena; razmak krajeva je {2}.", roleName, c.Layer, Bs.Meters(gap, 3)), Bs.T("Zatvorite poliliniju; program ne spaja krajeve sam.")));
                }
                else
                {
                    r.Add(new ValidationIssue(Severity.Error, IssueKind.GeometricError, "SDP-I007", c.Handle,
                        Bs.T("{0} (sloj {1}) je otvorena polilinija (razmak krajeva {2}).", roleName, c.Layer, Bs.Meters(gap, 2)), Bs.T("Granica mora biti zatvorena polilinija.")));
                }
            }
        }

        private static void ReportDuplicates(ValidationReport r, List<SourceObject> curves, string roleName, double k)
        {
            var seen = new Dictionary<string, SourceObject>(StringComparer.Ordinal);
            foreach (SourceObject c in curves)
            {
                // orientation- and start-independent key of rounded plan vertices (1 cm)
                List<string> pts = c.Vertices.Select(v => Math.Round(v.X * k, 2).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":" + Math.Round(v.Y * k, 2).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).ToList();
                if (pts.Count > 1 && c.Closed && pts[0] == pts[pts.Count - 1])
                {
                    pts.RemoveAt(pts.Count - 1);
                }

                string key = string.Join("|", pts.OrderBy(p => p, StringComparer.Ordinal));
                if (seen.TryGetValue(key, out SourceObject? first))
                {
                    r.Add(new ValidationIssue(Severity.Warning, IssueKind.GeometricError, "SDP-I008", c.Handle,
                        Bs.T("{0} (sloj {1}) je duplikat druge geometrije.", roleName, c.Layer), Bs.T("Obrišite duplikat da se objekat ne bi obradio dvaput.")) { RelatedElementId = first.Handle });
                }
                else
                {
                    seen[key] = c;
                }
            }
        }
    }
}
