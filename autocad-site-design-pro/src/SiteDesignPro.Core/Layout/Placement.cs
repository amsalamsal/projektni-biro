using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Layout
{
    /// <summary>A computed insertion: position plus rotation (radians CCW from +X).</summary>
    public readonly record struct Placement(Vec3 Position, double Rotation);

    public enum SpacingMode
    {
        /// <summary>Exact spacing from the start offset; the remainder is left at the end.</summary>
        Fixed,

        /// <summary>Spacing adjusted (never exceeding the requested spacing) so items fill the usable length evenly, both ends included.</summary>
        Fit,
    }

    /// <summary>Repeats insertions along a path (trees, lights, bollards, benches).</summary>
    public static class PathPlacement
    {
        /// <param name="ring">Path in any consistent length unit.</param>
        /// <param name="spacing">Spacing in the same unit (&gt; 0).</param>
        /// <param name="lateralOffset">Offset to the left (+) or right (-) of the path direction.</param>
        /// <param name="rotationOffset">Added to the path tangent angle (radians).</param>
        public static List<Placement> Distribute(Ring ring, bool closed, double spacing, double startOffset = 0, double endOffset = 0,
            SpacingMode mode = SpacingMode.Fixed, double lateralOffset = 0, double rotationOffset = 0, int maxCount = 100000)
        {
            if (!(spacing > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), "Spacing must be positive.");
            }

            double length = GeometryMath.Length(ring, closed);
            double usable = length - startOffset - endOffset;
            var result = new List<Placement>();
            if (usable < -1e-9)
            {
                return result;
            }

            usable = Math.Max(0, usable);
            int count;
            double step;
            if (mode == SpacingMode.Fit)
            {
                int gaps = (int)Math.Ceiling((usable / spacing) - 1e-9);
                if (closed && Math.Abs(startOffset) < 1e-12 && Math.Abs(endOffset) < 1e-12)
                {
                    // Closed loop: the end coincides with the start, so do not place twice.
                    gaps = Math.Max(1, gaps);
                    count = gaps;
                }
                else
                {
                    count = gaps + 1;
                }

                step = gaps > 0 ? usable / gaps : 0;
            }
            else
            {
                count = (int)Math.Floor((usable / spacing) + 1e-9) + 1;
                if (closed && Math.Abs(startOffset) < 1e-12 && Math.Abs(endOffset) < 1e-12 && Math.Abs(((count - 1) * spacing) - length) < 1e-9)
                {
                    count--; // last item would duplicate the first
                }

                step = spacing;
            }

            if (count > maxCount)
            {
                throw new InvalidOperationException("Placement would create " + count + " items (limit " + maxCount + "). Check spacing and units.");
            }

            for (int i = 0; i < count; i++)
            {
                double d = startOffset + (i * step);
                (Vec3 p, double angle) = GeometryMath.PointAtDistance(ring, closed, d);
                if (lateralOffset != 0)
                {
                    Vec2 left = new Vec2(Math.Cos(angle), Math.Sin(angle)).Perp() * lateralOffset;
                    p = new Vec3(p.X + left.X, p.Y + left.Y, p.Z);
                }

                result.Add(new Placement(p, GeometryMath.NormalizeAngle(angle + rotationOffset)));
            }

            return result;
        }
    }

    /// <summary>Generates rows of parking bays along an aisle edge.</summary>
    public static class ParkingLayout
    {
        /// <summary>
        /// Bays along the line start-end. Each placement is the centre of the bay's aisle edge with rotation such
        /// that the bay (local +Y) extends to the chosen side. Angle is the bay angle to the aisle in degrees (90 = perpendicular).
        /// </summary>
        public static List<Placement> Row(Vec3 start, Vec3 end, double bayWidth, bool toLeft, double angleDegrees = 90, int? count = null)
        {
            if (!(bayWidth > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(bayWidth));
            }

            if (angleDegrees < 30 || angleDegrees > 90)
            {
                throw new ArgumentOutOfRangeException(nameof(angleDegrees), "Bay angle must be between 30 and 90 degrees.");
            }

            Vec2 dir = end.XY - start.XY;
            double length = dir.Length;
            Vec2 u = dir.Normalized();
            double alpha = angleDegrees * Math.PI / 180.0;
            double pitch = bayWidth / Math.Sin(alpha);
            int n = count ?? (int)Math.Floor((length / pitch) + 1e-9);
            Vec2 bayAxis = toLeft ? u.Rotate(alpha) : u.Rotate(-alpha);
            double rotation = GeometryMath.NormalizeAngle(bayAxis.Angle - (Math.PI / 2));
            var list = new List<Placement>(Math.Max(0, n));
            for (int i = 0; i < n; i++)
            {
                double d = pitch * (i + 0.5);
                double t = length > 0 ? d / length : 0;
                Vec3 p = Vec3.Lerp(start, end, t);
                list.Add(new Placement(p, rotation));
            }

            return list;
        }
    }

    /// <summary>Regular grids of insertion points inside an area (planting grids, orchards).</summary>
    public static class GridLayout
    {
        /// <param name="outline">Closed outline (any unit).</param>
        /// <param name="angle">Grid direction (radians).</param>
        /// <param name="margin">Minimum distance from the outline.</param>
        /// <param name="staggered">Offset every second row by half the spacing (triangular planting).</param>
        public static List<Vec2> InPolygon(IReadOnlyList<Vec2> outline, double spacingX, double spacingY, double angle = 0, double margin = 0, bool staggered = false, int maxCount = 200000)
        {
            if (!(spacingX > 0) || !(spacingY > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(spacingX), "Grid spacing must be positive.");
            }

            var result = new List<Vec2>();
            if (outline.Count < 3)
            {
                return result;
            }

            Vec2 origin = outline[0];
            List<Vec2> local = outline.Select(p => (p - origin).Rotate(-angle)).ToList();
            Box2 b = GeometryMath.BoundsOf(local);
            long estimate = (long)((b.Width / spacingX) + 2) * (long)((b.Height / spacingY) + 2);
            if (estimate > maxCount * 4L)
            {
                throw new InvalidOperationException("Grid would contain too many points; check spacing and units.");
            }

            int row = 0;
            for (double y = b.MinY + (spacingY / 2); y <= b.MaxY; y += spacingY, row++)
            {
                double shift = staggered && (row % 2 == 1) ? spacingX / 2 : 0;
                for (double x = b.MinX + (spacingX / 2) + shift; x <= b.MaxX; x += spacingX)
                {
                    var q = new Vec2(x, y);
                    if (GeometryMath.PointInPolygon(q, local) && (margin <= 0 || GeometryMath.DistanceToBoundary(q, local) >= margin))
                    {
                        result.Add(origin + q.Rotate(angle));
                        if (result.Count > maxCount)
                        {
                            throw new InvalidOperationException("Grid exceeds " + maxCount + " points.");
                        }
                    }
                }
            }

            return result;
        }
    }
}
