using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using SiteDesignPro.AutoCAD.Data;
using SiteDesignPro.AutoCAD.Drawing;
using SiteDesignPro.AutoCAD.Infrastructure;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// Road network (kartica SAOBRAĆAJNICE): roads are axes with their cross-section stored on them; the carriageway,
    /// bike lanes, kerbs, sidewalks, lane markings and arrows of ALL roads are rebuilt together, so roads never overlap
    /// and every junction gets rounded kerbs. Pedestrian crossings are ready-made blocks.
    /// </summary>
    public sealed class RoadCommands
    {
        internal const string RoadApp = "SITEDESIGNPRO_CESTA";
        internal const string NetOwner = "roadnet";
        internal const string LayerName = "Saobraćajnice";
        internal const string AxisLayerName = "Saobraćajnice-osovine";
        private const string PrefKey = "roadSpec";

        // hatch colours (RGB) and transparency (%)
        private static readonly Dictionary<RoadLevel, (byte R, byte G, byte B, int T)> Fill = new Dictionary<RoadLevel, (byte, byte, byte, int)>
        {
            [RoadLevel.Carriageway] = (196, 196, 196, 30),
            [RoadLevel.Bike] = (128, 0, 32, 30),        // bordo
            [RoadLevel.Kerb] = (150, 150, 150, 0),
            [RoadLevel.Sidewalk] = (70, 70, 70, 30),    // tamno sivi
            [RoadLevel.Edge] = (150, 150, 150, 0),
        };

        /// <summary>True while a road command writes to the drawing (the watcher ignores those changes).</summary>
        internal static bool Busy { get; private set; }

        // ------------------------------------------------------------------ commands

        /// <summary>New road: cross-section in a window, then the axis is drawn or picked; the network is rebuilt.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAOBRACAJNICA", CommandFlags.Modal)]
        public void NewRoad() => Guarded(() => CommandContext.Run("SDPSAOBRACAJNICA", ctx =>
        {
            EnsureUnits(ctx);
            RoadSpec spec = RoadSpec.FromParameterString(UI.Preferences.Current.Default(PrefKey, new RoadSpec().ToParameterString()));
            RoadSpec? chosen = CommandContext.Interactive ? UI.RoadDialog.Ask(spec, "Nova saobraćajnica", UnitText(ctx)) : AskText(ctx, spec);
            if (chosen == null)
            {
                throw new CommandCancelled();
            }

            foreach (string note in chosen.Normalize())
            {
                ctx.Write(note);
            }

            UI.Preferences.Current.RememberDefault(PrefKey, chosen.ToParameterString());
            ctx.Write(Describe(chosen) + ".");
            string how = ctx.Keyword("Osovina saobraćajnice [Nacrtaj/Odaberi]", new[] { "Draw|Nacrtaj", "Select|Odaberi" }, "Draw");
            ObjectId axisId;
            if (how == "Draw")
            {
                ctx.Write("Nacrtajte osovinu (sredinu kolovoza) u smjeru crtanja: desne trake voze u tom smjeru. Saobraćajnica se spaja sa saobraćajnicama koje dodiruje.");
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

            var axis = (Entity)ctx.Tr.GetObject(axisId, OpenMode.ForWrite);
            if (ElementStore.ReadAux(axis).HasValue)
            {
                ctx.Write("Odabrani objekat je dio nacrtane saobraćajnice; odaberite osovinu ili nacrtajte novu.");
                return;
            }

            axis.LayerId = AxisLayer(ctx);
            WriteSpec(axis, chosen, ctx.Tr);
            Rebuild(ctx, true);
        }));

        /// <summary>Changes the cross-section of an existing road (pick its axis or anything drawn for it).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAOBRACAJNICAUREDI", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void EditRoad() => Guarded(() => CommandContext.Run("SDPSAOBRACAJNICAUREDI", ctx =>
        {
            List<(ObjectId Id, RoadSpec Spec)> roads = ReadRoads(ctx);
            if (roads.Count == 0)
            {
                ctx.Write("U crtežu nema saobraćajnica. Nova: SDPSAOBRACAJNICA.");
                return;
            }

            Point3d? pick = ctx.Point("Kliknite osovinu ili kolovoz saobraćajnice");
            if (!pick.HasValue)
            {
                return;
            }

            (ObjectId id, RoadSpec spec, _, _, double dist) = Nearest(roads, pick.Value, ctx.K);
            if (dist > Math.Max(spec.Extent(RoadLevel.Edge, true), spec.Extent(RoadLevel.Edge, false)) + 2)
            {
                ctx.Write("Na tom mjestu nema saobraćajnice.");
                return;
            }

            ctx.Write("Odabrana saobraćajnica: " + Describe(spec) + ".");
            RoadSpec? chosen = CommandContext.Interactive ? UI.RoadDialog.Ask(spec, "Uredi saobraćajnicu", UnitText(ctx)) : AskText(ctx, spec);
            if (chosen == null)
            {
                throw new CommandCancelled();
            }

            foreach (string note in chosen.Normalize())
            {
                ctx.Write(note);
            }

            WriteSpec((Entity)ctx.Tr.GetObject(id, OpenMode.ForWrite), chosen, ctx.Tr);
            UI.Preferences.Current.RememberDefault(PrefKey, chosen.ToParameterString());
            Rebuild(ctx, true);
        }));

        /// <summary>Redraws all roads from their axes (after moving, stretching, copying or erasing axes).</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAOBRACAJNICEOSVJEZI", CommandFlags.Modal)]
        public void RefreshRoads() => Guarded(() => CommandContext.Run("SDPSAOBRACAJNICEOSVJEZI", ctx => Rebuild(ctx, true)));

        /// <summary>Quiet redraw started automatically after an axis was changed by an ordinary AutoCAD command.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAOBRACAJNICEAUTO", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void AutoRefresh() => Guarded(() => CommandContext.Run("SDPSAOBRACAJNICEAUTO", ctx => Rebuild(ctx, false)));

        /// <summary>Removes the drawn roads (axes stay) or the roads with their axes.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPSAOBRACAJNICEUKLONI", CommandFlags.Modal)]
        public void RemoveRoads() => Guarded(() => CommandContext.Run("SDPSAOBRACAJNICEUKLONI", ctx =>
        {
            string mode = ctx.Keyword("Ukloniti [Crtez/Sve]", new[] { "Drawing|Crtez", "All|Sve" }, "Drawing");
            int n = EraseNetwork(ctx);
            if (mode == "All")
            {
                foreach ((ObjectId id, _) in ReadRoads(ctx))
                {
                    ctx.Tr.GetObject(id, OpenMode.ForWrite).Erase();
                }

                ctx.Write("Saobraćajnice i njihove osovine su uklonjene.");
            }
            else
            {
                ctx.Write("Uklonjeno je " + n + " nacrtanih dijelova; osovine ostaju (SDPSAOBRACAJNICEOSVJEZI ih ponovo crta).");
            }
        }));

        /// <summary>Pedestrian crossing as a ready-made block: white 0,50 m bars with 0,50 m gaps across all lanes.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPJESACKIPRIJELAZ", CommandFlags.Modal)]
        public void Crossing() => Guarded(() => CommandContext.Run("SDPPJESACKIPRIJELAZ", ctx =>
        {
            EnsureUnits(ctx);
            double k = ctx.K;
            Point3d? at = ctx.Point("Mjesto pješačkog prijelaza (klik na saobraćajnicu)");
            if (!at.HasValue)
            {
                return;
            }

            List<(ObjectId Id, RoadSpec Spec)> roads = ReadRoads(ctx);
            int lanes = 2;
            double lw = 3.0;
            Vec2 centre = new Vec2(at.Value.X / k, at.Value.Y / k);
            double angle;
            bool onRoad = false;
            if (roads.Count > 0)
            {
                (_, RoadSpec spec, Vec2 foot, Vec2 dir, double dist) = Nearest(roads, at.Value, k);
                if (dist <= Math.Max(spec.CarriagewayLeft, spec.CarriagewayRight) + 1.0)
                {
                    onRoad = true;
                    lanes = spec.TotalLanes;
                    lw = spec.LaneWidth;
                    centre = foot + (dir.Perp() * ((spec.CarriagewayLeft - spec.CarriagewayRight) / 2));
                    angle = dir.Angle;
                    ctx.Write("Saobraćajnica: " + Bs.Count(lanes, "traka", "trake", "traka") + " × " + Bs.Num(lw, 2) + " m; prijelaz se postavlja okomito na osovinu, preko cijelog kolovoza.");
                }
                else
                {
                    angle = ctx.Angle("Smjer saobraćajnice (drugi klik ili ugao)", at.Value, 0);
                }
            }
            else
            {
                angle = ctx.Angle("Smjer saobraćajnice (drugi klik ili ugao)", at.Value, 0);
            }

            lanes = Math.Min(6, ctx.Integer("Broj traka", lanes, 1));
            lw = ctx.Number("Širina saobraćajne trake (m)", lw);
            double length = ctx.Number("Širina prijelaza – dužina pruga (m)", 4.0);
            if (lw < 1 || length < 1)
            {
                ctx.Write("Širina trake i prijelaza moraju biti najmanje 1 m.");
                return;
            }

            Busy = true;
            ObjectId block = CrossingBlock(ctx, lanes, lw, length);
            var br = new BlockReference(new Point3d(centre.X * k, centre.Y * k, 0), block) { Rotation = angle - (Math.PI / 2) };
            br.SetDatabaseDefaults(ctx.Db);
            br.LayerId = RoadLayer(ctx);
            BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            ms.AppendEntity(br);
            ctx.Tr.AddNewlyCreatedDBObject(br, true);
            int bars = Bars(lanes * lw).Count;
            ctx.Write("Pješački prijelaz: " + bars + " pruga 0,50 m s razmakom 0,50 m preko " + Bs.Num(lanes * lw, 2) + " m kolovoza, dužina pruga " + Bs.Num(length, 2) + " m" +
                      (onRoad ? string.Empty : " (slobodno postavljen)") + ". Prijelaz je blok: pomjera se i rotira kao jedan element.");
        }));

        // ------------------------------------------------------------------ network

        internal static RoadNetworkResult? Rebuild(CommandContext ctx, bool verbose)
        {
            Busy = true;
            List<(ObjectId Id, RoadSpec Spec)> roads = ReadRoads(ctx);
            EraseNetwork(ctx);
            if (roads.Count == 0)
            {
                if (verbose)
                {
                    ctx.Write("U crtežu nema saobraćajnica (osovina s podacima saobraćajnice).");
                }

                return null;
            }

            RoadNetworkResult res = RoadNetwork.Build(roads.Select(r => r.Spec).ToList());
            double k = ctx.K;
            ObjectId layer = RoadLayer(ctx);
            BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForWrite);
            var hatches = new ObjectIdCollection();
            int failed = 0;
            void Add(Entity e, string role, Color? color, int transparency)
            {
                e.SetDatabaseDefaults(ctx.Db);
                e.LayerId = layer;
                if (color != null)
                {
                    e.Color = color;
                }

                if (transparency > 0)
                {
                    e.Transparency = new Transparency((byte)Math.Round(255 * (100 - transparency) / 100.0));
                }

                ms.AppendEntity(e);
                ctx.Tr.AddNewlyCreatedDBObject(e, true);
                ElementStore.WriteAux(e, NetOwner, role, ctx.Tr);
            }

            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                (byte r, byte g, byte b, int t) = Fill[band.Key];
                foreach (RoadRegion region in band.Value)
                {
                    try
                    {
                        Hatch h = RegionHatch(region, k);
                        Add(h, "povrsina:" + (int)band.Key, Color.FromRgb(r, g, b), t);
                        hatches.Add(h.ObjectId);
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        failed++;
                        Log.Info("Šrafura saobraćajnice nije nacrtana: " + ex.Message);
                    }
                }
            }

            double bike = res.EffectiveRadius - res.ActualRadius;
            foreach (KeyValuePair<RoadLevel, List<RoadLoop>> level in res.Outlines)
            {
                foreach (RoadLoop loop in level.Value)
                {
                    Add(LoopPolyline(loop, k), "rub:" + (int)level.Key + ":" + bike.ToString("0.###", CultureInfo.InvariantCulture), null, 0);
                }
            }

            Color white = Color.FromRgb(255, 255, 255);
            foreach (RoadMarking m in res.Markings)
            {
                var pl = new Polyline();
                for (int i = 0; i < m.Points.Count; i++)
                {
                    pl.AddVertexAt(i, new Point2d(m.Points[i].X * k, m.Points[i].Y * k), 0, 0, 0);
                }

                pl.ConstantWidth = 0.12 * k;
                Add(pl, m.Centre ? "oznaka:sredina" : "oznaka:traka", white, 0);
            }

            foreach (RoadArrow a in res.Arrows)
            {
                try
                {
                    var outline = new RoadLoop(a.Outline(), a.Outline().Select(_ => 0.0).ToList());
                    Hatch h = RegionHatch(new RoadRegion(outline, new List<RoadLoop>(), 0), k);
                    Add(h, "strelica", white, 0);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    failed++;
                }
            }

            if (hatches.Count > 0)
            {
                var dot = (DrawOrderTable)ctx.Tr.GetObject(ms.DrawOrderTableId, OpenMode.ForWrite);
                dot.MoveToBottom(hatches);
            }

            if (verbose)
            {
                foreach (string note in res.Notes)
                {
                    ctx.Write("  " + note);
                }

                int junctions = res.Outlines.TryGetValue(RoadLevel.Carriageway, out List<RoadLoop>? edges)
                    ? edges.Sum(l => l.ArcRadii().Count(r => Math.Abs(r - res.EffectiveRadius) < 0.05))
                    : 0;
                ctx.Write(Bs.T("Saobraćajnice ({0}): kolovoz {1}, biciklističke staze {2}, trotoari {3}; {4} oznaka traka, {5} strelica; zaobljenih uglova raskrsnica: {6}.",
                    roads.Count, Bs.SquareMeters(res.Area(RoadLevel.Carriageway), 1), Bs.SquareMeters(res.Area(RoadLevel.Bike), 1),
                    Bs.SquareMeters(res.Area(RoadLevel.Sidewalk), 1), res.Markings.Count, res.Arrows.Count, junctions));
                ctx.Write("Radijus ivičnjaka (stvarni) " + Bs.Meters(res.ActualRadius) + ", efektivni radijus (ivica kolovoza) " + Bs.Meters(res.EffectiveRadius) +
                          ". Radijus pojedinog luka vidite kad kursor stoji na liniji. Sloj: " + LayerName + ". Nakon pomjeranja osovina crtež se osvježava sam.");
            }

            if (failed > 0)
            {
                ctx.Write("Upozorenje: " + failed + " površina nije bilo moguće išrafirati (vjerovatno vrlo uske ili samopresijecajuće); provjerite osovine.");
            }

            return res;
        }

        private static int EraseNetwork(CommandContext ctx)
        {
            int n = 0;
            foreach ((ObjectId id, string _) in ctx.Svc.Aux(NetOwner).ToList())
            {
                if (!id.IsErased)
                {
                    ctx.Tr.GetObject(id, OpenMode.ForWrite).Erase();
                    n++;
                }
            }

            return n;
        }

        internal static List<(ObjectId Id, RoadSpec Spec)> ReadRoads(CommandContext ctx)
        {
            var list = new List<(ObjectId, RoadSpec)>();
            double k = ctx.K;
            BlockTableRecord ms = ctx.Svc.ModelSpace(OpenMode.ForRead);
            RXClass curveClass = RXObject.GetClass(typeof(Curve));
            foreach (ObjectId id in ms)
            {
                if (id.IsErased || !id.ObjectClass.IsDerivedFrom(curveClass))
                {
                    continue;
                }

                var c = (Curve)ctx.Tr.GetObject(id, OpenMode.ForRead);
                RoadSpec? spec = ReadSpec(c);
                if (spec == null)
                {
                    continue;
                }

                (Ring ring, bool closed) = ElementService.ReadCurve(c);
                spec.Id = c.Handle.ToString();
                spec.Closed = closed;
                spec.Axis = GeometryMath.Tessellate(ring, closed, 0.02 * k).Select(v => new Vec2(v.X / k, v.Y / k)).ToList();
                list.Add((id, spec));
            }

            return list;
        }

        internal static RoadSpec? ReadSpec(Entity e)
        {
            using ResultBuffer? rb = e.GetXDataForApplication(RoadApp);
            if (rb == null)
            {
                return null;
            }

            TypedValue[] v = rb.AsArray();
            return v.Length >= 2 && v[1].Value is string s ? RoadSpec.FromParameterString(s) : null;
        }

        private static void WriteSpec(Entity e, RoadSpec s, Transaction tr)
        {
            var table = (RegAppTable)tr.GetObject(e.Database.RegAppTableId, OpenMode.ForRead);
            if (!table.Has(RoadApp))
            {
                table.UpgradeOpen();
                var rec = new RegAppTableRecord { Name = RoadApp };
                table.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }

            if (!e.IsWriteEnabled)
            {
                e.UpgradeOpen();
            }

            e.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, RoadApp), new TypedValue((int)DxfCode.ExtendedDataAsciiString, s.ToParameterString()));
        }

        /// <summary>The road whose axis is closest to a point: its foot point and direction there (metres).</summary>
        private static (ObjectId Id, RoadSpec Spec, Vec2 Foot, Vec2 Dir, double Dist) Nearest(List<(ObjectId Id, RoadSpec Spec)> roads, Point3d p, double k)
        {
            Vec2 q = new Vec2(p.X / k, p.Y / k);
            (ObjectId, RoadSpec, Vec2, Vec2, double) best = (roads[0].Id, roads[0].Spec, q, new Vec2(1, 0), double.MaxValue);
            foreach ((ObjectId id, RoadSpec s) in roads)
            {
                int segs = s.Closed ? s.Axis.Count : s.Axis.Count - 1;
                for (int i = 0; i < segs; i++)
                {
                    Vec2 a = s.Axis[i], b = s.Axis[(i + 1) % s.Axis.Count];
                    Vec2 ab = b - a;
                    double len2 = ab.Dot(ab);
                    if (len2 < 1e-12)
                    {
                        continue;
                    }

                    double t = Math.Max(0, Math.Min(1, (q - a).Dot(ab) / len2));
                    Vec2 foot = a + (ab * t);
                    double d = foot.DistanceTo(q);
                    if (d < best.Item5)
                    {
                        best = (id, s, foot, ab.Normalized(), d);
                    }
                }
            }

            return best;
        }

        // ------------------------------------------------------------------ drawing helpers

        private static Hatch RegionHatch(RoadRegion region, double k)
        {
            var h = new Hatch();
            h.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            h.Associative = false;
            (Point2dCollection v, DoubleCollection b) = LoopData(region.Outer, k);
            h.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, v, b);
            foreach (RoadLoop hole in region.Holes)
            {
                (Point2dCollection hv, DoubleCollection hb) = LoopData(hole, k);
                h.AppendLoop(HatchLoopTypes.Polyline, hv, hb);
            }

            h.EvaluateHatch(true);
            return h;
        }

        private static (Point2dCollection, DoubleCollection) LoopData(RoadLoop loop, double k)
        {
            var v = new Point2dCollection();
            var b = new DoubleCollection();
            for (int i = 0; i < loop.Points.Count; i++)
            {
                v.Add(new Point2d(loop.Points[i].X * k, loop.Points[i].Y * k));
                b.Add(loop.Bulges[i]);
            }

            v.Add(new Point2d(loop.Points[0].X * k, loop.Points[0].Y * k));
            b.Add(0);
            return (v, b);
        }

        private static Polyline LoopPolyline(RoadLoop loop, double k)
        {
            var pl = new Polyline();
            for (int i = 0; i < loop.Points.Count; i++)
            {
                pl.AddVertexAt(i, new Point2d(loop.Points[i].X * k, loop.Points[i].Y * k), loop.Bulges[i], 0, 0);
            }

            pl.Closed = true;
            return pl;
        }

        private static ObjectId RoadLayer(CommandContext ctx) => ctx.Svc.Layers.Ensure(new LayerDefinition
        {
            Key = "roads_network",
            Name = LayerName,
            Color = 7,
            Plot = true,
            Description = "Vanjsko uređenje PRO – saobraćajnice (kolovoz, staze, trotoari, ivičnjaci, oznake)",
        });

        private static ObjectId AxisLayer(CommandContext ctx) => ctx.Svc.Layers.Ensure(new LayerDefinition
        {
            Key = "roads_axis",
            Name = AxisLayerName,
            Color = 1,
            Plot = false,
            Description = "Vanjsko uređenje PRO – osovine saobraćajnica (podaci za crtanje; ne štampa se)",
        });

        /// <summary>Starting position of each 0,50 m bar across a carriageway of the given width (centred, 0,50 m gaps).</summary>
        internal static List<double> Bars(double width)
        {
            const double bar = 0.5, gap = 0.5;
            int n = Math.Max(1, (int)Math.Floor((width + gap) / (bar + gap) + 1e-9));
            double total = (n * bar) + ((n - 1) * gap);
            return Enumerable.Range(0, n).Select(i => (-total / 2) + (i * (bar + gap))).ToList();
        }

        /// <summary>Block of a pedestrian crossing (X across the road, Y along it), made once per size.</summary>
        private static ObjectId CrossingBlock(CommandContext ctx, int lanes, double laneWidth, double length)
        {
            double k = ctx.K;
            string name = "VU_PJESACKI_PRIJELAZ_" + lanes + "x" + Math.Round(laneWidth * 100) + "cm_" + Math.Round(length * 100) + "cm_" + ctx.Settings.DrawingUnit;
            var bt = (BlockTable)ctx.Tr.GetObject(ctx.Db.BlockTableId, OpenMode.ForRead);
            if (bt.Has(name))
            {
                return bt[name];
            }

            bt.UpgradeOpen();
            var btr = new BlockTableRecord { Name = name, Origin = Point3d.Origin, Comments = "Pješački prijelaz: " + lanes + " × " + Bs.Num(laneWidth, 2) + " m, pruge 0,50/0,50 m, dužina " + Bs.Num(length, 2) + " m" };
            ObjectId id = bt.Add(btr);
            ctx.Tr.AddNewlyCreatedDBObject(btr, true);

            List<double> bars = Bars(lanes * laneWidth);
            double y0 = -length / 2 * k, y1 = length / 2 * k;
            foreach (double x in bars)
            {
                var h = new Hatch();
                h.SetDatabaseDefaults(ctx.Db);
                h.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                h.Associative = false;
                h.Color = Color.FromRgb(255, 255, 255);
                var v = new Point2dCollection
                {
                    new Point2d(x * k, y0), new Point2d((x + 0.5) * k, y0), new Point2d((x + 0.5) * k, y1), new Point2d(x * k, y1), new Point2d(x * k, y0),
                };
                h.AppendLoop(HatchLoopTypes.Polyline | HatchLoopTypes.External, v, new DoubleCollection { 0, 0, 0, 0, 0 });
                h.EvaluateHatch(true);
                btr.AppendEntity(h);
                ctx.Tr.AddNewlyCreatedDBObject(h, true);
            }

            // dimensions of one bar and one gap (0,50 m), above the crossing
            if (bars.Count >= 2)
            {
                double th = new Annotations(ctx.Db, ctx.Tr, ctx.Svc).TextHeight;
                double yd = y1 + Math.Max(0.6 * k, th * 2);
                foreach ((double a, double b) in new[] { (bars[0], bars[0] + 0.5), (bars[0] + 0.5, bars[1]) })
                {
                    var dim = new AlignedDimension(new Point3d(a * k, y1, 0), new Point3d(b * k, y1, 0), new Point3d(a * k, yd, 0), string.Empty, ctx.Db.Dimstyle);
                    dim.SetDatabaseDefaults(ctx.Db);
                    btr.AppendEntity(dim);
                    ctx.Tr.AddNewlyCreatedDBObject(dim, true);
                    dim.Dimlfac = 1.0 / k;
                    dim.Dimdec = 2;
                    dim.Dimtxt = th;
                    dim.Dimtsz = th * 0.6;
                    dim.Dimexo = th * 0.3;
                    dim.Dimexe = th * 0.4;
                    dim.Dimgap = th * 0.3;
                    dim.Dimtix = false;
                    dim.Dimtad = 1;
                    dim.Dimzin = 0;
                }
            }

            return id;
        }

        // ------------------------------------------------------------------ input and units

        /// <summary>A drawing without a project gets the road scale 1 m = 100 cm (drawing unit centimetre).</summary>
        private static void EnsureUnits(CommandContext ctx)
        {
            if (ProjectStore.Read(ctx.Db, ctx.Tr) != null)
            {
                return;
            }

            ctx.Settings.DrawingUnit = "cm";
            ctx.Db.Insunits = UnitsValue.Centimeters;
            ctx.SaveSettings();
            ctx.Write("Crtež još nema projekta: postavljeno je mjerilo 1 m = 100 cm (jedinica crteža cm). Promjena: Postavke (SDPSETTINGS).");
        }

        private static string UnitText(CommandContext ctx) => ctx.Settings.DrawingUnit + " (1 m = " + Bs.Num(ctx.K, ctx.K < 1 ? 3 : 0) + " " + ctx.Settings.DrawingUnit + ")";

        /// <summary>The same questions as the window, in the command line (scripts).</summary>
        private static RoadSpec AskText(CommandContext ctx, RoadSpec s)
        {
            var r = s.CloneParameters();
            r.OneWay = ctx.Keyword("Smjer [Dvosmjerna/Jednosmjerna]", new[] { "Two|Dvosmjerna", "One|Jednosmjerna" }, s.OneWay ? "One" : "Two") == "One";
            if (r.OneWay)
            {
                r.LanesRight = ctx.Integer("Broj traka (1–3)", s.LanesRight, 1);
            }
            else
            {
                r.LanesLeft = ctx.Integer("Broj traka lijevo (1–3)", s.LanesLeft, 1);
                r.LanesRight = ctx.Integer("Broj traka desno (1–3)", s.LanesRight, 1);
            }

            r.LaneWidth = ctx.Number("Širina saobraćajne trake (m)", s.LaneWidth);
            r.SidewalkLeft = ctx.Number("Trotoar lijevo (m, 0 = nema)", s.SidewalkLeft, false, true);
            r.SidewalkRight = ctx.Number("Trotoar desno (m, 0 = nema)", s.SidewalkRight, false, true);
            r.BikeLeft = ctx.Number("Biciklistička staza lijevo (m, 0 = nema)", s.BikeLeft, false, true);
            r.BikeRight = ctx.Number("Biciklistička staza desno (m, 0 = nema)", s.BikeRight, false, true);
            r.CornerRadius = ctx.Number("Radijus ivičnjaka na raskrsnicama (m)", s.CornerRadius);
            r.Arrows = ctx.Keyword("Strelice smjera [Da/Ne]", new[] { "Yes|Da", "No|Ne" }, s.Arrows ? "Yes" : "No") == "Yes";
            return r;
        }

        internal static string Describe(RoadSpec s)
        {
            string lanes = s.OneWay
                ? "jednosmjerna, " + Bs.Count(s.TotalLanes, "traka", "trake", "traka")
                : "dvosmjerna, " + s.LanesLeft + " + " + Bs.Count(s.LanesRight, "traka", "trake", "traka");
            return lanes + " × " + Bs.Meters(s.LaneWidth) + ", trotoari " + Bs.Num(s.SidewalkLeft) + " / " + Bs.Num(s.SidewalkRight) + " m, biciklističke staze " +
                   Bs.Num(s.BikeLeft) + " / " + Bs.Num(s.BikeRight) + " m, radijus ivičnjaka " + Bs.Meters(s.CornerRadius) + (s.Arrows ? ", strelice svakih " + Bs.Num(s.ArrowSpacing, 0) + " m" : ", bez strelica");
        }

        private static void Guarded(Action body)
        {
            try
            {
                Busy = true;
                body();
            }
            finally
            {
                Busy = false;
            }
        }
    }

    /// <summary>
    /// Shows the radius of a kerb or carriageway-edge arc (actual and effective) and the cross-section of a road axis
    /// in the cursor tooltip, and redraws the roads after their axes were moved, stretched, copied or erased.
    /// </summary>
    internal static class RoadWatcher
    {
        private static readonly HashSet<Database> Dirty = new HashSet<Database>();
        private static readonly HashSet<string> UndoCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "U", "UNDO", "REDO", "MREDO", "OOPS" };
        private static string? running;

        public static void Attach(Document doc)
        {
            doc.Editor.PointMonitor += OnPointMonitor;
            Database db = doc.Database;
            db.ObjectModified += (s, e) => Touched(db, e.DBObject);
            db.ObjectAppended += (s, e) => Touched(db, e.DBObject);
            db.ObjectErased += (s, e) => Touched(db, e.DBObject);
            doc.CommandWillStart += (s, e) => running = e.GlobalCommandName;
            doc.CommandEnded += (s, e) => running = null;
            doc.CommandCancelled += (s, e) => running = null;
            doc.CommandFailed += (s, e) => running = null;
        }

        private static void Touched(Database db, DBObject o)
        {
            try
            {
                if (RoadCommands.Busy || o is not Curve c || (running != null && (UndoCommands.Contains(running) || running.StartsWith("SDP", StringComparison.OrdinalIgnoreCase))))
                {
                    return;
                }

                using ResultBuffer? rb = c.GetXDataForApplication(RoadCommands.RoadApp);
                if (rb != null)
                {
                    Dirty.Add(db);
                }
            }
            catch (System.Exception)
            {
                // reading XData of an object being erased can fail; it is not needed then
            }
        }

        /// <summary>Called on Idle: redraws roads of the active drawing once no command is running.</summary>
        public static void OnIdle(object? sender, EventArgs e)
        {
            Document? doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null || !Dirty.Contains(doc.Database) || !string.IsNullOrEmpty(doc.CommandInProgress))
            {
                return;
            }

            Dirty.Remove(doc.Database);
            doc.SendStringToExecute("_.SDPSAOBRACAJNICEAUTO ", true, false, false);
        }

        private static void OnPointMonitor(object? sender, PointMonitorEventArgs e)
        {
            try
            {
                FullSubentityPath[] paths = e.Context.GetPickedEntities();
                if (paths == null || paths.Length == 0 || sender is not Editor ed)
                {
                    return;
                }

                ObjectId[] ids = paths[0].GetObjectIds();
                if (ids.Length == 0)
                {
                    return;
                }

                Database db = ed.Document.Database;
                using Transaction tr = db.TransactionManager.StartOpenCloseTransaction();
                if (tr.GetObject(ids[0], OpenMode.ForRead) is Entity ent)
                {
                    string? tip = Tip(ent, e.Context.ComputedPoint, db, tr);
                    if (tip != null)
                    {
                        e.AppendToolTipText(tip);
                    }
                }

                tr.Commit();
            }
            catch (System.Exception)
            {
                // a tooltip must never disturb the cursor
            }
        }

        private static string? Tip(Entity ent, Point3d at, Database db, Transaction tr)
        {
            RoadSpec? spec = RoadCommands.ReadSpec(ent);
            if (spec != null)
            {
                return "Saobraćajnica: " + RoadCommands.Describe(spec) + ".\nIzmjena: Uredi saobraćajnicu (SDPSAOBRACAJNICAUREDI).";
            }

            (string OwnerId, string Role)? aux = ElementStore.ReadAux(ent);
            if (!aux.HasValue || aux.Value.OwnerId != RoadCommands.NetOwner || !aux.Value.Role.StartsWith("rub:", StringComparison.Ordinal) || ent is not Polyline pl)
            {
                return null;
            }

            string[] parts = aux.Value.Role.Split(':');
            int level = int.Parse(parts[1], CultureInfo.InvariantCulture);
            double bike = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
            Point3d close = pl.GetClosestPointTo(at, false);
            int seg = (int)Math.Floor(pl.GetParameterAtPoint(close));
            if (seg >= pl.NumberOfVertices)
            {
                seg = pl.NumberOfVertices - 1;
            }

            if (Math.Abs(pl.GetBulgeAt(seg)) < 1e-9)
            {
                return null;
            }

            double k = ProjectStore.ReadOrDefault(db, tr).Scale.DrawingUnitsPerMeter;
            double r = pl.GetArcSegment2dAt(seg).Radius / k;
            switch ((RoadLevel)level)
            {
                case RoadLevel.Carriageway:
                    return bike > 0
                        ? "Ivica kolovoza: efektivni radijus R = " + Bs.Meters(r) + "\nStvarni radijus ivičnjaka = " + Bs.Meters(Math.Max(0, r - bike)) + " (biciklistička staza " + Bs.Meters(bike) + ")"
                        : "Ivičnjak uz kolovoz: stvarni radijus R = " + Bs.Meters(r) + "\nEfektivni radijus = " + Bs.Meters(r) + " (nema biciklističke staze)";
                case RoadLevel.Bike:
                    return "Ivičnjak: stvarni radijus R = " + Bs.Meters(r) + "\nEfektivni radijus (ivica kolovoza) = " + Bs.Meters(r + bike);
                case RoadLevel.Kerb:
                    return "Ivičnjak 15 cm, strana trotoara: R = " + Bs.Meters(r);
                case RoadLevel.Sidewalk:
                    return "Vanjska ivica trotoara: R = " + Bs.Meters(r);
                default:
                    return "Ivičnjak 10 cm uz trotoar: R = " + Bs.Meters(r);
            }
        }
    }
}
