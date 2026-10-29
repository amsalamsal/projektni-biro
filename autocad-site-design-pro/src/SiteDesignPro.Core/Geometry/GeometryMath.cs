using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteDesignPro.Core.Geometry
{
    /// <summary>Description of a circular arc segment derived from a bulge.</summary>
    public readonly record struct ArcSegment(Vec2 Center, double Radius, double StartAngle, double Sweep)
    {
        public double Length => Radius * Math.Abs(Sweep);

        public Vec2 PointAt(double t) => Center + new Vec2(Math.Cos(StartAngle + (Sweep * t)), Math.Sin(StartAngle + (Sweep * t))) * Radius;
    }

    /// <summary>Oriented rectangle: Origin is the corner at (minU, minV); U runs along the first boundary edge.</summary>
    public readonly record struct OrientedRect(Vec2 Origin, Vec2 U, Vec2 V, double LengthU, double LengthV, double Z)
    {
        public Vec2 At(double u, double v) => Origin + (U * u) + (V * v);

        public double Area => LengthU * LengthV;
    }

    /// <summary>Plan-geometry algorithms shared by validation, quantities, layout and symbol generation.</summary>
    public static class GeometryMath
    {
        public const double Epsilon = 1e-9;

        /// <summary>Arc for a segment with a non-zero bulge (tan(sweep/4), positive = counter-clockwise).</summary>
        public static ArcSegment ArcFromBulge(Vec2 a, Vec2 b, double bulge)
        {
            if (bulge == 0)
            {
                throw new ArgumentException("Bulge must be non-zero for an arc.", nameof(bulge));
            }

            Vec2 chord = b - a;
            double c = chord.Length;
            if (c < Epsilon)
            {
                return new ArcSegment(a, 0, 0, 0);
            }

            double sweep = 4.0 * Math.Atan(bulge);
            double radius = c * (1 + (bulge * bulge)) / (4 * Math.Abs(bulge));
            Vec2 mid = (a + b) * 0.5;
            Vec2 n = chord.Normalized().Perp();
            Vec2 center = mid + (n * (c / 2.0 * (1 - (bulge * bulge)) / (2 * bulge)));
            double start = (a - center).Angle;
            return new ArcSegment(center, radius, start, sweep);
        }

        public static double SegmentLength(Vec3 a, Vec3 b, double bulge)
        {
            if (bulge == 0)
            {
                return a.XY.DistanceTo(b.XY);
            }

            return ArcFromBulge(a.XY, b.XY, bulge).Length;
        }

        public static int SegmentCount(Ring ring, bool closed) => closed ? ring.Count : Math.Max(0, ring.Count - 1);

        /// <summary>Plan (2D) length of a path or ring perimeter including arcs.</summary>
        public static double Length(Ring ring, bool closed)
        {
            double total = 0;
            int n = SegmentCount(ring, closed);
            for (int i = 0; i < n; i++)
            {
                total += SegmentLength(ring.Vertices[i], ring.Vertices[(i + 1) % ring.Count], ring.BulgeAt(i));
            }

            return total;
        }

        /// <summary>Signed plan area of a closed ring including circular segments (counter-clockwise positive).</summary>
        public static double SignedArea(Ring ring)
        {
            int n = ring.Count;
            if (n < 2)
            {
                return 0;
            }

            double area = 0;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = ring.Vertices[i].XY;
                Vec2 b = ring.Vertices[(i + 1) % n].XY;
                area += a.Cross(b);
            }

            area *= 0.5;

            if (ring.HasArcs)
            {
                for (int i = 0; i < n; i++)
                {
                    double bulge = ring.BulgeAt(i);
                    if (bulge == 0)
                    {
                        continue;
                    }

                    ArcSegment arc = ArcFromBulge(ring.Vertices[i].XY, ring.Vertices[(i + 1) % n].XY, bulge);
                    double theta = Math.Abs(arc.Sweep);
                    double segment = arc.Radius * arc.Radius / 2.0 * (theta - Math.Sin(theta));
                    area += Math.Sign(bulge) * segment;
                }
            }

            return area;
        }

        public static double Area(PolygonGeometry polygon)
        {
            double area = Math.Abs(SignedArea(polygon.Outer));
            foreach (Ring hole in polygon.Holes)
            {
                area -= Math.Abs(SignedArea(hole));
            }

            return Math.Max(0, area);
        }

        /// <summary>
        /// Converts a ring with arcs to straight segments. Z is interpolated linearly along each segment.
        /// For an open path the last vertex is included; for a closed ring it is not repeated.
        /// </summary>
        public static List<Vec3> Tessellate(Ring ring, bool closed, double maxDeviation = 0.01, int maxSegmentsPerArc = 256)
        {
            var result = new List<Vec3>(ring.Count * 2);
            int n = SegmentCount(ring, closed);
            for (int i = 0; i < n; i++)
            {
                Vec3 a = ring.Vertices[i];
                Vec3 b = ring.Vertices[(i + 1) % ring.Count];
                result.Add(a);
                double bulge = ring.BulgeAt(i);
                if (bulge == 0)
                {
                    continue;
                }

                ArcSegment arc = ArcFromBulge(a.XY, b.XY, bulge);
                if (arc.Radius < Epsilon)
                {
                    continue;
                }

                int steps = ArcSteps(arc, maxDeviation, maxSegmentsPerArc);
                for (int s = 1; s < steps; s++)
                {
                    double t = (double)s / steps;
                    Vec2 p = arc.PointAt(t);
                    result.Add(new Vec3(p.X, p.Y, a.Z + ((b.Z - a.Z) * t)));
                }
            }

            if (!closed && ring.Count > 0)
            {
                result.Add(ring.Vertices[ring.Count - 1]);
            }

            return result;
        }

        public static int ArcSteps(ArcSegment arc, double maxDeviation, int maxSegments)
        {
            double tol = Math.Max(1e-6, Math.Min(maxDeviation, arc.Radius));
            double stepAngle = 2 * Math.Acos(Math.Max(-1, Math.Min(1, 1 - (tol / arc.Radius))));
            if (stepAngle < 1e-6)
            {
                return maxSegments;
            }

            int steps = (int)Math.Ceiling(Math.Abs(arc.Sweep) / stepAngle);
            return Math.Max(2, Math.Min(maxSegments, steps));
        }

        /// <summary>Point and direction (radians) at a plan distance along a path.</summary>
        public static (Vec3 Point, double Angle) PointAtDistance(Ring ring, bool closed, double distance)
        {
            int n = SegmentCount(ring, closed);
            if (n == 0)
            {
                return (ring.Count > 0 ? ring.Vertices[0] : Vec3.Zero, 0);
            }

            double remaining = Math.Max(0, distance);
            for (int i = 0; i < n; i++)
            {
                Vec3 a = ring.Vertices[i];
                Vec3 b = ring.Vertices[(i + 1) % ring.Count];
                double bulge = ring.BulgeAt(i);
                double len = SegmentLength(a, b, bulge);
                bool last = i == n - 1;
                if (remaining <= len || last)
                {
                    double t = len < Epsilon ? 0 : Math.Min(1, remaining / len);
                    double z = a.Z + ((b.Z - a.Z) * t);
                    if (bulge == 0)
                    {
                        Vec2 p = a.XY + ((b.XY - a.XY) * t);
                        return (p.WithZ(z), (b.XY - a.XY).Angle);
                    }

                    ArcSegment arc = ArcFromBulge(a.XY, b.XY, bulge);
                    Vec2 q = arc.PointAt(t);
                    double tangent = arc.StartAngle + (arc.Sweep * t) + (Math.Sign(arc.Sweep) * Math.PI / 2);
                    return (q.WithZ(z), NormalizeAngle(tangent));
                }

                remaining -= len;
            }

            return (ring.Vertices[ring.Count - 1], 0);
        }

        public static double NormalizeAngle(double angle)
        {
            double twoPi = 2 * Math.PI;
            angle %= twoPi;
            if (angle < 0)
            {
                angle += twoPi;
            }

            return angle;
        }

        /// <summary>Point-in-closed-ring test that honours arc segments (tessellated to 1 cm).</summary>
        public static bool PointInRing(Vec2 p, Ring ring) => PointInPolygon(p, Tessellate(ring, true).Select(v => v.XY).ToList());

        public static bool PointInPolygon(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vec2 a = poly[i], b = poly[j];
                if (((a.Y > p.Y) != (b.Y > p.Y)) && (p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>True when segments ab and cd cross at a single interior point (touching and collinear overlaps excluded).</summary>
        public static bool SegmentsCrossProperly(Vec2 a, Vec2 b, Vec2 c, Vec2 d, double eps = 1e-9)
        {
            double d1 = (b - a).Cross(c - a);
            double d2 = (b - a).Cross(d - a);
            double d3 = (d - c).Cross(a - c);
            double d4 = (d - c).Cross(b - c);
            double scale = Math.Max(1.0, Math.Max((b - a).Length, (d - c).Length));
            double tol = eps * scale * scale;
            return ((d1 > tol && d2 < -tol) || (d1 < -tol && d2 > tol)) && ((d3 > tol && d4 < -tol) || (d3 < -tol && d4 > tol));
        }

        /// <summary>Detects proper self-intersections of a closed ring (after tessellation).</summary>
        public static bool IsSelfIntersecting(Ring ring)
        {
            List<Vec2> pts = Tessellate(ring, true, 0.02).Select(v => v.XY).ToList();
            int n = pts.Count;
            if (n < 4)
            {
                return false;
            }

            for (int i = 0; i < n; i++)
            {
                Vec2 a = pts[i], b = pts[(i + 1) % n];
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1)
                    {
                        continue;
                    }

                    if (SegmentsCrossProperly(a, b, pts[j], pts[(j + 1) % n]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns a point strictly inside the polygon (scanline through the middle of the bounding box).</summary>
        public static Vec2? InteriorPoint(IReadOnlyList<Vec2> poly) => InteriorPoint(new[] { poly });

        /// <summary>
        /// Returns a point strictly inside an area given as rings (outer first, then holes). Even-odd scanline
        /// intervals exclude holes automatically.
        /// </summary>
        public static Vec2? InteriorPoint(IReadOnlyList<IReadOnlyList<Vec2>> rings)
        {
            if (rings.Count == 0 || rings[0].Count < 3)
            {
                return null;
            }

            double minY = rings[0].Min(p => p.Y), maxY = rings[0].Max(p => p.Y);
            foreach (double frac in new[] { 0.5, 0.37, 0.63, 0.21, 0.79, 0.11, 0.89, 0.05, 0.95 })
            {
                double y = minY + ((maxY - minY) * frac) + ((maxY - minY) * 1e-7);
                var xs = new List<double>();
                foreach (IReadOnlyList<Vec2> poly in rings)
                {
                    for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                    {
                        Vec2 a = poly[i], b = poly[j];
                        if ((a.Y > y) != (b.Y > y))
                        {
                            xs.Add(a.X + ((y - a.Y) * (b.X - a.X) / (b.Y - a.Y)));
                        }
                    }
                }

                xs.Sort();
                double bestWidth = 0;
                Vec2? best = null;
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    double w = xs[k + 1] - xs[k];
                    if (w > bestWidth)
                    {
                        bestWidth = w;
                        best = new Vec2((xs[k] + xs[k + 1]) / 2, y);
                    }
                }

                if (best.HasValue && bestWidth > 1e-6)
                {
                    return best;
                }
            }

            return null;
        }

        /// <summary>True when p lies inside the outer ring and outside every hole.</summary>
        public static bool PointInArea(Vec2 p, IReadOnlyList<IReadOnlyList<Vec2>> rings)
        {
            if (rings.Count == 0 || !PointInPolygon(p, rings[0]))
            {
                return false;
            }

            for (int h = 1; h < rings.Count; h++)
            {
                if (PointInPolygon(p, rings[h]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when two closed areas share interior area: edges cross properly, or one contains an interior
        /// point of the other. Polygons that only touch along a shared edge are not reported.
        /// </summary>
        public static bool PolygonsOverlap(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b) => AreasOverlap(new[] { a }, new[] { b });

        /// <summary>Overlap test for areas with holes (rings: outer first, then holes).</summary>
        public static bool AreasOverlap(IReadOnlyList<IReadOnlyList<Vec2>> a, IReadOnlyList<IReadOnlyList<Vec2>> b)
        {
            if (a.Count == 0 || b.Count == 0 || !BoundsOf(a[0]).Intersects(BoundsOf(b[0])))
            {
                return false;
            }

            foreach (IReadOnlyList<Vec2> ra in a)
            {
                for (int i = 0; i < ra.Count; i++)
                {
                    Vec2 a1 = ra[i], a2 = ra[(i + 1) % ra.Count];
                    foreach (IReadOnlyList<Vec2> rb in b)
                    {
                        for (int j = 0; j < rb.Count; j++)
                        {
                            if (SegmentsCrossProperly(a1, a2, rb[j], rb[(j + 1) % rb.Count]))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // No proper crossing: each region is either inside or outside the other except along shared
            // boundaries. Probe several interior samples of each area (several scanlines, several points per span).
            foreach (Vec2 pa in InteriorSamples(a))
            {
                if (PointInArea(pa, b) && b.All(r => DistanceToBoundary(pa, r) > 1e-6))
                {
                    return true;
                }
            }

            foreach (Vec2 pb in InteriorSamples(b))
            {
                if (PointInArea(pb, a) && a.All(r => DistanceToBoundary(pb, r) > 1e-6))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Sample points strictly inside an area (outer ring first, then holes).</summary>
        public static IEnumerable<Vec2> InteriorSamples(IReadOnlyList<IReadOnlyList<Vec2>> rings)
        {
            if (rings.Count == 0 || rings[0].Count < 3)
            {
                yield break;
            }

            double minY = rings[0].Min(p => p.Y), maxY = rings[0].Max(p => p.Y);
            foreach (double frac in new[] { 0.5, 0.37, 0.63, 0.21, 0.79, 0.11, 0.89, 0.03, 0.97 })
            {
                double y = minY + ((maxY - minY) * frac) + ((maxY - minY) * 1e-7);
                var xs = new List<double>();
                foreach (IReadOnlyList<Vec2> poly in rings)
                {
                    for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                    {
                        Vec2 p = poly[i], q = poly[j];
                        if ((p.Y > y) != (q.Y > y))
                        {
                            xs.Add(p.X + ((y - p.Y) * (q.X - p.X) / (q.Y - p.Y)));
                        }
                    }
                }

                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    double w = xs[k + 1] - xs[k];
                    if (w <= 1e-6)
                    {
                        continue;
                    }

                    foreach (double t in new[] { 0.5, 0.13, 0.87, 0.31, 0.69 })
                    {
                        yield return new Vec2(xs[k] + (w * t), y);
                    }
                }
            }
        }

        public static double DistanceToBoundary(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            double best = double.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                best = Math.Min(best, DistancePointSegment(p, poly[i], poly[(i + 1) % poly.Count]));
            }

            return best;
        }

        public static double DistancePointSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double l2 = ab.Dot(ab);
            if (l2 < Epsilon)
            {
                return p.DistanceTo(a);
            }

            double t = Math.Max(0, Math.Min(1, (p - a).Dot(ab) / l2));
            return p.DistanceTo(a + (ab * t));
        }

        public static Box2 BoundsOf(IEnumerable<Vec2> pts)
        {
            Box2 b = Box2.Empty;
            foreach (Vec2 p in pts)
            {
                b = b.Include(p);
            }

            return b;
        }

        /// <summary>Bounding rectangle aligned with the first boundary edge, oriented so the area lies on +V.</summary>
        public static OrientedRect OrientedBoundingRect(Ring ring)
        {
            List<Vec3> pts = Tessellate(ring, true, 0.01);
            if (pts.Count < 2)
            {
                throw new ArgumentException("Ring needs at least two vertices.", nameof(ring));
            }

            Vec2 o = pts[0].XY;
            Vec2 u = (ring.Vertices[1].XY - ring.Vertices[0].XY).Normalized();
            if (u.Length < 0.5)
            {
                u = new Vec2(1, 0);
            }

            Vec2 v = u.Perp();
            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            foreach (Vec3 p3 in pts)
            {
                Vec2 d = p3.XY - o;
                double pu = d.Dot(u), pv = d.Dot(v);
                minU = Math.Min(minU, pu);
                maxU = Math.Max(maxU, pu);
                minV = Math.Min(minV, pv);
                maxV = Math.Max(maxV, pv);
            }

            if (Math.Abs(minV) > Math.Abs(maxV))
            {
                // Area lies to the right of the first edge: flip V so it points into the area.
                v = new Vec2(-v.X, -v.Y);
                double t = minV;
                minV = -maxV;
                maxV = -t;
            }

            Vec2 origin = o + (u * minU) + (v * minV);
            return new OrientedRect(origin, u, v, maxU - minU, maxV - minV, ring.Vertices[0].Z);
        }

        /// <summary>True when the ring is a rectangle (4 straight sides, right angles within tolerance).</summary>
        public static bool IsRectangle(Ring ring, double angleTolDeg = 0.5)
        {
            if (ring.HasArcs || ring.Count != 4)
            {
                return false;
            }

            double tol = Math.Sin(angleTolDeg * Math.PI / 180.0);
            for (int i = 0; i < 4; i++)
            {
                Vec2 a = (ring.Vertices[(i + 1) % 4].XY - ring.Vertices[i].XY).Normalized();
                Vec2 b = (ring.Vertices[(i + 2) % 4].XY - ring.Vertices[(i + 1) % 4].XY).Normalized();
                if (Math.Abs(a.Dot(b)) > tol)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Removes consecutive duplicate vertices (and a closing duplicate of the first vertex).</summary>
        public static Ring RemoveDuplicateVertices(Ring ring, bool closed, double tol = 1e-6)
        {
            var verts = new List<Vec3>();
            var bulges = new List<double>();
            for (int i = 0; i < ring.Count; i++)
            {
                Vec3 v = ring.Vertices[i];
                if (verts.Count > 0 && (verts[verts.Count - 1] - v).Length <= tol)
                {
                    bulges[bulges.Count - 1] = ring.BulgeAt(i);
                    continue;
                }

                verts.Add(v);
                bulges.Add(ring.BulgeAt(i));
            }

            if (closed && verts.Count > 1 && (verts[0] - verts[verts.Count - 1]).Length <= tol)
            {
                verts.RemoveAt(verts.Count - 1);
                bulges.RemoveAt(bulges.Count - 1);
            }

            return new Ring(verts, bulges);
        }

        public static IReadOnlyList<Vec2> PlanOutline(PolygonGeometry polygon, double maxDeviation = 0.02)
            => Tessellate(polygon.Outer, true, maxDeviation).Select(v => v.XY).ToList();
    }
}
