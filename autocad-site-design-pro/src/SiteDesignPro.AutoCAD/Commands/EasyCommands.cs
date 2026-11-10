using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Drawing;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Labels;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// One-step tools that replace long command sequences: a whole roundabout from one
    /// point, and a finished sheet (numbers, labels, legend, tables, north, scale bar, title block) from one click.
    /// </summary>
    public sealed class EasyCommands
    {
        private static SiteElement Make(CommandContext ctx, List<SiteElement> existing, ElementType type, ElementGeometry g, double? width)
        {
            SiteElement e = ctx.NewElement(type, g, existing);
            if (width.HasValue && type.Parameter("width") != null)
            {
                e.Parameters["width"] = ParamValue.Of(width.Value);
            }

            ctx.Svc.Create(e);
            existing.Add(e);
            return e;
        }

        // ------------------------------------------------------------------ roundabout

        /// <summary>A complete roundabout from its centre: ring, island, kerbs, splitter islands, crossings, arrows, signs, arms.</summary>
        /// <summary>
        /// Mini roundabout (up to 25 km/h): green central island (radius 15–30 m), truck apron 1,20 m (AR-B88), a
        /// ring with as many lanes as enter from each arm (dashed lane lines, direction arrows), arms as streets with
        /// a median, splitter islands 10 m long in the straight part interrupted at the pedestrian crossing and the
        /// accessible ramps, hatched median, give-way line and triangles. Everything on the layer Saobraćajnice
        /// (the island on the greenery layer); the ring and the arms join the road network.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKRUZNITOK", CommandFlags.Modal)]
        public void Roundabout() => CommandContext.RunMajor("SDPKRUZNITOK", "Kružni tok (SDPKRUZNITOK)", ctx =>
        {
            Point3d c = ctx.Point("Centar kružnog toka")!.Value;
            double ri = ctx.Number("Poluprečnik srednjeg otoka – zelenilo (m, 15–30)", 15);
            if (ri < 15 || ri > 30)
            {
                ri = Math.Max(15, Math.Min(30, ri));
                ctx.Write("Poluprečnik srednjeg otoka je ograničen na " + CommandContext.F(ri, 2) + " m (mini kružni tok: 15–30 m).");
            }

            double apron = ctx.Number("Širina prijelaznog prstena (m)", 1.2);
            int arms = Math.Min(ctx.Integer("Broj krakova (0–6)", 4, 0), 6);
            double first = arms > 0 ? ctx.Angle("Smjer prvog kraka (klik ili ugao)", c, 0) : 0;
            double armLength = arms > 0 ? ctx.Number("Dužina krakova od vanjske ivice kružnog toka (m)", 40) : 0;

            // cross-section of the arms (and the lanes of the ring): the same window as for a street
            RoadSpec arm = RoadSpec.FromParameterString(UI.Preferences.Current.Default("roadSpec", new RoadSpec().ToParameterString()));
            arm.OneWay = false;
            RoadSpec? chosen = CommandContext.Interactive ? UI.RoadDialog.Ask(arm, "Krakovi kružnog toka", ctx.K >= 99 ? "cm" : "m") : arm;
            if (chosen == null)
            {
                ctx.Write("Kružni tok nije nacrtan.");
                return;
            }

            arm = chosen;
            arm.OneWay = false;
            double median = arms > 0 ? Math.Max(1.8, Math.Min(RoadSpec.MaxMedian, ctx.Number("Širina razdjelnog otoka između smjerova (m, najmanje 1,8)", 2.0))) : 0;
            arm.Median = median;
            arm.Normalize();

            double k = ctx.K;
            List<SiteElement> existing = ctx.ExistingElements();
            Ring Circle(double rMeters) => new Ring(new List<Vec3> { new Vec3(c.X + (rMeters * k), c.Y, 0), new Vec3(c.X - (rMeters * k), c.Y, 0) }, new List<double> { 1.0, 1.0 });
            ElementType T(string id) => ctx.Catalog.RequireType(id);
            int n = 0;
            SiteElement Add(string type, ElementGeometry g, Action<SiteElement>? set = null)
            {
                SiteElement e = ctx.NewElement(T(type), g, existing);
                set?.Invoke(e);
                ctx.Svc.Create(e);
                existing.Add(e);
                n++;
                return e;
            }

            // the ring: as many lanes as enter from an arm, lane width of the arms; kerb 15 cm against the apron
            int lanes = Math.Max(1, arm.LanesLeft);
            double lw = arm.LaneWidth;
            double w = lanes * lw;
            double inner = ri + apron + RoadSpec.KerbWidth;
            double ro = inner + w;
            Add(RoadElements.StreetType, new PathGeometry(Circle(inner + (w / 2)), true), e =>
            {
                var ring = new RoadSpec
                {
                    OneWay = true, LanesRight = lanes, LaneWidth = lw, SidewalkLeft = 0, BikeLeft = 0,
                    SidewalkRight = arm.SidewalkRight, BikeRight = arm.BikeRight, CornerRadius = arm.CornerRadius, Arrows = true, ArrowSpacing = 20,
                };
                foreach (KeyValuePair<string, ParamValue> kv in RoadCommands.Params(ring))
                {
                    e.Parameters[kv.Key] = kv.Value;
                }

                e.Name = "Kružni tok";
            });

            // green island and the mountable apron around it
            Add("landscape.lawn_area", new PolygonGeometry(Circle(ri)));
            if (apron > 0.05)
            {
                Add("road.roundabout_apron", new PolygonGeometry(Circle(ri + apron), new List<Ring> { Circle(ri) }));
            }

            for (int i = 0; i < arms; i++)
            {
                double a = first + (i * 2 * Math.PI / arms);
                double ux = Math.Cos(a), uy = Math.Sin(a);
                Vec3 P(double along, double left) => new Vec3(c.X + (((ux * along) - (uy * left)) * k), c.Y + (((uy * along) + (ux * left)) * k), 0);
                PolygonGeometry Box(double s0, double s1, double y0, double y1) => new PolygonGeometry(new Ring(new List<Vec3> { P(s0, y0), P(s1, y0), P(s1, y1), P(s0, y1) }));

                // the arm: a street with the median, from the ring outwards (it is joined to the ring axis)
                Add(RoadElements.StreetType, new PathGeometry(new Ring(new List<Vec3> { P(ro - 0.5, 0), P(ro + armLength, 0) }), false), e =>
                {
                    foreach (KeyValuePair<string, ParamValue> kv in RoadCommands.Params(arm))
                    {
                        e.Parameters[kv.Key] = kv.Value;
                    }
                });

                // where the straight part starts: the corner arc between the arm edge and the ring ends there
                double R = arm.CornerRadius;
                double hl = arm.Extent(RoadLevel.Carriageway, true), hr = arm.Extent(RoadLevel.Carriageway, false);
                double h = Math.Max(hl, hr);
                double st = Math.Sqrt(Math.Max(0, ((ro + R) * (ro + R)) - ((h + R) * (h + R))));
                st = Math.Max(st, ro + 4);
                double m2 = median / 2;

                // pedestrian crossing over the whole carriageway at the ramps (start of the straight part), 3 m wide
                Add(RoadElements.CrossingType, Box(st, st + 3, -hr, hl));

                // splitter island 10 m long, interrupted at the crossing (the refuge is at road level)
                double iw = Math.Max(0.6, median - 0.3) / 2;
                Add("road.splitter_island", Box(Math.Max(ro + 0.8, st - 3), st, -iw, iw));
                Add("road.splitter_island", Box(st + 3, st + 10, -iw, iw));

                // hatched median between the ring and the island, and a taper behind the island
                double hw = Math.Max(0.2, m2 - 0.6);
                if (st - 3 > ro + 1.5)
                {
                    Add(RoadElements.HatchedAreaType, Box(ro + 0.8, Math.Max(ro + 0.8, st - 3) , -hw, hw));
                }

                Add(RoadElements.HatchedAreaType, new PolygonGeometry(new Ring(new List<Vec3> { P(st + 10, -hw), P(st + 16, 0), P(st + 10, hw) })));

                // give-way line (broken, 30 cm) along the ring edge across the entry lanes (left of the arm axis:
                // right-hand traffic drives towards the ring on that side), and a triangle in every entry lane
                for (double y = m2 + 0.25; y + 0.5 <= hl - 0.1; y += 1.0)
                {
                    double s0 = Math.Sqrt(Math.Max(0, ((ro + 0.4) * (ro + 0.4)) - (y * y)));
                    double s1 = Math.Sqrt(Math.Max(0, ((ro + 0.4) * (ro + 0.4)) - ((y + 0.5) * (y + 0.5))));
                    Add(RoadElements.MarkingType, new PathGeometry(new Ring(new List<Vec3> { P(s0, y), P(s1, y + 0.5) }), false), e => e.Parameters["width"] = ParamValue.Of(0.3));
                }

                for (int l = 0; l < arm.LanesLeft; l++)
                {
                    double yc = m2 + ((l + 0.5) * lw);
                    double sb = Math.Sqrt(Math.Max(0, (ro * ro) - (yc * yc))) + 1.5;
                    double half = Math.Min(0.75, lw * 0.3);
                    Add(RoadElements.MarkingType, new PathGeometry(new Ring(new List<Vec3> { P(sb, yc - half), P(sb, yc + half), P(sb + 3.0, yc) }), true), e => e.Parameters["width"] = ParamValue.Of(0.12));
                }
            }

            RoadCommands.RebuildNow(ctx);
            ctx.Write(Bs.T("Kružni tok: otok R {0} m, prijelazni prsten {1} m, prsten {2} × {3} m (vanjski poluprečnik {4} m), krakova {5}; elemenata: {6}.",
                CommandContext.F(ri, 2), CommandContext.F(apron, 2), lanes, CommandContext.F(lw, 2), CommandContext.F(ro, 2), arms, n));
            ctx.Write("Ulice i ceste koje nacrtate do vanjske ivice kružnog toka same se spoje bez preklapanja.");
        });

        // ------------------------------------------------------------------ finished sheet

        private const string SheetOwner = "sheet";

        private static readonly (string Name, double W, double H)[] Sheets =
        {
            ("A3", 420, 297), ("A2", 594, 420), ("A1", 841, 594), ("A0", 1189, 841),
        };

        /// <summary>
        /// Numbers, labels, area and width labels, legend, quantity tables, north arrow, scale bar and title block in
        /// one step, placed around the plan on the smallest sheet that fits. Running it again replaces the sheet.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPDOKUMENTUJ", CommandFlags.Modal)]
        public void Document() => CommandContext.RunMajor("SDPDOKUMENTUJ", "Dokumentovanje lista (SDPDOKUMENTUJ)", ctx =>
        {
            List<ManagedElement> managed = ctx.Svc.ReadAll().Where(m => m.Element != null && !m.IsCopy && m.Type != null).ToList();
            if (managed.Count == 0)
            {
                ctx.Write("Crtež još nema elemenata. Prvo nacrtajte parcelu, ceste i ostalo.");
                return;
            }

            UI.Preferences prefs = UI.Preferences.Current;
            string title = ctx.Text("Naziv crteža", prefs.Default("sheetTitle", "Situacija vanjskog uređenja"));
            string sheetNo = ctx.Text("Broj lista", prefs.Default("sheetNumber", "VU-01"), false);
            string format = ctx.Keyword("Format lista [Auto/A3/A2/A1/A0]", new[] { "Auto", "A3", "A2", "A1", "A0" }, "Auto");
            prefs.RememberDefault("sheetTitle", title);
            prefs.RememberDefault("sheetNumber", sheetNo);

            // 1. numbers for elements that have none (existing numbers stay stable between runs)
            var elements = managed.Select(m => m.Element!).ToList();
            List<SiteElement> renumbered = new LabelNumbering(ctx.Settings.Labels, ctx.Catalog).Assign(elements, false, 3.0 * ctx.K);
            var byId = managed.ToDictionary(m => m.Element!.Id);
            foreach (SiteElement e in renumbered)
            {
                ctx.Svc.Update(byId[e.Id], e);
            }

            // 2. labels: one leader label per element, area labels for surfaces, widths for roads and paths
            HashSet<ObjectId> beforeLabels = ModelSpaceIds(ctx);
            var ann = new Annotations(ctx.Db, ctx.Tr, ctx.Svc);
            double h = ann.TextHeight;
            foreach (ManagedElement m in managed)
            {
                ann.Label(m, ctx.Catalog);
                if (m.Element!.Geometry is PolygonGeometry)
                {
                    ann.AreaLabel(m, ctx.Catalog);
                }
                else if (m.Element.Geometry is PathGeometry && m.Type!.Builder.PolylineWidthParam == "width")
                {
                    ann.WidthLabel(m);
                }
            }

            // 3. the previous sheet (legend, tables, frame) is replaced
            ctx.Svc.EraseAux(SheetOwner, SheetOwner);
            Box2 plan = Box2.Empty;
            foreach (ManagedElement m in managed)
            {
                plan = plan.Include(Extents(ctx, m.ObjectId));
            }

            foreach (ObjectId id in ModelSpaceIds(ctx).Where(i => !beforeLabels.Contains(i) && !i.IsErased))
            {
                plan = plan.Include(Extents(ctx, id));
            }

            HashSet<ObjectId> before = ModelSpaceIds(ctx);

            // 4. legend right of the plan, tables right of the legend in columns no taller than the plan
            double gap = h * 4;
            var legendAt = new Point3d(plan.MaxX + gap, plan.MaxY, 0);
            ann.Legend(legendAt, managed, ctx.Catalog);
            Box2 legend = Box2.Empty;
            foreach (ObjectId id in ModelSpaceIds(ctx).Where(i => !before.Contains(i)))
            {
                legend = legend.Include(Extents(ctx, id));
            }

            // tables are created first and arranged once the sheet is known
            List<SiteElement> packageFrame = managed.Select(m => PackageWriter.ToPackageFrame(m.Element!, ctx.Settings)).ToList();
            var builder = new ScheduleBuilder(ctx.Catalog);
            var tables = new List<Table>();
            foreach (ScheduleDefinition def in builder.Applicable(packageFrame))
            {
                ScheduleTable table = builder.Build(def, packageFrame, ctx.Settings.Labels.LinearPrecision);
                ObjectId tid = ScheduleTables.Insert(ctx.Db, ctx.Tr, ctx.Svc, table, new Point3d(plan.MaxX, plan.MinY, 0), h);
                tables.Add((Table)ctx.Tr.GetObject(tid, OpenMode.ForWrite));
            }

            double leftRight = legend.IsEmpty ? plan.MaxX : legend.MaxX;
            double leftWidth = leftRight - plan.MinX;
            double leftHeight = Math.Max(plan.Height, legend.IsEmpty ? 0 : legend.Height);
            double extraTop = h * 10, extraBottom = h * 8;

            // 6. smallest sheet whose drawing area holds the plan, the legend and the tables in columns
            double f = ctx.Settings.Labels.ScaleDenominator / 1000.0 * ctx.K;
            (string Name, double W, double H) sheet = Sheets[Sheets.Length - 1];
            List<(double X, double Y)> layout = new List<(double X, double Y)>();
            bool below = false;
            bool fits = false;
            foreach ((string Name, double W, double H) s in Sheets.Where(s => format == "Auto" || s.Name == format))
            {
                (double uw, double uh) = Usable(s.W, s.H);
                sheet = s;

                // a) tables in columns right of the legend
                double columnLimit = Math.Max(leftHeight, (uh * f) - extraTop - extraBottom);
                (List<(double X, double Y)> right, double rw, double rh) = Columns(tables, columnLimit, gap, h * 3);
                if (leftWidth + (tables.Count > 0 ? gap + rw : 0) <= uw * f && Math.Max(leftHeight, rh) + extraTop + extraBottom <= uh * f)
                {
                    layout = right;
                    fits = true;
                    break;
                }

                // b) tables in rows under the plan
                (List<(double X, double Y)> under, double bw, double bh) = Rows(tables, uw * f, gap, h * 3);
                if (Math.Max(leftWidth, bw) <= uw * f && leftHeight + extraTop + extraBottom + bh <= uh * f)
                {
                    layout = under;
                    below = true;
                    fits = true;
                    break;
                }

                layout = right;
            }

            Log.Info(Bs.T("Dokumentuj: plan {0}×{1}, lijevo {2}×{3}, {4} tabela, f={5} → {6}{7}", F(plan.Width), F(plan.Height), F(leftWidth), F(leftHeight), tables.Count, F(f), sheet.Name, below ? " (tabele ispod)" : string.Empty));
            double belowTop = Math.Min(plan.MinY, legend.IsEmpty ? plan.MinY : legend.MinY) - extraBottom;
            for (int i = 0; i < tables.Count; i++)
            {
                tables[i].Position = below
                    ? new Point3d(plan.MinX + layout[i].X, belowTop - layout[i].Y, 0)
                    : new Point3d(leftRight + gap + layout[i].X, plan.MaxY - layout[i].Y, 0);
            }

            // north arrow above the plan's right corner, scale bar under the plan
            ann.NorthArrow(new Point3d(plan.MaxX - (h * 4), plan.MaxY + (h * 7), 0));
            double barLength = NiceLength(plan.Width / ctx.K / 5);
            ann.ScaleBar(new Point3d(plan.MinX, plan.MinY - (h * 5), 0), barLength, 5);

            Box2 content = plan;
            foreach (ObjectId id in ModelSpaceIds(ctx).Where(i => !before.Contains(i)))
            {
                content = content.Include(Extents(ctx, id));
            }

            (double usableW, double usableH) = Usable(sheet.W, sheet.H);
            double cx = (content.MinX + content.MaxX) / 2, cy = (content.MinY + content.MaxY) / 2;
            double paperCx = 12 + (usableW / 2), paperCy = 10 + 56 + 4 + (usableH / 2);
            var corner = new Point3d(cx - (paperCx * f), cy - (paperCy * f), 0);
            ann.TitleBlock(corner, sheet.Name, title, sheetNo);

            // 7. everything placed here belongs to the sheet and is replaced on the next run
            int tagged = 0;
            foreach (ObjectId id in ModelSpaceIds(ctx).Where(i => !before.Contains(i)))
            {
                ElementStore.WriteAux((Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite), SheetOwner, SheetOwner, ctx.Tr);
                tagged++;
            }

            ctx.Write(Bs.T("List {0} u mjerilu 1:{1}: {2} elemenata označeno, legenda, {3} tabela, sjever, razmjernik i sastavnica.",
                sheet.Name, CommandContext.F(ctx.Settings.Labels.ScaleDenominator, 0), managed.Count, tables.Count));
            if (renumbered.Count > 0)
            {
                ctx.Write("Novih brojeva: " + renumbered.Count + ". Ponovno pokretanje zamjenjuje list i oznake bez dupliranja.");
            }

            if (!fits)
            {
                ctx.Write(Bs.T("Pažnja: sadržaj je veći od lista {0} u mjerilu 1:{1}. Povećajte mjerilo (Postavke → Mjerilo, npr. 1:{2}) i ponovite.",
                    sheet.Name, CommandContext.F(ctx.Settings.Labels.ScaleDenominator, 0), CommandContext.F(ctx.Settings.Labels.ScaleDenominator * 2, 0)));
            }

            ctx.Ed.Regen();
        });

        private static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Tables in columns no taller than the limit: offsets from the top-left corner and the total size.</summary>
        private static (List<(double X, double Y)> Positions, double Width, double Height) Columns(List<Table> tables, double limit, double gap, double rowGap)
        {
            var pos = new List<(double X, double Y)>();
            double x = 0, y = 0, colW = 0, maxH = 0;
            foreach (Table t in tables)
            {
                if (y > 0 && y + t.Height > limit)
                {
                    x += colW + gap;
                    y = 0;
                    colW = 0;
                }

                pos.Add((x, y));
                colW = Math.Max(colW, t.Width);
                y += t.Height + rowGap;
                maxH = Math.Max(maxH, y - rowGap);
            }

            return (pos, tables.Count > 0 ? x + colW : 0, maxH);
        }

        /// <summary>Tables left to right in rows no wider than the limit: offsets from the top-left corner and the total size.</summary>
        private static (List<(double X, double Y)> Positions, double Width, double Height) Rows(List<Table> tables, double limit, double gap, double rowGap)
        {
            var pos = new List<(double X, double Y)>();
            double x = 0, y = 0, rowH = 0, maxW = 0;
            foreach (Table t in tables)
            {
                if (x > 0 && x + t.Width > limit)
                {
                    y += rowH + rowGap;
                    x = 0;
                    rowH = 0;
                }

                pos.Add((x, y));
                rowH = Math.Max(rowH, t.Height);
                maxW = Math.Max(maxW, x + t.Width);
                x += t.Width + gap;
            }

            return (pos, maxW, tables.Count > 0 ? y + rowH : 0);
        }

        /// <summary>Paper area (mm) for the drawing: inside the 10 mm border, above the 56 mm title block.</summary>
        private static (double W, double H) Usable(double w, double h) => (w - 24, h - 20 - 56 - 6);

        private static double NiceLength(double approx)
        {
            if (!(approx > 0))
            {
                return 10;
            }

            double p = Math.Pow(10, Math.Floor(Math.Log10(approx)));
            foreach (double m in new[] { 1.0, 2.0, 5.0, 10.0 })
            {
                if (m * p >= approx * 0.8)
                {
                    return m * p;
                }
            }

            return 10 * p;
        }

        private static HashSet<ObjectId> ModelSpaceIds(CommandContext ctx)
        {
            var set = new HashSet<ObjectId>();
            foreach (ObjectId id in ctx.Svc.ModelSpace(OpenMode.ForRead))
            {
                set.Add(id);
            }

            return set;
        }

        private static Box2 Extents(CommandContext ctx, ObjectId id)
        {
            try
            {
                if (ctx.Tr.GetObject(id, OpenMode.ForRead) is Entity e && e.Bounds.HasValue)
                {
                    Extents3d x = e.Bounds.Value;
                    return new Box2(x.MinPoint.X, x.MinPoint.Y, x.MaxPoint.X, x.MaxPoint.Y);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // entities without extents (empty text) do not count
            }

            return Box2.Empty;
        }

        // ------------------------------------------------------------------ first steps

        /// <summary>Opens the "Prvi koraci" checklist in the palette.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPRVIKORACI", CommandFlags.Modal)]
        public void FirstSteps() => CommandContext.Safe("SDPPRVIKORACI", UI.SitePalette.ShowFirstSteps);
    }
}
