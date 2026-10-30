using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Samples;
using SiteDesignPro.Core.Text;
using SiteDesignPro.Core.Units;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Projekat: postavke, standard slojeva, provjera podataka i primjer projekta.</summary>
    public sealed class ProjectCommands
    {
        [CommandMethod("SITEDESIGNPRO", "SDPNEW", CommandFlags.Modal)]
        public void NewProject() => CommandContext.Run("SDPNEW", ctx =>
        {
            ProjectSettings s = ctx.Settings;
            s.Name = ctx.Text("Naziv projekta", s.Name);
            s.Number = ctx.Text("Broj projekta", s.Number.Length > 0 ? s.Number : "-");
            if (s.Number == "-")
            {
                s.Number = string.Empty;
            }

            s.Client = ctx.Text("Investitor", s.Client.Length > 0 ? s.Client : "-").Trim('-');
            s.Location = ctx.Text("Lokacija", s.Location.Length > 0 ? s.Location : "-").Trim('-');
            ChooseUnits(ctx, s);
            Point3d? origin = ctx.Point("Ishodište projekta (u SketchUp-u postaje 0,0,0) <0,0,0>", null, true);
            Point3d o = origin ?? Point3d.Origin;
            s.Origin = new[] { o.X, o.Y, o.Z };
            s.TrueNorthDegrees = ctx.Number("Ugao pravog sjevera, stepeni u smjeru kazaljke od +Y crteža", s.TrueNorthDegrees, true, true);
            s.Labels.ScaleDenominator = ctx.Number("Mjerilo anotacija 1:", s.Labels.ScaleDenominator);
            ctx.SaveSettings();
            (int created, _) = ctx.Svc.Layers.EnsureAll(false);
            ctx.Write(Bs.T("Projekat '{0}' je postavljen ({1}, ishodište {2}; {3}; {4}). Napravljeno slojeva: {5}.", s.Name, s.DrawingUnit,
                CommandContext.F(o.X), CommandContext.F(o.Y), CommandContext.F(o.Z), created));
            ctx.Write("Visine nisu zadane: definišite nultu kotu objekta (SDPDATUM), zatim mapirajte slojeve ulaznog crteža (SDPMAPIRANJE).");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPSETTINGS", CommandFlags.Modal)]
        public void Settings() => CommandContext.Run("SDPSETTINGS", ctx =>
        {
            ProjectSettings s = ctx.Settings;
            while (true)
            {
                ctx.Write(Bs.T("Projekat: {0} {1} | Jedinice: {2} | Ishodište: {3} | Sjever: {4}° | Mjerilo 1:{5} | Tekst {6} mm | Zona '{7}' | Revizija {8}",
                    s.Name, s.Number, s.DrawingUnit, string.Join("; ", s.Origin.Select(v => CommandContext.F(v))), CommandContext.F(s.TrueNorthDegrees, 4),
                    CommandContext.F(s.Labels.ScaleDenominator, 0), CommandContext.F(s.Labels.TextHeightMm, 2), s.Labels.Zone, s.Revision));
                string k = ctx.Keyword("Izmjena [Naziv/Broj/INvestitor/Lokacija/Autor/Revizija/Jedinice/ISHodiste/SJever/Sistem/Mjerilo/Tekst/Zona/NUmeracija/Izlaz]",
                    new[] { "Naziv", "Broj", "Investitor|INvestitor", "Lokacija", "Autor", "Revizija", "Jedinice", "Ishodiste|ISHodiste", "Sjever|SJever", "Sistem", "Mjerilo", "Tekst", "Zona", "Numeracija|NUmeracija", "Izlaz" }, "Izlaz");
                switch (k)
                {
                    case "Naziv": s.Name = ctx.Text("Naziv projekta", s.Name); break;
                    case "Broj": s.Number = ctx.Text("Broj projekta", s.Number); break;
                    case "Investitor": s.Client = ctx.Text("Investitor", s.Client); break;
                    case "Lokacija": s.Location = ctx.Text("Lokacija", s.Location); break;
                    case "Autor": s.Author = ctx.Text("Autor", s.Author); break;
                    case "Revizija": s.Revision = ctx.Text("Revizija", s.Revision); break;
                    case "Jedinice": ChooseUnits(ctx, s); break;
                    case "Ishodiste":
                        {
                            Point3d o = ctx.Point("Ishodište projekta")!.Value;
                            s.Origin = new[] { o.X, o.Y, o.Z };
                            break;
                        }

                    case "Sjever": s.TrueNorthDegrees = ctx.Number("Pravi sjever, stepeni u smjeru kazaljke od +Y", s.TrueNorthDegrees, true, true); break;
                    case "Sistem": s.Crs = ctx.Text("Koordinatni sistem (npr. EPSG:3908); kontrolne tačke: SDPGEOREF", s.Crs); break;
                    case "Mjerilo": s.Labels.ScaleDenominator = ctx.Number("Mjerilo anotacija 1:", s.Labels.ScaleDenominator); break;
                    case "Tekst": s.Labels.TextHeightMm = ctx.Number("Visina teksta na papiru (mm)", s.Labels.TextHeightMm); break;
                    case "Zona": s.Labels.Zone = ctx.Text("Oznaka zone u numeraciji (- za bez)", s.Labels.Zone.Length > 0 ? s.Labels.Zone : "-").Trim('-'); break;
                    case "Numeracija":
                        s.Labels.NumberingScope = ctx.Keyword("Niz brojeva po [Prefiksu/Kategoriji]", new[] { "Prefiksu", "Kategoriji" }, "Prefiksu") == "Prefiksu" ? "prefix" : "category";
                        s.Labels.NumberWidth = ctx.Integer("Broj cifara", s.Labels.NumberWidth);
                        break;
                    default:
                        ctx.SaveSettings();
                        ctx.Write("Postavke su sačuvane. Visine: SDPDATUM, georeferenciranje: SDPGEOREF, sigurnosne kopije: SDPKOPIJEPOSTAVKE.");
                        return;
                }
            }
        });

        private static void ChooseUnits(CommandContext ctx, ProjectSettings s)
        {
            LengthUnit? ins = UnitConversion.FromInsUnits((int)ctx.Db.Insunits);
            string current = ins.HasValue ? UnitConversion.Code(ins.Value) : s.DrawingUnit;
            string k = ctx.Keyword("Jedinica crteža [MIlimetri/Centimetri/MEtri]", new[] { "Milimetri|MIlimetri", "Centimetri", "Metri|MEtri" },
                current == "m" ? "Metri" : current == "cm" ? "Centimetri" : "Milimetri");
            LengthUnit unit = k == "Metri" ? LengthUnit.Meter : k == "Centimetri" ? LengthUnit.Centimeter : LengthUnit.Millimeter;
            if (ctx.ExistingElements().Count > 0 && UnitConversion.Code(unit) != s.DrawingUnit)
            {
                ctx.Write("Upozorenje: crtež već sadrži elemente nacrtane u jedinici " + s.DrawingUnit + ". Promjena jedinice ne mijenja mjerilo geometrije.");
            }

            s.DrawingUnit = UnitConversion.Code(unit);
            ctx.Db.Insunits = (UnitsValue)UnitConversion.ToInsUnits(unit);
        }

        [CommandMethod("SITEDESIGNPRO", "SDPLAYERS", CommandFlags.Modal)]
        public void Layers() => CommandContext.Run("SDPLAYERS", ctx =>
        {
            string k = ctx.Keyword("Standard slojeva [Napravi/POpravi/PRovjeri/Stari]", new[] { "Napravi", "Popravi|POpravi", "Provjeri|PRovjeri", "Stari" }, "Napravi");
            if (k == "Stari")
            {
                List<(string Old, string New)> legacy = ctx.Svc.Layers.LegacyLayers();
                if (legacy.Count == 0)
                {
                    ctx.Write("Nema slojeva starog standarda (verzija 0.1) za preimenovanje.");
                    return;
                }

                legacy.ForEach(l => ctx.Write("  " + l.Old + " → " + l.New));
                ctx.Write("Pažnja: ovi nazivi su možda i slojevi Vašeg izvornog crteža (npr. AIA standard). Preimenujte samo ako ih je napravilo ovo proširenje.");
                if (ctx.Keyword("Preimenovati navedene slojeve? [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Da")
                {
                    ctx.Write(Bs.T("Preimenovano slojeva: {0}.", ctx.Svc.Layers.RenameLegacy(legacy)));
                }

                return;
            }

            if (k == "Provjeri")
            {
                List<string> issues = ctx.Svc.Layers.Audit();
                ctx.Write(issues.Count == 0 ? "Svi standardni slojevi odgovaraju standardu." : Bs.T("Odstupanja slojeva: {0}", issues.Count));
                issues.ForEach(i => ctx.Write("  " + i));
                return;
            }

            (int created, int repaired) = ctx.Svc.Layers.EnsureAll(k == "Popravi");
            ctx.Write(Bs.T("Napravljeno slojeva: {0}, usklađeno sa standardom: {1}. Slojevi ulaznog DWG-a se ne preimenuju.", created, repaired));
        });

        /// <summary>Repairs identifiers (copies, duplicates), refreshes representations and reports problems.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPAUDIT", CommandFlags.Modal)]
        public void Audit() => CommandContext.Run("SDPAUDIT", ctx =>
        {
            List<ManagedElement> all = ctx.Svc.ReadAll();
            int copies = 0, duplicates = 0, unknown = 0, broken = 0, refreshed = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var numbering = new Core.Labels.LabelNumbering(ctx.Settings.Labels, ctx.Catalog);
            List<SiteElement> existing = all.Where(m => m.Element != null && !m.IsCopy).Select(m => m.Element!).ToList();
            foreach (ManagedElement m in all.OrderBy(m => m.IsCopy))
            {
                var entity = (Entity)ctx.Tr.GetObject(m.ObjectId, OpenMode.ForWrite);
                if (m.Type == null)
                {
                    unknown++;
                    ctx.Write("Nepoznat tip '" + m.Record.TypeId + "' na objektu " + entity.Handle + " (instalirajte korisnički katalog koji ga definiše).");
                    continue;
                }

                bool newId = m.IsCopy || !seen.Add(m.Record.Id) || !SiteElement.IsValidId(m.Record.Id);
                if (newId)
                {
                    if (m.IsCopy)
                    {
                        copies++;
                    }
                    else
                    {
                        duplicates++;
                    }

                    m.Record.Id = SiteElement.NewId();
                    seen.Add(m.Record.Id);
                    m.Record.LastExportHash = null;
                    m.Record.Revision = 1;
                    if (m.Element != null)
                    {
                        m.Element.Id = m.Record.Id;
                        m.Element.Label = numbering.Next(m.Type, existing);
                        m.Record.Label = m.Element.Label;
                        existing.Add(m.Element);
                    }
                }

                ElementStore.Write(entity, m.Record, ctx.Tr);
                if (m.Element == null)
                {
                    broken++;
                    ctx.Write("Problem geometrije na " + (m.Record.Label ?? entity.Handle.ToString()) + ": " + m.GeometryProblem);
                    continue;
                }

                ctx.Svc.ApplyRepresentation(entity, m.Element, m.Type);
                refreshed++;
            }

            ctx.Svc.Layers.EnsureAll(false);
            ctx.Write(Bs.T("Provjera: {0} elemenata, novi identifikatori za {1} kopija i {2} duplikata, nepoznatih tipova {3}, problema geometrije {4}, osvježeno prikaza {5}.",
                all.Count, copies, duplicates, unknown, broken, refreshed));
        });

        /// <summary>Builds the sample project: managed elements and/or the synthetic source drawing for the input workflow.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAMPLE", CommandFlags.Modal)]
        public void Sample() => CommandContext.RunMajor("SDPSAMPLE", "Primjer projekta", ctx =>
        {
            string what = ctx.Keyword("Primjer [Elementi/Izvorni/Oba] (Izvorni = ulazni DWG za mapiranje, provjeru, kote i teren)", new[] { "Elementi", "Izvorni", "Oba" }, "Oba");
            if (ctx.ExistingElements().Count > 0 && what != "Izvorni" &&
                ctx.Keyword("Crtež već sadrži elemente Vanjsko uređenje PRO. Ipak dodati primjer? [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Ne")
            {
                return;
            }

            ProjectSettings sample = SampleResidentialComplex.CreateSettings();
            ProjectSettings s = ctx.Settings;
            s.ProjectId = sample.ProjectId;
            s.Name = sample.Name;
            s.Number = sample.Number;
            s.Client = sample.Client;
            s.Location = sample.Location;
            s.Description = sample.Description;
            s.Crs = sample.Crs;
            s.Elevations = sample.Elevations; // the sample's own datum (520,15 m) – stated, never a default of new projects
            s.DrawingUnit = "mm"; // the source drawing and the elements are generated in millimetres
            Vec3 om = SampleResidentialComplex.OriginMeters;
            double k = s.Scale.DrawingUnitsPerMeter;
            s.Origin = new[] { om.X * k, om.Y * k, om.Z * k };
            ctx.Db.Insunits = UnitsValue.Millimeters;
            ctx.Svc.Layers.EnsureAll(false);

            int created = 0, sources = 0;
            if (what != "Izvorni")
            {
                List<SiteElement> local = SampleResidentialComplex.CreateLocal(ctx.Catalog);
                foreach (SiteElement e in local)
                {
                    e.Geometry = e.Geometry.Transform(v => new Vec3((v.X + om.X) * k, (v.Y + om.Y) * k, (v.Z + om.Z) * k));
                    ctx.Svc.Create(e);
                }

                created = local.Count;
            }

            if (what != "Elementi")
            {
                sources = DrawSourceDrawing(ctx);
                s.LayerMapping = SampleSourceDrawing.Mapping();
                s.LayerMappingName = s.LayerMapping.Name;
            }

            ctx.SaveSettings();
            ctx.Write(Bs.T("Primjer: {0} elemenata i {1} objekata izvornog crteža (mm, ishodište E {2} N {3}, ±0,00 = 520,15 m n.v. – vrijednosti primjera).",
                created, sources, CommandContext.F(om.X, 0), CommandContext.F(om.Y, 0)));
            ctx.Write("Dalje: ZOOM Extents, SDPPROVJERADWG, SDPKOTEIZOZNAKA, SDPTEREN, SDPVALIDATE, SDPEXPORT.");
        });

        private static int DrawSourceDrawing(CommandContext ctx)
        {
            var ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            var layerColors = new Dictionary<string, short>
            {
                [SampleSourceDrawing.ParcelLayer] = 1, [SampleSourceDrawing.BuildingLayer] = 5, [SampleSourceDrawing.RoadLayer] = 8,
                [SampleSourceDrawing.ParterreLayer] = 3, [SampleSourceDrawing.LabelLayer] = 2, [SampleSourceDrawing.SurveyLayer] = 6,
                [SampleSourceDrawing.BreaklineLayer] = 30,
            };
            var lt = (LayerTable)ctx.Tr.GetObject(ctx.Db.LayerTableId, OpenMode.ForRead);
            foreach (KeyValuePair<string, short> kv in layerColors)
            {
                if (!lt.Has(kv.Key))
                {
                    lt.UpgradeOpen();
                    var rec = new LayerTableRecord { Name = kv.Key, Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, kv.Value) };
                    lt.Add(rec);
                    ctx.Tr.AddNewlyCreatedDBObject(rec, true);
                }
            }

            double h = ctx.Settings.Labels.ModelTextHeightMeters * ctx.K;
            int n = 0;
            foreach (SourceObject o in SampleSourceDrawing.Create())
            {
                Entity e;
                switch (o.Kind)
                {
                    case SourceKind.Text:
                        e = new DBText { Position = P(o.Vertices[0]), Height = h, TextString = o.Text };
                        break;
                    case SourceKind.Point:
                        e = new DBPoint(P(o.Vertices[0]));
                        break;
                    default:
                        if (o.Vertices.Any(v => Math.Abs(v.Z - o.Vertices[0].Z) > 1e-9))
                        {
                            var p3 = new Polyline3d(Poly3dType.SimplePoly, new Point3dCollection(o.Vertices.Select(P).ToArray()), o.Closed);
                            e = p3;
                        }
                        else
                        {
                            var pl = new Polyline();
                            for (int i = 0; i < o.Vertices.Count; i++)
                            {
                                pl.AddVertexAt(i, new Point2d(o.Vertices[i].X, o.Vertices[i].Y), 0, 0, 0);
                            }

                            pl.Closed = o.Closed;
                            pl.Elevation = o.Vertices[0].Z;
                            e = pl;
                        }

                        break;
                }

                e.SetDatabaseDefaults(ctx.Db);
                e.Layer = o.Layer;
                ms.AppendEntity(e);
                ctx.Tr.AddNewlyCreatedDBObject(e, true);
                n++;
            }

            return n;
        }

        private static Point3d P(Vec3 v) => new Point3d(v.X, v.Y, v.Z);

        [CommandMethod("SITEDESIGNPRO", "SDPRELOAD", CommandFlags.Modal)]
        public void Reload() => CommandContext.Run("SDPRELOAD", ctx =>
        {
            AcadEnv.ReloadCatalog();
            ctx.Write(Bs.T("Katalog ponovo učitan: {0} tipova elemenata (korisnički katalozi: {1}).", AcadEnv.Catalog.Types.Count(), AcadEnv.UserCatalogDirectory));
            foreach (string m in AcadEnv.Catalog.LoadMessages)
            {
                ctx.Write("  " + m);
            }
        });

        [CommandMethod("SITEDESIGNPRO", "SDPCATALOGFOLDER", CommandFlags.Modal)]
        public void CatalogFolder()
        {
            CommandContext.Safe("SDPCATALOGFOLDER", () =>
            {
                string dir = AcadEnv.UserCatalogDirectory;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true });
                AcadEnv.Message("Korisnički katalozi: " + dir + " (dodajte *.json datoteke s \"types\", \"layers\" ili \"materials\", zatim pokrenite SDPRELOAD).");
            });
        }

        [CommandMethod("SITEDESIGNPRO", "SDPABOUT", CommandFlags.Modal)]
        public void About()
        {
            CommandContext.Safe("SDPABOUT", () =>
            {
                AcadEnv.Message("Vanjsko uređenje PRO za AutoCAD " + AcadEnv.Version + " (.NET " + Environment.Version + ") | shema razmjene " + Core.Exchange.PackageFormat.SchemaVersion +
                    " | katalog " + AcadEnv.Catalog.CatalogId + " " + AcadEnv.Catalog.Version + " (" + AcadEnv.Catalog.Types.Count() + " tipova) | dnevnik: " + Log.FilePath);
            });
        }
    }
}
