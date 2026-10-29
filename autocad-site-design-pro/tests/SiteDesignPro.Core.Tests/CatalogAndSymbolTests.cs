using System;
using System.IO;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Symbols;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class CatalogTests
    {
        [Fact]
        public void EmbeddedCatalog_MatchesSharedFiles()
        {
            ElementCatalog fromDir = ElementCatalog.LoadFromDirectory(Path.Combine(TestSupport.SharedDir, "catalog"));
            ElementCatalog embedded = TestSupport.Catalog;
            Assert.Equal(fromDir.Types.Select(t => t.Id), embedded.Types.Select(t => t.Id));
            Assert.True(embedded.Types.Count() >= 250, "catalog should cover the full library");
            Assert.Equal(16, embedded.Categories.Count);
        }

        [Fact]
        public void EveryType_ResolvesLayerModelGroupScheduleAndDefaults()
        {
            ElementCatalog c = TestSupport.Catalog;
            foreach (ElementType t in c.Types)
            {
                Assert.NotNull(c.FindLayer(t.LayerKey));
                Assert.NotNull(c.FindModelGroup(t.ModelGroup));
                Assert.NotNull(c.FindSchedule(t.ScheduleId));
                Assert.NotNull(c.FindCategory(t.Category));
                foreach (ParameterDefinition p in t.Parameters.Where(p => p.Type == ParameterType.Material && !p.Default.IsNull))
                {
                    Assert.True(c.FindMaterial(p.Default.AsText()) != null, t.Id + "." + p.Key + " material " + p.Default.AsText());
                }

                foreach (ParameterDefinition p in t.Parameters.Where(p => p.Type == ParameterType.Enum))
                {
                    Assert.Contains(p.Default.AsText(), p.Options);
                }

                foreach (ParameterDefinition p in t.Parameters.Where(p => p.IsNumeric && !p.Default.IsNull))
                {
                    double d = p.Default.AsNumber();
                    Assert.True((!p.Min.HasValue || d >= p.Min) && (!p.Max.HasValue || d <= p.Max), t.Id + "." + p.Key + " default outside range");
                }
            }
        }

        [Fact]
        public void EveryPointType_HasA2DSymbolBuiltFromDefaults()
        {
            foreach (ElementType t in TestSupport.Catalog.Types.Where(t => t.Geometry == GeometryKind.Point))
            {
                Assert.True(SymbolLibrary.HasSymbol(t.Builder.Id), t.Builder.Id);
                SymbolDrawing d = SymbolLibrary.Build(t.Builder.Id, t.CreateDefaultParameters());
                Assert.NotEmpty(d.Items);
                Assert.False(d.Bounds().IsEmpty, t.Id);
            }
        }

        [Fact]
        public void UserOverlay_AddsTypeAndOverridesLayer()
        {
            string dir = TestSupport.TempDir();
            File.WriteAllText(Path.Combine(dir, "office.json"), @"{
              ""layers"": [ { ""key"": ""planting_trees"", ""name"": ""LA-TREES"", ""color"": 94 } ],
              ""types"": [ { ""id"": ""office.tree_avenue"", ""name"": ""Avenue tree (office standard)"", ""category"": ""landscape"", ""builder"": ""tree"",
                             ""layer"": ""planting_trees"", ""labelPrefix"": ""AT"", ""schedule"": ""planting"", ""defaults"": { ""height"": 12, ""canopyDiameter"": 7 } } ]
            }");
            File.WriteAllText(Path.Combine(dir, "broken.json"), @"{ ""types"": [ { ""id"": ""x.y"", ""builder"": ""does_not_exist"" } ] }");
            ElementCatalog c = ElementCatalog.LoadWithOverlays(null, dir);
            ElementType t = c.RequireType("office.tree_avenue");
            Assert.Equal(12, t.Parameter("height")!.Default.AsNumber());
            Assert.Equal("office.json", t.Source);
            Assert.Equal("LA-TREES", c.FindLayer("planting_trees")!.Name);
            Assert.Equal("Continuous", c.FindLayer("planting_trees")!.Linetype); // unspecified fields keep core values
            Assert.Contains(c.LoadMessages, m => m.Contains("broken.json", StringComparison.Ordinal));
        }

        [Fact]
        public void UnknownDefaultParameter_IsRejected()
        {
            ElementCatalog c = ElementCatalog.LoadEmbedded();
            Assert.Throws<FormatException>(() => c.ApplyOverlay(@"{ ""types"": [ { ""id"": ""a.b"", ""category"": ""landscape"", ""builder"": ""tree"", ""defaults"": { ""heigth"": 3 } } ] }", "typo.json"));
        }
    }

    public class SymbolTests
    {
        [Fact]
        public void TreeSymbol_MatchesCanopyDiameter()
        {
            var p = TestSupport.Catalog.RequireType("landscape.tree_deciduous").CreateDefaultParameters();
            p["canopyDiameter"] = ParamValue.Of(6.0);
            Box2 b = SymbolLibrary.Build("tree", p).Bounds();
            Assert.Equal(6.0, b.Width, 6);
            Assert.Equal(6.0, b.Height, 6);
        }

        [Fact]
        public void ParkingBay_ExtendsAlongLocalY()
        {
            var p = TestSupport.Catalog.RequireType("road.parking_bay").CreateDefaultParameters();
            Box2 b = SymbolLibrary.Build("parking_bay", p).Bounds();
            Assert.Equal(-1.25, b.MinX, 6);
            Assert.Equal(1.25, b.MaxX, 6);
            Assert.Equal(0, b.MinY, 6);
            Assert.Equal(5, b.MaxY, 6);
        }

        [Fact]
        public void EqualParameters_ShareBlock_DifferentParametersDoNot()
        {
            var t = TestSupport.Catalog.RequireType("furniture.bench");
            SymbolDrawing a = SymbolLibrary.Build("bench", t.CreateDefaultParameters());
            SymbolDrawing b = SymbolLibrary.Build("bench", t.CreateDefaultParameters());
            var p = t.CreateDefaultParameters();
            p["length"] = ParamValue.Of(2.4);
            SymbolDrawing c = SymbolLibrary.Build("bench", p);
            Assert.Equal(a.BlockName("mm"), b.BlockName("mm"));
            Assert.NotEqual(a.BlockName("mm"), c.BlockName("mm"));
            Assert.NotEqual(a.BlockName("mm"), a.BlockName("m"));
            Assert.Matches("^SDP_bench_mm_[0-9a-f]{10}$", a.BlockName("mm"));
        }
    }
}
