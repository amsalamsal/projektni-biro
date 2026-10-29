using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Drawing;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Samples;
using SiteDesignPro.Core.Units;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Project set-up, settings, layer standard, audit and the sample project.</summary>
    public sealed class ProjectCommands
    {
        [CommandMethod("SITEDESIGNPRO", "SDPNEW", CommandFlags.Modal)]
        public void NewProject() => CommandContext.Run("SDPNEW", ctx =>
        {
            ProjectSettings s = ctx.Settings;
            s.Name = ctx.Text("Project name", s.Name);
            s.Number = ctx.Text("Project number", s.Number.Length > 0 ? s.Number : "-");
            if (s.Number == "-")
            {
                s.Number = string.Empty;
            }

            s.Client = ctx.Text("Client", s.Client.Length > 0 ? s.Client : "-").Trim('-');
            s.Location = ctx.Text("Location", s.Location.Length > 0 ? s.Location : "-").Trim('-');
            ChooseUnits(ctx, s);
            Point3d? origin = ctx.Point("Project origin (maps to SketchUp 0,0,0) <0,0,0>", null, true);
            Point3d o = origin ?? Point3d.Origin;
            s.Origin = new[] { o.X, o.Y, o.Z };
            s.TrueNorthDegrees = ctx.Number("True north angle, degrees clockwise from drawing +Y", s.TrueNorthDegrees, true, true);
            s.Labels.ScaleDenominator = ctx.Number("Annotation scale 1:", s.Labels.ScaleDenominator);
            ctx.SaveSettings();
            (int created, _) = ctx.Svc.Layers.EnsureAll(false);
            ctx.Write("Site Design Pro project '" + s.Name + "' set up (" + s.DrawingUnit + ", origin " + CommandContext.F(o.X) + "," + CommandContext.F(o.Y) + "," + CommandContext.F(o.Z) + "). " + created + " layers created.");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPSETTINGS", CommandFlags.Modal)]
        public void Settings() => CommandContext.Run("SDPSETTINGS", ctx =>
        {
            ProjectSettings s = ctx.Settings;
            while (true)
            {
                ctx.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Project: {0} {1} | Units: {2} | Origin: {3} | North: {4} deg | Scale 1:{5} | Text {6} mm | Zone '{7}' | Revision {8}",
                    s.Name, s.Number, s.DrawingUnit, string.Join(",", s.Origin.Select(v => CommandContext.F(v))), s.TrueNorthDegrees,
                    s.Labels.ScaleDenominator, s.Labels.TextHeightMm, s.Labels.Zone, s.Revision));
                string k = ctx.Keyword("Change [Name/Number/Client/Location/Author/Revision/Units/Origin/North/Crs/Scale/TextHeight/Zone/Numbering/eXit]",
                    new[] { "Name", "Number", "Client", "Location", "Author", "Revision", "Units", "Origin", "North", "Crs", "Scale", "TextHeight", "Zone", "Numbering", "eXit" }, "eXit");
                switch (k)
                {
                    case "Name": s.Name = ctx.Text("Project name", s.Name); break;
                    case "Number": s.Number = ctx.Text("Project number", s.Number); break;
                    case "Client": s.Client = ctx.Text("Client", s.Client); break;
                    case "Location": s.Location = ctx.Text("Location", s.Location); break;
                    case "Author": s.Author = ctx.Text("Author", s.Author); break;
                    case "Revision": s.Revision = ctx.Text("Revision", s.Revision); break;
                    case "Units": ChooseUnits(ctx, s); break;
                    case "Origin":
                        {
                            Point3d o = ctx.Point("Project origin")!.Value;
                            s.Origin = new[] { o.X, o.Y, o.Z };
                            break;
                        }

                    case "North": s.TrueNorthDegrees = ctx.Number("True north, degrees clockwise from +Y", s.TrueNorthDegrees, true, true); break;
                    case "Crs": s.Crs = ctx.Text("Coordinate reference system (e.g. EPSG:31276)", s.Crs); break;
                    case "Scale": s.Labels.ScaleDenominator = ctx.Number("Annotation scale 1:", s.Labels.ScaleDenominator); break;
                    case "TextHeight": s.Labels.TextHeightMm = ctx.Number("Plotted text height (mm)", s.Labels.TextHeightMm); break;
                    case "Zone": s.Labels.Zone = ctx.Text("Numbering zone code (- for none)", s.Labels.Zone.Length > 0 ? s.Labels.Zone : "-").Trim('-'); break;
                    case "Numbering":
                        s.Labels.NumberingScope = ctx.Keyword("Number sequences per [Prefix/Category]", new[] { "Prefix", "Category" }, "Prefix").ToLowerInvariant();
                        s.Labels.NumberWidth = ctx.Integer("Digits", s.Labels.NumberWidth);
                        break;
                    default:
                        ctx.SaveSettings();
                        ctx.Write("Settings saved.");
                        return;
                }
            }
        });

        private static void ChooseUnits(CommandContext ctx, ProjectSettings s)
        {
            LengthUnit? ins = UnitConversion.FromInsUnits((int)ctx.Db.Insunits);
            string current = ins.HasValue ? UnitConversion.Code(ins.Value) : s.DrawingUnit;
            string k = ctx.Keyword("Drawing unit [Millimeters/Centimeters/Meters]", new[] { "Millimeters", "Centimeters", "Meters" },
                current == "m" ? "Meters" : current == "cm" ? "Centimeters" : "Millimeters");
            LengthUnit unit = k == "Meters" ? LengthUnit.Meter : k == "Centimeters" ? LengthUnit.Centimeter : LengthUnit.Millimeter;
            if (ctx.ExistingElements().Count > 0 && UnitConversion.Code(unit) != s.DrawingUnit)
            {
                ctx.Write("Warning: the drawing already contains elements drawn in " + s.DrawingUnit + ". Changing the unit does not rescale geometry.");
            }

            s.DrawingUnit = UnitConversion.Code(unit);
            ctx.Db.Insunits = (UnitsValue)UnitConversion.ToInsUnits(unit);
        }

        [CommandMethod("SITEDESIGNPRO", "SDPLAYERS", CommandFlags.Modal)]
        public void Layers() => CommandContext.Run("SDPLAYERS", ctx =>
        {
            string k = ctx.Keyword("Layer standard [Create/Repair/Audit]", new[] { "Create", "Repair", "Audit" }, "Create");
            if (k == "Audit")
            {
                List<string> issues = ctx.Svc.Layers.Audit();
                ctx.Write(issues.Count == 0 ? "All standard layers match the standard." : issues.Count + " layer deviations:");
                issues.ForEach(i => ctx.Write("  " + i));
                return;
            }

            (int created, int repaired) = ctx.Svc.Layers.EnsureAll(k == "Repair");
            ctx.Write(created + " layers created, " + repaired + " repaired to the standard.");
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
                    ctx.Write("Unknown type '" + m.Record.TypeId + "' on " + entity.Handle + " (install the user catalog that defines it).");
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
                    ctx.Write("Geometry problem on " + (m.Record.Label ?? entity.Handle.ToString()) + ": " + m.GeometryProblem);
                    continue;
                }

                ctx.Svc.ApplyRepresentation(entity, m.Element, m.Type);
                refreshed++;
            }

            ctx.Svc.Layers.EnsureAll(false);
            ctx.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Audit: {0} elements, {1} copies and {2} duplicate ids given new ids, {3} unknown types, {4} geometry problems, {5} representations refreshed.",
                all.Count, copies, duplicates, unknown, broken, refreshed));
        });

        /// <summary>Builds the sample residential complex in the current drawing.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAMPLE", CommandFlags.Modal)]
        public void Sample() => CommandContext.Run("SDPSAMPLE", ctx =>
        {
            if (ctx.ExistingElements().Count > 0 && ctx.Keyword("The drawing already contains Site Design Pro elements. Add the sample anyway? [Yes/No]", new[] { "Yes", "No" }, "No") == "No")
            {
                return;
            }

            ProjectSettings sample = SampleResidentialComplex.CreateSettings();
            LengthUnit unit = UnitConversion.FromInsUnits((int)ctx.Db.Insunits) ?? LengthUnit.Millimeter;
            if (unit != LengthUnit.Millimeter && unit != LengthUnit.Meter && unit != LengthUnit.Centimeter)
            {
                unit = LengthUnit.Millimeter;
            }

            ProjectSettings s = ctx.Settings;
            s.ProjectId = sample.ProjectId;
            s.Name = sample.Name;
            s.Number = sample.Number;
            s.Client = sample.Client;
            s.Location = sample.Location;
            s.Description = sample.Description;
            s.Crs = sample.Crs;
            s.DrawingUnit = UnitConversion.Code(unit);
            double k = s.Scale.DrawingUnitsPerMeter;
            Vec3 om = SampleResidentialComplex.OriginMeters;
            s.Origin = new[] { om.X * k, om.Y * k, om.Z * k };
            ctx.Db.Insunits = (UnitsValue)UnitConversion.ToInsUnits(unit);
            ctx.SaveSettings();
            ctx.Svc.Layers.EnsureAll(false);
            List<SiteElement> local = SampleResidentialComplex.CreateLocal(ctx.Catalog);
            foreach (SiteElement e in local)
            {
                e.Geometry = e.Geometry.Transform(v => new Vec3((v.X + om.X) * k, (v.Y + om.Y) * k, (v.Z + om.Z) * k));
                ctx.Svc.Create(e);
            }

            ctx.Write(local.Count + " sample elements created (" + s.DrawingUnit + ", origin E " + CommandContext.F(om.X) + " N " + CommandContext.F(om.Y) + "). Use ZOOM Extents, then SDPEXPORT.");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPRELOAD", CommandFlags.Modal)]
        public void Reload() => CommandContext.Run("SDPRELOAD", ctx =>
        {
            AcadEnv.ReloadCatalog();
            ctx.Write("Catalog reloaded: " + AcadEnv.Catalog.Types.Count() + " element types (user catalogs: " + AcadEnv.UserCatalogDirectory + ").");
            foreach (string m in AcadEnv.Catalog.LoadMessages)
            {
                ctx.Write("  " + m);
            }
        });

        [CommandMethod("SITEDESIGNPRO", "SDPCATALOGFOLDER", CommandFlags.Modal)]
        public void CatalogFolder()
        {
            string dir = AcadEnv.UserCatalogDirectory;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true });
            AcadEnv.Message("User catalogs: " + dir + " (add *.json files with \"types\", \"layers\" or \"materials\", then run SDPRELOAD).");
        }

        [CommandMethod("SITEDESIGNPRO", "SDPABOUT", CommandFlags.Modal)]
        public void About()
        {
            AcadEnv.Message("Site Design Pro for AutoCAD " + AcadEnv.Version + " | exchange schema " + Core.Exchange.PackageFormat.SchemaVersion +
                " | catalog " + AcadEnv.Catalog.CatalogId + " " + AcadEnv.Catalog.Version + " (" + AcadEnv.Catalog.Types.Count() + " types) | log: " + Log.FilePath);
        }
    }
}
