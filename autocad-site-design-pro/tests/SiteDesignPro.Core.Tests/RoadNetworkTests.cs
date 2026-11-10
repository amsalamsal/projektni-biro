using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Roads;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class RoadNetworkTests
    {
        private static RoadSpec Road(string id, double x0, double y0, double x1, double y1, Action<RoadSpec>? set = null)
        {
            var r = new RoadSpec
            {
                Id = id,
                Axis = new List<Vec2> { new Vec2(x0, y0), new Vec2(x1, y1) },
                LanesLeft = 1,
                LanesRight = 1,
                LaneWidth = 3.0,
                SidewalkLeft = 2.0,
                SidewalkRight = 2.0,
                CornerRadius = 6.0,
            };
            set?.Invoke(r);
            return r;
        }

        [Fact]
        public void SingleRoad_HasCarriagewayKerbsAndSidewalksOfTheRightSize()
        {
            RoadNetworkResult res = RoadNetwork.Build(new[] { Road("A", 0, 0, 100, 0) });
            Assert.Equal(600, res.Area(RoadLevel.Carriageway), 0);            // 100 m x 6 m
            Assert.Equal(0, res.Area(RoadLevel.Bike), 3);
            Assert.Equal(2 * 100 * 0.15, res.Area(RoadLevel.Kerb), 0);        // 15 cm kerb on both sides
            Assert.Equal(2 * 100 * 2.0, res.Area(RoadLevel.Sidewalk), 0);
            Assert.Equal(2 * 100 * 0.10, res.Area(RoadLevel.Edge), 0);        // 10 cm edge along the sidewalk
            Assert.Single(res.Bands[RoadLevel.Carriageway]);
        }

        [Fact]
        public void OneWayRoad_IsCentredOnItsAxis()
        {
            RoadSpec r = Road("A", 0, 0, 50, 0, x => { x.OneWay = true; x.LanesRight = 2; x.LaneWidth = 3.25; });
            Assert.Equal(3.25, r.CarriagewayLeft, 6);
            Assert.Equal(3.25, r.CarriagewayRight, 6);
            RoadNetworkResult res = RoadNetwork.Build(new[] { r });
            Assert.Equal(50 * 6.5, res.Area(RoadLevel.Carriageway), 0);

            // one dashed line between the two lanes, arrows all pointing along the axis
            Assert.All(res.Markings, m => Assert.False(m.Centre));
            Assert.NotEmpty(res.Arrows);
            Assert.All(res.Arrows, a => Assert.Equal(0, a.Angle, 6));
        }

        [Fact]
        public void Crossroads_DoNotOverlap_AndCornersAreRoundedWithTheKerbRadius()
        {
            var a = Road("A", -60, 0, 60, 0);
            var b = Road("B", 0, -60, 0, 60);
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });

            // one carriageway: two 120 x 6 strips minus the shared 6 x 6 square, plus four corner fillets of the
            // effective radius (no bike lane: 6 m): each adds r^2 - pi r^2 / 4
            double rects = (2 * 120 * 6) - 36;
            double fillet = (36 - (Math.PI * 36 / 4)) * 4;
            Assert.Single(res.Bands[RoadLevel.Carriageway]);
            Assert.Equal(rects + fillet, res.Area(RoadLevel.Carriageway), 0);

            // kerb lines are real arcs with the kerb radius
            List<double> radii = res.Outlines[RoadLevel.Bike].Count > 0
                ? res.Outlines[RoadLevel.Bike].SelectMany(l => l.ArcRadii()).ToList()
                : res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()).ToList();
            Assert.Equal(4, radii.Count(r => Math.Abs(r - 6.0) < 0.05));

            // sidewalks never cover the carriageway
            Assert.True(res.Area(RoadLevel.Sidewalk) > 0);
        }

        [Fact]
        public void BikeLanes_MakeTheEffectiveRadiusLargerThanTheKerbRadius()
        {
            var a = Road("A", -60, 0, 60, 0, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; });
            var b = Road("B", 0, -60, 0, 60, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; });
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });
            Assert.Equal(6.0, res.ActualRadius, 6);
            Assert.Equal(7.5, res.EffectiveRadius, 6);
            Assert.Equal(4, res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()).Count(r => Math.Abs(r - 7.5) < 0.05));
            Assert.Equal(4, res.Outlines[RoadLevel.Bike].SelectMany(l => l.ArcRadii()).Count(r => Math.Abs(r - 6.0) < 0.05));
            Assert.True(res.Area(RoadLevel.Bike) > 0);
        }

        [Fact]
        public void SideStreetDrawnShortOfTheMainRoad_IsJoinedToIt()
        {
            var main = Road("A", -60, 0, 60, 0);
            var side = Road("B", 0, -60, 0, -5); // ends 2 m before the carriageway edge
            RoadNetworkResult res = RoadNetwork.Build(new[] { main, side });
            Assert.Equal(new Vec2(0, 0), res.Axes["B"][1]);
            Assert.Single(res.Bands[RoadLevel.Carriageway]);
            Assert.Contains(res.Notes, n => n.Contains("produžena"));

            // T junction: two rounded corners
            Assert.Equal(2, res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()).Count(r => Math.Abs(r - 6.0) < 0.05));
        }

        [Fact]
        public void StraightEdgesStayStraight_OnlyTheCornersAreArcs()
        {
            var a = Road("A", -60, 0, 60, 0, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; });
            var b = Road("B", 0, -60, 0, 60, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; });
            var c = Road("C", 30, -60, 30, -5, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; });
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b, c });

            // concentric corner radii: 7.5 carriageway, 6.0 kerb, 5.85 kerb back, 4.25 sidewalk, 4.15 edge
            var allowed = new[] { 7.5, 6.0, 5.85, 4.25, 4.15 };
            foreach (KeyValuePair<RoadLevel, List<RoadLoop>> level in res.Outlines)
            {
                foreach (double r in level.Value.SelectMany(l => l.ArcRadii()))
                {
                    Assert.True(allowed.Any(x => Math.Abs(x - r) < 0.05), level.Key + ": unexpected arc radius " + r);
                }
            }

            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                foreach (RoadRegion region in band.Value)
                {
                    foreach (double r in region.Outer.ArcRadii().Concat(region.Holes.SelectMany(h => h.ArcRadii())))
                    {
                        Assert.True(allowed.Any(x => Math.Abs(x - r) < 0.05), band.Key + " band: unexpected arc radius " + r);
                    }
                }
            }
        }

        [Fact]
        public void BendOfOneStreet_IsRoundedOnTheInnerAndOuterSide()
        {
            // L-shaped axis, 6 m carriageway, kerb radius 6: axis radius 9, inner kerb 6, outer kerb 12
            var r = Road("A", 0, 0, 50, 0);
            r.Axis.Add(new Vec2(50, 50));
            RoadNetworkResult res = RoadNetwork.Build(new[] { r });
            List<double> kerbLine = res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()).ToList();
            Assert.Contains(kerbLine, x => Math.Abs(x - 6.0) < 0.05);
            Assert.Contains(kerbLine, x => Math.Abs(x - 12.0) < 0.05);

            // the sidewalk on the inner side follows the kerb concentrically: kerb back 5,85, sidewalk 3,85, edge 3,75
            var allowed = new[] { 6.0, 12.0, 5.85, 12.15, 3.85, 14.15, 3.75, 14.25 };
            foreach (RoadRegion region in res.Bands.Values.SelectMany(b => b))
            {
                foreach (double x in region.Outer.ArcRadii().Concat(region.Holes.SelectMany(h => h.ArcRadii())))
                {
                    Assert.True(allowed.Any(a => Math.Abs(a - x) < 0.05), "unexpected arc radius " + x);
                }
            }

            Assert.Single(res.Bands[RoadLevel.Carriageway]);
            Assert.Equal(2, res.Bands[RoadLevel.Sidewalk].Count);
        }

        [Fact]
        public void StreetMeetingASimpleRoad_KeepsItsLevelsConcentricAtTheCorners()
        {
            var street = Road("A", -60, 0, 60, 0, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; });
            var simple = Road("B", 0, -60, 0, 60, x => { x.SidewalkLeft = 0; x.SidewalkRight = 0; x.Kerbs = false; x.Markings = true; x.Arrows = false; x.LaneWidth = 2.75; });
            RoadNetworkResult res = RoadNetwork.Build(new[] { street, simple });

            // the kerb line has the kerb radius, the carriageway edge is concentric with it where the bike lane runs
            Assert.All(res.Outlines[RoadLevel.Bike].SelectMany(l => l.ArcRadii()), x => Assert.True(Math.Abs(x - 6.0) < 0.05, "kerb line arc " + x));
            Assert.All(res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()), x => Assert.True(Math.Abs(x - 6.0) < 0.05 || Math.Abs(x - 7.5) < 0.05, "carriageway arc " + x));

            // no bike lane along the simple road: away from the corners its sides are carriageway up to the kerb line
            Assert.All(res.Bands[RoadLevel.Bike], b => Assert.All(b.Outer.Points, p => Assert.True(Math.Abs(p.Y) < 40 || Math.Abs(p.X) > 10, "bike lane along the simple road at " + p)));
            Assert.True(res.Area(RoadLevel.Sidewalk) > 4 * 50 * 1.6);
        }

        [Fact]
        public void ParallelStreetsCloseToEachOther_AreNotMerged()
        {
            var a = Road("A", 0, 0, 100, 0);
            var b = Road("B", 0, 12, 100, 12); // 6 m between the carriageways
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });
            Assert.Equal(2, res.Bands[RoadLevel.Carriageway].Count);
            Assert.Equal(1200, res.Area(RoadLevel.Carriageway), 0);
        }

        [Fact]
        public void LaneMarkings_StopBeforeTheJunction()
        {
            var a = Road("A", -100, 0, 100, 0);
            var b = Road("B", 0, -100, 0, 100);
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });
            Assert.NotEmpty(res.Markings);
            foreach (RoadMarking m in res.Markings.Where(m => m.RoadId == "A"))
            {
                Assert.All(m.Points, p => Assert.True(Math.Abs(p.X) >= 3 + 1 - 0.01, "A marking enters the junction at " + p));
            }

            // dashes 3 m long
            Assert.All(res.Markings, m => Assert.True(Length(m.Points) <= 3.0 + 1e-6));
        }

        [Fact]
        public void LaneMarkings_AreInterruptedAtAPedestrianCrossing()
        {
            var crossing = new List<Vec2> { new Vec2(48, -4), new Vec2(52, -4), new Vec2(52, 4), new Vec2(48, 4) };
            RoadNetworkResult res = RoadNetwork.Build(new[] { Road("A", 0, 0, 100, 0) }, new[] { crossing });
            Assert.NotEmpty(res.Markings);
            Assert.All(res.Markings.SelectMany(m => m.Points), p => Assert.False(p.X > 48.01 && p.X < 51.99, "marking inside the crossing at " + p));
            Assert.All(res.Arrows, a => Assert.False(Math.Abs(a.Position.X - 50) < 5, "arrow on the crossing at " + a.Position));
        }

        [Fact]
        public void Arrows_EveryFortyMetres_InBothDirections()
        {
            RoadNetworkResult res = RoadNetwork.Build(new[] { Road("A", 0, 0, 200, 0) });
            // stations 20, 60, 100, 140, 180; one arrow per lane
            Assert.Equal(10, res.Arrows.Count);
            Assert.Equal(5, res.Arrows.Count(a => Math.Abs(a.Angle) < 1e-6 && a.Position.Y < 0));       // right lane, with the axis
            Assert.Equal(5, res.Arrows.Count(a => Math.Abs(Math.Abs(a.Angle) - Math.PI) < 1e-6 && a.Position.Y > 0)); // left lane, against it
            Assert.Empty(RoadNetwork.Build(new[] { Road("A", 0, 0, 200, 0, x => x.Arrows = false) }).Arrows);
        }

        [Fact]
        public void TwoLanesEachWay_GetASolidCentreLine()
        {
            RoadNetworkResult res = RoadNetwork.Build(new[] { Road("A", 0, 0, 100, 0, x => { x.LanesLeft = 2; x.LanesRight = 2; }) });
            Assert.Contains(res.Markings, m => m.Centre && Length(m.Points) > 90);
            Assert.Contains(res.Markings, m => !m.Centre);
        }

        [Fact]
        public void Parameters_RoundTripAndAreClampedToTheSpecifiedRanges()
        {
            var r = new RoadSpec { OneWay = true, LanesRight = 5, LaneWidth = 3.25, SidewalkLeft = 7, SidewalkRight = 0, BikeLeft = 3, BikeRight = 1.2, Arrows = false, CornerRadius = 5 };
            List<string> notes = r.Normalize();
            Assert.Equal(3, r.LanesRight);
            Assert.Equal(5.0, r.SidewalkLeft);
            Assert.Equal(2.0, r.BikeLeft);
            Assert.NotEmpty(notes);

            RoadSpec back = RoadSpec.FromParameterString(r.ToParameterString());
            Assert.True(back.OneWay);
            Assert.Equal(3, back.LanesRight);
            Assert.Equal(3.25, back.LaneWidth, 6);
            Assert.Equal(0, back.SidewalkRight, 6);
            Assert.Equal(1.2, back.BikeRight, 6);
            Assert.False(back.Arrows);
            Assert.Equal(5, back.CornerRadius, 6);
        }

        [Fact]
        public void LargeStateCoordinates_GiveTheSameResult()
        {
            double x0 = 6_530_000, y0 = 4_860_000;
            var a = Road("A", x0 - 60, y0, x0 + 60, y0);
            var b = Road("B", x0, y0 - 60, x0, y0 + 60);
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });
            Assert.Equal((2 * 120 * 6) - 36 + ((36 - (Math.PI * 36 / 4)) * 4), res.Area(RoadLevel.Carriageway), 0);
            Assert.All(res.Outlines[RoadLevel.Carriageway][0].Points, p => Assert.True(p.X > x0 - 100 && p.Y > y0 - 100));
        }

        private static double Length(List<Vec2> pts)
        {
            double s = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                s += pts[i].DistanceTo(pts[i - 1]);
            }

            return s;
        }
    }
}
