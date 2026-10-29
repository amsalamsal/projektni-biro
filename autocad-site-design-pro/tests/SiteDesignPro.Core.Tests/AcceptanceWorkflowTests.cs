using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Elevation;
using SiteDesignPro.Core.Geo;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Input;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Samples;
using SiteDesignPro.Core.Terrain;
using SiteDesignPro.Core.Units;
using SiteDesignPro.Core.Validation;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>
    /// The host-independent part of the acceptance workflow on the synthetic source drawing: mapping, input
    /// validation, datum, label → object elevation assignment, terrain, georeference and settings persistence.
    /// </summary>
    public class AcceptanceWorkflowTests
    {
        private const double K = 0.001; // millimetre drawing

        private static ProjectSettings Settings() => SampleResidentialComplex.CreateSettings();

        [Fact]
        public void SourceDrawing_ValidatesWithoutErrors_UnderTheSampleMapping()
        {
            ProjectSettings s = Settings();
            var input = new InputSettings { DrawingUnit = LengthUnit.Millimeter, Crs = s.Crs, Datum = s.Elevations };
            (ValidationReport r, InputSummary sum) = InputValidator.Validate(SampleSourceDrawing.Create(), SampleSourceDrawing.Mapping(), input);
            Assert.False(r.HasErrors, string.Join("\n", r.Issues));
            Assert.Equal(1, sum.ParcelCount);
            Assert.Equal(2, sum.BuildingCount);
            Assert.Equal(5, sum.UsableElevationLabels);
            Assert.True(sum.TerrainPointsWithZ > 100);
            Assert.Equal(9600, sum.ParcelAreaM2, 6);
            Assert.Contains("Gauss-Krüger", sum.CoordinateNote, StringComparison.Ordinal);
        }

        [Fact]
        public void SameDrawing_WithoutMappingOrDatum_ReportsWhatIsMissing()
        {
            var input = new InputSettings { DrawingUnit = LengthUnit.Millimeter };
            (ValidationReport r, _) = InputValidator.Validate(SampleSourceDrawing.Create(), new LayerMapping(), input);
            Assert.True(r.HasErrors);
            Assert.Contains(r.Issues, i => i.Code == "SDP-I010");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I047");
            Assert.Contains(r.Issues, i => i.Code == "SDP-I060");
        }

        [Fact]
        public void LayerNameSuggestion_FindsTheSampleRoles()
        {
            LayerMapping m = LayerMapping.Suggest(SampleSourceDrawing.Create().Select(o => o.Layer).Distinct());
            Assert.Contains(SampleSourceDrawing.ParcelLayer, m.Patterns(SourceRole.ParcelBoundary));
            Assert.Contains(SampleSourceDrawing.BuildingLayer, m.Patterns(SourceRole.BuildingFootprint));
            Assert.Contains(SampleSourceDrawing.ParterreLayer, m.Patterns(SourceRole.ParterreLine));
            Assert.Contains(SampleSourceDrawing.LabelLayer, m.Patterns(SourceRole.ElevationLabels));
        }

        [Fact]
        public void Labels_AreAssignedOnlyToTheirOwnObjects()
        {
            List<SourceObject> objs = SampleSourceDrawing.Create();
            LayerMapping map = SampleSourceDrawing.Mapping();
            List<AssignmentTarget> targets = SiteWorkflow.Targets(objs, map, K);
            Assert.Equal(4, targets.Count); // two buildings, road, parterre (the parcel never receives a label)
            List<AssignmentProposal> p = ElevationAssigner.Propose(SiteWorkflow.Labels(objs, map, K), targets, Settings().Elevations);
            Assert.All(p, x => Assert.Equal(ProposalStatus.Proposed, x.Status));
            Assert.Equal(2, p.Count(x => x.Target!.Description.StartsWith("Tlocrt objekta", StringComparison.Ordinal) && x.RelativeElevation == 0));
            AssignmentProposal parterre = p.Single(x => x.Label.Text == "+0,00");
            Assert.StartsWith("Linija partera", parterre.Target!.Description, StringComparison.Ordinal);
            List<AssignmentProposal> road = p.Where(x => x.Target!.Description.StartsWith("Saobraćajnice", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, road.Count); // "-0,15" and "KT 520,00" agree: 520.00 = 520.15 - 0.15
            Assert.All(road, x => Assert.Equal(-0.15, x.RelativeElevation!.Value, 9));
        }

        [Fact]
        public void Terrain_FromSurveyLayers_MatchesTheSurveyedSurface()
        {
            ProjectSettings s = Settings();
            (TerrainSources src, List<string> notes) = SiteWorkflow.TerrainSourcesFrom(SampleSourceDrawing.Create(), SampleSourceDrawing.Mapping(), s.Elevations, K, false);
            Assert.Empty(notes);
            TerrainModel m = TerrainModel.Build(src, s.Terrain);
            Assert.True(m.IsUsable);
            Vec3 o = SampleResidentialComplex.OriginMeters;
            // a survey grid node: (60, 50) relative to the origin
            TerrainSources local = SampleResidentialComplex.CreateTerrainSources();
            Vec3 node = local.Points.First(p => p.X == 60 && p.Y == 50);
            Assert.Equal(o.Z + node.Z, m.ElevationAt(new Vec2(o.X + 60, o.Y + 50))!.Value, 6);
            // the breakline along the road edge is honoured
            Assert.Equal(o.Z + 0.35 - (0.004 * 45), m.ElevationAt(new Vec2(o.X + 45, o.Y + 30))!.Value, 6);
        }

        [Fact]
        public void Terrain_FromLabelsAlone_NeedsADatumForRelativeLabels()
        {
            List<SourceObject> labelsOnly = SampleSourceDrawing.Create().Where(x => x.Layer == SampleSourceDrawing.LabelLayer).ToList();
            (TerrainSources withDatum, List<string> n1) = SiteWorkflow.TerrainSourcesFrom(labelsOnly, SampleSourceDrawing.Mapping(), Settings().Elevations, K, true);
            Assert.Equal(5, withDatum.Points.Count);
            Assert.Contains(withDatum.Points, p => Math.Abs(p.Z - 520.15) < 1e-9);

            (TerrainSources noDatum, List<string> n2) = SiteWorkflow.TerrainSourcesFrom(labelsOnly, SampleSourceDrawing.Mapping(), new ElevationDatum(), K, true);
            Assert.Single(noDatum.Points); // only the absolute "KT 520,00" can be placed without a datum
            Assert.Contains(n2, n => n.Contains("preskočeno 4", StringComparison.Ordinal));
        }

        [Fact]
        public void DrawingAlreadyInGaussKrueger_GivesKmlCoordinatesWithoutControlPoints()
        {
            ProjectedCrs crs = CrsRegistry.Find(Settings().Crs)!;
            var identity = new PlanTransform();
            (double e, double n) = identity.Apply(6550000, 4860000);
            (double lon, double lat) = crs.ToWgs84Geographic(e, n);
            Assert.Equal(18.617202057, lon, 7);
            Assert.Equal(43.882785194, lat, 7);
        }

        [Fact]
        public void ProjectSettings_RoundTripMappingDatumGeoreferenceAndTerrain()
        {
            ProjectSettings s = Settings();
            s.LayerMapping = SampleSourceDrawing.Mapping();
            s.Georeference.ControlPoints.Add(new ControlPoint { Name = "Ugao A", LocalX = 1, LocalY = 2, Easting = 6550001, Northing = 4860002 });
            s.Georeference.Transform = new PlanTransform { Kind = TransformKind.Rigid, Tx = 10, Ty = 20, RotationDegrees = 1.5 };
            s.TerrainSource.Enabled = true;
            s.Elevations.RoadLevels.Add(new RoadLevel { Name = "Garaža", OffsetBelowParterre = 0.3 });
            ProjectSettings r = ProjectSettings.FromJson(s.ToJson());
            Assert.Equal(new[] { SampleSourceDrawing.ParcelLayer }, r.LayerMapping!.Patterns(SourceRole.ParcelBoundary));
            Assert.Equal(520.15, r.Elevations.BuildingZeroAbsolute!.Value, 9);
            Assert.Equal(0.15, r.Elevations.DefaultRoadOffsetBelowParterre!.Value, 9);
            Assert.Equal("Garaža", r.Elevations.RoadLevels.Single().Name);
            Assert.Equal(TransformKind.Rigid, r.Georeference.Transform.Kind);
            Assert.Equal("Ugao A", r.Georeference.ControlPoints.Single().Name);
            Assert.Equal("EPSG:3908", r.Crs);
            Assert.True(r.TerrainSource.Enabled);
            Assert.Contains("\"ParcelBoundary\"", s.ToJson(), StringComparison.Ordinal);
        }

        [Fact]
        public void Version1Settings_AreUpgradedWithoutInventingElevations()
        {
            const string v1 = "{\"settingsVersion\":1,\"projectId\":\"11111111-1111-4111-8111-111111111111\",\"name\":\"Stari\",\"drawingUnit\":\"mm\",\"origin\":[0,0,0],\"crs\":\"EPSG:31276\",\"heightDatum\":\"Trst\"}";
            ProjectSettings r = ProjectSettings.FromJson(v1);
            Assert.Equal(2, r.SettingsVersion);
            Assert.Equal("EPSG:31276", r.Georeference.Crs);
            Assert.Equal("Trst", r.Elevations.VerticalDatum);
            Assert.Null(r.Elevations.BuildingZeroAbsolute);
            Assert.Null(r.LayerMapping);
        }
    }
}

