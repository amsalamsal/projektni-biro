using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Drawing;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Labels;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>Labels, numbering, annotation symbols, legends, title blocks and quantity schedules.</summary>
    public sealed class DocumentationCommands
    {
        [CommandMethod("SITEDESIGNPRO", "SDPLABEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Label() => CommandContext.Run("SDPLABEL", ctx =>
        {
            List<ManagedElement> all = ctx.Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy).ToList();
            List<ManagedElement> sel = ctx.SelectElements("Odaberite elemente za označavanje", true, m => m.Element != null && !m.IsCopy);
            var numbering = new LabelNumbering(ctx.Settings.Labels, ctx.Catalog);
            var elements = all.Select(m => m.Element!).ToList();
            var unlabeled = sel.Where(m => string.IsNullOrEmpty(m.Element!.Label)).ToList();
            foreach (ManagedElement m in unlabeled)
            {
                m.Element!.Label = numbering.Next(m.Type!, elements);
                ctx.Svc.Update(m, m.Element);
            }

            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            foreach (ManagedElement m in sel)
            {
                ann.Label(m, ctx.Catalog);
            }

            ctx.Write("Postavljeno oznaka: " + sel.Count + " (novih brojeva: " + unlabeled.Count + "). Oznake prate prenumeraciju (SDPNUMBER).");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPNUMBER", CommandFlags.Modal)]
        public void Number() => CommandContext.Run("SDPNUMBER", ctx =>
        {
            bool all = ctx.Keyword("Numeracija [Nedostajuce/Sve] (Sve = prenumeracija od sjevera prema jugu, od zapada prema istoku)", new[] { "Missing|Nedostajuce", "All|Sve" }, "Missing") == "All";
            List<ManagedElement> managed = ctx.Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy && m.Type != null).ToList();
            var elements = managed.Select(m => m.Element!).ToList();
            List<SiteElement> changed = new LabelNumbering(ctx.Settings.Labels, ctx.Catalog).Assign(elements, all, 3.0 * ctx.K);
            var byId = managed.ToDictionary(m => m.Element!.Id);
            foreach (SiteElement e in changed)
            {
                ctx.Svc.Update(byId[e.Id], e);
            }

            ctx.Write("Izmijenjeno oznaka: " + changed.Count + ".");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPNORTH", CommandFlags.Modal)]
        public void North() => CommandContext.Run("SDPNORTH", ctx =>
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).NorthArrow(ctx.Point("Položaj strelice sjevera")!.Value));

        [CommandMethod("SITEDESIGNPRO", "SDPSCALEBAR", CommandFlags.Modal)]
        public void ScaleBar() => CommandContext.Run("SDPSCALEBAR", ctx =>
        {
            Point3d at = ctx.Point("Početna tačka razmjernika")!.Value;
            double len = ctx.Number("Dužina (m)", Math.Max(5, Math.Round(ctx.Settings.Labels.ScaleDenominator / 10)));
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).ScaleBar(at, len, ctx.Integer("Segments", 4));
        });

        [CommandMethod("SITEDESIGNPRO", "SDPSPOTLEVEL", CommandFlags.Modal)]
        public void SpotLevel() => CommandContext.Run("SDPSPOTLEVEL", ctx =>
        {
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            while (ctx.Point("Tačka visinske kote (koristi njenu Z) [Enter za kraj]", null, true) is Point3d p)
            {
                string mode = ctx.Keyword("Kota [IzTacke/Unos]", new[] { "FromPoint|IzTacke", "Enter|Unos" }, "FromPoint");
                ann.SpotLevel(p, mode == "Enter" ? ctx.Settings.Elevations.DrawingZFromRelative(InputCommands.NumberText(ctx, "Relativna kota u odnosu na ±0,00 (m)", null)) : (double?)null);
            }
        });

        [CommandMethod("SITEDESIGNPRO", "SDPSLOPE", CommandFlags.Modal)]
        public void Slope() => CommandContext.Run("SDPSLOPE", ctx =>
        {
            Point3d high = ctx.Point("Viša tačka")!.Value;
            Point3d low = ctx.Point("Niža tačka", high)!.Value;
            string mode = ctx.Keyword("Nagib [IzTacaka/Unos]", new[] { "FromPoints|IzTacaka", "Enter|Unos" }, "FromPoints");
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).SlopeArrow(high, low, mode == "Enter" ? ctx.Number("Nagib (%)", 2.0) : (double?)null);
        });

        [CommandMethod("SITEDESIGNPRO", "SDPCOORD", CommandFlags.Modal)]
        public void Coordinate() => CommandContext.Run("SDPCOORD", ctx =>
        {
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            while (ctx.Point("Tačka za koordinate [Enter za kraj]", null, true) is Point3d p)
            {
                ann.Coordinate(p);
            }
        });

        [CommandMethod("SITEDESIGNPRO", "SDPSECTION", CommandFlags.Modal)]
        public void Section() => CommandContext.Run("SDPSECTION", ctx =>
        {
            Point3d a = ctx.Point("Početak linije presjeka")!.Value;
            Point3d b = ctx.Point("Kraj linije presjeka", a)!.Value;
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).SectionMarker(a, b, ctx.Text("Oznaka presjeka", "A", false));
        });

        [CommandMethod("SITEDESIGNPRO", "SDPDETAIL", CommandFlags.Modal)]
        public void Detail() => CommandContext.Run("SDPDETAIL", ctx =>
        {
            Point3d at = ctx.Point("Položaj oznake detalja")!.Value;
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).DetailReference(at, ctx.Text("Broj detalja", "1", false), ctx.Text("List", "D-01", false));
        });

        [CommandMethod("SITEDESIGNPRO", "SDPAREALABEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void AreaLabel() => CommandContext.Run("SDPAREALABEL", ctx =>
        {
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            List<ManagedElement> sel = ctx.SelectElements("Odaberite površinske elemente", true, m => m.Element?.Geometry is PolygonGeometry && !m.IsCopy);
            sel.ForEach(m => ann.AreaLabel(m, ctx.Catalog));
            ctx.Write("Postavljeno oznaka površina: " + sel.Count + ".");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPWIDTHLABEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void WidthLabel() => CommandContext.Run("SDPWIDTHLABEL", ctx =>
        {
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            List<ManagedElement> sel = ctx.SelectElements("Odaberite saobraćajnice, staze ili zidove", true, m => m.Element?.Geometry is PathGeometry && m.Type?.Builder.PolylineWidthParam != null && !m.IsCopy);
            sel.ForEach(ann.WidthLabel);
            ctx.Write("Postavljeno oznaka širine: " + sel.Count + ".");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPLEGEND", CommandFlags.Modal)]
        public void Legend() => CommandContext.Run("SDPLEGEND", ctx =>
        {
            Point3d at = ctx.Point("Gornji lijevi ugao legende")!.Value;
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).Legend(at, ctx.Svc.ReadAll().Where(m => !m.IsCopy), ctx.Catalog);
        });

        [CommandMethod("SITEDESIGNPRO", "SDPTITLEBLOCK", CommandFlags.Modal)]
        public void TitleBlock() => CommandContext.Run("SDPTITLEBLOCK", ctx =>
        {
            Point3d corner = ctx.Point("Donji lijevi ugao lista")!.Value;
            string size = ctx.Keyword("Format lista [A0/A1/A2/A3/A4]", new[] { "A0", "A1", "A2", "A3", "A4" }, "A1");
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).TitleBlock(corner, size, ctx.Text("Naziv crteža", "Situacija vanjskog uređenja"), ctx.Text("Broj lista", "VU-100", false));
        });

        [CommandMethod("SITEDESIGNPRO", "SDPREVCLOUD", CommandFlags.Modal)]
        public void RevisionCloud() => CommandContext.Run("SDPREVCLOUD", ctx =>
        {
            Point3d a = ctx.Point("Prvi ugao")!.Value;
            Point3d b = ctx.Point("Suprotni ugao", a)!.Value;
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).RevisionCloud(a, b, ctx.Text("Napomena revizije", ctx.Settings.Revision + ": izmijenjeno"));
        });

        [CommandMethod("SITEDESIGNPRO", "SDPGRIDLINES", CommandFlags.Modal)]
        public void GridLines() => CommandContext.Run("SDPGRIDLINES", ctx =>
        {
            Point3d a = ctx.Point("Prvi ugao mreže")!.Value;
            Point3d b = ctx.Point("Suprotni ugao", a)!.Value;
            new Annotations(ctx.Db, ctx.Tr, ctx.Svc).GridLines(a, b, ctx.Number("Razmak mreže (m)", 10));
        });

        [CommandMethod("SITEDESIGNPRO", "SDPCLEARANCE", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void Clearance() => CommandContext.Run("SDPCLEARANCE", ctx =>
        {
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            List<ManagedElement> sel = ctx.SelectElements("Odaberite elemente", true, m => m.Element?.Geometry is PointGeometry && !m.IsCopy);
            int n = sel.Sum(ann.Clearances);
            ctx.Write("Nacrtano zona razmaka: " + n + " (sloj " + ctx.Svc.Layers.NameFor("anno_clearance") +
                "). Zone koriste vrijednosti razmaka unesene za svaki element; elementi bez vrijednosti se ne crtaju.");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPLIGHTCOVER", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void LightCoverage() => CommandContext.Run("SDPLIGHTCOVER", ctx =>
        {
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            List<ManagedElement> sel = ctx.SelectElements("Odaberite svjetiljke", true, m => m.Type?.Category == "lighting" && m.Element?.Geometry is PointGeometry && !m.IsCopy);
            double r = ctx.Number("Ilustrativni radijus kada nije zadan (m)", 8);
            int n = sel.Count(m => ann.LightCoverage(m, r));
            ctx.Write("Nacrtano ilustrativnih krugova dometa: " + n + ". To su samo pomoć pri rasporedu, a ne fotometrijski proračun.");
        });

        // ------------------------------------------------------------------ quantities and schedules

        private static List<SiteElement> PackageFrameElements(CommandContext ctx)
            => ctx.Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy && m.Type != null)
                .Select(m => PackageWriter.ToPackageFrame(m.Element!, ctx.Settings)).ToList();

        [CommandMethod("SITEDESIGNPRO", "SDPSCHEDULE", CommandFlags.Modal)]
        public void Schedule() => CommandContext.Run("SDPSCHEDULE", ctx =>
        {
            List<SiteElement> elements = PackageFrameElements(ctx);
            var builder = new ScheduleBuilder(ctx.Catalog);
            List<ScheduleDefinition> defs = builder.Applicable(elements).ToList();
            if (defs.Count == 0)
            {
                ctx.Write("Crtež nema elemenata za tabele.");
                return;
            }

            for (int i = 0; i < defs.Count; i++)
            {
                ctx.Write("  T" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " = " + defs[i].Title);
            }

            string[] keys = defs.Select((d, i) => ToKeyword(d.Id) + "|T" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)).Concat(new[] { "All|Sve" }).ToArray();
            string k = ctx.Keyword("Tabela [T1...T" + defs.Count + "/Sve]", keys, "All");
            Point3d at = ctx.Point("Tačka umetanja tabele (gornji lijevi ugao)")!.Value;
            double h = new Annotations(ctx.Db, ctx.Tr, ctx.Svc).TextHeight;
            foreach (ScheduleDefinition def in defs.Where(d => k == "All" || ToKeyword(d.Id) == k))
            {
                ScheduleTable table = builder.Build(def, elements, ctx.Settings.Labels.LinearPrecision);
                ObjectId id = ScheduleTables.Insert(ctx.Db, ctx.Tr, ctx.Svc, table, at, h);
                var t = (Table)ctx.Tr.GetObject(id, OpenMode.ForRead);
                at = new Point3d(at.X, at.Y - t.Height - (h * 4), at.Z);
                ctx.Write(table.Title + ": redova " + table.Rows.Count + ", " + Bs.Elements(table.ElementCount) + ".");
            }
        });

        private static string ToKeyword(string id) => string.Concat(id.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));

        [CommandMethod("SITEDESIGNPRO", "SDPSCHEDULECSV", CommandFlags.Modal)]
        public void ScheduleCsv() => CommandContext.Run("SDPSCHEDULECSV", ctx =>
        {
            List<SiteElement> elements = PackageFrameElements(ctx);
            var builder = new ScheduleBuilder(ctx.Catalog);
            var o = new PromptSaveFileOptions("\nCSV datoteka tabela (po jedna datoteka za svaku tabelu)") { Filter = "CSV (*.csv)|*.csv", DialogCaption = "Izvoz tabela (Excel)" };
            PromptFileNameResult r = ctx.Ed.GetFileNameForSave(o);
            if (r.Status != PromptStatus.OK)
            {
                throw new CommandCancelled();
            }

            string dir = Path.GetDirectoryName(r.StringResult)!;
            string stem = Path.GetFileNameWithoutExtension(r.StringResult);
            int n = 0;
            foreach (ScheduleDefinition def in builder.Applicable(elements))
            {
                ScheduleTable table = builder.Build(def, elements);
                string file = Path.Combine(dir, stem + "-" + def.Id + ".csv");
                File.WriteAllText(file, table.ToCsv(';'), new UTF8Encoding(true)); // ";" + decimal comma: opens directly in Excel with Bosnian settings
                File.WriteAllText(Path.ChangeExtension(file, ".json"), table.ToJson().ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                n++;
            }

            ctx.Write("Zapisano tabela: " + n + " u " + dir + " (CSV za Excel i JSON; ID elemenata povezuje redove s objektima crteža).");
        });

        [CommandMethod("SITEDESIGNPRO", "SDPQUANTITIES", CommandFlags.Modal)]
        public void Quantities() => CommandContext.Run("SDPQUANTITIES", ctx =>
        {
            List<SiteElement> elements = PackageFrameElements(ctx);
            foreach (IGrouping<string, SiteElement> g in elements.GroupBy(e => e.TypeId).OrderBy(g => g.Key))
            {
                ElementType t = ctx.Catalog.RequireType(g.Key);
                double q = 0, s = 0;
                string? su = null;
                foreach (SiteElement e in g)
                {
                    ElementQuantity eq = QuantityCalculator.For(e, t);
                    q += eq.Value;
                    if (eq.Secondary.HasValue)
                    {
                        s += eq.Secondary.Value;
                        su = eq.SecondaryUnit;
                    }
                }

                ctx.Write("  " + t.Name.PadRight(48) + CommandContext.F(q, 2).PadLeft(10) + " " + ScheduleTable.UnitLabel(t.Builder.Quantity.Unit) + (su != null ? "   (" + CommandContext.F(s, 2) + " " + ScheduleTable.UnitLabel(su) + ")" : string.Empty));
            }

            ctx.Write(Bs.Elements(elements.Count) + ". Količine su izmjerene iz trenutne geometrije crteža; dodatne količine (zapremine, površine iz širine) su izračunate iz zadanih parametara, a označene s ~ su procjene. Nedostajuće debljine ili dimenzije se ne izmišljaju.");
        });
    }
}
