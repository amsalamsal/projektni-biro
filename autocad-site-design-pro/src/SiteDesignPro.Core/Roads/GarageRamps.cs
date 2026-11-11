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

        public bool Combined => Kind == "combined";

        /// <summary>A ramp with arcs (a combined ramp only when it has one).</summary>
        public bool Curved => Combined ? Segments().Any(g => g.Arc) : Kind != "straight";

        public int Lanes => TwoWay ? 2 : 1;

        public double Width => (Lanes * LaneWidth) + (2 * Margin);

        /// <summary>Height at the start and end (relative to the base elevation of the roads) from the marks, if any.</summary>
        public double? StartHeight { get; set; }

        public double? EndHeight { get; set; }

        /// <summary>Length through the middle (arcs exact).</summary>
        public double Length => Segments().Sum(g => g.Length);

        public double Rise => (EndHeight ?? 0) - (StartHeight ?? 0);

        /// <summary>
        /// Length of the transition at the top and at the bottom, where the slope is half of the slope between them
        /// (0: the height rises evenly from end to end).
        /// </summary>
        public double Transition { get; set; }

        /// <summary>
        /// Length of the nearly level part at the top (the street end), where the slope is at most 5 %, so a car
        /// stands almost level before it drives out (0: none).
        /// </summary>
        public double Flat { get; set; }

        public const double FlatSlope = 0.05;

        /// <summary>A slab above the ramp from this station on (0: none) with its underside at <see cref="CeilingLevel"/>.</summary>
        public double CeilingFrom { get; set; }

        /// <summary>Underside of the slab above the ramp, relative to the base elevation of the roads.</summary>
        public double CeilingLevel { get; set; }

        /// <summary>Clear height needed under the slab.</summary>
        public double MinClearance { get; set; } = 2.10;

        /// <summary>Line drain across the ramp: none, bottom or both.</summary>
        public string Drain { get; set; } = "bottom";

        /// <summary>Heated surface (hatched in the plan).</summary>
        public bool Heated { get; set; }

        /// <summary>Retaining walls with a railing along the part below the level of the top end (3D).</summary>
        public bool Walls { get; set; } = true;

        public const double WallThickness = 0.25;
        public const double RailingHeight = 1.0;
        public const double DrainWidth = 0.2;

        /// <summary>The flat part actually used: none on a ramp too short for it.</summary>
        public double FlatLength => Flat > 0.01 && Length >= Flat + 3 ? Flat : 0;

        /// <summary>The transition actually used: none on a ramp too short for two of them.</summary>
        public double TransitionLength => Transition > 0.01 && Length - FlatLength >= (2 * Transition) + 1 ? Transition : 0;

        /// <summary>Average slope (rise / length).</summary>
        public double AverageSlope => Length < 1e-9 ? 0 : Math.Abs(Rise) / Length;

        /// <summary>Slope of the flat part at the top (5 %, or the average slope of a gentler ramp).</summary>
        public double FlatPartSlope => Math.Min(FlatSlope, AverageSlope);

        /// <summary>
        /// Longitudinal slope through the middle: with a flat part or transitions, the slope of the main part (the
        /// steepest).
        /// </summary>
        public double Slope
        {
            get
            {
                double main = Length - FlatLength - TransitionLength;
                return main < 1e-9 ? 0 : (Math.Abs(Rise) - (FlatPartSlope * FlatLength)) / main;
            }
        }

        /// <summary>Slope of the transitions (half of the slope of the main part).</summary>
        public double TransitionSlope => Slope / 2;

        /// <summary>True when the top of the ramp is its start.</summary>
        public bool TopAtStart => (StartHeight ?? 0) >= (EndHeight ?? StartHeight ?? 0);

        /// <summary>
        /// The longitudinal profile: stations where the slope changes with the height there (flat part at the top,
        /// transition, main part, transition).
        /// </summary>
        public List<(double Station, double Height)> Profile()
        {
            double total = Length, h0 = StartHeight ?? 0, h1 = EndHeight ?? h0;
            double drop = Math.Abs(h1 - h0), f = FlatLength, t = TransitionLength, m = Slope, fs = FlatPartSlope;
            var down = new List<(double U, double Drop)> { (0, 0) }; // from the top end
            void Add(double u, double d)
            {
                if (u - down[down.Count - 1].U > 1e-6)
                {
                    down.Add((u, d));
                }
            }

            Add(f, fs * f);
            if (t > 0)
            {
                Add(f + t, (fs * f) + (m * t / 2));
                Add(total - t, drop - (m * t / 2));
            }

            Add(total, drop);
            down[down.Count - 1] = (total, drop);
            bool topAtStart = TopAtStart;
            double top = Math.Max(h0, h1);
            List<(double, double)> list = down.Select(x => (topAtStart ? x.U : total - x.U, top - x.Drop)).ToList();
            if (!topAtStart)
            {
                list.Reverse();
            }

            return list;
        }

        /// <summary>The parts of the profile: from, to, slope (positive number) and whether the part is the main one.</summary>
        public List<(double From, double To, double Slope)> Parts()
        {
            List<(double Station, double Height)> p = Profile();
            var list = new List<(double, double, double)>();
            for (int i = 1; i < p.Count; i++)
            {
                double len = p[i].Station - p[i - 1].Station;
                list.Add((p[i - 1].Station, p[i].Station, len < 1e-9 ? 0 : Math.Abs(p[i].Height - p[i - 1].Height) / len));
            }

            return list;
        }

        /// <summary>Clear height under the slab above the ramp at its lowest place (null: no slab) and the station of it.</summary>
        public (double Clearance, double Station)? Clearance
        {
            get
            {
                double total = Length;
                if (CeilingFrom <= 0.01 || CeilingFrom >= total - 0.01)
                {
                    return null;
                }

                double best = double.MaxValue, at = CeilingFrom;
                foreach (double st in new[] { CeilingFrom, total }.Concat(Profile().Select(x => x.Station).Where(x => x > CeilingFrom)))
                {
                    double c = CeilingLevel - HeightAt(st, 0);
                    if (c < best - 1e-9)
                    {
                        (best, at) = (c, st);
                    }
                }

                return (best, at);
            }
        }

        public bool TooLow => Clearance.HasValue && Clearance.Value.Clearance < MinClearance - 1e-9;

        public bool TooSteep => Slope > RoadLevels.MaxRampSlope + 1e-9;

        /// <summary>Allowed lane width range of the kind (straight 2,5–3,5 m, curved 3,5–5,0 m).</summary>
        public (double Min, double Max) LaneRange => Combined ? (2.5, 5.0) : Curved ? (3.5, 5.0) : (2.5, 3.5);

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
                Transition = Math.Max(0, Math.Min(8, e.Parameters.Number("transition", 0))),
                Flat = Math.Max(0, Math.Min(10, e.Parameters.Number("flatLength", 0))),
                CeilingFrom = Math.Max(0, e.Parameters.Number("ceilingFrom", 0)),
                CeilingLevel = e.Parameters.Number("ceilingLevel", 0),
                MinClearance = Math.Max(1.8, e.Parameters.Number("minClearance", 2.10)),
                Drain = e.Parameters.Text("drain", "bottom"),
                Heated = e.Parameters.Bool("heated", false),
                Walls = e.Parameters.Bool("walls", true),
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

            // where the slope changes
            bool added = false;
            foreach (double brk in Profile().Select(x => x.Station))
            {
                if (brk > from + 1e-6 && brk < to - 1e-6 && !list.Any(x => Math.Abs(x - brk) < 1e-6))
                {
                    list.Add(brk);
                    added = true;
                }
            }

            if (added)
            {
                list.Sort();
            }

            return list;
        }

        /// <summary>Height of the surface at a station, <paramref name="d"/> metres to the left of the centreline.</summary>
        public double HeightAt(double station, double d)
        {
            List<(double Station, double Height)> p = Profile();
            double s = Math.Max(0, Math.Min(p[p.Count - 1].Station, station));
            double z = p[p.Count - 1].Height;
            for (int i = 1; i < p.Count; i++)
            {
                if (s <= p[i].Station + 1e-9)
                {
                    double len = p[i].Station - p[i - 1].Station;
                    z = len < 1e-9 ? p[i].Height : p[i - 1].Height + ((p[i].Height - p[i - 1].Height) * (s - p[i - 1].Station) / len);
                    break;
                }
            }

            return z - (CrossSlope * d * CrossSide(station));
        }

        /// <summary>
        /// Which way the surface falls across at a station (+1 to the left, −1 to the right, 0 level): the inside of
        /// the curve; on a combined ramp only in its arcs, growing over 3 m from each end of the arc.
        /// </summary>
        public double CrossSide(double station)
        {
            if (!Combined)
            {
                return InnerSide;
            }

            foreach (Seg g in Segments())
            {
                if (g.Arc && station >= g.Start && station <= g.Start + g.Length)
                {
                    double run = Math.Min(3.0, g.Length / 2);
                    double f = run < 1e-9 ? 1 : Math.Min(1, Math.Min(station - g.Start, g.Start + g.Length - station) / run);
                    return (g.Sweep > 0 ? 1 : -1) * f;
                }
            }

            return 0;
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
                if (Combined || !Curved || Axis.Count < 3)
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

        private static string Num(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>"L=12,50 m", "h=2,10 m", "16,80 %" and, on a curved ramp, the cross slope.</summary>
        public List<string> Labels()
        {
            string N(double v) => Num(v);
            var list = new List<string> { "L=" + N(Length) + " m", "h=" + N(Math.Abs(Rise)) + " m", N(Slope * 100) + " %" + (TooSteep ? " > 17 %!" : string.Empty) };
            if (Curved)
            {
                list.Add("popr. " + (CrossSlope * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + (Combined ? " % u luku" : " % unutra"));
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
            double h = 0.35;
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
                // on a long ramp an arrow about every 20 m
                int count = Math.Max(1, (int)Math.Round(free / 20));
                var stations = new List<double>();
                for (int i = 0; i < count; i++)
                {
                    double s = 0.5 + (free * (i + 0.5) / count);
                    stations.Add(s);
                    stations.Add(total - s);
                }

                foreach (double s in stations)
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

        /// <summary>
        /// The direction arrow in its own frame (along the lane, across it), scaled; on a curved ramp its shaft has more
        /// points, so it follows the lane. Order: one long side of the shaft, the head (3 points), the other side back.
        /// </summary>
        private List<Vec2> ArrowShape(double scale)
        {
            IReadOnlyList<Vec2> shape = RoadArrow.Shape; // tail, shaft end, head (3), shaft end, tail
            int n = Curved ? Math.Max(1, (int)Math.Ceiling((shape[1].X - shape[0].X) * scale / 0.5)) : 1;
            var list = new List<Vec2>();
            for (int i = 0; i <= n; i++)
            {
                list.Add(new Vec2(shape[0].X + ((shape[1].X - shape[0].X) * i / n), shape[0].Y) * scale);
            }

            list.Add(shape[2] * scale);
            list.Add(shape[3] * scale);
            list.Add(shape[4] * scale);
            for (int i = n; i >= 0; i--)
            {
                list.Add(new Vec2(shape[0].X + ((shape[1].X - shape[0].X) * i / n), shape[6].Y) * scale);
            }

            return list;
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
                List<Vec2> a = ArrowShape(scale).Select(p => PointAt(s + (p.X * (back ? -1 : 1)), off + (p.Y * (back ? -1 : 1))).Point).ToList();
                list.Add(new RampShape { Role = "arrow", Points = a, Bulges = a.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
            }

            (double From, double To)? island = IslandSpan;
            if (island.HasValue)
            {
                double w = IslandWidth / 2;
                var box = new List<Vec2> { PointAt(island.Value.From, -w).Point, PointAt(island.Value.To, -w).Point, PointAt(island.Value.To, w).Point, PointAt(island.Value.From, w).Point };
                list.Add(new RampShape { Role = "island", Points = box, Bulges = box.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
            }

            RampShape Note(string note, double station, double d, double height, bool warning = false)
            {
                (Vec2 p, Vec2 dir) = PointAt(station, lay.Flip ? -d : d);
                return new RampShape { Role = "text", Text = note, Position = p, Rotation = Math.Atan2(dir.Y, dir.X) + (lay.Flip ? Math.PI : 0), Height = height, Length = note.Length * 0.68 * height, Warning = warning };
            }

            RampShape Across(string role, double station, double from, double to) =>
                new RampShape { Role = role, Points = new List<Vec2> { PointAt(station, from).Point, PointAt(station, to).Point }, Bulges = new List<double> { 0, 0 } };

            double drive = half - Margin, beside = drive - 0.45;

            // heated surface: the driving width, hatched
            if (Heated)
            {
                (List<Vec2> hl, List<double> hlb) = Sub(drive, 0, total);
                (List<Vec2> hr, List<double> hrb) = Sub(-drive, 0, total);
                var hp = new List<Vec2>(hl);
                var hb = new List<double>(hlb);
                for (int i = hr.Count - 1; i >= 0; i--)
                {
                    hp.Add(hr[i]);
                    hb.Add(i > 0 ? -hrb[i - 1] : 0);
                }

                list.Add(new RampShape { Role = "heated", Points = hp, Bulges = hb, Closed = true });
                list.Add(Note("grijana rampa", total * 0.3, beside, 0.22));
            }

            // line drains across the driving width, 30 cm from the end
            foreach ((double d0, double d1) in DrainSpans())
            {
                var grate = new List<Vec2> { PointAt(d0, -drive).Point, PointAt(d1, -drive).Point, PointAt(d1, drive).Point, PointAt(d0, drive).Point };
                list.Add(new RampShape { Role = "drain", Points = grate, Bulges = grate.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
            }

            // retaining walls: their outer faces along the part below the top
            (double From, double To)? wallSpan = WallSpan();
            if (wallSpan.HasValue)
            {
                foreach (double side in new[] { 1.0, -1.0 })
                {
                    (List<Vec2> wp, List<double> wb) = Sub(side * (half + WallThickness), wallSpan.Value.From, wallSpan.Value.To);
                    list.Add(new RampShape { Role = "wall", Points = wp, Bulges = wb });
                    list.Add(Across("wall", wallSpan.Value.From, side * half, side * (half + WallThickness)));
                    list.Add(Across("wall", wallSpan.Value.To, side * half, side * (half + WallThickness)));
                }
            }

            // the parts with a gentler slope (flat part at the top, transitions): a thin line across where the slope
            // changes and "length  slope" beside the right edge
            double mainSlope = Slope;
            foreach ((double p0, double p1, double slope) in Parts())
            {
                if (p0 > 1e-6)
                {
                    list.Add(Across("transition", p0, half, -half));
                }

                if (slope < mainSlope - 1e-6)
                {
                    RampShape note = Note(Num(p1 - p0) + " m  " + Num(slope * 100) + " %", (p0 + p1) / 2, -beside, 0.22);
                    if (note.Length < p1 - p0 - 0.3)
                    {
                        list.Add(note);
                    }
                }
            }

            // the slab above the ramp: its edge across and the clear height under it
            (double Clearance, double Station)? clear = Clearance;
            if (clear.HasValue)
            {
                list.Add(Across("ceiling", CeilingFrom, half + 0.6, -(half + 0.6)));
                string ct = "svijetla visina " + Num(clear.Value.Clearance) + " m" + (TooLow ? " < " + Num(MinClearance) + " m!" : string.Empty);
                list.Add(Note(ct, CeilingFrom + ((lay.Flip ? -1 : 1) * ((ct.Length * 0.68 * 0.22 / 2) + 0.3)), beside, 0.22, TooLow));
            }

            // the arrow of the slope (pointing uphill) beside the text in the middle
            double arrowLength = Math.Min(3.0, total * 0.3), up = TopAtStart ? -1 : 1, lateral = (lay.Flip ? -1 : 1) * 0.55;
            var slopeArrow = new List<Vec2>();
            int arrowSteps = Curved ? 6 : 1;
            for (int i = 0; i <= arrowSteps; i++)
            {
                slopeArrow.Add(PointAt((total / 2) + (up * arrowLength * ((i / (double)arrowSteps) - 0.5)), lateral).Point);
            }

            double tip = (total / 2) + (up * arrowLength / 2);
            slopeArrow.Add(PointAt(tip - (up * 0.45), lateral + 0.14).Point);
            slopeArrow.Add(PointAt(tip, lateral).Point);
            slopeArrow.Add(PointAt(tip - (up * 0.45), lateral - 0.14).Point);
            list.Add(new RampShape { Role = "slope", Points = slopeArrow, Bulges = slopeArrow.Select(_ => 0.0).ToList() });

            foreach ((double s, string text, double len) in lay.Texts)
            {
                (Vec2 p, Vec2 dir) = PointAt(s, 0);
                double rot = Math.Atan2(dir.Y, dir.X) + (lay.Flip ? Math.PI : 0);
                list.Add(new RampShape { Role = "text", Text = text, Position = p, Rotation = rot, Height = lay.TextHeight, Length = len, Warning = TooSteep });
            }

            return list;
        }

        /// <summary>
        /// A centreline through the given points with its bends rounded by arcs of one radius (smaller where the
        /// straight parts are too short): vertices and the bulge of the segment after each.
        /// </summary>
        public static (List<Vec2> Points, List<double> Bulges) FilletPath(IReadOnlyList<Vec2> points, double radius)
        {
            var pts = new List<Vec2> { points[0] };
            var bulges = new List<double> { 0 };
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vec2 a = points[i - 1], b = points[i], c = points[i + 1];
                Vec2 din = (b - a).Normalized(), dout = (c - b).Normalized();
                double turn = Math.Atan2(din.Cross(dout), din.Dot(dout));
                double room = Math.Min(a.DistanceTo(b) * (i == 1 ? 0.9 : 0.45), b.DistanceTo(c) * (i == points.Count - 2 ? 0.9 : 0.45));
                double tangent = Math.Min(room, radius * Math.Tan(Math.Abs(turn) / 2));
                if (radius < 0.1 || Math.Abs(turn) < 0.01 || Math.Abs(turn) > 3.0 || tangent < 0.05)
                {
                    pts.Add(b);
                    bulges.Add(0);
                    continue;
                }

                pts.Add(b - (din * tangent));
                bulges.Add(Math.Tan(turn / 4));
                pts.Add(b + (dout * tangent));
                bulges.Add(0);
            }

            pts.Add(points[points.Count - 1]);
            bulges.Add(0);
            return (pts, bulges);
        }

        /// <summary>Line drains across the ramp: stations of the two edges of each (30 cm from the end).</summary>
        public List<(double From, double To)> DrainSpans()
        {
            var list = new List<(double, double)>();
            double total = Length;
            if (total < 4 || (Drain != "bottom" && Drain != "both"))
            {
                return list;
            }

            (double, double) atStart = (0.3, 0.3 + DrainWidth), atEnd = (total - 0.3 - DrainWidth, total - 0.3);
            list.Add(LowAtEnd ? atEnd : atStart);
            if (Drain == "both")
            {
                list.Add(LowAtEnd ? atStart : atEnd);
            }

            return list;
        }

        /// <summary>Stations between which the ramp lies more than 5 cm below its top end (where the walls stand), or null.</summary>
        public (double From, double To)? WallSpan()
        {
            double total = Length, top = Math.Max(StartHeight ?? 0, EndHeight ?? StartHeight ?? 0);
            if (!Walls || total < 1 || Math.Abs(Rise) < 0.3)
            {
                return null;
            }

            // the height falls steadily from the top end: find where it is 5 cm below the top
            double lo = 0, hi = total;
            bool topAtStart = TopAtStart;
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                bool below = top - HeightAt(topAtStart ? mid : total - mid, 0) > 0.05;
                if (below)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid;
                }
            }

            return topAtStart ? (hi, total) : (0, total - hi);
        }

        /// <summary>
        /// Retaining walls on both sides, from the surface up to the level of the top end, along the part below it, and a
        /// railing 1,00 m high on them (3D): (walls, railing).
        /// </summary>
        public (List<Vec3[]> Walls, List<Vec3[]> Railing) Walls3()
        {
            var walls = new List<Vec3[]>();
            var rail = new List<Vec3[]>();
            (double From, double To)? span = WallSpan();
            if (!span.HasValue)
            {
                return (walls, rail);
            }

            double half = Width / 2, top = Math.Max(StartHeight ?? 0, EndHeight ?? StartHeight ?? 0);
            List<double> st = Stations(span.Value.From, span.Value.To);
            void Quad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, List<Vec3[]> to)
            {
                to.Add(new[] { a, b, c });
                to.Add(new[] { a, c, d });
            }

            Vec3 At(double s, double d, double z) => PointAt(s, d).Point.WithZ(z);
            foreach (double side in new[] { 1.0, -1.0 })
            {
                double inner = side * half, outer = side * (half + WallThickness), mid = side * (half + (WallThickness / 2));
                for (int i = 1; i < st.Count; i++)
                {
                    double s0 = st[i - 1], s1 = st[i];
                    double z0 = HeightAt(s0, inner) - SlabThickness, z1 = HeightAt(s1, inner) - SlabThickness;
                    Quad(At(s0, inner, z0), At(s1, inner, z1), At(s1, inner, top), At(s0, inner, top), walls);
                    Quad(At(s0, outer, z0), At(s1, outer, z1), At(s1, outer, top), At(s0, outer, top), walls);
                    Quad(At(s0, inner, top), At(s1, inner, top), At(s1, outer, top), At(s0, outer, top), walls);

                    // top rail 5 × 5 cm
                    double r0 = top + RailingHeight - 0.05, r1 = top + RailingHeight, w = 0.025;
                    Quad(At(s0, mid - w, r1), At(s1, mid - w, r1), At(s1, mid + w, r1), At(s0, mid + w, r1), rail);
                    Quad(At(s0, mid - w, r0), At(s1, mid - w, r0), At(s1, mid - w, r1), At(s0, mid - w, r1), rail);
                    Quad(At(s0, mid + w, r0), At(s1, mid + w, r0), At(s1, mid + w, r1), At(s0, mid + w, r1), rail);
                }

                // the two ends of the wall
                foreach (double s in new[] { span.Value.From, span.Value.To })
                {
                    double z = HeightAt(s, inner) - SlabThickness;
                    Quad(At(s, inner, z), At(s, outer, z), At(s, outer, top), At(s, inner, top), walls);
                }

                // posts about every 1,5 m
                double length = span.Value.To - span.Value.From;
                int posts = Math.Max(1, (int)Math.Round(length / 1.5));
                for (int i = 0; i <= posts; i++)
                {
                    double s = span.Value.From + (length * i / posts);
                    double a = Math.Max(span.Value.From, s - 0.025), b = Math.Min(span.Value.To, s + 0.025), w = 0.025, z1 = top + RailingHeight - 0.05;
                    Quad(At(a, mid - w, top), At(b, mid - w, top), At(b, mid - w, z1), At(a, mid - w, z1), rail);
                    Quad(At(a, mid + w, top), At(b, mid + w, top), At(b, mid + w, z1), At(a, mid + w, z1), rail);
                    Quad(At(a, mid - w, top), At(a, mid + w, top), At(a, mid + w, z1), At(a, mid - w, z1), rail);
                    Quad(At(b, mid - w, top), At(b, mid + w, top), At(b, mid + w, z1), At(b, mid - w, z1), rail);
                }
            }

            return (walls, rail);
        }

        /// <summary>
        /// The longitudinal section through the middle of the ramp, to be drawn beside the plan: x is the station, y the
        /// height above a datum line at y = 0. The slab of the ramp, verticals at every change of slope with station and
        /// height, "length  slope" on every part, the slab above with the clear height, and a title.
        /// </summary>
        public List<RampShape> Section()
        {
            var list = new List<RampShape>();
            List<(double Station, double Height)> prof = Profile();
            double total = Length;
            if (total < 1e-6 || prof.Count < 2)
            {
                return list;
            }

            double low = prof.Min(x => x.Height), high = prof.Max(x => x.Height);
            double Y(double z) => z - low + 2.0;
            RampShape Text(string text, double x, double y, double height, double rotation = 0, bool warning = false) =>
                new RampShape { Role = "text", Text = text, Position = new Vec2(x, y), Rotation = rotation, Height = height, Length = text.Length * 0.68 * height, Warning = warning };
            RampShape Line(string role, params Vec2[] pts) => new RampShape { Role = role, Points = pts.ToList(), Bulges = pts.Select(_ => 0.0).ToList() };
            string Level(double z) => Math.Abs(z) < 0.005 ? "±0,00" : (z > 0 ? "+" : "−") + Num(Math.Abs(z));

            // the slab of the ramp
            var slab = prof.Select(x => new Vec2(x.Station, Y(x.Height))).ToList();
            slab.AddRange(Enumerable.Reverse(prof).Select(x => new Vec2(x.Station, Y(x.Height) - SlabThickness)));
            list.Add(new RampShape { Role = "surface", Points = slab, Bulges = slab.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
            list.Add(new RampShape { Role = "edge", Points = slab, Bulges = slab.Select(_ => 0.0).ToList(), Closed = true });
            RampShape top = Line("middle", prof.Select(x => new Vec2(x.Station, Y(x.Height))).ToArray());
            top.Width = 0.06;
            list.Add(top);

            // datum line, verticals, stations below and heights above
            list.Add(Line("thin", new Vec2(-0.5, 0), new Vec2(total + 0.5, 0)));
            foreach ((double st, double z) in prof)
            {
                list.Add(Line("thin", new Vec2(st, -0.15), new Vec2(st, Y(z) - SlabThickness)));
                list.Add(Text(Num(st), st, -0.45, 0.22));
                list.Add(Text(Level(z), st, Y(z) + 0.45, 0.25));
            }

            // every part: its length and slope along it
            foreach ((double from, double to, double slope) in Parts())
            {
                double z0 = HeightAt(from, 0), z1 = HeightAt(to, 0);
                double rot = Math.Atan2(z1 - z0, to - from);
                bool steep = slope > RoadLevels.MaxRampSlope + 1e-9;
                RampShape t = Text("L=" + Num(to - from) + " m  " + Num(slope * 100) + " %" + (steep ? " > 17 %!" : string.Empty), (from + to) / 2, Y((z0 + z1) / 2) + 0.4, 0.22, rot, steep);
                if (t.Length > to - from - 0.2)
                {
                    t.Height *= (to - from - 0.2) / t.Length;
                    t.Length = to - from - 0.2;
                }

                if (t.Height >= 0.08)
                {
                    list.Add(t);
                }
            }

            // the slab above the ramp and the clear height under it
            (double Clearance, double Station)? clear = Clearance;
            double head = Y(high) + 1.3;
            if (clear.HasValue)
            {
                double c = Y(CeilingLevel);
                var ceiling = new List<Vec2> { new Vec2(CeilingFrom, c), new Vec2(total, c), new Vec2(total, c + 0.3), new Vec2(CeilingFrom, c + 0.3) };
                list.Add(new RampShape { Role = "surface", Points = ceiling, Bulges = ceiling.Select(_ => 0.0).ToList(), Closed = true, Fill = true });
                list.Add(new RampShape { Role = "edge", Points = ceiling, Bulges = ceiling.Select(_ => 0.0).ToList(), Closed = true });
                double x = clear.Value.Station, y0 = Y(HeightAt(x, 0));
                bool lowClear = TooLow;
                list.Add(Line(lowClear ? "warning" : "thin", new Vec2(x, y0), new Vec2(x, c)));
                list.Add(Line(lowClear ? "warning" : "thin", new Vec2(x - 0.15, y0), new Vec2(x + 0.15, y0)));
                list.Add(Line(lowClear ? "warning" : "thin", new Vec2(x - 0.15, c), new Vec2(x + 0.15, c)));
                string text = "svijetla visina " + Num(clear.Value.Clearance) + " m" + (lowClear ? " < " + Num(MinClearance) + " m!" : string.Empty);
                list.Add(Text(text, x + 0.3 + (text.Length * 0.68 * 0.22 / 2), (y0 + c) / 2, 0.22, 0, lowClear));
                head = Math.Max(head, c + 1.0);
            }

            string title = "Uzdužni presjek rampe " + Name + ":  L=" + Num(total) + " m,  h=" + Num(Math.Abs(Rise)) + " m,  najveći nagib " + Num(Slope * 100) + " %";
            RampShape tt = Text(title, total / 2, head, 0.3, 0, TooSteep);
            if (tt.Length > Math.Max(total, 12))
            {
                tt.Height *= Math.Max(total, 12) / tt.Length;
                tt.Length = Math.Max(total, 12);
            }

            list.Add(tt);
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
                List<Vec2> a = ArrowShape(scale);
                Vec3 Q(int i) => P3(s + (a[i].X * sign), off + (a[i].Y * sign), Lift);
                int m = (a.Count - 3) / 2; // points of each long side of the shaft
                for (int i = 0; i + 1 < m; i++)
                {
                    white.Add(new[] { Q(i), Q(i + 1), Q(a.Count - 2 - i) });
                    white.Add(new[] { Q(i), Q(a.Count - 2 - i), Q(a.Count - 1 - i) });
                }

                white.Add(new[] { Q(m), Q(m + 1), Q(m + 2) });
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
                (List<Vec3[]> wall, List<Vec3[]> rail) = r.Walls3();
                Add("Garažna rampa – potporni zidovi", wall, null);
                Add("Garažna rampa – ograda", rail, "steel_dark");
                Add("Garažna rampa – rešetka odvodnje", r.DrainSpans().SelectMany(x => r.Band3(-((r.Width / 2) - r.Margin), (r.Width / 2) - r.Margin, x.From, x.To, 0.004)).ToList(), "grate_steel");
            }

            return list;
        }
    }
}
