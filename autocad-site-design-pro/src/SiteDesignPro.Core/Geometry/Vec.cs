using System;
using System.Globalization;

namespace SiteDesignPro.Core.Geometry
{
    /// <summary>Immutable 2D vector (metres unless stated otherwise).</summary>
    public readonly record struct Vec2(double X, double Y)
    {
        public static readonly Vec2 Zero = new Vec2(0, 0);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);

        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public double Dot(Vec2 o) => (X * o.X) + (Y * o.Y);

        public double Cross(Vec2 o) => (X * o.Y) - (Y * o.X);

        public Vec2 Normalized()
        {
            double l = Length;
            return l < 1e-15 ? Zero : new Vec2(X / l, Y / l);
        }

        /// <summary>Left-hand perpendicular (rotated +90 degrees).</summary>
        public Vec2 Perp() => new Vec2(-Y, X);

        public Vec2 Rotate(double angle)
        {
            double c = Math.Cos(angle), s = Math.Sin(angle);
            return new Vec2((X * c) - (Y * s), (X * s) + (Y * c));
        }

        public double Angle => Math.Atan2(Y, X);

        public double DistanceTo(Vec2 o) => (this - o).Length;

        public Vec3 WithZ(double z) => new Vec3(X, Y, z);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.######}, {1:0.######})", X, Y);
    }

    /// <summary>Immutable 3D vector (metres unless stated otherwise).</summary>
    public readonly record struct Vec3(double X, double Y, double Z)
    {
        public static readonly Vec3 Zero = new Vec3(0, 0, 0);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public Vec2 XY => new Vec2(X, Y);

        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

        public static Vec3 Lerp(Vec3 a, Vec3 b, double t) => new Vec3(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), a.Z + ((b.Z - a.Z) * t));

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.######}, {1:0.######}, {2:0.######})", X, Y, Z);
    }

    /// <summary>Axis-aligned 2D bounding box.</summary>
    public readonly record struct Box2(double MinX, double MinY, double MaxX, double MaxY)
    {
        public static readonly Box2 Empty = new Box2(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

        public bool IsEmpty => MinX > MaxX || MinY > MaxY;

        public double Width => MaxX - MinX;

        public double Height => MaxY - MinY;

        public Box2 Include(Vec2 p) => new Box2(Math.Min(MinX, p.X), Math.Min(MinY, p.Y), Math.Max(MaxX, p.X), Math.Max(MaxY, p.Y));

        public Box2 Include(Box2 b) => b.IsEmpty ? this : new Box2(Math.Min(MinX, b.MinX), Math.Min(MinY, b.MinY), Math.Max(MaxX, b.MaxX), Math.Max(MaxY, b.MaxY));

        public Box2 Inflate(double d) => new Box2(MinX - d, MinY - d, MaxX + d, MaxY + d);

        public bool Intersects(Box2 o) => !(o.MinX > MaxX || o.MaxX < MinX || o.MinY > MaxY || o.MaxY < MinY);
    }
}
