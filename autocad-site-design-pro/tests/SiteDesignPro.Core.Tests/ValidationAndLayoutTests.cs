using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Labels;
using SiteDesignPro.Core.Layout;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Quantities;
using SiteDesignPro.Core.Validation;
using Xunit;
using static SiteDesignPro.Core.Tests.TestSupport;

namespace SiteDesignPro.Core.Tests
{
    public class ValidationTests
    {
        private static ValidationReport Run(params SiteElement[] elements) => new ProjectValidator(TestSupport.Catalog).Validate(elements);

        [Fact]
        public void OpenOrDegenerateBoundary_IsGeometricError()
        {
            var twoPoints = new PolygonGeometry(new Ring(new[] { new Vec3(0, 0, 0), new Vec3(5, 0, 0) }));
            var collinear = new PolygonGeometry(new Ring(new[] { new Vec3(0, 0, 0), new Vec3(5, 0, 0), new Vec3(10, 0, 0) }));
            ValidationReport r = Run(Element("surface.gravel", twoPoints), Element("surface.gravel", collinear));
            Assert.Contains(r.Issues, i => i.Code == "SDP-G005" && i.Severity == Severity.Error && i.Kind == IssueKind.GeometricError);
            Assert.Contains(r.Issues, i => i.Code == "SDP-G006");
        }

        [Fact]
        public void SelfIntersectingBoundary_AndZeroLengthPath_AreErrors()
        {
            var bowtie = new PolygonGeometry(new Ring(new[] { new Vec3(0, 0, 0), new Vec3(10, 10, 0), new Vec3(10, 0, 0), new Vec3(0, 10, 0) }));
            ValidationReport r = Run(Element("surface.lawn", bowtie), Element("wall.concrete", Line(1, 1, 1, 1)));
            Assert.Contains(r.Issues, i => i.Code == "SDP-G007");
            Assert.Contains(r.Issues, i => i.Code == "SDP-G003");
        }

        [Fact]
        public void WrongGeometryKind_MissingIdAndDuplicates()
        {
            SiteElement a = Element("furniture.bench", Pt(0, 0));
            SiteElement b = Element("furniture.bench", Pt(10, 0));
            b.Id = a.Id;
            SiteElement c = Element("surface.lawn", Pt(0, 0));
            SiteElement d = Element("furniture.bench", Pt(20, 0));
            d.Id = "not-an-id";
            ValidationReport r = Run(a, b, c, d, new SiteElement(SiteElement.NewId(), "custom.unknown", Pt(0, 0)));
            Assert.Contains(r.Issues, i => i.Code == "SDP-D002");
            Assert.Contains(r.Issues, i => i.Code == "SDP-G001");
            Assert.Contains(r.Issues, i => i.Code == "SDP-D001");
            Assert.Contains(r.Issues, i => i.Code == "SDP-D003");
        }

        [Fact]
        public void ParameterChecks_RangeTypeEnumRequiredMaterial()
        {
            SiteElement e = Element("landscape.tree_deciduous", Pt(0, 0), p =>
            {
                p["height"] = ParamValue.Of(8000); // millimetres typed into a metre field
                p["form"] = ParamValue.Of("cubic");
                p["canopyDiameter"] = ParamValue.Null;
                p["foliageMaterial"] = ParamValue.Of("unobtainium");
                p["trunkDiameter"] = ParamValue.Of("thick");
            });
            ValidationReport r = Run(e);
            Assert.Contains(r.Issues, i => i.Code == "SDP-P004" && i.Resolution.Contains("milimetrima", StringComparison.Ordinal));
            Assert.Contains(r.Issues, i => i.Code == "SDP-P006");
            Assert.Contains(r.Issues, i => i.Code == "SDP-P001");
            Assert.Contains(r.Issues, i => i.Code == "SDP-P007" && i.Severity == Severity.Warning);
            Assert.Contains(r.Issues, i => i.Code == "SDP-P002");
        }

        [Fact]
        public void ReviewRequired_IsReviewNotError()
        {
            ValidationReport r = Run(Element("utility.waste_container_ug_5m3", Pt(0, 0)));
            Assert.Equal(0, r.ErrorCount);
            Assert.True(r.ReviewCount >= 2);
            Assert.All(r.Issues.Where(i => i.Code == "SDP-R001"), i => Assert.Equal(IssueKind.ProfessionalReview, i.Kind));
        }

