using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clipper2Lib;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>
    /// Rules every road network must keep, whatever was drawn: the surfaces do not overlap, leave no thin seams between
    /// them, nothing of the road lies on a garage ramp, the markings stay on the carriageway, no slivers, and the 3D
    /// elements are finite and at sensible heights. Each broken rule is returned as one line.
    /// </summary>
    internal static class RoadInvariants
    {
        private const int Precision = 4;

        public static List<string> Check(SiteElement[] elements, RoadNetworkResult res, ElementCatalog cat)
        {
            var issues = new List<string>();
            var layers = new List<(string Name, PathsD Paths)>();
            foreach (RoadLevel level in res.Bands.Keys)
            {
                layers.Add((level.ToString(), Regions(res.Bands[level])));
            }

            layers.Add(("BikeRamps", Regions(res.BikeRamps)));
            layers.Add(("KerbRamps", Regions(res.KerbRamps)));
            layers.Add(("KerbCutRamps", new PathsD(res.Ramps.Select(Loop))));

            // 1. no two surfaces overlap
            for (int i = 0; i < layers.Count; i++)
            {
                for (int j = i + 1; j < layers.Count; j++)
                {
                    PathsD both = Clipper.Intersect(layers[i].Paths, layers[j].Paths, FillRule.NonZero, Precision);
                    double a = Area(both);
                    if (a > 0.02)
                    {
                        issues.Add("overlap " + layers[i].Name + "/" + layers[j].Name + " " + F(a) + " m2 at " + At(both));
                    }
                }
            }

            // within one layer the regions do not overlap either
            foreach ((string name, PathsD paths) in layers)
            {
                double sum = paths.Sum(p => Clipper.Area(p));
                double union = Area(Clipper.Union(paths, FillRule.NonZero));
                if (sum - union > 0.02)
                {
                    issues.Add("self-overlap " + name + " " + F(sum - union) + " m2");
                }
            }

            // 2. no thin seams (narrower than 6 cm) between the surfaces
            PathsD all = Clipper.Union(new PathsD(layers.SelectMany(l => l.Paths)), FillRule.NonZero);
            PathsD closed = Clipper.InflatePaths(Clipper.InflatePaths(all, 0.03, JoinType.Miter, EndType.Polygon, 3, Precision), -0.03, JoinType.Miter, EndType.Polygon, 3, Precision);
            PathsD seams = Clipper.Difference(closed, all, FillRule.NonZero, Precision);
            seams = new PathsD(seams.Where(p => Math.Abs(Clipper.Area(p)) > 0.005));
            if (Area(seams) > 0.02)
            {
                issues.Add("seam " + F(Area(seams)) + " m2 at " + At(seams));
            }

            // 3. slivers: tiny pieces or pieces thinner than 3 cm on average
            foreach ((string name, PathsD paths) in layers)
            {
                foreach (PathD p in Clipper.Union(paths, FillRule.NonZero))
                {
                    double a = Math.Abs(Clipper.Area(p));
                    if (a < 1e-6)
                    {
                        continue;
                    }

                    double per = Perimeter(p);
                    if (a < 0.01 || a / per < 0.015)
                    {
                        issues.Add("sliver " + name + " " + F(a) + " m2 (width ~" + F(2 * a / per) + " m) at " + At(new PathsD { p }));
                    }
                }
            }

            // 4. nothing of the road lies on a garage ramp
            foreach (GarageRamp ramp in GarageRamp.All(elements))
            {
                RampShape? surface = ramp.Plan().FirstOrDefault(x => x.Role == "surface");
                if (surface == null)
                {
                    continue;
                }

                var outline = new PathsD { Tess(surface.Points, surface.Bulges) };
                foreach ((string name, PathsD paths) in layers)
                {
                    double a = Area(Clipper.Intersect(paths, outline, FillRule.NonZero, Precision));
                    if (a > 0.02)
                    {
                        issues.Add("on ramp " + ramp.Name + ": " + name + " " + F(a) + " m2");
                    }
                }
            }

            // 5. markings and arrows on the carriageway (or the lowered bike lane)
            PathsD drive = Clipper.Union(new PathsD(layers.Where(l => l.Name == "Carriageway" || l.Name == "BikeRamps").SelectMany(l => l.Paths)), FillRule.NonZero);
            PathsD driveTol = Clipper.InflatePaths(drive, 0.2, JoinType.Round, EndType.Polygon, 2, Precision);
            foreach (RoadMarking m in res.Markings)
            {
                foreach (Vec2 p in m.Points)
                {
                    if (!InAny(driveTol, p))
                    {
                        issues.Add("marking off the carriageway at " + V(p));
                        break;
                    }
                }
            }

            foreach (RoadArrow ar in res.Arrows)
            {
                if (!InAny(driveTol, ar.Position))
                {
                    issues.Add("arrow off the carriageway at " + V(ar.Position));
                }
            }

            // 6. the 3D elements: finite, closed outlines and heights within a sensible range
            List<SiteElement> derived;
            try
            {
                derived = RoadElements.Derived(elements, cat);
            }
            catch (Exception ex)
            {
                issues.Add("3D failed: " + ex.GetType().Name + ": " + ex.Message);
                return issues;
            }

            foreach (SiteElement e in derived)
            {
                if (e.Geometry is PolygonGeometry pg && pg.Outer.Vertices.Any(v => !Finite(v)))
                {
                    issues.Add("3D " + e.TypeId + " has a point that is not a number");
                }

                ParamValue tv = e.Custom["triangles"];
                if (!tv.IsNull)
                {
                    string text = tv.AsText();
                    foreach (string tri in text.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] pts = tri.Split(';');
                        if (pts.Length != 3)
                        {
                            issues.Add("3D " + e.TypeId + " triangle with " + pts.Length + " points");
                            break;
                        }

                        double[] z = pts.Select(q => double.Parse(q.Split(',')[2], CultureInfo.InvariantCulture)).ToArray();
                        if (z.Any(x => double.IsNaN(x) || double.IsInfinity(x) || x < -50 || x > 50))
                        {
                            issues.Add("3D " + e.TypeId + " height out of range " + string.Join(",", z.Select(F)));
                            break;
                        }
                    }
                }
            }

            return issues;
        }

        private static bool Finite(Vec3 v) => !double.IsNaN(v.X) && !double.IsNaN(v.Y) && !double.IsNaN(v.Z) && !double.IsInfinity(v.X) && !double.IsInfinity(v.Y);

        private static bool InAny(PathsD paths, Vec2 p)
        {
            // even-odd over all rings: inside an outer ring and not inside its hole
            int inside = 0;
            foreach (PathD path in paths)
            {
                if (Clipper.PointInPolygon(new PointD(p.X, p.Y), path) != PointInPolygonResult.IsOutside)
                {
                    inside += Clipper.IsPositive(path) ? 1 : -1;
                }
            }

            return inside > 0;
        }

        public static PathsD Regions(IEnumerable<RoadRegion> regions)
        {
            var list = new PathsD();
            foreach (RoadRegion r in regions)
            {
                var one = new PathsD { Loop(r.Outer) };
                foreach (RoadLoop h in r.Holes)
                {
                    one = Clipper.Difference(one, new PathsD { Loop(h) }, FillRule.NonZero, Precision);
                }

                list.AddRange(one);
            }

            return list;
        }

        private static PathD Loop(RoadLoop l) => Tess(l.Points, l.Bulges);

        private static PathD Tess(IReadOnlyList<Vec2> pts, IReadOnlyList<double> bulges)
        {
            List<Vec3> t = GeometryMath.Tessellate(new Ring(pts.Select(p => p.WithZ(0)).ToList(), bulges.ToList()), true, 0.005);
            var path = new PathD(t.Select(v => new PointD(v.X, v.Y)));
            return Clipper.IsPositive(path) ? path : new PathD(path.AsEnumerable().Reverse());
        }

        private static double Area(PathsD p) => Math.Abs(p.Sum(x => Clipper.Area(x)));

        private static double Perimeter(PathD p)
        {
            double s = 0;
            for (int i = 0; i < p.Count; i++)
            {
                PointD a = p[i], b = p[(i + 1) % p.Count];
                s += Math.Sqrt(((a.x - b.x) * (a.x - b.x)) + ((a.y - b.y) * (a.y - b.y)));
            }

            return s;
        }

        private static string At(PathsD p)
        {
            PathD? big = p.OrderByDescending(x => Math.Abs(Clipper.Area(x))).FirstOrDefault();
            return big == null || big.Count == 0 ? "?" : "(" + F(big.Average(v => v.x)) + "; " + F(big.Average(v => v.y)) + ")";
        }

        private static string V(Vec2 p) => "(" + F(p.X) + "; " + F(p.Y) + ")";

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
