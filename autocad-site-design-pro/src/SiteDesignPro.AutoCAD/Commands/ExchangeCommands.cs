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
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Units;
using SiteDesignPro.Core.Validation;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Validation, package export for SketchUp and application of SketchUp changes.</summary>
    public sealed class ExchangeCommands
    {
        private static string OutcomeName(ChangeOutcome o)
        {
            switch (o)
            {
                case ChangeOutcome.Applied: return "primijenjeno";
                case ChangeOutcome.Unchanged: return "bez promjene";
                case ChangeOutcome.Conflict: return "konflikt";
                case ChangeOutcome.Missing: return "nedostaje";
                default: return "neispravno";
            }
        }

        /// <summary>Terrain built in drawing metres → package frame (metres relative to the project origin).</summary>
        private static Core.Terrain.TerrainModel ToPackageFrame(Core.Terrain.TerrainModel t, ProjectSettings s)
        {
            Core.Geometry.Vec3 o = s.OriginMeters;
            var copy = new Core.Terrain.TerrainModel();
            copy.Vertices.AddRange(t.Vertices.Select(v => new Core.Geometry.Vec3(v.X - o.X, v.Y - o.Y, v.Z - o.Z)));
            copy.Triangles.AddRange(t.Triangles);
            copy.Issues.AddRange(t.Issues);
            copy.Breaklines.AddRange(t.Breaklines.Select(b => (IReadOnlyList<Core.Geometry.Vec3>)b.Select(v => new Core.Geometry.Vec3(v.X - o.X, v.Y - o.Y, v.Z - o.Z)).ToList()));
            return copy;
        }

        /// <summary>Validates the drawing; results go to the command line and the palette.</summary>
        internal static ValidationReport ValidateDrawing(CommandContext ctx, out List<ManagedElement> managed)
        {
            managed = ctx.Svc.ReadAll();
            var report = new ValidationReport();
            LengthUnit? ins = UnitConversion.FromInsUnits((int)ctx.Db.Insunits);
            if (ins.HasValue && UnitConversion.Code(ins.Value) != ctx.Settings.DrawingUnit)
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-U001", null,
                    "Jedinice crteža (INSUNITS = " + UnitConversion.Code(ins.Value) + ") razlikuju se od jedinice projekta (" + ctx.Settings.DrawingUnit + ").",
                    "Pokrenite SDPSETTINGS > Jedinice i odaberite jedinicu u kojoj je geometrija stvarno nacrtana.");
            }
            else if (!ins.HasValue)
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-U002", null, "INSUNITS nije postavljen; koristi se jedinica projekta " + ctx.Settings.DrawingUnit + ".", "Postavite jedinicu crteža: SDPSETTINGS > Jedinice.");
            }

            foreach (ManagedElement m in managed)
            {
                if (m.IsCopy)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D002", m.Record.Id, "Kopirani objekat nosi identifikator drugog elementa.", "Pokrenite SDPAUDIT da kopije dobiju nove identifikatore.") { Label = m.Record.Label });
                }

                if (m.Type == null)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D003", m.Record.Id, "Nepodržan tip elementa '" + m.Record.TypeId + "'.", "Instalirajte korisnički katalog koji ga definiše ili promijenite tip elementa.") { Label = m.Record.Label });
                }

                if (m.SymbolTransformNote != null)
                {
                    report.Add(new ValidationIssue(Severity.Warning, IssueKind.GeometricError, "SDP-G021", m.Record.Id, m.SymbolTransformNote, "Vratite mjerilo bloka na 1 (ili ponovo postavite element) i dimenzije mijenjajte naredbom SDPEDIT.") { Label = m.Record.Label });
                }

                if (m.GeometryProblem != null)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.GeometricError, "SDP-G020", m.Record.Id, m.GeometryProblem, "Nacrtajte objekat kao poliliniju u ravni XY (WCS).") { Label = m.Record.Label });
                }
            }

            List<SiteElement> local = managed.Where(m => m.Element != null && !m.IsCopy).Select(m => PackageWriter.ToPackageFrame(m.Element!, ctx.Settings)).ToList();
            ValidationReport v = new ProjectValidator(ctx.Catalog, ctx.Settings.Checks).Validate(local);
            report.Issues.AddRange(v.Issues);
            report.Issues.AddRange(SiteChecks.Run(local, ctx.Catalog, ctx.Settings).Issues);
            foreach (string problem in ctx.Settings.Elevations.Problems())
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-E000", null, problem, "Visinski sistem projekta: SDPDATUM.");
            }
            report.ElementCount = managed.Count;
            return report;
        }

        [CommandMethod("SITEDESIGNPRO", "SDPVALIDATE", CommandFlags.Modal)]
        public void Validate() => CommandContext.Run("SDPVALIDATE", ctx =>
        {
            ValidationReport report = ValidateDrawing(ctx, out List<ManagedElement> managed);
            Print(ctx, report, 40);
            Dictionary<string, string> handles = managed.Where(m => !m.IsCopy).GroupBy(m => m.Record.Id).ToDictionary(g => g.Key, g => g.First().ObjectId.Handle.ToString());
            SitePalette.ShowValidation(report, handles);
        });

        internal static void Print(CommandContext ctx, ValidationReport report, int max)
        {
            ctx.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Provjera ({0} objekata): grešaka {1}, upozorenja {2}, za stručni pregled {3}.",
                report.ElementCount, report.ErrorCount, report.WarningCount, report.ReviewCount));
            foreach (ValidationIssue i in report.Issues.OrderBy(i => i.Severity).Take(max))
            {
                ctx.Write("  [" + ValidationReport.SeverityName(i.Severity).ToUpperInvariant() + "] " + i.Code + " " + (i.Label ?? i.ElementId ?? "crtež") + ": " + i.Message);
            }

            if (report.Issues.Count > max)
            {
                ctx.Write("  ... još " + (report.Issues.Count - max) + " (pogledajte paletu Vanjsko uređenje PRO).");
            }

            ctx.Write(ValidationReport.Disclaimer);
        }

        /// <summary>Validates and writes the exchange package (.sdpkg) for SketchUp.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPEXPORT", CommandFlags.Modal)]
        public void Export() => CommandContext.Run("SDPEXPORT", ctx =>
        {
            // heights typed as m n.v. are made relative first, and the roads redrawn with them
            if (RoadCommands.FixAbsoluteLevels(ctx) > 0)
            {
                RoadCommands.RebuildNow(ctx);
            }

            ValidationReport report = ValidateDrawing(ctx, out List<ManagedElement> managed);
            Print(ctx, report, 15);
            if (report.HasErrors && ctx.Keyword("Postoje greške. Elementi s greškama mogu biti preskočeni ili pojednostavljeni. Ipak izvesti? [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Ne")
            {
                return;
            }

            string defaultName = Path.Combine(Path.GetDirectoryName(ctx.Db.Filename) ?? string.Empty, Path.GetFileNameWithoutExtension(ctx.Db.Filename) + ".sdpkg");
            var o = new PromptSaveFileOptions("\nPaket razmjene")
            {
                Filter = "Paket Vanjsko uređenje PRO (*.sdpkg)|*.sdpkg",
                DialogCaption = "Izvoz za SketchUp",
                InitialFileName = ctx.Settings.LastExportPath.Length > 0 ? ctx.Settings.LastExportPath : defaultName,
            };
            PromptFileNameResult r = ctx.Ed.GetFileNameForSave(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            // revision tracking: bump the revision of every element whose content changed since the last export
            var elements = new List<SiteElement>();
            foreach (ManagedElement m in managed.Where(m => m.Element != null && !m.IsCopy && m.Type != null))
            {
                string hash = ContentHasher.Hash(PackageWriter.ToPackageFrame(m.Element!, ctx.Settings));
                if (m.Record.LastExportHash != hash)
                {
                    if (m.Record.LastExportHash != null)
                    {
                        m.Record.Revision++;
                        m.Element!.Revision = m.Record.Revision;
                    }

                    m.Record.LastExportHash = hash;
                    ElementStore.Write((Entity)ctx.Tr.GetObject(m.ObjectId, OpenMode.ForWrite), m.Record, ctx.Tr);
                }

                elements.Add(m.Element!);
            }

            string host = "AutoCAD " + AcApp.Version;
            var options = new PackageWriteOptions
            {
                GeneratorVersion = AcadEnv.Version,
                Host = host,
                SourceDocument = Path.GetFileName(ctx.Db.Filename),
                Validation = report,
            };
            if (ctx.Settings.TerrainSource.Enabled)
            {
                Core.Terrain.TerrainModel? terrain = TerrainCommands.BuildTerrain(ctx, false);
                if (terrain == null)
                {
                    ctx.Write("Upozorenje: teren nije moguće izgraditi iz trenutnih podataka (SDPTEREN za detalje); paket se izvozi bez terena.");
                }
                else
                {
                    options.Terrains.Add((ctx.Settings.TerrainSource.Id, ctx.Settings.TerrainSource.Name, ToPackageFrame(terrain, ctx.Settings)));
                }
            }

            PackageWriteResult result = PackageWriter.Write(r.StringResult, ctx.Settings, elements, ctx.Catalog, options);
            ctx.Settings.LastExportPath = result.Path;
            ctx.SaveSettings();
            ctx.Write("Izvezeno elemenata: " + result.ElementCount + (options.Terrains.Count > 0 ? " i teren" : string.Empty) + " → " + result.Path + ". Uvoz u SketchUp: Proširenja > Vanjsko uređenje PRO > Uvoz paketa.");
            SitePalette.MarkExported();
            foreach (string s in result.Skipped)
            {
                ctx.Write("  preskočeno: " + s);
            }

            Log.Info("Izvezeno " + result.ElementCount + " elemenata u " + result.Path);
        });

        /// <summary>
        /// Brings a package (.sdpkg) into the drawing as editable elements – e.g. a package converted from another DWG.
        /// In a drawing without elements the package's unit, origin and datum are taken over, so coordinates match the source.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPUVOZPAKETA", CommandFlags.Modal)]
        public void ImportPackage() => CommandContext.RunMajor("SDPUVOZPAKETA", "Uvoz paketa u crtež", ctx =>
        {
            var o = new PromptOpenFileOptions("\nPaket Vanjsko uređenje PRO") { Filter = "Paket Vanjsko uređenje PRO (*.sdpkg)|*.sdpkg", DialogCaption = "Uvoz paketa u crtež" };
            PromptFileNameResult r = ctx.Ed.GetFileNameForOpen(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            PackageReadResult read = PackageReader.Read(r.StringResult);
            if (read.Package == null)
            {
                Print(ctx, read.Report, 10);
                return;
            }

            SitePackage pkg = read.Package;
            List<ManagedElement> existing = ctx.Svc.ReadAll();
            bool adopt = existing.Count == 0;
            if (!adopt && pkg.ProjectId != ctx.Settings.ProjectId &&
                ctx.Keyword("Crtež već sadrži elemente drugog projekta. Dodati elemente paketa u postojeći koordinatni okvir crteža? [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Ne")
            {
                return;
            }

            if (adopt)
            {
                PackageImport.AdoptProjectFrame(pkg, ctx.Settings);
                LengthUnit unit = ctx.Settings.Unit;
                LengthUnit? ins = UnitConversion.FromInsUnits((int)ctx.Db.Insunits);
                if (!ins.HasValue || (ins.Value != unit && ctx.Keyword("INSUNITS crteža (" + (ins.HasValue ? UnitConversion.Code(ins.Value) : "-") + ") razlikuje se od jedinice paketa (" + UnitConversion.Code(unit) + "). Postaviti INSUNITS na " + UnitConversion.Code(unit) + "? [Da/Ne]", new[] { "Da", "Ne" }, "Da") == "Da"))
                {
                    ctx.Db.Insunits = (UnitsValue)UnitConversion.ToInsUnits(unit);
                }
            }

            var byId = existing.Where(m => !m.IsCopy).GroupBy(m => m.Record.Id).ToDictionary(g => g.Key, g => g.First());

            // the carriageways, kerbs, sidewalks, markings, … of the package are left out: AutoCAD draws its own road
            // network from the streets, roads and marks (as if they had been drawn here)
            List<PackageElement> elements = pkg.Elements.Where(pe => !IsGenerated(pe.Element)).ToList();
            int generated = pkg.Elements.Count - elements.Count;
            bool fromSketchUpRoads = pkg.GeneratorHost == RoadEngine.Host;
            bool replace = elements.Any(e => byId.ContainsKey(e.Element.Id)) &&
                ctx.Keyword("Neki elementi paketa već postoje u crtežu [Preskoci/Zamijeni]", new[] { "Preskoci", "Zamijeni" }, "Preskoci") == "Zamijeni";
            ctx.Svc.Layers.EnsureAll(false);
            int created = 0, replaced = 0, skipped = 0, failed = 0, roads = 0;
            foreach (PackageElement pe in elements)
            {
                if (ctx.Catalog.FindType(pe.Element.TypeId) == null)
                {
                    ctx.Write("  preskočeno " + (pe.Element.Label ?? pe.Element.Id) + ": nepoznat tip '" + pe.Element.TypeId + "'");
                    failed++;
                    continue;
                }

                if (byId.TryGetValue(pe.Element.Id, out ManagedElement? old))
                {
                    if (!replace)
                    {
                        skipped++;
                        continue;
                    }

                    foreach ((ObjectId auxId, string _) in ctx.Svc.Aux(old.Record.Id).ToList())
                    {
                        ctx.Tr.GetObject(auxId, OpenMode.ForWrite).Erase();
                    }

                    ctx.Tr.GetObject(old.ObjectId, OpenMode.ForWrite).Erase();
                    replaced++;
                }

                try
                {
                    SiteElement element = pe.Element;
                    element.Custom.Remove(Core.Roads.RoadElements.NetworkFlag);
                    element.Custom.Remove(Core.Roads.RoadElements.DerivedFlag);
                    ctx.Svc.Create(PackageImport.ToDrawingFrame(element, ctx.Settings));
                    created++;
                    if (Core.Roads.RoadElements.SourceTypes.Contains(element.TypeId))
                    {
                        roads++;
                    }
                }
                catch (System.Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is Autodesk.AutoCAD.Runtime.Exception)
                {
                    ctx.Write("  nije uvezeno " + (pe.Element.Label ?? pe.Element.Id) + ": " + ex.Message);
                    failed++;
                    if (old?.Element != null && replace)
                    {
                        // the drawing's element was erased for the replacement: put it back rather than lose it
                        replaced--;
                        try
                        {
                            ctx.Svc.Create(old.Element);
                            ctx.Write("    zadržan je postojeći element " + (old.Element.Label ?? old.Element.Id) + ".");
                        }
                        catch (System.Exception restoreEx) when (restoreEx is InvalidOperationException || restoreEx is ArgumentException || restoreEx is Autodesk.AutoCAD.Runtime.Exception)
                        {
                            ctx.Write("    postojeći element " + (old.Element.Label ?? old.Element.Id) + " nije moguće vratiti (" + restoreEx.Message + "); poništite naredbu s UNDO.");
                        }
                    }
                }
            }

            // roads erased in SketchUp: a package of the SketchUp road tools of this project holds all its roads
            int erased = 0;
            if (fromSketchUpRoads && !adopt && pkg.ProjectId == ctx.Settings.ProjectId)
            {
                var inPackage = new HashSet<string>(pkg.Elements.Select(e => e.Element.Id), StringComparer.Ordinal);
                List<ManagedElement> gone = existing.Where(m => !m.IsCopy && Core.Roads.RoadElements.SourceTypes.Contains(m.Record.TypeId) && !inPackage.Contains(m.Record.Id)).ToList();
                if (gone.Count > 0 &&
                    ctx.Keyword(Core.Text.Bs.T("U crtežu je {0} saobraćajnih elemenata kojih nema u paketu iz SketchUp-a (tamo su obrisani). Obrisati ih i u crtežu? [Da/Ne]", gone.Count), new[] { "Da", "Ne" }, "Ne") == "Da")
                {
                    foreach (ManagedElement m in gone)
                    {
                        foreach ((ObjectId auxId, string _) in ctx.Svc.Aux(m.Record.Id).ToList())
                        {
                            ctx.Tr.GetObject(auxId, OpenMode.ForWrite).Erase();
                        }

                        ctx.Tr.GetObject(m.ObjectId, OpenMode.ForWrite).Erase();
                        erased++;
                    }
                }
            }

            ctx.SaveSettings();
            ctx.Write(Core.Text.Bs.T("Uvoz paketa {0}: kreirano {1} elemenata (zamijenjeno {2}, preskočeno postojećih {3}, neuspjelo {4}).",
                Path.GetFileName(r.StringResult), created, replaced, skipped, failed));
            if (generated > 0)
            {
                ctx.Write(Core.Text.Bs.T("  {0} elemenata mreže (kolovoz, ivičnjaci, trotoari, oznake, …) nije uvezeno: crtež ih crta sam iz ulica i cesta.", generated));
            }

            if (erased > 0)
            {
                ctx.Write(Core.Text.Bs.T("  obrisano {0} saobraćajnih elemenata kojih više nema u SketchUp-u.", erased));
            }

            if (roads > 0 || erased > 0)
            {
                RoadCommands.RebuildNow(ctx);
                ctx.Write("  Mreža saobraćajnica je ponovo nacrtana (kolovozi, ivičnjaci, trotoari, oznake, rampe).");
            }
            if (adopt)
            {
                ctx.Write(Core.Text.Bs.T("Preuzeto iz paketa: jedinica {0}, ishodište E {1} N {2}{3}.", ctx.Settings.DrawingUnit,
                    CommandContext.F(ctx.Settings.OriginMeters.X, 2), CommandContext.F(ctx.Settings.OriginMeters.Y, 2),
                    ctx.Settings.Elevations.BuildingZeroAbsolute.HasValue ? ", ±0,00 = " + CommandContext.F(ctx.Settings.Elevations.BuildingZeroAbsolute.Value, 2) + " m n.v." : string.Empty));
            }

            ctx.Write("Dalje: ZOOM Extents, SDPEDIT za izmjene, SDPVALIDATE, SDPEXPORT.");
            Log.Info("Uvezen paket " + r.StringResult + ": " + created + " elemenata");
        });

        /// <summary>An element the road network or a garage ramp generated (flagged in the package, or of a type only the network makes).</summary>
        internal static bool IsGenerated(SiteElement e) =>
            (e.Custom.Contains(Core.Roads.RoadElements.DerivedFlag) && e.Custom[Core.Roads.RoadElements.DerivedFlag].AsBool()) ||
            Core.Roads.RoadElements.NetworkOnlyTypes.Contains(e.TypeId);

        /// <summary>Applies a SketchUp change file (parameter edits, moved/rotated point elements).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPAPPLYCHANGES", CommandFlags.Modal)]
        public void ApplyChanges() => CommandContext.RunMajor("SDPAPPLYCHANGES", "Primjena izmjena iz SketchUp-a", ctx =>
        {
            var o = new PromptOpenFileOptions("\nDatoteka izmjena iz SketchUp-a") { Filter = "Izmjene Vanjsko uređenje PRO (*.json)|*.json", DialogCaption = "Primjena izmjena iz SketchUp-a" };
            PromptFileNameResult r = ctx.Ed.GetFileNameForOpen(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            ChangeSetDocument doc = ChangeSetReader.Read(r.StringResult);
            if (doc.ProjectId != ctx.Settings.ProjectId &&
                ctx.Keyword("Datoteka izmjena pripada drugom projektu (" + doc.ProjectId + "). Nastaviti? [Da/Ne]", new[] { "Da", "Ne" }, "Ne") == "Ne")
            {
                return;
            }

            bool force = ctx.Keyword("Elementi izmijenjeni u AutoCAD-u nakon izvoza [PREskoci/PREPisi]", new[] { "Preskoci|PREskoci", "Prepisi|PREPisi" }, "Preskoci") == "Prepisi";
            var counts = new Dictionary<ChangeOutcome, int>();
            foreach (ElementChange change in doc.Changes)
            {
                ManagedElement? m = ctx.Svc.FindById(change.Id);
                ChangeOutcome outcome;
                string message;
                if (m?.Element == null)
                {
                    (outcome, message) = (ChangeOutcome.Missing, "Element nije pronađen u ovom crtežu.");
                }
                else
                {
                    (outcome, message) = ChangeSetReader.Apply(change, m.Element, ctx.Settings, ctx.Catalog, force);
                    if (outcome == ChangeOutcome.Applied)
                    {
                        ctx.Svc.Update(m, m.Element);
                    }
                }

                counts[outcome] = counts.TryGetValue(outcome, out int n) ? n + 1 : 1;
                if (outcome != ChangeOutcome.Applied && outcome != ChangeOutcome.Unchanged)
                {
                    ctx.Write("  " + OutcomeName(outcome) + " " + (change.Label ?? change.Id) + ": " + message);
                }
            }

            ctx.Write("Izmjene iz SketchUp-a: " + string.Join(", ", counts.Select(kv => OutcomeName(kv.Key) + " " + kv.Value)) + ". Ponovo izvezite (SDPEXPORT) da SketchUp dobije usklađeno stanje.");
        });
    }
}