        [Fact]
        public void PointCollisions_RespectZonesAndTouching()
        {
            SiteElement b1 = Element("furniture.bench", Pt(0, 0));
            SiteElement b2 = Element("furniture.bench", Pt(1.0, 0.2));        // overlaps b1
            SiteElement b3 = Element("furniture.bench", Pt(10, 0));
            SiteElement b4 = Element("furniture.bench", Pt(11.8, 0));         // touches b3 exactly
            SiteElement tank = Element("water.rainwater_tank", Pt(30, 0));    // underground
            SiteElement bench = Element("furniture.bench", Pt(30, 0));        // above the tank: no conflict
            SiteElement tank2 = Element("utility.septic_tank", Pt(31, 0));    // overlaps tank underground
            ValidationReport r = Run(b1, b2, b3, b4, tank, bench, tank2);
            List<ValidationIssue> collisions = r.Issues.Where(i => i.Code == "SDP-C001").ToList();
            Assert.Equal(2, collisions.Count);
            Assert.Contains(collisions, i => (i.ElementId == b1.Id || i.RelatedElementId == b1.Id) && (i.ElementId == b2.Id || i.RelatedElementId == b2.Id));
            Assert.Contains(collisions, i => i.Message.Contains("podzemno", StringComparison.Ordinal));
        }

        [Fact]
        public void ParkingBay_ConflictsWithObstaclesButNotWheelStops()
        {
            SiteElement bay = Element("road.parking_bay", Pt(0, 0));
            SiteElement stop = Element("road.wheel_stop", Pt(0, 4.5));
            SiteElement tree = Element("landscape.tree_deciduous", Pt(0, 2));
            ValidationReport r = Run(bay, stop, tree);
            Assert.Single(r.Issues, i => i.Code == "SDP-C001");
            Assert.Contains(r.Issues, i => i.Code == "SDP-C001" && (i.ElementId == tree.Id || i.RelatedElementId == tree.Id));
        }

        [Fact]
        public void ServiceClearance_ReportsIntrusions()
        {
            SiteElement pump = Element("sustainability.heat_pump", Pt(0, 0), p => p["serviceClearance"] = ParamValue.Of(1.0));
            SiteElement bench = Element("furniture.bench", Pt(0, -1.2));
            SiteElement far = Element("furniture.bench", Pt(0, -5));
            ValidationReport r = Run(pump, bench, far);
            ValidationIssue issue = Assert.Single(r.Issues, i => i.Code == "SDP-C002");
            Assert.Equal(bench.Id, issue.RelatedElementId);
            Assert.Equal(IssueKind.DesignConflict, issue.Kind);
        }

        [Fact]
        public void SurfaceOverlap_AndCanopyClearance()
        {
            SiteElement a = Element("surface.gravel", Rect(0, 0, 10, 10));
            SiteElement b = Element("surface.lawn", Rect(5, 5, 15, 15));
            SiteElement c = Element("surface.lawn", Rect(10, 0, 20, 5)); // touches a along x = 10 only
            SiteElement house = Element("building.footprint", Rect(30, 0, 40, 10));
            SiteElement tree = Element("landscape.tree_deciduous", Pt(27, 5), p => p["matureCanopyDiameter"] = ParamValue.Of(10));
            ValidationReport r = Run(a, b, c, house, tree);
            Assert.Single(r.Issues, i => i.Code == "SDP-C003" && ((i.ElementId == a.Id && i.RelatedElementId == b.Id) || (i.ElementId == b.Id && i.RelatedElementId == a.Id)));
            Assert.DoesNotContain(r.Issues, i => i.Code == "SDP-C003" && (i.ElementId == c.Id || i.RelatedElementId == c.Id) && (i.ElementId == a.Id || i.RelatedElementId == a.Id));
            Assert.Contains(r.Issues, i => i.Code == "SDP-C005" && i.ElementId == tree.Id);
        }

        [Fact]
        public void ElevationOutsideRange_HintsAtUnits()
        {
            ValidationReport r = Run(Element("furniture.bench", Pt(0, 0, 150)), Element("furniture.bench", Pt(10, 0, 1500)));
            ValidationIssue issue = Assert.Single(r.Issues, i => i.Code == "SDP-G012");
            Assert.Contains("jedinice", issue.Resolution, StringComparison.Ordinal);
        }

