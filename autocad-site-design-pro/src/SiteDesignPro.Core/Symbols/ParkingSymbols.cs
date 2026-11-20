using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Symbols
{
    /// <summary>
    /// Markings of a parking bay that is part of a row (SDPPARKINGOKOMITO, SDPPARKINGPARALELNO, SDPPARKINGUGAO), in
    /// the bay's frame: the origin is the middle of its edge at the aisle, +Y goes into the bay (to the kerb), the row
    /// runs along <c>rowDir</c> × X. Every bay draws its end line (a T head at the aisle, or a hook at the end of the
    /// row); the first bay also draws the start line with a hook. Lines 10 cm, T heads 1,00 m, hooks 55 cm (50 cm at
    /// 45°). An accessible bay is yellow, has the wheelchair symbol, a hatched area of 1,40 m after it and a kerb
    /// ramp beyond the kerb at that area. At 45° the triangle before the first line is hatched and the angle is shown.
    /// </summary>
    public static class ParkingSymbols
    {
        public const double LineWidth = 0.10;
        public const double Head = 1.00;
        public const double Hook = 0.55;
        public const double AngledHook = 0.50;
        public const double AccessibleBay = 2.50;
        public const double KerbRampDepth = 0.70;
        public const short Yellow = 2;

        public static void Build(List<SymbolPrimitive> items, string layout, double w, double depth, int rowDir, bool first, bool last, bool accessible, double hatch, string number)
        {
            double f = rowDir < 0 ? -1 : 1;
            short color = accessible ? Yellow : (short)0;
            double half = w / 2;
            Vec2 P(double s, double y) => new Vec2(f * s, y);
            void Line(double s0, double y0, double s1, double y1, short c, double width = LineWidth) =>
                items.Add(new SymPolyline(new[] { P(s0, y0), P(s1, y1) }, false, null, width) { Color = c });

            // numbers stand with their top towards the kerb (along the bay)
            if (layout == "angled")
            {
                double t = depth; // 45°: the line moves on along the row as far as it goes in
                double hook = AngledHook;
                if (first)
                {
                    Line(-half, 0, -half + t, depth, color);
                    Line(-half - (LineWidth / 2), LineWidth / 2, -half + hook, LineWidth / 2, color);
                    Triangle(items, P(-half, 0), P(-half, depth), P(-half + t, depth));
                    Angle(items, P(-half + t, depth), f);
                }

                Line(half, 0, half + t, depth, color);
                if (last)
                {
                    Line(half - hook, LineWidth / 2, half + (LineWidth / 2), LineWidth / 2, color);
                }
                else
                {
                    Line(half - (Head / 2), LineWidth / 2, half + (Head / 2), LineWidth / 2, color);
                }

                if (first)
                {
                    Angle(items, P(half + t, depth), f);
                }

                if (number.Length > 0)
                {
                    items.Add(new SymText(P(1.1, 1.1), 0.45, number, Math.Atan2(-f, 1)));
                }

                return;
            }

            // perpendicular and parallel: lines square to the row
            double bayEnd = accessible && hatch > 0 ? half - hatch : half;
            if (first)
            {
                Line(-half, 0, -half, depth, color);
                Line(-half - (LineWidth / 2), LineWidth / 2, -half + Hook, LineWidth / 2, color);
            }

            if (accessible && hatch > 0)
            {
                Hatched(items, P, bayEnd, half, depth);
                KerbRamp(items, P, bayEnd, half, depth);
            }
            else
            {
                Line(half, 0, half, depth, color);
            }

            if (last)
            {
                Line(half - Hook, LineWidth / 2, half + (LineWidth / 2), LineWidth / 2, color);
            }
            else if (!(accessible && hatch > 0))
            {
                Line(half - (Head / 2), LineWidth / 2, half + (Head / 2), LineWidth / 2, color);
            }

            if (accessible)
            {
                double mid = (-half + bayEnd) / 2;
                Wheelchair(items, P(mid, depth * 0.55), f, Math.Min(1.3, (bayEnd + half) * 0.55));
            }

            if (number.Length > 0)
            {
                bool parallel = layout == "parallel";
                double mid = (-half + bayEnd) / 2;
                Vec2 at = parallel ? P(-half + 1.0, 0.55) : P(mid, Math.Min(1.4, depth * 0.3));
                items.Add(new SymText(at, parallel ? 0.45 : 0.5, number));
            }
        }

        /// <summary>The area beside an accessible bay: yellow border and yellow stripes at 45°.</summary>
        private static void Hatched(List<SymbolPrimitive> items, Func<double, double, Vec2> p, double s0, double s1, double depth)
        {
            double lw = ParkingSymbols.LineWidth, h = lw / 2;
            items.Add(new SymPolyline(new[] { p(s0 + h, h), p(s1 - h, h), p(s1 - h, depth - h), p(s0 + h, depth - h) }, true, null, lw) { Color = Yellow });
            double step = 0.70;
            double width = s1 - s0 - lw;
            for (double c = step; c < depth + width; c += step)
            {
                // the line s - s0 + (depth - y) = c, clipped to the inside of the border
                var pts = new List<Vec2>();
                double lo = s0 + lw, hi = s1 - lw, ylo = lw, yhi = depth - lw;
                foreach (double y in new[] { ylo, yhi })
                {
                    double s = s0 + c - (depth - y);
                    if (s >= lo - 1e-9 && s <= hi + 1e-9)
                    {
                        pts.Add(new Vec2(s, y));
                    }
                }

                foreach (double s in new[] { lo, hi })
                {
                    double y = depth - (c - (s - s0));
                    if (y > ylo + 1e-9 && y < yhi - 1e-9)
                    {
                        pts.Add(new Vec2(s, y));
                    }
                }

                if (pts.Count >= 2)
                {
                    Vec2 a = pts.OrderBy(v => v.X).First(), b = pts.OrderBy(v => v.X).Last();
                    if (a.DistanceTo(b) > 0.05)
                    {
                        items.Add(new SymPolyline(new[] { p(a.X, a.Y), p(b.X, b.Y) }, false, null, lw) { Color = Yellow });
                    }
                }
            }
        }

        /// <summary>The kerb ramp beyond the kerb at the hatched area (thin lines): the ramp and its two flares.</summary>
        private static void KerbRamp(List<SymbolPrimitive> items, Func<double, double, Vec2> p, double s0, double s1, double depth)
        {
            double d = depth + KerbRampDepth, flare = Math.Min(0.25, (s1 - s0) / 4);
            items.Add(new SymPolyline(new[] { p(s0, depth), p(s0, d), p(s1, d), p(s1, depth) }, false));
            items.Add(new SymLine(p(s0, depth + flare), p(s0 + flare, d)));
            items.Add(new SymLine(p(s1, depth + flare), p(s1 - flare, d)));
            items.Add(new SymLine(p(s0, depth), p(s1, depth)));
        }

        /// <summary>The hatched triangle before the first line of a 45° row (thin border and stripes square to the line).</summary>
        private static void Triangle(List<SymbolPrimitive> items, Vec2 a, Vec2 b, Vec2 c)
        {
            items.Add(new SymPolyline(new[] { a, b, c }, true));
            Vec2 dir = (c - a).Normalized();
            Vec2 across = new Vec2(-dir.Y, dir.X);
            if (across.Dot(b - a) < 0)
            {
                across = -across;
            }

            double len = (c - a).Length;
            for (double t = 0.5; t < len; t += 0.5)
            {
                Vec2 start = a + (dir * t);

                // from the hypotenuse across to the first of the other two sides
                double best = double.MaxValue;
                foreach ((Vec2 p, Vec2 q) in new[] { (a, b), (b, c) })
                {
                    Vec2 e = q - p;
                    double den = (across.X * e.Y) - (across.Y * e.X);
                    if (Math.Abs(den) < 1e-12)
                    {
                        continue;
                    }

                    Vec2 w = p - start;
                    double k = ((w.X * e.Y) - (w.Y * e.X)) / den;
                    double m = ((w.X * across.Y) - (w.Y * across.X)) / den;
                    if (k > 1e-6 && m >= -1e-9 && m <= 1 + 1e-9)
                    {
                        best = Math.Min(best, k);
                    }
                }

                if (best < double.MaxValue && best > 0.05)
                {
                    items.Add(new SymLine(start, start + (across * best)));
                }
            }
        }

        /// <summary>The 45° between a line of the row and the kerb, at the kerb end of the line.</summary>
        private static void Angle(List<SymbolPrimitive> items, Vec2 at, double f)
        {
            const double r = 1.2;
            double start = f > 0 ? Math.PI : 1.75 * Math.PI, end = start + (Math.PI / 4);
            items.Add(new SymArc(at, r, start, end));
            double mid = (start + end) / 2;
            items.Add(new SymText(at + (new Vec2(Math.Cos(mid), Math.Sin(mid)) * (r + 0.35)), 0.25, "45°"));
        }

        /// <summary>The wheelchair symbol (yellow), about <paramref name="size"/> high, its top towards the kerb.</summary>
        private static void Wheelchair(List<SymbolPrimitive> items, Vec2 centre, double f, double size)
        {
            double k = size / 1.3;
            Vec2 Q(double x, double y) => centre + new Vec2(-f * x * k, (y - 0.15) * k);
            var wheel = new List<Vec2>();
            for (int i = 0; i <= 24; i++)
            {
                double a = (Math.PI * 0.35) + (Math.PI * 1.55 * i / 24);
                wheel.Add(Q(0.42 * Math.Cos(a), 0.42 * Math.Sin(a)));
            }

            items.Add(new SymPolyline(wheel, false, null, 0.08 * k) { Color = Yellow });
            items.Add(new SymCircle(Q(0.05, 0.98), 0.12 * k) { Color = Yellow });
            items.Add(new SymFill(Enumerable.Range(0, 12).Select(i => Q(0.05 + (0.12 * Math.Cos(i * Math.PI / 6)), 0.98 + (0.12 * Math.Sin(i * Math.PI / 6)))).ToList()) { Color = Yellow });
            items.Add(new SymPolyline(new[] { Q(0.0, 0.75), Q(0.0, 0.18), Q(0.40, 0.18), Q(0.58, -0.32), Q(0.72, -0.32) }, false, null, 0.09 * k) { Color = Yellow });
            items.Add(new SymPolyline(new[] { Q(0.0, 0.52), Q(0.32, 0.52) }, false, null, 0.08 * k) { Color = Yellow });
        }
    }
}
