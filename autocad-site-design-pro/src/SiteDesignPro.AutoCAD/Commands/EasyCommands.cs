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
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// One-step tools that replace long command sequences: a whole street from one axis, a whole roundabout from one
    /// point, and a finished sheet (numbers, labels, legend, tables, north, scale bar, title block) from one click.
    /// </summary>
    public sealed class EasyCommands
    {
        // ------------------------------------------------------------------ street profile

        private static readonly StreetProfile[] Profiles =
        {
            new StreetProfile("Residential|STambena", "Stambena ulica", "kolovoz 5,5 m, ivičnjaci, trotoari 1,6 m, ulična rasvjeta s lijeve strane", 5.5,
                new StreetPart(StreetPartKind.Axis, "road.access_road", 0, 5.5, false),
                new StreetPart(StreetPartKind.Line, "edge.kerb", 2.75),
                new StreetPart(StreetPartKind.Band, "surface.concrete_paving", 2.9, 1.6),
                new StreetPart(StreetPartKind.Row, "lighting.street_light", 3.3, 0, false, 25)),
            new StreetProfile("Collector|SAbirna", "Sabirna ulica s biciklističkim stazama", "kolovoz 7 m, ivičnjaci, rasvjeta, biciklističke staze 2,5 m, trotoari 2 m, drvored", 7.0,
                new StreetPart(StreetPartKind.Axis, "road.access_road", 0, 7.0, false),
                new StreetPart(StreetPartKind.Line, "edge.kerb", 3.5),
                new StreetPart(StreetPartKind.Row, "lighting.street_light", 4.0, 0, true, 30),
                new StreetPart(StreetPartKind.Line, "cycling.bike_path", 5.75, 2.5),
                new StreetPart(StreetPartKind.Line, "cycling.separation_line", 7.05, 0.1),
                new StreetPart(StreetPartKind.Band, "surface.concrete_paving", 7.1, 2.0),
                new StreetPart(StreetPartKind.Row, "landscape.tree_deciduous", 10.0, 0, true, 12)),
            new StreetProfile("Boulevard|Bulevar", "Bulevar s drvoredom", "kolovoz 7 m, zeleni pojas s drvoredom 2,4 m, biciklističke staze 2,5 m, trotoari 2,5 m", 7.0,
                new StreetPart(StreetPartKind.Axis, "road.access_road", 0, 7.0, false),
                new StreetPart(StreetPartKind.Line, "edge.kerb", 3.5),
                new StreetPart(StreetPartKind.Band, "landscape.lawn_area", 3.6, 2.4),
                new StreetPart(StreetPartKind.Row, "landscape.tree_deciduous", 4.8, 0, true, 10),
                new StreetPart(StreetPartKind.Row, "lighting.street_light", 4.8, 0, true, 30),
                new StreetPart(StreetPartKind.Line, "cycling.bike_path", 7.25, 2.5),
                new StreetPart(StreetPartKind.Line, "cycling.separation_line", 8.55, 0.1),
                new StreetPart(StreetPartKind.Band, "surface.concrete_paving", 8.6, 2.5)),
            new StreetProfile("Path|PJesacka", "Pješačko-biciklistička staza", "staza 3 m, pješačka rasvjeta lijevo, klupe desno", 3.0,
                new StreetPart(StreetPartKind.Axis, "cycling.shared_path", 0, 3.0, false),
                new StreetPart(StreetPartKind.Row, "lighting.pedestrian_light", 2.0, 0, false, 20),
                new StreetPart(StreetPartKind.Row, "furniture.bench", -2.4, 0, false, 40)),
            new StreetProfile("Driveway|PRilaz", "Kolni prilaz", "prilaz 3,5 m s ivičnjacima", 3.5,
                new StreetPart(StreetPartKind.Axis, "road.driveway", 0, 3.5, false),
                new StreetPart(StreetPartKind.Line, "edge.kerb", 1.75)),
        };

        /// <summary>A whole street cross-section (carriageway, kerbs, bike paths, sidewalks, trees, lights) along one axis.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPULICA", CommandFlags.Modal)]
        public void Street() => CommandContext.RunMajor("SDPULICA", "Ulica po profilu (SDPULICA)", ctx =>
        {
            foreach (StreetProfile p in Profiles)
            {
                ctx.Write("  " + p.Key.Split('|')[1].PadRight(11) + p.Name + ": " + p.Description + ".");
            }

            string key = ctx.Keyword("Profil ulice [STambena/SAbirna/Bulevar/PJesacka/PRilaz]", Profiles.Select(p => p.Key).ToArray(), "Collector");
            StreetProfile profile = Profiles.First(p => p.Key.StartsWith(key + "|", StringComparison.Ordinal));
            StreetPart axisPart = profile.Parts.First(p => p.Kind == StreetPartKind.Axis);
            double width = ctx.Number("Širina " + (axisPart.Type.StartsWith("road.", StringComparison.Ordinal) ? "kolovoza" : "staze") + " (m)", profile.AxisWidth);
            double shift = (width - profile.AxisWidth) / 2;

            string how = ctx.Keyword("Osovina ulice [Nacrtaj/Odaberi]", new[] { "Draw|Nacrtaj", "Select|Odaberi" }, "Draw");
            ObjectId axisId;
            if (how == "Draw")
            {
                ctx.Write("Nacrtajte osovinu (sredinu kolovoza) od početka do kraja ulice.");
                axisId = PlacementCommands.DrawPolyline(ctx, false);
                if (axisId.IsNull)
                {
                    ctx.Write("Osovina nije nacrtana.");
                    return;
                }
            }
            else
            {
                axisId = ctx.Entity("Odaberite osovinu (polilinija, linija ili luk)", typeof(Curve));
            }

            var axis = (Curve)ctx.Tr.GetObject(axisId, OpenMode.ForWrite);
            var existing = ctx.ExistingElements();
            ElementType axisType = ctx.Catalog.RequireType(axisPart.Type);
            if (ElementStore.IsManaged(axis))
            {
                ctx.Write("Odabrana osovina je već element; dodaju se samo ostali dijelovi profila.");
            }
            else
            {
                PlacementCommands.ConvertCurves(ctx, axisType, new[] { axisId }, new Dictionary<string, ParamValue> { ["width"] = ParamValue.Of(width) });
                existing = ctx.ExistingElements();
            }

            (Ring axisRing, bool closed) = ElementService.ReadCurve(axis);
            double k = ctx.K;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal) { [axisType.Name] = 1 };
            var rows = new Dictionary<long, List<Placement>>();
            foreach (StreetPart part in profile.Parts.Where(p => p.Kind != StreetPartKind.Axis))
            {
                foreach (int side in part.Both ? new[] { 1, -1 } : new[] { 1 })
                {
                    double offset = side * (part.Offset + (Math.Sign(part.Offset) * shift));
                    ElementType type = ctx.Catalog.RequireType(part.Type);
                    int made = 0;
                    switch (part.Kind)
                    {
                        case StreetPartKind.Line:
                            {
                                Ring? r = OffsetRing(axis, offset * k);
                                if (r != null)
                                {
                                    Make(ctx, existing, type, new PathGeometry(r, closed), part.Size > 0 && type.Builder.PolylineWidthParam == "width" ? part.Size : (double?)null);
                                    made = 1;
                                }

                                break;
                            }

                        case StreetPartKind.Band:
                            {
                                double outer = offset + (Math.Sign(offset) * part.Size);
                                Ring? a = OffsetRing(axis, offset * k);
                                Ring? b = OffsetRing(axis, outer * k);
                                if (a != null && b != null && !closed)
                                {
                                    List<Vec3> pa = GeometryMath.Tessellate(a, false, 0.05 * k);
                                    List<Vec3> pb = GeometryMath.Tessellate(b, false, 0.05 * k);
                                    pb.Reverse();
                                    Make(ctx, existing, type, new PolygonGeometry(new Ring(pa.Concat(pb).ToList())), null);
                                    made = 1;
                                }

                                break;
                            }

                        case StreetPartKind.Row:
                            {
                                long key = (long)Math.Round(offset * 100);
                                List<Placement> row = rows.TryGetValue(key, out List<Placement>? earlier) && earlier.Count > 1
                                    ? Between(earlier, part.Spacing * k)
                                    : PathPlacement.Distribute(axisRing, closed, part.Spacing * k, part.Spacing * k / 2, part.Spacing * k / 2, SpacingMode.Fit, offset * k, 0);
                                if (!rows.ContainsKey(key))
                                {
                                    rows[key] = row;
                                }

                                foreach (Placement pl in row)
                                {
                                    Make(ctx, existing, type, new PointGeometry(pl.Position, pl.Rotation), null);
                                }

                                made = row.Count;
                                break;
                            }
                    }

                    counts[type.Name] = (counts.TryGetValue(type.Name, out int c) ? c : 0) + made;
                }
            }

            ctx.Write(profile.Name + " (širina " + CommandContext.F(width, 2) + " m): " + string.Join(", ", counts.Where(kv => kv.Value > 0).Select(kv => kv.Value + " × " + kv.Key)) + ".");
            ctx.Write("Pojedine dijelove možete urediti (Uredi element) ili obrisati; ulica je jedan korak poništavanja (UNDO).");
        });

        /// <summary>
        /// A second row on the same line as an earlier one (lights in a tree row) goes into the gaps between the earlier
        /// items, every n-th gap so the spacing stays close to the requested one; nothing lands on a tree.
        /// </summary>
        private static List<Placement> Between(List<Placement> earlier, double spacing)
        {
            double step = 0;
            for (int i = 1; i < earlier.Count; i++)
            {
                step += (earlier[i].Position - earlier[i - 1].Position).Length;
            }

            step /= earlier.Count - 1;
            int every = Math.Max(1, (int)Math.Round(spacing / Math.Max(step, 1e-9)));
            var result = new List<Placement>();
            for (int i = every / 2; i < earlier.Count - 1; i += every)
            {
                Placement a = earlier[i], b = earlier[i + 1];
                result.Add(new Placement((a.Position + b.Position) * 0.5, a.Rotation));
            }

            return result;
        }

        /// <summary>Curve parallel to the axis, offset to the left (positive) or right (negative), as a ring.</summary>
        private static Ring? OffsetRing(Curve axis, double leftOffset)
        {
            if (Math.Abs(leftOffset) < 1e-9)
            {
                return ElementService.ReadCurve(axis).Ring;
            }

            Point3d start = axis.StartPoint;
            Vector3d dir = axis.GetFirstDerivative(axis.StartParam).GetNormal();
            Point3d expected = start + (new Vector3d(-dir.Y, dir.X, 0) * leftOffset);
            foreach (double d in new[] { leftOffset, -leftOffset })
            {
                DBObjectCollection curves;
                try
                {
                    curves = axis.GetOffsetCurves(d);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    continue;
                }

                Curve? c = curves.Cast<DBObject>().OfType<Curve>().FirstOrDefault();
                Ring? ring = null;
                if (c != null && c.StartPoint.DistanceTo(expected) < Math.Abs(leftOffset) * 0.5)
                {
                    ring = ElementService.ReadCurve(c).Ring;
                }

                foreach (DBObject o in curves)
                {
                    o.Dispose();
                }

                if (ring != null)
                {
                    return ring;
                }
            }

            return null;
        }

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
        [CommandMethod("SITEDESIGNPRO", "SDPKRUZNITOK", CommandFlags.Modal)]
        public void Roundabout() => CommandContext.RunMajor("SDPKRUZNITOK", "Kružni tok (SDPKRUZNITOK)", ctx =>
        {
            Point3d c = ctx.Point("Centar kružnog toka")!.Value;
            double ro = ctx.Number("Vanjski poluprečnik (m)", 14);
            double w = Math.Min(ctx.Number("Širina kolovoza prstena (m)", 6), ro * 0.8);
            int arms = ctx.Integer("Broj krakova (0–8)", 4, 0);
            arms = Math.Min(arms, 8);
            double first = arms > 0 ? ctx.Angle("Smjer prvog kraka (klik ili ugao)", c, 0) : 0;
            double armLength = arms > 0 ? ctx.Number("Dužina cesta krakova (m, 0 = bez)", 25, false, true) : 0;
            double armWidth = arms > 0 ? ctx.Number("Širina cesta krakova (m)", Math.Max(w, 5.5)) : 0;

            double k = ctx.K;
            List<SiteElement> existing = ctx.ExistingElements();
            Vec3 center = new Vec3(c.X, c.Y, c.Z);
            Ring Circle(double rMeters) => new Ring(new List<Vec3> { new Vec3(c.X + (rMeters * k), c.Y, c.Z), new Vec3(c.X - (rMeters * k), c.Y, c.Z) }, new List<double> { 1.0, 1.0 });
            ElementType T(string id) => ctx.Catalog.RequireType(id);

            double ri = ro - w;
            Make(ctx, existing, T("road.access_road"), new PathGeometry(Circle(ro - (w / 2)), true), w);
            Make(ctx, existing, T("edge.kerb"), new PathGeometry(Circle(ro), true), null);
            int n = 3;
            if (ri > 1.5)
            {
                Make(ctx, existing, T("edge.kerb"), new PathGeometry(Circle(ri), true), null);
                Make(ctx, existing, T("landscape.lawn_area"), new PolygonGeometry(Circle(ri - 0.2)), null);
                n += 2;
                if (ri > 4)
                {
                    Make(ctx, existing, T("landscape.flower_bed"), new PolygonGeometry(Circle(ri * 0.4)), null);
                    Make(ctx, existing, T("decorative.sculpture"), new PointGeometry(center, 0), null);
                    n += 2;
                }
            }

            for (int i = 0; i < arms; i++)
            {
                double a = first + (i * 2 * Math.PI / arms);
                double ux = Math.Cos(a), uy = Math.Sin(a);
                Vec3 P(double along, double left) => new Vec3(c.X + (((ux * along) - (uy * left)) * k), c.Y + (((uy * along) + (ux * left)) * k), c.Z);

                // splitter island on the arm axis, separating the entry from the exit lane
                double islandEnd = ro + Math.Min(12, Math.Max(4, armLength > 0 ? armLength - 6 : 10));
                Make(ctx, existing, T("road.hatched_area"), new PolygonGeometry(new Ring(new List<Vec3> { P(ro + 1.5, -0.4), P(islandEnd, -1.2), P(islandEnd, 1.2), P(ro + 1.5, 0.4) })), null);
                n++;
                if (armLength > 0)
                {
                    Make(ctx, existing, T("road.access_road"), new PathGeometry(new Ring(new List<Vec3> { P(ro, 0), P(ro + armLength, 0) }), false), armWidth);
                    n++;
                    if (armLength >= islandEnd - ro + 5)
                    {
                        double d0 = islandEnd + 1;
                        Make(ctx, existing, T("road.pedestrian_crossing"), new PolygonGeometry(new Ring(new List<Vec3> { P(d0, -armWidth / 2), P(d0 + 4, -armWidth / 2), P(d0 + 4, armWidth / 2), P(d0, armWidth / 2) })), null);
                        n++;
                    }
                }

                // counter-clockwise traffic: arrow on the ring just past the arm, sign on the entry side
                double ta = a + (15 * Math.PI / 180);
                double ra = ro - (w / 2);
                Make(ctx, existing, T("road.arrow"), new PointGeometry(new Vec3(c.X + (Math.Cos(ta) * ra * k), c.Y + (Math.Sin(ta) * ra * k), c.Z), ta + (Math.PI / 2)), null);
                Make(ctx, existing, T("road.traffic_sign"), new PointGeometry(P(islandEnd, (armWidth / 2) + 1.0), a + Math.PI), null);
                n += 2;
            }

            ctx.Write(Bs.T("Kružni tok: poluprečnik {0} m, prsten {1} m, krakova {2}; napravljeno elemenata: {3}.", CommandContext.F(ro, 2), CommandContext.F(w, 2), arms, n));
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
