using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.AutoCAD.UI;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geo;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Safety;
using SiteDesignPro.Core.Text;
using SiteDesignPro.Core.Validation;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>DWG input: layer mapping, input validation, taking over mapped geometry, georeferencing and KML.</summary>
    public sealed class InputCommands
    {
        internal static LayerMapping? RequireMapping(CommandContext ctx)
        {
            LayerMapping? m = ctx.Settings.LayerMapping;
            if (m == null || m.Roles.Count == 0)
            {
                ctx.Write("Slojevi crteža još nisu mapirani. Pokrenite SDPMAPIRANJE i dodijelite slojeve ulogama projekta.");
                return null;
            }

            return m;
        }

        internal static List<SourceObject> MappedSources(CommandContext ctx, LayerMapping mapping) =>
            SourceReader.Read(ctx.Db, ctx.Tr, layer => mapping.RolesOf(layer).Count > 0);

        internal static double MetersPerUnit(CommandContext ctx) => ctx.Settings.Scale.MetersPerDrawingUnit;

        internal static Entity? EntityByHandle(CommandContext ctx, string handle, OpenMode mode = OpenMode.ForRead)
        {
            try
            {
                ObjectId id = ctx.Db.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
                return id.IsNull || id.IsErased ? null : ctx.Tr.GetObject(id, mode) as Entity;
            }
            catch (System.Exception ex) when (ex is FormatException || ex is Autodesk.AutoCAD.Runtime.Exception)
            {
                return null;
            }
        }

        /// <summary>Assigns the drawing's layers to project roles (dialog). Layer names are not changed.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPMAPIRANJE", CommandFlags.Modal)]
        public void MapLayers() => CommandContext.Run("SDPMAPIRANJE", ctx =>
        {
            SortedDictionary<string, int> usage = SourceReader.LayerUsage(ctx.Db, ctx.Tr);
            using (var dlg = new LayerMappingDialog(usage, ctx.Settings.LayerMapping))
            {
                if (AcApp.ShowModalDialog(dlg) != System.Windows.Forms.DialogResult.OK)
                {
                    throw new CommandCancelled();
                }

                ctx.Settings.LayerMapping = dlg.Result();
                ctx.Settings.LayerMappingName = dlg.MappingName;
            }

            ctx.SaveSettings();
            foreach (KeyValuePair<SourceRole, List<string>> kv in ctx.Settings.LayerMapping!.Roles.OrderBy(k => k.Key))
            {
                ctx.Write("  " + SourceRoles.Name(kv.Key).PadRight(30) + string.Join(", ", kv.Value));
            }

            ctx.Write("Mapiranje je sačuvano u crtežu. Sljedeći korak: SDPPROVJERADWG (provjera ulaznog crteža).");
        });

        /// <summary>Checks the source drawing against the mapping before anything is built (Bosnian report).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPROVJERADWG", CommandFlags.Modal)]
        public void CheckInput() => CommandContext.Run("SDPPROVJERADWG", ctx =>
        {
            LayerMapping mapping = ctx.Settings.LayerMapping ?? new LayerMapping();
            if (mapping.Roles.Count == 0)
            {
                ctx.Write("Slojevi nisu mapirani; provjera će prijaviti sve obavezne uloge kao nedostajuće. Mapiranje: SDPMAPIRANJE.");
            }

            List<SourceObject> all = SourceReader.Read(ctx.Db, ctx.Tr);
            var input = new InputSettings
            {
                DrawingUnit = ctx.Settings.Unit,
                Crs = ctx.Settings.Crs,
                Datum = ctx.Settings.Elevations,
            };
            (ValidationReport report, InputSummary sum) = InputValidator.Validate(all, mapping, input);
            ctx.Write(Bs.T("Provjera ulaznog crteža: {0} objekata, parcela {1}, objekata {2}, oznaka objekata {3}, oznaka kota {4} (upotrebljivo {5}), tačaka terena s visinom {6}.",
                all.Count, sum.ParcelCount, sum.BuildingCount, sum.MarkerCount, sum.ElevationLabelCount, sum.UsableElevationLabels, sum.TerrainPointsWithZ));
            if (sum.ParcelAreaM2 > 0)
            {
                ctx.Write(Bs.T("Površina parcele: {0}", Bs.SquareMeters(sum.ParcelAreaM2, 2)));
            }

            if (sum.CoordinateNote != null)
            {
                ctx.Write(sum.CoordinateNote);
            }

            if (sum.UnmappedLayers.Count > 0)
            {
                ctx.Write(Bs.T("Slojevi bez uloge ({0}): {1}", sum.UnmappedLayers.Count, string.Join(", ", sum.UnmappedLayers.Take(25)) + (sum.UnmappedLayers.Count > 25 ? " ..." : string.Empty)));
            }

            ExchangeCommands.Print(ctx, report, 40);
            SitePalette.ShowValidation(report, report.Issues.Where(i => i.ElementId != null).Select(i => i.ElementId!).Distinct().ToDictionary(h => h, h => h));
            if (ctx.Keyword("Sačuvati izvještaj u tekstualnu datoteku? [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Da")
            {
                string dir = string.IsNullOrEmpty(ctx.Db.Filename) ? Path.GetTempPath() : Path.GetDirectoryName(ctx.Db.Filename)!;
                string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(ctx.Db.Filename) ? "crtez" : ctx.Db.Filename) + "_provjera_ulaza.txt");
                SafeFile.WriteAllTextAtomic(path, report.ToText());
                ctx.Write("Izvještaj: " + path);
            }
        });

        /// <summary>Creates project elements (parcel, building footprints) from mapped source geometry; originals stay untouched.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPREUZMI", CommandFlags.Modal)]
        public void TakeOver() => CommandContext.RunMajor("SDPPREUZMI", "Preuzimanje mapirane geometrije", ctx =>
        {
            LayerMapping? mapping = RequireMapping(ctx);
            if (mapping == null)
            {
                return;
            }

            List<SourceObject> sources = MappedSources(ctx, mapping);
            List<SiteElement> existing = ctx.ExistingElements();
            var taken = new HashSet<string>(existing.Where(e => !string.IsNullOrEmpty(e.SourceHandle)).Select(e => e.SourceHandle!), StringComparer.OrdinalIgnoreCase);
            int created = 0, skipped = 0, already = 0;
            foreach ((SourceRole role, string typeId) in new[] { (SourceRole.ParcelBoundary, "site.boundary"), (SourceRole.BuildingFootprint, "building.footprint") })
            {
                ElementType type = ctx.Catalog.RequireType(typeId);
                foreach (SourceObject o in sources.Where(s => mapping.RolesOf(s.Layer).Contains(role) && s.Kind == SourceKind.Polyline))
                {
                    if (taken.Contains(o.Handle))
                    {
                        already++;
                        continue;
                    }

                    if (!o.Closed || o.Vertices.Count < 3)
                    {
                        skipped++;
                        ctx.Write(Bs.T("  preskočeno: {0} {1} nije zatvorena polilinija (provjerite SDPPROVJERADWG).", SourceRoles.Name(role), o.Handle));
                        continue;
                    }

                    SiteElement e = ctx.NewElement(type, new PolygonGeometry(new Ring(o.Vertices, o.Bulges)), existing);
                    e.SourceHandle = o.Handle;
                    e.Properties["sourceLayer"] = o.Layer;
                    ctx.Svc.Create(e);
                    existing.Add(e);
                    created++;
                }
            }

            if (mapping.AllowMarkerWorkflow)
            {
                created += TakeOverMarkers(ctx, mapping, sources, taken, existing);
            }

            ctx.Write(Bs.T("Preuzeto: {0} novih elemenata; već preuzeto ranije: {1}; preskočeno: {2}. Izvorna geometrija nije mijenjana.", created, already, skipped));
        });

        private static int TakeOverMarkers(CommandContext ctx, LayerMapping mapping, List<SourceObject> sources, HashSet<string> taken, List<SiteElement> existing)
        {
            ElementType type = ctx.Catalog.RequireType("building.footprint");
            int created = 0;
            foreach (SourceObject o in sources.Where(s => s.Kind == SourceKind.Block && mapping.RolesOf(s.Layer).Contains(SourceRole.BuildingMarker) && !taken.Contains(s.Handle)))
            {
                Entity? e = EntityByHandle(ctx, o.Handle);
                if (!(e is BlockReference br))
                {
                    continue;
                }

                br.Highlight();
                try
                {
                    ctx.Write(Bs.T("Oznaka objekta {0} (blok {1}) – tlocrt se ne pretpostavlja; unesite stvarne dimenzije ili preskočite.", o.Handle, o.BlockName));
                    if (ctx.Keyword("Napraviti tlocrt za ovu oznaku? [Da/Preskoci]", new[] { "Da", "Preskoci" }, "Preskoci") != "Da")
                    {
                        continue;
                    }

                    double w = ctx.Number("Dužina objekta duž ose bloka (m)", 10) * ctx.K;
                    double d = ctx.Number("Širina objekta okomito na osu bloka (m)", 10) * ctx.K;
                    double c = Math.Cos(br.Rotation), s = Math.Sin(br.Rotation);
                    var p = br.Position;
                    var corners = new[] { (-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2) }
                        .Select(q => new Vec3(p.X + (q.Item1 * c) - (q.Item2 * s), p.Y + (q.Item1 * s) + (q.Item2 * c), p.Z)).ToList();
                    SiteElement el = ctx.NewElement(type, new PolygonGeometry(new Ring(corners)), existing);
                    el.SourceHandle = o.Handle;
                    el.Properties["sourceLayer"] = o.Layer;
                    el.Properties["notes"] = "Tlocrt iz oznake objekta; dimenzije unio korisnik.";
                    ctx.Svc.Create(el);
                    existing.Add(el);
                    created++;
                }
                finally
                {
                    br.Unhighlight();
                }
            }

            return created;
        }

        /// <summary>Coordinate system, control points and the drawing → project transformation.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPGEOREF", CommandFlags.Modal)]
        public void Georeference() => CommandContext.Run("SDPGEOREF", ctx =>
        {
            GeoreferenceSettings g = ctx.Settings.Georeference;
            double mpu = MetersPerUnit(ctx);
            GeoreferenceResult? last = null;
            while (true)
            {
                ProjectedCrs? crs = CrsRegistry.Find(g.Crs);
                ctx.Write(Bs.T("Koordinatni sistem: {0} | kontrolnih tačaka: {1} | transformacija: {2}", string.IsNullOrEmpty(g.Crs) ? "nije naveden" : g.Crs + (crs != null ? " – " + crs.Name : " (nije u registru)"),
                    g.ControlPoints.Count, Describe(g.Transform)));
                string k = ctx.Keyword("Georeferenciranje [Sistem/Dodaj/Lista/Obrisi/Izracunaj/Ponisti/Izlaz]", new[] { "Sistem", "Dodaj", "Lista", "Obrisi", "Izracunaj", "Ponisti", "Izlaz" }, "Izlaz");
                switch (k)
                {
                    case "Sistem":
                        ctx.Write("Podržani sistemi za KML: " + CrsRegistry.Supported());
                        g.Crs = ctx.Text("EPSG kod koordinatnog sistema projekta (npr. EPSG:3908)", g.Crs, false).Trim();
                        if (CrsRegistry.Find(g.Crs) == null)
                        {
                            ctx.Write("Upozorenje: sistem nije u registru programa; zapisuje se u projekat, ali KML izvoz neće biti moguć.");
                        }

                        break;
                    case "Dodaj":
                        {
                            var p = ctx.Point("Tačka u crtežu (npr. ugao objekta)")!.Value;
                            string name = ctx.Text("Naziv tačke", "T" + (g.ControlPoints.Count + 1), true);
                            double e = NumberText(ctx, "Koordinata E (y) u sistemu projekta");
                            double n = NumberText(ctx, "Koordinata N (x) u sistemu projekta");
                            g.ControlPoints.RemoveAll(c => c.Name == name);
                            g.ControlPoints.Add(new ControlPoint { Name = name, LocalX = p.X * mpu, LocalY = p.Y * mpu, Easting = e, Northing = n });
                            break;
                        }

                    case "Lista":
                        foreach (ControlPoint c in g.ControlPoints)
                        {
                            Residual? r = last?.Residuals.FirstOrDefault(x => x.Name == c.Name);
                            ctx.Write(Bs.T("  {0}: crtež ({1}; {2}) m → E {3}, N {4}{5}", c.Name, Bs.Num(c.LocalX, 3), Bs.Num(c.LocalY, 3), Bs.Num(c.Easting, 3), Bs.Num(c.Northing, 3),
                                r != null ? " | odstupanje " + Bs.Meters(r.Distance, 3) : string.Empty));
                        }

                        break;
                    case "Obrisi":
                        {
                            string name = ctx.Text("Naziv tačke za brisanje", null, true);
                            ctx.Write(g.ControlPoints.RemoveAll(c => c.Name == name) > 0 ? "Tačka obrisana." : "Tačka nije pronađena.");
                            break;
                        }

                    case "Izracunaj":
                        {
                            bool fitScale = g.ControlPoints.Count >= 2 && ctx.Keyword("Određivanje mjerila [Kruto/Slicnost] (kruto = mjerilo 1)", new[] { "Kruto", "Slicnost" }, "Kruto") == "Slicnost";
                            double? rot = null;
                            if (g.ControlPoints.Count(c => c.Enabled) == 1)
                            {
                                rot = NumberText(ctx, "Jedna tačka ne određuje rotaciju. Unesite poznatu rotaciju crteža u stepenima (suprotno satu)");
                            }

                            last = GeoreferenceSolver.Solve(g.ControlPoints, fitScale, rot, g.ScaleTolerancePpm);
                            foreach (string w in last.Warnings)
                            {
                                ctx.Write("  ! " + w);
                            }

                            if (!last.Usable)
                            {
                                break;
                            }

                            ctx.Write(Bs.T("Rezultat: {0}, srednje odstupanje {1}", Describe(last.Transform), Bs.Meters(last.RmsError, 3)));
                            foreach (Residual r in last.Residuals)
                            {
                                ctx.Write(Bs.T("  {0}: dE {1}, dN {2}", r.Name, Bs.Num(r.DE, 3), Bs.Num(r.DN, 3)));
                            }

                            if (ctx.Keyword("Primijeniti ovu transformaciju na projekat? Geometrija crteža se ne mijenja. [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Da")
                            {
                                g.Transform = last.Transform;
                                g.History.Add(new TransformRecord
                                {
                                    Utc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                                    Description = "SDPGEOREF " + Describe(last.Transform),
                                    Transform = last.Transform,
                                    RmsError = last.RmsError,
                                    ControlPointCount = g.ControlPoints.Count(c => c.Enabled),
                                });
                                ctx.Write("Transformacija je zapisana u projekat (s historijom izmjena).");
                            }

                            break;
                        }

                    case "Ponisti":
                        g.Transform = new PlanTransform();
                        g.History.Add(new TransformRecord { Utc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture), Description = "Poništeno: koordinate crteža = koordinate projekta" });
                        ctx.Write("Transformacija poništena: koordinate crteža se smatraju koordinatama projekta.");
                        break;
                    default:
                        ctx.SaveSettings();
                        return;
                }

                ctx.SaveSettings();
            }
        });

        /// <summary>Writes a KML overlay of the parcel and buildings for checking the position in Google Earth.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKML", CommandFlags.Modal)]
        public void ExportKml() => CommandContext.Run("SDPKML", ctx =>
        {
            GeoreferenceSettings g = ctx.Settings.Georeference;
            ProjectedCrs? crs = CrsRegistry.Find(g.Crs);
            if (crs == null)
            {
                ctx.Write("KML zahtijeva poznat koordinatni sistem projekta (SDPGEOREF > Sistem). Podržani: " + CrsRegistry.Supported());
                return;
            }

            double mpu = MetersPerUnit(ctx);
            var features = new List<(string Name, IReadOnlyList<(double Lon, double Lat)> Ring, bool Closed)>();
            foreach (ManagedElement m in ctx.Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy && m.Element.Geometry is PolygonGeometry))
            {
                string cat = m.Type?.Category ?? string.Empty;
                if (cat != "site" && cat != "buildings" && cat != "surfaces" && cat != "roads")
                {
                    continue;
                }

                var pg = (PolygonGeometry)m.Element!.Geometry;
                List<(double, double)> ring = GeometryMath.Tessellate(pg.Outer, true, 0.05 / mpu).Select(v =>
                {
                    (double e, double n) = g.Transform.Apply(v.X * mpu, v.Y * mpu);
                    return crs.ToWgs84Geographic(e, n);
                }).ToList();
                features.Add(((m.Element.Label ?? string.Empty) + " " + (m.Type?.Name ?? string.Empty), ring, true));
            }

            if (features.Count == 0)
            {
                ctx.Write("Nema površinskih elemenata (parcela, objekti, površine) za KML. Preuzmite geometriju naredbom SDPPREUZMI ili SDPCONVERT.");
                return;
            }

            var o = new PromptSaveFileOptions("\nKML datoteka") { Filter = "Google Earth KML (*.kml)|*.kml", DialogCaption = "Izvoz preklopa za Google Earth" };
            PromptFileNameResult r = ctx.Ed.GetFileNameForSave(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            SafeFile.WriteAllTextAtomic(r.StringResult, KmlWriter.Write(ctx.Settings.Name, features, KmlWriter.AccuracyNote(crs)));
            ctx.Write(Bs.T("KML zapisan: {0} ({1} kontura). {2}", r.StringResult, features.Count, crs.DatumNote));
            if (g.Transform.Kind == TransformKind.Identity && ctx.Settings.Origin[0] == 0 && features.Count > 0)
            {
                ctx.Write("Napomena: koordinate crteža se koriste direktno kao koordinate sistema " + crs.Code + ". Ako je crtež u lokalnim koordinatama, prvo georeferencirajte (SDPGEOREF).");
            }
        });

        internal static double NumberText(CommandContext ctx, string message, double? current = null)
        {
            for (int i = 0; i < 5; i++)
            {
                string t = ctx.Text(message, current.HasValue ? Bs.Num(current.Value, 3) : null, false);
                if (Bs.TryParse(t, out double v))
                {
                    return v;
                }

                ctx.Write("Neispravan broj. Koristite decimalni zarez ili tačku, npr. 518,56.");
            }

            throw new CommandCancelled();
        }

        private static string Describe(PlanTransform t)
        {
            switch (t.Kind)
            {
                case TransformKind.Identity: return "nema (koordinate crteža = koordinate projekta)";
                case TransformKind.TranslationWithGivenRotation: return Bs.T("pomak iz jedne tačke, rotacija {0}° (zadana)", Bs.Num(t.RotationDegrees, 4));
                case TransformKind.Rigid: return Bs.T("kruta: rotacija {0}°, pomak ({1}; {2})", Bs.Num(t.RotationDegrees, 5), Bs.Num(t.Tx, 3), Bs.Num(t.Ty, 3));
                default: return Bs.T("sličnost: mjerilo {0}, rotacija {1}°, pomak ({2}; {3})", Bs.Num(t.Scale, 7), Bs.Num(t.RotationDegrees, 5), Bs.Num(t.Tx, 3), Bs.Num(t.Ty, 3));
            }
        }
    }
}
