using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Units;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class UnitsTests
    {
        [Theory]
        [InlineData(LengthUnit.Millimeter, 0.001)]
        [InlineData(LengthUnit.Centimeter, 0.01)]
        [InlineData(LengthUnit.Meter, 1.0)]
        [InlineData(LengthUnit.Inch, 0.0254)]
        [InlineData(LengthUnit.Foot, 0.3048)]
        public void MetersPerUnit_IsExact(LengthUnit unit, double expected) => Assert.Equal(expected, UnitConversion.MetersPerUnit(unit));

        [Fact]
        public void MillimetreMetreRoundTrip()
        {
            Assert.Equal(1.234, UnitConversion.Convert(1234, LengthUnit.Millimeter, LengthUnit.Meter), 12);
            Assert.Equal(1234, UnitConversion.Convert(1.234, LengthUnit.Meter, LengthUnit.Millimeter), 9);
            var s = new UnitScale(LengthUnit.Millimeter);
            Assert.Equal(6550000.0, s.ToMeters(6550000000.0), 6);
            Assert.Equal(2500.0, s.ToDrawing(2.5), 9);
        }

        [Fact]
        public void InsUnitsMapping()
        {
            Assert.Equal(LengthUnit.Millimeter, UnitConversion.FromInsUnits(4));
            Assert.Equal(LengthUnit.Meter, UnitConversion.FromInsUnits(6));
            Assert.Null(UnitConversion.FromInsUnits(0));
            Assert.Equal(6, UnitConversion.ToInsUnits(LengthUnit.Meter));
            Assert.Equal(LengthUnit.Millimeter, UnitConversion.Parse("millimetres"));
            Assert.Throws<FormatException>(() => UnitConversion.Parse("furlong"));
        }

        [Fact]
        public void ProjectSettings_JsonRoundTrip()
        {
            var s = new ProjectSettings { Name = "Test", DrawingUnit = "m", Origin = new[] { 1.0, 2.0, 3.0 }, TrueNorthDegrees = 12.5 };
            s.Labels.Zone = "A";
            ProjectSettings r = ProjectSettings.FromJson(s.ToJson());
            Assert.Equal("Test", r.Name);
            Assert.Equal(new[] { 1.0, 2.0, 3.0 }, r.Origin);
            Assert.Equal(12.5, r.TrueNorthDegrees);
            Assert.Equal("A", r.Labels.Zone);
            Assert.Equal(new Vec3(1, 2, 3), r.OriginMeters);
        }
    }

    public class GeometryTests
    {
        [Fact]
        public void SemicircleBulge_GivesCentreAndRadius()
        {
            ArcSegment arc = GeometryMath.ArcFromBulge(new Vec2(0, 0), new Vec2(2, 0), 1.0);
            Assert.Equal(1.0, arc.Radius, 12);
            Assert.Equal(0.0, arc.Center.DistanceTo(new Vec2(1, 0)), 12);
            Assert.Equal(Math.PI, arc.Sweep, 12);
            // positive bulge: counter-clockwise from (0,0) passes through (1,-1)
            Assert.Equal(0.0, arc.PointAt(0.5).DistanceTo(new Vec2(1, -1)), 9);
        }

        [Fact]
        public void CircleFromTwoBulges_HasExactAreaAndLength()
        {
            var ring = new Ring(new[] { new Vec3(-5, 0, 0), new Vec3(5, 0, 0) }, new[] { 1.0, 1.0 });
            Assert.Equal(Math.PI * 25, Math.Abs(GeometryMath.SignedArea(ring)), 9);
            Assert.Equal(Math.PI * 10, GeometryMath.Length(ring, true), 9);
        }

        [Fact]
        public void PolygonArea_WithHoleAndArc()
        {
            var outer = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(10, 10, 0), new Vec3(0, 10, 0) });
            var hole = new Ring(new[] { new Vec3(2, 2, 0), new Vec3(4, 2, 0), new Vec3(4, 4, 0), new Vec3(2, 4, 0) });
            Assert.Equal(96, GeometryMath.Area(new PolygonGeometry(outer, new[] { hole })), 9);

            // A bulged edge on a counter-clockwise square adds a circular segment.
            var arcRing = new Ring(outer.Vertices, new[] { 0.0, 0.0, 1.0, 0.0 });
            Assert.Equal(100 + (Math.PI * 25 / 2), Math.Abs(GeometryMath.SignedArea(arcRing)), 9);
        }

        [Fact]
        public void Tessellation_KeepsEndpointsAndInterpolatesZ()
        {
            var ring = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 1) }, new[] { 0.5 });
            List<Vec3> pts = GeometryMath.Tessellate(ring, false, 0.01);
            Assert.True(pts.Count > 10);
            Assert.Equal(new Vec3(0, 0, 0), pts[0]);
            Assert.Equal(new Vec3(10, 0, 1), pts[pts.Count - 1]);
            Assert.All(pts, p => Assert.InRange(p.Z, 0, 1));
        }

        [Fact]
        public void PointAtDistance_OnLineAndArc()
        {
            (Vec3 p, double a) = GeometryMath.PointAtDistance(new Ring(new[] { new Vec3(0, 0, 0), new Vec3(0, 10, 2) }), false, 5);
            Assert.Equal(0, p.DistanceToXY(new Vec2(0, 5)), 9);
            Assert.Equal(1.0, p.Z, 9);
            Assert.Equal(Math.PI / 2, a, 9);

            var semi = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(2, 0, 0) }, new[] { 1.0 });
            (Vec3 q, double tangent) = GeometryMath.PointAtDistance(semi, false, Math.PI / 2);
            Assert.Equal(0, q.DistanceToXY(new Vec2(1, -1)), 9);
            Assert.Equal(0, Math.Abs(Math.Sin(tangent)), 9); // horizontal tangent at the bottom of the arc
        }

        [Fact]
        public void SelfIntersection_DetectsBowtie()
        {
            var bowtie = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(10, 10, 0), new Vec3(10, 0, 0), new Vec3(0, 10, 0) });
            Assert.True(GeometryMath.IsSelfIntersecting(bowtie));
            var square = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(10, 10, 0), new Vec3(0, 10, 0) });
            Assert.False(GeometryMath.IsSelfIntersecting(square));
        }

        [Fact]
        public void OrientedBoundingRect_FollowsFirstEdgeAndPointsInward()
        {
            double a = 30 * Math.PI / 180;
            Vec2 u = new Vec2(Math.Cos(a), Math.Sin(a)), v = u.Perp();
            Vec2 o = new Vec2(100, 50);
            // clockwise rectangle: area lies to the right of the first edge
            var pts = new[] { o, o + (u * 8), o + (u * 8) - (v * 3), o - (v * 3) };
            var ring = new Ring(pts.Select(p => p.WithZ(2)).ToList());
            OrientedRect r = GeometryMath.OrientedBoundingRect(ring);
            Assert.Equal(8, r.LengthU, 9);
            Assert.Equal(3, r.LengthV, 9);
            Assert.Equal(24, r.Area, 9);
            Assert.True(r.V.Dot(-v) > 0.999);
            Assert.Equal(2, r.Z);
            Assert.True(GeometryMath.IsRectangle(ring));
        }

        [Fact]
        public void AreasOverlap_SharedEdgeIsNotOverlap_HoleExcludes()
        {
            IReadOnlyList<Vec2> A(double x0, double y0, double x1, double y1) => new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) };
            Assert.False(GeometryMath.PolygonsOverlap(A(0, 0, 10, 10), A(10, 0, 20, 10)));
            Assert.True(GeometryMath.PolygonsOverlap(A(0, 0, 10, 10), A(5, 5, 15, 15)));
            Assert.True(GeometryMath.PolygonsOverlap(A(0, 0, 10, 10), A(2, 2, 4, 4)));
            Assert.True(GeometryMath.PolygonsOverlap(A(0, 0, 10, 10), A(0, 0, 10, 10)));
            var withHole = new[] { A(0, 0, 10, 10), A(2, 2, 4, 4) };
            Assert.False(GeometryMath.AreasOverlap(withHole, new[] { A(2, 2, 4, 4) }));
            Assert.True(GeometryMath.AreasOverlap(withHole, new[] { A(1, 1, 5, 5) }));
        }

        [Fact]
        public void RemoveDuplicateVertices_DropsClosingDuplicate()
        {
            var ring = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(0, 0, 0), new Vec3(5, 0, 0), new Vec3(5, 5, 0), new Vec3(0, 0, 0) });
            Ring clean = GeometryMath.RemoveDuplicateVertices(ring, true);
            Assert.Equal(3, clean.Count);
        }
    }

    internal static class VecTestExtensions
    {
        public static double DistanceToXY(this Vec3 p, Vec2 q) => p.XY.DistanceTo(q);
    }
}
