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
        /// <summary>Validates the drawing; results go to the command line and the palette.</summary>
        internal static ValidationReport ValidateDrawing(CommandContext ctx, out List<ManagedElement> managed)
        {
            managed = ctx.Svc.ReadAll();
            var report = new ValidationReport();
            LengthUnit? ins = UnitConversion.FromInsUnits((int)ctx.Db.Insunits);
            if (ins.HasValue && UnitConversion.Code(ins.Value) != ctx.Settings.DrawingUnit)
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-U001", null,
                    "Drawing units (INSUNITS = " + UnitConversion.Code(ins.Value) + ") differ from the project unit (" + ctx.Settings.DrawingUnit + ").",
                    "Run SDPSETTINGS > Units and choose the unit the geometry is actually drawn in.");
            }
            else if (!ins.HasValue)
            {
                report.Add(Severity.Warning, IssueKind.DataError, "SDP-U002", null, "INSUNITS is unitless; the project unit " + ctx.Settings.DrawingUnit + " is assumed.", "Set the drawing unit with SDPSETTINGS > Units.");
            }

            foreach (ManagedElement m in managed)
            {
                if (m.IsCopy)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D002", m.Record.Id, "Copied object carries another element's identifier.", "Run SDPAUDIT to give copies new identifiers.") { Label = m.Record.Label });
                }

                if (m.Type == null)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.DataError, "SDP-D003", m.Record.Id, "Unsupported element type '" + m.Record.TypeId + "'.", "Install the user catalog that defines it, or change the element type.") { Label = m.Record.Label });
                }

                if (m.SymbolTransformNote != null)
                {
                    report.Add(new ValidationIssue(Severity.Warning, IssueKind.GeometricError, "SDP-G021", m.Record.Id, m.SymbolTransformNote, "Reset the block scale to 1 (or re-place the element) and change size parameters with SDPEDIT.") { Label = m.Record.Label });
                }

                if (m.GeometryProblem != null)
                {
                    report.Add(new ValidationIssue(Severity.Error, IssueKind.GeometricError, "SDP-G020", m.Record.Id, m.GeometryProblem, "Redraw the object as a polyline in the world XY plane.") { Label = m.Record.Label });
                }
            }

            List<SiteElement> local = managed.Where(m => m.Element != null && !m.IsCopy).Select(m => PackageWriter.ToPackageFrame(m.Element!, ctx.Settings)).ToList();
            ValidationReport v = new ProjectValidator(ctx.Catalog, ctx.Settings.Checks).Validate(local);
            report.Issues.AddRange(v.Issues);
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
            ctx.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Validation of {0} elements: {1} errors, {2} warnings, {3} review items.",
                report.ElementCount, report.ErrorCount, report.WarningCount, report.ReviewCount));
            foreach (ValidationIssue i in report.Issues.OrderBy(i => i.Severity).Take(max))
            {
                ctx.Write("  [" + i.Severity.ToString().ToUpperInvariant() + "] " + i.Code + " " + (i.Label ?? i.ElementId ?? "drawing") + ": " + i.Message);
            }

            if (report.Issues.Count > max)
            {
                ctx.Write("  ... " + (report.Issues.Count - max) + " more (see the Site Design Pro palette).");
            }

            ctx.Write(ValidationReport.Disclaimer);
        }

        /// <summary>Validates and writes the exchange package (.sdpkg) for SketchUp.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPEXPORT", CommandFlags.Modal)]
        public void Export() => CommandContext.Run("SDPEXPORT", ctx =>
        {
            ValidationReport report = ValidateDrawing(ctx, out List<ManagedElement> managed);
            Print(ctx, report, 15);
            if (report.HasErrors && ctx.Keyword("There are validation errors. Elements with errors may be skipped or simplified. Export anyway? [Yes/No]", new[] { "Yes", "No" }, "No") == "No")
            {
                return;
            }

            string defaultName = Path.Combine(Path.GetDirectoryName(ctx.Db.Filename) ?? string.Empty, Path.GetFileNameWithoutExtension(ctx.Db.Filename) + ".sdpkg");
            var o = new PromptSaveFileOptions("\nExchange package")
            {
                Filter = "Site Design Pro package (*.sdpkg)|*.sdpkg",
                DialogCaption = "Export for SketchUp",
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
            PackageWriteResult result = PackageWriter.Write(r.StringResult, ctx.Settings, elements, ctx.Catalog, new PackageWriteOptions
            {
                GeneratorVersion = AcadEnv.Version,
                Host = host,
                SourceDocument = Path.GetFileName(ctx.Db.Filename),
                Validation = report,
            });
            ctx.Settings.LastExportPath = result.Path;
            ctx.SaveSettings();
            ctx.Write("Exported " + result.ElementCount + " elements to " + result.Path + ".");
            foreach (string s in result.Skipped)
            {
                ctx.Write("  skipped: " + s);
            }

            Log.Info("Exported " + result.ElementCount + " elements to " + result.Path);
        });

        /// <summary>Applies a SketchUp change file (parameter edits, moved/rotated point elements).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPAPPLYCHANGES", CommandFlags.Modal)]
        public void ApplyChanges() => CommandContext.Run("SDPAPPLYCHANGES", ctx =>
        {
            var o = new PromptOpenFileOptions("\nSketchUp change file") { Filter = "Site Design Pro changes (*.json)|*.json", DialogCaption = "Apply SketchUp changes" };
            PromptFileNameResult r = ctx.Ed.GetFileNameForOpen(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            ChangeSetDocument doc = ChangeSetReader.Read(r.StringResult);
            if (doc.ProjectId != ctx.Settings.ProjectId &&
                ctx.Keyword("The change file belongs to another project (" + doc.ProjectId + "). Continue? [Yes/No]", new[] { "Yes", "No" }, "No") == "No")
            {
                return;
            }

            bool force = ctx.Keyword("Elements changed in AutoCAD since the export [Skip/Overwrite]", new[] { "Skip", "Overwrite" }, "Skip") == "Overwrite";
            var counts = new Dictionary<ChangeOutcome, int>();
            foreach (ElementChange change in doc.Changes)
            {
                ManagedElement? m = ctx.Svc.FindById(change.Id);
                ChangeOutcome outcome;
                string message;
                if (m?.Element == null)
                {
                    (outcome, message) = (ChangeOutcome.Missing, "Element not found in this drawing.");
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
                    ctx.Write("  " + outcome + " " + (change.Label ?? change.Id) + ": " + message);
                }
            }

            ctx.Write("SketchUp changes: " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)) + ". Re-export to send the merged state to SketchUp.");
        });
    }
}
