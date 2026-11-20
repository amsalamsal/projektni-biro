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

            // in the middle the inner edge (left) is lower than the outer edge by the full cross slope
            double half = r.Width / 2, mid = r.Length / 2;
            Assert.InRange(r.HeightAt(mid, -half) - r.HeightAt(mid, half), (GarageRamp.CrossSlope * r.Width) - 0.001, (GarageRamp.CrossSlope * r.Width) + 0.001);

            // both ends lie level across for the first and the last metre (flush with the road or the slab)
            foreach (double s in new[] { 0.0, 0.5, GarageRamp.LevelEnd, r.Length - GarageRamp.LevelEnd, r.Length - 0.3, r.Length })
            {
                Assert.Equal(r.HeightAt(s, half), r.HeightAt(s, -half), 9);
            }

            // the cross slope grows after the level metre, and the 3D has an edge where it starts
            Assert.True(r.HeightAt(GarageRamp.LevelEnd + 1, -half) > r.HeightAt(GarageRamp.LevelEnd + 1, half));
            Assert.Contains(r.Stations(0, r.Length), x => Math.Abs(x - GarageRamp.LevelEnd) < 1e-6);
            Assert.Contains(r.Stations(0, r.Length), x => Math.Abs(x - (r.Length - GarageRamp.LevelEnd)) < 1e-6);
        }

        [Fact]
        public void RampsBelowTheTechnicalMinimum_AreRefused()
        {
            var axis = new Ring(new List<Vec3> { new Vec3(0, 0, 0), new Vec3(20, 0, 0) });
            SiteElement ok = Ramp(axis, "straight", "one_way", 0, -3);
            Assert.Empty(GarageRamp.From(ok, 1.0)!.Violations());
            Validation.RampMinimums.Require(ok, TestSupport.Catalog);

            // 4 m over 20 m: 20 %
            SiteElement steep = Ramp(axis, "straight", "one_way", 0, -4);
            Assert.Contains(GarageRamp.From(steep, 1.0)!.Violations(), v => v.StartsWith("uzdužni nagib", StringComparison.Ordinal));
            var ex = Assert.Throws<Validation.DesignRuleException>(() => Validation.RampMinimums.Require(steep, TestSupport.Catalog));
            Assert.Contains("tehničkog minimuma", ex.Message, StringComparison.Ordinal);

            // a semicircle of radius 6: inner edge 6 - 2,25 = 3,75 m < 5 m
            var small = new Ring(new List<Vec3> { new Vec3(6, 0, 0), new Vec3(-6, 0, 0) }, new List<double> { 1.0, 0.0 });
            Assert.Contains(GarageRamp.From(Ramp(small, "semicircular", "one_way", 0, -1), 1.0)!.Violations(), v => v.StartsWith("unutrašnji poluprečnik", StringComparison.Ordinal));

            // too narrow a lane on a curved ramp
            var big = new Ring(new List<Vec3> { new Vec3(10, 0, 0), new Vec3(-10, 0, 0) }, new List<double> { 1.0, 0.0 });
            SiteElement narrow = Ramp(big, "semicircular", "one_way", 0, -1);
            narrow.Parameters["laneWidth"] = ParamValue.Of(3.0);
            Assert.Contains(GarageRamp.From(narrow, 1.0)!.Violations(), v => v.StartsWith("širina trake", StringComparison.Ordinal));

            // a slab too low above the ramp
            SiteElement low = Ramp(axis, "straight", "one_way", 0, -3);
            low.Parameters["ceilingFrom"] = ParamValue.Of(10.0);
            low.Parameters["ceilingLevel"] = ParamValue.Of(0.3);
            Assert.Contains(GarageRamp.From(low, 1.0)!.Violations(), v => v.StartsWith("svijetla visina", StringComparison.Ordinal));
        }

        [Fact]
        public void PedestrianRamp_AtMost833Percent_AtLeast120cmWide()
        {
            SiteElement R(double w, double l, double rise)
            {
                var g = new PolygonGeometry(new Ring(new[] { new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, l, 0), new Vec3(0, l, 0) }));
                var e = new SiteElement(Guid.NewGuid().ToString(), "level.ramp", g) { Parameters = TestSupport.Catalog.RequireType("level.ramp").CreateDefaultParameters() };
                e.Parameters["rise"] = ParamValue.Of(rise);
                return e;
            }

            Assert.Empty(Validation.RampMinimums.Check(R(1.5, 6, 0.3), TestSupport.Catalog));
            Assert.Contains(Validation.RampMinimums.Check(R(1.5, 3, 0.3), TestSupport.Catalog), v => v.StartsWith("nagib", StringComparison.Ordinal));
            Assert.Contains(Validation.RampMinimums.Check(R(1.0, 6, 0.3), TestSupport.Catalog), v => v.StartsWith("svijetla širina", StringComparison.Ordinal));
        }

        [Fact]
        public void WallLeds_Every250cm_40cmAboveTheFloor()
        {
            var axis = new Ring(new List<Vec3> { new Vec3(0, 0, 0), new Vec3(30, 0, 0) });
            GarageRamp r = GarageRamp.From(Ramp(axis, "straight", "one_way", 0, -3), 1.0)!;
            List<Vec3[]> leds = r.Leds3();
            Assert.NotEmpty(leds);
            Assert.Equal(0, leds.Count % 10); // a box of five quads (two triangles each) per light

            // the lights stand on both sides, 2,50 m apart, 8 × 3 cm, from 40 cm above the floor
            var boxes = Enumerable.Range(0, leds.Count / 10).Select(i => leds.Skip(i * 10).Take(10).SelectMany(t => t).ToList()).ToList();
            foreach (List<Vec3> b in boxes)
            {
                double x = (b.Min(p => p.X) + b.Max(p => p.X)) / 2;
                Assert.Equal(GarageRamp.LedLength, b.Max(p => p.X) - b.Min(p => p.X), 6);
                Assert.Equal(GarageRamp.LedHeight, b.Max(p => p.Z) - b.Min(p => p.Z), 6);
                Assert.Equal(r.HeightAt(x, 0) + GarageRamp.LedAbove, b.Min(p => p.Z), 2);
            }

            List<double> left = boxes.Where(b => b.Average(p => p.Y) > 0).Select(b => b.Average(p => p.X)).OrderBy(x => x).ToList();
            Assert.True(left.Count >= 5);
            for (int i = 1; i < left.Count; i++)
            {
                Assert.Equal(GarageRamp.LedSpacing, left[i] - left[i - 1], 6);
            }
        }
    }
}
