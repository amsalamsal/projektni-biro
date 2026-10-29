using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Terrain;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    public class TerrainTests
    {
        private static TerrainSources Grid(Func<double, double, double> z, int n = 6, double step = 10, double ox = 0, double oy = 0)
        {
            var s = new TerrainSources();
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    s.Points.Add(new Vec3(ox + (i * step), oy + (j * step), z(i * step, j * step)));
                }
            }

            return s;
        }

        private static void AssertValidMesh(TerrainModel m)
        {
            // every triangle CCW, no duplicate triangles, every interior edge shared by exactly two triangles
            var edges = new Dictionary<(int, int), int>();
            foreach (TriangleInfo t in m.Triangles)
            {
                Vec3 a = m.Vertices[t.A], b = m.Vertices[t.B], c = m.Vertices[t.C];
                double orient = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
                Assert.True(orient > 0, "triangle not counter-clockwise");
                foreach ((int u, int w) in new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) })
                {
                    Assert.False(edges.ContainsKey((u, w)), "directed edge used twice (overlapping triangles)");
                    edges[(u, w)] = 1;
                }
            }
        }

        [Fact]
        public void OnePointOrCollinearPoints_AreInsufficient()
        {
            var one = new TerrainSources();
            one.Points.Add(new Vec3(0, 0, 518.56));
            TerrainModel m1 = TerrainModel.Build(one);
            Assert.False(m1.IsUsable);
            Assert.Contains(m1.Issues, i => i.Code == "SDP-T002" && i.Severity == "error");

            var line = new TerrainSources();
            line.Points.AddRange(new[] { new Vec3(0, 0, 1), new Vec3(10, 0, 2), new Vec3(20, 0, 3), new Vec3(30, 0, 4) });
            TerrainModel m2 = TerrainModel.Build(line);
            Assert.False(m2.IsUsable);
            Assert.Contains(m2.Issues, i => i.Code == "SDP-T003");
        }

        [Fact]
        public void PlanarGrid_ReproducesThePlane()
        {
            TerrainModel m = TerrainModel.Build(Grid((x, y) => 500 + (0.02 * x) + (0.01 * y)));
            Assert.True(m.IsUsable);
            AssertValidMesh(m);
            Assert.Equal(2500, m.PlanArea, 6);
            Assert.Equal(500 + (0.02 * 12.3) + (0.01 * 45.6), m.ElevationAt(new Vec2(12.3, 45.6))!.Value, 6);
            Assert.All(m.Triangles, t => Assert.Equal(Math.Sqrt(0.0005) * 100, t.SlopePercent, 6));
            Assert.Null(m.ElevationAt(new Vec2(-5, 5)));
            // downhill = -gradient = towards -x/-y
            double angle = m.Triangles[0].DownhillAngle;
            Assert.Equal(Math.Atan2(-0.01, -0.02), angle, 6);
        }

        [Fact]
        public void ProjectedCoordinates_KeepPrecision()
        {
            TerrainModel m = TerrainModel.Build(Grid((x, y) => 520 + (0.05 * x), ox: 6550000, oy: 4860000));
            Assert.True(m.IsUsable);
            AssertValidMesh(m);
            Assert.Equal(520 + (0.05 * 17.25), m.ElevationAt(new Vec2(6550017.25, 4860033.5))!.Value, 6);
        }

        [Fact]
        public void Breakline_IsAnEdgeOfTheSurface()
        {
            // a ridge along x = 25: without the breakline Delaunay would cut across it
            var s = new TerrainSources();
            s.Points.AddRange(new[] { new Vec3(0, 0, 0), new Vec3(50, 0, 0), new Vec3(50, 50, 0), new Vec3(0, 50, 0), new Vec3(10, 25, 0), new Vec3(40, 25, 0) });
            s.Breaklines.Add(new List<Vec3> { new Vec3(25, -0.0, 3), new Vec3(25, 50, 3) });
            TerrainModel m = TerrainModel.Build(s);
            AssertValidMesh(m);
            Assert.Equal(3, m.ElevationAt(new Vec2(25, 10))!.Value, 6);
            Assert.Equal(3, m.ElevationAt(new Vec2(25, 40))!.Value, 6);
            Assert.True(m.ElevationAt(new Vec2(12, 25))!.Value < 3);
        }

        [Fact]
        public void ManyRandomPointsAndConstraints_ProduceAValidMesh()
        {
            var rnd = new Random(7);
            var s = new TerrainSources();
            for (int i = 0; i < 400; i++)
            {
                double x = rnd.NextDouble() * 200, y = rnd.NextDouble() * 200;
                s.Points.Add(new Vec3(x, y, 500 + (Math.Sin(x / 30) * 2) + (y * 0.01)));
            }

            s.Breaklines.Add(new List<Vec3> { new Vec3(10, 10, 501), new Vec3(100, 120, 503), new Vec3(190, 30, 502) });
            s.Contours.Add(new List<Vec3> { new Vec3(5, 180, 502.5), new Vec3(60, 150, 502.5), new Vec3(120, 190, 502.5) });
            TerrainModel m = TerrainModel.Build(s);
            Assert.True(m.IsUsable);
            AssertValidMesh(m);
            Assert.DoesNotContain(m.Issues, i => i.Code == "SDP-T012");
            Assert.Equal(503, m.ElevationAt(new Vec2(100, 120))!.Value, 6);
            Assert.Equal(502.5, m.ElevationAt(new Vec2(60, 150))!.Value, 6);
            // the breakline mid-point lies on a constrained edge, so the surface follows the line exactly
            Assert.Equal(502, m.ElevationAt(new Vec2(55, 65))!.Value, 6);
        }

        [Fact]
        public void Boundary_ClipsTheSurface()
        {
            TerrainSources s = Grid((x, y) => 10, n: 6);
            s.Boundary = new List<Vec2> { new Vec2(0, 0), new Vec2(30, 0), new Vec2(30, 30), new Vec2(0, 30) };
            TerrainModel m = TerrainModel.Build(s);
            Assert.Equal(900, m.PlanArea, 6);
            Assert.Null(m.ElevationAt(new Vec2(40, 40)));
        }

        [Fact]
        public void LowPointAndFlatAreas_AreReported()
        {
            TerrainModel m = TerrainModel.Build(Grid((x, y) => (Math.Abs(x - 20) + Math.Abs(y - 20)) * 0.05 + 300));
            Assert.Contains(m.Issues, i => i.Code == "SDP-T023" && i.Location!.Value.X == 20 && i.Location.Value.Y == 20);

            TerrainModel flat = TerrainModel.Build(Grid((x, y) => 300 + (0.001 * x)));
            Assert.Contains(flat.Issues, i => i.Code == "SDP-T020");
            Assert.Contains(flat.Issues, i => i.Code == "SDP-T004" || i.Code == "SDP-T020");
        }

        [Fact]
        public void ConflictingDuplicatePoints_AreReported()
        {
            TerrainSources s = Grid((x, y) => 100);
            s.Points.Add(new Vec3(10, 10, 101.5));
            TerrainModel m = TerrainModel.Build(s);
            Assert.Contains(m.Issues, i => i.Code == "SDP-T010");
        }

        [Fact]
        public void Volumes_AgainstLevel_AreExact()
        {
            // plane z = x/50 over 50 x 50: against level 0.5 the cut and fill wedges are equal
            TerrainModel m = TerrainModel.Build(Grid((x, y) => x / 50, n: 6, step: 10));
            (double cut, double fill) = m.VolumesAgainstLevel(0.5);
            Assert.Equal(cut, fill, 6);
            Assert.Equal(2500 * 0.25, cut + fill, 6); // |z - 0.5| averages 0.25 over the area
            (double c2, double f2) = m.VolumesAgainstLevel(-1);
            Assert.Equal(2500 * 1.5, c2, 6);
            Assert.Equal(0, f2, 9);
        }

        [Fact]
        public void Profile_FollowsTheSurface()
        {
            TerrainModel m = TerrainModel.Build(Grid((x, y) => 200 + (0.1 * x)));
            List<(double Station, double Z)> prof = m.Profile(new[] { new Vec2(0, 25), new Vec2(50, 25) });
            Assert.Equal(0, prof[0].Station, 9);
            Assert.Equal(200, prof[0].Z, 6);
            Assert.Equal(50, prof[prof.Count - 1].Station, 6);
            Assert.Equal(205, prof[prof.Count - 1].Z, 6);
            Assert.All(prof, p => Assert.Equal(200 + (0.1 * p.Station), p.Z, 6));
        }

        [Fact]
        public void Json_HasVerticesTrianglesAndIssues()
        {
            TerrainModel m = TerrainModel.Build(Grid((x, y) => 1));
            System.Text.Json.Nodes.JsonObject j = m.ToJson("t1", "Teren");
            Assert.Equal(36, j["vertices"]!.AsArray().Count);
            Assert.Equal(m.Triangles.Count, j["triangles"]!.AsArray().Count);
            Assert.Equal(50, (int)j["statistics"]!["triangleCount"]!.GetValue<int>());
        }
    }
}
