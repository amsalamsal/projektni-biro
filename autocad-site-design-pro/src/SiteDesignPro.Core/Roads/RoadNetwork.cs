using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>Closed loop of a region boundary; segment i runs from point i to point i+1 with bulge i (0 = straight).</summary>
    /// <summary>
    /// A kerb ramp on its kerb line: the middle of the lowered kerb, the direction along the kerb, the direction into the
    /// sidewalk and the depth over which the surface rises to the sidewalk.
    /// </summary>
    public sealed class RoadRampFrame
    {
        public RoadRampFrame(Vec2 foot, Vec2 along, Vec2 inward, double depth)
        {
            Foot = foot;
            Along = along;
            Inward = inward;
            Depth = depth;
        }

        public Vec2 Foot { get; }

        public Vec2 Along { get; }

        public Vec2 Inward { get; }

        public double Depth { get; }
    }

    public sealed class RoadLoop
    {
        public RoadLoop(List<Vec2> points, List<double> bulges)
        {
            Points = points;
            Bulges = bulges;
        }

        public List<Vec2> Points { get; }

        public List<double> Bulges { get; }

        /// <summary>Radius of segment i, or 0 for a straight one.</summary>
        public double Radius(int i)
        {
            double b = Bulges[i];
            if (Math.Abs(b) < 1e-9)
            {
                return 0;
            }

            double chord = Points[i].DistanceTo(Points[(i + 1) % Points.Count]);
            double theta = 4 * Math.Atan(Math.Abs(b));
            return chord / (2 * Math.Sin(theta / 2));
        }

        /// <summary>Radii of the arc segments of this loop.</summary>
        public IEnumerable<double> ArcRadii() => Enumerable.Range(0, Points.Count).Select(Radius).Where(r => r > 0);
    }

    /// <summary>Area to hatch: an outer loop with holes.</summary>
    public sealed class RoadRegion
    {
        public RoadRegion(RoadLoop outer, List<RoadLoop> holes, double area)
        {
            Outer = outer;
            Holes = holes;
            Area = area;
        }

        public RoadLoop Outer { get; }

        public List<RoadLoop> Holes { get; }

        public double Area { get; }
    }

    /// <summary>One dash (or a solid run) of a lane marking.</summary>
    public sealed class RoadMarking
    {
        public RoadMarking(string roadId, List<Vec2> points, bool centre)
        {
            RoadId = roadId;
            Points = points;
            Centre = centre;
        }

        public string RoadId { get; }

        public List<Vec2> Points { get; }

        /// <summary>Line between the two directions (otherwise between lanes of one direction).</summary>
        public bool Centre { get; }
    }

    /// <summary>Direction arrow painted in a lane.</summary>
    public sealed class RoadArrow
    {
        public RoadArrow(string roadId, Vec2 position, double angle)
        {
            RoadId = roadId;
            Position = position;
            Angle = angle;
        }

        public string RoadId { get; }

        public Vec2 Position { get; }

        /// <summary>Driving direction (radians).</summary>
        public double Angle { get; }

        /// <summary>Outline of a 5 m straight arrow pointing along +X, centred on the origin (metres).</summary>
        public static IReadOnlyList<Vec2> Shape { get; } = new[]
        {
            new Vec2(-2.5, -0.09), new Vec2(0.6, -0.09), new Vec2(0.6, -0.32), new Vec2(2.5, 0), new Vec2(0.6, 0.32), new Vec2(0.6, 0.09), new Vec2(-2.5, 0.09),
        };

        public List<Vec2> Outline() => Shape.Select(p => Position + p.Rotate(Angle)).ToList();
    }

    public sealed class RoadNetworkResult
    {
        /// <summary>Areas to hatch per level: the carriageway, and the band each further level adds around the previous one.</summary>
        public Dictionary<RoadLevel, List<RoadRegion>> Bands { get; } = new Dictionary<RoadLevel, List<RoadRegion>>();

        /// <summary>Boundary lines of each level (the carriageway edge, kerb lines, sidewalk edges).</summary>
        public Dictionary<RoadLevel, List<RoadLoop>> Outlines { get; } = new Dictionary<RoadLevel, List<RoadLoop>>();

        public List<RoadMarking> Markings { get; } = new List<RoadMarking>();

        public List<RoadArrow> Arrows { get; } = new List<RoadArrow>();

        /// <summary>Axes after joining (ends pulled to the axis of the road they meet), by road id.</summary>
        public Dictionary<string, List<Vec2>> Axes { get; } = new Dictionary<string, List<Vec2>>();

        public List<string> Notes { get; } = new List<string>();

        /// <summary>Accessible kerb ramps 120 cm at the start of the straight part after each junction corner.</summary>
        public List<RoadLoop> Ramps { get; } = new List<RoadLoop>();

        /// <summary>Position of each ramp of <see cref="Ramps"/> on its kerb line (same order).</summary>
        public List<RoadRampFrame> RampFrames { get; } = new List<RoadRampFrame>();

        /// <summary>The rounded junction corners added to the road surface (metres), for checks.</summary>
        public List<List<Vec2>> Fillets { get; } = new List<List<Vec2>>();

        /// <summary>Kerb radius used at junctions (kerb line between road level and sidewalk).</summary>
        public double ActualRadius { get; set; }

        /// <summary>Radius of the carriageway edge at junctions (kerb radius plus the bike lane).</summary>
        public double EffectiveRadius { get; set; }

        public double Area(RoadLevel level) => Bands.TryGetValue(level, out List<RoadRegion>? r) ? r.Sum(x => x.Area) : 0;
    }

    /// <summary>
    /// Builds a road network from road axes and cross-sections: every level (carriageway, bike lanes, kerbs,
    /// sidewalks, edge strips) is the union of all roads, so roads never overlap, and every inner corner of a
    /// junction is rounded with the kerb radius (morphological closing around each junction). The carriageway edge, kerb line and
    /// sidewalk edges are concentric, so the effective radius follows the actual one. Lane markings stop before
    /// the junction; arrows are painted every 40 m (default) along each lane.
    /// </summary>
    public static class RoadNetwork
    {
        private const int Precision = 3; // millimetres
        private const double ArcTolerance = 0.003;
        private const double DashLength = 3.0;
        private const double DashGap = 3.0;

        public static RoadNetworkResult Build(IReadOnlyList<RoadSpec> input) => Build(input, null);

        /// <summary>
        /// Builds the network; <paramref name="noMarking"/> are areas (pedestrian crossings, metres) where lane lines
        /// and arrows are left out.
        /// </summary>
        public static RoadNetworkResult Build(IReadOnlyList<RoadSpec> input, IEnumerable<IReadOnlyList<Vec2>>? noMarking) => Build(input, noMarking, null);

        /// <summary>
        /// Builds the network; <paramref name="crossings"/> are the pedestrian crossings (metres), which get an accessible
        /// ramp on both ends where there is a sidewalk.
        /// </summary>
        public static RoadNetworkResult Build(IReadOnlyList<RoadSpec> input, IEnumerable<IReadOnlyList<Vec2>>? noMarking, IEnumerable<IReadOnlyList<Vec2>>? crossings) =>
            Build(input, noMarking, crossings, null);

        /// <summary>
        /// Builds the network; <paramref name="rampMarks"/> are the points (metres) where the user placed an accessible ramp:
        /// on a sidewalk, a ramp on that side; on the carriageway, a ramp on both sides.
        /// </summary>
        public static RoadNetworkResult Build(IReadOnlyList<RoadSpec> input, IEnumerable<IReadOnlyList<Vec2>>? noMarking, IEnumerable<IReadOnlyList<Vec2>>? crossings, IEnumerable<Vec2>? rampMarks)
        {
            var result = new RoadNetworkResult();
            List<RoadSpec> roads = input.Where(r => r.Axis.Count >= 2).Select(Copy).ToList();
            foreach (RoadSpec r in input.Where(r => r.Axis.Count < 2))
            {
                result.Notes.Add("Osovina " + r.DisplayName + " ima manje od dvije tačke i preskočena je.");
            }

            if (roads.Count == 0)
            {
                return result;
            }

            // work in a local frame (state coordinates are large numbers)
            Vec2 origin = roads[0].Axis[0];
            foreach (RoadSpec r in roads)
            {
                r.Axis = Clean(r.Axis.Select(p => p - origin).ToList());
                r.Corners = r.Corners?.Select(p => p - origin).ToList();
                if (r.Closed && r.Axis.Count > 2 && r.Axis[0].DistanceTo(r.Axis[r.Axis.Count - 1]) < 1e-6)
                {
                    r.Axis.RemoveAt(r.Axis.Count - 1);
                }
            }

            roads = roads.Where(r => r.Axis.Count >= 2).ToList();
            JoinEnds(roads, result.Notes);
            foreach (RoadSpec r in roads)
            {
                r.Axis = Fillet(r);
                result.Axes[r.Id] = r.Axis.Select(p => p + origin).ToList();
            }

            double radius = roads.Max(r => r.CornerRadius);
            result.ActualRadius = radius;
            result.EffectiveRadius = radius; // the kerb stands at the carriageway edge

            // per-road carriageway and road surface up to the outer edge (lane lines and arrows use them)
            var bands = new Dictionary<RoadLevel, List<PathsD>>
            {
                [RoadLevel.Carriageway] = roads.Select(r => Band(r.Axis, r.Closed, r.Extent(RoadLevel.Carriageway, true), r.Extent(RoadLevel.Carriageway, false))).ToList(),
                [RoadLevel.Bike] = roads.Select(r => Band(r.Axis, r.Closed, r.Extent(RoadLevel.Bike, true), r.Extent(RoadLevel.Bike, false))).ToList(),
            };

            // the kerb line is the carriageway edge: all carriageways, inner junction corners rounded with the kerb radius
            var allSurface = new PathsD();
            foreach (PathsD p in bands[RoadLevel.Carriageway])
            {
                allSurface.AddRange(p);
            }

            PathsD raw = Union(allSurface);
            PathsD surface = raw;
            PathsD junctionCorners = JunctionCorners(bands[RoadLevel.Carriageway], radius);
            if (junctionCorners.Count > 0)
            {
                surface = Clipper.Union(raw, junctionCorners, FillRule.NonZero, Precision);
            }

            // the corner fillets the rounding added; every level is concentric with them. A real fillet lies between
            // two roads; pieces touching one road only are not used to guide the levels. An opening removes the
            // millimetre slivers the rounding leaves along straight edges (attached to a fillet they would carry the
            // kerb and sidewalk to the far end of the road).
            var carriageways = bands[RoadLevel.Carriageway].Select(Union).ToList();
            PathsD added = Clipper.Difference(surface, raw, FillRule.NonZero, Precision);
            added = Clipper.InflatePaths(Clipper.InflatePaths(added, -0.01, JoinType.Miter, EndType.Polygon, 10, Precision), 0.01, JoinType.Miter, EndType.Polygon, 10, Precision);
            List<PathsD> fillets = Union(added)
                .Where(f => Clipper.Area(f) > 0.02 && Clipper.Area(f) / Perimeter(f) > 0.02)
                .Select(f => new PathsD { f })
                .Where(f => carriageways.Count(c => Touches(f, c, 0.05)) >= 2)
                .ToList();
            foreach (PathsD f in fillets)
            {
                result.Fillets.AddRange(f.Select(path => path.Select(q => new Vec2(q.x, q.y) + origin).ToList()));
            }

            // outside the kerb line: rings of the kerb, bike lane, sidewalk and edging widths around the carriageway,
            // along the road sides that have them and around their corners (concentric with the kerb line)
            var grown = new Dictionary<long, PathsD>();
            PathsD Grow(double d)
            {
                long key = (long)Math.Round(d * 1000);
                if (key <= 0)
                {
                    return surface;
                }

                if (!grown.TryGetValue(key, out PathsD? g))
                {
                    g = Union(Clipper.InflatePaths(surface, d, JoinType.Round, EndType.Polygon, 2, Precision, ArcTolerance));
                    grown[key] = g;
                }

                return g;
            }

            PathsD Ring(double from, double to) => Clipper.Difference(Grow(to), Grow(from), FillRule.NonZero, Precision);

            // one ring per level and distinct distance from the kerb line: (level, from, to) for each road side
            var pieces = new Dictionary<RoadLevel, PathsD>
            {
                [RoadLevel.Kerb] = new PathsD(),
                [RoadLevel.Bike] = new PathsD(),
                [RoadLevel.Sidewalk] = new PathsD(),
                [RoadLevel.Edge] = new PathsD(),
            };
            RoadLevel[] outer = { RoadLevel.Kerb, RoadLevel.Bike, RoadLevel.Sidewalk, RoadLevel.Edge };
            RoadLevel Inner(RoadLevel level) => level == RoadLevel.Kerb ? RoadLevel.Carriageway : outer[Array.IndexOf(outer, level) - 1];
            foreach (RoadLevel level in outer)
            {
                var spans = new List<(double From, double To)>();
                foreach (RoadSpec r in roads)
                {
                    foreach (bool left in new[] { true, false })
                    {
                        double c = r.Extent(RoadLevel.Carriageway, left);
                        (double f, double t) = (r.Extent(Inner(level), left) - c, r.Extent(level, left) - c);
                        if (t - f > 1e-6 && !spans.Any(x => Math.Abs(x.From - f) < 1e-6 && Math.Abs(x.To - t) < 1e-6))
                        {
                            spans.Add((f, t));
                        }
                    }
                }

                foreach ((double from, double to) in spans)
                {
                    bool Has(RoadSpec r, bool left)
                    {
                        double c = r.Extent(RoadLevel.Carriageway, left);
                        return Math.Abs(r.Extent(Inner(level), left) - c - from) < 1e-6 && Math.Abs(r.Extent(level, left) - c - to) < 1e-6;
                    }

                    PathsD strips = Strips(roads, Has, (r, left) => r.Extent(Inner(level), left), (r, left) => r.Extent(level, left));
                    List<PathsD> next = fillets.Where(f => Touches(f, strips, from + 0.05)).ToList();
                    pieces[level].AddRange(Clipper.Intersect(Ring(from, to), Zone(strips, next, -1, to + 0.05), FillRule.NonZero, Precision));
                }
            }

            PathsD carriageway = surface;
            PathsD kerb = Clipper.Difference(Union(pieces[RoadLevel.Kerb]), surface, FillRule.NonZero, Precision);
            PathsD toKerb = Clipper.Union(surface, kerb, FillRule.NonZero, Precision);
            PathsD bike = Clipper.Difference(Union(pieces[RoadLevel.Bike]), toKerb, FillRule.NonZero, Precision);
            PathsD toBike = Clipper.Union(toKerb, bike, FillRule.NonZero, Precision);
            PathsD walk = Clipper.Difference(Union(pieces[RoadLevel.Sidewalk]), toBike, FillRule.NonZero, Precision);
            PathsD toWalk = Clipper.Union(toBike, walk, FillRule.NonZero, Precision);
            PathsD edge = Clipper.Difference(Union(pieces[RoadLevel.Edge]), toWalk, FillRule.NonZero, Precision);

            // accessible kerb ramps (120 cm) only where the user wants them: at the ends of each pedestrian crossing and at
            // the ramp marks; the ramp lowers the kerb and slopes up to the bike lane or sidewalk
            PathsD behindKerb = Clipper.Union(Clipper.Union(kerb, bike, FillRule.NonZero, Precision), walk, FillRule.NonZero, Precision);
            if (walk.Count > 0)
            {
                PathsD done = new PathsD();
                void AddRamps(IEnumerable<(PathD Ramp, RoadRampFrame Frame)> found)
                {
                    foreach ((PathD ramp, RoadRampFrame f) in found)
                    {
                        if (Clipper.Intersect(new PathsD { ramp }, done, FillRule.NonZero, Precision).Sum(q => Math.Abs(Clipper.Area(q))) > 0.1)
                        {
                            continue;
                        }

                        done.Add(ramp);
                        result.Ramps.Add(new RoadLoop(ramp.Select(q => new Vec2(q.x, q.y) + origin).ToList(), ramp.Select(_ => 0.0).ToList()));
                        result.RampFrames.Add(new RoadRampFrame(f.Foot + origin, f.Along, f.Inward, f.Depth));
                    }
                }

                foreach (IReadOnlyList<Vec2> area in crossings ?? Enumerable.Empty<IReadOnlyList<Vec2>>())
                {
                    if (area.Count >= 3)
                    {
                        AddRamps(RampsAt(area.Select(q => q - origin).ToList(), false, roads, behindKerb, walk));
                    }
                }

                foreach (Vec2 mark in rampMarks ?? Enumerable.Empty<Vec2>())
                {
                    AddRamps(RampsAt(new List<Vec2> { mark - origin }, true, roads, behindKerb, walk));
                }
            }

            // the ramps lower the kerb, bike lane and sidewalk: those surfaces leave them out
            PathsD rampArea = new PathsD(result.Ramps.Select(l => Positive(l.Points.Select(q => q - origin))));
            PathsD Lowered(PathsD band) => rampArea.Count == 0 ? band : Clipper.Difference(band, rampArea, FillRule.NonZero, Precision);

            var region = new Dictionary<RoadLevel, PathsD>
            {
                [RoadLevel.Carriageway] = carriageway,
                [RoadLevel.Kerb] = Lowered(kerb),
                [RoadLevel.Bike] = Lowered(bike),
                [RoadLevel.Sidewalk] = Lowered(walk),
                [RoadLevel.Edge] = edge,
            };
            var outline = new Dictionary<RoadLevel, PathsD>
            {
                [RoadLevel.Carriageway] = carriageway,
                [RoadLevel.Kerb] = toKerb,
                [RoadLevel.Bike] = toBike,
                [RoadLevel.Sidewalk] = toWalk,
                [RoadLevel.Edge] = Clipper.Union(toWalk, edge, FillRule.NonZero, Precision),
            };

            foreach (RoadLevel level in (RoadLevel[])Enum.GetValues(typeof(RoadLevel)))
            {
                result.Bands[level] = Regions(region[level], origin);
                bool draw = level == RoadLevel.Carriageway || region[level].Count > 0;
                result.Outlines[level] = draw ? outline[level].Select(p => FitArcs(p, origin)).ToList() : new List<RoadLoop>();
            }

            var gaps = new PathsD();
            foreach (IReadOnlyList<Vec2> area in noMarking ?? Enumerable.Empty<IReadOnlyList<Vec2>>())
            {
                if (area.Count >= 3)
                {
                    gaps.Add(Positive(area.Select(p => p - origin)));
                }
            }

            for (int i = 0; i < roads.Count; i++)
            {
                Paint(roads, i, bands, result, origin, gaps);
            }

            return result;
        }

        // ------------------------------------------------------------------ joining

        /// <summary>
        /// An axis that ends near another road (pointing at it, within its width plus 3 m) is extended to that road's
        /// axis, so a side street drawn up to the kerb or a little short still joins the main road.
        /// </summary>
        private static void JoinEnds(List<RoadSpec> roads, List<string> notes)
        {
            for (int i = 0; i < roads.Count; i++)
            {
                RoadSpec r = roads[i];
                if (r.Closed)
                {
                    continue;
                }

                foreach (bool atStart in new[] { true, false })
                {
                    List<Vec2> a = r.Axis;
                    Vec2 end = atStart ? a[0] : a[a.Count - 1];
                    Vec2 prev = atStart ? a[1] : a[a.Count - 2];
                    Vec2 dir = (end - prev).Normalized();
                    double best = double.MaxValue;
                    Vec2 hit = end;
                    RoadSpec? target = null;
                    for (int j = 0; j < roads.Count; j++)
                    {
                        if (j == i)
                        {
                            continue;
                        }

                        RoadSpec o = roads[j];
                        double reach = Math.Max(o.Extent(RoadLevel.Bike, true), o.Extent(RoadLevel.Bike, false)) + 3.0;
                        int segs = o.Closed ? o.Axis.Count : o.Axis.Count - 1;
                        for (int s = 0; s < segs; s++)
                        {
                            Vec2 p = o.Axis[s], q = o.Axis[(s + 1) % o.Axis.Count];
                            double? t = RaySegment(end, dir, p, q);
                            if (t.HasValue && t.Value > 1e-6 && t.Value <= reach && t.Value < best)
                            {
                                best = t.Value;
                                hit = end + (dir * t.Value);
                                target = o;
                            }
                        }
                    }

                    if (target != null)
                    {
                        if (atStart)
                        {
                            a[0] = hit;
                        }
                        else
                        {
                            a[a.Count - 1] = hit;
                        }

                        notes.Add("Cesta " + r.DisplayName + " je produžena " + Fmt(best) + " m do osovine ceste " + target.DisplayName + ".");
                    }
                }
            }
        }

        private static double? RaySegment(Vec2 o, Vec2 d, Vec2 p, Vec2 q)
        {
            Vec2 e = q - p;
            double den = d.Cross(e);
            if (Math.Abs(den) < 1e-12)
            {
                return null;
            }

            Vec2 w = p - o;
            double t = w.Cross(e) / den;
            double u = w.Cross(d) / den;
            return u >= -1e-9 && u <= 1 + 1e-9 ? t : (double?)null;
        }

        // ------------------------------------------------------------------ bands and closing

        /// <summary>
        /// Area within <paramref name="left"/> metres left and <paramref name="right"/> metres right of the axis, square
        /// ends: the polygon between the two offset lines (mitred joints), so curved edges are clean offsets of the
        /// axis without the small dents that pieces per segment leave.
        /// </summary>
        internal static PathsD Band(List<Vec2> axis, bool closed, double left, double right)
        {
            if (axis.Count < 2 || left + right < 1e-9)
            {
                return new PathsD();
            }

            if (!closed)
            {
                List<Vec2> l = OffsetLine(axis, false, left);
                List<Vec2> r = OffsetLine(axis, false, -right);
                r.Reverse();
                var poly = new PathD(l.Concat(r).Select(p => new PointD(p.X, p.Y)));
                if (Clipper.Area(poly) < 0)
                {
                    poly.Reverse();
                }

                // inverted lobes (an offset larger than the radius on the inside of a tight bend) are left out
                return Clipper.BooleanOp(ClipType.Union, new PathsD { poly }, null, FillRule.Positive, Precision);
            }

            List<Vec2> outer = OffsetLine(axis, true, left);
            List<Vec2> inner = OffsetLine(axis, true, -right);
            outer.RemoveAt(outer.Count - 1);
            inner.RemoveAt(inner.Count - 1);
            PathsD a = Clipper.BooleanOp(ClipType.Union, new PathsD { Positive(outer) }, null, FillRule.Positive, Precision);
            PathsD b = Clipper.BooleanOp(ClipType.Union, new PathsD { Positive(inner) }, null, FillRule.Positive, Precision);
            return Clipper.BooleanOp(ClipType.Xor, a, b, FillRule.NonZero, Precision);
        }

        private static PathsD Union(PathsD paths) => Clipper.BooleanOp(ClipType.Union, paths, null, FillRule.NonZero, Precision);

        private static PathD Positive(IEnumerable<Vec2> pts)
        {
            var path = new PathD(pts.Select(p => new PointD(p.X, p.Y)));
            if (Clipper.Area(path) < 0)
            {
                path.Reverse();
            }

            return path;
        }

        /// <summary>Rounds every inner (concave) corner with <paramref name="r"/>; outer corners and straight edges stay as they are.</summary>
        internal static PathsD Close(PathsD shape, double r)
        {
            if (r < 0.05 || shape.Count == 0)
            {
                return shape;
            }

            PathsD grown = Clipper.InflatePaths(shape, r, JoinType.Miter, EndType.Polygon, 10, Precision);
            return Clipper.InflatePaths(grown, -r, JoinType.Round, EndType.Polygon, 2, Precision, ArcTolerance);
        }

        /// <summary>Width of an accessible kerb ramp along the kerb.</summary>
        public const double RampWidth = 1.2;

        /// <summary>Flare on each side of the lowered kerb, where the kerb rises back to its height (at the kerb line).</summary>
        public const double RampFlare = 0.8;

        /// <summary>The same at the back of the ramp.</summary>
        public const double RampBackFlare = 0.65;

        /// <summary>
        /// Depth of a ramp behind the kerb line: the kerb (0,15 m) and up to 2,00 m of slope to the sidewalk (about 6,5 %),
        /// less if the sidewalk ends sooner.
        /// </summary>
        public const double RampDepth = RoadSpec.KerbWidth + 2.0;

        /// <summary>
        /// Ramps (1,20 m wide) at the two ends of a pedestrian crossing: across the kerb, bike lane and sidewalk of the
        /// road the crossing lies on, on the sides with a sidewalk.
        /// </summary>
        private static IEnumerable<(PathD Ramp, RoadRampFrame Frame)> RampsAt(List<Vec2> area, bool mark, List<RoadSpec> roads, PathsD behindKerb, PathsD walk)
        {
            Vec2 c = new Vec2(area.Average(q => q.X), area.Average(q => q.Y));
            RoadSpec? road = null;
            Vec2 foot = c, along = new Vec2(1, 0);
            double best = double.MaxValue;
            foreach (RoadSpec r in roads)
            {
                int m = r.Axis.Count;
                for (int i = 0; i < (r.Closed ? m : m - 1); i++)
                {
                    Vec2 a = r.Axis[i], b = r.Axis[(i + 1) % m];
                    Vec2 ab = b - a;
                    double l2 = ab.Dot(ab);
                    if (l2 < 1e-12)
                    {
                        continue;
                    }

                    double t = Math.Max(0, Math.Min(1, (c - a).Dot(ab) / l2));
                    Vec2 f = a + (ab * t);
                    double d = f.DistanceTo(c);
                    if (d < best)
                    {
                        best = d;
                        foot = f;
                        along = ab.Normalized();
                        road = r;
                    }
                }
            }

            if (road == null)
            {
                yield break;
            }

            Vec2 across = along.Perp();
            int at = (c - foot).Dot(across) >= 0 ? 1 : -1;
            double reachOut = mark ? Math.Max(road.Extent(RoadLevel.Edge, at > 0), road.Extent(RoadLevel.Carriageway, at > 0)) + 0.5 : road.Extent(RoadLevel.Carriageway, at > 0);
            if (best > Math.Max(reachOut, Math.Max(road.Extent(RoadLevel.Carriageway, true), road.Extent(RoadLevel.Carriageway, false))))
            {
                yield break;
            }

            // a mark behind the kerb line: only that side
            bool oneSide = mark && best > road.Extent(RoadLevel.Carriageway, at > 0) - 0.05;
            foreach (int side in oneSide ? new[] { at } : new[] { 1, -1 })
            {
                bool left = side > 0;
                double kerbLine = road.Extent(RoadLevel.Carriageway, left);
                double outer = Math.Max(road.Extent(RoadLevel.Edge, left), kerbLine) + 0.5;
                Vec2 s0 = foot + (across * (side * (kerbLine - 0.01)));
                Vec2 s1 = foot + (across * (side * outer));
                Vec2 h = along * (RampWidth / 2);
                var rect = new[] { s0 - h, s0 + h, s1 + h, s1 - h };
                PathsD reach = Clipper.Intersect(new PathsD { Positive(rect) }, behindKerb, FillRule.NonZero, Precision);
                if (Clipper.Intersect(reach, walk, FillRule.NonZero, Precision).Sum(q => Math.Abs(Clipper.Area(q))) < 0.1)
                {
                    continue;
                }

                // footprint like a dropped kerb with flares: the lowered kerb, 0,80 m of flare on both sides at the
                // kerb, narrowing a little towards the back
                double depth = Math.Min(RampDepth, road.Extent(RoadLevel.Sidewalk, left) - kerbLine);
                if (depth < 0.5)
                {
                    continue;
                }

                Vec2 inward = across * side;
                Vec2 k0 = foot + (inward * kerbLine);
                double front = (RampWidth / 2) + RampFlare, back = (RampWidth / 2) + RampBackFlare;
                var shape = new[]
                {
                    k0 - (along * front) - (inward * 0.01), k0 + (along * front) - (inward * 0.01),
                    k0 + (along * back) + (inward * depth), k0 - (along * back) + (inward * depth),
                };
                PathsD ramp = Clipper.Intersect(new PathsD { Positive(shape) }, behindKerb, FillRule.NonZero, Precision);

                Vec2 probe = foot + (across * (side * (kerbLine + 0.07)));
                foreach (PathD piece in ramp.Where(q => Clipper.Area(q) > 0.05))
                {
                    if (Clipper.PointInPolygon(new PointD(probe.X, probe.Y), piece) != PointInPolygonResult.IsOutside)
                    {
                        yield return (piece, new RoadRampFrame(k0, along, inward, depth));
                    }
                }
            }
        }

        /// <summary>Strips between two offsets from the axis on the road sides selected by <paramref name="has"/> (square ends).</summary>
        private static PathsD Strips(List<RoadSpec> roads, Func<RoadSpec, bool, bool> has, Func<RoadSpec, bool, double> from, Func<RoadSpec, bool, double> to)
        {
            var all = new PathsD();
            foreach (RoadSpec r in roads)
            {
                foreach (bool left in new[] { true, false })
                {
                    double a = from(r, left), b = to(r, left);
                    if (!has(r, left) || b - a < 1e-6)
                    {
                        continue;
                    }

                    // a little wider than the ring it selects, so its edges never run along the ring's edges
                    a = Math.Max(0, a - 0.05);
                    b += 0.05;

                    PathsD outer = Union(Band(r.Axis, r.Closed, left ? b : 0, left ? 0 : b));
                    PathsD inner = Union(Band(r.Axis, r.Closed, left ? a : 0, left ? 0 : a));
                    all.AddRange(Clipper.Difference(outer, inner, FillRule.NonZero, Precision));
                }
            }

            return Union(all);
        }

        private static bool Touches(PathsD piece, PathsD strips, double reach) =>
            strips.Count > 0 && Clipper.Intersect(Clipper.InflatePaths(piece, reach, JoinType.Round, EndType.Polygon, 2, Precision, 0.02), strips, FillRule.NonZero, Precision).Sum(p => Math.Abs(Clipper.Area(p))) > 1e-4;

        /// <summary>
        /// Where a ring of the cross-section is drawn: the strips along the roads that have it, plus the corner fillets
        /// next to them (found within <paramref name="touch"/>; a negative value takes all given fillets), grown by
        /// <paramref name="grow"/> so a ring outside the kerb line follows the whole corner arc.
        /// </summary>
        private static PathsD Zone(PathsD strips, List<PathsD> fillets, double touch, double grow)
        {
            var zone = new PathsD(strips);
            foreach (PathsD f in fillets)
            {
                if (touch >= 0 && !Touches(f, strips, touch))
                {
                    continue;
                }

                zone.AddRange(grow > 0 ? Clipper.InflatePaths(f, grow, JoinType.Round, EndType.Polygon, 2, Precision, 0.02) : f);
            }

            return Union(zone);
        }

        /// <summary>One rounded bend of an axis: vertices First..Last replaced by an arc from T1 to T2.</summary>
        internal sealed class BendArc
        {
            public int First { get; set; }

            public int Last { get; set; }

            public Vec2 T1 { get; set; }

            public Vec2 T2 { get; set; }

            public Vec2 Centre { get; set; }

            public double Radius { get; set; }

            /// <summary>Signed turn (positive: left, counter-clockwise).</summary>
            public double Turn { get; set; }

            public bool Fits { get; set; }
        }

        /// <summary>
        /// Plans the rounding of the bends of an axis. A bend gets the axis radius <paramref name="axisRadius"/>
        /// (by the inner side); where the points are too close for that (a curve drawn with several points), the
        /// neighbouring bends turning the same way are taken together and rounded with one arc.
        /// </summary>
        internal static (List<int> Keep, List<BendArc> Bends) PlanBends(IReadOnlyList<Vec2> all, bool closed, Func<int, bool> isCornerAll, Func<bool, double> axisRadius)
        {
            List<int> keep = Simplify(all, closed, i => (closed || (i > 0 && i < all.Count - 1)) && isCornerAll(i), BendTolerance);

            // a bend right before an open end whose last piece is too short for its radius (a stray last click) is
            // left out: the road ends straight instead of with a tight curl
            bool trimmed = true;
            while (!closed && trimmed && keep.Count > 2)
            {
                trimmed = false;
                foreach (bool atStart in new[] { true, false })
                {
                    if (keep.Count <= 2)
                    {
                        break;
                    }

                    int m = keep.Count;
                    int end = atStart ? keep[0] : keep[m - 1], v = atStart ? keep[1] : keep[m - 2], other = atStart ? keep[2] : keep[m - 3];
                    if (!isCornerAll(v))
                    {
                        continue;
                    }

                    Vec2 u1 = all[v] - all[other], u2 = all[end] - all[v];
                    if (u1.Length < 1e-9 || u2.Length < 1e-9)
                    {
                        continue;
                    }

                    double turn = Math.Atan2(u1.Cross(u2), u1.Dot(u2));
                    if (Math.Abs(turn) < 0.01)
                    {
                        continue;
                    }

                    bool turnLeft = atStart ? turn < 0 : turn > 0;
                    double need = axisRadius(turnLeft) * Math.Tan(Math.Abs(turn) / 2);
                    if (u2.Length * 0.98 < need * 0.5)
                    {
                        keep.RemoveAt(atStart ? 1 : m - 2);
                        trimmed = true;
                    }
                }
            }
            var a = keep.Select(i => all[i]).ToList();
            bool isCorner(int j) => isCornerAll(keep[j]);
            var list = new List<BendArc>();
            int n = a.Count;
            if (n < 3)
            {
                return (keep, list);
            }

            int W(int i) => ((i % n) + n) % n;
            bool Interior(int i) => closed || (i > 0 && i < n - 1);
            bool Bendable(int i) => Interior(i) && isCorner(i);
            double Turn(int i)
            {
                Vec2 u1 = a[i] - a[W(i - 1)], u2 = a[W(i + 1)] - a[i];
                if (u1.Length < 1e-9 || u2.Length < 1e-9)
                {
                    return 0;
                }

                return Math.Atan2(u1.Cross(u2), u1.Dot(u2));
            }

            bool IsBend(int i) => Bendable(i) && Math.Abs(Turn(i)) >= 0.01;

            BendArc? Make(int s, int e)
            {
                Vec2 pIn = a[W(s - 1)], pOut = a[W(e + 1)];
                Vec2 dIn = a[s] - pIn, dOut = pOut - a[e];
                if (dIn.Length < 1e-9 || dOut.Length < 1e-9)
                {
                    return null;
                }

                Vec2 uIn = dIn.Normalized(), uOut = dOut.Normalized();
                double turn = Math.Atan2(uIn.Cross(uOut), uIn.Dot(uOut));
                double th = Math.Abs(turn);
                if (th < 0.01 || th > Math.PI - 0.05)
                {
                    return null;
                }

                Vec2 v = a[s];
                if (e != s)
                {
                    double den = uIn.Cross(uOut);
                    v = a[s] + (uIn * ((a[e] - a[s]).Cross(uOut) / den));
                }

                double lenIn = (v - pIn).Dot(uIn), lenOut = (pOut - v).Dot(uOut);
                if (lenIn <= 1e-6 || lenOut <= 1e-6)
                {
                    return null;
                }

                bool left = turn > 0;
                double ra = axisRadius(left);
                double tan = Math.Tan(th / 2);
                double need = ra * tan;
                bool bendIn = IsBend(W(s - 1)) && W(s - 1) != e, bendOut = IsBend(W(e + 1)) && W(e + 1) != s;
                double availIn = lenIn * (bendIn ? 0.5 : 0.98);
                double availOut = lenOut * (bendOut ? 0.5 : 0.98);

                // in a chain of bends (a curve drawn with several points) every arc takes its whole share of the
                // segments, so the arcs meet and the road is one smooth curve instead of a rounded polygon
                // (only where the points are close compared with the radius; long sides keep the road radius)
                bool closeIn = bendIn && lenIn < 3 * ra, closeOut = bendOut && lenOut < 3 * ra;
                double smooth = closeIn && closeOut ? Math.Min(availIn, availOut) : closeIn ? availIn : closeOut ? availOut : 0;
                double t = Math.Min(Math.Max(need, smooth), Math.Min(availIn, availOut));
                double r = t / tan;
                Vec2 t1 = v - (uIn * t);
                return new BendArc
                {
                    First = s,
                    Last = e,
                    T1 = t1,
                    T2 = v + (uOut * t),
                    Centre = t1 + ((left ? uIn.Perp() : -uIn.Perp()) * r),
                    Radius = r,
                    Turn = turn,
                    Fits = need <= Math.Min(availIn, availOut) + 1e-9,
                };
            }

            int i = 0;
            while (i < n)
            {
                double ti = Bendable(i) ? Turn(i) : 0;
                if (Math.Abs(ti) < 0.01)
                {
                    i++;
                    continue;
                }

                BendArc? best = Make(i, i);
                int e = i;
                double total = ti;
                while ((best == null || !best.Fits) && e + 1 < n && Bendable(e + 1))
                {
                    double tn = Turn(e + 1);
                    if ((Math.Abs(tn) >= 0.1 && Math.Sign(tn) != Math.Sign(ti)) || Math.Abs(total + tn) > Math.PI - 0.05)
                    {
                        break;
                    }

                    e++;
                    total += tn;
                    BendArc? c = Make(i, e);
                    if (c != null && (best == null || c.Fits || c.Radius > best.Radius))
                    {
                        best = c;
                    }
                }

                if (best == null)
                {
                    i++;
                    continue;
                }

                list.Add(best);
                i = best.Last + 1;
            }

            return (keep, list);
        }

        /// <summary>Points of a hand-drawn curve closer than this to the line through their neighbours are left out.</summary>
        internal const double BendTolerance = 0.3;

        /// <summary>
        /// Douglas-Peucker over the vertices that may be dropped (<paramref name="droppable"/>); the others are always
        /// kept. Returns the kept vertex indices in order.
        /// </summary>
        internal static List<int> Simplify(IReadOnlyList<Vec2> a, bool closed, Func<int, bool> droppable, double tol)
        {
            int n = a.Count;
            var keep = new bool[n];
            for (int i = 0; i < n; i++)
            {
                keep[i] = !droppable(i);
            }

            if (n < 3)
            {
                return Enumerable.Range(0, n).ToList();
            }

            if (!keep.Any(k => k))
            {
                // closed loop of bends only: keep the first vertex and the one farthest from it
                keep[0] = true;
                int far = 0;
                for (int i = 1; i < n; i++)
                {
                    if (a[i].DistanceTo(a[0]) > a[far].DistanceTo(a[0]))
                    {
                        far = i;
                    }
                }

                keep[far] = true;
            }

            void Dp(int p, int q)
            {
                // vertices strictly between p and q (indices modulo n, q may be p + n for a closed loop)
                double best = -1;
                int bi = -1;
                for (int k = p + 1; k < q; k++)
                {
                    double d = SegmentDistance(a[k % n], a[p % n], a[q % n]);
                    if (d > best)
                    {
                        best = d;
                        bi = k;
                    }
                }

                if (bi >= 0 && best > tol)
                {
                    keep[bi % n] = true;
                    Dp(p, bi);
                    Dp(bi, q);
                }
            }

            var anchors = Enumerable.Range(0, n).Where(i => keep[i]).ToList();
            for (int j = 0; j + 1 < anchors.Count; j++)
            {
                Dp(anchors[j], anchors[j + 1]);
            }

            if (closed)
            {
                Dp(anchors[anchors.Count - 1], anchors[0] + n);
            }

            return Enumerable.Range(0, n).Where(i => keep[i]).ToList();
        }

        private static double SegmentDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double l2 = ab.Dot(ab);
            if (l2 < 1e-18)
            {
                return p.DistanceTo(a);
            }

            double t = Math.Max(0, Math.Min(1, (p - a).Dot(ab) / l2));
            return p.DistanceTo(a + (ab * t));
        }

        /// <summary>Axis radius of a bend of road <paramref name="r"/>: the inner kerb gets the kerb radius.</summary>
        internal static double AxisRadius(RoadSpec r, bool left) => r.CornerRadius + r.Extent(RoadLevel.Carriageway, left);

        /// <summary>
        /// Rounds the bends of an axis drawn with straight segments, so the inner kerb of the bend gets the kerb radius
        /// and every edge of the cross-section is concentric (inner and outer side). Arcs drawn in the axis stay.
        /// </summary>
        internal static List<Vec2> Fillet(RoadSpec r)
        {
            List<Vec2> all = r.Axis;
            (List<int> keep, List<BendArc> bends) = PlanBends(all, r.Closed, i => r.Corners == null || r.Corners.Any(c => c.DistanceTo(all[i]) < 1e-3), left => AxisRadius(r, left));
            if (bends.Count == 0 && keep.Count == all.Count)
            {
                return all;
            }

            var a = keep.Select(i => all[i]).ToList();
            var byFirst = bends.ToDictionary(b => b.First);
            var result = new List<Vec2>(a.Count * 4);
            for (int v = 0; v < a.Count; v++)
            {
                if (!byFirst.TryGetValue(v, out BendArc? b))
                {
                    result.Add(a[v]);
                    continue;
                }

                double a0 = (b.T1 - b.Centre).Angle;
                int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(b.Turn) / (Math.PI / 90)));
                for (int k = 0; k <= steps; k++)
                {
                    double ang = a0 + (b.Turn * k / steps);
                    result.Add(b.Centre + (new Vec2(Math.Cos(ang), Math.Sin(ang)) * b.Radius));
                }

                v = b.Last;
            }

            return Clean(result);
        }

        /// <summary>
        /// The rounded inner corners of every junction: for each pair of roads that overlap, the closing of the two
        /// roads with radius <paramref name="r"/>, kept only around their overlap. Roads that merely run close to each
        /// other (parallel streets, a service road) are not merged.
        /// </summary>
        private static PathsD JunctionCorners(List<PathsD> perRoad, double r)
        {
            var fill = new PathsD();
            if (r < 0.05)
            {
                return fill;
            }

            var boxes = perRoad.Select(Bounds).ToList();
            for (int i = 0; i < perRoad.Count; i++)
            {
                for (int j = i + 1; j < perRoad.Count; j++)
                {
                    if (!boxes[i].Intersects(boxes[j]))
                    {
                        continue;
                    }

                    PathsD overlap = Clipper.Intersect(perRoad[i], perRoad[j], FillRule.NonZero, Precision);
                    double overlapArea = overlap.Sum(p => Math.Abs(Clipper.Area(p)));
                    if (overlapArea < 0.01)
                    {
                        continue;
                    }

                    // two roads that overlap only inside a third one (two arms of a roundabout, both reaching the
                    // ring axis) do not meet each other: their corners are the third road's
                    var others = new PathsD();
                    for (int o = 0; o < perRoad.Count; o++)
                    {
                        if (o != i && o != j && boxes[o].Intersects(boxes[i]) && boxes[o].Intersects(boxes[j]))
                        {
                            others.AddRange(perRoad[o]);
                        }
                    }

                    if (others.Count > 0 && Clipper.Difference(overlap, others, FillRule.NonZero, Precision).Sum(p => Math.Abs(Clipper.Area(p))) < 0.02 * overlapArea)
                    {
                        continue;
                    }

                    PathsD zone = Clipper.InflatePaths(overlap, Math.Max(8 * r, 3), JoinType.Round, EndType.Polygon, 2, Precision, 0.05);
                    var pair = new PathsD(perRoad[i]);
                    pair.AddRange(perRoad[j]);
                    fill.AddRange(Clipper.Intersect(Close(Union(pair), r), zone, FillRule.NonZero, Precision));
                }
            }

            return fill;
        }

        private static Box2 Bounds(PathsD paths)
        {
            Box2 b = Box2.Empty;
            foreach (PathD p in paths)
            {
                foreach (PointD q in p)
                {
                    b = b.Include(new Vec2(q.x, q.y));
                }
            }

            return b;
        }

        private static List<RoadRegion> Regions(PathsD paths, Vec2 origin)
        {
            var tree = new PolyTreeD();
            var c = new ClipperD(Precision);
            c.AddSubject(paths);
            c.Execute(ClipType.Union, FillRule.NonZero, tree);
            var list = new List<RoadRegion>();
            void Walk(PolyPathD node)
            {
                for (int i = 0; i < node.Count; i++)
                {
                    PolyPathD outer = node[i];
                    if (outer.Polygon == null)
                    {
                        continue;
                    }

                    double area = Math.Abs(Clipper.Area(outer.Polygon));
                    double perimeter = Perimeter(outer.Polygon);
                    var holes = new List<RoadLoop>();
                    for (int h = 0; h < outer.Count; h++)
                    {
                        PolyPathD hole = outer[h];
                        if (hole.Polygon != null)
                        {
                            area -= Math.Abs(Clipper.Area(hole.Polygon));
                            holes.Add(FitArcs(hole.Polygon, origin));
                        }

                        Walk(hole); // islands inside holes
                    }

                    // slivers from nearly coincident boundaries are dropped
                    if (area > 0.05 && area / Math.Max(perimeter, 1e-9) > 0.02)
                    {
                        list.Add(new RoadRegion(FitArcs(outer.Polygon, origin), holes, area));
                    }
                }
            }

            Walk(tree);
            return list;
        }

        private static double Perimeter(PathD p)
        {
            double s = 0;
            for (int i = 0; i < p.Count; i++)
            {
                PointD a = p[i], b = p[(i + 1) % p.Count];
                s += Math.Sqrt(((b.x - a.x) * (b.x - a.x)) + ((b.y - a.y) * (b.y - a.y)));
            }

            return s;
        }

        // ------------------------------------------------------------------ arcs

        /// <summary>
        /// Replaces runs of points lying on one circle (round joins from the closing, bends of the axis) with arc
        /// segments, so corners are real arcs whose radius can be read in the drawing.
        /// </summary>
        internal static RoadLoop FitArcs(PathD path, Vec2 origin)
        {
            List<Vec2> pts = Clean(path.Select(p => new Vec2(p.x, p.y)).ToList());
            if (pts.Count > 2 && pts[0].DistanceTo(pts[pts.Count - 1]) < 1e-6)
            {
                pts.RemoveAt(pts.Count - 1);
            }

            pts = Despike(pts);

            int n = pts.Count;
            var outPts = new List<Vec2>();
            var outBulges = new List<double>();
            if (n < 4)
            {
                outPts.AddRange(pts.Select(p => p + origin));
                outBulges.AddRange(pts.Select(_ => 0.0));
                return new RoadLoop(outPts, outBulges);
            }

            // start at a vertex that is not inside an arc: a sharp turn or a long edge
            int startAt = 0;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = pts[(i - 1 + n) % n], b = pts[i], c = pts[(i + 1) % n];
                double ang = Math.Abs(Math.Atan2((b - a).Cross(c - b), (b - a).Dot(c - b)));
                if (ang > 0.35 || (b - a).Length > 2.0 || (c - b).Length > 2.0)
                {
                    startAt = i;
                    break;
                }
            }

            var ring = new List<Vec2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                ring.Add(pts[(startAt + i) % n]);
            }

            int idx = 0;
            while (idx < n)
            {
                int bestEnd = -1;
                double bestBulge = 0;
                for (int end = idx + 3; end <= n; end++)
                {
                    if (!OnArc(ring, idx, end, out double bulge))
                    {
                        break;
                    }

                    // a run that bulges less than a centimetre from its chord is a straight edge, not an arc
                    double chord = ring[idx].DistanceTo(ring[end]);
                    double sweep = 4 * Math.Atan(Math.Abs(bulge));
                    if (chord / 2 * Math.Tan(sweep / 4) < 0.01)
                    {
                        continue;
                    }

                    bestEnd = end;
                    bestBulge = bulge;
                }

                outPts.Add(ring[idx] + origin);
                if (bestEnd > 0)
                {
                    outBulges.Add(bestBulge);
                    idx = bestEnd;
                }
                else
                {
                    outBulges.Add(0);
                    idx++;
                }
            }

            return new RoadLoop(outPts, outBulges);
        }

        private static bool OnArc(List<Vec2> ring, int from, int to, out double bulge)
        {
            bulge = 0;
            Vec2 a = ring[from], m = ring[(from + to) / 2], b = ring[to];
            if (!Circle(a, m, b, out Vec2 c, out double r) || r > 300 || r < 0.05)
            {
                return false;
            }

            // least squares over all points of the run: millimetre rounding of single points does not tilt the circle
            if (FitCircle(ring, from, to, out Vec2 fc, out double fr) && fr < 300 && fr > 0.05)
            {
                c = fc;
                r = fr;
            }

            // the points come from round joins polygonised to a few millimetres: every point lies on the circle within
            // that, and no chord bulges away from the circle by more than that (a long straight edge next to an arc
            // would otherwise be swallowed into a flat, wrong arc)
            const double tol = 0.005;
            const double maxSagitta = 0.012;
            double sign = 0;
            double sweep = 0;
            for (int i = from; i < to; i++)
            {
                Vec2 p = ring[i], q = ring[i + 1];
                if (Math.Abs(p.DistanceTo(c) - r) > tol || Math.Abs(q.DistanceTo(c) - r) > tol)
                {
                    return false;
                }

                double half = (q - p).Length / 2;
                if (half >= r || r - Math.Sqrt((r * r) - (half * half)) > maxSagitta)
                {
                    return false;
                }

                double step = Math.Atan2((p - c).Cross(q - c), (p - c).Dot(q - c));
                if (sign == 0)
                {
                    sign = Math.Sign(step);
                }
                else if (Math.Sign(step) != sign)
                {
                    return false;
                }

                sweep += step;
            }

            if (Math.Abs(sweep) > Math.PI * 1.01 || Math.Abs(sweep) < 1e-3)
            {
                return false;
            }


            bulge = Math.Tan(sweep / 4);
            return true;
        }

        /// <summary>Algebraic least-squares circle (Kåsa) through the points from..to.</summary>
        private static bool FitCircle(List<Vec2> ring, int from, int to, out Vec2 centre, out double r)
        {
            centre = default;
            r = 0;
            Vec2 o = ring[from];
            double sxx = 0, sxy = 0, syy = 0, sx = 0, sy = 0, sz = 0, sxz = 0, syz = 0;
            int n = 0;
            for (int i = from; i <= to; i++)
            {
                Vec2 p = ring[i] - o;
                double z = (p.X * p.X) + (p.Y * p.Y);
                sxx += p.X * p.X;
                sxy += p.X * p.Y;
                syy += p.Y * p.Y;
                sx += p.X;
                sy += p.Y;
                sz += z;
                sxz += p.X * z;
                syz += p.Y * z;
                n++;
            }

            // solve [sxx sxy sx; sxy syy sy; sx sy n] [D E F] = -[sxz syz sz]
            double[,] m = { { sxx, sxy, sx, -sxz }, { sxy, syy, sy, -syz }, { sx, sy, n, -sz } };
            for (int col = 0; col < 3; col++)
            {
                int piv = col;
                for (int row = col + 1; row < 3; row++)
                {
                    if (Math.Abs(m[row, col]) > Math.Abs(m[piv, col]))
                    {
                        piv = row;
                    }
                }

                if (Math.Abs(m[piv, col]) < 1e-12)
                {
                    return false;
                }

                for (int k = 0; k < 4; k++)
                {
                    (m[col, k], m[piv, k]) = (m[piv, k], m[col, k]);
                }

                for (int row = 0; row < 3; row++)
                {
                    if (row == col)
                    {
                        continue;
                    }

                    double f = m[row, col] / m[col, col];
                    for (int k = col; k < 4; k++)
                    {
                        m[row, k] -= f * m[col, k];
                    }
                }
            }

            double d = m[0, 3] / m[0, 0], e = m[1, 3] / m[1, 1], ff = m[2, 3] / m[2, 2];
            double rr = ((d * d) + (e * e)) / 4 - ff;
            if (rr <= 0)
            {
                return false;
            }

            centre = o + new Vec2(-d / 2, -e / 2);
            r = Math.Sqrt(rr);
            return true;
        }

        private static bool Circle(Vec2 a, Vec2 b, Vec2 c, out Vec2 centre, out double r)
        {
            double d = 2 * ((a.X * (b.Y - c.Y)) + (b.X * (c.Y - a.Y)) + (c.X * (a.Y - b.Y)));
            centre = default;
            r = 0;
            if (Math.Abs(d) < 1e-12)
            {
                return false;
            }

            double a2 = a.Dot(a), b2 = b.Dot(b), c2 = c.Dot(c);
            centre = new Vec2(((a2 * (b.Y - c.Y)) + (b2 * (c.Y - a.Y)) + (c2 * (a.Y - b.Y))) / d, ((a2 * (c.X - b.X)) + (b2 * (a.X - c.X)) + (c2 * (b.X - a.X))) / d);
            r = centre.DistanceTo(a);
            return true;
        }

        // ------------------------------------------------------------------ markings and arrows

        private static void Paint(List<RoadSpec> roads, int i, Dictionary<RoadLevel, List<PathsD>> bands, RoadNetworkResult result, Vec2 origin, PathsD gaps)
        {
            RoadSpec r = roads[i];

            // where markings may go: inside this carriageway, away from the other roads
            PathsD inside = Clipper.InflatePaths(Union(bands[RoadLevel.Carriageway][i]), -0.2, JoinType.Miter, EndType.Polygon, 4, Precision);
            var others = new PathsD();
            var othersWide = new PathsD();
            for (int j = 0; j < roads.Count; j++)
            {
                if (j != i)
                {
                    others.AddRange(Clipper.InflatePaths(bands[RoadLevel.Bike][j], 1.0, JoinType.Round, EndType.Polygon, 2, Precision, 0.05));
                    othersWide.AddRange(Clipper.InflatePaths(bands[RoadLevel.Bike][j], 6.0, JoinType.Round, EndType.Polygon, 2, Precision, 0.05));
                }
            }

            if (gaps.Count > 0)
            {
                others.AddRange(Clipper.InflatePaths(gaps, 0.5, JoinType.Miter, EndType.Polygon, 4, Precision));
                othersWide.AddRange(Clipper.InflatePaths(gaps, 3.0, JoinType.Miter, EndType.Polygon, 4, Precision));
            }
            others = Union(others);
            othersWide = Union(othersWide);

            // lines between lanes (offset from the axis, left positive)
            var lines = new List<(double Offset, bool Centre)>();
            double lw = r.LaneWidth;
            if (r.Markings && r.OneWay)
            {
                for (int k = 1; k < r.TotalLanes; k++)
                {
                    lines.Add((r.CarriagewayLeft - (k * lw), false));
                }
            }
            else if (r.Markings)
            {
                double m = r.HalfMedian;
                if (m > 0)
                {
                    // the median is bordered by continuous lines on both sides
                    lines.Add((m, true));
                    lines.Add((-m, true));
                }
                else
                {
                    lines.Add((0, true));
                }

                for (int k = 1; k < r.LanesLeft; k++)
                {
                    lines.Add((m + (k * lw), false));
                }

                for (int k = 1; k < r.LanesRight; k++)
                {
                    lines.Add((-m - (k * lw), false));
                }
            }

            // a two-way road with two or more lanes in each direction, or with a median, gets continuous centre lines
            bool solidCentre = !r.OneWay && ((r.LanesLeft >= 2 && r.LanesRight >= 2) || r.Median > 0);
            foreach ((double offset, bool centre) in lines)
            {
                List<Vec2> line = OffsetLine(r.Axis, r.Closed, offset);
                var c = new ClipperD(Precision);
                c.AddOpenSubject(new PathsD { new PathD(line.Select(p => new PointD(p.X, p.Y))) });
                c.AddClip(inside);
                var closedOut = new PathsD();
                var kept = new PathsD();
                c.Execute(ClipType.Intersection, FillRule.NonZero, closedOut, kept);
                var c2 = new ClipperD(Precision);
                c2.AddOpenSubject(kept);
                c2.AddClip(others);
                var openOut = new PathsD();
                c2.Execute(ClipType.Difference, FillRule.NonZero, closedOut, openOut);
                foreach (PathD run in openOut)
                {
                    List<Vec2> pts = run.Select(p => new Vec2(p.x, p.y)).ToList();
                    if (centre && solidCentre)
                    {
                        result.Markings.Add(new RoadMarking(r.Id, pts.Select(p => p + origin).ToList(), true));
                    }
                    else
                    {
                        foreach (List<Vec2> dash in Dashes(pts, DashLength, DashGap))
                        {
                            result.Markings.Add(new RoadMarking(r.Id, dash.Select(p => p + origin).ToList(), centre));
                        }
                    }
                }
            }

            if (!r.Arrows)
            {
                return;
            }

            // arrows: lane centres, every ArrowSpacing metres, not near junctions
            var lanes = new List<(double Offset, bool Forward)>();
            if (r.OneWay)
            {
                for (int k = 0; k < r.TotalLanes; k++)
                {
                    lanes.Add((r.CarriagewayLeft - ((k + 0.5) * lw), true));
                }
            }
            else
            {
                for (int k = 0; k < r.LanesRight; k++)
                {
                    lanes.Add((-r.HalfMedian - ((k + 0.5) * lw), true));
                }

                for (int k = 0; k < r.LanesLeft; k++)
                {
                    lanes.Add((r.HalfMedian + ((k + 0.5) * lw), false));
                }
            }

            double length = Length(r.Axis, r.Closed);
            double spacing = r.ArrowSpacing;
            for (double st = spacing / 2; st <= length - 10; st += spacing)
            {
                if (st < 10)
                {
                    continue;
                }

                (Vec2 at, Vec2 dir) = PointAt(r.Axis, r.Closed, st);
                Vec2 nrm = dir.Perp();
                foreach ((double offset, bool forward) in lanes)
                {
                    Vec2 p = at + (nrm * offset);
                    if (!Inside(p, inside) || Inside(p, othersWide))
                    {
                        continue;
                    }

                    result.Arrows.Add(new RoadArrow(r.Id, p + origin, forward ? dir.Angle : (-dir).Angle));
                }
            }
        }

        internal static List<Vec2> OffsetLine(List<Vec2> axis, bool closed, double offset)
        {
            int n = axis.Count;
            var pts = new List<Vec2>(n + 1);
            for (int i = 0; i < n; i++)
            {
                bool hasPrev = closed || i > 0, hasNext = closed || i < n - 1;
                Vec2 n1 = hasPrev ? (axis[i] - axis[(i - 1 + n) % n]).Normalized().Perp() : (axis[(i + 1) % n] - axis[i]).Normalized().Perp();
                Vec2 n2 = hasNext ? (axis[(i + 1) % n] - axis[i]).Normalized().Perp() : n1;
                Vec2 m = (n1 + n2).Normalized();
                double cos = Math.Max(0.25, m.Dot(n1));
                pts.Add(axis[i] + (m * (offset / cos)));
            }

            if (closed)
            {
                pts.Add(pts[0]);
            }

            return pts;
        }

        internal static IEnumerable<List<Vec2>> Dashes(List<Vec2> line, double dash, double gap)
        {
            double period = dash + gap;
            double total = 0;
            for (int i = 1; i < line.Count; i++)
            {
                total += line[i].DistanceTo(line[i - 1]);
            }

            for (double s = 0; s < total - 0.5; s += period)
            {
                double e = Math.Min(s + dash, total);
                if (e - s < 0.5)
                {
                    continue;
                }

                yield return Slice(line, s, e);
            }
        }

        private static List<Vec2> Slice(List<Vec2> line, double from, double to)
        {
            var pts = new List<Vec2>();
            double acc = 0;
            for (int i = 1; i < line.Count; i++)
            {
                Vec2 a = line[i - 1], b = line[i];
                double len = a.DistanceTo(b);
                if (len < 1e-12)
                {
                    continue;
                }

                double s0 = acc, s1 = acc + len;
                if (s1 >= from && s0 <= to)
                {
                    double t0 = Math.Max(0, (from - s0) / len), t1 = Math.Min(1, (to - s0) / len);
                    Vec2 p0 = a + ((b - a) * t0), p1 = a + ((b - a) * t1);
                    if (pts.Count == 0 || pts[pts.Count - 1].DistanceTo(p0) > 1e-9)
                    {
                        pts.Add(p0);
                    }

                    pts.Add(p1);
                }

                acc = s1;
            }

            return pts;
        }

        internal static double Length(List<Vec2> axis, bool closed)
        {
            double s = 0;
            int segs = closed ? axis.Count : axis.Count - 1;
            for (int i = 0; i < segs; i++)
            {
                s += axis[i].DistanceTo(axis[(i + 1) % axis.Count]);
            }

            return s;
        }

        internal static (Vec2 Point, Vec2 Direction) PointAt(List<Vec2> axis, bool closed, double station)
        {
            double acc = 0;
            int segs = closed ? axis.Count : axis.Count - 1;
            for (int i = 0; i < segs; i++)
            {
                Vec2 a = axis[i], b = axis[(i + 1) % axis.Count];
                double len = a.DistanceTo(b);
                if (acc + len >= station || i == segs - 1)
                {
                    double t = len > 1e-12 ? Math.Max(0, Math.Min(1, (station - acc) / len)) : 0;
                    return (a + ((b - a) * t), (b - a).Normalized());
                }

                acc += len;
            }

            return (axis[0], new Vec2(1, 0));
        }

        /// <summary>Non-zero winding test against a set of paths.</summary>
        internal static bool Inside(Vec2 p, PathsD paths)
        {
            int winding = 0;
            foreach (PathD path in paths)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    PointD a = path[i], b = path[(i + 1) % path.Count];
                    if (a.y <= p.Y)
                    {
                        if (b.y > p.Y && ((b.x - a.x) * (p.Y - a.y)) - ((p.X - a.x) * (b.y - a.y)) > 0)
                        {
                            winding++;
                        }
                    }
                    else if (b.y <= p.Y && ((b.x - a.x) * (p.Y - a.y)) - ((p.X - a.x) * (b.y - a.y)) < 0)
                    {
                        winding--;
                    }
                }
            }

            return winding != 0;
        }

        /// <summary>
        /// Removes back-steps under 1 cm (rounding where two offset boundaries meet) and zero-width spikes from a
        /// closed loop, so its arcs are recognised whole.
        /// </summary>
        internal static List<Vec2> Despike(List<Vec2> pts)
        {
            var list = new List<Vec2>(pts);
            bool changed = true;
            while (changed && list.Count > 3)
            {
                changed = false;
                for (int i = 0; i < list.Count && list.Count > 3; i++)
                {
                    Vec2 a = list[(i - 1 + list.Count) % list.Count], b = list[i], c = list[(i + 1) % list.Count];
                    Vec2 ab = b - a, bc = c - b;
                    double la = ab.Length, lc = bc.Length;
                    bool tiny = la < 0.01;
                    bool spike = la > 1e-9 && lc > 1e-9 && ab.Dot(bc) < 0 && Math.Abs(ab.Cross(bc)) / (la * lc) < 0.09;
                    if (tiny || spike)
                    {
                        list.RemoveAt(i);
                        i--;
                        changed = true;
                    }
                }
            }

            return list;
        }

        private static List<Vec2> Clean(List<Vec2> pts)
        {
            var list = new List<Vec2>(pts.Count);
            foreach (Vec2 p in pts)
            {
                if (list.Count == 0 || list[list.Count - 1].DistanceTo(p) > 1e-6)
                {
                    list.Add(p);
                }
            }

            return list;
        }

        private static RoadSpec Copy(RoadSpec r)
        {
            RoadSpec c = r.CloneParameters();
            c.Id = r.Id;
            c.Name = r.Name;
            c.Corners = r.Corners == null ? null : new List<Vec2>(r.Corners);
            c.Axis = new List<Vec2>(r.Axis);
            c.Closed = r.Closed;
            return c;
        }

        private static string Fmt(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
