using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SiteDesignPro.Core.Exchange;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Symbols
{
    /// <summary>2D drawing primitive in the element's local frame (metres; insertion point at the origin).</summary>
    public abstract record SymbolPrimitive;

    public sealed record SymLine(Vec2 A, Vec2 B) : SymbolPrimitive;

    public sealed record SymCircle(Vec2 Center, double Radius) : SymbolPrimitive;

    /// <summary>Counter-clockwise arc from StartAngle to EndAngle (radians).</summary>
    public sealed record SymArc(Vec2 Center, double Radius, double StartAngle, double EndAngle) : SymbolPrimitive;

    public sealed record SymPolyline(IReadOnlyList<Vec2> Points, bool Closed, IReadOnlyList<double>? Bulges = null, double Width = 0) : SymbolPrimitive;

    /// <summary>Solid-filled closed region.</summary>
    public sealed record SymFill(IReadOnlyList<Vec2> Points) : SymbolPrimitive;

    /// <summary>Middle-centred text.</summary>
    public sealed record SymText(Vec2 Position, double Height, string Text) : SymbolPrimitive;

    /// <summary>Generated plan symbol for a point element.</summary>
    public sealed class SymbolDrawing
    {
        public SymbolDrawing(string symbol, List<SymbolPrimitive> items)
        {
            Symbol = symbol;
            Items = items;
            Hash = ComputeHash(symbol, items);
        }

        public string Symbol { get; }

        public List<SymbolPrimitive> Items { get; }

        /// <summary>Hash of the geometry; equal symbols share one block definition.</summary>
        public string Hash { get; }

        /// <summary>Block name for a drawing unit, e.g. SDP_tree_mm_3fa94c1d20.</summary>
        public string BlockName(string unitCode) => "SDP_" + Symbol + "_" + unitCode + "_" + Hash.Substring(0, 10);

        public Box2 Bounds()
        {
            Box2 b = Box2.Empty;
            foreach (SymbolPrimitive p in Items)
            {
                switch (p)
                {
                    case SymLine l:
                        b = b.Include(l.A).Include(l.B);
                        break;
                    case SymCircle c:
                        b = b.Include(c.Center - new Vec2(c.Radius, c.Radius)).Include(c.Center + new Vec2(c.Radius, c.Radius));
                        break;
                    case SymArc a:
                        b = b.Include(a.Center - new Vec2(a.Radius, a.Radius)).Include(a.Center + new Vec2(a.Radius, a.Radius));
                        break;
                    case SymPolyline pl:
                        foreach (Vec2 v in pl.Points)
                        {
                            b = b.Include(v);
                        }

                        break;
                    case SymFill f:
                        foreach (Vec2 v in f.Points)
                        {
                            b = b.Include(v);
                        }

                        break;
                    case SymText t:
                        b = b.Include(t.Position);
                        break;
                }
            }

            return b;
        }

        private static string ComputeHash(string symbol, List<SymbolPrimitive> items)
        {
            var sb = new StringBuilder(symbol);
            string N(double v) => GeometryJson.NumberText(Math.Round(v, 6));
            string V(Vec2 v) => N(v.X) + "," + N(v.Y);
            foreach (SymbolPrimitive p in items)
            {
                sb.Append('|');
                switch (p)
                {
                    case SymLine l: sb.Append("L").Append(V(l.A)).Append(';').Append(V(l.B)); break;
                    case SymCircle c: sb.Append("C").Append(V(c.Center)).Append(';').Append(N(c.Radius)); break;
                    case SymArc a: sb.Append("A").Append(V(a.Center)).Append(';').Append(N(a.Radius)).Append(';').Append(N(a.StartAngle)).Append(';').Append(N(a.EndAngle)); break;
                    case SymPolyline pl:
                        sb.Append("P").Append(pl.Closed ? "c" : "o").Append(N(pl.Width));
                        foreach (Vec2 v in pl.Points)
                        {
                            sb.Append(';').Append(V(v));
                        }

                        if (pl.Bulges != null)
                        {
                            sb.Append("b").Append(string.Join(",", pl.Bulges.Select(N)));
                        }

                        break;
                    case SymFill f:
                        sb.Append("F");
                        foreach (Vec2 v in f.Points)
                        {
                            sb.Append(';').Append(V(v));
                        }

                        break;
                    case SymText t: sb.Append("T").Append(V(t.Position)).Append(';').Append(N(t.Height)).Append(';').Append(t.Text); break;
                }
            }

            return ContentHasher.Sha256Hex(sb.ToString());
        }
    }

    /// <summary>
    /// Builds dimensionally accurate, parameter-driven plan symbols for point builders. Symbols use real sizes;
    /// very small items (poles, pumps) get a minimum legible marker radius.
    /// </summary>
    public static class SymbolLibrary
    {
        /// <summary>Minimum marker radius for tiny objects, metres.</summary>
        public const double MinMarker = 0.15;

        public static SymbolDrawing Build(string builder, ParameterSet p, Func<string, double>? defaults = null)
        {
            double N(string k, double fallback)
            {
                double v = p.Number(k, double.NaN);
                if (Compat.IsFinite(v))
                {
                    return v;
                }

                double d = defaults?.Invoke(k) ?? double.NaN;
                return Compat.IsFinite(d) ? d : fallback;
            }

            bool B(string k) => p.Bool(k);
            string T(string k, string fallback) => p.Contains(k) && !p[k].IsNull ? p.Text(k) : fallback;

            var items = new List<SymbolPrimitive>();
            string symbol = builder;
            switch (builder)
            {
                case "tree":
                    {
                        double r = N("canopyDiameter", 5) / 2;
                        double trunk = Math.Max(N("trunkDiameter", 0.25) / 2, 0.05);
                        string form = T("form", "round");
                        string status = T("status", "proposed");
                        items.Add(new SymCircle(Vec2.Zero, r));
                        if (form == "conical")
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                Vec2 dir = new Vec2(1, 0).Rotate(i * Math.PI / 6);
                                items.Add(new SymLine(dir * (r * 0.35), dir * r));
                            }
                        }
                        else if (form == "columnar")
                        {
                            items.Add(new SymCircle(Vec2.Zero, r * 0.6));
                        }
                        else
                        {
                            items.Add(Scallop(r * 0.92, 10, 0.25));
                        }

                        items.Add(new SymCircle(Vec2.Zero, trunk));
                        items.Add(new SymLine(new Vec2(-trunk * 1.6, 0), new Vec2(trunk * 1.6, 0)));
                        items.Add(new SymLine(new Vec2(0, -trunk * 1.6), new Vec2(0, trunk * 1.6)));
                        if (status == "existing_remove")
                        {
                            Vec2 d1 = new Vec2(r, r) * Math.Sqrt(0.5);
                            Vec2 d2 = new Vec2(r, -r) * Math.Sqrt(0.5);
                            items.Add(new SymLine(d1 * -1, d1));
                            items.Add(new SymLine(d2 * -1, d2));
                        }
                        else if (status == "existing_retain")
                        {
                            items.Add(new SymCircle(Vec2.Zero, r * 1.05));
                        }

                        break;
                    }

                case "shrub":
                    items.Add(Scallop(N("diameter", 1.2) / 2, 7, 0.35));
                    items.Add(new SymCircle(Vec2.Zero, Math.Max(0.02, N("diameter", 1.2) * 0.04)));
                    break;
                case "pole_mounted":
                    {
                        symbol = "pole";
                        double r = Math.Max(N("poleDiameter", 0.12) / 2, MinMarker);
                        double arm = N("armLength", 0.6);
                        double hl = N("headLength", 0.6), hw = N("headWidth", 0.25);
                        items.Add(new SymCircle(Vec2.Zero, r));
                        items.Add(new SymLine(new Vec2(-r, 0), new Vec2(r, 0)));
                        items.Add(new SymLine(new Vec2(0, -r), new Vec2(0, r)));
                        if (arm > 1e-6)
                        {
                            items.Add(new SymLine(new Vec2(r, 0), new Vec2(arm, 0)));
                            items.Add(Rect(arm + (hl / 2), 0, hl, hw));
                        }
                        else
                        {
                            items.Add(Rect(0, 0, Math.Max(hl, 2 * r), Math.Max(hw, 2 * r)));
                        }

                        if (B("solarPanel"))
                        {
                            items.Add(Rect(0, 0, 0.8, 0.5));
                        }

                        break;
                    }

                case "bollard":
                    {
                        double r = Math.Max(N("diameter", 0.2) / 2, 0.05);
                        items.Add(new SymFill(CirclePoints(r, 16)));
                        items.Add(new SymCircle(Vec2.Zero, r));
                        if (B("lit"))
                        {
                            items.Add(new SymCircle(Vec2.Zero, r * 1.8));
                        }

                        break;
                    }

                case "bench":
                    {
                        double l = N("length", 1.8), d = N("depth", 0.6);
                        items.Add(Rect(0, 0, l, d));
                        if (B("backrest"))
                        {
                            double band = Math.Min(0.1, d * 0.2);
                            items.Add(new SymLine(new Vec2(-l / 2, (d / 2) - band), new Vec2(l / 2, (d / 2) - band)));
                        }

                        if (B("armrests"))
                        {
                            double a = Math.Min(0.06, l * 0.05);
                            items.Add(Rect((-l / 2) + (a / 2), 0, a, d));
                            items.Add(Rect((l / 2) - (a / 2), 0, a, d));
                        }

                        break;
                    }

                case "table":
                    {
                        double l = N("length", 2), w = N("width", 0.8);
                        items.Add(Rect(0, 0, l, w));
                        if (B("benches"))
                        {
                            double bw = N("benchWidth", 0.3);
                            items.Add(Rect(0, (w / 2) + (bw / 2), l, bw));
                            items.Add(Rect(0, -(w / 2) - (bw / 2), l, bw));
                        }

                        break;
                    }

                case "bike_stand":
                    {
                        int count = Math.Max(1, (int)Math.Round(N("count", 1)));
                        double spacing = N("spacing", 1), w = N("width", 0.8), tube = Math.Max(N("tubeDiameter", 0.05), 0.03);
                        for (int i = 0; i < count; i++)
                        {
                            items.Add(Rect(0, i * spacing, w, tube));
                            items.Add(new SymCircle(new Vec2(-w / 2, i * spacing), tube));
                            items.Add(new SymCircle(new Vec2(w / 2, i * spacing), tube));
                        }

                        break;
                    }

                case "box_equipment":
                    {
                        symbol = "box";
                        double w = N("width", 1), d = N("depth", 0.6);
                        items.Add(Rect(0, 0, w, d));
                        items.Add(new SymLine(new Vec2(-w / 2, -d / 2), new Vec2(w / 2, d / 2)));
                        items.Add(new SymLine(new Vec2(-w / 2, d / 2), new Vec2(w / 2, -d / 2)));
                        // front indicator (local -Y)
                        double tick = Math.Min(w, d) * 0.15;
                        items.Add(new SymLine(new Vec2(-tick, -d / 2), new Vec2(0, (-d / 2) - tick)));
                        items.Add(new SymLine(new Vec2(0, (-d / 2) - tick), new Vec2(tick, -d / 2)));
                        break;
                    }

                case "cylinder_equipment":
                    {
                        symbol = "cylinder";
                        double r = Math.Max(N("diameter", 0.6) / 2, 0.05);
                        items.Add(new SymCircle(Vec2.Zero, r));
                        double k = r * Math.Sqrt(0.5);
                        items.Add(new SymLine(new Vec2(-k, -k), new Vec2(k, k)));
                        items.Add(new SymLine(new Vec2(-k, k), new Vec2(k, -k)));
                        break;
                    }

                case "underground_container":
                    {
                        symbol = "container_ug";
                        double l = N("bodyLength", 2), w = N("bodyWidth", 2);
                        items.Add(Rect(0, 0, l, w));
                        double inset = Math.Min(0.1, Math.Min(l, w) * 0.1);
                        items.Add(Rect(0, 0, l - (2 * inset), w - (2 * inset)));
                        double cr = N("columnDiameter", 0.8) / 2;
                        items.Add(new SymCircle(Vec2.Zero, cr));
                        items.Add(new SymText(Vec2.Zero, Math.Max(0.12, cr * 0.6), FractionCode(T("wasteFraction", "mixed"))));
                        break;
                    }

                case "chamber":
                    {
                        double inner = N("innerSize", 1), wall = N("wallThickness", 0.12), cover = N("coverSize", 0.6);
                        bool round = T("shape", "round") == "round";
                        if (round)
                        {
                            items.Add(new SymCircle(Vec2.Zero, (inner / 2) + wall));
                            items.Add(new SymCircle(Vec2.Zero, inner / 2));
                        }
                        else
                        {
                            items.Add(Rect(0, 0, inner + (2 * wall), inner + (2 * wall)));
                            items.Add(Rect(0, 0, inner, inner));
                        }

                        if (T("coverShape", "round") == "round")
                        {
                            items.Add(new SymCircle(Vec2.Zero, cover / 2));
                        }
                        else
                        {
                            items.Add(Rect(0, 0, cover, cover));
                        }

                        items.Add(new SymLine(new Vec2(-cover / 2, 0), new Vec2(cover / 2, 0)));
                        items.Add(new SymLine(new Vec2(0, -cover / 2), new Vec2(0, cover / 2)));
                        break;
                    }

                case "tank":
                    {
                        string shape = T("shape", "box");
                        double l = N("length", 3), w = N("width", 1.5);
                        if (shape == "vertical_cylinder")
                        {
                            items.Add(new SymCircle(Vec2.Zero, w / 2));
                        }
                        else
                        {
                            items.Add(Rect(0, 0, l, w));
                            items.Add(new SymLine(new Vec2(-l / 2, 0), new Vec2(l / 2, 0)));
                        }

                        if (B("accessShaft"))
                        {
                            items.Add(new SymCircle(Vec2.Zero, N("shaftDiameter", 0.6) / 2));
                        }

                        break;
                    }

                case "shelter":
                    {
                        double w = N("width", 3), d = N("depth", 1.5), post = Math.Max(N("postSize", 0.1), 0.05);
                        items.Add(Rect(0, 0, w, d));
                        foreach (Vec2 c in new[] { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 1), new Vec2(-1, 1) })
                        {
                            items.Add(new SymFill(RectPoints((c.X * ((w / 2) - (post / 2))), c.Y * ((d / 2) - (post / 2)), post, post)));
                        }

                        if (B("backWall"))
                        {
                            items.Add(new SymPolyline(new[] { new Vec2(-w / 2, d / 2), new Vec2(w / 2, d / 2) }, false, null, 0.03));
                        }

                        if (B("sideWalls"))
                        {
                            items.Add(new SymPolyline(new[] { new Vec2(-w / 2, -d / 2), new Vec2(-w / 2, d / 2) }, false, null, 0.03));
                            items.Add(new SymPolyline(new[] { new Vec2(w / 2, -d / 2), new Vec2(w / 2, d / 2) }, false, null, 0.03));
                        }

                        break;
                    }

                case "parking_bay":
                    {
                        double w = N("width", 2.5), l = N("length", 5), lw = N("lineWidth", 0.12);
                        items.Add(new SymPolyline(new[] { new Vec2(-w / 2, 0), new Vec2(-w / 2, l), new Vec2(w / 2, l), new Vec2(w / 2, 0) }, false, null, lw));
                        string code = B("accessible") ? "A" : B("ev") ? "EV" : string.Empty;
                        string number = T("bayNumber", string.Empty);
                        if (code.Length > 0)
                        {
                            items.Add(new SymText(new Vec2(0, l * 0.5), Math.Min(0.8, w * 0.3), code));
                        }

                        if (number.Length > 0)
                        {
                            items.Add(new SymText(new Vec2(0, l * 0.82), Math.Min(0.4, w * 0.16), number));
                        }

                        break;
                    }

                case "arrow_marking":
                    {
                        symbol = "arrow";
                        double l = N("length", 5), hw = N("width", 1), sw = N("shaftWidth", 0.15);
                        double head = Math.Min(l * 0.4, hw * 1.6);
                        var pts = new List<Vec2>
                        {
                            new Vec2(0, -sw / 2), new Vec2(l - head, -sw / 2), new Vec2(l - head, -hw / 2), new Vec2(l, 0),
                            new Vec2(l - head, hw / 2), new Vec2(l - head, sw / 2), new Vec2(0, sw / 2),
                        };
                        items.Add(new SymFill(pts));
                        items.Add(new SymPolyline(pts, true));
                        break;
                    }

                case "sign_post":
                    {
                        symbol = "sign";
                        int posts = (int)Math.Round(N("postCount", 1));
                        double pw = N("plateWidth", 0.6), pd = Math.Max(N("postDiameter", 0.076) / 2, 0.04), pt = Math.Max(N("plateThickness", 0.02), 0.02);
                        if (posts == 1)
                        {
                            items.Add(new SymCircle(Vec2.Zero, pd));
                        }
                        else if (posts >= 2)
                        {
                            items.Add(new SymCircle(new Vec2(-pw * 0.4, 0), pd));
                            items.Add(new SymCircle(new Vec2(pw * 0.4, 0), pd));
                        }

                        items.Add(Rect(0, -pd - (pt / 2), pw, pt));
                        double tri = Math.Min(0.25, pw * 0.3);
                        items.Add(new SymPolyline(new[] { new Vec2(-tri / 2, -pd - pt - 0.05), new Vec2(0, -pd - pt - 0.05 - tri), new Vec2(tri / 2, -pd - pt - 0.05) }, true));
                        break;
                    }

                case "gate":
                    {
                        double w = N("width", 1), post = Math.Max(N("postSize", 0.1), 0.05);
                        string kind = T("gateType", "swing_single");
                        double side = T("openingSide", "back") == "back" ? 1 : -1;
                        items.Add(new SymFill(RectPoints((-w / 2) - (post / 2), 0, post, post)));
                        items.Add(new SymFill(RectPoints((w / 2) + (post / 2), 0, post, post)));
                        switch (kind)
                        {
                            case "swing_double":
                                {
                                    double h = w / 2;
                                    items.Add(new SymLine(new Vec2(-w / 2, 0), new Vec2(-w / 2, side * h)));
                                    items.Add(new SymLine(new Vec2(w / 2, 0), new Vec2(w / 2, side * h)));
                                    items.Add(side > 0 ? new SymArc(new Vec2(-w / 2, 0), h, 0, Math.PI / 2) : new SymArc(new Vec2(-w / 2, 0), h, -Math.PI / 2, 0));
                                    items.Add(side > 0 ? new SymArc(new Vec2(w / 2, 0), h, Math.PI / 2, Math.PI) : new SymArc(new Vec2(w / 2, 0), h, Math.PI, 1.5 * Math.PI));
                                    break;
                                }

                            case "sliding":
                                items.Add(new SymPolyline(new[] { new Vec2(-w / 2, -0.08), new Vec2(w / 2, -0.08) }, false, null, 0.05));
                                items.Add(new SymLine(new Vec2(-w / 2, -0.2), new Vec2((-w / 2) - w, -0.2)));
                                items.Add(new SymLine(new Vec2((-w / 2) - w, -0.2), new Vec2((-w / 2) - w + 0.25, -0.1)));
                                items.Add(new SymLine(new Vec2((-w / 2) - w, -0.2), new Vec2((-w / 2) - w + 0.25, -0.3)));
                                break;
                            case "barrier":
                                items.Add(new SymCircle(new Vec2(-w / 2, 0), post));
                                items.Add(new SymPolyline(new[] { new Vec2(-w / 2, 0), new Vec2(w / 2, 0) }, false, null, 0.06));
                                break;
                            default:
                                items.Add(new SymLine(new Vec2(-w / 2, 0), new Vec2(-w / 2, side * w)));
                                items.Add(side > 0 ? new SymArc(new Vec2(-w / 2, 0), w, 0, Math.PI / 2) : new SymArc(new Vec2(-w / 2, 0), w, -Math.PI / 2, 0));
                                break;
                        }

                        break;
                    }

                case "swing":
                    {
                        double w = N("width", 3.4), s = N("frameSpread", 1.8);
                        int seats = Math.Max(1, (int)Math.Round(N("seats", 2)));
                        items.Add(Rect(0, 0, w, s));
                        items.Add(new SymLine(new Vec2(-w / 2, 0), new Vec2(w / 2, 0)));
                        for (int i = 0; i < seats; i++)
                        {
                            double x = (-w / 2) + ((i + 0.5) * w / seats);
                            items.Add(Rect(x, 0, Math.Min(0.45, (w / seats) * 0.6), 0.2));
                        }

                        break;
                    }

                case "solar_array":
                    {
                        int rows = Math.Max(1, (int)Math.Round(N("rows", 2)));
                        int mods = Math.Max(1, (int)Math.Round(N("modulesPerRow", 10)));
                        double mw = N("moduleWidth", 1.13), ml = N("moduleLength", 1.72), pitch = N("rowSpacing", 3.5);
                        double depth = ml * Math.Cos(N("tilt", 25) * Math.PI / 180);
                        for (int r = 0; r < rows; r++)
                        {
                            double y0 = r * pitch;
                            items.Add(new SymPolyline(new[] { new Vec2(0, y0), new Vec2(mods * mw, y0), new Vec2(mods * mw, y0 + depth), new Vec2(0, y0 + depth) }, true));
                            for (int m = 1; m < mods; m++)
                            {
                                items.Add(new SymLine(new Vec2(m * mw, y0), new Vec2(m * mw, y0 + depth)));
                            }
                        }

                        break;
                    }

                case "vawt":
                    {
                        double r = N("rotorDiameter", 1.5) / 2;
                        items.Add(new SymCircle(Vec2.Zero, r));
                        items.Add(new SymCircle(Vec2.Zero, Math.Max(N("mastDiameter", 0.15) / 2, 0.05)));
                        for (int i = 0; i < 3; i++)
                        {
                            Vec2 d = new Vec2(1, 0).Rotate(i * 2 * Math.PI / 3);
                            items.Add(new SymLine(Vec2.Zero, d * r));
                        }

                        break;
                    }

                case "fountain":
                    {
                        double r = N("basinDiameter", 3) / 2, rim = N("rimWidth", 0.25);
                        items.Add(new SymCircle(Vec2.Zero, r));
                        items.Add(new SymCircle(Vec2.Zero, Math.Max(0.05, r - rim)));
                        if (N("jetHeight", 2) > 0)
                        {
                            items.Add(new SymFill(CirclePoints(Math.Max(0.05, N("jetDiameter", 0.08) / 2), 12)));
                        }

                        break;
                    }

                case "boulder":
                    {
                        double a = N("length", 1.2) / 2, b = N("width", 0.9) / 2;
                        double[] f = { 1.0, 0.86, 0.95, 0.8, 1.0, 0.9, 0.84, 0.97, 0.88 };
                        var pts = new List<Vec2>();
                        for (int i = 0; i < f.Length; i++)
                        {
                            double t = i * 2 * Math.PI / f.Length;
                            pts.Add(new Vec2(Math.Cos(t) * a * f[i], Math.Sin(t) * b * f[i]));
                        }

                        items.Add(new SymPolyline(pts, true));
                        break;
                    }

                case "marker":
                    {
                        double s = N("size", 1);
                        var pts = new List<Vec2> { new Vec2(s * 0.5, 0), new Vec2(-s * 0.5, s * 0.35), new Vec2(-s * 0.5, -s * 0.35) };
                        items.Add(new SymFill(pts));
                        items.Add(new SymPolyline(pts, true));
                        string reference = T("reference", string.Empty);
                        if (reference.Length > 0)
                        {
                            items.Add(new SymText(new Vec2(-s * 0.9, 0), s * 0.3, reference));
                        }

                        break;
                    }

                case "circle_zone":
                    items.Add(new SymCircle(Vec2.Zero, N("radius", 2)));
                    break;
                default:
                    throw new NotSupportedException("Za generator '" + builder + "' nije definisan simbol u tlocrtu.");
            }

            return new SymbolDrawing(symbol, items);
        }

        public static bool HasSymbol(string builder)
        {
            switch (builder)
            {
                case "tree": case "shrub": case "pole_mounted": case "bollard": case "bench": case "table": case "bike_stand":
                case "box_equipment": case "cylinder_equipment": case "underground_container": case "chamber": case "tank":
                case "shelter": case "parking_bay": case "arrow_marking": case "sign_post": case "gate": case "swing":
                case "solar_array": case "vawt": case "fountain": case "boulder": case "marker": case "circle_zone":
                    return true;
                default:
                    return false;
            }
        }

        private static string FractionCode(string fraction) => fraction switch
        {
            "paper" => "P",
            "plastic" => "PL",
            "glass" => "G",
            "organic" => "O",
            "metal" => "M",
            "textile" => "T",
            _ => "R",
        };

        private static SymPolyline Rect(double cx, double cy, double w, double h) => new SymPolyline(RectPoints(cx, cy, w, h), true);

        private static List<Vec2> RectPoints(double cx, double cy, double w, double h) => new List<Vec2>
        {
            new Vec2(cx - (w / 2), cy - (h / 2)), new Vec2(cx + (w / 2), cy - (h / 2)), new Vec2(cx + (w / 2), cy + (h / 2)), new Vec2(cx - (w / 2), cy + (h / 2)),
        };

        private static List<Vec2> CirclePoints(double r, int n)
        {
            var pts = new List<Vec2>(n);
            for (int i = 0; i < n; i++)
            {
                double t = i * 2 * Math.PI / n;
                pts.Add(new Vec2(Math.Cos(t) * r, Math.Sin(t) * r));
            }

            return pts;
        }

        /// <summary>Cloud-like closed outline: n outward arcs around a circle (counter-clockwise, positive bulges).</summary>
        private static SymPolyline Scallop(double r, int n, double bulge)
        {
            return new SymPolyline(CirclePoints(r, n), true, Enumerable.Repeat(bulge, n).ToList());
        }

        public static string Describe(SymbolDrawing d) => string.Format(CultureInfo.InvariantCulture, "{0} ({1} primitives)", d.Symbol, d.Items.Count);
    }
}
