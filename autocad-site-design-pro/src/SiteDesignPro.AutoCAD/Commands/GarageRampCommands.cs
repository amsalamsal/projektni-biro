using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// Garage ramps (layer "Garažna rampa"): straight, semicircular or circular/spiral, one-way or two-way. The ramp is an
    /// element drawn by its centreline; edges, the yellow lines of the safety margin, the middle line, direction arrows,
    /// the island and the text L / h / slope are drawn with the roads.
    /// </summary>
    public sealed partial class RoadCommands
    {
        internal const string RampOwner = "garaznerampe";
        internal const string RampLayerName = "Garažna rampa";

        /// <summary>Garažna rampa: kind, direction, lane width, margin, island, geometry and the heights of the two ends.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPGARAZNARAMPA", CommandFlags.Modal)]
        public void GarageRampCommand() => Guarded(() => CommandContext.Run("SDPGARAZNARAMPA", ctx =>
        {
            EnsureUnits(ctx);
            double k = ctx.K;
            var form = new Form("Garažna rampa", "garazna-rampa", "Vrsta i presjek rampe. Nakon „U redu“ crtate rampu u crtežu (dok birate tačke, rampa se vidi), a zatim se otvara prozor za visine.");
            Func<FormValues, bool> isStraight = v => v.Is("kind", "straight");
            Func<FormValues, bool> isRound = v => v.Is("kind", "semicircular") || v.Is("kind", "spiral");
            form.Choice("kind", "Vrsta rampe", new[] { "straight|Pravolinijska", "semicircular|Polukružna", "spiral|Spiralna", "combined|Kombinovana" }, "straight",
                "Kombinovana: pravi dijelovi spojeni lukovima istog poluprečnika.");
            form.Choice("direction", "Smjer", new[] { "one_way|Jednosmjerna", "two_way|Dvosmjerna" }, "one_way");
            form.Number("laneWidth", "Širina trake (m)", 3.0, 2.5, 5.0, "Pravolinijska 2,50–3,50 m, zakrivljena 3,50–5,00 m, kombinovana 2,50–5,00 m (u lukovima najmanje 3,50 m).");
            form.Number("margin", "Zaštitni razmak sa obje strane (m)", 0.25, 0, 0.5, "Pravolinijska 0–0,50 m, zakrivljena 0,25–0,50 m.", null, false, true);
            form.Bool("island", "Srednje ostrvo 60 × 250 cm na dnu", true, null, v => isStraight(v) && v.Is("direction", "two_way"));
            form.Number("bend", "Poluprečnik srednje linije u krivinama (m)", 8.0, 3, 100, "Unutrašnja ivica luka mora imati najmanje 5,00 m.", v => v.Is("kind", "combined"));
            form.Choice("turn", "Smjer okretanja", new[] { "Left|Lijevo", "Right|Desno" }, "Left", null, isRound);
            form.Number("turns", "Broj punih krugova", 1.0, 1, 5, "Npr. 1 ili 1,5.", v => v.Is("kind", "spiral"));
            form.Check = v =>
            {
                (double l, double h) = v.Is("kind", "combined") ? (2.5, 5.0) : v.Is("kind", "straight") ? (2.5, 3.5) : (3.5, 5.0);
                if (v.Number("laneWidth") < l - 1e-9 || v.Number("laneWidth") > h + 1e-9)
                {
                    return "Širina trake za ovu vrstu rampe mora biti od " + Bs.Num(l, 2) + " do " + Bs.Num(h, 2) + " m.";
                }

                if (!v.Is("kind", "straight") && v.Number("margin") < 0.25 - 1e-9)
                {
                    return "Zaštitni razmak zakrivljene rampe mora biti najmanje 0,25 m.";
                }

                double w = ((v.Is("direction", "two_way") ? 2 : 1) * v.Number("laneWidth")) + (2 * v.Number("margin"));
                if (v.Is("kind", "combined") && v.Number("bend") - (w / 2) < GarageRamp.MinInnerRadius - 1e-9)
                {
                    return "Unutrašnja ivica luka bi imala " + Bs.Num(v.Number("bend") - (w / 2), 2) + " m; najmanje je 5,00 m – poluprečnik srednje linije mora biti najmanje " + Bs.Num(GarageRamp.MinInnerRadius + (w / 2), 2) + " m.";
                }

                return null;
            };
            FormValues kindAnswers = ctx.Ask(form);
            string kind = kindAnswers.Choice("kind");
            bool combined = kind == "combined";
            bool curved = kind != "straight";
            bool twoWay = kindAnswers.Is("direction", "two_way");
            double lw = kindAnswers.Number("laneWidth");
            double margin = kindAnswers.Number("margin");
            bool island = !curved && twoWay && kindAnswers.Bool("island");
            double width = ((twoWay ? 2 : 1) * lw) + (2 * margin);

            // while the points are picked, the ramp is shown at the cursor as it will be drawn
            List<PreviewShape> Preview(List<Vec2> points, List<double> pointBulges)
            {
                var probe = new GarageRamp
                {
                    Kind = kind, TwoWay = twoWay, LaneWidth = lw, Margin = margin, Island = island, Walls = false, Drain = "none",
                    RingPoints = points, RingBulges = pointBulges, Axis = points, StartHeight = 0, EndHeight = -1,
                };
                var shapes = new List<PreviewShape>();
                foreach (RampShape x in probe.Plan().Where(x => x.Role == "edge" || x.Role == "margin" || x.Role == "middle" || x.Role == "arrow" || x.Role == "island"))
                {
                    var shape = new PreviewShape { Closed = x.Closed, Color = x.Role == "margin" ? (short)2 : (short)7 };
                    for (int i = 0; i < x.Points.Count; i++)
                    {
                        shape.Points.Add(new Point2d(x.Points[i].X * k, x.Points[i].Y * k));
                        shape.Bulges.Add(i < x.Bulges.Count ? x.Bulges[i] : 0);
                    }

                    shapes.Add(shape);
                }

                return shapes;
            }

            Vec2 M(Point3d p) => new Vec2(p.X / k, p.Y / k);

            // a ramp that starts at a road: a helper line square to the road through the first point, and the next
            // point is pulled onto it when the cursor is within 6° of it
            List<RoadSpec> nearRoads = RoadElements.Specs(ctx.ExistingElements(), ctx.Catalog, k);
            Vec2? Square(Vec2 p) => SquareToRoad(p, nearRoads);
            Point3d Pull(Point3d first, Point3d cur, Vec2? n)
            {
                if (!n.HasValue)
                {
                    return cur;
                }

                Vec2 a0 = M(first), v = M(cur) - a0;
                double along = v.Dot(n.Value);
                if (v.Length < 1e-6 || Math.Abs(along) / v.Length < Math.Cos(6 * Math.PI / 180))
                {
                    return cur;
                }

                Vec2 q = a0 + (n.Value * along);
                return new Point3d(q.X * k, q.Y * k, cur.Z);
            }

            List<PreviewShape> WithHelper(List<PreviewShape> shapes, Point3d first, Vec2? n)
            {
                if (n.HasValue)
                {
                    Vec2 a0 = M(first), p0 = a0 - (n.Value * 40), p1 = a0 + (n.Value * 40);
                    var helper = new PreviewShape { Color = 4 };
                    helper.Points.Add(new Point2d(p0.X * k, p0.Y * k));
                    helper.Points.Add(new Point2d(p1.X * k, p1.Y * k));
                    shapes.Insert(0, helper);
                }

                return shapes;
            }

            Ring axis;
            double axisLength;
            if (!curved)
            {
                Point3d? a = ctx.Point("Početak rampe (sredina, gornji kraj)");
                if (!a.HasValue)
                {
                    return;
                }

                Vec2? square = Square(M(a.Value));
                if (square.HasValue)
                {
                    ctx.Write("Početak rampe je uz saobraćajnicu: plava pomoćna linija pokazuje smjer okomit na nju.");
                }

                Point3d? b = ctx.PointPreview("Kraj rampe (sredina, donji kraj)", a.Value, false, cur =>
                    WithHelper(Preview(new List<Vec2> { M(a.Value), M(Pull(a.Value, cur, square)) }, new List<double> { 0, 0 }), a.Value, square));
                if (b.HasValue)
                {
                    b = Pull(a.Value, b.Value, square);
                }

                if (!b.HasValue || a.Value.DistanceTo(b.Value) < 0.5 * k)
                {
                    ctx.Write("Rampa nije nacrtana.");
                    return;
                }

                axis = new Ring(new List<Vec3> { new Vec3(a.Value.X, a.Value.Y, 0), new Vec3(b.Value.X, b.Value.Y, 0) });
                axisLength = a.Value.DistanceTo(b.Value) / k;
            }
            else if (combined)
            {
                // straight parts joined by arcs: the points of the centreline and one radius for its bends
                double bend = kindAnswers.Number("bend");
                var clicked = new List<Vec2>();
                Point3d? last = null;
                Vec2? square = null;
                while (true)
                {
                    Vec2? helper = clicked.Count == 1 ? square : null;
                    Point3d? pt = clicked.Count == 0
                        ? ctx.Point("Početak rampe (sredina, gornji kraj)")
                        : ctx.PointPreview("Sljedeća tačka srednje linije (Enter za kraj)", last, clicked.Count >= 2, cur =>
                        {
                            (List<Vec2> pp, List<double> pb) = GarageRamp.FilletPath(clicked.Concat(new[] { M(Pull(last!.Value, cur, helper)) }).ToList(), bend);
                            return WithHelper(Preview(pp, pb), last!.Value, helper);
                        });
                    if (!pt.HasValue)
                    {
                        break;
                    }

                    if (clicked.Count == 0)
                    {
                        square = Square(M(pt.Value));
                        if (square.HasValue)
                        {
                            ctx.Write("Početak rampe je uz saobraćajnicu: plava pomoćna linija pokazuje smjer okomit na nju.");
                        }
                    }
                    else if (clicked.Count == 1)
                    {
                        pt = Pull(last!.Value, pt.Value, helper);
                    }

                    clicked.Add(new Vec2(pt.Value.X / k, pt.Value.Y / k));
                    last = pt;
                }

                if (clicked.Count < 2)
                {
                    ctx.Write("Rampa nije nacrtana.");
                    return;
                }

                (List<Vec2> fp, List<double> fb) = GarageRamp.FilletPath(clicked, bend);
                axis = new Ring(fp.Select(v => new Vec3(v.X * k, v.Y * k, 0)).ToList(), fb);
                var probe = new GarageRamp { Kind = "combined", RingPoints = fp, RingBulges = fb };
                axisLength = probe.Length;
                if (probe.AxisRadius > 0 && probe.AxisRadius - (width / 2) < 5.0)
                {
                    ctx.Write("UPOZORENJE: unutrašnji poluprečnik rampe je " + Bs.Num(probe.AxisRadius - (width / 2), 2) + " m (preporučeno najmanje 5 m).");
                }
            }
            else
            {
                Point3d? c = ctx.Point("Središte rampe");
                if (!c.HasValue)
                {
                    return;
                }

                bool left = kindAnswers.Choice("turn") == "Left";
                int halves = 1;
                if (kind == "spiral")
                {
                    double turns = kindAnswers.Number("turns");
                    halves = Math.Max(2, (int)Math.Round(turns * 2));
                }

                Point3d? s = ctx.PointPreview("Početak rampe na srednjoj liniji", c.Value, false, cur =>
                {
                    Vec2 start = M(cur), opposite = (M(c.Value) * 2) - start;
                    var pp = new List<Vec2>();
                    var pb = new List<double>();
                    for (int i = 0; i <= halves; i++)
                    {
                        pp.Add(i % 2 == 0 ? start : opposite);
                        pb.Add(i == halves ? 0 : (left ? 1.0 : -1.0));
                    }

                    return Preview(pp, pb);
                });
                if (!s.HasValue || c.Value.DistanceTo(s.Value) < 1.0 * k)
                {
                    ctx.Write("Rampa nije nacrtana.");
                    return;
                }

                double radius = c.Value.DistanceTo(s.Value) / k;
                if (radius - (width / 2) < 5.0)
                {
                    ctx.Write("UPOZORENJE: unutrašnji poluprečnik rampe je " + Bs.Num(radius - (width / 2), 2) + " m (preporučeno najmanje 5 m).");
                }

                var pts = new List<Vec3>();
                var bulges = new List<double>();
                Vec3 p = new Vec3(s.Value.X, s.Value.Y, 0), q = new Vec3((2 * c.Value.X) - s.Value.X, (2 * c.Value.Y) - s.Value.Y, 0);
                for (int i = 0; i <= halves; i++)
                {
                    pts.Add(i % 2 == 0 ? p : q);
                    bulges.Add(i == halves ? 0 : (left ? 1.0 : -1.0));
                }

                axis = new Ring(pts, bulges);
                axisLength = Math.PI * radius * halves;
            }

            RoadLevelSettings levels = RoadLevelSettings.Read(ctx.Db, ctx.Tr);

            // a ramp that starts at a road starts at the height of the road there
            Vec3 startAt = axis.Vertices[0];
            double? roadLevel = RoadElements.LevelNear(ctx.ExistingElements(), ctx.Catalog, k, new Vec2(startAt.X / k, startAt.Y / k));
            double length = axisLength;
            var hf = new Form("Garažna rampa – visine", "garazna-rampa-visine",
                "Dužina rampe po osovini: " + Bs.Num(length, 2) + " m." + (roadLevel.HasValue ? " Rampa počinje uz saobraćajnicu koja je tu na " + LevelText(roadLevel.Value) + "." : string.Empty) +
                " Visine su u odnosu na osnovnu visinu saobraćajnica.");
            if (!levels.Defined)
            {
                hf.Number("base", "Osnovna (nulta) visina saobraćajnica (m n.v.)", 0, null, null, "Zadaje se jednom za crtež.", null, true, true);
            }

            Func<FormValues, double> avg = v => length < 1e-9 ? 0 : Math.Abs(v.Number("end") - v.Number("start")) / length;
            hf.Number("start", "Visina početka rampe (m)", roadLevel ?? 0, null, null, "Npr. 0,00.", null, true, true);
            hf.Number("end", "Visina kraja rampe (m)", (roadLevel ?? 0) - 3, null, null, "Npr. -3,00.", null, true, true);
            hf.Number("transition", "Blaže prijelazne dionice s pola nagiba na vrhu i dnu (m, 0 = bez)", 0, 0, Math.Max(0, Math.Min(8, (length - 1) / 2)), "Ima smisla kad je prosječni nagib veći od 10 %.", v => avg(v) > 0.10 + 1e-9 && length >= 9, false, true);
            hf.Number("flat", "Blagi dio pred ulicom na vrhu, najviše 5 % (m, 0 = bez)", 0, 0, Math.Max(0, Math.Min(10, length - 3)), null, v => avg(v) > GarageRamp.FlatSlope + 1e-9 && length >= 12, false, true);
            hf.Number("ceilingFrom", "Ploča iznad rampe počinje na (m od početka, 0 = nema ploče)", 0, 0, null, "Provjerava se svijetla visina ispod ploče (najmanje 2,10 m).", null, false, true);
            hf.Number("ceilingLevel", "Donja ivica te ploče (m)", -0.3, null, null, "Npr. -0,30.", v => v.Number("ceilingFrom") > 0.01, true, true);
            foreach (string key in new[] { "start", "end", "ceilingFrom", "ceilingLevel" })
            {
                hf.Field(key).Remember = false;
            }

            hf.Summary = v => "Prosječni nagib: " + Bs.Num(avg(v) * 100, 2) + " % (najviše " + Bs.Num(RoadLevels.MaxRampSlope * 100, 0) + " %).";
            hf.Check = v => avg(v) > RoadLevels.MaxRampSlope + 1e-9
                ? "Nagib " + Bs.Num(avg(v) * 100, 2) + " % je veći od dozvoljenih " + Bs.Num(RoadLevels.MaxRampSlope * 100, 0) + " %: za ovu visinsku razliku rampa mora biti duga najmanje " + Bs.Num(Math.Abs(v.Number("end") - v.Number("start")) / RoadLevels.MaxRampSlope, 2) + " m."
                : null;
            FormValues hv = ctx.Ask(hf);
            if (!levels.Defined)
            {
                levels.Flat = false;
                levels.Base = hv.Number("base");
                levels.Write(ctx.Db, ctx.Tr);
            }

            double h0 = RoadCommands.Relative(ctx, hv.Number("start"), levels.Base);
            double h1 = RoadCommands.Relative(ctx, hv.Number("end"), levels.Base);
            double average = axisLength < 1e-9 ? 0 : Math.Abs(h1 - h0) / axisLength;
            double transition = average > 0.10 + 1e-9 && axisLength >= 9 ? hv.Number("transition") : 0;
            double flat = average > GarageRamp.FlatSlope + 1e-9 && axisLength >= 12 ? hv.Number("flat") : 0;
            double ceilingFrom = hv.Number("ceilingFrom");
            double ceilingLevel = ceilingFrom > 0.01 ? RoadCommands.Relative(ctx, hv.Number("ceilingLevel"), levels.Base) : 0;

            ElementType type = ctx.Catalog.RequireType(GarageRamp.Type);
            SiteElement e = ctx.NewElement(type, new PathGeometry(axis, false), ctx.ExistingElements());
            e.Parameters["kind"] = ParamValue.Of(kind);
            e.Parameters["direction"] = ParamValue.Of(twoWay ? "two_way" : "one_way");
            e.Parameters["laneWidth"] = ParamValue.Of(lw);
            e.Parameters["margin"] = ParamValue.Of(margin);
            e.Parameters["island"] = ParamValue.Of(island);
            e.Parameters["transition"] = ParamValue.Of(transition);
            e.Parameters["flatLength"] = ParamValue.Of(flat);
            e.Parameters["ceilingFrom"] = ParamValue.Of(ceilingFrom);
            e.Parameters["ceilingLevel"] = ParamValue.Of(ceilingLevel);
            e.Parameters["heated"] = ParamValue.Of(false);
            e.Parameters["startLevel"] = ParamValue.Of(h0);
            e.Parameters["endLevel"] = ParamValue.Of(h1);
            e.Parameters["width"] = ParamValue.Of(width);
            e.Name = "Garažna rampa";
            ctx.Svc.Create(e);

            GarageRamp? ramp = GarageRamp.From(e, k);
            if (ramp != null)
            {
                ctx.Write("Garažna rampa: " + RampText(ramp) + ", širina " + Bs.Num(ramp.Width, 2) + " m.");
                foreach ((double from, double to, double slope) in ramp.Parts())
                {
                    ctx.Write("  " + Bs.Num(from, 2) + "–" + Bs.Num(to, 2) + " m: " + Bs.Num(slope * 100, 2) + " %");
                }

                ctx.Write("  Uzdužni presjek: Presjek rampe (SDPRAMPAPRESJEK).");
            }

            RebuildNow(ctx);
        }));

        /// <summary>
        /// Presjek rampe: the longitudinal section of a garage ramp, placed where the user clicks; it follows the ramp
        /// when the ramp changes and goes when its small mark is erased.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPRAMPAPRESJEK", CommandFlags.Modal)]
        public void RampSection() => Guarded(() => CommandContext.Run("SDPRAMPAPRESJEK", ctx =>
        {
            double k = ctx.K;
            List<SiteElement> elements = ctx.ExistingElements();
            List<GarageRamp> ramps = GarageRamp.All(elements, k);
            if (ramps.Count == 0)
            {
                ctx.Write("U crtežu nema garažne rampe. Nova rampa: Garažna rampa (SDPGARAZNARAMPA).");
                return;
            }

            GarageRamp ramp = ramps[0];
            if (ramps.Count > 1)
            {
                Point3d? pick = ctx.Point("Kliknite rampu");
                if (!pick.HasValue)
                {
                    return;
                }

                var at = new Vec2(pick.Value.X / k, pick.Value.Y / k);
                ramp = ramps.OrderBy(r => r.Axis.Min(q => q.DistanceTo(at))).First();
            }

            Point3d? where = ctx.Point("Mjesto presjeka rampe " + ramp.Name + " (lijevi kraj donje linije)");
            if (!where.HasValue)
            {
                return;
            }

            ElementType type = ctx.Catalog.RequireType(RoadElements.RampSectionType);
            SiteElement e = ctx.NewElement(type, new PointGeometry(new Vec3(where.Value.X, where.Value.Y, 0), 0), elements);
            e.Parameters["reference"] = ParamValue.Of(ramp.Name);
            e.Name = "Uzdužni presjek rampe " + ramp.Name;
            ctx.Svc.Create(e);
            RebuildNow(ctx);
            ctx.Write("Uzdužni presjek rampe " + ramp.Name + " je nacrtan; prati izmjene rampe, a uklanja se brisanjem njegove oznake.");
        }));

        /// <summary>"L=12,50 m, h=2,10 m, 16,80 %".</summary>
        internal static string RampText(GarageRamp r) =>
            "L=" + Bs.Num(r.Length, 2) + " m, h=" + Bs.Num(Math.Abs(r.Rise), 2) + " m, " + Bs.Num(r.Slope * 100, 2) + " %";

        /// <summary>Draws all garage ramps of the drawing (their own layer and owner; erased and redrawn each time).</summary>
        /// <summary>
        /// The direction square to the nearest road (metres) when a point lies on or beside it (within its outer edge
        /// and 3 m), else null.
        /// </summary>
        internal static Vec2? SquareToRoad(Vec2 p, List<RoadSpec> roads)
        {
            Vec2? best = null;
            double bestGap = double.MaxValue;
            foreach (RoadSpec r in roads)
            {
                List<Vec2> pts = r.Closed && r.Axis.Count > 2 ? r.Axis.Concat(new[] { r.Axis[0] }).ToList() : r.Axis;
                double half = Math.Max(r.Extent(RoadLevel.Edge, true), r.Extent(RoadLevel.Edge, false));
                for (int i = 1; i < pts.Count; i++)
                {
                    Vec2 a = pts[i - 1], d = pts[i] - a;
                    double l2 = d.Dot(d);
                    if (l2 < 1e-12)
                    {
                        continue;
                    }

                    double t = Math.Max(0, Math.Min(1, (p - a).Dot(d) / l2));
                    double gap = p.DistanceTo(a + (d * t)) - half;
                    if (gap < bestGap && gap < 3.0)
                    {
                        bestGap = gap;
                        best = d.Normalized().Perp();
                    }
                }
            }

            return best;
        }

        internal static void DrawGarageRamps(CommandContext ctx, List<SiteElement> elements)
        {
            foreach ((ObjectId id, string _) in ctx.Svc.Aux(RampOwner).ToList())
            {
                if (!id.IsErased)
                {
                    ctx.Tr.GetObject(id, OpenMode.ForWrite).Erase();
                }
            }

            double k = ctx.K;
            List<GarageRamp> ramps = GarageRamp.All(elements, k);
            if (ramps.Count == 0)
            {
                return;
            }

            ObjectId layer = ctx.Svc.Layers.Ensure(new LayerDefinition
            {
                Key = "garage_ramps",
                Name = RampLayerName,
                Color = 2,
                Plot = true,
                Description = "Vanjsko uređenje PRO – garažne rampe",
            });
            var layerRecord = (LayerTableRecord)ctx.Tr.GetObject(layer, OpenMode.ForWrite);
            bool wasLocked = layerRecord.IsLocked;
            layerRecord.IsLocked = false;
            var ms = (BlockTableRecord)ctx.Tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(ctx.Db), OpenMode.ForWrite);
            void Add(Entity en, string role, Color? color, int transparency)
            {
                en.SetDatabaseDefaults(ctx.Db);
                en.LayerId = layer;
                en.Color = color ?? Color.FromColorIndex(ColorMethod.ByLayer, 256);
                if (transparency > 0)
                {
                    en.Transparency = new Transparency((byte)Math.Round(255 * (100 - transparency) / 100.0));
                }

                ms.AppendEntity(en);
                ctx.Tr.AddNewlyCreatedDBObject(en, true);
                ElementStore.WriteAux(en, RampOwner, role, ctx.Tr);
            }

            Polyline Pl(RampShape shape)
            {
                var pl = new Polyline();
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    pl.AddVertexAt(i, new Point2d(shape.Points[i].X * k, shape.Points[i].Y * k), i < shape.Bulges.Count ? shape.Bulges[i] : 0, 0, 0);
                }

                pl.Closed = shape.Closed;
                if (shape.Width > 0)
                {
                    pl.ConstantWidth = shape.Width * k;
                }

                return pl;
            }

            Color white = Color.FromColorIndex(ColorMethod.ByAci, 7);
            Color yellow = Color.FromColorIndex(ColorMethod.ByAci, 2);
            Color red = Color.FromRgb(230, 40, 40);
            RoadLevelSettings levels = RoadLevelSettings.Read(ctx.Db, ctx.Tr);
            var hatches = new ObjectIdCollection();
            List<RoadSpec> specs = RoadElements.Specs(elements, ctx.Catalog, k);
            void Draw(RampShape shape)
            {
                if (shape.Role == "text")
                {
                    // fitted between two points, so the text has exactly the length kept free for it in the middle line
                    var dir = new Vec2(Math.Cos(shape.Rotation), Math.Sin(shape.Rotation));
                    Vec2 baseLine = shape.Position - (dir.Perp() * (shape.Height / 2));
                    Vec2 p0 = baseLine - (dir * (shape.Length / 2)), p1 = baseLine + (dir * (shape.Length / 2));
                    var t = new DBText
                    {
                        Height = shape.Height * k,
                        TextString = shape.Text ?? string.Empty,
                        Rotation = shape.Rotation,
                        HorizontalMode = TextHorizontalMode.TextFit,
                        VerticalMode = TextVerticalMode.TextBase,
                        Position = new Point3d(p0.X * k, p0.Y * k, 0),
                        AlignmentPoint = new Point3d(p1.X * k, p1.Y * k, 0),
                    };
                    Add(t, shape.Warning ? "rampa:tekst:previse" : "rampa:tekst", shape.Warning ? red : white, 0);
                    t.AdjustAlignment(ctx.Db);
                    return;
                }

                if (shape.Fill)
                {
                    try
                    {
                        Hatch h = RegionHatch(new RoadRegion(new RoadLoop(shape.Points, shape.Bulges), new List<RoadLoop>(), 0), k);
                        (Color color, int transparency) = shape.Role == "surface" ? (Color.FromRgb(196, 196, 196), 30) : shape.Role == "island" ? (Color.FromRgb(110, 110, 110), 0) : shape.Role == "drain" ? (Color.FromRgb(60, 60, 60), 0) : (white, 0);
                        Add(h, "rampa:" + shape.Role, color, transparency);
                        if (shape.Role == "surface")
                        {
                            hatches.Add(h.ObjectId);
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        Add(Pl(shape), "rampa:" + shape.Role, white, 0);
                    }

                    if (shape.Role == "island" || shape.Role == "drain")
                    {
                        Add(Pl(shape), "rampa:" + shape.Role, white, 0);
                    }

                    return;
                }

                if (shape.Role == "heated")
                {
                    // the heated driving width: a line hatch about every 30 cm
                    try
                    {
                        var h = new Hatch { PatternScale = 0.095 * k };
                        h.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                        h.Associative = false;
                        var v = new Point2dCollection();
                        var bl = new DoubleCollection();
                        for (int i = 0; i <= shape.Points.Count; i++)
                        {
                            int j = i % shape.Points.Count;
                            v.Add(new Point2d(shape.Points[j].X * k, shape.Points[j].Y * k));
                            bl.Add(i < shape.Points.Count ? shape.Bulges[j] : 0);
                        }

                        h.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, v, bl);
                        h.EvaluateHatch(true);
                        Add(h, "rampa:grijanje", Color.FromRgb(255, 140, 60), 40);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        // the note "grijana rampa" stays
                    }

                    return;
                }

                Color line = shape.Role == "margin" ? yellow : shape.Role == "warning" ? red : shape.Role == "ceiling" || shape.Role == "thin" ? Color.FromRgb(170, 170, 170) : white;
                Add(Pl(shape), "rampa:" + shape.Role, line, 0);
            }

            try
            {
                foreach (GarageRamp r in ramps)
                {
                    foreach (RampShape shape in r.Plan())
                    {
                        Draw(shape);
                    }

                    // elevation symbols beside the two ends (on opposite sides, so a full circle does not overlap them)
                    (Vec2 start, Vec2 end) = r.LevelPoints();
                    if (r.StartHeight.HasValue)
                    {
                        LevelSymbol(ctx, (en, role, color, tr) => Add(en, "rampa:" + role, color, tr), start, r.StartHeight.Value, levels);
                    }

                    if (r.EndHeight.HasValue)
                    {
                        LevelSymbol(ctx, (en, role, color, tr) => Add(en, "rampa:" + role, color, tr), end, r.EndHeight.Value, levels);
                    }

                    if (r.TooSteep)
                    {
                        ctx.Write("UPOZORENJE: uzdužni nagib garažne rampe " + r.Name + " je " + Bs.Num(r.Slope * 100, 2) + " % (dozvoljeno najviše 17 %).");
                    }

                    if (r.Curved && r.InnerRadius < 5.0)
                    {
                        ctx.Write("UPOZORENJE: unutrašnji poluprečnik garažne rampe " + r.Name + " je " + Bs.Num(r.InnerRadius, 2) + " m (preporučeno najmanje 5 m).");
                    }

                    if (r.TooLow)
                    {
                        ctx.Write("UPOZORENJE: svijetla visina pod pločom iznad rampe " + r.Name + " je " + Bs.Num(r.Clearance!.Value.Clearance, 2) + " m (potrebno " + Bs.Num(r.MinClearance, 2) + " m).");
                    }

                    if (r.FlatLength < 3 && r.Parts().First(x => r.TopAtStart ? x.From < 1e-6 : x.To > r.Length - 1e-6).Slope > GarageRamp.FlatSlope + 1e-9 && RoadElements.AtRoad(r, r.TopAtStart ? 0 : r.Length, specs))
                    {
                        ctx.Write("PREPORUKA: rampa " + r.Name + " izlazi na ulicu s nagibom većim od 5 %; dodajte blagi dio pred ulicom (oko 5 m, Svojstva).");
                    }
                }

                // longitudinal sections placed in the drawing: each follows its ramp
                foreach (SiteElement e in elements.Where(x => x.TypeId == RoadElements.RampSectionType && x.Geometry is PointGeometry))
                {
                    string name = e.Parameters.Text("reference", string.Empty);
                    GarageRamp? r = ramps.FirstOrDefault(x => x.Name == name);
                    if (r == null)
                    {
                        continue;
                    }

                    Vec3 at = ((PointGeometry)e.Geometry).Position;
                    var origin = new Vec2(at.X / k, at.Y / k);
                    foreach (RampShape shape in r.Section())
                    {
                        shape.Points = shape.Points.Select(q => q + origin).ToList();
                        shape.Position += origin;
                        Draw(shape);
                    }
                }

                if (hatches.Count > 0)
                {
                    var dot = (DrawOrderTable)ctx.Tr.GetObject(ms.DrawOrderTableId, OpenMode.ForWrite);
                    dot.MoveToBottom(hatches);
                }
            }
            finally
            {
                layerRecord.IsLocked = wasLocked;
            }
        }
    }
}
