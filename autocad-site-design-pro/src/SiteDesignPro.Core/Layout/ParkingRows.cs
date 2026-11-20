using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Layout
{
    /// <summary>One bay of a row: where it stands (middle of its edge at the aisle), how it is turned, its markings.</summary>
    public sealed class ParkingRowBay
    {
        public Vec2 Position { get; set; }

        /// <summary>Rotation of the bay's frame (radians): local +Y goes into the bay, to the kerb.</summary>
        public double Rotation { get; set; }

        /// <summary>Width along the row (with the hatched area of an accessible bay).</summary>
        public double Width { get; set; }

        public double Depth { get; set; }

        public bool Accessible { get; set; }

        public double Hatch { get; set; }

        /// <summary>Number written in the bay (empty for an accessible bay).</summary>
        public string Number { get; set; } = string.Empty;

        public int RowDir { get; set; } = 1;

        public bool First { get; set; }

        public bool Last { get; set; }
    }

    /// <summary>An arrow in the aisle: its tail and direction (along the row).</summary>
    public readonly record struct ParkingArrow(Vec2 Position, double Rotation);

    /// <summary>
    /// Rows of parking bays as in the project standard: perpendicular 2,50 × 5,00 m (an accessible bay 2,50 m with
    /// a hatched area of 1,40 m and a kerb ramp, always before bay 1), parallel 5,50 × 2,50 m and at 45° (3,40 m
    /// along the row, 4,75 m deep). Aisle 3,00 m; along parallel and 45° rows an arrow in the aisle shows the
    /// direction. Units: metres (the caller scales).
    /// </summary>
    public static class ParkingRows
    {
        public const string Perpendicular = "perpendicular";
        public const string Parallel = "parallel";
        public const string Angled = "angled";

        public const double PerpendicularWidth = 2.50, PerpendicularDepth = 5.00;
        public const double AccessibleWidth = 2.50, AccessibleHatch = 1.40;
        public const double ParallelLength = 5.50, ParallelDepth = 2.50;
        public const double AngledPitch = 3.40, AngledDepth = 4.75;
        public const double Aisle = 3.00;
        public const double ArrowLength = 4.0, ArrowWidth = 0.6, ArrowShaft = 0.12;

        public static (double Width, double Depth) Size(string kind) => kind switch
        {
            Parallel => (ParallelLength, ParallelDepth),
            Angled => (AngledPitch, AngledDepth),
            _ => (PerpendicularWidth, PerpendicularDepth),
        };

        /// <summary>Length of the row along the kerb.</summary>
        public static double Length(string kind, int count, int accessible)
        {
            (double w, double d) = Size(kind);
            if (kind == Perpendicular)
            {
                return (accessible * (AccessibleWidth + AccessibleHatch)) + ((count - accessible) * w);
            }

            return (count * w) + (kind == Angled ? d : 0);
        }

        /// <summary>
        /// The bays of a row along the kerb from <paramref name="start"/> in the direction <paramref name="along"/>,
        /// on the side of <paramref name="aisleSide"/> (a point on the aisle side of the kerb line). The first
        /// <paramref name="accessible"/> bays (perpendicular rows only) are accessible; the others are numbered from
        /// <paramref name="firstNumber"/>.
        /// </summary>
        public static List<ParkingRowBay> Bays(Vec2 start, Vec2 along, Vec2 aisleSide, string kind, int count, int accessible, int firstNumber)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Broj parking mjesta mora biti najmanje 1.");
            }

            accessible = kind == Perpendicular ? Math.Max(0, Math.Min(count, accessible)) : 0;
            (Vec2 u, Vec2 a) = Frame(start, along, aisleSide);
            (double w, double depth) = Size(kind);

            // the bay's frame: +Y from the aisle to the kerb (−a), +X that turned a quarter clockwise
            Vec2 y = -a;
            Vec2 x = new Vec2(y.Y, -y.X);
            int dir = x.Dot(u) >= 0 ? 1 : -1;
            double rotation = Math.Atan2(x.Y, x.X);

            var list = new List<ParkingRowBay>();
            double s = 0;
            int number = firstNumber;
            for (int i = 0; i < count; i++)
            {
                bool acc = i < accessible;
                double width = acc ? AccessibleWidth + AccessibleHatch : w;
                double mid = s + (width / 2);
                list.Add(new ParkingRowBay
                {
                    Position = start + (u * mid) + (a * depth),
                    Rotation = rotation,
                    Width = width,
                    Depth = depth,
                    Accessible = acc,
                    Hatch = acc ? AccessibleHatch : 0,
                    Number = acc ? string.Empty : (number++).ToString(CultureInfo.InvariantCulture),
                    RowDir = dir,
                    First = i == 0,
                    Last = i == count - 1,
                });
                s += width;
            }

            // a 45° row: the first line starts at the aisle at the start and reaches the kerb one depth further on
            // (the triangle before it is hatched)
            return list;
        }

        /// <summary>Arrows in the middle of the aisle, one about every 12 m (parallel and 45° rows).</summary>
        public static List<ParkingArrow> Arrows(Vec2 start, Vec2 along, Vec2 aisleSide, string kind, int count)
        {
            var list = new List<ParkingArrow>();
            if (kind == Perpendicular)
            {
                return list;
            }

            (Vec2 u, Vec2 a) = Frame(start, along, aisleSide);
            (double _, double depth) = Size(kind);
            double from = 0, to = Length(kind, count, 0) - (kind == Angled ? depth : 0);
            double len = to - from;
            int n = Math.Max(1, (int)Math.Round(len / 12.0));
            double arrow = Math.Min(ArrowLength, len * 0.8);
            for (int i = 0; i < n; i++)
            {
                double mid = from + (len * (i + 0.5) / n);
                Vec2 tail = start + (u * (mid - (arrow / 2))) + (a * (depth + (Aisle / 2)));
                list.Add(new ParkingArrow(tail, Math.Atan2(u.Y, u.X)));
            }

            return list;
        }

        public static double ArrowSize(string kind, int count)
        {
            (double _, double depth) = Size(kind);
            double len = Length(kind, count, 0) - (kind == Angled ? depth : 0);
            return Math.Min(ArrowLength, len * 0.8);
        }

        /// <summary>The row direction and the unit vector from the kerb line to the aisle.</summary>
        private static (Vec2 U, Vec2 A) Frame(Vec2 start, Vec2 along, Vec2 aisleSide)
        {
            Vec2 u = (along - start).Normalized();
            if (u.Length < 0.5)
            {
                throw new ArgumentException("Smjer niza nije zadan (druga tačka je na prvoj).", nameof(along));
            }

            Vec2 a = new Vec2(-u.Y, u.X);
            if ((aisleSide - start).Dot(a) < 0)
            {
                a = -a;
            }

            return (u, a);
        }
    }
}
