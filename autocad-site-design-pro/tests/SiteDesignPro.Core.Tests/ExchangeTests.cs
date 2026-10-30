using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Samples;
using SiteDesignPro.Core.Validation;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class ExchangeTests
    {
        private static ProjectSettings MillimetreSettings() => new ProjectSettings
        {
            Name = "Exchange test",
            DrawingUnit = "mm",
            Origin = new[] { 6550000000.0, 4860000000.0, 520000.0 },
        };

        private static List<SiteElement> DrawingElements()
        {
            const double ox = 6550000000.0, oy = 4860000000.0, oz = 520000.0;
            SiteElement tree = TestSupport.Element("landscape.tree_deciduous",
                new PointGeometry(new Vec3(ox + 12500, oy + 7250, oz + 150), Math.PI / 6), p => p["height"] = ParamValue.Of(9.5), "T-001");
            tree.Properties["botanicalName"] = "Acer campestre";
            SiteElement fence = TestSupport.Element("wall.mesh_fence",
                new PathGeometry(new Ring(new[] { new Vec3(ox, oy, oz), new Vec3(ox + 10000, oy, oz), new Vec3(ox + 10000, oy + 5000, oz + 500) }, new[] { 0.0, 0.4 }), false));
            SiteElement paving = TestSupport.Element("surface.concrete_paving",
                new PolygonGeometry(new Ring(new[] { new Vec3(ox, oy, oz + 150), new Vec3(ox + 8000, oy, oz + 150), new Vec3(ox + 8000, oy + 6000, oz + 150), new Vec3(ox, oy + 6000, oz + 150) }),
                    new[] { new Ring(new[] { new Vec3(ox + 1000, oy + 1000, oz + 150), new Vec3(ox + 2000, oy + 1000, oz + 150), new Vec3(ox + 2000, oy + 2000, oz + 150), new Vec3(ox + 1000, oy + 2000, oz + 150) }) }));
            paving.Custom["phase"] = ParamValue.Of("2");
            return new List<SiteElement> { tree, fence, paving };
        }

        [Theory]
        [InlineData("pkg.sdpkg")]
        [InlineData("pkg-folder")]
        public void RoundTrip_PreservesIdsDataUnitsOriginRotationAndElevation(string name)
        {
            string path = Path.Combine(TestSupport.TempDir(), name);
            ProjectSettings settings = MillimetreSettings();
            List<SiteElement> source = DrawingElements();
            PackageWriteResult w = PackageWriter.Write(path, settings, source, TestSupport.Catalog);
            Assert.Equal(3, w.ElementCount);
            Assert.Empty(w.Skipped);

            PackageReadResult r = PackageReader.Read(path);
            Assert.True(r.Success, r.Report.ToText());
            SitePackage pkg = r.Package!;
            Assert.Equal("mm", pkg.SourceLengthUnit);
            Assert.Equal(0.001, pkg.SourceUnitsToMeters);
            Assert.Equal(new[] { 6550000.0, 4860000.0, 520.0 }, pkg.Origin);
            Assert.Equal(source.Select(e => e.Id), pkg.Elements.Select(e => e.Element.Id));

            var tree = (PointGeometry)pkg.Elements[0].Element.Geometry;
            Assert.Equal(12.5, tree.Position.X, 9);
            Assert.Equal(7.25, tree.Position.Y, 9);
            Assert.Equal(0.15, tree.Position.Z, 9);
            Assert.Equal(Math.PI / 6, tree.Rotation, 9);
            Assert.Equal(9.5, pkg.Elements[0].Element.Parameters.Number("height"));
            Assert.Equal("Acer campestre", pkg.Elements[0].Element.Properties["botanicalName"]);
            Assert.Equal("T-001", pkg.Elements[0].Element.Label);
            Assert.Equal("VU-ZELENILO-DRVECE", pkg.Elements[0].Layer);
            Assert.Equal("planting", pkg.Elements[0].ModelGroup);

            var fence = (PathGeometry)pkg.Elements[1].Element.Geometry;
            Assert.Equal(0.4, fence.Ring.BulgeAt(1));
            Assert.Equal(0.5, fence.Ring.Vertices[2].Z, 9);
            var paving = (PolygonGeometry)pkg.Elements[2].Element.Geometry;
            Assert.Single(paving.Holes);
            Assert.Equal(47.0, GeometryMath.Area(paving), 9);
            Assert.Equal("2", pkg.Elements[2].Element.Custom.Text("phase"));
            Assert.Equal(ContentHasher.Hash(PackageWriter.ToPackageFrame(source[2], settings)), pkg.Elements[2].ContentHash);
        }

        [Fact]
        public void Import_IntoEmptyDrawing_RestoresUnitOriginDatumAndDrawingGeometry()
        {
            string path = Path.Combine(TestSupport.TempDir(), "import.sdpkg");
            ProjectSettings settings = MillimetreSettings();
            settings.Elevations.BuildingZeroAbsolute = 520.15;
            settings.Elevations.VerticalDatum = "test";
            settings.Elevations.DrawingZ = Core.Elevation.DrawingZMode.Relative;
            List<SiteElement> source = DrawingElements();
            PackageWriter.Write(path, settings, source, TestSupport.Catalog);
            SitePackage pkg = PackageReader.Read(path).Package!;

            var target = new ProjectSettings { DrawingUnit = "m" };
            PackageImport.AdoptProjectFrame(pkg, target);
            Assert.Equal("mm", target.DrawingUnit);
            Assert.Equal(settings.Origin, target.Origin);
            Assert.Equal(settings.ProjectId, target.ProjectId);
            Assert.Equal(520.15, target.Elevations.BuildingZeroAbsolute);
            Assert.Equal(Core.Elevation.DrawingZMode.Relative, target.Elevations.DrawingZ);

            for (int i = 0; i < source.Count; i++)
            {
                SiteElement back = PackageImport.ToDrawingFrame(pkg.Elements[i].Element, target);
                Assert.Equal(source[i].Id, back.Id);
                Assert.Equal(source[i].Label, back.Label);
                List<Vec3> a = Vertices(source[i].Geometry), b = Vertices(back.Geometry);
                Assert.Equal(a.Count, b.Count);
                for (int j = 0; j < a.Count; j++)
                {
                    Assert.True((a[j] - b[j]).Length < 1e-3, $"{source[i].TypeId} vrh {j}: {a[j]} != {b[j]}");
                }
            }

            Assert.Equal(Math.PI / 6, ((PointGeometry)PackageImport.ToDrawingFrame(pkg.Elements[0].Element, target).Geometry).Rotation, 9);
        }

        private static List<Vec3> Vertices(ElementGeometry g) => g switch
        {
            PointGeometry p => new List<Vec3> { p.Position },
            PathGeometry p => p.Ring.Vertices.ToList(),
            PolygonGeometry p => p.Outer.Vertices.Concat(p.Holes.SelectMany(h => h.Vertices)).ToList(),
            _ => new List<Vec3>(),
        };

        [Fact]
        public void ContentHash_IsStableAndSensitive()
        {
            SiteElement e = DrawingElements()[0];
            string h1 = ContentHasher.Hash(e);
            Assert.Equal(h1, ContentHasher.Hash(e));
            e.Parameters["height"] = ParamValue.Of(9.6);
            Assert.NotEqual(h1, ContentHasher.Hash(e));
            e.Parameters["height"] = ParamValue.Of(9.5);
            Assert.Equal(h1, ContentHasher.Hash(e));
            e.Geometry = new PointGeometry(((PointGeometry)e.Geometry).Position + new Vec3(0.001, 0, 0), ((PointGeometry)e.Geometry).Rotation);
            Assert.NotEqual(h1, ContentHasher.Hash(e));
        }

        [Fact]
        public void Writer_SkipsUnknownTypesDuplicatesAndWrongGeometry()
        {
            var settings = new ProjectSettings { DrawingUnit = "m" };
            SiteElement good = TestSupport.Element("furniture.bench", TestSupport.Pt(1, 1));
            SiteElement dup = TestSupport.Element("furniture.bench", TestSupport.Pt(5, 1));
            dup.Id = good.Id;
            SiteElement unknown = new SiteElement(SiteElement.NewId(), "nope.type", TestSupport.Pt(0, 0));
            SiteElement wrong = TestSupport.Element("surface.lawn", TestSupport.Pt(0, 0));
            PackageWriteResult w = PackageWriter.Write(Path.Combine(TestSupport.TempDir(), "x.sdpkg"), settings, new[] { good, dup, unknown, wrong }, TestSupport.Catalog);
            Assert.Equal(1, w.ElementCount);
            Assert.Equal(3, w.Skipped.Count);
        }

        [Fact]
        public void Reader_RejectsForeignFormatAndUnsupportedMajor()
        {
            (string m, string g) = Documents();
            JsonObject man = JsonNode.Parse(m)!.AsObject();
            man["format"] = "something-else";
            Assert.False(PackageReader.ReadFromStrings(man.ToJsonString(), g).Success);

            man = JsonNode.Parse(m)!.AsObject();
            man["schemaVersion"] = "2.0.0";
            PackageReadResult r = PackageReader.ReadFromStrings(man.ToJsonString(), g);
            Assert.Null(r.Package);
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-F001" && i.Message.Contains("Nepodržana verzija sheme", StringComparison.Ordinal));

            man = JsonNode.Parse(m)!.AsObject();
            man["schemaVersion"] = "1.7.0";
            r = PackageReader.ReadFromStrings(man.ToJsonString(), g);
            Assert.True(r.Success);
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-F002");
        }

        [Fact]
        public void Reader_ReportsDuplicateIdsMissingGeometryAndBadCoordinates()
        {
            (string m, string g) = Documents();
            JsonObject man = JsonNode.Parse(m)!.AsObject();
            JsonArray els = man["elements"]!.AsArray();
            els.Add(els[0]!.DeepClone());
            JsonObject extra = els[1]!.DeepClone().AsObject();
            extra["id"] = Guid.NewGuid().ToString("D");
            els.Add(extra);
            JsonObject geo = JsonNode.Parse(g)!.AsObject();
            geo["elements"]!.AsArray()[1]!["vertices"] = null;
            PackageReadResult r = PackageReader.ReadFromStrings(man.ToJsonString(), geo.ToJsonString());
            Assert.NotNull(r.Package);
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-D002");
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-F011");
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-G010");
        }

        [Fact]
        public void Reader_ReportsMissingAndUnsafeAssets()
        {
            (string m, string g) = Documents();
            JsonObject man = JsonNode.Parse(m)!.AsObject();
            man["assets"] = new JsonArray(
                new JsonObject { ["id"] = "tex1", ["path"] = "assets/textures/stone.jpg", ["kind"] = "texture" },
                new JsonObject { ["id"] = "evil", ["path"] = "assets/../../evil.skp", ["kind"] = "component" });
            string dir = TestSupport.TempDir();
            File.WriteAllText(Path.Combine(dir, "manifest.json"), man.ToJsonString());
            File.WriteAllText(Path.Combine(dir, "geometry.json"), g);
            PackageReadResult r = PackageReader.Read(dir);
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-F021");
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-F020");
        }

        [Fact]
        public void Reader_HandlesCorruptArchive()
        {
            string file = Path.Combine(TestSupport.TempDir(), "broken.sdpkg");
            File.WriteAllText(file, "not a zip");
            PackageReadResult r = PackageReader.Read(file);
            Assert.Null(r.Package);
            Assert.Contains(r.Report.Issues, i => i.Code == "SDP-F001");
        }

        [Fact]
        public void FolderWriter_RefusesToReplaceNonPackageFolder()
        {
            string dir = TestSupport.TempDir();
            File.WriteAllText(Path.Combine(dir, "important.txt"), "keep me");
            Assert.Throws<IOException>(() => PackageWriter.Write(dir, new ProjectSettings { DrawingUnit = "m" }, Array.Empty<SiteElement>(), TestSupport.Catalog));
            Assert.True(File.Exists(Path.Combine(dir, "important.txt")));
        }

        [Fact]
        public void SamplePackage_IsDeterministicAndClean()
        {
            string a = Path.Combine(TestSupport.TempDir(), "a.sdpkg");
            string b = Path.Combine(TestSupport.TempDir(), "b.sdpkg");
            foreach (string path in new[] { a, b })
            {
                (ProjectSettings s, List<SiteElement> els) = SampleResidentialComplex.CreateDrawing(TestSupport.Catalog);
                PackageWriter.Write(path, s, els, TestSupport.Catalog, new PackageWriteOptions { PackageId = SampleResidentialComplex.DeterministicId("package"), CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            }

            Assert.Equal(Entry(a, "manifest.json"), Entry(b, "manifest.json"));
            Assert.Equal(Entry(a, "geometry.json"), Entry(b, "geometry.json"));

            List<SiteElement> local = SampleResidentialComplex.CreateLocal(TestSupport.Catalog);
            ValidationReport report = new ProjectValidator(TestSupport.Catalog).Validate(local);
            Assert.Equal(0, report.ErrorCount);
            Assert.Equal(0, report.WarningCount);
            Assert.True(local.Count > 100);
            foreach (string required in new[] { "building.footprint", "road.access_road", "road.parking_bay", "surface.concrete_paving", "landscape.tree_deciduous",
                "landscape.flower_bed", "lighting.street_light", "furniture.bench", "cycling.bike_rack", "utility.waste_container_ug_5m3", "water.drainage_channel",
                "wall.mesh_fence", "structure.pergola" })
            {
                Assert.Contains(local, e => e.TypeId == required);
            }
        }

        private static string Entry(string zipPath, string name)
        {
            using ZipArchive z = ZipFile.OpenRead(zipPath);
            using var r = new StreamReader(z.GetEntry(name)!.Open());
            return r.ReadToEnd();
        }

        private static (string Manifest, string Geometry) Documents()
        {
            string dir = Path.Combine(TestSupport.TempDir(), "docs");
            PackageWriter.Write(dir, MillimetreSettings(), DrawingElements(), TestSupport.Catalog);
            return (File.ReadAllText(Path.Combine(dir, "manifest.json")), File.ReadAllText(Path.Combine(dir, "geometry.json")));
        }
    }
}
