using System;
using System.Collections.Generic;
using System.Linq;
using SiteDesignPro.Core.Catalog;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>
    /// Garage ramp drawn from its centreline: straight, semicircular or circular/spiral; one-way or two-way. Plan:
    /// edges, the middle line, yellow lines of the safety margin, direction arrows, an island at the bottom of a
    /// straight ramp and the text L / h / slope. Heights come from the elevation marks at its two ends.
    /// </summary>
    /// <summary>One thing of the plan of a garage ramp: a line or filled outline (with arcs as bulges) or a text.</summary>
    public sealed class RampShape
    {
        /// <summary>surface, edge, margin, middle, arrow, island or text.</summary>
        public string Role { get; set; } = string.Empty;

        public List<Vec2> Points { get; set; } = new List<Vec2>();

        /// <summary>Bulge of the segment that starts at the point of the same index.</summary>
        public List<double> Bulges { get; set; } = new List<double>();

        public bool Closed { get; set; }

        public bool Fill { get; set; }

        /// <summary>Line width in metres (0: thin).</summary>
        public double Width { get; set; }

        public string? Text { get; set; }

        /// <summary>Texts: the middle of the text, its direction, height and length.</summary>
        public Vec2 Position { get; set; }

        public double Rotation { get; set; }

        public double Height { get; set; }

        public double Length { get; set; }

        public bool Warning { get; set; }
    }

    public sealed class GarageRamp
    {
        public const double MiddleLineWidth = 0.12;
        public const double MarginLineWidth = 0.10;
        public const double IslandWidth = 0.6;
        public const double IslandLength = 2.5;
        public const double IslandHeight = 0.15;
        public const double SlabThickness = 0.25;

        public const string Type = "road.garage_ramp";
        public const string SurfaceType = "road.garage_ramp_surface";

        /// <summary>Largest cross slope towards the inside of a curved ramp.</summary>
        public const double CrossSlope = 0.03;

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>straight, semicircular or spiral.</summary>
        public string Kind { get; set; } = "straight";

        public bool TwoWay { get; set; }

        public double LaneWidth { get; set; } = 3.0;

        /// <summary>Safety margin on both sides (yellow lines).</summary>
        public double Margin { get; set; } = 0.25;

        /// <summary>Straight ramps: island 0,60 × 2,50 m in the middle at the bottom.</summary>
        public bool Island { get; set; }

        /// <summary>Centreline in metres, from the start to the end of the ramp (arcs tessellated).</summary>
        public List<Vec2> Axis { get; set; } = new List<Vec2>();

        /// <summary>The centreline as drawn, in metres: vertices and the bulge of the segment after each (arcs stay arcs).</summary>
        public List<Vec2> RingPoints { get; set; } = new List<Vec2>();

        public List<double> RingBulges { get; set; } = new List<double>();

        public bool Curved => Kind != "straight";

        public int Lanes => TwoWay ? 2 : 1;

        public double Width => (Lanes * LaneWidth) + (2 * Margin);

        /// <summary>Height at the start and end (relative to the base elevation of the roads) from the marks, if any.</summary>
        public double? StartHeight { get; set; }

        public double? EndHeight { get; set; }

        /// <summary>Length through the middle (arcs exact).</summary>
        public double Length => Segments().Sum(g => g.Length);

        public double Rise => (EndHeight ?? 0) - (StartHeight ?? 0);

        /// <summary>Longitudinal slope through the middle (rise / length).</summary>
        public double Slope => Length < 1e-9 ? 0 : Math.Abs(Rise) / Length;

        public bool TooSteep => Slope > RoadLevels.MaxRampSlope + 1e-9;

        /// <summary>Allowed lane width range of the kind (straight 2,5–3,5 m, curved 3,5–5,0 m).</summary>
        public (double Min, double Max) LaneRange => Curved ? (3.5, 5.0) : (2.5, 3.5);

        public static GarageRamp? From(SiteElement e, double unitsPerMeter = 1.0)
        {
            if (e.TypeId != Type || e.Geometry is not PathGeometry path || path.Ring.Count < 2)
            {
                return null;
            }

            var r = new GarageRamp
            {
                Id = e.Id,
                Name = string.IsNullOrWhiteSpace(e.Label) ? e.Name : e.Label!,
                Kind = e.Parameters.Text("kind", "straight"),
                TwoWay = e.Parameters.Text("direction", "one_way") == "two_way",
                LaneWidth = e.Parameters.Number("laneWidth", 3.0),
                Margin = e.Parameters.Number("margin", 0.25),
                Island = e.Parameters.Bool("island", false),
                Axis = GeometryMath.Tessellate(path.Ring, false, 0.002 * unitsPerMeter, 1024).Select(v => new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter)).ToList(),
            };
            r.RingPoints = path.Ring.Vertices.Select(v => new Vec2(v.X / unitsPerMeter, v.Y / unitsPerMeter)).ToList();
            r.RingBulges = Enumerable.Range(0, path.Ring.Count).Select(i => i < path.Ring.Count - 1 ? path.Ring.BulgeAt(i) : 0.0).ToList();

            // the ranges of the kind hold also after the properties were edited
            (double lo, double hi) = r.LaneRange;
            r.LaneWidth = Math.Max(lo, Math.Min(hi, r.LaneWidth));
            r.Margin = Math.Max(r.Curved ? 0.25 : 0, Math.Min(0.5, r.Margin));
            r.Island = r.Island && r.TwoWay && !r.Curved;
            double s0 = e.Parameters.Number("startLevel"), s1 = e.Parameters.Number("endLevel");
            r.StartHeight = double.IsNaN(s0) ? (double?)null : s0;
            r.EndHeight = double.IsNaN(s1) ? (double?)null : s1;
            return r;
        }

        /// <summary>
        /// Heights of the ends not given on the ramp itself: from the elevation marks within the ramp width of the end points.
        /// </summary>
        public void ReadHeights(IEnumerable<RoadLevelMark> marks)
        {
            if (StartHeight.HasValue && EndHeight.HasValue)
            {
                return;
            }

            Vec2 a = Axis[0], b = Axis[Axis.Count - 1];
            double reach = (Width / 2) + 1.5;
            RoadLevelMark? ma = marks.Where(m => m.Position.DistanceTo(a) <= reach).OrderBy(m => m.Position.DistanceTo(a)).FirstOrDefault();
            RoadLevelMark? mb = marks.Where(m => m.Position.DistanceTo(b) <= reach).OrderBy(m => m.Position.DistanceTo(b)).FirstOrDefault();
            StartHeight ??= ma?.Value;
            EndHeight ??= mb?.Value;
        }

        /// <summary>Left and right offset lines of the centreline (left positive).</summary>
        public List<Vec2> Offset(double d)
        {
            var list = new List<Vec2>();
            int n = Axis.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2 t = i == 0 ? Axis[1] - Axis[0] : i == n - 1 ? Axis[n - 1] - Axis[n - 2] : Axis[i + 1] - Axis[i - 1];
                list.Add(Axis[i] + (t.Normalized().Perp() * d));
            }

            return list;
        }

        /// <summary>Point and direction at a station of the centreline.</summary>
        public (Vec2 Point, Vec2 Direction) At(double station) => PointAt(station, 0);

        private sealed class Seg
        {
            public Vec2 A { get; set; }

            public Vec2 B { get; set; }

            public bool Arc { get; set; }

            public Vec2 Centre { get; set; }

            public double Radius { get; set; }

            public double StartAngle { get; set; }

            public double Sweep { get; set; }

            public double Length { get; set; }

            public double Start { get; set; }
        }

        private List<Seg>? segs;

        private List<Seg> Segments()
        {
            if (segs != null)
            {
                return segs;
            }

            bool ring = RingPoints.Count >= 2;
            List<Vec2> pts = ring ? RingPoints : Axis;
            var list = new List<Seg>();
            double run = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vec2 a = pts[i], b = pts[i + 1];
                double bulge = ring && i < RingBulges.Count ? RingBulges[i] : 0;
                double chord = a.DistanceTo(b);
                if (chord < 1e-9)
                {
                    continue;
                }

                var g = new Seg { A = a, B = b, Start = run };
                if (Math.Abs(bulge) > 1e-9)
                {
                    double sweep = 4 * Math.Atan(bulge);
                    double radius = chord / (2 * Math.Sin(Math.Abs(sweep) / 2));
                    Vec2 mid = (a + b) * 0.5;
                    Vec2 left = (b - a).Normalized().Perp();
                    double h = radius * Math.Cos(Math.Abs(sweep) / 2);
                    g.Arc = true;
                    g.Centre = mid + (left * (bulge > 0 ? h : -h));
                    g.Radius = radius;
                    g.StartAngle = Math.Atan2(a.Y - g.Centre.Y, a.X - g.Centre.X);
                    g.Sweep = sweep;
                    g.Length = radius * Math.Abs(sweep);
                }
                else
                {
                    g.Length = chord;
                }

                run += g.Length;
                list.Add(g);
            }

            segs = list;
            return list;
        }

        /// <summary>Radius of the centreline of a curved ramp (its first arc; 0 for a straight ramp).</summary>
        public double AxisRadius => Segments().FirstOrDefault(g => g.Arc)?.Radius ?? 0;

        /// <summary>Radius of the inner edge of a curved ramp.</summary>
        public double InnerRadius => AxisRadius - (Width / 2);

        /// <summary>Point at a station, <paramref name="d"/> metres to the left of the centreline, and the direction there.</summary>
        public (Vec2 Point, Vec2 Direction) PointAt(double station, double d)
        {
            List<Seg> list = Segments();
            if (list.Count == 0)
            {
                return (Axis.Count > 0 ? Axis[0] : default, new Vec2(1, 0));
            }

            Seg g = list[list.Count - 1];
            foreach (Seg x in list)
            {
                if (station <= x.Start + x.Length + 1e-9)
                {
                    g = x;
                    break;
                }
            }

            double t = station - g.Start;
            if (!g.Arc)
            {
                Vec2 dir = (g.B - g.A).Normalized();
                return (g.A + (dir * t) + (dir.Perp() * d), dir);
            }

            double sign = g.Sweep > 0 ? 1 : -1;
            double ang = g.StartAngle + (g.Sweep * (t / g.Length));
            var u = new Vec2(Math.Cos(ang), Math.Sin(ang));
            return (g.Centre + (u * Math.Max(0.01, g.Radius - (d * sign))), u.Perp() * sign);
        }

        /// <summary>The line <paramref name="d"/> metres to the left of the centreline between two stations, arcs as bulges.</summary>
        public (List<Vec2> Points, List<double> Bulges) Sub(double d, double from, double to)
        {
            var pts = new List<Vec2>();
            var bulges = new List<double>();
            foreach (Seg g in Segments())
            {
                double a = Math.Max(from, g.Start), b = Math.Min(to, g.Start + g.Length);
                if (b - a < 1e-6)
                {
                    continue;
                }

                if (pts.Count == 0)
                {
                    pts.Add(PointAt(a, d).Point);
                    bulges.Add(0);
                }

                bulges[bulges.Count - 1] = g.Arc ? Math.Tan(g.Sweep * ((b - a) / g.Length) / 4) : 0;
                pts.Add(PointAt(b, d).Point);
                bulges.Add(0);
            }

            return (pts, bulges);
        }

        /// <summary>Stations for 3D between two stations: the ends, the ends of the segments and every 3° of an arc.</summary>
        public List<double> Stations(double from, double to)
        {
            var list = new List<double> { from };
            foreach (Seg g in Segments())
            {
                double a = Math.Max(from, g.Start), b = Math.Min(to, g.Start + g.Length);
                if (b - a < 1e-6)
                {
                    continue;
                }

                int n = g.Arc ? Math.Max(1, (int)Math.Ceiling(Math.Abs(g.Sweep) * ((b - a) / g.Length) / (Math.PI / 60))) : 1;
                for (int i = 1; i <= n; i++)
                {
                    double s = a + ((b - a) * i / n);
                    if (s - list[list.Count - 1] > 1e-6)
                    {
                        list.Add(s);
                    }
                }
            }

            if (list.Count == 1)
            {
                list.Add(to);
            }

            return list;
        }

        /// <summary>Height of the surface at a station, <paramref name="d"/> metres to the left of the centreline.</summary>
        public double HeightAt(double station, double d)
        {
            double h0 = StartHeight ?? 0, h1 = EndHeight ?? h0;
            double total = Length;
            double z = total < 1e-9 ? h0 : h0 + ((h1 - h0) * Math.Max(0, Math.Min(1, station / total)));
            return z - (CrossSlope * d * InnerSide);
        }

        private Vec3 P3(double station, double d, double lift) => PointAt(station, d).Point.WithZ(HeightAt(station, d) + lift);

        /// <summary>A band of the surface between two offsets and two stations, lifted above it (3D).</summary>
        public List<Vec3[]> Band3(double right, double left, double from, double to, double lift)
        {
            var list = new List<Vec3[]>();
            List<double> st = Stations(from, to);
            for (int i = 1; i < st.Count; i++)
            {
                Vec3 l0 = P3(st[i - 1], left, lift), l1 = P3(st[i], left, lift), r0 = P3(st[i - 1], right, lift), r1 = P3(st[i], right, lift);
                list.Add(new[] { r0, r1, l1 });
                list.Add(new[] { r0, l1, l0 });
            }

            return list;
        }

        /// <summary>Which side the inside of the curve is on (+1 left, −1 right, 0 straight).</summary>
        public int InnerSide
        {
            get
            {
                if (!Curved || Axis.Count < 3)
                {
                    return 0;
                }

                double turn = 0;
                for (int i = 1; i < Axis.Count - 1; i++)
                {
                    turn += (Axis[i] - Axis[i - 1]).Cross(Axis[i + 1] - Axis[i]);
                }

                return turn > 0 ? 1 : -1;
            }
        }

        /// <summary>
        /// Surface triangles in 3D: height linear along the centreline from the start to the end height; a curved ramp
        /// falls 3 % across towards the inside of the curve.
        /// </summary>
        public List<Vec3[]> Triangles() => Band3(-Width / 2, Width / 2, 0, Length, 0);

        /// <summary>The sides and ends of the ramp slab below the surface (3D).</summary>
        public List<Vec3[]> Sides()
        {
            var list = new List<Vec3[]>();
            double half = Width / 2, total = Length;
            List<double> st = Stations(0, total);
            foreach (double d in new[] { half, -half })
            {
                for (int i = 1; i < st.Count; i++)
                {
                    Vec3 a = P3(st[i - 1], d, 0), b = P3(st[i], d, 0), a1 = P3(st[i - 1], d, -SlabThickness), b1 = P3(st[i], d, -SlabThickness);
                    list.Add(new[] { a, b, b1 });
                    list.Add(new[] { a, b1, a1 });
                }
            }

            foreach (double s0 in new[] { 0.0, total })
            {
                Vec3 a = P3(s0, -half, 0), b = P3(s0, half, 0), a1 = P3(s0, -half, -SlabThickness), b1 = P3(s0, half, -SlabThickness);
                list.Add(new[] { a, b, b1 });
                list.Add(new[] { a, b1, a1 });
            }

            return list;
        }

        /// <summary>True when the lower end of the ramp is its end (else its start).</summary>
        public bool LowAtEnd => (EndHeight ?? 0) <= (StartHeight ?? 0);

        /// <summary>The island of a straight two-way ramp (0,60 × 2,50 m at the bottom): stations of its two ends, or null.</summary>
        public (double From, double To)? IslandSpan
        {
            get
            {
                double total = Length;
                if (!Island || !TwoWay || Curved || total < IslandLength + 3)
                {
                    return null;
                }

                return LowAtEnd ? (total - 0.5 - IslandLength, total - 0.5) : (0.5, 0.5 + IslandLength);
            }
        }

        /// <summary>"L=12,50 m", "h=2,10 m", "16,80 %" and, on a curved ramp, the cross slope.</summary>
        public List<string> Labels()
        {
            string N(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            var list = new List<string> { "L=" + N(Length) + " m", "h=" + N(Math.Abs(Rise)) + " m", N(Slope * 100) + " %" + (TooSteep ? " > 17 %!" : string.Empty) };
            if (Curved)
            {
                list.Add("popr. " + (CrossSlope * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " % unutra");
            }

            return list;
        }

        private sealed class Layout
        {
            public double TextHeight { get; set; }

            public bool Flip { get; set; }

            public List<(double Station, string Text, double Length)> Texts { get; } = new List<(double, string, double)>();

            public List<(double Station, double Offset, bool Back, double Scale)> Arrows { get; } = new List<(double, double, bool, double)>();

            /// <summary>Where the middle line is interrupted in the plan (text, arrows of a one-way ramp, island).</summary>
            public List<(double From, double To)> Gaps { get; } = new List<(double, double)>();

            /// <summary>The same in 3D, where there is no text.</summary>
            public List<(double From, double To)> Gaps3 { get; } = new List<(double, double)>();
        }

        /// <summary>Where the texts and arrows go along the ramp, and where the middle line is interrupted for them.</summary>
        private Layout Arrange()
        {
            var lay = new Layout();
            double total = Length;
            const double CharWidth = 0.68, Gap = 0.7;
            List<string> labels = Labels();
            List<string> pieces = Curved ? labels : new List<string> { string.Join(", ", labels) };
            double h = 0.30;
            double Need(double height) => pieces.Sum(t => t.Length * CharWidth * height) + (Gap * (pieces.Count - 1));
            while (h > 0.12 && Need(h) > total * 0.55)
            {
                h -= 0.02;
            }

            lay.TextHeight = h;
            double need = Need(h);
            Vec2 midDir = PointAt(total / 2, 0).Direction;
            lay.Flip = midDir.X < -1e-9 || (Math.Abs(midDir.X) <= 1e-9 && midDir.Y < 0);
            double at = (total / 2) - (need / 2);
            foreach (string t in lay.Flip ? Enumerable.Reverse(pieces) : pieces)
            {
                double len = t.Length * CharWidth * h;
                lay.Texts.Add((at + (len / 2), t, len));
                at += len + Gap;
            }

            double t0 = (total / 2) - (need / 2) - 0.4, t1 = (total / 2) + (need / 2) + 0.4;
            lay.Gaps.Add((t0, t1));

            // one arrow per lane in each half, in the middle of the free length beside the text
            double free = t0 - 0.5;
            double scale = Math.Min(1.0, (free - 0.4) / 5.0);
            if (scale >= 0.15)
            {
                foreach (double s in new[] { 0.5 + (free / 2), total - 0.5 - (free / 2) })
                {
                    if (TwoWay)
                    {
                        lay.Arrows.Add((s, -LaneWidth / 2, false, scale));
                        lay.Arrows.Add((s, LaneWidth / 2, true, scale));
                    }
                    else
                    {
                        lay.Arrows.Add((s, 0, false, scale));
                        (double, double) gap = (s - (2.5 * scale) - 0.2, s + (2.5 * scale) + 0.2);
                        lay.Gaps.Add(gap);
                        lay.Gaps3.Add(gap);
                    }
                }
            }

            (double From, double To)? island = IslandSpan;
            if (island.HasValue)
            {
                lay.Gaps.Add(island.Value);
                lay.Gaps3.Add(island.Value);
            }

            return lay;
        }

        /// <summary>The stretches of the middle line between the given interruptions.</summary>
        private List<(double From, double To)> Runs(List<(double From, double To)> gaps)
        {
            var list = new List<(double, double)>();
            double total = Length, at = 0;
            foreach ((double from, double to) in gaps.OrderBy(g => g.From).Concat(new[] { (total, total) }))
            {
                double end = Math.Min(from, total);
                if (end - at > 0.2)
                {
                    list.Add((at, end));
                }

                at = Math.Max(at, to);
            }

            return list;
        }

        /// <summary>
        /// The plan of the ramp: surface, edges, yellow lines of the safety margin, the middle line (interrupted at the
        /// text, the arrows of a one-way ramp and the island), direction arrows, the island and the texts L / h / slope
        /// through the middle. Arcs are arcs (bulges); metres.
        /// </summary>
        public List<RampShape> Plan()
        {
            var list = new List<RampShape>();
            double total = Length, half = Width / 2;
            if (total < 1e-6)
            {
                return list;
            }

            (List<Vec2> lp, List<double> lb) = Sub(half, 0, total);
            (List<Vec2> rp, List<double> rb) = Sub(-half, 0, total);
            var outline = new List<Vec2>(lp);
            var bulges = new List<double>(lb);
            for (int i = rp.Count - 1; i >= 0; i--)
            {
                outline.Add(rp[i]);
                bulges.Add(i > 0 ? -rb[i - 1] : 0);
            }

            list.Add(new RampShape { Role = "surface", Points = outline, Bulges = bulges, Closed = true, Fill = true });
            list.Add(new RampShape { Role = "edge", Points = outline, Bulges = bulges, Closed = true });
            if (Margin > 0.01)
            {
                foreach (double d in new[] { half - Margin, -(half - Margin) })
                {
                    (List<Vec2> mp, List<double> mb) = Sub(d, 0, total);
                    list.Add(new RampShape { Role = "margin", Points = mp, Bulges = mb, Width = MarginLineWidth });
                }
            }

            Layout lay = Arrange();
            foreach ((double from, double to) in Runs(lay.Gaps))
            {
                (List<Vec2> mp, List<double> mb) = Sub(0, from, to);
                if (mp.Count >= 2)
                {
                    list.Add(new RampShape { Role = "middle", Points = mp, Bulges = mb, Width = MiddleLineWidth });
                }
            }

            foreach ((double s, double off, bool back, double scale) in lay.Arrows)
            {
                double sign = back ? -1 : 1;
                List<Vec2> a = RoadArrow.Shape.Select(p => PointAt(s + (p.X * scale * sign), off + (p.Y * scale * sign)).Point).ToList();
                list.Add(new RampShape { Role = "arrow", Points = a, Bulges = a.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
            }

            (double From, double To)? island = IslandSpan;
            if (island.HasValue)
            {
                double w = IslandWidth / 2;
                var box = new List<Vec2> { PointAt(island.Value.From, -w).Point, PointAt(island.Value.To, -w).Point, PointAt(island.Value.To, w).Point, PointAt(island.Value.From, w).Point };
                list.Add(new RampShape { Role = "island", Points = box, Bulges = box.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
            }

            foreach ((double s, string text, double len) in lay.Texts)
            {
                (Vec2 p, Vec2 dir) = PointAt(s, 0);
                double rot = Math.Atan2(dir.Y, dir.X) + (lay.Flip ? Math.PI : 0);
                list.Add(new RampShape { Role = "text", Text = text, Position = p, Rotation = rot, Height = lay.TextHeight, Length = len, Warning = TooSteep });
            }

            return list;
        }

        /// <summary>Where the elevation symbols of the two ends stand (beside the ramp, on opposite sides).</summary>
        public (Vec2 Start, Vec2 End) LevelPoints() => (PointAt(0, (Width / 2) + 0.8).Point, PointAt(Length, -((Width / 2) + 0.8)).Point);

        /// <summary>The painted lines and arrows in 3D, a little above the surface: (white, yellow).</summary>
        public (List<Vec3[]> White, List<Vec3[]> Yellow) Paint3()
        {
            const double Lift = 0.006;
            var white = new List<Vec3[]>();
            var yellow = new List<Vec3[]>();
            double total = Length, half = Width / 2;
            if (Margin > 0.01)
            {
                foreach (double d in new[] { half - Margin, -(half - Margin) })
                {
                    yellow.AddRange(Band3(d - (MarginLineWidth / 2), d + (MarginLineWidth / 2), 0, total, Lift));
                }
            }

            Layout lay = Arrange();
            foreach ((double from, double to) in Runs(lay.Gaps3))
            {
                white.AddRange(Band3(-MiddleLineWidth / 2, MiddleLineWidth / 2, from, to, Lift));
            }

            foreach ((double s, double off, bool back, double scale) in lay.Arrows)
            {
                double sign = back ? -1 : 1;
                Vec3 Q(int i) => P3(s + (RoadArrow.Shape[i].X * scale * sign), off + (RoadArrow.Shape[i].Y * scale * sign), Lift);
                white.Add(new[] { Q(0), Q(1), Q(5) });
                white.Add(new[] { Q(0), Q(5), Q(6) });
                white.Add(new[] { Q(2), Q(3), Q(4) });
            }

            return (white, yellow);
        }

        /// <summary>The island as a block on the surface (3D).</summary>
        public List<Vec3[]> Island3()
        {
            var list = new List<Vec3[]>();
            (double From, double To)? island = IslandSpan;
            if (!island.HasValue)
            {
                return list;
            }

            double w = IslandWidth / 2, a = island.Value.From, b = island.Value.To;
            list.AddRange(Band3(-w, w, a, b, IslandHeight));
            foreach ((double s0, double d0, double s1, double d1) in new[] { (a, -w, b, -w), (b, -w, b, w), (b, w, a, w), (a, w, a, -w) })
            {
                Vec3 p0 = P3(s0, d0, 0), p1 = P3(s1, d1, 0), q0 = P3(s0, d0, IslandHeight), q1 = P3(s1, d1, IslandHeight);
                list.Add(new[] { p0, p1, q1 });
                list.Add(new[] { p0, q1, q0 });
            }

            return list;
        }

        /// <summary>The ramps of a package-frame element list with the heights of their marks.</summary>
        public static List<GarageRamp> All(IEnumerable<SiteElement> elements, double unitsPerMeter = 1.0)
        {
            List<SiteElement> all = elements as List<SiteElement> ?? elements.ToList();
            List<RoadLevelMark> marks = RoadElements.LevelMarks(all, unitsPerMeter);
            var list = new List<GarageRamp>();
            foreach (SiteElement e in all)
            {
                GarageRamp? r = From(e, unitsPerMeter);
                if (r != null && r.Axis.Count >= 2)
                {
                    r.ReadHeights(marks);
                    list.Add(r);
                }
            }

            return list;
        }

        /// <summary>The 3D surfaces of the ramps (generated elements for the SketchUp package).</summary>
        public static List<SiteElement> Derived(IEnumerable<SiteElement> elements, ElementCatalog catalog)
        {
            var list = new List<SiteElement>();
            ElementType? t = catalog.FindType(SurfaceType);
            if (t == null)
            {
                return list;
            }

            foreach (GarageRamp r in All(elements))
            {
                List<Vec2> left = r.Offset(r.Width / 2), right = r.Offset(-r.Width / 2);
                right.Reverse();
                var outline = left.Concat(right).Select(p => p.WithZ(0)).ToList();
                int n = 0;
                void Add(string name, List<Vec3[]> tris, string? material)
                {
                    if (tris.Count == 0)
                    {
                        return;
                    }

                    var e = new SiteElement(RoadElements.Uuid("ramp|" + r.Id + "|" + n++), SurfaceType, new PolygonGeometry(new Ring(outline)))
                    {
                        Name = name,
                        Parameters = t.CreateDefaultParameters(),
                    };
                    if (material != null)
                    {
                        e.Parameters["material"] = ParamValue.Of(material);
                    }

                    e.Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(tris));
                    list.Add(e);
                }

                (List<Vec3[]> white, List<Vec3[]> yellow) = r.Paint3();
                Add("Garažna rampa", r.Triangles().Concat(r.Sides()).ToList(), null);
                Add("Garažna rampa – bijele oznake", white, "line_paint_white");
                Add("Garažna rampa – zaštitni razmak", yellow, "line_paint_yellow");
                Add("Garažna rampa – srednje ostrvo", r.Island3(), null);
            }

            return list;
        }
    }
}
