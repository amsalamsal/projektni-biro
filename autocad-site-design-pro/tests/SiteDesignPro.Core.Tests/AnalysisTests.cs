using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Presentation;
using SiteDesignPro.Core.Urban;
using Xunit;
using static SiteDesignPro.Core.Tests.TestSupport;

namespace SiteDesignPro.Core.Tests
{
    public class UrbanIndicatorTests
    {
        private static List<SiteElement> Site() => new List<SiteElement>
        {
            Element("site.boundary", Rect(0, 0, 100, 50)),                                  // 5000 m²
            Element("building.footprint", Rect(10, 10, 40, 30), p => p["storeys"] = ParamValue.Of(4)),  // 600 m², BGP 2400
            Element("surface.lawn", Rect(50, 10, 90, 40)),                                  // 1200 m² green
            Element("surface.grass_grid", Rect(0, 40, 20, 50)),                             // 200 m² permeable
            Element("road.parking_bay", Pt(5, 5)),
            Element("road.parking_bay", Pt(8, 5)),
            Element("road.parking_bay_accessible", Pt(11, 5)),
            Element("road.parking_bay_ev", Pt(14, 5)),
        };

        [Fact]
        public void Measures_AreasCountsAndRatios()
        {
            UrbanMeasures m = UrbanIndicators.Measure(Site(), TestSupport.Catalog);
            Assert.Equal(5000, m.Parcel, 6);
            Assert.Equal(600, m.Footprint, 6);
            Assert.Equal(2400, m.FloorArea, 6);
            Assert.Equal(1200, m.Green, 6);
            Assert.Equal(1400, m.Permeable, 6);
            Assert.Equal(4, m.Parking);
            Assert.Equal(1, m.Accessible);
            Assert.Equal(1, m.Ev);
        }

        [Fact]
        public void Requirements_AreJudged()
        {
            UrbanMeasures m = UrbanIndicators.Measure(Site(), TestSupport.Catalog);
            var r = new UrbanRequirements { MaxCoverage = 0.10, MaxFloorAreaRatio = 0.6, MinGreenPercent = 20, MinPermeablePercent = 30, Dwellings = 4, ParkingPerDwelling = 1.1, MinAccessiblePercent = 5 };
            List<UrbanIndicator> rows = UrbanIndicators.Compute(m, r);
            Assert.False(rows.Single(x => x.Name.StartsWith("Koeficijent izgrađenosti")).Met);   // 0.12 > 0.10
            Assert.True(rows.Single(x => x.Name.StartsWith("Koeficijent iskorištenosti")).Met);   // 0.48 <= 0.6
            Assert.True(rows.Single(x => x.Name == "Zelene površine").Met);                       // 24 % >= 20
            Assert.False(rows.Single(x => x.Name == "Vodopropusne površine").Met);                // 28 % < 30
            Assert.False(rows.Single(x => x.Name == "Parking mjesta").Met);                       // 4 < 5
            Assert.True(rows.Single(x => x.Name.StartsWith("Parking mjesta za osobe")).Met);      // 1 >= 1
            Assert.Equal(5, UrbanIndicators.RequiredParking(r));
            Assert.Equal(4, UrbanIndicators.ToTable(rows).Columns.Count);
        }

        [Fact]
        public void NoRequirement_GivesNoVerdict()
        {
            List<UrbanIndicator> rows = UrbanIndicators.Compute(UrbanIndicators.Measure(Site(), TestSupport.Catalog), new UrbanRequirements());
            Assert.All(rows, r => Assert.Null(r.Met));
        }
    }

    public class PresentationTests
    {
        [Fact]
        public void Paint_ByMaterialAndKind()
        {
            Assert.Equal(PresentationKind.Canopy, PresentationStyle.For(TestSupport.Catalog.RequireType("landscape.tree_deciduous"), Element("landscape.tree_deciduous", Pt(0, 0)), TestSupport.Catalog).Kind);
            Assert.Equal(PresentationKind.Building, PresentationStyle.For(TestSupport.Catalog.RequireType("building.footprint"), Element("building.footprint", Rect(0, 0, 1, 1)), TestSupport.Catalog).Kind);
            PresentationPaint pool = PresentationStyle.For(TestSupport.Catalog.RequireType("water.swimming_pool"), Element("water.swimming_pool", Rect(0, 0, 1, 1)), TestSupport.Catalog);
            Assert.True(pool.B > pool.R);
            Assert.Equal(PresentationKind.Fill, PresentationStyle.For(TestSupport.Catalog.RequireType("surface.concrete_paving"), Element("surface.concrete_paving", Rect(0, 0, 1, 1)), TestSupport.Catalog).Kind);
            Assert.Equal(PresentationKind.Band, PresentationStyle.For(TestSupport.Catalog.RequireType("cycling.bike_path"), Element("cycling.bike_path", Line(0, 0, 10, 0)), TestSupport.Catalog).Kind);

            // roads of the road network are painted by the network itself
            Assert.Equal(PresentationKind.None, PresentationStyle.For(TestSupport.Catalog.RequireType("road.access_road"), Element("road.access_road", Line(0, 0, 10, 0)), TestSupport.Catalog).Kind);
            Assert.Equal(PresentationKind.None, PresentationStyle.For(TestSupport.Catalog.RequireType("site.boundary"), Element("site.boundary", Rect(0, 0, 1, 1)), TestSupport.Catalog).Kind);
        }
    }

