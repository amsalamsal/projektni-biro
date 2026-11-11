using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;
using SiteDesignPro.Core.Roads;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>
    /// Builds a set of junction situations the way the AutoCAD add-in does (bends rounded into the axis, then the
    /// network) and checks them; with SDP_ROAD_RENDER set, each one is also written as an SVG for looking at.
    /// </summary>
    public class RoadRenderTests
    {
        private static ElementCatalog Cat => TestSupport.Catalog;

        private static SiteElement S(string type, double[] xy, params (string Key, ParamValue Value)[] p)
        {
            ElementType t = Cat.RequireType(type);
            var pts = new List<Vec3>();
            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                pts.Add(new Vec3(xy[i], xy[i + 1], 0));
            }

            bool closed = pts.Count > 3 && pts[0].XY.DistanceTo(pts[pts.Count - 1].XY) < 1e-6;
            if (closed)
            {
                pts.RemoveAt(pts.Count - 1);
            }

            var e = new SiteElement(Guid.NewGuid().ToString(), type, new PathGeometry(new Ring(pts), closed)) { Parameters = t.CreateDefaultParameters(), Label = "U" };
            foreach ((string key, ParamValue value) in p)
            {
                e.Parameters[key] = value;
            }

            // like the add-in: the bends of the drawn axis are rounded into the axis itself
            RoadSpec? spec = RoadElements.Spec(e, t);
            if (spec != null)
            {
                (List<Vec2> Points, List<double> Bulges)? r = RoadElements.RoundedAxis(pts.Select(q => q.XY).ToList(), pts.Select(_ => 0.0).ToList(), closed, spec);
                if (r != null)
                {
                    e.Geometry = new PathGeometry(new Ring(r.Value.Points.Select(q => q.WithZ(0)).ToList(), r.Value.Bulges), closed);
                }
            }

            return e;
        }

        private static SiteElement St(params double[] xy) => S(RoadElements.StreetType, xy);

        private static SiteElement St((string Key, ParamValue Value)[] p, params double[] xy) => S(RoadElements.StreetType, xy, p);

        private static (string Key, ParamValue Value)[] P(params (string Key, ParamValue Value)[] p) => p;

        private static (string, ParamValue) N(string key, double v) => (key, ParamValue.Of(v));

        private static SiteElement Ring(double r, params (string Key, ParamValue Value)[] p)
        {
            ElementType t = Cat.RequireType(RoadElements.StreetType);
            var circle = new Ring(new List<Vec3> { new Vec3(r, 0, 0), new Vec3(-r, 0, 0) }, new List<double> { 1, 1 });
            var e = new SiteElement(Guid.NewGuid().ToString(), RoadElements.StreetType, new PathGeometry(circle, true)) { Parameters = t.CreateDefaultParameters(), Label = "K" };
            e.Parameters["direction"] = ParamValue.Of("one_way");
            e.Parameters["lanesRight"] = ParamValue.Of(1.0);
            e.Parameters["laneWidth"] = ParamValue.Of(6.0);
            e.Parameters["sidewalkLeft"] = ParamValue.Of(0.0);
            e.Parameters["arrows"] = ParamValue.Of(false);
            foreach ((string key, ParamValue value) in p)
            {
                e.Parameters[key] = value;
            }

            return e;
        }

        public static IEnumerable<object[]> Scenarios()
        {
            double c60 = Math.Cos(Math.PI / 3), s60 = Math.Sin(Math.PI / 3), c30 = Math.Cos(Math.PI / 6), s30 = Math.Sin(Math.PI / 6);
            var bike = P(N("bikeLeft", 1.5), N("bikeRight", 1.5));
            var wide = P(N("lanesLeft", 2), N("lanesRight", 2));
            var list = new List<(string, SiteElement[])>
            {
                ("01_T_do_ose", new[] { St(-60, 0, 60, 0), St(0, 0, 0, 50) }),
                ("02_T_do_ivice", new[] { St(-60, 0, 60, 0), St(0, 3, 0, 50) }),
                ("03_T_preko_ose", new[] { St(-60, 0, 60, 0), St(0, -1.5, 0, 50) }),
                ("04_T_kratko_do_trotoara", new[] { St(-60, 0, 60, 0), St(0, 4.9, 0, 50) }),
                ("05_raskrsnica_90", new[] { St(-60, 0, 60, 0), St(0, -50, 0, 50) }),
                ("06_raskrsnica_60", new[] { St(-60, 0, 60, 0), St(-50 * c60, -50 * s60, 50 * c60, 50 * s60) }),
                ("07_raskrsnica_30", new[] { St(-70, 0, 70, 0), St(-60 * c30, -60 * s30, 60 * c30, 60 * s30) }),
                ("08_T_koso_45", new[] { St(-60, 0, 60, 0), St(0, 0, 40, 40) }),
                ("09_Y_tri_kraka", new[] { St(0, 0, 50, 0), St(0, 0, -50 * c60, 50 * s60), St(0, 0, -50 * c60, -50 * s60) }),
                ("10_nastavak_ravno", new[] { St(-60, 0, 0, 0), St(0, 0, 60, 0) }),
                ("11_nastavak_pod_uglom", new[] { St(-60, 0, 0, 0), St(0, 0, 50, 30) }),
                ("12_nastavak_10_stepeni", new[] { St(-60, 0, 0, 0), St(0, 0, 60 * Math.Cos(0.1745), 60 * Math.Sin(0.1745)) }),
                ("13_ugao_L_dvije_ulice", new[] { St(-60, 0, 0, 0), St(0, 0, 0, 50) }),
                ("14_siroka_i_uska", new[] { St(wide, -60, 0, 60, 0), St(0, 0, 0, 50) }),
                ("15_staze_raskrsnica", new[] { St(bike, -60, 0, 60, 0), St(bike, 0, -50, 0, 50) }),
                ("16_staza_samo_glavna_T", new[] { St(bike, -60, 0, 60, 0), St(0, 0, 0, 50) }),
                ("17_staza_samo_sporedna_T", new[] { St(-60, 0, 60, 0), St(bike, 0, 0, 0, 50) }),
                ("18_trotoar_jedna_strana", new[] { St(P(N("sidewalkRight", 0)), -60, 0, 60, 0), St(P(N("sidewalkLeft", 0)), 0, -50, 0, 50) }),
                ("19_krivina_i_T_na_kosom", new[] { St(-60, 0, 0, 0, 40, 40, 100, 40), St(20, 20, 50, -10) }),
                ("20_T_odmah_iza_krivine", new[] { St(-60, 0, 0, 0, 40, 40), St(-12, 0, -12, -40) }),
                ("21_T_u_tjemenu_krivine", new[] { St(-60, 0, 0, 0, 40, 40), St(0, 0, 10, -45) }),
                ("22_dva_T_blizu", new[] { St(-60, 0, 60, 0), St(-7, 0, -7, 45), St(7, 0, 7, -45) }),
                ("23_dva_T_ista_strana_blizu", new[] { St(-60, 0, 60, 0), St(-8, 0, -8, 45), St(8, 0, 8, 45) }),
                ("24_paralelne_i_spojna", new[] { St(-60, 0, 60, 0), St(-60, 16, 60, 16), St(0, 0, 0, 16) }),
                ("25_paralelne_trotoari_se_dodiruju", new[] { St(-60, 0, 60, 0), St(-60, 9.5, 60, 9.5) }),
                ("26_kruzni_tok_4_kraka", new[] { Ring(17, N("sidewalkRight", 2)), St(20, 0, 60, 0), St(-20, 0, -60, 0), St(0, 20, 0, 60), St(0, -20, 0, -60) }),
                ("27_kruzni_tok_koso", new[] { Ring(17, N("sidewalkRight", 2)), St(20 * c30, 20 * s30, 60 * c30, 60 * s30), St(-20, 0, -60, 0), St(bike, 0, -20, 0, -60) }),
                ("28_zvijezda_5", Enumerable.Range(0, 5).Select(i => St(0, 0, 50 * Math.Cos(i * 2 * Math.PI / 5), 50 * Math.Sin(i * 2 * Math.PI / 5))).ToArray()),
                ("29_blok_zatvoren_i_prolaz", new[] { St(-40, -30, 40, -30, 40, 30, -40, 30, -40, -30), St(0, -30, 0, 30), St(40, 0, 90, 0) }),
                ("30_pristupna_cesta_T", new[] { St(-60, 0, 60, 0), S("road.access_road", new[] { 0.0, 0, 0, 40 }), S("road.driveway", new[] { 25.0, 0, 25, -30 }) }),
                ("31_kratki_krak", new[] { St(-60, 0, 60, 0), St(0, 0, 0, 9) }),
                ("32_jednosmjerna_u_dvosmjernu", new[] { St(-60, 0, 60, 0), St(P(("direction", ParamValue.Of("one_way"))), 0, 0, 0, 50) }),
                ("33_razdjelni_pojas", new[] { St(P(N("median", 2)), -60, 0, 60, 0), St(0, 0, 0, 50) }),
                ("34_veliki_radijus", new[] { St(P(N("cornerRadius", 12)), -60, 0, 60, 0), St(P(N("cornerRadius", 12)), 0, -50, 0, 50) }),
                ("35_razliciti_radijusi", new[] { St(P(N("cornerRadius", 3)), -60, 0, 60, 0), St(P(N("cornerRadius", 10)), 0, 0, 0, 50) }),
                ("36_S_krivina_i_raskrsnica", new[] { St(-80, 0, -30, 0, 0, 25, 40, 25, 70, 0, 110, 0), St(20, -40, 20, 60) }),
                ("37_ostar_zavoj", new[] { St(-60, 0, 0, 0, -40, 25) }),
                ("38_T_na_kraju_ulice", new[] { St(-60, 0, 0, 0), St(0, -40, 0, 40) }),
                ("39_kraj_blizu_ali_ne_dodiruje", new[] { St(-60, 0, 60, 0), St(0, 8, 0, 50) }),
                ("40_cetiri_kraka_iz_tacke", new[] { St(0, 0, 50, 0), St(0, 0, -50, 0), St(0, 0, 0, 50), St(0, 0, 0, -50) }),
            };
            return list.Select(x => new object[] { x.Item1, x.Item2 });
        }

        [Theory]
        [MemberData(nameof(Scenarios))]
        public void Scenario(string name, SiteElement[] elements)
        {
            RoadNetworkResult res = RoadElements.Network(elements, Cat)!;
            Assert.NotNull(res);
            var issues = new List<string>();
            foreach (RoadLevel level in res.Bands.Keys)
            {
                foreach (RoadRegion r in res.Bands[level])
                {
                    if (r.Area < 0.3)
                    {
                        issues.Add("sliver " + level + " " + r.Area.ToString("0.###", CultureInfo.InvariantCulture) + " m2 at " + Fmt(r.Outer.Points[0]));
                    }
                }
            }

            string? dir = Environment.GetEnvironmentVariable("SDP_ROAD_RENDER");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, name + ".svg"), Svg(name, elements, res, issues));
            }

            if (!string.IsNullOrEmpty(dir) && issues.Count > 0)
            {
                File.AppendAllText(Path.Combine(dir, "problemi.txt"), name + ": " + string.Join("; ", issues) + "\n");
            }
        }

        private static string Fmt(Vec2 p) => p.X.ToString("0.##", CultureInfo.InvariantCulture) + "," + p.Y.ToString("0.##", CultureInfo.InvariantCulture);

        private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string PathOf(RoadLoop l)
        {
            var sb = new StringBuilder();
            int n = l.Points.Count;
            sb.Append("M").Append(F(l.Points[0].X)).Append(' ').Append(F(l.Points[0].Y));
            for (int i = 0; i < n; i++)
            {
                Vec2 a = l.Points[i], b = l.Points[(i + 1) % n];
                double bulge = i < l.Bulges.Count ? l.Bulges[i] : 0;
                if (Math.Abs(bulge) < 1e-9)
                {
                    sb.Append('L').Append(F(b.X)).Append(' ').Append(F(b.Y));
                }
                else
                {
                    double r = a.DistanceTo(b) / 2 * (1 + (bulge * bulge)) / (2 * Math.Abs(bulge));
                    sb.Append('A').Append(F(r)).Append(' ').Append(F(r)).Append(" 0 ").Append(Math.Abs(bulge) > 1 ? 1 : 0).Append(' ').Append(bulge > 0 ? 1 : 0).Append(' ').Append(F(b.X)).Append(' ').Append(F(b.Y));
                }
            }

            return sb.Append('Z').ToString();
        }

        private static string Svg(string name, SiteElement[] elements, RoadNetworkResult res, List<string> issues)
        {
            var all = res.Bands.Values.SelectMany(b => b).SelectMany(r => r.Outer.Points).ToList();
            double x0 = all.Min(p => p.X) - 3, x1 = all.Max(p => p.X) + 3, y0 = all.Min(p => p.Y) - 3, y1 = all.Max(p => p.Y) + 3;
            var colors = new Dictionary<RoadLevel, string>
            {
                [RoadLevel.Edge] = "#e8d9a0", [RoadLevel.Sidewalk] = "#5b6470", [RoadLevel.Bike] = "#8a1030", [RoadLevel.Kerb] = "#f2f2f2", [RoadLevel.Carriageway] = "#a9a9a9",
            };
            var sb = new StringBuilder();
            sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='").Append(F(x0)).Append(' ').Append(F(-y1)).Append(' ').Append(F(x1 - x0)).Append(' ').Append(F(y1 - y0)).Append("'>");
            sb.Append("<rect x='").Append(F(x0)).Append("' y='").Append(F(-y1)).Append("' width='").Append(F(x1 - x0)).Append("' height='").Append(F(y1 - y0)).Append("' fill='#1d2530'/>");
            sb.Append("<g transform='scale(1,-1)'>");
            foreach (RoadLevel level in new[] { RoadLevel.Edge, RoadLevel.Sidewalk, RoadLevel.Bike, RoadLevel.Kerb, RoadLevel.Carriageway })
            {
                if (!res.Bands.TryGetValue(level, out List<RoadRegion>? regions))
                {
                    continue;
                }

                foreach (RoadRegion r in regions)
                {
                    sb.Append("<path fill-rule='evenodd' fill-opacity='0.8' stroke='#000' stroke-width='0.04' fill='").Append(colors[level]).Append("' d='").Append(PathOf(r.Outer));
                    foreach (RoadLoop h in r.Holes)
                    {
                        sb.Append(PathOf(h));
                    }

                    sb.Append("'/>");
                }
            }

            foreach (RoadMarking m in res.Markings)
            {
                sb.Append("<polyline fill='none' stroke='#fff' stroke-width='0.12'").Append(m.Centre ? string.Empty : " stroke-dasharray='3 3'").Append(" points='").Append(string.Join(" ", m.Points.Select(Fmt))).Append("'/>");
            }

            foreach (RoadArrow a in res.Arrows)
            {
                sb.Append("<polygon fill='#fff' points='").Append(string.Join(" ", a.Outline().Select(Fmt))).Append("'/>");
            }

            foreach (RoadLoop ramp in res.Ramps)
            {
                sb.Append("<polygon fill='#4a90e2' points='").Append(string.Join(" ", ramp.Points.Select(Fmt))).Append("'/>");
            }

            foreach (SiteElement e in elements)
            {
                if (e.Geometry is PathGeometry pg)
                {
                    List<Vec3> t = GeometryMath.Tessellate(pg.Ring, pg.Closed, 0.01, 256).ToList();
                    sb.Append("<polyline fill='none' stroke='#ff4040' stroke-width='0.08' stroke-dasharray='0.6 0.6' points='").Append(string.Join(" ", t.Select(v => Fmt(v.XY)))).Append(pg.Closed ? " " + Fmt(t[0].XY) : string.Empty).Append("'/>");
                }
            }

            sb.Append("</g>");
            sb.Append("<text x='").Append(F(x0 + 1)).Append("' y='").Append(F(-y1 + 3)).Append("' font-size='2.2' fill='#fff' font-family='sans-serif'>").Append(name).Append(issues.Count > 0 ? " !! " + string.Join("; ", issues) : string.Empty).Append("</text>");
            return sb.Append("</svg>").ToString();
        }
    }
}
