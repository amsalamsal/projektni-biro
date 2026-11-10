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
            double bikeMax = roads.Max(r => Math.Max(r.BikeLeft, r.BikeRight));
            double walkMax = roads.Max(r => Math.Max(r.SidewalkLeft, r.SidewalkRight));
            result.ActualRadius = radius;
            result.EffectiveRadius = radius + bikeMax;

            // per-road carriageway and road surface up to the kerb line (lane lines and arrows use them)
            var bands = new Dictionary<RoadLevel, List<PathsD>>
            {
                [RoadLevel.Carriageway] = roads.Select(r => Band(r.Axis, r.Closed, r.Extent(RoadLevel.Carriageway, true), r.Extent(RoadLevel.Carriageway, false))).ToList(),
                [RoadLevel.Bike] = roads.Select(r => Band(r.Axis, r.Closed, r.Extent(RoadLevel.Bike, true), r.Extent(RoadLevel.Bike, false))).ToList(),
            };

            // the kerb line: road surface of all roads, inner junction corners rounded with the kerb radius
            var allSurface = new PathsD();
            foreach (PathsD p in bands[RoadLevel.Bike])
            {
                allSurface.AddRange(p);
            }

            PathsD raw = Union(allSurface);
            PathsD surface = raw;
            PathsD junctionCorners = JunctionCorners(bands[RoadLevel.Bike], radius);
            if (junctionCorners.Count > 0)
            {
                surface = Clipper.Union(raw, junctionCorners, FillRule.NonZero, Precision);
            }

            // the corner fillets the rounding added; every level is concentric with them
            List<PathsD> fillets = Clipper.Difference(surface, raw, FillRule.NonZero, Precision)
                .Where(f => Clipper.Area(f) > 0.02 && Clipper.Area(f) / Perimeter(f) > 0.02) // no slivers along shared edges
                .Select(f => new PathsD { f })
                .ToList();

            // bike lanes: the ring of their width inside the kerb line, along roads that have one and around the
            // corners next to them (tapering out where the other road has none)
            var bikePieces = new PathsD();
            foreach (double b in Widths(roads, (r, left) => left ? r.BikeLeft : r.BikeRight))
            {
                PathsD strips = Strips(roads, (r, left) => (left ? r.BikeLeft : r.BikeRight) == b, (r, left) => r.Extent(RoadLevel.Carriageway, left), (r, left) => r.Extent(RoadLevel.Bike, left));
                PathsD ring = Clipper.Difference(surface, Clipper.InflatePaths(surface, -b, JoinType.Round, EndType.Polygon, 2, Precision, ArcTolerance), FillRule.NonZero, Precision);
                bikePieces.AddRange(Clipper.Intersect(ring, Zone(strips, fillets, 0.05, 0), FillRule.NonZero, Precision));
            }

            PathsD bike = Union(bikePieces);
            PathsD carriageway = bike.Count > 0 ? Clipper.Difference(surface, bike, FillRule.NonZero, Precision) : surface;

            // outside the kerb line: rings of the kerb, sidewalk and edge widths around the road surface, along the roads
            // that have them and around their corners (concentric with the kerb line)
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

            double k = RoadSpec.KerbWidth;
            PathsD kerbStrips = Strips(roads, (r, left) => r.Kerbs, (r, left) => r.Extent(RoadLevel.Bike, left), (r, left) => r.Extent(RoadLevel.Kerb, left));
            PathsD kerb = kerbStrips.Count > 0 ? Union(Clipper.Intersect(Ring(0, k), Zone(kerbStrips, fillets, 0.05, k + 0.05), FillRule.NonZero, Precision)) : new PathsD();

            var walkPieces = new PathsD();
            var edgePieces = new PathsD();
            foreach (double w in Widths(roads, (r, left) => r.Kerbs ? (left ? r.SidewalkLeft : r.SidewalkRight) : 0))
            {
                bool Has(RoadSpec r, bool left) => r.Kerbs && (left ? r.SidewalkLeft : r.SidewalkRight) == w;
                PathsD walkStrips = Strips(roads, Has, (r, left) => r.Extent(RoadLevel.Kerb, left), (r, left) => r.Extent(RoadLevel.Sidewalk, left));
                PathsD edgeStrips = Strips(roads, Has, (r, left) => r.Extent(RoadLevel.Sidewalk, left), (r, left) => r.Extent(RoadLevel.Edge, left));
                List<PathsD> next = fillets.Where(f => Touches(f, walkStrips, k + 0.05)).ToList();
                walkPieces.AddRange(Clipper.Intersect(Ring(k, k + w), Zone(walkStrips, next, -1, k + w + 0.05), FillRule.NonZero, Precision));
                edgePieces.AddRange(Clipper.Intersect(Ring(k + w, k + w + RoadSpec.EdgeWidth), Zone(edgeStrips, next, -1, k + w + RoadSpec.EdgeWidth + 0.05), FillRule.NonZero, Precision));
            }

            PathsD inner = Clipper.Union(surface, kerb, FillRule.NonZero, Precision);
            PathsD walk = walkPieces.Count > 0 ? Clipper.Difference(Union(walkPieces), inner, FillRule.NonZero, Precision) : new PathsD();
            PathsD innerWalk = Clipper.Union(inner, walk, FillRule.NonZero, Precision);
            PathsD edge = edgePieces.Count > 0 ? Clipper.Difference(Union(edgePieces), innerWalk, FillRule.NonZero, Precision) : new PathsD();

            var region = new Dictionary<RoadLevel, PathsD>
            {
                [RoadLevel.Carriageway] = carriageway,
                [RoadLevel.Bike] = bike,
                [RoadLevel.Kerb] = kerb,
                [RoadLevel.Sidewalk] = walk,
                [RoadLevel.Edge] = edge,
            };
            var outline = new Dictionary<RoadLevel, PathsD>
            {
                [RoadLevel.Carriageway] = carriageway,
                [RoadLevel.Bike] = surface,
                [RoadLevel.Kerb] = inner,
                [RoadLevel.Sidewalk] = innerWalk,
                [RoadLevel.Edge] = Clipper.Union(innerWalk, edge, FillRule.NonZero, Precision),
            };

            bool anyBike = bike.Count > 0;
            bool anyWalk = walk.Count > 0;
            foreach (RoadLevel level in (RoadLevel[])Enum.GetValues(typeof(RoadLevel)))
            {
                result.Bands[level] = Regions(region[level], origin);
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

        /// <summary>Distinct positive widths of one element of the cross-section over all road sides.</summary>
        private static List<double> Widths(List<RoadSpec> roads, Func<RoadSpec, bool, double> width)
        {
            var list = new List<double>();
            foreach (RoadSpec r in roads)
            {
                foreach (bool left in new[] { true, false })
                {
                    double w = width(r, left);
                    if (w > 1e-6 && !list.Contains(w))
                    {
                        list.Add(w);
                    }
                }
            }

            return list;
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

        /// <summary>
        /// Rounds the bends of an axis drawn with straight segments, so the inner kerb of the bend gets the kerb radius
        /// and every edge of the cross-section is concentric (inner and outer side). Arcs drawn in the axis stay.
        /// </summary>
        internal static List<Vec2> Fillet(RoadSpec r)
        {
            List<Vec2> a = r.Axis;
            int n = a.Count;
            if (n < 3)
            {
                return a;
            }

            var result = new List<Vec2>(n * 4);
            for (int v = 0; v < n; v++)
            {
                Vec2 cur = a[v];
                bool interior = r.Closed || (v > 0 && v < n - 1);
                if (!interior || (r.Corners != null && !r.Corners.Any(c => c.DistanceTo(cur) < 1e-3)))
                {
                    result.Add(cur);
                    continue;
                }

                Vec2 prev = a[(v - 1 + n) % n], next = a[(v + 1) % n];
                double l1 = (cur - prev).Length, l2 = (next - cur).Length;
                if (l1 < 1e-6 || l2 < 1e-6)
                {
                    result.Add(cur);
                    continue;
                }

                Vec2 u1 = (cur - prev) * (1 / l1), u2 = (next - cur) * (1 / l2);
                double turn = Math.Atan2(u1.Cross(u2), u1.Dot(u2));
                double th = Math.Abs(turn);
                if (th < 0.01 || th > Math.PI - 0.01)
                {
                    result.Add(cur);
                    continue;
                }

                bool left = turn > 0; // the inner side of the bend
                double ra = r.CornerRadius + r.Extent(RoadLevel.Bike, left);
                double t = ra * Math.Tan(th / 2);
                bool prevEnd = !r.Closed && v - 1 == 0, nextEnd = !r.Closed && v + 1 == n - 1;
                double tMax = Math.Min(l1 * (prevEnd ? 0.98 : 0.5), l2 * (nextEnd ? 0.98 : 0.5));
                if (t > tMax)
                {
                    t = tMax;
                    ra = t / Math.Tan(th / 2);
                }

                Vec2 t1 = cur - (u1 * t);
                Vec2 centre = t1 + ((left ? u1.Perp() : -u1.Perp()) * ra);
                double a0 = (t1 - centre).Angle;
                int steps = Math.Max(2, (int)Math.Ceiling(th / (Math.PI / 36)));
                for (int s = 0; s <= steps; s++)
                {
                    double ang = a0 + (turn * s / steps);
                    result.Add(centre + (new Vec2(Math.Cos(ang), Math.Sin(ang)) * ra));
                }
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

        /// <summary>
        /// Removes millimetre back-steps (rounding where two offset boundaries meet) and zero-width spikes from a
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
                    bool tiny = la < 0.0025;
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