    public class SweptPathTests
    {
        private static DesignVehicle Fire => DesignVehicle.Standard.First(v => v.Key == "fire");

        private static List<Vec2> Corner(double radius)
        {
            // straight 30 m east, quarter circle turning north, straight 30 m north
            var pts = new List<Vec2> { new Vec2(-30, 0), new Vec2(0, 0) };
            for (int i = 1; i <= 30; i++)
            {
                double a = (Math.PI / 2) * i / 30;
                pts.Add(new Vec2(Math.Sin(a) * radius, radius - (Math.Cos(a) * radius)));
            }

            pts.Add(new Vec2(radius, radius + 30));
            return pts;
        }

        [Fact]
        public void StraightRoad_HasNoConflicts()
        {
            SweptPathResult r = SweptPath.Run(new List<Vec2> { new Vec2(0, 0), new Vec2(50, 0) }, Fire, Array.Empty<SweptObstacle>());
            Assert.Empty(r.Conflicts);
            Assert.Equal(50, r.Length, 6);
            Assert.True(r.MaxSteerUsed < 0.01);
        }

        [Fact]
        public void WideBend_Passes_TightBend_IsFlagged()
        {
            Assert.Empty(SweptPath.Run(Corner(15), Fire, Array.Empty<SweptObstacle>()).Conflicts);
            SweptPathResult tight = SweptPath.Run(Corner(4), Fire, Array.Empty<SweptObstacle>());
            Assert.Contains(tight.Conflicts, c => c.ObstacleId == null);
        }

        [Fact]
        public void WheelsOverIsland_AreFlagged_OverhangAndHolesAreNot()
        {
            var path = new List<Vec2> { new Vec2(0, 0), new Vec2(40, 0) };
            List<Vec2> Box(double x0, double y0, double x1, double y1) => new List<Vec2> { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) };

            // island across the road: wheels run over it
            var island = SweptObstacle.Ground("i", "ostrvo", Box(18, -3, 22, 3));
            Assert.Contains(SweptPath.Run(path, Fire, new[] { island }).Conflicts, c => c.ObstacleId == "i");

            // verge starting 1.2 m beside the axis: only the body (half width 1.25) overhangs, the wheels stay off
            var verge = SweptObstacle.Ground("v", "travnjak", Box(0, 1.2, 40, 5));
            Assert.Empty(SweptPath.Run(path, Fire, new[] { verge }).Conflicts);

            // lawn with the road cut out as a hole: no conflict
            var lawn = SweptObstacle.Ground("l", "travnjak", Box(-10, -10, 50, 10), new[] { (IReadOnlyList<Vec2>)Box(-10, -2, 50, 2) });
            Assert.Empty(SweptPath.Run(path, Fire, new[] { lawn }).Conflicts);
        }

        [Fact]
        public void TreeBesideTheRoad_IsHitOnlyWhenTooClose()
        {
            var path = new List<Vec2> { new Vec2(0, 0), new Vec2(40, 0) };
            var far = SweptObstacle.Circle("t1", "drvo", new Vec2(20, 3.0), 0.4);
            var near = SweptObstacle.Circle("t2", "drvo", new Vec2(20, 1.4), 0.4);
            Assert.Empty(SweptPath.Run(path, Fire, new[] { far }).Conflicts);
            Assert.Single(SweptPath.Run(path, Fire, new[] { near }).Conflicts);
        }

        [Fact]
        public void InsideOfBend_CutsCorner_OffTracking()
        {
            SweptPathResult r = SweptPath.Run(Corner(12), Fire, Array.Empty<SweptObstacle>());
            // the rear axle runs inside the front axle path in the bend (off-tracking)
            VehiclePose mid = r.Poses[r.Poses.Count / 2];
            double frontRadius = (mid.Front - new Vec2(0, 12)).Length;
            double rearRadius = (mid.Rear - new Vec2(0, 12)).Length;
            Assert.True(rearRadius < frontRadius - 0.3);
        }
    }
}
