using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>Closed loop of a region boundary; segment i runs from point i to point i+1 with bulge i (0 = straight).</summary>
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
        public static RoadNetworkResult Build(IReadOnlyList<RoadSpec> input, IEnumerable<IReadOnlyList<Vec2>>? noMarking)
        {
            var result = new RoadNetworkResult();
            List<RoadSpec> roads = input.Where(r => r.Axis.Count >= 2).Select(Copy).ToList();
            foreach (RoadSpec r in input.Where(r => r.Axis.Count < 2))
            {
                result.Notes.Add("Osovina " + r.Id + " ima manje od dvije tačke i preskočena je.");
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
                if (r.Closed && r.Axis.Count > 2 && r.Axis[0].DistanceTo(r.Axis[r.Axis.Count - 1]) < 1e-6)
                {
                    r.Axis.RemoveAt(r.Axis.Count - 1);
                }
            }

            roads = roads.Where(r => r.Axis.Count >= 2).ToList();
            JoinEnds(roads, result.Notes);
            foreach (RoadSpec r in roads)
            {
                result.Axes[r.Id] = r.Axis.Select(p => p + origin).ToList();
            }

            double radius = roads.Max(r => r.CornerRadius);
            double bikeMax = roads.Max(r => Math.Max(r.BikeLeft, r.BikeRight));
            double walkMax = roads.Max(r => Math.Max(r.SidewalkLeft, r.SidewalkRight));
            result.ActualRadius = radius;
            result.EffectiveRadius = radius + bikeMax;

            // concentric corner radii: inner levels further from the corner centre, outer levels closer
            var radii = new Dictionary<RoadLevel, double>
            {
                [RoadLevel.Carriageway] = radius + bikeMax,
                [RoadLevel.Bike] = radius,
                [RoadLevel.Kerb] = Math.Max(0, radius - RoadSpec.KerbWidth),
                [RoadLevel.Sidewalk] = Math.Max(0, radius - RoadSpec.KerbWidth - walkMax),
                [RoadLevel.Edge] = Math.Max(0, radius - RoadSpec.KerbWidth - walkMax - RoadSpec.EdgeWidth),
            };

            var levels = (RoadLevel[])Enum.GetValues(typeof(RoadLevel));
            var bands = new Dictionary<RoadLevel, List<PathsD>>();
            var outline = new Dictionary<RoadLevel, PathsD>();
            PathsD? previous = null;
            foreach (RoadLevel level in levels)
            {
                var perRoad = roads.Select(r => Band(r.Axis, r.Closed, r.Extent(level, true), r.Extent(level, false))).ToList();
                bands[level] = perRoad;
                var all = new PathsD();
                foreach (PathsD p in perRoad)
                {
                    all.AddRange(p);
                }

                PathsD closed = Union(all);
                PathsD corners = JunctionCorners(perRoad, radii[level]);
                if (corners.Count > 0)
                {
                    closed = Clipper.Union(closed, corners, FillRule.NonZero, Precision);
                }

                if (previous != null)
                {
                    closed = Clipper.Union(closed, previous, FillRule.NonZero, Precision);
                }

                outline[level] = closed;
                previous = closed;
            }

            bool anyBike = bikeMax > 0;
            bool anyWalk = walkMax > 0;
            foreach (RoadLevel level in levels)
            {
                PathsD region = level == RoadLevel.Carriageway ? outline[level] : Clipper.Difference(outline[level], outline[level - 1], FillRule.NonZero, Precision);
                result.Bands[level] = Regions(region, origin);
                bool draw = level == RoadLevel.Carriageway || level == RoadLevel.Kerb || (level == RoadLevel.Bike && anyBike) || (level >= RoadLevel.Sidewalk && anyWalk);
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

                        notes.Add("Saobraćajnica " + r.Id + " je produžena " + Fmt(best) + " m do osovine saobraćajnice " + target.Id + ".");
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

        /// <summary>Area within <paramref name="left"/> metres left and <paramref name="right"/> metres right of the axis, square ends.</summary>
        internal static PathsD Band(List<Vec2> axis, bool closed, double left, double right)
        {
            var paths = new PathsD();
            int n = axis.Count;
            int segs = closed ? n : n - 1;
            for (int s = 0; s < segs; s++)
            {
                Vec2 p = axis[s], q = axis[(s + 1) % n];
                Vec2 u = (q - p).Normalized();
                Vec2 nrm = u.Perp();
                paths.Add(Positive(new[] { p + (nrm * left), q + (nrm * left), q - (nrm * right), p - (nrm * right) }));
            }

            // joints: a round fan on the outer side of each bend
            for (int v = closed ? 0 : 1; v < (closed ? n : n - 1); v++)
            {
                Vec2 prev = axis[(v - 1 + n) % n], cur = axis[v], next = axis[(v + 1) % n];
                Vec2 u1 = (cur - prev).Normalized(), u2 = (next - cur).Normalized();
                double turn = u1.Cross(u2);
                if (Math.Abs(turn) < 1e-9 && u1.Dot(u2) > 0)
                {
                    continue;
                }

                bool outerRight = turn > 0;
                double d = outerRight ? right : left;
                if (d <= 0)
                {
                    continue;
                }

                Vec2 a1 = outerRight ? -u1.Perp() : u1.Perp();
                Vec2 a2 = outerRight ? -u2.Perp() : u2.Perp();
                double start = a1.Angle;
                double sweep = Math.Atan2(a1.Cross(a2), a1.Dot(a2));
                int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 36)));
                var fan = new List<Vec2> { cur };
                for (int k = 0; k <= steps; k++)
                {
                    double ang = start + (sweep * k / steps);
                    fan.Add(cur + (new Vec2(Math.Cos(ang), Math.Sin(ang)) * d));
                }

                paths.Add(Positive(fan));
            }

            return paths;
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
                    if (overlap.Sum(p => Math.Abs(Clipper.Area(p))) < 0.01)
                    {
                        continue;
                    }

                    PathsD zone = Clipper.InflatePaths(overlap, Math.Max(4 * r, 3), JoinType.Round, EndType.Polygon, 2, Precision, 0.05);
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

            others.AddRange(gaps);
            if (gaps.Count > 0)
            {
                othersWide.AddRange(Clipper.InflatePaths(gaps, 3.0, JoinType.Miter, EndType.Polygon, 4, Precision));
            }
            others = Union(others);
            othersWide = Union(othersWide);

            // lines between lanes (offset from the axis, left positive)
            var lines = new List<(double Offset, bool Centre)>();
            double lw = r.LaneWidth;
            if (r.OneWay)
            {
                for (int k = 1; k < r.TotalLanes; k++)
                {
                    lines.Add((r.CarriagewayLeft - (k * lw), false));
                }
            }
            else
            {
                lines.Add((0, true));
                for (int k = 1; k < r.LanesLeft; k++)
                {
                    lines.Add((k * lw, false));
                }

                for (int k = 1; k < r.LanesRight; k++)
                {
                    lines.Add((-k * lw, false));
                }
            }

            // a two-way road with two or more lanes in each direction gets a continuous centre line
            bool solidCentre = !r.OneWay && r.LanesLeft >= 2 && r.LanesRight >= 2;
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
                    lanes.Add((-(k + 0.5) * lw, true));
                }

                for (int k = 0; k < r.LanesLeft; k++)
                {
                    lanes.Add(((k + 0.5) * lw, false));
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
            c.Axis = new List<Vec2>(r.Axis);
            c.Closed = r.Closed;
            return c;
        }

        private static string Fmt(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
