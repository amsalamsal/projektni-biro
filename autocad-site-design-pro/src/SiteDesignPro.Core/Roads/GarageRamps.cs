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
    public sealed class GarageRamp
    {
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

        public bool Curved => Kind != "straight";

        public int Lanes => TwoWay ? 2 : 1;

        public double Width => (Lanes * LaneWidth) + (2 * Margin);

        /// <summary>Height at the start and end (relative to the base elevation of the roads) from the marks, if any.</summary>
        public double? StartHeight { get; set; }

        public double? EndHeight { get; set; }

        public double Length
        {
            get
            {
                double l = 0;
                for (int i = 1; i < Axis.Count; i++)
                {
                    l += Axis[i - 1].DistanceTo(Axis[i]);
                }

                return l;
            }
        }

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
        public (Vec2 Point, Vec2 Direction) At(double station)
        {
            double run = 0;
            for (int i = 1; i < Axis.Count; i++)
            {
                double l = Axis[i - 1].DistanceTo(Axis[i]);
                if (run + l >= station || i == Axis.Count - 1)
                {
                    double t = l < 1e-9 ? 0 : Math.Max(0, Math.Min(1, (station - run) / l));
                    return (Axis[i - 1] + ((Axis[i] - Axis[i - 1]) * t), (Axis[i] - Axis[i - 1]).Normalized());
                }

                run += l;
            }

            return (Axis[0], new Vec2(1, 0));
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
        public List<Vec3[]> Triangles()
        {
            var list = new List<Vec3[]>();
            double h0 = StartHeight ?? 0, h1 = EndHeight ?? h0;
            double total = Length;
            List<Vec2> left = Offset(Width / 2), right = Offset(-Width / 2);
            int inner = InnerSide;
            double run = 0;
            Vec3 P(List<Vec2> side, int i, double s, double lateral)
            {
                double z = total < 1e-9 ? h0 : h0 + ((h1 - h0) * s / total);
                z -= inner == 0 ? 0 : CrossSlope * (Width / 2) * (lateral * inner);
                return side[i].WithZ(z);
            }

            for (int i = 1; i < Axis.Count; i++)
            {
                double s0 = run;
                run += Axis[i - 1].DistanceTo(Axis[i]);
                Vec3 l0 = P(left, i - 1, s0, 1), l1 = P(left, i, run, 1), r0 = P(right, i - 1, s0, -1), r1 = P(right, i, run, -1);
                list.Add(new[] { r0, r1, l1 });
                list.Add(new[] { r0, l1, l0 });
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
                var e = new SiteElement(RoadElements.Uuid("ramp|" + r.Id), SurfaceType, new PolygonGeometry(new Ring(outline)))
                {
                    Name = "Garažna rampa",
                    Parameters = t.CreateDefaultParameters(),
                };
                e.Custom["triangles"] = ParamValue.Of(RoadLevels.Encode(r.Triangles()));
                list.Add(e);
            }

            return list;
        }
    }
}
