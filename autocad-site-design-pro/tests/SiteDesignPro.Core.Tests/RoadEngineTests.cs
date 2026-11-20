using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>The headless road engine of the SketchUp road tools (sdp-ceste).</summary>
    public class RoadEngineTests
    {
        private static ElementCatalog Cat => TestSupport.Catalog;

        private static JsonObject Element(string type, JsonObject geometry, JsonObject? parameters = null, string? label = null)
        {
            string id = Guid.NewGuid().ToString("D");
            geometry["id"] = id;
            var o = new JsonObject { ["id"] = id, ["type"] = type, ["geometry"] = geometry, ["parameters"] = parameters ?? new JsonObject() };
            if (label != null)
            {
                o["label"] = label;
            }

            return o;
        }

        private static JsonObject Path(bool closed, params double[] xyz)
        {
            var v = new JsonArray();
            for (int i = 0; i + 2 < xyz.Length; i += 3)
            {
                v.Add(new JsonArray(xyz[i], xyz[i + 1], xyz[i + 2]));
            }

            return new JsonObject { ["kind"] = "path", ["vertices"] = v, ["closed"] = closed };
        }

        private static JsonObject Point(double x, double y) => new JsonObject { ["kind"] = "point", ["position"] = new JsonArray(x, y, 0.0), ["rotation"] = 0.0 };

        private static string Request(params JsonObject[] elements) => new JsonObject
        {
            ["format"] = RoadEngineRequest.FormatId,
            ["version"] = 1,
            ["project"] = new JsonObject { ["id"] = "7a0f5d3e-3c55-4d47-9a7e-1f0d8f2b9c01", ["name"] = "Test saobraćajnice" },
            ["base"] = 500.0,
            ["elements"] = new JsonArray(elements.Select(e => JsonNode.Parse(e.ToJsonString())).ToArray()),
        }.ToJsonString();

        [Fact]
        public void Request_IsRead_BadElementsAreReported_DefaultsFilled()
        {
            JsonObject street = Element(RoadElements.StreetType, Path(false, 0, 0, 0, 50, 0, 0), new JsonObject { ["lanesLeft"] = 2.0 }, "U-001");
            JsonObject unknown = Element("road.nonexistent", Point(0, 0));
            JsonObject wrongKind = Element(RoadElements.StreetType, Point(1, 1));
            RoadEngineRequest req = RoadEngine.ParseRequest(Request(street, unknown, wrongKind), Cat);
            Assert.Single(req.Elements);
            Assert.Equal(2, req.Problems.Count);
            Assert.Equal(500.0, req.BaseLevel);
            SiteElement e = req.Elements[0];
            Assert.Equal("U-001", e.Label);
            Assert.Equal(2.0, e.Parameters.Number("lanesLeft"));
            Assert.True(e.Parameters.Contains("laneWidth")); // filled from the catalog defaults
        }

        [Fact]
        public void AxisHeights_BecomeElevationMarks_AndTheAxisIsFlat()
        {
            JsonObject street = Element(RoadElements.StreetType, Path(false, 0, 0, 0, 50, 0, 2.5, 100, 0, 2.5));
            RoadEngineRequest req = RoadEngine.ParseRequest(Request(street), Cat);
            List<SiteElement> prepared = RoadEngine.Prepare(req.Elements, Cat);
            List<SiteElement> marks = prepared.Where(x => x.TypeId == RoadElements.LevelMarkType).ToList();
            Assert.Equal(3, marks.Count);
            Assert.Equal(new[] { 0.0, 2.5, 2.5 }, marks.Select(m => m.Parameters.Number("level")).ToArray());
            Assert.All(((PathGeometry)prepared.First(x => x.TypeId == RoadElements.StreetType).Geometry).Ring.Vertices, v => Assert.Equal(0, v.Z));

            // the same request again gives the same ids (the import only updates what changed)
            List<SiteElement> again = RoadEngine.Prepare(RoadEngine.ParseRequest(Request(street), Cat).Elements, Cat);
            Assert.Equal(marks.Select(m => m.Id), again.Where(x => x.TypeId == RoadElements.LevelMarkType).Select(m => m.Id));

            // a flat axis carries no marks
            JsonObject flat = Element(RoadElements.StreetType, Path(false, 0, 0, 0, 50, 0, 0));
            Assert.DoesNotContain(RoadEngine.Prepare(RoadEngine.ParseRequest(Request(flat), Cat).Elements, Cat), x => x.TypeId == RoadElements.LevelMarkType);
        }

        [Fact]
        public void Build_WritesAPackageWithTheNetwork_AndTheResult()
        {
            JsonObject a = Element(RoadElements.StreetType, Path(false, -60, 0, 0, 60, 0, 0), null, "U-001");
            JsonObject b = Element(RoadElements.StreetType, Path(false, 0, 0, 0, 0, 60, 3.0), null, "U-002");
            JsonObject crossing = Element(RoadElements.CrossingType, new JsonObject
            {
                ["kind"] = "polygon",
                ["outer"] = new JsonObject { ["vertices"] = new JsonArray(new JsonArray(-30.0, -3.0, 0.0), new JsonArray(-26.0, -3.0, 0.0), new JsonArray(-26.0, 3.0, 0.0), new JsonArray(-30.0, 3.0, 0.0)) },
            });
            string dir = TestSupport.TempDir();
            RoadEngineRequest req = RoadEngine.ParseRequest(Request(a, b, crossing), Cat);
            JsonObject result = RoadEngine.Build(req, dir, Cat, "test");
            RoadEngine.WriteResult(dir, result);

            Assert.True(result["ok"]!.GetValue<bool>());
            Assert.Equal(2, result["roads"]!.GetValue<int>());
            Assert.Equal(1, result["others"]!.GetValue<int>());
            Assert.True(result["areas"]!["carriageway"]!.GetValue<double>() > 100);
            Assert.True(File.Exists(System.IO.Path.Combine(dir, RoadEngine.ResultFile)));

            PackageReadResult read = PackageReader.Read(dir);
            Assert.NotNull(read.Package);
            Assert.False(read.Report.HasErrors, read.Report.ToString());
            List<string> types = read.Package!.Elements.Select(x => x.Element.TypeId).ToList();
            Assert.Contains("road.carriageway", types);
            Assert.Contains("road.sidewalk", types);
            Assert.Contains("road.street_kerb", types);
            Assert.Contains(RoadElements.LevelMarkType, types); // from the heights of U-002
            Assert.Equal("7a0f5d3e-3c55-4d47-9a7e-1f0d8f2b9c01", read.Package.ProjectId);

            // building again gives the same generated ids and hashes (nothing to update in SketchUp)
            string dir2 = TestSupport.TempDir();
            RoadEngine.Build(RoadEngine.ParseRequest(Request(a, b, crossing), Cat), dir2, Cat, "test");
            PackageReadResult read2 = PackageReader.Read(dir2);
            Assert.Equal(read.Package.Elements.Select(x => x.Element.Id + x.ContentHash).OrderBy(x => x), read2.Package!.Elements.Select(x => x.Element.Id + x.ContentHash).OrderBy(x => x));
        }

        [Fact]
        public void Export_WritesTheSourcesForAutoCad_GeneratedAreFlagged_InTheProjectFrame()
        {
            JsonObject a = Element(RoadElements.StreetType, Path(false, -60, 0, 0, 60, 0, 0), null, "U-001");
            JsonObject b = Element(RoadElements.StreetType, Path(false, 0, 0, 0, 0, 60, 3.0), null, "U-002");

            // any element of the library placed in SketchUp goes along, at its height
            JsonObject bench = Element("furniture.bench", new JsonObject { ["kind"] = "point", ["position"] = new JsonArray(12.0, 7.0, 0.45), ["rotation"] = 0.3 }, null, "K-001");
            JsonObject o = JsonNode.Parse(Request(a, b, bench))!.AsObject();
            o["origin"] = new JsonArray(6500000.0, 4800000.0, 0.0);
            RoadEngineRequest req = RoadEngine.ParseRequest(o.ToJsonString(), Cat);
            Assert.Equal(6500000.0, req.Origin[0]);
            string file = System.IO.Path.Combine(TestSupport.TempDir(), "saobracajnice.sdpkg");
            JsonObject result = RoadEngine.Export(req, file, Cat, "test");
            Assert.True(result["ok"]!.GetValue<bool>());
            Assert.Equal(2, result["roads"]!.GetValue<int>());
            Assert.True(File.Exists(file));

            PackageReadResult read = PackageReader.Read(file);
            Assert.NotNull(read.Package);
            Assert.False(read.Report.HasErrors, read.Report.ToString());
            SitePackage pkg = read.Package!;
            Assert.Equal(RoadEngine.Host, pkg.GeneratorHost);
            Assert.Equal("7a0f5d3e-3c55-4d47-9a7e-1f0d8f2b9c01", pkg.ProjectId);
            Assert.Equal(6500000.0, pkg.Origin[0]);

            List<SiteElement> sources = pkg.Elements.Select(x => x.Element).Where(x => RoadElements.SourceTypes.Contains(x.TypeId) && !(x.Custom.Contains(RoadElements.DerivedFlag) && x.Custom[RoadElements.DerivedFlag].AsBool())).ToList();
            Assert.Equal(2, sources.Count(x => x.TypeId == RoadElements.StreetType));
            Assert.Contains(sources, x => x.TypeId == RoadElements.LevelMarkType); // heights of U-002
            Assert.Contains(sources, x => x.Label == "U-001");
            SiteElement kb = pkg.Elements.Select(x => x.Element).Single(x => x.TypeId == "furniture.bench");
            Assert.Equal("K-001", kb.Label);
            Assert.Equal(0.45, ((PointGeometry)kb.Geometry).Position.Z, 6);
            Assert.Equal(0.3, ((PointGeometry)kb.Geometry).Rotation, 6);

            // everything else (surfaces, kerbs, markings) is generated: AutoCAD leaves it out and draws its own network
            List<SiteElement> generated = pkg.Elements.Select(x => x.Element).Except(sources).Where(x => x.TypeId != "furniture.bench").ToList();
            Assert.NotEmpty(generated);
            Assert.All(generated, x => Assert.True(RoadElements.NetworkOnlyTypes.Contains(x.TypeId) || (x.Custom.Contains(RoadElements.DerivedFlag) && x.Custom[RoadElements.DerivedFlag].AsBool()), x.TypeId));
            Assert.Contains(generated, x => x.TypeId == "road.carriageway");
        }

        [Fact]
        public void Roundabout_PartsAreTheAutoCadRoundabout_AndBuildWithoutErrors()
        {
            string options = new JsonObject
            {
                ["centre"] = new JsonArray(100.0, 50.0, 0.0),
                ["islandRadius"] = 40.0, // clamped to 30
                ["apron"] = 1.2,
                ["arms"] = 4,
                ["armLength"] = 40.0,
                ["seed"] = "test",
                ["arm"] = new JsonObject { ["lanesLeft"] = 1.0, ["lanesRight"] = 1.0, ["laneWidth"] = 3.25, ["sidewalkLeft"] = 2.0, ["sidewalkRight"] = 2.0 },
            }.ToJsonString();
            JsonObject parts = RoadEngine.RoundaboutParts(options, Cat);
            Assert.Equal(30.0, parts["islandRadius"]!.GetValue<double>());
            Assert.NotEmpty(parts["notes"]!.AsArray());
            JsonArray elements = parts["elements"]!.AsArray();
            List<string> roles = elements.Select(x => x!["role"]!.GetValue<string>()).ToList();
            Assert.Equal(1, roles.Count(r => r == "ring"));
            Assert.Equal(4, roles.Count(r => r == "arm"));
            Assert.Equal(4, roles.Count(r => r == "crossing"));
            Assert.Equal(8, roles.Count(r => r == "splitter"));
            Assert.Contains("apron", roles);
            Assert.Contains("island", roles);

            // the arms carry the median that ends 15 m from the ring
            JsonObject arm = elements.First(x => x!["role"]!.GetValue<string>() == "arm")!.AsObject();
            Assert.Equal(Roundabout.SplitterMedian, arm["parameters"]!["median"]!.GetValue<double>());
            Assert.True(arm["parameters"]!["medianTo"]!.GetValue<double>() > 0);

            // the same seed gives the same ids
            JsonArray again = RoadEngine.RoundaboutParts(options, Cat)["elements"]!.AsArray();
            Assert.Equal(elements.Select(x => x!["id"]!.GetValue<string>()), again.Select(x => x!["id"]!.GetValue<string>()));

            string request = new JsonObject
            {
                ["format"] = RoadEngineRequest.FormatId,
                ["elements"] = JsonNode.Parse(elements.ToJsonString()),
            }.ToJsonString();
            string dir = TestSupport.TempDir();
            JsonObject result = RoadEngine.Build(RoadEngine.ParseRequest(request, Cat), dir, Cat);
            Assert.True(result["ok"]!.GetValue<bool>());
            Assert.Equal(5, result["roads"]!.GetValue<int>());
            PackageReadResult read = PackageReader.Read(dir);
            Assert.False(read.Report.HasErrors);
            Assert.Contains(read.Package!.Elements, x => x.Element.TypeId == "road.carriageway");
        }

        [Fact]
        public void Roundabout_IsTheSameInAutoCadAndSketchUp()
        {
            var o = new RoundaboutOptions { Centre = Vec2.Zero, IslandRadius = 15, Apron = 1.2, Arms = 3, ArmLength = 40, Arm = new RoadSpec() };
            Roundabout.Normalize(o);
            List<RoundaboutPart> parts = Roundabout.Create(o);
            double ro = Roundabout.OuterRadius(o);
            Assert.Equal(15 + 1.2 + 0.15 + 3.0, ro, 6);
            RoundaboutPart ring = parts.Single(p => p.Role == "ring");
            Assert.Equal("one_way", ring.Parameters["direction"].AsText());
            Assert.True(((PathGeometry)ring.Geometry).Closed);
            Assert.All(parts.Where(p => p.Role == "arm"), p => Assert.Equal(ro - 0.5, ((PathGeometry)p.Geometry).Ring.Vertices[0].XY.Length, 6));
            Assert.Equal(3, parts.Count(p => p.Role == "triangle"));
        }

        [Fact]
        public void GarageRamp_ResultCarriesTheTextsTheSectionAndTheWarnings()
        {
            JsonObject street = Element(RoadElements.StreetType, Path(false, -40, 0, 0, 40, 0, 0), null, "U-001");
            JsonObject ramp = Element(GarageRamp.Type, Path(false, 0, -5.5, 0, 0, -20.5, 0), new JsonObject
            {
                ["kind"] = "straight",
                ["direction"] = "one_way",
                ["laneWidth"] = 3.0,
                ["margin"] = 0.25,
                ["startLevel"] = 0.0,
                ["endLevel"] = -3.0,
                ["width"] = 3.5,
            }, "GR-001");
            JsonObject section = Element(RoadElements.RampSectionType, Point(20, -30), new JsonObject { ["reference"] = ramp["id"]!.GetValue<string>() });
            string dir = TestSupport.TempDir();
            JsonObject result = RoadEngine.Build(RoadEngine.ParseRequest(Request(street, ramp, section), Cat), dir, Cat);
            JsonArray ramps = result["ramps"]!.AsArray();
            Assert.Single(ramps);
            Assert.StartsWith("L=15", ramps[0]!["text"]!.GetValue<string>());
            Assert.Equal(0.2, ramps[0]!["slope"]!.GetValue<double>(), 3);
            Assert.Contains(result["warnings"]!.AsArray(), w => w!.GetValue<string>().Contains("17 %"));
            JsonArray sections = result["sections"]!.AsArray();
            Assert.Single(sections);
            Assert.NotEmpty(sections[0]!["shapes"]!.AsArray());
            PackageReadResult read = PackageReader.Read(dir);
            Assert.Contains(read.Package!.Elements, x => x.Element.TypeId == GarageRamp.SurfaceType);
        }
    }
}