        [Fact]
        public void LogicChecks_RequiredWidthRampSlopeAndDuplicateLabels()
        {
            SiteElement fire = Element("road.fire_access", Line(0, 0, 50, 0), p => { p["width"] = ParamValue.Of(3.0); p["requiredClearWidth"] = ParamValue.Of(3.5); });
            var ramp = new PolygonGeometry(new Ring(new[] { new Vec3(0, 20, 0), new Vec3(2, 20, 0), new Vec3(2, 23, 0), new Vec3(0, 23, 0) }));
            SiteElement r1 = Element("level.ramp", ramp, p => { p["rise"] = ParamValue.Of(0.3); p["maxSlope"] = ParamValue.Of(6.0); }, "RA-001");
            SiteElement l2 = Element("furniture.bench", Pt(100, 100), null, "RA-001");
            ValidationReport r = Run(fire, r1, l2);
            Assert.Contains(r.Issues, i => i.Code == "SDP-C010");
            Assert.Contains(r.Issues, i => i.Code == "SDP-C011" && i.Message.Contains("10,0 %", StringComparison.Ordinal));
            Assert.Contains(r.Issues, i => i.Code == "SDP-D010");
        }

        [Fact]
        public void LargeProject_ValidatesInReasonableTime()
        {
            var list = new List<SiteElement>();
            for (int i = 0; i < 5000; i++)
            {
                list.Add(Element("landscape.tree_deciduous", Pt((i % 70) * 7.0, (i / 70) * 7.0)));
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            ValidationReport r = new ProjectValidator(TestSupport.Catalog).Validate(list);
            sw.Stop();
            Assert.Equal(0, r.WarningCount);
            Assert.True(sw.ElapsedMilliseconds < 20000, "validation took " + sw.ElapsedMilliseconds + " ms");
        }
    }

    public class QuantityAndScheduleTests
    {
        [Fact]
        public void Quantities_CountLengthAreaAndVolume()
        {
            ElementType rack = TestSupport.Catalog.RequireType("cycling.bike_rack");
            Assert.Equal(5, QuantityCalculator.For(Element("cycling.bike_rack", Pt(0, 0)), rack).Value);
            ElementQuantity fence = QuantityCalculator.For(Element("wall.mesh_fence", Line(0, 0, 30, 0, 30, 40)), TestSupport.Catalog.RequireType("wall.mesh_fence"));
            Assert.Equal(70, fence.Value, 9);
            Assert.Equal("m", fence.Unit);
            ElementQuantity slab = QuantityCalculator.For(Element("surface.concrete_paving", Rect(0, 0, 10, 5)), TestSupport.Catalog.RequireType("surface.concrete_paving"));
            Assert.Equal(50, slab.Value, 9);
            Assert.Equal(4.0, slab.Secondary!.Value, 9);
            ElementQuantity pv = QuantityCalculator.For(Element("sustainability.solar_array", Pt(0, 0)), TestSupport.Catalog.RequireType("sustainability.solar_array"));
            Assert.Equal(20, pv.Value);
        }

        [Fact]
        public void Schedule_GroupsRowsAndKeepsElementReferences()
        {
            var trees = Enumerable.Range(1, 4).Select(i => Element("landscape.tree_deciduous", Pt(i * 10, 0), null, "T-00" + i)).ToList();
            trees[3].Parameters["height"] = ParamValue.Of(12);
            trees.ForEach(t => t.Properties["botanicalName"] = "Tilia cordata");
            ScheduleTable t = new ScheduleBuilder(TestSupport.Catalog).Build(TestSupport.Catalog.FindSchedule("planting")!, trees);
            Assert.Equal(2, t.Rows.Count);
            ScheduleRow big = t.Rows.Single(r => r.ElementIds.Count == 3);
            Assert.Equal("T-001..T-003", big.Cells[0]);
            Assert.Equal(4, t.Totals["pcs"]);
            string csv = t.ToCsv();
            Assert.Contains("ID elemenata", csv, StringComparison.Ordinal);
            Assert.Contains(trees[3].Id, csv, StringComparison.Ordinal);
        }

        [Fact]
        public void Csv_EscapesSeparatorsAndQuotes()
        {
            var e = Element("furniture.bench", Pt(0, 0), null, "FB-001");
            e.Properties["notes"] = "Seat \"A\", south";
            ScheduleTable t = new ScheduleBuilder(TestSupport.Catalog).Build(TestSupport.Catalog.FindSchedule("furniture")!, new[] { e });
            Assert.Contains("\"Seat \"\"A\"\", south\"", t.ToCsv(), StringComparison.Ordinal);
        }

        [Fact]
        public void CompactLabels_CompressesRuns()
        {
            Assert.Equal("L-001..L-004, L-007", ScheduleBuilder.CompactLabels(new[] { "L-003", "L-001", "L-002", "L-004", "L-007" }));
            Assert.Equal("A-1, A-2", ScheduleBuilder.CompactLabels(new[] { "A-2", "A-1" }));
        }
    }

