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
        public void BikeLanes_StandBehindTheKerb_TheEffectiveRadiusIsTheKerbRadius()
        {
            var a = Road("A", -60, 0, 60, 0, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; });
            var b = Road("B", 0, -60, 0, 60, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; });
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });
            Assert.Equal(6.0, res.ActualRadius, 6);
            Assert.Equal(6.0, res.EffectiveRadius, 6);

            // kerb at the carriageway edge (R 6), the bike lane behind it (outer edge R 6 - 0,15 - 1,5 = 4,35)
            Assert.Equal(4, res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()).Count(r => Math.Abs(r - 6.0) < 0.05));
            Assert.Equal(4, res.Outlines[RoadLevel.Kerb].SelectMany(l => l.ArcRadii()).Count(r => Math.Abs(r - 5.85) < 0.05));
            Assert.Equal(4, res.Outlines[RoadLevel.Bike].SelectMany(l => l.ArcRadii()).Count(r => Math.Abs(r - 4.35) < 0.05));
            Assert.InRange(res.Area(RoadLevel.Bike), (4 * 120 * 1.5) - 60 - (8 * 1.2 * 1.5), 4 * 120 * 1.5); // less the corners and the 8 ramps
        }

        [Fact]
        public void Median_SeparatesTheDirections_WithContinuousLines()
        {
            var r = Road("A", 0, 0, 100, 0, x => { x.Median = 2; });
            Assert.Equal(4, r.CarriagewayLeft, 6);
            RoadNetworkResult res = RoadNetwork.Build(new[] { r });
            Assert.Equal(100 * 8, res.Area(RoadLevel.Carriageway), 0);
            Assert.All(res.Markings, m => Assert.True(m.Centre));
            Assert.Contains(res.Markings, m => Math.Abs(m.Points[0].Y - 1) < 1e-6);
            Assert.Contains(res.Markings, m => Math.Abs(m.Points[0].Y + 1) < 1e-6);
            Assert.All(res.Arrows, a => Assert.True(Math.Abs(Math.Abs(a.Position.Y) - 2.5) < 1e-6, "arrow at " + a.Position));
        }

        [Fact]
        public void Crossroads_GetAnAccessibleRampAtEveryCornerEnd()
        {
            var a = Road("A", -60, 0, 60, 0);
            var b = Road("B", 0, -60, 0, 60);
            RoadNetworkResult res = RoadNetwork.Build(new[] { a, b });
            Assert.Equal(8, res.Ramps.Count);
            foreach (RoadLoop ramp in res.Ramps)
            {
                // 1,20 m along the kerb, across the kerb (0,15) and the sidewalk (2,00), right after the corner arc
                double area = Math.Abs(Area(ramp.Points));
                Assert.InRange(area, (1.2 * 2.15) - 0.05, (1.2 * 2.15) + 0.05);
                Assert.All(ramp.Points, p => Assert.True(Math.Abs(p.X) >= 8.99 || Math.Abs(p.Y) >= 8.99, "ramp inside the corner at " + p));
            }

            // no sidewalk: no ramp
            Assert.Empty(RoadNetwork.Build(new[] { Road("A", -60, 0, 60, 0, x => { x.SidewalkLeft = 0; x.SidewalkRight = 0; }), Road("B", 0, -60, 0, 60, x => { x.SidewalkLeft = 0; x.SidewalkRight = 0; }) }).Ramps);
        }

        [Fact]
        public void PedestrianCrossing_GetsARampOnBothEnds()
        {
            // crossing 4 m wide across the middle of a straight street (carriageway 6 m, sidewalks 2 m)
            var box = new List<Vec2> { new Vec2(48, -3), new Vec2(52, -3), new Vec2(52, 3), new Vec2(48, 3) };
            RoadNetworkResult res = RoadNetwork.Build(new[] { Road("A", 0, 0, 100, 0) }, new[] { box }, new[] { box });
            Assert.Equal(2, res.Ramps.Count);
            foreach (RoadLoop ramp in res.Ramps)
            {
                Assert.InRange(Math.Abs(Area(ramp.Points)), (1.2 * 2.15) - 0.05, (1.2 * 2.15) + 0.05);
                Assert.All(ramp.Points, p => Assert.InRange(p.X, 49.39, 50.61));
            }

            Assert.Contains(res.Ramps, r => r.Points.All(p => p.Y > 2.9));
            Assert.Contains(res.Ramps, r => r.Points.All(p => p.Y < -2.9));

            // the sidewalk and kerb are cut where the ramps are (they are lowered there)
            double walk = res.Bands[RoadLevel.Sidewalk].Sum(r => r.Area);
            Assert.InRange(walk, (2 * 100 * 2.0) - (2 * 1.2 * 2.0) - 0.2, (2 * 100 * 2.0) - (2 * 1.2 * 2.0) + 0.2);

            // in 3D the ramp rises from the carriageway (0) to the sidewalk (0,13 above the ramp's own offset)
            List<List<Vec2>> kerbLines = res.Outlines[RoadLevel.Carriageway].Select(l => l.Points).ToList();
            List<Vec3[]> tris = RoadElements.RampTriangles(res.Ramps[0].Points, kerbLines, 0.13, _ => 0);
            Assert.NotEmpty(tris);
            Assert.InRange(tris.SelectMany(t => t).Min(v => v.Z), -0.001, 0.001);
            Assert.InRange(tris.SelectMany(t => t).Max(v => v.Z), 0.129, 0.131);

            // no sidewalk on the right: only the left ramp
            RoadNetworkResult one = RoadNetwork.Build(new[] { Road("A", 0, 0, 100, 0, x => x.SidewalkRight = 0) }, new[] { box }, new[] { box });
            Assert.Single(one.Ramps);
        }

        [Fact]
        public void CrossingNearAJunction_ReplacesTheCornerRampsOnItsKerbs()
        {
            // crossing on road A just after the corner (corner ramps of A at x 9,0–10,2 on both kerbs)
            var box = new List<Vec2> { new Vec2(13, -3), new Vec2(17, -3), new Vec2(17, 3), new Vec2(13, 3) };
            RoadNetworkResult res = RoadNetwork.Build(new[] { Road("A", -60, 0, 60, 0), Road("B", 0, -60, 0, 60) }, new[] { box }, new[] { box });
            Assert.Equal(8, res.Ramps.Count);
            Assert.DoesNotContain(res.Ramps, r => r.Points.Average(p => p.X) > 8.5 && r.Points.Average(p => p.X) < 11);
            Assert.Equal(2, res.Ramps.Count(r => Math.Abs(r.Points.Average(p => p.X) - 15) < 0.1));
        }

        private static double Area(IReadOnlyList<Vec2> p)
        {
            double s = 0;
            for (int i = 0; i < p.Count; i++)
            {
                s += p[i].Cross(p[(i + 1) % p.Count]);
            }

            return s / 2;
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

            // concentric corner radii: 6.0 kerb at the carriageway, 5.85 kerb back, 4.35 bike lane, 2.75 sidewalk, 2.65 edge
            var allowed = new[] { 6.0, 5.85, 4.35, 2.75, 2.65 };
            foreach (KeyValuePair<RoadLevel, List<RoadLoop>> level in res.Outlines)
            {
                foreach (RoadLoop loop in level.Value)
                {
                    foreach (double r in loop.ArcRadii())
                    {
                        Assert.True(allowed.Any(x => Math.Abs(x - r) < 0.05), level.Key + ": unexpected arc radius " + r + "\n" + Dump(loop));
                    }
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

            // the sidewalk (2 m) on the inner side follows the kerb concentrically: kerb back 5,85, sidewalk 3,85, edge 3,75
            var allowed = new[] { 6.0, 12.0, 5.85, 12.15, 3.85, 14.15, 3.75, 14.25 };
            foreach (RoadRegion region in res.Bands.Values.SelectMany(b => b))
            {
                foreach (RoadLoop loop in new[] { region.Outer }.Concat(region.Holes))
                {
                    foreach (double x in loop.ArcRadii())
                    {
                        Assert.True(allowed.Any(a => Math.Abs(a - x) < 0.05), "unexpected arc radius " + x + "\n" + Dump(loop));
                    }
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

            // the kerb line (carriageway edge) has the kerb radius
            Assert.All(res.Outlines[RoadLevel.Carriageway].SelectMany(l => l.ArcRadii()), x => Assert.True(Math.Abs(x - 6.0) < 0.05, "carriageway arc " + x));
            Assert.True(res.Area(RoadLevel.Bike) > 0);

            // no bike lane along the simple road: away from the corners its sides are carriageway up to the kerb line
            Assert.All(res.Bands[RoadLevel.Bike], b => Assert.All(b.Outer.Points, p => Assert.True(Math.Abs(p.Y) < 40 || Math.Abs(p.X) > 10, "bike lane along the simple road at " + p)));
            Assert.True(res.Area(RoadLevel.Sidewalk) > 4 * 50 * 1.6);
        }

        [Fact]
        public void DeadEndNearAJunction_EndsSquare()
        {
            var main = Road("A", -60, 0, 60, 0, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; });
            var side = Road("B", 20, -30, 0, -5, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; });
            RoadNetworkResult res = RoadNetwork.Build(new[] { main, side });

            // nothing reaches past the line across the dead end of B at (20, -30)
            Vec2 dir = (new Vec2(20, -30) - new Vec2(0, -5)).Normalized();
            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                foreach (RoadRegion region in band.Value)
                {
                    foreach (Vec2 p in region.Outer.Points)
                    {
                        double along = (p - new Vec2(20, -30)).Dot(dir);
                        if (p.DistanceTo(new Vec2(20, -30)) > 10)
                        {
                            continue;
                        }

                        Assert.True(along < 0.01, band.Key + " reaches " + along.ToString("0.###") + " m past the dead end at " + p);
                    }
                }
            }
        }

        [Fact]
        public void StreetToARoundabout_EndsSquare()
        {
            var circle = new List<Vec2>();
            for (int i = 0; i < 180; i++)
            {
                double a = 2 * Math.PI * i / 180;
                circle.Add(new Vec2(Math.Cos(a) * 11, Math.Sin(a) * 11));
            }

            var ring = new RoadSpec { Id = "R", Axis = circle, Closed = true, OneWay = true, LanesRight = 1, LaneWidth = 6, SidewalkLeft = 0, SidewalkRight = 1.6, Arrows = false, Corners = new List<Vec2>() };
            var roads = new List<RoadSpec> { ring };
            for (int i = 0; i < 3; i++)
            {
                double a = i * 2 * Math.PI / 3;
                var u = new Vec2(Math.Cos(a), Math.Sin(a));
                roads.Add(Road("A" + i, u.X * 14, u.Y * 14, u.X * 39, u.Y * 39, x => { x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; }));
            }

            roads.Add(Road("S", 14.7324, -24.7912, 7.35075, -12.1623, x => { x.BikeLeft = 1.5; x.BikeRight = 1.5; x.SidewalkLeft = 1.6; x.SidewalkRight = 1.6; }));
            RoadNetworkResult res = RoadNetwork.Build(roads);
            var end = new Vec2(14.7324, -24.7912);
            Vec2 dir = (end - new Vec2(7.35075, -12.1623)).Normalized();
            foreach (KeyValuePair<RoadLevel, List<RoadRegion>> band in res.Bands)
            {
                foreach (RoadRegion region in band.Value)
                {
                    foreach (Vec2 p in region.Outer.Points)
                    {
                        if (p.DistanceTo(end) > 10)
                        {
                            continue;
                        }

                        double along = (p - end).Dot(dir);
                        Assert.True(along < 0.01, band.Key + " reaches " + along.ToString("0.###") + " m past the dead end at " + p + "; fillets near: " + res.Notes.Count);
                    }
                }
            }
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

        private static string Dump(RoadLoop l) => string.Join("\n", l.Points.Select((p, i) => p.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " " + p.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " b=" + l.Bulges[i].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));

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
