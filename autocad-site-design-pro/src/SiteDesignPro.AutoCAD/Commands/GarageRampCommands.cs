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
            string kind = ctx.Keyword("Vrsta rampe [Pravolinijska/Polukruzna/Spiralna]", new[] { "straight|Pravolinijska", "semicircular|Polukruzna", "spiral|Spiralna" }, "straight");
            bool curved = kind != "straight";
            bool twoWay = ctx.Keyword("Smjer [Jednosmjerna/Dvosmjerna]", new[] { "one_way|Jednosmjerna", "two_way|Dvosmjerna" }, "one_way") == "two_way";
            (double lo, double hi) = curved ? (3.5, 5.0) : (2.5, 3.5);
            double lw = ctx.Number("Širina trake (m, " + Bs.Num(lo, 1) + "–" + Bs.Num(hi, 1) + ")", curved ? 4.0 : 3.0);
            if (lw < lo || lw > hi)
            {
                lw = Math.Max(lo, Math.Min(hi, lw));
                ctx.Write("Širina trake je ograničena na " + Bs.Num(lw, 2) + " m.");
            }

            double margin = ctx.Number("Zaštitni razmak sa obje strane (m, " + (curved ? "0,25–0,50" : "0–0,50") + ")", curved ? 0.5 : 0.25, false, true);
            margin = Math.Max(curved ? 0.25 : 0, Math.Min(0.5, margin));
            bool island = !curved && twoWay && ctx.Keyword("Srednje ostrvo 60 × 250 cm na dnu [Da/Ne]", new[] { "Yes|Da", "No|Ne" }, "Yes") == "Yes";
            double width = ((twoWay ? 2 : 1) * lw) + (2 * margin);

            Ring axis;
            if (!curved)
            {
                Point3d? a = ctx.Point("Početak rampe (sredina, gornji kraj)");
                if (!a.HasValue)
                {
                    return;
                }

                Point3d? b = ctx.Point("Kraj rampe (sredina, donji kraj)", a.Value);
                if (!b.HasValue || a.Value.DistanceTo(b.Value) < 0.5 * k)
                {
                    ctx.Write("Rampa nije nacrtana.");
                    return;
                }

                axis = new Ring(new List<Vec3> { new Vec3(a.Value.X, a.Value.Y, 0), new Vec3(b.Value.X, b.Value.Y, 0) });
            }
            else
            {
                Point3d? c = ctx.Point("Središte rampe");
                if (!c.HasValue)
                {
                    return;
                }

                Point3d? s = ctx.Point("Početak rampe na srednjoj liniji", c.Value);
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

                bool left = ctx.Keyword("Smjer okretanja [Lijevo/Desno]", new[] { "Left|Lijevo", "Right|Desno" }, "Left") == "Left";
                int halves = 1;
                if (kind == "spiral")
                {
                    double turns = ctx.Number("Broj punih krugova (npr. 1 ili 1,5)", 1.0);
                    halves = Math.Max(2, (int)Math.Round(turns * 2));
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
            }

            RoadLevelSettings levels = RoadLevelSettings.Read(ctx.Db, ctx.Tr);
            if (!levels.Defined)
            {
                ctx.Write("Osnovna visina saobraćajnica još nije zadana; zadajte je sada (može se promijeniti s Visine saobraćajnica).");
                levels.Flat = false;
                levels.Base = ctx.Number("Osnovna visina saobraćajnica (m n.v.)", 0, true, true);
                levels.Write(ctx.Db, ctx.Tr);
            }

            double h0 = ctx.Number("Visina početka rampe u odnosu na osnovnu " + Bs.Num(levels.Base, 2) + " m n.v. (npr. 0,00)", 0, true, true);
            double h1 = ctx.Number("Visina kraja rampe u odnosu na osnovnu (npr. -3,00)", h0 - 3, true, true);

            ElementType type = ctx.Catalog.RequireType(GarageRamp.Type);
            SiteElement e = ctx.NewElement(type, new PathGeometry(axis, false), ctx.ExistingElements());
            e.Parameters["kind"] = ParamValue.Of(kind);
            e.Parameters["direction"] = ParamValue.Of(twoWay ? "two_way" : "one_way");
            e.Parameters["laneWidth"] = ParamValue.Of(lw);
            e.Parameters["margin"] = ParamValue.Of(margin);
            e.Parameters["island"] = ParamValue.Of(island);
            e.Parameters["startLevel"] = ParamValue.Of(h0);
            e.Parameters["endLevel"] = ParamValue.Of(h1);
            e.Parameters["width"] = ParamValue.Of(width);
            e.Name = "Garažna rampa";
            ctx.Svc.Create(e);

            GarageRamp? ramp = GarageRamp.From(e, k);
            if (ramp != null)
            {
                ctx.Write("Garažna rampa: " + RampText(ramp) + ", širina " + Bs.Num(ramp.Width, 2) + " m.");
            }

            RebuildNow(ctx);
        }));

        /// <summary>"L=12,50 m, h=2,10 m, 16,80 %".</summary>
        internal static string RampText(GarageRamp r) =>
            "L=" + Bs.Num(r.Length, 2) + " m, h=" + Bs.Num(Math.Abs(r.Rise), 2) + " m, " + Bs.Num(r.Slope * 100, 2) + " %";

        /// <summary>Draws all garage ramps of the drawing (their own layer and owner; erased and redrawn each time).</summary>
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
            try
            {
                foreach (GarageRamp r in ramps)
                {
                    foreach (RampShape shape in r.Plan())
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
                            continue;
                        }

                        if (shape.Fill)
                        {
                            try
                            {
                                Hatch h = RegionHatch(new RoadRegion(new RoadLoop(shape.Points, shape.Bulges), new List<RoadLoop>(), 0), k);
                                (Color color, int transparency) = shape.Role == "surface" ? (Color.FromRgb(196, 196, 196), 30) : shape.Role == "island" ? (Color.FromRgb(110, 110, 110), 0) : (white, 0);
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

                            if (shape.Role == "island")
                            {
                                Add(Pl(shape), "rampa:ostrvo", white, 0);
                            }

                            continue;
                        }

                        Add(Pl(shape), "rampa:" + shape.Role, shape.Role == "margin" ? yellow : white, 0);
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
