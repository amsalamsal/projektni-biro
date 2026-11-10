using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Roads;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class RoadElementsTests
    {
        private static ElementCatalog Cat => TestSupport.Catalog;

        private static SiteElement Path(string type, double x0, double y0, double x1, double y1, params (string Key, ParamValue Value)[] p)
        {
            ElementType t = Cat.RequireType(type);
            var e = new SiteElement(Guid.NewGuid().ToString(), type, new PathGeometry(new Ring(new List<Vec3> { new Vec3(x0, y0, 0), new Vec3(x1, y1, 0) }), false))
            {
                Parameters = t.CreateDefaultParameters(),
                Label = type == RoadElements.StreetType ? "U-001" : "R-001",
            };
            foreach ((string key, ParamValue value) in p)
            {
                e.Parameters[key] = value;
            }

            return e;
        }

        [Fact]
        public void Catalog_HasTheStreetAndHidesTheGeneratedTypes()
        {
            ElementType street = Cat.RequireType(RoadElements.StreetType);
            Assert.Equal("street", street.Builder.Id);
            Assert.True(RoadElements.IsNetworkRoad(street));
            Assert.True(RoadElements.IsNetworkRoad(Cat.RequireType("road.access_road")));
            Assert.False(RoadElements.IsNetworkRoad(Cat.RequireType("road.fire_access")));
            Assert.False(RoadElements.IsNetworkRoad(Cat.RequireType("cycling.bike_path")));
            foreach ((string type, string _) in RoadElements.LevelTypes.Values)
            {
                Assert.True(Cat.RequireType(type).Generated, type);
                Assert.DoesNotContain(Cat.PlaceableTypes, t => t.Id == type);
            }

            Assert.Contains(Cat.PlaceableTypes, t => t.Id == RoadElements.StreetType);
        }

        [Fact]
        public void Spec_ReadsTheCrossSectionOfAStreet_AndTheWidthOfASimpleRoad()
        {
            SiteElement street = Path(RoadElements.StreetType, 0, 0, 100, 0,
                ("direction", ParamValue.Of("one_way")), ("lanesRight", ParamValue.Of(2.0)), ("laneWidth", ParamValue.Of(3.25)),
                ("bikeLeft", ParamValue.Of(1.5)), ("sidewalkRight", ParamValue.Of(0.0)), ("arrows", ParamValue.Of(false)));
            RoadSpec s = RoadElements.Spec(street, Cat.RequireType(RoadElements.StreetType))!;
            Assert.True(s.OneWay);
            Assert.Equal(2, s.TotalLanes);
            Assert.Equal(3.25, s.CarriagewayLeft, 6);
            Assert.Equal(1.5, s.BikeLeft, 6);
            Assert.Equal(0, s.SidewalkRight, 6);
            Assert.Equal(1.6, s.SidewalkLeft, 6); // default
            Assert.False(s.Arrows);
            Assert.True(s.Kerbs);

            SiteElement road = Path("road.access_road", 0, 0, 100, 0); // 5,50 m: two-way, centre line, no kerbs or sidewalks
            RoadSpec r = RoadElements.Spec(road, Cat.RequireType("road.access_road"))!;
            Assert.False(r.OneWay);
            Assert.Equal(5.5, r.CarriagewayWidth, 6);
            Assert.False(r.Kerbs);
            Assert.True(r.Markings);
            Assert.Equal(0, r.SidewalkLeft, 6);
            Assert.Equal(r.CarriagewayLeft, r.Extent(RoadLevel.Edge, true), 6);

            RoadSpec d = RoadElements.Spec(Path("road.driveway", 0, 0, 50, 0), Cat.RequireType("road.driveway"))!; // 3,50 m: one lane
            Assert.True(d.OneWay);
            Assert.Equal(3.5, d.CarriagewayWidth, 6);
            Assert.False(d.Markings);
        }

        [Fact]
        public void Spec_ConvertsDrawingUnitsToMetres()
        {
            SiteElement cm = Path(RoadElements.StreetType, 0, 0, 10000, 0);
            RoadSpec s = RoadElements.Spec(cm, Cat.RequireType(RoadElements.StreetType), 100)!;
            Assert.Equal(100, s.Axis[s.Axis.Count - 1].X, 6);
        }

        [Fact]
        public void Derived_GivesSurfacesMarkingsAndArrows_WithStableValidIds()
        {
            var elements = new List<SiteElement>
            {
                Path(RoadElements.StreetType, -60, 0, 60, 0, ("bikeLeft", ParamValue.Of(1.5)), ("bikeRight", ParamValue.Of(1.5))),
                Path(RoadElements.StreetType, 0, -60, 0, 60),
                Path("road.driveway", 30, -40, 30, -5),
            };
            List<SiteElement> d1 = RoadElements.Derived(elements, Cat);
            List<SiteElement> d2 = RoadElements.Derived(elements, Cat);
            Assert.Equal(d1.Select(e => e.Id), d2.Select(e => e.Id));
            Assert.All(d1, e => Assert.True(SiteElement.IsValidId(e.Id), e.Id));
            Assert.Equal(d1.Count, d1.Select(e => e.Id).Distinct().Count());

            Assert.Single(d1, e => e.TypeId == "road.carriageway");
            Assert.Contains(d1, e => e.TypeId == "road.street_bike_lane");
            Assert.Contains(d1, e => e.TypeId == "road.street_kerb");
            Assert.Contains(d1, e => e.TypeId == "road.sidewalk");
            Assert.Contains(d1, e => e.TypeId == "road.sidewalk_edge");
            Assert.Contains(d1, e => e.TypeId == RoadElements.MarkingType);
            Assert.Contains(d1, e => e.TypeId == RoadElements.ArrowType);

            // one joined carriageway: both streets (6 m) and the driveway, with rounded corners
            var carriageway = (PolygonGeometry)d1.Single(e => e.TypeId == "road.carriageway").Geometry;
            Assert.True(GeometryMath.Area(carriageway) > (120 * 6) + (120 * 6) - 36);
            Assert.True(carriageway.Outer.HasArcs);

            // an arrow element starts at its tail, 2,5 m behind the arrow centre, and points along its lane
            SiteElement arrow = d1.First(e => e.TypeId == RoadElements.ArrowType);
            Assert.Equal(5.0, arrow.Parameters.Number("length"), 6);
        }

        [Fact]
        public void Schedules_ListTheGeneratedCarriagewayAndSidewalks_AndRoadsCountOnlyTheirLength()
        {
            var elements = new List<SiteElement>
            {
                Path(RoadElements.StreetType, 0, 0, 100, 0),
                Path("road.access_road", 50, 0, 50, -40),
            };
            var builder = new ScheduleBuilder(Cat);
            ScheduleDefinition roads = builder.Applicable(elements).Single(d => d.Id == "roads");
            ScheduleTable table = builder.Build(roads, elements);
            string text = string.Join("\n", table.Rows.Select(r => string.Join(" | ", r.Cells)));
            Assert.Contains("Kolovoz", text);
            Assert.Contains("Trotoar", text);
            Assert.Contains("Ivičnjak 15 cm", text);

            ElementQuantity q = QuantityCalculator.For(elements[1], Cat.RequireType("road.access_road"));
            Assert.Equal(40, q.Value, 6);
            Assert.Null(q.Secondary); // its surface is the generated carriageway, not counted twice
        }

        [Fact]
        public void Package_ContainsTheGeneratedElements_AndFlagsTheRoads()
        {
            var elements = new List<SiteElement>
            {
                Path(RoadElements.StreetType, 0, 0, 100, 0),
                Path("road.access_road", 50, 0, 50, -40),
            };
            (JsonObject manifest, JsonObject _, List<string> skipped) = PackageWriter.BuildDocuments(new ProjectSettings(), elements, Cat, new PackageWriteOptions());
            Assert.Empty(skipped);
            var list = manifest["elements"]!.AsArray().Select(n => n!.AsObject()).ToList();
            Assert.Contains(list, e => (string?)e["type"] == "road.carriageway");
            Assert.Contains(list, e => (string?)e["type"] == "road.sidewalk");
            foreach (JsonObject road in list.Where(e => (string?)e["type"] == RoadElements.StreetType || (string?)e["type"] == "road.access_road"))
            {
                Assert.True((bool?)road["custom"]?[RoadElements.NetworkFlag] == true);
            }

            Assert.Contains(manifest["typeDefinitions"]!.AsArray(), t => (string?)t!["id"] == "road.carriageway");
        }

        [Fact]
        public void RoundedAxis_GivesTheBendTheRoadRadius_AndOneArcForACurveOfManyPoints()
        {
            var spec = new RoadSpec { LanesLeft = 1, LanesRight = 1, LaneWidth = 3, CornerRadius = 6 };

            // L-shaped axis in centimetres: one arc with axis radius 6 + 3 = 9 m, left turn of 90 degrees
            var l = RoadElements.RoundedAxis(new[] { new Vec2(0, 0), new Vec2(5000, 0), new Vec2(5000, 5000) }, new[] { 0.0, 0, 0 }, false, spec, 100)!.Value;
            Assert.Equal(4, l.Points.Count);
            Assert.Equal(4100, l.Points[1].X, 3);
            Assert.Equal(Math.Tan(Math.PI / 8), l.Bulges[1], 6);
            Assert.Equal(900, l.Points[2].Y, 3);

            // a curve drawn with points 2 m apart turning 90 degrees in steps: rounded with one arc, not a wave
            var pts = new List<Vec2> { new Vec2(-30, 0) };
            for (int i = 0; i <= 6; i++)
            {
                double a = Math.PI / 2 * i / 6;
                pts.Add(new Vec2(Math.Sin(a) * 4, 4 - (Math.Cos(a) * 4)));
            }

            pts.Add(new Vec2(4, 40));
            var c = RoadElements.RoundedAxis(pts, pts.Select(_ => 0.0).ToList(), false, spec, 1)!.Value;
            Assert.Single(c.Bulges, b => Math.Abs(b) > 1e-9);
            Assert.Equal(4, c.Points.Count);

            // the same curve drawn unevenly by hand (points 15 cm off the curve, alternating): still one smooth arc
            var wobbly = new List<Vec2> { new Vec2(-30, 0) };
            for (int i = 0; i <= 12; i++)
            {
                double a = Math.PI / 2 * i / 12;
                double rr = 12 + (i % 2 == 0 ? 0.15 : -0.15);
                wobbly.Add(new Vec2(Math.Sin(a) * rr, 12 - (Math.Cos(a) * rr)));
            }

            wobbly.Add(new Vec2(12, 50));
            var w = RoadElements.RoundedAxis(wobbly, wobbly.Select(_ => 0.0).ToList(), false, spec, 1)!.Value;
            Assert.All(w.Bulges.Where(b => Math.Abs(b) > 1e-9), b => Assert.True(b > 0, "the curve waves: bulge " + b));
            Assert.True(w.Points.Count < wobbly.Count, "points of the hand-drawn curve are simplified");

            // already rounded: nothing to do
            Assert.Null(RoadElements.RoundedAxis(l.Points, l.Bulges, false, spec, 100));
        }

        [Fact]
        public void StripeBars_FollowTheFirstEdge_LikeSketchUp()
        {
            // crossing 4 m along the road (first edge) and 5,5 m across it: 6 bars of 0,50 m with 0,50 m gaps
            var outer = new List<Vec2> { new Vec2(0, 0), new Vec2(4, 0), new Vec2(4, 5.5), new Vec2(0, 5.5) };
            List<Vec2[]> bars = RoadElements.StripeBars(outer, 0.5, 0.5);
            Assert.Equal(6, bars.Count);
            Assert.All(bars, b => Assert.Equal(4, b[0].DistanceTo(b[1]), 6));
            Assert.All(bars, b => Assert.Equal(0.5, b[1].DistanceTo(b[2]), 6));
            Assert.Equal(5.0, bars[5][0].Y, 6);
        }
    }
}
