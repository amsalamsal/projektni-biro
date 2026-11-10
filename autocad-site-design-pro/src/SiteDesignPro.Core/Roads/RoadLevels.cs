using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clipper2Lib;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>An elevation mark placed on a road: height relative to the base elevation of the roads (m).</summary>
    public sealed class RoadLevelMark
    {
        public RoadLevelMark(string id, Vec2 position, double value)
        {
            Id = id;
            Position = position;
            Value = value;
        }

        public string Id { get; }

        /// <summary>Position in metres (package or network frame).</summary>
        public Vec2 Position { get; }

        /// <summary>Height above (+) or below (−) the base elevation of the roads, in metres.</summary>
        public double Value { get; }
    }

    /// <summary>Heights along one road: marks projected on its axis, linear in between, level beyond the first and last.</summary>
    public sealed class RoadProfile
    {
        public RoadProfile(RoadSpec road, double length, List<(double Station, double Height)> points)
        {
            Road = road;
            Length = length;
            Points = points;
        }

        public RoadSpec Road { get; }

        public double Length { get; }

        /// <summary>Stations (m from the start of the axis) and heights, sorted by station.</summary>
        public List<(double Station, double Height)> Points { get; }

        public double At(double station)
        {
            if (Points.Count == 0)
            {
                return 0;
            }

            if (station <= Points[0].Station)
            {
                return Points[0].Height;
            }

            for (int i = 1; i < Points.Count; i++)
            {
                if (station <= Points[i].Station)
                {
                    (double s0, double h0) = Points[i - 1];
                    (double s1, double h1) = Points[i];
                    return s1 - s0 < 1e-9 ? h1 : h0 + ((h1 - h0) * (station - s0) / (s1 - s0));
                }
            }

            return Points[Points.Count - 1].Height;
        }
    }

    /// <summary>A stretch of a road between two marks with its longitudinal slope.</summary>
    public sealed class RoadSlope
    {
        public RoadSlope(string roadName, double from, double to, double length, double rise, double limit)
        {
            RoadName = roadName;
            From = from;
            To = to;
            Length = length;
            Rise = rise;
            Limit = limit;
        }

        public string RoadName { get; }

        public double From { get; }

        public double To { get; }

        public double Length { get; }

        public double Rise { get; }

        /// <summary>Longitudinal slope (rise / length).</summary>
        public double Slope => Length < 1e-9 ? 0 : Math.Abs(Rise) / Length;

        public double Limit { get; }

        public bool TooSteep => Slope > Limit + 1e-9;
    }

    /// <summary>
    /// Heights of the roads from elevation marks: every mark is projected onto the nearest road axis, heights are
    /// linear between the marks of a road and level before the first and after the last one. A road without marks
    /// lies at the base elevation (0). Surfaces get their heights from the road they belong to (nearest axis).
    /// </summary>
    public static class RoadLevels
    {
        /// <summary>Largest longitudinal slope of a road (10 %).</summary>
        public const double MaxRoadSlope = 0.10;

        /// <summary>Largest longitudinal slope of a garage ramp (17 %).</summary>
        public const double MaxRampSlope = 0.17;

        /// <summary>Spacing of the height grid on the road surfaces (50 × 50 cm).</summary>
        public const double GridSpacing = 0.5;

        public static List<RoadProfile> Profiles(IReadOnlyList<RoadSpec> roads, IEnumerable<RoadLevelMark> marks)
        {
            var points = roads.Select(_ => new List<(double, double)>()).ToList();
            foreach (RoadLevelMark m in marks)
            {
                int best = -1;
                double bestGap = double.MaxValue, bestStation = 0;
                for (int i = 0; i < roads.Count; i++)
                {
                    RoadSpec r = roads[i];
                    (double station, double distance) = Project(r.Axis, r.Closed, m.Position);
                    double half = Math.Max(r.Extent(RoadLevel.Edge, true), r.Extent(RoadLevel.Edge, false));
                    double gap = distance - half;
                    if (gap < 1.0 && gap < bestGap)
                    {
                        best = i;
                        bestGap = gap;
                        bestStation = station;
                    }
                }

                if (best >= 0)
                {
                    points[best].Add((bestStation, m.Value));
                }
            }

            var list = new List<RoadProfile>();
            for (int i = 0; i < roads.Count; i++)
            {
                list.Add(new RoadProfile(roads[i], Length(roads[i].Axis, roads[i].Closed), points[i].OrderBy(p => p.Item1).ToList()));
            }

            return list;
        }

        /// <summary>Slopes between consecutive marks of every road, with the limit for roads (10 %).</summary>
        public static List<RoadSlope> Slopes(IEnumerable<RoadProfile> profiles, double limit = MaxRoadSlope)
        {
            var list = new List<RoadSlope>();
            foreach (RoadProfile p in profiles)
            {
                string name = p.Road.DisplayName;
                for (int i = 1; i < p.Points.Count; i++)
                {
                    (double s0, double h0) = p.Points[i - 1];
                    (double s1, double h1) = p.Points[i];
                    if (s1 - s0 > 0.01)
                    {
                        list.Add(new RoadSlope(name, s0, s1, s1 - s0, h1 - h0, limit));
                    }
                }
            }

            return list;
        }

        /// <summary>Height of a point of the road surfaces: from the road whose axis is nearest.</summary>
        public static double Height(IReadOnlyList<RoadProfile> profiles, Vec2 p)
        {
            RoadProfile? best = null;
            double bestDistance = double.MaxValue, bestStation = 0;
            foreach (RoadProfile prof in profiles)
            {
                (double station, double distance) = Project(prof.Road.Axis, prof.Road.Closed, p);
                double half = Math.Max(prof.Road.Extent(RoadLevel.Edge, true), prof.Road.Extent(RoadLevel.Edge, false));
                double gap = distance - half;
                if (gap < bestDistance)
                {
                    best = prof;
                    bestDistance = gap;
                    bestStation = station;
                }
            }

            return best?.At(bestStation) ?? 0;
        }

        /// <summary>
        /// Triangles of a surface region with heights (x, y, z per corner): the outline is divided at the grid
        /// spacing so the surface follows the road profile, then triangulated.
        /// </summary>
        public static List<Vec3[]> Drape(RoadRegion region, IReadOnlyList<RoadProfile> profiles)
        {
            var paths = new PathsD { Densify(region.Outer) };
            foreach (RoadLoop hole in region.Holes)
            {
                paths.Add(Densify(hole));
            }

            var list = new List<Vec3[]>();
            if (Clipper.Triangulate(paths, 4, out PathsD tris) != TriangulateResult.success)
            {
                return list;
            }

            foreach (PathD t in tris.Where(t => t.Count == 3))
            {
                list.Add(t.Select(q => new Vec3(q.x, q.y, Height(profiles, new Vec2(q.x, q.y)))).ToArray());
            }

            return list;
        }

        /// <summary>Triangles as text for the package (x,y,z;x,y,z;x,y,z|...) in invariant culture.</summary>
        public static string Encode(IEnumerable<Vec3[]> triangles)
        {
            string N(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            return string.Join("|", triangles.Select(t => string.Join(";", t.Select(p => N(p.X) + "," + N(p.Y) + "," + N(p.Z)))));
        }

        private static PathD Densify(RoadLoop loop)
        {
            var path = new PathD();
            List<Vec2> pts = loop.Points.ToList();
            for (int i = 0; i < pts.Count; i++)
            {
                Vec2 a = pts[i], b = pts[(i + 1) % pts.Count];
                double bulge = loop.Bulges[i];
                if (Math.Abs(bulge) > 1e-9)
                {
                    // arc: points at most a grid step apart along the chord
                    double chord = a.DistanceTo(b);
                    double sweep = 4 * Math.Atan(bulge);
                    double r = chord / (2 * Math.Sin(Math.Abs(sweep) / 2));
                    int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) * r / GridSpacing));
                    Vec2 mid = (a + b) * 0.5;
                    Vec2 u = (b - a).Normalized();
                    double h = r * Math.Cos(sweep / 2);
                    Vec2 c = mid + (u.Perp() * (h * Math.Sign(bulge)));
                    double a0 = (a - c).Angle;
                    for (int k = 0; k < steps; k++)
                    {
                        double ang = a0 + (sweep * k / steps);
                        Vec2 q = c + (new Vec2(Math.Cos(ang), Math.Sin(ang)) * r);
                        path.Add(new PointD(q.X, q.Y));
                    }
                }
                else
                {
                    double len = a.DistanceTo(b);
                    int steps = Math.Max(1, (int)Math.Ceiling(len / GridSpacing));
                    for (int k = 0; k < steps; k++)
                    {
                        Vec2 q = a + ((b - a) * ((double)k / steps));
                        path.Add(new PointD(q.X, q.Y));
                    }
                }
            }

            return path;
        }

        /// <summary>Point and direction of an axis at a station.</summary>
        public static (Vec2 Point, Vec2 Direction) PointAt(List<Vec2> axis, bool closed, double station) => RoadNetwork.PointAt(axis, closed, station);

        /// <summary>Station along the axis of the point nearest to p, and the distance to it.</summary>
        public static (double Station, double Distance) Project(IReadOnlyList<Vec2> axis, bool closed, Vec2 p)
        {
            double best = double.MaxValue, bestStation = 0, run = 0;
            int n = axis.Count;
            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                Vec2 a = axis[i], b = axis[(i + 1) % n];
                Vec2 ab = b - a;
                double len = ab.Length;
                double t = len < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (p - a).Dot(ab) / (len * len)));
                double d = p.DistanceTo(a + (ab * t));
                if (d < best)
                {
                    best = d;
                    bestStation = run + (t * len);
                }

                run += len;
            }

            return (bestStation, best);
        }

        private static double Length(IReadOnlyList<Vec2> axis, bool closed)
        {
            double sum = 0;
            int n = axis.Count;
            for (int i = 0; i < (closed ? n : n - 1); i++)
            {
                sum += axis[i].DistanceTo(axis[(i + 1) % n]);
            }

            return sum;
        }
    }
}