    public class LayoutAndLabelTests
    {
        [Fact]
        public void Distribute_FixedAndFit()
        {
            var ring = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(100, 0, 0) });
            List<Placement> fixedP = PathPlacement.Distribute(ring, false, 30);
            Assert.Equal(new[] { 0.0, 30, 60, 90 }, fixedP.Select(p => Math.Round(p.Position.X, 9)));
            List<Placement> fit = PathPlacement.Distribute(ring, false, 30, 0, 0, SpacingMode.Fit);
            Assert.Equal(5, fit.Count);
            Assert.Equal(100, fit.Last().Position.X, 9);
            Assert.True(fit.Zip(fit.Skip(1), (a, b) => b.Position.X - a.Position.X).All(d => d <= 30 + 1e-9));
        }

        [Fact]
        public void Distribute_ClosedLoopDoesNotDuplicateStart()
        {
            var square = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(10, 10, 0), new Vec3(0, 10, 0) });
            Assert.Equal(4, PathPlacement.Distribute(square, true, 10).Count);
            Assert.Equal(8, PathPlacement.Distribute(square, true, 5, 0, 0, SpacingMode.Fit).Count);
        }

        [Fact]
        public void Distribute_LateralOffsetAndRotation()
        {
            var ring = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(50, 0, 0) });
            Placement p = PathPlacement.Distribute(ring, false, 25, 0, 0, SpacingMode.Fixed, 3.5, -Math.PI / 2)[0];
            Assert.Equal(3.5, p.Position.Y, 9);
            Assert.Equal(1.5 * Math.PI, p.Rotation, 9);
        }

        [Fact]
        public void Distribute_GuardsAgainstUnitMistakes()
        {
            var ring = new Ring(new[] { new Vec3(0, 0, 0), new Vec3(1000000, 0, 0) });
            Assert.Throws<InvalidOperationException>(() => PathPlacement.Distribute(ring, false, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PathPlacement.Distribute(ring, false, 0));
        }

        [Fact]
        public void ParkingRow_PerpendicularAndAngled()
        {
            List<Placement> row = ParkingLayout.Row(new Vec3(0, 0, 0), new Vec3(25, 0, 0), 2.5, true);
            Assert.Equal(10, row.Count);
            Assert.Equal(1.25, row[0].Position.X, 9);
            Assert.Equal(0, row[0].Rotation, 9); // bay extends +Y (left of the aisle direction)
            List<Placement> right = ParkingLayout.Row(new Vec3(0, 0, 0), new Vec3(25, 0, 0), 2.5, false);
            Assert.Equal(Math.PI, right[0].Rotation, 9);
            List<Placement> angled = ParkingLayout.Row(new Vec3(0, 0, 0), new Vec3(25, 0, 0), 2.5, true, 60);
            Assert.Equal(8, angled.Count); // pitch 2.5 / sin 60 = 2.887
        }

        [Fact]
        public void Grid_PointsStayInsideWithMargin()
        {
            var outline = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10) };
            List<Vec2> pts = GridLayout.InPolygon(outline, 2, 2);
            Assert.Equal(25, pts.Count);
            List<Vec2> withMargin = GridLayout.InPolygon(outline, 2, 2, 0, 1.5);
            Assert.Equal(9, withMargin.Count);
        }

        [Fact]
        public void Numbering_FormatsAndContinuesSequences()
        {
            var settings = new LabelSettings();
            var n = new LabelNumbering(settings, TestSupport.Catalog);
            ElementType tree = TestSupport.Catalog.RequireType("landscape.tree_deciduous");
            var existing = new[] { Element("landscape.tree_deciduous", Pt(0, 0), null, "T-007"), Element("landscape.tree_deciduous", Pt(0, 0), null, "X-100") };
            Assert.Equal("T-008", n.Next(tree, existing));
            settings.Zone = "A";
            Assert.Equal("T-A-001", n.Next(tree, existing));

            settings.Zone = string.Empty;
            var list = new List<SiteElement>
            {
                Element("landscape.tree_deciduous", Pt(10, 0)),
                Element("landscape.tree_deciduous", Pt(0, 50)),
                Element("landscape.tree_deciduous", Pt(0, 0)),
            };
            n.Assign(list, true, 3.0);
            Assert.Equal("T-001", list[1].Label); // northernmost first
            Assert.Equal("T-002", list[2].Label); // then west to east
            Assert.Equal("T-003", list[0].Label);
        }
    }
}