namespace SiteDesignPro.Core.Tests
{
    public class SiteChecksTests
    {
        private static (List<SiteDesignPro.Core.Model.SiteElement> Elements, SiteDesignPro.Core.Model.ProjectSettings Settings, SiteDesignPro.Core.Catalog.ElementCatalog Catalog) Sample()
        {
            SiteDesignPro.Core.Catalog.ElementCatalog cat = TestSupport.Catalog;
            return (SampleResidentialComplex.CreateLocal(cat), SampleResidentialComplex.CreateSettings(), cat);
        }

        [Fact]
        public void SampleProject_PassesSiteChecks()
        {
            var (els, s, cat) = Sample();
            ValidationReport r = SiteChecks.Run(els, cat, s);
            Assert.Empty(r.Issues);
            Assert.True(els.Count(e => e.Properties.ContainsKey(ElevationAssignment.PropMode)) >= 4, "the sample assigns elevations object by object");
        }

        [Fact]
        public void MovedObject_RoadAboveParterre_AndOutsideParcel_AreReported()
        {
            var (els, s, cat) = Sample();
            SiteDesignPro.Core.Model.SiteElement building = els.First(e => e.TypeId == "building.footprint");
            building.Geometry = building.Geometry.Transform(v => new Vec3(v.X, v.Y, v.Z + 0.5)); // moved up after assignment
            SiteDesignPro.Core.Model.SiteElement road = els.First(e => e.TypeId == "surface.asphalt");
            road.Properties[ElevationAssignment.PropRelative] = "0.2";
            SiteDesignPro.Core.Model.SiteElement bench = els.First(e => e.TypeId == "furniture.bench");
            bench.Geometry = new PointGeometry(new Vec3(130, 40, 0.15), 0);
            ValidationReport r = SiteChecks.Run(els, cat, s);
            Assert.Contains(r.Issues, i => i.Code == "SDP-E012" && i.ElementId == building.Id);
            Assert.Contains(r.Issues, i => i.Code == "SDP-E010" && i.ElementId == road.Id);
            Assert.Contains(r.Issues, i => i.Code == "SDP-C020" && i.ElementId == bench.Id);
        }

        [Fact]
        public void AssignedElevation_WithoutDatum_IsReported()
        {
            var (els, s, cat) = Sample();
            s.Elevations.BuildingZeroAbsolute = null;
            ValidationReport r = SiteChecks.Run(els, cat, s);
            Assert.Contains(r.Issues, i => i.Code == "SDP-E011");
        }
    }
}
