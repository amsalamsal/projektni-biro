using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Symbols;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>PARKING MJESTA U NIZU: perpendicular, parallel and 45° rows as in the project standard.</summary>
    public class ParkingRowTests
    {
        private static SymbolDrawing Symbol(ParkingRowBay b, string layout)
        {
            var p = new ParameterSet();
            p["layout"] = ParamValue.Of(layout);
            p["width"] = ParamValue.Of(b.Width);
            p["length"] = ParamValue.Of(b.Depth);
            p["rowDir"] = ParamValue.Of((double)b.RowDir);
            p["rowStart"] = ParamValue.Of(b.First);
            p["rowEnd"] = ParamValue.Of(b.Last);
            p["accessible"] = ParamValue.Of(b.Accessible);
            p["hatchWidth"] = ParamValue.Of(b.Hatch);
            p["bayNumber"] = ParamValue.Of(b.Number);
            return SymbolLibrary.Build("parking_bay", p);
        }

        [Fact]
        public void Perpendicular_AccessibleBaysComeBeforeNumberOne()
        {
            // kerb along +X from the origin, the aisle on the +Y side
            List<ParkingRowBay> bays = ParkingRows.Bays(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Perpendicular, 5, 1, 1);
            Assert.Equal(5, bays.Count);
            Assert.True(bays[0].Accessible);
            Assert.Equal(string.Empty, bays[0].Number);
            Assert.Equal(new[] { "1", "2", "3", "4" }, bays.Skip(1).Select(b => b.Number));
            Assert.Equal(3.9, bays[0].Width, 9);
            Assert.Equal(1.4, bays[0].Hatch, 9);
            Assert.Equal(13.9, ParkingRows.Length(ParkingRows.Perpendicular, 5, 1), 9);

            // the middle of the aisle edge of each bay, 5,00 m from the kerb
            Assert.Equal(1.95, bays[0].Position.X, 9);
            Assert.Equal(5.0, bays[0].Position.Y, 9);
            Assert.Equal(3.9 + 1.25, bays[1].Position.X, 9);
            Assert.True(bays[0].First && !bays[0].Last && bays[4].Last);

            // the bay's +Y points from the aisle to the kerb (−Y here)
            Vec2 y = new Vec2(-Math.Sin(bays[0].Rotation), Math.Cos(bays[0].Rotation));
            Assert.Equal(-1, y.Y, 9);

            // numbering goes on
            Assert.Equal("7", ParkingRows.Bays(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Perpendicular, 3, 0, 7)[0].Number);
            Assert.Empty(ParkingRows.Arrows(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Perpendicular, 5));
        }

        [Fact]
        public void Symbols_AsInTheStandard()
        {
            List<ParkingRowBay> bays = ParkingRows.Bays(Vec2.Zero, new Vec2(10, 0), new Vec2(0, -5), ParkingRows.Perpendicular, 3, 1, 1);
            SymbolDrawing acc = Symbol(bays[0], ParkingRows.Perpendicular);
            Assert.Contains(acc.Items, i => i.Color == ParkingSymbols.Yellow);
            Assert.DoesNotContain(acc.Items, i => i is SymText);
            Bounds(acc, out double minY, out double maxY);
            Assert.True(maxY > 5.0 + ParkingSymbols.KerbRampDepth - 0.01, "the kerb ramp beyond the kerb");

            SymbolDrawing one = Symbol(bays[1], ParkingRows.Perpendicular);
            Assert.Contains(one.Items, i => i is SymText t && t.Text == "1");
            Assert.All(one.Items.OfType<SymPolyline>(), l => Assert.Equal(ParkingSymbols.LineWidth, l.Width, 9));
            Assert.DoesNotContain(one.Items, i => i.Color != 0);

            // 45°: the first bay shows the angle and the hatched triangle
            List<ParkingRowBay> angled = ParkingRows.Bays(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Angled, 4, 0, 1);
            Assert.Equal(4 * 3.4 + 4.75, ParkingRows.Length(ParkingRows.Angled, 4, 0), 9);
            SymbolDrawing first = Symbol(angled[0], ParkingRows.Angled);
            Assert.Contains(first.Items, i => i is SymText t && t.Text == "45°");
            Assert.Contains(first.Items, i => i is SymArc);
            Assert.True(first.Items.OfType<SymLine>().Count() > 3, "stripes of the triangle");
            Assert.DoesNotContain(Symbol(angled[1], ParkingRows.Angled).Items, i => i is SymArc);
            Assert.NotEmpty(ParkingRows.Arrows(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Angled, 4));

            // parallel: 5,50 × 2,50 m, one arrow per about 12 m in the middle of the 3,00 m aisle
            List<ParkingArrow> arrows = ParkingRows.Arrows(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Parallel, 4);
            Assert.Equal(2, arrows.Count);
            Assert.All(arrows, a => Assert.Equal(2.5 + 1.5, a.Position.Y, 9));
            Assert.Equal(0, arrows[0].Rotation, 9);
        }

        [Fact]
        public void BothSidesOfTheKerb_TheRowRunsTheSameWay()
        {
            List<ParkingRowBay> left = ParkingRows.Bays(Vec2.Zero, new Vec2(10, 0), new Vec2(0, 5), ParkingRows.Parallel, 2, 0, 1);
            List<ParkingRowBay> right = ParkingRows.Bays(Vec2.Zero, new Vec2(10, 0), new Vec2(0, -5), ParkingRows.Parallel, 2, 0, 1);
            Assert.Equal(-left[0].RowDir, right[0].RowDir);
            foreach (ParkingRowBay b in left.Concat(right))
            {
                Vec2 forward = new Vec2(Math.Cos(b.Rotation), Math.Sin(b.Rotation)) * b.RowDir;
                Assert.Equal(1, forward.X, 9);
            }
        }

        [Fact]
        public void ToolExamples_EveryMainToolOnce_RampWithinTheMinimums_NetworkBuilds()
        {
            List<SiteElement> all = Samples.ToolExamples.Create(TestSupport.Catalog);
            Assert.Equal(2, all.Count(e => e.TypeId == Roads.RoadElements.StreetType));
            Assert.Contains(all, e => e.TypeId == Roads.GarageRamp.Type);
            Assert.Contains(all, e => e.TypeId == Roads.RoadElements.CrossingType);
            Assert.True(all.Count(e => e.TypeId.StartsWith("road.parking_bay", StringComparison.Ordinal)) == 15);
            Assert.Contains(all, e => e.TypeId == "road.arrow");
            Assert.Contains(all, e => e.TypeId == "landscape.tree_deciduous");
            Assert.All(all, e => Assert.Empty(Validation.RampMinimums.Check(e, TestSupport.Catalog)));
            Assert.NotEmpty(Roads.RoadElements.Derived(all, TestSupport.Catalog));
        }

        private static void Bounds(SymbolDrawing s, out double minY, out double maxY)
        {
            Box2 b = s.Bounds();
            minY = b.MinY;
            maxY = b.MaxY;
        }
    }
}
