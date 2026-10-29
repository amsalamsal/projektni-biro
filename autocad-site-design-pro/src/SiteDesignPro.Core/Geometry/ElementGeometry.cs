using System;
using System.Collections.Generic;
using System.Linq;

namespace SiteDesignPro.Core.Geometry
{
    /// <summary>Geometry kinds understood by the exchange format.</summary>
    public enum GeometryKind
    {
        Point,
        Path,
        Polygon,
    }

    /// <summary>Base class of element geometry. Coordinates are metres in the package/local frame.</summary>
    public abstract class ElementGeometry
    {
        public abstract GeometryKind Kind { get; }

        /// <summary>Returns a copy with every vertex/position mapped by <paramref name="map"/> (rotation is preserved).</summary>
        public abstract ElementGeometry Transform(Func<Vec3, Vec3> map);

        public abstract Box2 Bounds();

        public static string KindCode(GeometryKind kind) => kind switch
        {
            GeometryKind.Point => "point",
            GeometryKind.Path => "path",
            GeometryKind.Polygon => "polygon",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static GeometryKind ParseKind(string code) => code switch
        {
            "point" => GeometryKind.Point,
            "path" => GeometryKind.Path,
            "polygon" => GeometryKind.Polygon,
            _ => throw new FormatException("Unknown geometry kind '" + code + "'."),
        };
    }

    /// <summary>Insertion point plus rotation (radians, counter-clockwise about +Z from +X).</summary>
    public sealed class PointGeometry : ElementGeometry
    {
        public PointGeometry(Vec3 position, double rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public override GeometryKind Kind => GeometryKind.Point;

        public Vec3 Position { get; }

        public double Rotation { get; }

        public override ElementGeometry Transform(Func<Vec3, Vec3> map) => new PointGeometry(map(Position), Rotation);

        public override Box2 Bounds() => Box2.Empty.Include(Position.XY);
    }

    /// <summary>
    /// Sequence of vertices with optional bulges (AutoCAD convention): Bulges[i] applies to the segment from
    /// vertex i to vertex i+1 (and from the last vertex to the first for closed rings).
    /// </summary>
    public sealed class Ring
    {
        public Ring(IReadOnlyList<Vec3> vertices, IReadOnlyList<double>? bulges = null)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            Bulges = bulges != null && bulges.Any(b => b != 0) ? bulges : null;
        }

        public IReadOnlyList<Vec3> Vertices { get; }

        /// <summary>Null when every segment is straight.</summary>
        public IReadOnlyList<double>? Bulges { get; }

        public int Count => Vertices.Count;

        public double BulgeAt(int segment) => Bulges != null && segment < Bulges.Count ? Bulges[segment] : 0.0;

        public bool HasArcs => Bulges != null;

        public Ring Map(Func<Vec3, Vec3> map) => new Ring(Vertices.Select(map).ToList(), Bulges);

        public Box2 Bounds()
        {
            Box2 b = Box2.Empty;
            foreach (Vec3 v in Vertices)
            {
                b = b.Include(v.XY);
            }

            // Arcs may extend beyond their chord; include tessellated arc points.
            if (HasArcs)
            {
                foreach (Vec3 v in GeometryMath.Tessellate(this, true, 0.01))
                {
                    b = b.Include(v.XY);
                }
            }

            return b;
        }
    }

    /// <summary>Open or closed polyline path (centrelines, alignments, routes).</summary>
    public sealed class PathGeometry : ElementGeometry
    {
        public PathGeometry(Ring ring, bool closed)
        {
            Ring = ring;
            Closed = closed;
        }

        public override GeometryKind Kind => GeometryKind.Path;

        public Ring Ring { get; }

        public bool Closed { get; }

        public override ElementGeometry Transform(Func<Vec3, Vec3> map) => new PathGeometry(Ring.Map(map), Closed);

        public override Box2 Bounds() => Ring.Bounds();
    }

    /// <summary>Closed area with an outer ring and optional holes.</summary>
    public sealed class PolygonGeometry : ElementGeometry
    {
        public PolygonGeometry(Ring outer, IReadOnlyList<Ring>? holes = null)
        {
            Outer = outer;
            Holes = holes ?? Array.Empty<Ring>();
        }

        public override GeometryKind Kind => GeometryKind.Polygon;

        public Ring Outer { get; }

        public IReadOnlyList<Ring> Holes { get; }

        public override ElementGeometry Transform(Func<Vec3, Vec3> map) => new PolygonGeometry(Outer.Map(map), Holes.Select(h => h.Map(map)).ToList());

        public override Box2 Bounds() => Outer.Bounds();
    }
}
