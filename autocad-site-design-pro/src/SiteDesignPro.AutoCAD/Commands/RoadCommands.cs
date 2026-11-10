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
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using SiteDesignPro.Core.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SiteDesignPro.AutoCAD.Commands
{
    /// <summary>
    /// Streets and roads of the drawing as one network: streets (road.street) and the simple roads (access road,
    /// driveway, service route) are elements whose axis is drawn; their carriageway, bike lanes, kerbs, sidewalks, lane
    /// lines and arrows are drawn together, so roads never overlap and every junction gets rounded kerbs. Pedestrian
    /// crossings are elements (road.pedestrian_crossing).
    /// </summary>
    public sealed class RoadCommands
    {
        internal const string NetOwner = "roadnet";
        internal const string LayerName = "Saobraćajnice";
        private const string LegacyApp = "SITEDESIGNPRO_CESTA";
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

        /// <summary>
        /// Ulica: the cross-section in a window (direction, lanes, lane width, sidewalks, bike lanes, kerb radius, arrows),
        /// then the axis is drawn or picked; the street is an element (road.street) and joins all roads of the drawing.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPULICA", CommandFlags.Modal)]
        public void Street() => Guarded(() => CommandContext.RunMajor("SDPULICA", "Ulica (SDPULICA)", ctx =>
        {
            EnsureUnits(ctx);
            RoadSpec spec = RoadSpec.FromParameterString(UI.Preferences.Current.Default(PrefKey, new RoadSpec().ToParameterString()));
            RoadSpec? chosen = CommandContext.Interactive ? UI.RoadDialog.Ask(spec, "Ulica", UnitText(ctx)) : AskText(ctx, spec);
            if (chosen == null)
            {
                throw new CommandCancelled();
            }

            foreach (string note in chosen.Normalize())
            {
                ctx.Write(note);
            }

            UI.Preferences.Current.RememberDefault(PrefKey, chosen.ToParameterString());
            ElementType type = ctx.Catalog.RequireType(RoadElements.StreetType);
            ctx.Write("Ulica: " + Describe(chosen) + ".");
            string how = ctx.Keyword("Osovina ulice [Nacrtaj/Odaberi]", new[] { "Draw|Nacrtaj", "Select|Odaberi" }, "Draw");
            ObjectId axisId;
            if (how == "Draw")
            {
                ctx.Write("Nacrtajte osovinu (sredinu kolovoza) u smjeru vožnje desnih traka. Ulica se sama spaja s ulicama i cestama koje dodiruje.");
                axisId = PlacementCommands.DrawPolyline(ctx, false);
                if (axisId.IsNull)
                {
                    ctx.Write("Osovina nije nacrtana.");
                    return;
                }
            }
            else
            {
                axisId = ctx.Entity("Odaberite osovinu (polilinija, linija ili luk; može i postojeća cesta)", typeof(Curve));
            }

            var entity = (Entity)ctx.Tr.GetObject(axisId, OpenMode.ForRead);
            ManagedElement? existing = ElementStore.IsManaged(entity) ? ctx.Svc.Read(entity) : null;
            if (existing?.Element != null)
            {
                if (!RoadElements.IsNetworkRoad(existing.Type))
                {
                    ctx.Write("Odabrani objekat je element druge vrste (" + existing.Type?.Name + "); odaberite osovinu ili cestu.");
                    return;
                }

                ctx.Svc.Update(existing, AsStreet(existing.Element, chosen));
                ctx.Write("Cesta " + existing.Element.Label + " je postala ulica s ovim presjekom.");
            }
            else if (PlacementCommands.ConvertCurves(ctx, type, new[] { axisId }, Params(chosen)) == 0)
            {
                return;
            }

            Rebuild(ctx, true);
        }));

        /// <summary>Changes the cross-section of a street (or turns a simple road into a street) in the same window.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPULICAUREDI", CommandFlags.Modal)]
        public void EditStreet() => Guarded(() => CommandContext.Run("SDPULICAUREDI", ctx =>
        {
            List<ManagedElement> roads = ctx.Svc.ReadAll().Where(m => m.Element != null && RoadElements.IsNetworkRoad(m.Type)).ToList();
            if (roads.Count == 0)
            {
                ctx.Write("U crtežu nema ulica ni cesta. Nova ulica: Ulica (SDPULICA).");
                return;
            }

            Point3d? pick = ctx.Point("Kliknite ulicu ili cestu");
            if (!pick.HasValue)
            {
                return;
            }

            (ManagedElement? road, RoadSpec? spec, _, _, double dist) = Nearest(ctx, roads, pick.Value);
            if (road?.Element == null || spec == null || dist > Math.Max(spec.Extent(RoadLevel.Edge, true), spec.Extent(RoadLevel.Edge, false)) + 2)
            {
                ctx.Write("Na tom mjestu nema ulice ni ceste.");
                return;
            }

            bool street = road.Type!.Id == RoadElements.StreetType;
            ctx.Write((street ? "Ulica " : road.Type.Name + " ") + road.Element.Label + ": " + Describe(spec) + ".");
            if (!street)
            {
                ctx.Write("Ova cesta je zadana samo širinom; u prozoru joj možete dati trake, trotoare i staze (postaje ulica).");
            }

            RoadSpec? chosen = CommandContext.Interactive ? UI.RoadDialog.Ask(spec, "Uredi ulicu " + road.Element.Label, UnitText(ctx)) : AskText(ctx, spec);
            if (chosen == null)
            {
                throw new CommandCancelled();
            }

            foreach (string note in chosen.Normalize())
            {
                ctx.Write(note);
            }

            ctx.Svc.Update(road, AsStreet(road.Element, chosen));
            UI.Preferences.Current.RememberDefault(PrefKey, chosen.ToParameterString());
            Rebuild(ctx, true);
        }));

        /// <summary>Redraws carriageways, kerbs, sidewalks, lane lines and arrows of all streets and roads.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPCESTEOSVJEZI", CommandFlags.Modal)]
        public void RefreshRoads() => Guarded(() => CommandContext.Run("SDPCESTEOSVJEZI", ctx => Rebuild(ctx, true)));

        /// <summary>Quiet redraw started automatically after a street or road was changed, moved, copied or erased.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPCESTEAUTO", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void AutoRefresh() => Guarded(() => CommandContext.Run("SDPCESTEAUTO", ctx => Rebuild(ctx, false)));

        /// <summary>
        /// Pedestrian crossing as an element (road.pedestrian_crossing): white 0,50 m bars with 0,50 m gaps across all
        /// lanes; on a street or road it turns itself and takes the number and width of the lanes.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPPJESACKIPRIJELAZ", CommandFlags.Modal)]
        public void Crossing() => Guarded(() => CommandContext.Run("SDPPJESACKIPRIJELAZ", ctx =>
        {
            double k = ctx.K;
            Point3d? at = ctx.Point("Mjesto pješačkog prijelaza (klik na ulicu ili cestu)");
            if (!at.HasValue)
            {
                return;
            }

            List<ManagedElement> roads = ctx.Svc.ReadAll().Where(m => m.Element != null && RoadElements.IsNetworkRoad(m.Type)).ToList();
            int lanes = 2;
            double lw = 3.0;
            Vec2 centre = new Vec2(at.Value.X / k, at.Value.Y / k);
            Vec2 dir;
            bool onRoad = false;
            (ManagedElement? road, RoadSpec? spec, Vec2 foot, Vec2 roadDir, double dist) = roads.Count > 0 ? Nearest(ctx, roads, at.Value) : (null, null, centre, new Vec2(1, 0), double.MaxValue);
            if (spec != null && dist <= Math.Max(spec.CarriagewayLeft, spec.CarriagewayRight) + 1.0)
            {
                onRoad = true;
                lanes = spec.TotalLanes;
                lw = spec.LaneWidth;
                dir = roadDir;
                centre = foot + (dir.Perp() * ((spec.CarriagewayLeft - spec.CarriagewayRight) / 2));
                ctx.Write((road?.Element?.Label ?? string.Empty) + ": " + Bs.Count(lanes, "traka", "trake", "traka") + " × " + Bs.Num(lw, 2) + " m; prijelaz ide okomito na osovinu, preko cijelog kolovoza.");
            }
            else
            {
                double a = ctx.Angle("Smjer ulice (drugi klik ili ugao)", at.Value, 0);
                dir = new Vec2(Math.Cos(a), Math.Sin(a));
            }

            lanes = Math.Min(6, ctx.Integer("Broj traka", lanes, 1));
            lw = ctx.Number("Širina saobraćajne trake (m)", lw);
            double length = ctx.Number("Širina prijelaza – dužina pruga (m)", 4.0);
            if (lw < 1 || length < 1)
            {
                ctx.Write("Širina trake i prijelaza moraju biti najmanje 1 m.");
                return;
            }

            // the bars (0,50 m, gaps 0,50 m) cover the carriageway and are centred on it; the outline runs exactly
            // around them, its first edge along the road, so the stripes lie along the road as in SketchUp
            int bars = Math.Max(1, (int)Math.Floor(((lanes * lw) + 0.5) / 1.0 + 1e-9));
            double across = (bars * 0.5) + ((bars - 1) * 0.5);
            Vec2 side = dir.Perp();
            Vec2 c0 = centre - (dir * (length / 2)) - (side * (across / 2));
            var ring = new Ring(new List<Vec3>
            {
                (c0 * k).WithZ(0), ((c0 + (dir * length)) * k).WithZ(0), ((c0 + (dir * length) + (side * across)) * k).WithZ(0), ((c0 + (side * across)) * k).WithZ(0),
            });
            ElementType type = ctx.Catalog.RequireType(RoadElements.CrossingType);
            SiteElement crossing = ctx.NewElement(type, new PolygonGeometry(ring), ctx.ExistingElements());
            crossing.Parameters["stripeWidth"] = ParamValue.Of(0.5);
            crossing.Parameters["gapWidth"] = ParamValue.Of(0.5);
            ctx.Svc.Create(crossing);
            if (roads.Count > 0)
            {
                Rebuild(ctx, false); // lane lines and arrows stop at the crossing
            }

            ctx.Write("Pješački prijelaz " + crossing.Label + ": " + bars + " pruga 0,50 m s razmakom 0,50 m preko " + Bs.Num(lanes * lw, 2) + " m kolovoza, dužina pruga " + Bs.Num(length, 2) + " m" +
                      (onRoad ? string.Empty : " (slobodno postavljen)") + ". Pomjera se i uređuje kao svaki element; linije traka se na njemu prekidaju.");
        }));

        // ------------------------------------------------------------------ network

        /// <summary>Redraws the road network from another command (the watcher stays on afterwards).</summary>
        internal static void RebuildNow(CommandContext ctx)
        {
            bool was = Busy;
            try
            {
                Rebuild(ctx, false);
            }
            finally
            {
                Busy = was;
            }
        }

        internal static RoadNetworkResult? Rebuild(CommandContext ctx, bool verbose)
        {
            Busy = true;
            MigrateLegacy(ctx);
            List<ManagedElement> managed = ctx.Svc.ReadAll().Where(m => m.Element != null && m.Type != null).ToList();
            List<ManagedElement> roads = managed.Where(m => RoadElements.IsNetworkRoad(m.Type)).ToList();
            if (RoundAxes(ctx, roads) > 0)
            {
                managed = ctx.Svc.ReadAll().Where(m => m.Element != null && m.Type != null).ToList();
                roads = managed.Where(m => RoadElements.IsNetworkRoad(m.Type)).ToList();
            }
            foreach (ManagedElement m in roads)
            {
                // drawings made before the road network: the road was a wide band with its own edges
                ctx.Svc.EraseAux(m.Element!.Id, "edge");
                if (ctx.Tr.GetObject(m.ObjectId, OpenMode.ForRead) is Polyline pl && Math.Abs(pl.ConstantWidth) > 1e-9)
                {
                    pl.UpgradeOpen();
                    pl.ConstantWidth = 0;
                }
            }

            // a locked road layer is unlocked for the redraw and locked again afterwards
            ObjectId layerId = RoadLayer(ctx);
            var layerRecord = (LayerTableRecord)ctx.Tr.GetObject(layerId, OpenMode.ForWrite);
            bool wasLocked = layerRecord.IsLocked;
            layerRecord.IsLocked = false;
            try
            {
                return Redraw(ctx, managed.Select(m => m.Element!).ToList(), roads.Count, verbose);
            }
            finally
            {
                layerRecord.IsLocked = wasLocked;
            }
        }

        /// <summary>
        /// Rounds the sharp bends of the road axes (between two straight segments) with the radius of the road, so the
        /// drawn axis follows the curved road. Returns the number of axes changed.
        /// </summary>
        private static int RoundAxes(CommandContext ctx, List<ManagedElement> roads)
        {
            int changed = 0;
            foreach (ManagedElement m in roads)
            {
                if (m.IsCopy || !(ctx.Tr.GetObject(m.ObjectId, OpenMode.ForRead) is Polyline pl) || pl.NumberOfVertices < 3)
                {
                    continue;
                }

                RoadSpec? spec = RoadElements.Spec(m.Element!, m.Type!, ctx.K);
                if (spec == null)
                {
                    continue;
                }

                var pts = new List<Vec2>();
                var bulges = new List<double>();
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    Point2d p = pl.GetPoint2dAt(i);
                    pts.Add(new Vec2(p.X, p.Y));
                    bulges.Add(pl.GetBulgeAt(i));
                }

                if (Math.Abs(pl.Elevation) > 1e-9)
                {
                    // all road lines lie at Z = 0,00; heights come from the elevation marks
                    if (!pl.IsWriteEnabled)
                    {
                        pl.UpgradeOpen();
                    }

                    pl.Elevation = 0;
                    changed++;
                }

                (List<Vec2> Points, List<double> Bulges)? rounded = RoadElements.RoundedAxis(pts, bulges, pl.Closed, spec, ctx.K);
                if (rounded == null)
                {
                    continue;
                }

                if (!pl.IsWriteEnabled)
                {
                    pl.UpgradeOpen();
                }

                int old = pl.NumberOfVertices;
                List<Vec2> np = rounded.Value.Points;
                for (int i = 0; i < np.Count; i++)
                {
                    pl.AddVertexAt(old + i, new Point2d(np[i].X, np[i].Y), rounded.Value.Bulges[i], 0, 0);
                }

                for (int i = old - 1; i >= 0; i--)
                {
                    pl.RemoveVertexAt(i);
                }

                changed++;
            }

            return changed;
        }

        private static RoadNetworkResult? Redraw(CommandContext ctx, List<SiteElement> elements, int roadCount, bool verbose)
        {
            EraseNetwork(ctx);
            RoadNetworkResult? res = roadCount == 0 ? null : RoadElements.Network(elements, ctx.Catalog, ctx.K);
            if (res == null)
            {
                if (verbose)
                {
                    ctx.Write("U crtežu nema ulica ni cesta.");
                }

                return null;
            }

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

            string bikeText = (res.EffectiveRadius - res.ActualRadius).ToString("0.###", CultureInfo.InvariantCulture) + ":" + res.ActualRadius.ToString("0.###", CultureInfo.InvariantCulture);
            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                (byte r, byte g, byte b, int t) = Fill[band.Key];
                foreach (RoadRegion region in band.Value)
                {
                    try
                    {
                        Hatch h = RegionHatch(region, k);
                        Add(h, "povrsina:" + (int)band.Key + ":" + bikeText, Color.FromRgb(r, g, b), t);
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

            foreach (RoadLoop ramp in res.Ramps)
            {
                try
                {
                    Hatch h = RegionHatch(new RoadRegion(ramp, new List<RoadLoop>(), 0), k);
                    Add(h, "rampa", Color.FromRgb(90, 140, 210), 20);
                    Add(LoopPolyline(ramp, k), "rampa:rub", null, 0);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    failed++;
                }
            }

            // elevation marks: the symbol (quartered circle, line, relative height above, absolute height below) and
            // the longitudinal slope between consecutive marks; slopes over 10 % are reported at once
            List<RoadLevelMark> marks = RoadElements.LevelMarks(elements, k);
            if (marks.Count > 0)
            {
                RoadLevelSettings levels = RoadLevelSettings.Read(ctx.Db, ctx.Tr);
                double th = 0.35 * k;
                foreach (RoadLevelMark m in marks)
                {
                    Point3d p0 = new Point3d(m.Position.X * k, m.Position.Y * k, 0);
                    double r = 0.25 * k;
                    Add(new Circle(p0, Vector3d.ZAxis, r), "kota", null, 0);
                    foreach (int q in new[] { 0, 2 })
                    {
                        var wedge = new Polyline();
                        double a0 = (q * Math.PI / 2) + (Math.PI / 2);
                        wedge.AddVertexAt(0, new Point2d(p0.X, p0.Y), 0, 0, 0);
                        wedge.AddVertexAt(1, new Point2d(p0.X + (r * Math.Cos(a0)), p0.Y + (r * Math.Sin(a0))), Math.Tan(Math.PI / 8), 0, 0);
                        wedge.AddVertexAt(2, new Point2d(p0.X + (r * Math.Cos(a0 + (Math.PI / 2))), p0.Y + (r * Math.Sin(a0 + (Math.PI / 2)))), 0, 0, 0);
                        wedge.Closed = true;
                        try
                        {
                            var hw = new Hatch();
                            hw.SetDatabaseDefaults(ctx.Db);
                            hw.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                            BlockTableRecord msp = (BlockTableRecord)ctx.Tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(ctx.Db), OpenMode.ForWrite);
                            ObjectId wid = msp.AppendEntity(wedge);
                            ctx.Tr.AddNewlyCreatedDBObject(wedge, true);
                            hw.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection(new[] { wid }));
                            hw.EvaluateHatch(true);
                            wedge.Erase();
                            Add(hw, "kota", null, 0);
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception)
                        {
                            failed++;
                        }
                    }

                    var line = new Line(new Point3d(p0.X - (0.6 * k), p0.Y, 0), new Point3d(p0.X + (3.2 * k), p0.Y, 0));
                    Add(line, "kota", null, 0);
                    Add(new DBText { Position = new Point3d(p0.X + (0.5 * k), p0.Y + (0.1 * k), 0), Height = th, TextString = LevelText(m.Value) }, "kota", null, 0);
                    if (levels.Defined)
                    {
                        Add(new DBText { Position = new Point3d(p0.X + (0.5 * k), p0.Y - (0.1 * k) - (th * 0.8), 0), Height = th * 0.7, TextString = Bs.Num(levels.Base + m.Value, 2) + " m n.v." }, "kota", null, 0);
                    }
                }

                List<RoadProfile> profiles = RoadElements.Profiles(elements, ctx.Catalog, k);
                foreach (RoadProfile prof in profiles)
                {
                    for (int i = 1; i < prof.Points.Count; i++)
                    {
                        (double s0, double h0) = prof.Points[i - 1];
                        (double s1, double h1) = prof.Points[i];
                        if (s1 - s0 < 0.01)
                        {
                            continue;
                        }

                        double slope = Math.Abs(h1 - h0) / (s1 - s0);
                        (Vec2 at, Vec2 dir) = RoadLevels.PointAt(prof.Road.Axis, prof.Road.Closed, (s0 + s1) / 2);
                        double ang = Math.Atan2(dir.Y, dir.X);
                        if (ang > Math.PI / 2 || ang < -Math.PI / 2)
                        {
                            ang += Math.PI;
                        }

                        Vec2 off = at + (dir.Perp() * 0.6);
                        var t = new DBText { Position = new Point3d(off.X * k, off.Y * k, 0), Height = th * 0.8, Rotation = ang, TextString = "i = " + Bs.Num(slope * 100, 2) + " %" + (slope > RoadLevels.MaxRoadSlope + 1e-9 ? " > 10 %!" : string.Empty) };
                        Add(t, slope > RoadLevels.MaxRoadSlope + 1e-9 ? "nagib:previse" : "nagib", slope > RoadLevels.MaxRoadSlope + 1e-9 ? Color.FromRgb(230, 40, 40) : null, 0);
                    }
                }

                foreach (RoadSlope sl in RoadLevels.Slopes(profiles).Where(x => x.TooSteep))
                {
                    ctx.Write("UPOZORENJE: uzdužni nagib saobraćajnice " + sl.RoadName + " između stacionaža " + Bs.Num(sl.From, 2) + " i " + Bs.Num(sl.To, 2) + " m je " +
                              Bs.Num(sl.Slope * 100, 2) + " % (dozvoljeno najviše 10 %; garažne rampe 17 %).");
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
                ctx.Write(Bs.T("Ulice i ceste ({0}): kolovoz {1}, biciklističke staze {2}, trotoari {3}; {4} oznaka traka, {5} strelica; zaobljenih uglova raskrsnica: {6}; rampi za invalide: {7}.",
                    roadCount, Bs.SquareMeters(res.Area(RoadLevel.Carriageway), 1), Bs.SquareMeters(res.Area(RoadLevel.Bike), 1),
                    Bs.SquareMeters(res.Area(RoadLevel.Sidewalk), 1), res.Markings.Count, res.Arrows.Count, junctions, res.Ramps.Count));
                ctx.Write("Radijus ivičnjaka (stvarni) " + Bs.Meters(res.ActualRadius) + " = efektivni radijus (ivičnjak je na ivici kolovoza)" +
                          ". Radijus luka vidite kad kursor stoji na ivičnjaku. Nakon izmjene, pomjeranja ili brisanja ulice crtež se osvježava sam.");
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

        /// <summary>The road whose axis is closest to a point, with its cross-section, foot point and direction there (metres).</summary>
        private static (ManagedElement? Road, RoadSpec? Spec, Vec2 Foot, Vec2 Dir, double Dist) Nearest(CommandContext ctx, List<ManagedElement> roads, Point3d p)
        {
            double k = ctx.K;
            Vec2 q = new Vec2(p.X / k, p.Y / k);
            (ManagedElement?, RoadSpec?, Vec2, Vec2, double) best = (null, null, q, new Vec2(1, 0), double.MaxValue);
            foreach (ManagedElement m in roads)
            {
                RoadSpec? s = RoadElements.Spec(m.Element!, m.Type!, k);
                if (s == null)
                {
                    continue;
                }

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
                        best = (m, s, foot, ab.Normalized(), d);
                    }
                }
            }

            return best;
        }

        /// <summary>The cross-section as parameters of a street element.</summary>
        internal static Dictionary<string, ParamValue> Params(RoadSpec s) => new Dictionary<string, ParamValue>(StringComparer.Ordinal)
        {
            ["direction"] = ParamValue.Of(s.OneWay ? "one_way" : "two_way"),
            ["lanesLeft"] = ParamValue.Of((double)s.LanesLeft),
            ["lanesRight"] = ParamValue.Of((double)s.LanesRight),
            ["laneWidth"] = ParamValue.Of(s.LaneWidth),
            ["sidewalkLeft"] = ParamValue.Of(s.SidewalkLeft),
            ["sidewalkRight"] = ParamValue.Of(s.SidewalkRight),
            ["bikeLeft"] = ParamValue.Of(s.BikeLeft),
            ["bikeRight"] = ParamValue.Of(s.BikeRight),
            ["cornerRadius"] = ParamValue.Of(s.CornerRadius),
            ["arrows"] = ParamValue.Of(s.Arrows),
            ["arrowSpacing"] = ParamValue.Of(s.ArrowSpacing),
            ["median"] = ParamValue.Of(s.Median),
        };

        /// <summary>The element as a street with the given cross-section (a simple road becomes a street; label and data stay).</summary>
        private static SiteElement AsStreet(SiteElement e, RoadSpec s)
        {
            ElementType street = AcadEnv.Catalog.RequireType(RoadElements.StreetType);
            ParameterSet parameters = e.TypeId == RoadElements.StreetType ? e.Parameters.Clone() : street.CreateDefaultParameters();
            if (e.TypeId != RoadElements.StreetType && e.Parameters.Contains("material"))
            {
                parameters["material"] = e.Parameters["material"];
            }

            foreach (KeyValuePair<string, ParamValue> kv in Params(s))
            {
                parameters[kv.Key] = kv.Value;
            }

            return new SiteElement(e.Id, RoadElements.StreetType, e.Geometry)
            {
                Name = e.TypeId == RoadElements.StreetType ? e.Name : string.Empty,
                Label = e.Label,
                Parameters = parameters,
                Properties = new SortedDictionary<string, string>(e.Properties, StringComparer.Ordinal),
                Custom = e.Custom.Clone(),
                Revision = e.Revision,
                SourceHandle = e.SourceHandle,
                AssetId = e.AssetId,
            };
        }

        /// <summary>Axes of the first road version (cross-section stored on a plain polyline) become street elements.</summary>
        private static void MigrateLegacy(CommandContext ctx)
        {
            RXClass curveClass = RXObject.GetClass(typeof(Curve));
            var legacy = new List<(ObjectId Id, RoadSpec Spec)>();
            foreach (ObjectId id in ctx.Svc.ModelSpace(OpenMode.ForRead))
            {
                if (id.IsErased || !id.ObjectClass.IsDerivedFrom(curveClass))
                {
                    continue;
                }

                var c = (Curve)ctx.Tr.GetObject(id, OpenMode.ForRead);
                using ResultBuffer? rb = c.GetXDataForApplication(LegacyApp);
                TypedValue[]? v = rb?.AsArray();
                if (v != null && v.Length >= 2 && v[1].Value is string text && !ElementStore.IsManaged(c))
                {
                    legacy.Add((id, RoadSpec.FromParameterString(text)));
                }
            }

            if (legacy.Count == 0)
            {
                return;
            }

            ElementType street = ctx.Catalog.RequireType(RoadElements.StreetType);
            foreach ((ObjectId id, RoadSpec spec) in legacy)
            {
                var c = (Curve)ctx.Tr.GetObject(id, OpenMode.ForWrite);
                c.XData = new ResultBuffer(new TypedValue((int)DxfCode.ExtendedDataRegAppName, LegacyApp));
                PlacementCommands.ConvertCurves(ctx, street, new[] { id }, Params(spec));
            }

            ctx.Write("Osovine iz prve verzije (" + legacy.Count + ") pretvorene su u ulice.");
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

        // ------------------------------------------------------------------ input and units

        /// <summary>An empty drawing without a project gets the road scale 1 m = 100 cm (drawing unit centimetre).</summary>
        private static void EnsureUnits(CommandContext ctx)
        {
            // a drawing that already has elements keeps its scale (they were drawn in it)
            if (ProjectStore.Read(ctx.Db, ctx.Tr) != null || ctx.Svc.ReadAll().Any(m => m.Element != null))
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

        /// <summary>Base elevation of the roads (m above sea level) and the terrain mode: flat or uneven.</summary>
        [CommandMethod("SITEDESIGNPRO", "SDPVISINECESTA", CommandFlags.Modal)]
        public void RoadLevelBase() => Guarded(() => CommandContext.Run("SDPVISINECESTA", ctx =>
        {
            RoadLevelSettings s = RoadLevelSettings.Read(ctx.Db, ctx.Tr);
            string mode = ctx.Keyword("Teren [Ravan/Neravan]", new[] { "Ravan", "Neravan" }, s.Flat ? "Ravan" : "Neravan");
            s.Flat = mode == "Ravan";
            s.Base = ctx.Number(s.Flat ? "Visina saobraćajnica (m n.v.)" : "Osnovna visina saobraćajnica (m n.v.)", s.Base, true, true);
            s.Write(ctx.Db, ctx.Tr);
            ctx.Write(s.Flat
                ? "Potpuno ravan teren: sve saobraćajnice su na " + Bs.Num(s.Base, 2) + " m n.v."
                : "Neravan teren: osnovna visina " + Bs.Num(s.Base, 2) + " m n.v.; visine dijelova saobraćajnica zadajte kotama (Kota saobraćajnice), npr. −1,00 znači " + Bs.Num(s.Base - 1, 2) + " m n.v.");
            Rebuild(ctx, true);
        }));

        /// <summary>
        /// Elevation mark on a road: height relative to the base elevation of the roads (the whole width of the road at
        /// that place, with the sidewalks and bike lanes). Heights are linear between the marks of a road.
        /// </summary>
        [CommandMethod("SITEDESIGNPRO", "SDPKOTACESTE", CommandFlags.Modal)]
        public void RoadLevelMark() => Guarded(() => CommandContext.Run("SDPKOTACESTE", ctx =>
        {
            RoadLevelSettings s = RoadLevelSettings.Read(ctx.Db, ctx.Tr);
            if (!s.Defined)
            {
                ctx.Write("Osnovna visina saobraćajnica još nije zadana; zadajte je sada (može se promijeniti s Visine saobraćajnica).");
                s.Flat = false;
                s.Base = ctx.Number("Osnovna visina saobraćajnica (m n.v.)", 0, true, true);
                s.Write(ctx.Db, ctx.Tr);
            }
            else if (s.Flat)
            {
                s.Flat = false;
                s.Write(ctx.Db, ctx.Tr);
                ctx.Write("Teren je prebačen na neravan: kote određuju visine saobraćajnica.");
            }

            ElementType type = ctx.Catalog.RequireType(RoadElements.LevelMarkType);
            double last = double.TryParse(UI.Preferences.Current.Default("roadLevel", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out double lv) ? lv : 0;
            while (true)
            {
                Point3d? at = ctx.Point("Mjesto kote na saobraćajnici (Enter za kraj)", null, true);
                if (!at.HasValue)
                {
                    break;
                }

                double value = ctx.Number("Visina u odnosu na osnovnu " + Bs.Num(s.Base, 2) + " m n.v. (npr. -1,00 ili 5,00)", last, true, true);
                last = value;
                UI.Preferences.Current.RememberDefault("roadLevel", value.ToString(CultureInfo.InvariantCulture));
                SiteElement e = ctx.NewElement(type, new PointGeometry(new Vec3(at.Value.X, at.Value.Y, 0), 0), ctx.ExistingElements());
                e.Parameters["level"] = ParamValue.Of(value);
                ctx.Svc.Create(e);
                ctx.Write("Kota " + LevelText(value) + " = " + Bs.Num(s.Base + value, 2) + " m n.v.");
            }

            Rebuild(ctx, false);
        }));

        /// <summary>Relative height as written above the line of the symbol: +5,00 / −1,00 / ±0,00.</summary>
        internal static string LevelText(double v) => Math.Abs(v) < 0.005 ? "±0,00" : (v > 0 ? "+" : "−") + Bs.Num(Math.Abs(v), 2);

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
                // the road commands redraw themselves; undo restores the drawn roads together with the axes
                if (RoadCommands.Busy || (running != null && (UndoCommands.Contains(running) || running.StartsWith("SDPCESTE", StringComparison.OrdinalIgnoreCase))))
                {
                    return;
                }

                if (o is not Entity ent)
                {
                    return;
                }

                // a street, a road or a pedestrian crossing was added, changed, moved, copied or erased
                (string Id, string Type)? key = ElementStore.ReadKey(ent);
                if (key.HasValue && (key.Value.Type == RoadElements.CrossingType || key.Value.Type == RoadElements.HatchedAreaType || key.Value.Type == RoadElements.LevelMarkType || RoadElements.IsNetworkRoad(AcadEnv.Catalog.FindType(key.Value.Type))))
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
            doc.SendStringToExecute("_.SDPCESTEAUTO ", true, false, false);
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
            (string Id, string Type)? key = ElementStore.ReadKey(ent);
            if (key.HasValue && key.Value.Type == RoadElements.StreetType && ElementStore.Read(ent, tr) is ElementRecord rec)
            {
                ElementType street = AcadEnv.Catalog.RequireType(RoadElements.StreetType);
                var probe = new SiteElement(rec.Id, rec.TypeId, new PathGeometry(new Ring(new List<Vec3> { new Vec3(0, 0, 0), new Vec3(1, 0, 0) }), false)) { Parameters = rec.Parameters };
                RoadSpec? spec = RoadElements.Spec(probe, street);
                return spec == null ? null : "Ulica " + rec.Label + ": " + RoadCommands.Describe(spec) + ".\nIzmjena: Uredi ulicu (SDPULICAUREDI) ili paleta Svojstva.";
            }

            (string OwnerId, string Role)? aux = ElementStore.ReadAux(ent);
            if (!aux.HasValue || aux.Value.OwnerId != RoadCommands.NetOwner)
            {
                return null;
            }

            string[] parts = aux.Value.Role.Split(':');
            bool line = parts[0] == "rub";
            if ((!line && parts[0] != "povrsina") || parts.Length < 2)
            {
                return null;
            }

            int level = int.Parse(parts[1], CultureInfo.InvariantCulture);
            double bike = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
            double actual = parts.Length > 3 ? double.Parse(parts[3], CultureInfo.InvariantCulture) : 0;
            double k = ProjectStore.ReadOrDefault(db, tr).Scale.DrawingUnitsPerMeter;
            double? radius = null;
            if (ent is Polyline pl)
            {
                Point3d close = pl.GetClosestPointTo(at, false);
                int seg = Math.Min((int)Math.Floor(pl.GetParameterAtPoint(close)), pl.NumberOfVertices - 1);
                if (Math.Abs(pl.GetBulgeAt(seg)) > 1e-9)
                {
                    radius = pl.GetArcSegment2dAt(seg).Radius / k;
                }
            }
            else if (ent is Hatch h)
            {
                // hatch of a band: the arc of its boundary nearest to the cursor, if it is within 2 m
                var p = new Point2d(at.X, at.Y);
                double best = 2.0 * k;
                for (int i = 0; i < h.NumberOfLoops; i++)
                {
                    HatchLoop loop = h.GetLoopAt(i);
                    if (!loop.IsPolyline)
                    {
                        continue;
                    }

                    BulgeVertexCollection v = loop.Polyline;
                    for (int j = 0; j < v.Count - 1; j++)
                    {
                        if (Math.Abs(v[j].Bulge) < 1e-9)
                        {
                            continue;
                        }

                        var arc = new CircularArc2d(v[j].Vertex, v[j + 1].Vertex, v[j].Bulge, false);
                        double d = arc.GetDistanceTo(p);
                        if (d < best)
                        {
                            best = d;
                            radius = arc.Radius / k;
                        }
                    }
                }
            }

            if (!radius.HasValue)
            {
                return null;
            }

            double r = radius.Value;
            if (!line)
            {
                // which edge of the band the arc is: the kerb line (carriageway edge) carries the actual radius
                string what = (RoadLevel)level switch
                {
                    RoadLevel.Carriageway => "Ivičnjak uz kolovoz",
                    RoadLevel.Kerb => "Ivičnjak 15 cm, stražnja strana",
                    RoadLevel.Bike => "Biciklistička staza",
                    RoadLevel.Sidewalk => "Trotoar",
                    _ => "Ivičnjak 10 cm",
                };
                string kind = Math.Abs(r - actual) < 0.05 ? " (ivičnjak – stvarni = efektivni radijus)" : string.Empty;
                return what + ": luk R = " + Bs.Meters(r) + kind + "\nRaskrsnice: radijus ivičnjaka " + Bs.Meters(actual) + " (efektivni = stvarni)";
            }

            switch ((RoadLevel)level)
            {
                case RoadLevel.Carriageway:
                    return "Ivičnjak uz kolovoz: stvarni radijus R = " + Bs.Meters(r) + "\nEfektivni radijus (ivica kolovoza) = " + Bs.Meters(r);
                case RoadLevel.Kerb:
                    return "Ivičnjak 15 cm, stražnja strana: R = " + Bs.Meters(r);
                case RoadLevel.Bike:
                    return "Vanjska ivica biciklističke staze: R = " + Bs.Meters(r);
                case RoadLevel.Sidewalk:
                    return "Vanjska ivica trotoara: R = " + Bs.Meters(r);
                default:
                    return "Ivičnjak 10 cm uz trotoar: R = " + Bs.Meters(r);
            }
        }
    }
}
