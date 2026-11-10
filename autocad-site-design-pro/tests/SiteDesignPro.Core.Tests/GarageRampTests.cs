using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class GarageRampTests
    {
        private static SiteElement Ramp(Ring axis, string kind, string direction, double start, double end)
        {
            var e = new SiteElement(Guid.NewGuid().ToString(), GarageRamp.Type, new PathGeometry(axis, false))
            {
                Parameters = TestSupport.Catalog.RequireType(GarageRamp.Type).CreateDefaultParameters(),
                Label = "GR-001",
            };
            e.Parameters["kind"] = ParamValue.Of(kind);
            e.Parameters["direction"] = ParamValue.Of(direction);
            e.Parameters["laneWidth"] = ParamValue.Of(kind == "straight" ? 3.0 : 4.0);
            e.Parameters["margin"] = ParamValue.Of(0.25);
            e.Parameters["startLevel"] = ParamValue.Of(start);
            e.Parameters["endLevel"] = ParamValue.Of(end);
            return e;
        }

        [Fact]
        public void StraightRampHasLengthRiseAndSlope()
        {
            var axis = new Ring(new List<Vec3> { new Vec3(0, 0, 0), new Vec3(20, 0, 0) });
            GarageRamp r = GarageRamp.From(Ramp(axis, "straight", "two_way", 0, -3), 1.0)!;
            Assert.InRange(r.Length, 19.999, 20.001);
            Assert.InRange(r.Slope, 0.1499, 0.1501);
            Assert.False(r.TooSteep);
            Assert.InRange(r.Width, 6.499, 6.501);
            Assert.Equal(0, r.InnerSide);

            List<Vec3[]> tris = r.Triangles();
            Assert.NotEmpty(tris);
            Assert.InRange(tris.SelectMany(t => t).Min(v => v.Z), -3.001, -2.999);
            Assert.InRange(tris.SelectMany(t => t).Max(v => v.Z), -0.001, 0.001);
        }

        [Fact]
        public void SteepRampIsReported()
        {
            var axis = new Ring(new List<Vec3> { new Vec3(0, 0, 0), new Vec3(10, 0, 0) });
            GarageRamp r = GarageRamp.From(Ramp(axis, "straight", "one_way", 0, -2), 1.0)!;
            Assert.True(r.TooSteep);
        }

        [Fact]
        public void SemicircularRampFallsAcrossTowardsTheInside()
        {
            // semicircle of radius 8 around (0, 0), counter-clockwise: the inside is on the left
            var axis = new Ring(new List<Vec3> { new Vec3(8, 0, 0), new Vec3(-8, 0, 0) }, new List<double> { 1.0, 0.0 });
            GarageRamp r = GarageRamp.From(Ramp(axis, "semicircular", "one_way", 0, -3), 1.0)!;
            Assert.InRange(r.Length, (Math.PI * 8) - 0.01, (Math.PI * 8) + 0.01);
            Assert.Equal(1, r.InnerSide);

            // at the start the inner edge (left, radius 5,75) is lower than the outer edge (radius 10,25)
            Vec3[] second = r.Triangles()[1];
            Vec3 outer = second[0], inner = second[2];
            Assert.True(inner.XY.DistanceTo(new Vec2(0, 0)) < outer.XY.DistanceTo(new Vec2(0, 0)));
            Assert.True(inner.Z < outer.Z);
            Assert.InRange(outer.Z - inner.Z, (GarageRamp.CrossSlope * r.Width) - 0.01, (GarageRamp.CrossSlope * r.Width) + 0.01);
        }
    }
}
