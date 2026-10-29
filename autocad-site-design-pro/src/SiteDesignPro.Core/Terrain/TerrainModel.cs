using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Terrain
{
    /// <summary>Input data for a terrain surface (metres, package frame or drawing frame - the caller decides).</summary>
    public sealed class TerrainSources
    {
        /// <summary>Survey / elevation points.</summary>
        public List<Vec3> Points { get; } = new List<Vec3>();

        /// <summary>Contour lines (all vertices at the contour elevation).</summary>
        public List<IReadOnlyList<Vec3>> Contours { get; } = new List<IReadOnlyList<Vec3>>();

        /// <summary>Breaklines (vertex elevations as given).</summary>
        public List<IReadOnlyList<Vec3>> Breaklines { get; } = new List<IReadOnlyList<Vec3>>();

        /// <summary>Optional outer limit of the surface (plan).</summary>
        public IReadOnlyList<Vec2>? Boundary { get; set; }

        public int SourceCount => Points.Count + Contours.Sum(c => c.Count) + Breaklines.Sum(b => b.Count);
    }

    /// <summary>Project rules for terrain checks (project-specific values, not regulations).</summary>
    public sealed class TerrainRules
    {
        /// <summary>Below this slope (%) water may stand (ponding risk).</summary>
        public double MinDrainageSlopePercent { get; set; } = 0.5;

        /// <summary>Above this slope (%) the terrain is flagged as steep.</summary>
        public double MaxSlopePercent { get; set; } = 33.0;

        /// <summary>Maximum distance (m) between source points before the data is called sparse.</summary>
        public double MaxPointSpacing { get; set; } = 25.0;

        /// <summary>Vertical tolerance (m) for low-point detection.</summary>
        public double LowPointTolerance { get; set; } = 0.005;
    }

    public sealed class TerrainIssue
    {
        public string Code { get; set; } = string.Empty;

        public string Severity { get; set; } = "warning";

        public string Message { get; set; } = string.Empty;

        public Vec3? Location { get; set; }
    }

    public sealed class TriangleInfo
    {
        public int A { get; set; }

        public int B { get; set; }

        public int C { get; set; }

        public double SlopePercent { get; set; }

        /// <summary>Downhill direction in plan, radians CCW from +X; NaN for flat triangles.</summary>
        public double DownhillAngle { get; set; }

        public double PlanArea { get; set; }
    }

    /// <summary>A triangulated terrain surface (TIN) with analysis results.</summary>
    public sealed class TerrainModel
    {
        public List<Vec3> Vertices { get; } = new List<Vec3>();

        public List<TriangleInfo> Triangles { get; } = new List<TriangleInfo>();

        public List<TerrainIssue> Issues { get; } = new List<TerrainIssue>();

        public List<IReadOnlyList<Vec3>> Breaklines { get; } = new List<IReadOnlyList<Vec3>>();

        public bool IsUsable => Triangles.Count > 0;

        public double MinZ => Vertices.Count == 0 ? double.NaN : Vertices.Min(v => v.Z);

        public double MaxZ => Vertices.Count == 0 ? double.NaN : Vertices.Max(v => v.Z);

        public double PlanArea => Triangles.Sum(t => t.PlanArea);

        /// <summary>Checks whether the sources can define a surface at all; Bosnian explanations.</summary>
        public static List<TerrainIssue> CheckSufficiency(TerrainSources s)
        {
            var issues = new List<TerrainIssue>();
            List<Vec3> all = s.Points.Concat(s.Contours.SelectMany(c => c)).Concat(s.Breaklines.SelectMany(b => b)).ToList();
            List<Vec3> valid = all.Where(p => Compat.IsFinite(p.Z)).ToList();
            if (valid.Count < all.Count)
            {
                issues.Add(new TerrainIssue { Code = "SDP-T001", Message = Bs.T("{0} izvornih tačaka nema visinu i ne koristi se.", all.Count - valid.Count) });
            }

            List<Vec2> distinct = valid.Select(p => p.XY).Distinct().ToList();
            if (distinct.Count < 3)
            {
                issues.Add(new TerrainIssue
                {
                    Code = "SDP-T002",
                    Severity = "error",
                    Message = Bs.T("Za teren su potrebne najmanje tri tačke s visinom koje ne leže na istom pravcu (pronađeno: {0}). Jedna kota ne određuje površinu terena: dodajte tačke snimka, izohipse ili prelomne linije.", distinct.Count),
                });
                return issues;
            }

            Vec2 p0 = distinct[0];
            Vec2 far = distinct.OrderByDescending(p => (p - p0).Length).First();
            double maxOff = distinct.Max(p => Math.Abs(Cross(far - p0, p - p0)) / Math.Max((far - p0).Length, 1e-12));
            if (maxOff < 0.01)
            {
                issues.Add(new TerrainIssue
                {
                    Code = "SDP-T003",
                    Severity = "error",
                    Message = Bs.T("Sve tačke leže na jednom pravcu; površina terena se ne može odrediti. Dodajte tačke s obje strane linije."),
                });
                return issues;
            }

            if (valid.Select(p => Math.Round(p.Z, 3)).Distinct().Count() == 1)
            {
                issues.Add(new TerrainIssue
                {
                    Code = "SDP-T004",
                    Message = Bs.T("Sve tačke imaju istu visinu ({0} m). Provjerite jesu li visine zaista učitane (npr. Z = 0 zbog crteža u 2D).", Bs.Num(valid[0].Z, 2)),
                });
            }

            if (s.Boundary != null && s.Boundary.Count >= 3)
            {
                double boundaryArea = Math.Abs(SignedArea(s.Boundary));
                double hullArea = Math.Abs(SignedArea(ConvexHull(distinct)));
                if (boundaryArea > 0 && hullArea / boundaryArea < 0.8)
                {
                    issues.Add(new TerrainIssue
                    {
                        Code = "SDP-T005",
                        Message = Bs.T("Izvorni podaci pokrivaju samo {0} % granice terena; dijelovi izvan su ekstrapolirani i nepouzdani.", Bs.Num(hullArea / boundaryArea * 100, 0)),
                    });
                }
            }

            return issues;
        }

        /// <summary>Builds the surface; never invents elevations when the sources are insufficient.</summary>
        public static TerrainModel Build(TerrainSources s, TerrainRules? rules = null)
        {
            rules ??= new TerrainRules();
            var model = new TerrainModel();
            model.Issues.AddRange(CheckSufficiency(s));
            if (model.Issues.Any(i => i.Severity == "error"))
            {
                return model;
            }

            var constraints = s.Contours.Concat(s.Breaklines).Select(l => (IReadOnlyList<Vec3>)l.Where(p => Compat.IsFinite(p.Z)).ToList()).Where(l => l.Count >= 2).ToList();
            Triangulation tin = Triangulation.Build(s.Points.Where(p => Compat.IsFinite(p.Z)), constraints, s.Boundary);
            model.Vertices.AddRange(tin.Vertices);
            model.Breaklines.AddRange(s.Breaklines);
            foreach ((Vec3 p, int kept, double dz) in tin.Duplicates.Where(d => Math.Abs(d.DeltaZ) > 0.01))
            {
                model.Issues.Add(new TerrainIssue
                {
                    Code = "SDP-T010",
                    Message = Bs.T("Dvije izvorne tačke na istom mjestu imaju različite visine ({0} i {1} m); zadržana je prva.", Bs.Num(model.Vertices[kept].Z, 2), Bs.Num(p.Z, 2)),
                    Location = p,
                });
            }

            foreach ((Vec3 a, Vec3 b) in tin.CrossingConstraints)
            {
                model.Issues.Add(new TerrainIssue { Code = "SDP-T011", Message = Bs.T("Prelomne linije ili izohipse se sijeku; provjerite izvorne linije."), Location = a });
            }

            foreach ((Vec3 a, Vec3 b) in tin.UnrecoveredConstraints)
            {
                model.Issues.Add(new TerrainIssue { Code = "SDP-T012", Message = Bs.T("Segment prelomne linije nije mogao biti ugrađen u mrežu trouglova."), Location = a });
            }

            foreach ((int a, int b, int c) in tin.Triangles)
            {
                model.Triangles.Add(Describe(model.Vertices, a, b, c));
            }

            model.Analyse(s, rules);
            return model;
        }

        private static TriangleInfo Describe(List<Vec3> v, int a, int b, int c)
        {
            Vec3 pa = v[a], pb = v[b], pc = v[c];
            double ux = pb.X - pa.X, uy = pb.Y - pa.Y, uz = pb.Z - pa.Z, wx = pc.X - pa.X, wy = pc.Y - pa.Y, wz = pc.Z - pa.Z;
            double nx = (uy * wz) - (uz * wy), ny = (uz * wx) - (ux * wz), nz = (ux * wy) - (uy * wx);
            double planArea = Math.Abs(nz) / 2;
            double gx = Math.Abs(nz) < 1e-12 ? 0 : -nx / nz, gy = Math.Abs(nz) < 1e-12 ? 0 : -ny / nz; // dz/dx, dz/dy
            double slope = Math.Sqrt((gx * gx) + (gy * gy)) * 100;
            return new TriangleInfo
            {
                A = a,
                B = b,
                C = c,
                PlanArea = planArea,
                SlopePercent = slope,
                DownhillAngle = slope < 1e-9 ? double.NaN : Math.Atan2(-gy, -gx),
            };
        }

        private void Analyse(TerrainSources s, TerrainRules rules)
        {
            if (Triangles.Count == 0)
            {
                return;
            }

            // flat areas (ponding risk) and steep areas, reported as totals
            double flat = Triangles.Where(t => t.SlopePercent < rules.MinDrainageSlopePercent).Sum(t => t.PlanArea);
            if (flat > 0.5)
            {
                Issues.Add(new TerrainIssue
                {
                    Code = "SDP-T020",
                    Message = Bs.T("{0} terena ima nagib manji od {1}; moguće je zadržavanje vode.", Bs.SquareMeters(flat, 1), Bs.Percent(rules.MinDrainageSlopePercent)),
                });
            }

            double steep = Triangles.Where(t => t.SlopePercent > rules.MaxSlopePercent).Sum(t => t.PlanArea);
            if (steep > 0.5)
            {
                Issues.Add(new TerrainIssue
                {
                    Code = "SDP-T021",
                    Message = Bs.T("{0} terena ima nagib veći od {1}.", Bs.SquareMeters(steep, 1), Bs.Percent(rules.MaxSlopePercent)),
                });
            }

            // contour artefacts: triangles whose three vertices lie on one contour elevation
            var contourZ = new HashSet<double>(s.Contours.Where(c => c.Count > 0).Select(c => Math.Round(c[0].Z, 3)));
            int flatContour = Triangles.Count(t => contourZ.Contains(Math.Round(Vertices[t.A].Z, 3)) && Vertices[t.A].Z == Vertices[t.B].Z && Vertices[t.B].Z == Vertices[t.C].Z);
            if (flatContour > 0)
            {
                Issues.Add(new TerrainIssue
                {
                    Code = "SDP-T022",
                    Message = Bs.T("{0} trouglova leži ravno na jednoj izohipsi (vrhovi, dna dolina); dodajte kote vrhova ili prelomne linije.", flatContour),
                });
            }

            // local minima at interior vertices
            foreach (Vec3 low in LowPoints(rules.LowPointTolerance))
            {
                Issues.Add(new TerrainIssue
                {
                    Code = "SDP-T023",
                    Message = Bs.T("Lokalna najniža tačka na {0} m: voda nema prirodan odvod. Predvidite slivnik ili izmijenite nagibe.", Bs.Num(low.Z, 2)),
                    Location = low,
                });
            }

            // sparse data
            double avgArea = PlanArea / Math.Max(1, Vertices.Count);
            double spacing = Math.Sqrt(avgArea * 2);
            if (spacing > rules.MaxPointSpacing)
            {
                Issues.Add(new TerrainIssue
                {
                    Code = "SDP-T024",
                    Message = Bs.T("Prosječni razmak izvornih tačaka je oko {0}; teren je između tačaka linearno interpoliran i može odstupati od stvarnog.", Bs.Meters(spacing, 0)),
                });
            }
        }

        /// <summary>Interior vertices lower than all their neighbours (hull vertices are excluded: water leaves the model there).</summary>
        public List<Vec3> LowPoints(double tolerance)
        {
            var neighbours = new Dictionary<int, HashSet<int>>();
            var edgeCount = new Dictionary<(int, int), int>();
            void Edge(int a, int b)
            {
                (int, int) k = a < b ? (a, b) : (b, a);
                edgeCount[k] = edgeCount.TryGetValue(k, out int n) ? n + 1 : 1;
                if (!neighbours.TryGetValue(a, out HashSet<int>? sa))
                {
                    neighbours[a] = sa = new HashSet<int>();
                }

                if (!neighbours.TryGetValue(b, out HashSet<int>? sb))
                {
                    neighbours[b] = sb = new HashSet<int>();
                }

                sa.Add(b);
                sb.Add(a);
            }

            foreach (TriangleInfo t in Triangles)
            {
                Edge(t.A, t.B);
                Edge(t.B, t.C);
                Edge(t.C, t.A);
            }

            var hull = new HashSet<int>(edgeCount.Where(kv => kv.Value == 1).SelectMany(kv => new[] { kv.Key.Item1, kv.Key.Item2 }));
            var result = new List<Vec3>();
            foreach (KeyValuePair<int, HashSet<int>> kv in neighbours)
            {
                if (hull.Contains(kv.Key))
                {
                    continue;
                }

                double z = Vertices[kv.Key].Z;
                if (kv.Value.All(n => Vertices[n].Z > z + tolerance))
                {
                    result.Add(Vertices[kv.Key]);
                }
            }

            return result;
        }

        /// <summary>Elevation at a plan point, or null outside the surface.</summary>
        public double? ElevationAt(Vec2 p)
        {
            foreach (TriangleInfo t in Triangles)
            {
                Vec3 a = Vertices[t.A], b = Vertices[t.B], c = Vertices[t.C];
                double det = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
                if (Math.Abs(det) < 1e-15)
                {
                    continue;
                }

                double l1 = (((b.X - p.X) * (c.Y - p.Y)) - ((b.Y - p.Y) * (c.X - p.X))) / det;
                double l2 = (((c.X - p.X) * (a.Y - p.Y)) - ((c.Y - p.Y) * (a.X - p.X))) / det;
                double l3 = 1 - l1 - l2;
                if (l1 >= -1e-9 && l2 >= -1e-9 && l3 >= -1e-9)
                {
                    return (l1 * a.Z) + (l2 * b.Z) + (l3 * c.Z);
                }
            }

            return null;
        }

        /// <summary>Terrain profile along a plan polyline: (station, elevation) at every triangle edge crossing.</summary>
        public List<(double Station, double Z)> Profile(IReadOnlyList<Vec2> line)
        {
            var result = new List<(double, double)>();
            double station = 0;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vec2 p = line[i], q = line[i + 1];
                double len = (q - p).Length;
                var hits = new List<(double T, double Z)>();
                foreach (TriangleInfo t in Triangles)
                {
                    foreach ((int u, int w) in new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) })
                    {
                        Vec3 a = Vertices[u], b = Vertices[w];
                        if (SegmentIntersection(p, q, a.XY, b.XY, out double tp, out double tq))
                        {
                            hits.Add((tp, a.Z + ((b.Z - a.Z) * tq)));
                        }
                    }
                }

                double? z0 = ElevationAt(p);
                if (z0.HasValue)
                {
                    hits.Add((0, z0.Value));
                }

                double? z1 = ElevationAt(q);
                if (z1.HasValue)
                {
                    hits.Add((1, z1.Value));
                }

                foreach ((double tt, double z) in hits.OrderBy(h => h.T))
                {
                    double st = station + (tt * len);
                    if (result.Count == 0 || Math.Abs(result[result.Count - 1].Item1 - st) > 1e-6)
                    {
                        result.Add((st, z));
                    }
                }

                station += len;
            }

            return result;
        }

        /// <summary>Volumes between the surface and a horizontal plane: cut = above the plane, fill = below (prism method).</summary>
        public (double Cut, double Fill) VolumesAgainstLevel(double level)
        {
            double cut = 0, fill = 0;
            foreach (TriangleInfo t in Triangles)
            {
                double[] h = { Vertices[t.A].Z - level, Vertices[t.B].Z - level, Vertices[t.C].Z - level };
                (double c, double f) = PrismSplit(t.PlanArea, h);
                cut += c;
                fill += f;
            }

            return (cut, fill);
        }

        /// <summary>Exact volume above/below zero of a linear triangle prism with corner heights h.</summary>
        private static (double Cut, double Fill) PrismSplit(double area, double[] h)
        {
            int pos = h.Count(x => x > 0), neg = h.Count(x => x < 0);
            double mean = h.Sum() / 3;
            if (neg == 0)
            {
                return (area * mean, 0);
            }

            if (pos == 0)
            {
                return (0, -area * mean);
            }

            // one vertex alone on its side: its sub-tetra volume is area * h^3 / (3 * (h - a)(h - b)) relative to that vertex
            double[] s = h.OrderBy(x => x).ToArray();
            if (pos == 1)
            {
                double hp = s[2];
                double v = area * hp * hp * hp / (3 * (hp - s[0]) * (hp - s[1]));
                return (v, v - (area * mean));
            }
            else
            {
                double hn = -s[0];
                double v = area * hn * hn * hn / (3 * (hn + s[1]) * (hn + s[2]));
                return ((area * mean) + v, v);
            }
        }

        private static bool SegmentIntersection(Vec2 p, Vec2 q, Vec2 a, Vec2 b, out double tp, out double tq)
        {
            tp = tq = 0;
            Vec2 r = q - p, s = b - a;
            double den = Cross(r, s);
            if (Math.Abs(den) < 1e-12)
            {
                return false;
            }

            tp = Cross(a - p, s) / den;
            tq = Cross(a - p, r) / den;
            return tp >= 0 && tp <= 1 && tq >= 0 && tq <= 1;
        }

        private static double Cross(Vec2 a, Vec2 b) => (a.X * b.Y) - (a.Y * b.X);

        private static double SignedArea(IReadOnlyList<Vec2> p)
        {
            double a = 0;
            for (int i = 0; i < p.Count; i++)
            {
                Vec2 u = p[i], w = p[(i + 1) % p.Count];
                a += (u.X * w.Y) - (w.X * u.Y);
            }

            return a / 2;
        }

        private static List<Vec2> ConvexHull(List<Vec2> pts)
        {
            List<Vec2> sorted = pts.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var lower = new List<Vec2>();
            var upper = new List<Vec2>();
            foreach (Vec2 p in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 1] - lower[lower.Count - 2], p - lower[lower.Count - 2]) <= 0)
                {
                    lower.RemoveAt(lower.Count - 1);
                }

                lower.Add(p);
            }

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                Vec2 p = sorted[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 1] - upper[upper.Count - 2], p - upper[upper.Count - 2]) <= 0)
                {
                    upper.RemoveAt(upper.Count - 1);
                }

                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            return lower.Concat(upper).ToList();
        }

        /// <summary>terrain.json content: vertices (m), triangles, breaklines and issues; read by the SketchUp importer.</summary>
        public JsonObject ToJson(string id, string name)
        {
            var verts = new JsonArray();
            foreach (Vec3 v in Vertices)
            {
                verts.Add(new JsonArray(R(v.X), R(v.Y), R(v.Z)));
            }

            var tris = new JsonArray();
            foreach (TriangleInfo t in Triangles)
            {
                tris.Add(new JsonArray(t.A, t.B, t.C));
            }

            var issues = new JsonArray();
            foreach (TerrainIssue i in Issues)
            {
                var o = new JsonObject { ["code"] = i.Code, ["severity"] = i.Severity, ["message"] = i.Message };
                if (i.Location.HasValue)
                {
                    o["location"] = new JsonArray(R(i.Location.Value.X), R(i.Location.Value.Y), R(i.Location.Value.Z));
                }

                issues.Add(o);
            }

            var breaklines = new JsonArray();
            foreach (IReadOnlyList<Vec3> b in Breaklines)
            {
                breaklines.Add(new JsonArray(b.Select(v => (JsonNode)new JsonArray(R(v.X), R(v.Y), R(v.Z))).ToArray()));
            }

            return new JsonObject
            {
                ["id"] = id,
                ["name"] = name,
                ["vertices"] = verts,
                ["triangles"] = tris,
                ["breaklines"] = breaklines,
                ["statistics"] = new JsonObject
                {
                    ["planArea"] = Math.Round(PlanArea, 3),
                    ["minZ"] = Vertices.Count > 0 ? R(MinZ) : 0,
                    ["maxZ"] = Vertices.Count > 0 ? R(MaxZ) : 0,
                    ["triangleCount"] = Triangles.Count,
                },
                ["issues"] = issues,
            };
        }

        private static double R(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

        public string Summary() => Bs.T("Teren: {0} tačaka, {1} trouglova, površina {2}, visine {3} – {4} m.",
            Vertices.Count, Triangles.Count, Bs.SquareMeters(PlanArea, 1), Bs.Num(MinZ, 2), Bs.Num(MaxZ, 2));
    }
}
