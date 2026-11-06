using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Layout
{
    /// <summary>Design vehicle for the swept path check (metres).</summary>
    public sealed class DesignVehicle
    {
        public DesignVehicle(string key, string name, double length, double width, double wheelbase, double frontOverhang, double minOuterRadius)
        {
            Key = key;
            Name = name;
            Length = length;
            Width = width;
            Wheelbase = wheelbase;
            FrontOverhang = frontOverhang;
            MinOuterRadius = minOuterRadius;
        }

        public string Key { get; }

        public string Name { get; }

        public double Length { get; }

        public double Width { get; }

        /// <summary>Distance between the steering front axle and the (effective) rear axle.</summary>
        public double Wheelbase { get; }

        public double FrontOverhang { get; }

        public double RearOverhang => Math.Max(0, Length - Wheelbase - FrontOverhang);

        /// <summary>Smallest outer turning radius (wall-to-wall circle of the front outer corner).</summary>
        public double MinOuterRadius { get; }

        /// <summary>Largest steering angle of the front axle, derived from the outer turning radius.</summary>
        public double MaxSteer
        {
            get
            {
                // outer front corner: R_out² = (Rr + W/2)² + (L + Ff)² with Rr the rear axle centre radius
                double front = Wheelbase + FrontOverhang;
                double rr = Math.Sqrt(Math.Max(1e-6, (MinOuterRadius * MinOuterRadius) - (front * front))) - (Width / 2);
                return Math.Atan2(Wheelbase, Math.Max(rr, 0.1));
            }
        }

        /// <summary>Typical vehicles for site planning (rounded values used in fire-access and refuse-collection checks).</summary>
        public static IReadOnlyList<DesignVehicle> Standard { get; } = new[]
        {
            new DesignVehicle("fire", "Vatrogasno vozilo", 10.0, 2.5, 5.0, 1.5, 10.5),
            new DesignVehicle("refuse", "Kamion za smeće (3 osovine)", 10.3, 2.55, 5.5, 1.6, 10.0),
            new DesignVehicle("van", "Dostavno vozilo", 7.0, 2.2, 4.0, 1.0, 7.5),
            new DesignVehicle("car", "Putničko vozilo", 4.8, 1.85, 2.8, 0.9, 5.8),
        };
    }

    /// <summary>One sampled position of the vehicle along the path.</summary>
    public sealed class VehiclePose
    {
        public VehiclePose(Vec2 front, Vec2 rear, double station, double steer)
        {
            Front = front;
            Rear = rear;
            Station = station;
            Steer = steer;
        }

        /// <summary>Centre of the front axle (follows the path).</summary>
        public Vec2 Front { get; }

        /// <summary>Centre of the rear axle (trails behind as a tractrix).</summary>
        public Vec2 Rear { get; }

        /// <summary>Distance travelled by the front axle along the path.</summary>
        public double Station { get; }

        /// <summary>Steering angle needed here (radians, signed).</summary>
        public double Steer { get; }

        public Vec2 Heading
        {
            get
            {
                Vec2 d = Front - Rear;
                double l = d.Length;
                return l > 1e-9 ? d * (1 / l) : new Vec2(1, 0);
            }
        }

        /// <summary>Body outline: rear-left, rear-right, front-right, front-left.</summary>
        public Vec2[] Body(DesignVehicle v)
        {
            Vec2 h = Heading, n = new Vec2(-h.Y, h.X);
            Vec2 back = Rear - (h * v.RearOverhang), nose = Front + (h * v.FrontOverhang);
            double w = v.Width / 2;
            return new[] { back + (n * w), back - (n * w), nose - (n * w), nose + (n * w) };
        }
    }

    /// <summary>
    /// Obstacle on the swept path: a circle (trunk, post) or a polygon (building, wall) the body must not touch, or a
    /// ground area (lawn, planted island, flower bed) the wheels must not run over while the body may overhang it.
    /// </summary>
    public sealed class SweptObstacle
    {
        private SweptObstacle(string id, string label, Vec2 centre, double radius, IReadOnlyList<Vec2>? polygon, bool wheelsOnly = false, IReadOnlyList<IReadOnlyList<Vec2>>? holes = null)
        {
            Id = id;
            Label = label;
            Centre = centre;
            Radius = radius;
            Polygon = polygon;
            WheelsOnly = wheelsOnly;
            Holes = holes ?? Array.Empty<IReadOnlyList<Vec2>>();
        }

        /// <summary>Only the wheels are checked (low area the body may overhang: lawn, island, planting).</summary>
        public bool WheelsOnly { get; }

        /// <summary>Openings in a ground area (for example a paved path cut out of a lawn).</summary>
        public IReadOnlyList<IReadOnlyList<Vec2>> Holes { get; }

        public string Id { get; }

        public string Label { get; }

        public Vec2 Centre { get; }

        public double Radius { get; }

        public IReadOnlyList<Vec2>? Polygon { get; }

        public static SweptObstacle Circle(string id, string label, Vec2 centre, double radius) => new SweptObstacle(id, label, centre, radius, null);

        public static SweptObstacle Area(string id, string label, IReadOnlyList<Vec2> polygon)
        {
            Vec2 c = new Vec2(polygon.Average(p => p.X), polygon.Average(p => p.Y));
            return new SweptObstacle(id, label, c, 0, polygon);
        }

        public static SweptObstacle Ground(string id, string label, IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>>? holes = null)
        {
            Vec2 c = new Vec2(outer.Average(p => p.X), outer.Average(p => p.Y));
            return new SweptObstacle(id, label, c, 0, outer, true, holes);
        }
    }

    /// <summary>Problem found on the swept path.</summary>
    public sealed class SweptConflict
    {
        public SweptConflict(Vec2 at, double station, string message, string? obstacleId)
        {
            At = at;
            Station = station;
            Message = message;
            ObstacleId = obstacleId;
        }

        public Vec2 At { get; }

        public double Station { get; }

        public string Message { get; }

        public string? ObstacleId { get; }
    }

    public sealed class SweptPathResult
    {
        public List<VehiclePose> Poses { get; } = new List<VehiclePose>();

        public List<SweptConflict> Conflicts { get; } = new List<SweptConflict>();

        /// <summary>Paths of the four body corners and the two outer wheel tracks, for drawing the swept envelope.</summary>
        public List<List<Vec2>> Traces { get; } = new List<List<Vec2>>();

        public double MaxSteerUsed { get; set; }

        public double Length { get; set; }
    }

    /// <summary>
    /// Swept path of a two-axle design vehicle: the front axle follows the drawn path, the rear axle trails along a
    /// tractrix (the standard low-speed model). Reports where the needed steering exceeds the vehicle's limit and where
    /// the body touches obstacles. A planning check, not a certified vehicle-tracking analysis.
    /// </summary>
    public static class SweptPath
    {
        public static SweptPathResult Run(IReadOnlyList<Vec2> path, DesignVehicle v, IEnumerable<SweptObstacle> obstacles, double step = 0.25, double clearance = 0.0)
        {
            var result = new SweptPathResult();
            List<Vec2> pts = Resample(path, step);
            if (pts.Count < 2)
            {
                return result;
            }

            double L = v.Wheelbase;
            Vec2 dir0 = Normalize(pts[1] - pts[0]);
            Vec2 rear = pts[0] - (dir0 * L);
            double station = 0;
            double maxSteer = v.MaxSteer;
            var corners = new List<Vec2>[4] { new List<Vec2>(), new List<Vec2>(), new List<Vec2>(), new List<Vec2>() };
            var leftTrack = new List<Vec2>();
            var rightTrack = new List<Vec2>();
            List<SweptObstacle> obs = obstacles.ToList();
            var hit = new HashSet<string>();
            bool steerFlag = false;
            for (int i = 0; i < pts.Count; i++)
            {
                Vec2 front = pts[i];
                if (i > 0)
                {
                    station += (pts[i] - pts[i - 1]).Length;
                    Vec2 d = front - rear;
                    rear = front - (Normalize(d) * L);
                }

                Vec2 tangent = i < pts.Count - 1 ? Normalize(pts[i + 1] - pts[i]) : Normalize(pts[i] - pts[i - 1]);
                Vec2 heading = Normalize(front - rear);
                double steer = Math.Atan2((heading.X * tangent.Y) - (heading.Y * tangent.X), (heading.X * tangent.X) + (heading.Y * tangent.Y));
                var pose = new VehiclePose(front, rear, station, steer);
                result.Poses.Add(pose);
                result.MaxSteerUsed = Math.Max(result.MaxSteerUsed, Math.Abs(steer));

                bool over = Math.Abs(steer) > maxSteer + 1e-3;
                if (over && !steerFlag)
                {
                    result.Conflicts.Add(new SweptConflict(front, station,
                        "Krivina je preoštra: potreban zaokret " + Deg(steer) + "°, vozilo može najviše " + Deg(maxSteer) + "° (vanjski radijus " + v.MinOuterRadius.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " m).", null));
                }

                steerFlag = over;
                Vec2[] body = pose.Body(v);
                for (int c = 0; c < 4; c++)
                {
                    corners[c].Add(body[c]);
                }

                Vec2 n = new Vec2(-heading.Y, heading.X);
                leftTrack.Add(rear + (n * (v.Width / 2 - 0.15)));
                rightTrack.Add(rear - (n * (v.Width / 2 - 0.15)));

                Vec2[] test = clearance > 0 ? Inflate(body, clearance) : body;
                double tw = (v.Width / 2) - 0.15;
                Vec2[] wheels = { rear + (n * tw), rear - (n * tw), front - (n * tw), front + (n * tw) };
                foreach (SweptObstacle o in obs)
                {
                    if (hit.Contains(o.Id))
                    {
                        continue;
                    }

                    if (o.WheelsOnly)
                    {
                        Vec2? at = WheelsOn(wheels, o);
                        if (at.HasValue)
                        {
                            hit.Add(o.Id);
                            result.Conflicts.Add(new SweptConflict(at.Value, station, "Točkovi prelaze preko površine " + o.Label + " – ostrvo, travnjak ili zasad nije vozna površina.", o.Id));
                        }

                        continue;
                    }

                    if (!Touches(test, o))
                    {
                        continue;
                    }

                    hit.Add(o.Id);
                    result.Conflicts.Add(new SweptConflict(o.Centre, station, "Vozilo udara u " + o.Label + ".", o.Id));
                }
            }

            result.Length = station;
            result.Traces.AddRange(corners);
            result.Traces.Add(leftTrack);
            result.Traces.Add(rightTrack);
            return result;
        }

        /// <summary>Points every <paramref name="step"/> metres along the polyline (vertices kept).</summary>
        public static List<Vec2> Resample(IReadOnlyList<Vec2> path, double step)
        {
            var pts = new List<Vec2>();
            if (path.Count == 0)
            {
                return pts;
            }

            pts.Add(path[0]);
            for (int i = 1; i < path.Count; i++)
            {
                Vec2 a = path[i - 1], b = path[i];
                double len = (b - a).Length;
                if (len < 1e-9)
                {
                    continue;
                }

                int n = Math.Max(1, (int)Math.Ceiling(len / step));
                for (int k = 1; k <= n; k++)
                {
                    pts.Add(a + ((b - a) * ((double)k / n)));
                }
            }

            return pts;
        }

        /// <summary>Point where the wheel footprint enters a ground area (inside it and not in one of its holes), or null.</summary>
        public static Vec2? WheelsOn(Vec2[] wheels, SweptObstacle o)
        {
            if (o.Polygon == null)
            {
                return null;
            }

            foreach (Vec2 p in wheels)
            {
                if (PointInPolygon(p, o.Polygon) && !o.Holes.Any(h => PointInPolygon(p, h)))
                {
                    return p;
                }
            }

            foreach (IReadOnlyList<Vec2> ring in new[] { o.Polygon }.Concat(o.Holes))
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    Vec2 a = wheels[i], b = wheels[(i + 1) % wheels.Length];
                    for (int j = 0; j < ring.Count; j++)
                    {
                        Vec2 c = ring[j], d = ring[(j + 1) % ring.Count];
                        if (SegmentsIntersect(a, b, c, d))
                        {
                            // crossing point of the wheel edge and the area edge
                            double den = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));
                            double t = Math.Abs(den) < 1e-12 ? 0.5 : (((c.X - a.X) * (d.Y - c.Y)) - ((c.Y - a.Y) * (d.X - c.X))) / den;
                            return a + ((b - a) * t);
                        }
                    }
                }
            }

            return null;
        }

        public static bool Touches(Vec2[] body, SweptObstacle o)
        {
            if (o.Polygon == null)
            {
                return PointInPolygon(o.Centre, body) || DistanceToPolygonEdges(o.Centre, body) <= o.Radius;
            }

            IReadOnlyList<Vec2> poly = o.Polygon;
            foreach (Vec2 p in body)
            {
                if (PointInPolygon(p, poly))
                {
                    return true;
                }
            }

            foreach (Vec2 p in poly)
            {
                if (PointInPolygon(p, body))
                {
                    return true;
                }
            }

            for (int i = 0; i < body.Length; i++)
            {
                Vec2 a = body[i], b = body[(i + 1) % body.Length];
                for (int j = 0; j < poly.Count; j++)
                {
                    if (SegmentsIntersect(a, b, poly[j], poly[(j + 1) % poly.Count]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Vec2[] Inflate(Vec2[] body, double d)
        {
            Vec2 c = new Vec2(body.Average(p => p.X), body.Average(p => p.Y));
            Vec2 ax = Normalize(body[3] - body[0]), ay = Normalize(body[1] - body[0]);
            return body.Select(p =>
            {
                Vec2 r = p - c;
                double sx = Math.Sign((r.X * ax.X) + (r.Y * ax.Y)), sy = Math.Sign((r.X * ay.X) + (r.Y * ay.Y));
                return p + (ax * (sx * d)) + (ay * (sy * d));
            }).ToArray();
        }

        private static string Deg(double rad) => Math.Round(Math.Abs(rad) * 180 / Math.PI).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static Vec2 Normalize(Vec2 v)
        {
            double l = v.Length;
            return l > 1e-12 ? v * (1 / l) : new Vec2(1, 0);
        }

        private static bool PointInPolygon(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                    (p.X < ((poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y)) + poly[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static double DistanceToPolygonEdges(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            double best = double.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                Vec2 a = poly[i], b = poly[(i + 1) % poly.Count];
                Vec2 ab = b - a;
                double t = Math.Max(0, Math.Min(1, (((p.X - a.X) * ab.X) + ((p.Y - a.Y) * ab.Y)) / Math.Max((ab.X * ab.X) + (ab.Y * ab.Y), 1e-12)));
                best = Math.Min(best, (p - (a + (ab * t))).Length);
            }

            return best;
        }

        private static bool SegmentsIntersect(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2)
        {
            double Cross(Vec2 o, Vec2 a, Vec2 b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));
            double d1 = Cross(q1, q2, p1), d2 = Cross(q1, q2, p2), d3 = Cross(p1, p2, q1), d4 = Cross(p1, p2, q2);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
    }
}
