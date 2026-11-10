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

            // and the arcs meet: no straight piece longer than 10 cm inside the curve
            for (int i = 1; i + 2 < w.Points.Count; i++)
            {
                if (Math.Abs(w.Bulges[i]) < 1e-9)
                {
                    Assert.True(w.Points[i].DistanceTo(w.Points[i + 1]) < 0.1, "straight piece in the curve at " + w.Points[i]);
                }
            }

            // already rounded: nothing to do
            Assert.Null(RoadElements.RoundedAxis(l.Points, l.Bulges, false, spec, 100));
        }

        [Fact]
        public void StreetWithAnArcInItsAxis_HasConcentricSmoothEdges()
        {
            // the axis of a street from a test drawing (cm): straight, arc of 10,5 m, straight
            ElementType t = Cat.RequireType(RoadElements.StreetType);
            var ring = new Ring(new List<Vec3> { new Vec3(56.6675, 3512.41, 0), new Vec3(1794.08, 3512.41, 0), new Vec3(2819.52, 2236.63, 0), new Vec3(2307.41, -89.3119, 0) }, new List<double> { 0, -0.479211, 0, 0 });
            var e = new SiteElement(Guid.NewGuid().ToString(), RoadElements.StreetType, new PathGeometry(ring, false)) { Parameters = t.CreateDefaultParameters(), Label = "U-005" };
            e.Parameters["bikeLeft"] = ParamValue.Of(1.5);
            e.Parameters["bikeRight"] = ParamValue.Of(1.5);
            RoadNetworkResult res = RoadElements.Network(new[] { e }, Cat, 100)!;
            RoadSpec spec = RoadElements.Spec(e, t, 100)!;
            var expected = new List<double>();
            foreach (RoadLevel level in (RoadLevel[])Enum.GetValues(typeof(RoadLevel)))
            {
                foreach (bool left in new[] { true, false })
                {
                    double d = spec.Extent(level, left);
                    expected.Add(10.5 - d);
                    expected.Add(10.5 + d);
                }
            }

            expected.Add(10.5 - spec.Extent(RoadLevel.Bike, true) + RoadSpec.KerbWidth); // kerb back, inner side
            expected.Add(10.5 + spec.Extent(RoadLevel.Bike, false) + RoadSpec.KerbWidth);
            foreach (KeyValuePair<RoadLevel, List<RoadLoop>> level in res.Outlines)
            {
                foreach (RoadLoop loop in level.Value)
                {
                    foreach (double r in loop.ArcRadii())
                    {
                        Assert.True(expected.Any(x => Math.Abs(Math.Abs(x) - r) < 0.08), level.Key + ": arc radius " + r + " expected one of " + string.Join(", ", expected.Select(x => x.ToString("0.##"))) + "\n" +
                            string.Join("\n", loop.Points.Select((p, i) => p.X.ToString("0.###") + " " + p.Y.ToString("0.###") + " b=" + loop.Bulges[i].ToString("0.####"))));
                    }
                }
            }
        }

        [Fact]
        public void StrayLastClick_IsDropped_TheStreetEndsStraight()
        {
            var spec = new RoadSpec { LanesLeft = 1, LanesRight = 1, LaneWidth = 3, CornerRadius = 6, SidewalkLeft = 1.6, SidewalkRight = 1.6 };
            var pts = new List<Vec2> { new Vec2(0, 0), new Vec2(50, 0), new Vec2(50.5, 0.3) };
            var r = RoadElements.RoundedAxis(pts, new[] { 0.0, 0, 0 }, false, spec, 1)!.Value;
            Assert.Equal(2, r.Points.Count);
            Assert.All(r.Bulges, b => Assert.Equal(0, b, 9));
            Assert.Equal(50.5, r.Points[1].X, 6);
        }

        [Fact]
        public void RoundaboutRing_IsAOneWayStreet_AndAStreetJoinsItWithoutOverlap()
        {
            ElementType t = Cat.RequireType(RoadElements.StreetType);
            var circle = new Ring(new List<Vec3> { new Vec3(11, 0, 0), new Vec3(-11, 0, 0) }, new List<double> { 1, 1 });
            var ring = new SiteElement(Guid.NewGuid().ToString(), RoadElements.StreetType, new PathGeometry(circle, true)) { Parameters = t.CreateDefaultParameters(), Label = "U-001" };
            ring.Parameters["direction"] = ParamValue.Of("one_way");
            ring.Parameters["lanesRight"] = ParamValue.Of(1.0);
            ring.Parameters["laneWidth"] = ParamValue.Of(6.0);
            ring.Parameters["sidewalkLeft"] = ParamValue.Of(0.0);
            ring.Parameters["arrows"] = ParamValue.Of(false);
            SiteElement arm = Path(RoadElements.StreetType, 14, 0, 50, 0); // drawn up to the outer edge of the ring

            RoadNetworkResult res = RoadElements.Network(new[] { ring, arm }, Cat)!;
            Assert.Single(res.Bands[RoadLevel.Carriageway]);
            Assert.Equal(11, res.Axes[arm.Id][0].Length, 2); // extended to the ring axis
            Assert.DoesNotContain(res.Markings, m => m.RoadId == ring.Id); // one lane, no lines on the ring
            Assert.All(res.Markings.Where(m => m.RoadId == arm.Id).SelectMany(m => m.Points), p => Assert.True(p.Length > 14, "lane line enters the ring at " + p));

            // the carriageway is the ring and the arm without overlap: the island stays free, the corners are rounded
            double island = Math.PI * 8 * 8;
            Assert.True(res.Area(RoadLevel.Carriageway) < (Math.PI * 14 * 14) - island + (36 * 6) + 30);
            Assert.Contains(res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()), x => Math.Abs(x - 6) < 0.1);
            Assert.True(res.Area(RoadLevel.Sidewalk) > 0);
        }

        [Fact]
        public void StreetToARoundabout_InMillimetres_EndsSquare_AlsoItsArcs()
        {
            ElementType t = Cat.RequireType(RoadElements.StreetType);
            SiteElement Street(Ring ring, bool closed, params (string Key, ParamValue Value)[] p)
            {
                var e = new SiteElement(Guid.NewGuid().ToString(), RoadElements.StreetType, new PathGeometry(ring, closed)) { Parameters = t.CreateDefaultParameters(), Label = "U" };
                foreach ((string key, ParamValue value) in p)
                {
                    e.Parameters[key] = value;
                }

                return e;
            }

            var elements = new List<SiteElement>
            {
                Street(new Ring(new List<Vec3> { new Vec3(11000, 0, 0), new Vec3(-11000, 0, 0) }, new List<double> { 1, 1 }), true,
                    ("direction", ParamValue.Of("one_way")), ("lanesRight", ParamValue.Of(1.0)), ("laneWidth", ParamValue.Of(6.0)), ("sidewalkLeft", ParamValue.Of(0.0)), ("sidewalkRight", ParamValue.Of(1.6)),
                    ("bikeLeft", ParamValue.Of(0.0)), ("bikeRight", ParamValue.Of(0.0)), ("arrows", ParamValue.Of(false))),
            };
            for (int i = 0; i < 3; i++)
            {
                double a = i * 2 * Math.PI / 3;
                elements.Add(Street(new Ring(new List<Vec3> { new Vec3(Math.Cos(a) * 14000, Math.Sin(a) * 14000, 0), new Vec3(Math.Cos(a) * 39000, Math.Sin(a) * 39000, 0) }), false,
                    ("bikeLeft", ParamValue.Of(0.0)), ("bikeRight", ParamValue.Of(0.0))));
            }

            elements.Add(Street(new Ring(new List<Vec3> { new Vec3(14732.4, -24791.2, 0), new Vec3(7350.75, -12162.3, 0) }), false, ("bikeLeft", ParamValue.Of(1.5)), ("bikeRight", ParamValue.Of(1.5))));
            RoadNetworkResult res = RoadElements.Network(elements, Cat, 1000)!;

            var end = new Vec2(14.7324, -24.7912);
            Vec2 dir = (end - new Vec2(7.35075, -12.1623)).Normalized();
            void Check(string what, RoadLoop loop)
            {
                int n = loop.Points.Count;
                for (int i = 0; i < n; i++)
                {
                    Vec2 p = loop.Points[i], q = loop.Points[(i + 1) % n];
                    var probe = new List<Vec2> { p };
                    double b = loop.Bulges[i];
                    if (Math.Abs(b) > 1e-9)
                    {
                        Vec2 chord = q - p;
                        Vec2 mid = p + (chord * 0.5);
                        probe.Add(mid - (chord.Normalized().Perp() * (b * chord.Length / 2)));
                    }

                    foreach (Vec2 x in probe.Where(x => x.DistanceTo(end) < 10))
                    {
                        double along = (x - end).Dot(dir);
                        Assert.True(along < 0.01, what + " reaches " + along.ToString("0.###") + " m past the dead end at " + x + " (bulge " + b.ToString("0.####") + ")\n" +
                            "fillets:\n" + string.Join("\n", res.Fillets.Select(f => f.Count + " pts, nearest " + f.Min(z => z.DistanceTo(x)).ToString("0.###") + ", first " + f[0] + ", bbox " + f.Min(z => z.X).ToString("0.#") + ".." + f.Max(z => z.X).ToString("0.#") + " / " + f.Min(z => z.Y).ToString("0.#") + ".." + f.Max(z => z.Y).ToString("0.#"))));
                    }
                }
            }

            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                foreach (RoadRegion region in band.Value)
                {
                    Check(band.Key + " band", region.Outer);
                }
            }

            foreach (KeyValuePair<RoadLevel, List<RoadLoop>> level in res.Outlines)
            {
                foreach (RoadLoop loop in level.Value)
                {
                    Check(level.Key + " outline", loop);
                }
            }
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
