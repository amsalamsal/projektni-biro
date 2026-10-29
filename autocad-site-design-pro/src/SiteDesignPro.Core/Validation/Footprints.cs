using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Validation
{
    /// <summary>Vertical zone used to decide which footprints can collide.</summary>
    public enum FootprintZone
    {
        /// <summary>Occupies ground-level space (furniture, poles, equipment).</summary>
        Surface,

        /// <summary>Buried body (containers, tanks, chambers).</summary>
        Underground,

        /// <summary>Ground marking (parking bays): conflicts with other markings and with fixed obstacles only.</summary>
        Marking,
    }

    /// <summary>Oriented plan rectangle occupied by a point element.</summary>
    public readonly record struct Footprint(Vec2 Center, double HalfX, double HalfY, double Angle, FootprintZone Zone)
    {
        public Vec2 AxisX => new Vec2(Math.Cos(Angle), Math.Sin(Angle));

        public Vec2 AxisY => new Vec2(-Math.Sin(Angle), Math.Cos(Angle));

        public Footprint Inflate(double d) => this with { HalfX = HalfX + d, HalfY = HalfY + d };

        public Box2 Bounds()
        {
            double ex = Math.Abs(AxisX.X * HalfX) + Math.Abs(AxisY.X * HalfY);
            double ey = Math.Abs(AxisX.Y * HalfX) + Math.Abs(AxisY.Y * HalfY);
            return new Box2(Center.X - ex, Center.Y - ey, Center.X + ex, Center.Y + ey);
        }

        public IReadOnlyList<Vec2> Corners()
        {
            Vec2 ax = AxisX * HalfX, ay = AxisY * HalfY;
            return new[] { Center - ax - ay, Center + ax - ay, Center + ax + ay, Center - ax + ay };
        }

        /// <summary>Separating-axis test; touching rectangles (within tolerance) do not intersect.</summary>
        public bool Intersects(Footprint o, double tolerance = 1e-6)
        {
            foreach (Vec2 axis in new[] { AxisX, AxisY, o.AxisX, o.AxisY })
            {
                (double minA, double maxA) = Project(Corners(), axis);
                (double minB, double maxB) = Project(o.Corners(), axis);
                if (maxA <= minB + tolerance || maxB <= minA + tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static (double Min, double Max) Project(IReadOnlyList<Vec2> pts, Vec2 axis)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (Vec2 p in pts)
            {
                double d = p.Dot(axis);
                min = Math.Min(min, d);
                max = Math.Max(max, d);
            }

            return (min, max);
        }
    }

    /// <summary>Plan footprints of point builders, derived from their parameters (metres).</summary>
    public static class Footprints
    {
        /// <summary>
        /// Returns the footprints occupied by a point element, or an empty list for elements that do not occupy
        /// space at ground level (markings, review zones, wall-mounted items above head height).
        /// </summary>
        public static IReadOnlyList<Footprint> For(string builder, PointGeometry g, ParameterSet p)
        {
            var list = new List<Footprint>();
            Vec2 c = g.Position.XY;
            double a = g.Rotation;

            void Rect(double w, double d, double offX = 0, double offY = 0, FootprintZone zone = FootprintZone.Surface)
            {
                if (!(w > 0) || !(d > 0))
                {
                    return;
                }

                Vec2 off = new Vec2(offX, offY).Rotate(a);
                list.Add(new Footprint(c + off, w / 2, d / 2, a, zone));
            }

            switch (builder)
            {
                case "tree":
                    Rect(p.Number("trunkDiameter", 0.25), p.Number("trunkDiameter", 0.25));
                    break;
                case "pole_mounted":
                    Rect(p.Number("poleDiameter", 0.12), p.Number("poleDiameter", 0.12));
                    break;
                case "bollard":
                    Rect(p.Number("diameter", 0.2), p.Number("diameter", 0.2));
                    break;
                case "bench":
                    Rect(p.Number("length", 1.8), p.Number("depth", 0.6));
                    break;
                case "table":
                    {
                        double w = p.Number("width", 0.8);
                        if (p.Bool("benches"))
                        {
                            w += 2 * p.Number("benchWidth", 0.3);
                        }

                        Rect(p.Number("length", 2), w);
                        break;
                    }

                case "bike_stand":
                    {
                        int count = Math.Max(1, (int)p.Number("count", 1));
                        double spacing = p.Number("spacing", 1);
                        Rect(p.Number("width", 0.8), ((count - 1) * spacing) + p.Number("tubeDiameter", 0.05), 0, (count - 1) * spacing / 2);
                        break;
                    }

                case "box_equipment":
                    {
                        double baseOffset = p.Number("baseOffset", 0);
                        double top = baseOffset + p.Number("height", 1);
                        if (baseOffset >= 2.0)
                        {
                            break; // mounted overhead; does not occupy ground space
                        }

                        Rect(p.Number("width", 1), p.Number("depth", 0.6), 0, 0, top <= 0 ? FootprintZone.Underground : FootprintZone.Surface);
                        break;
                    }

                case "cylinder_equipment":
                    {
                        double baseOffset = p.Number("baseOffset", 0);
                        double top = baseOffset + p.Number("height", 1);
                        if (baseOffset >= 2.0)
                        {
                            break;
                        }

                        double d = p.Number("diameter", 0.6);
                        Rect(d, d, 0, 0, top <= 0 ? FootprintZone.Underground : FootprintZone.Surface);
                        break;
                    }

                case "underground_container":
                    Rect(p.Number("bodyLength", 2), p.Number("bodyWidth", 2), 0, 0, FootprintZone.Underground);
                    if (p.Text("installation") == "semi_underground")
                    {
                        Rect(p.Number("bodyLength", 2), p.Number("bodyWidth", 2));
                    }
                    else
                    {
                        Rect(p.Number("columnDiameter", 0.8), p.Number("columnDiameter", 0.8));
                    }

                    break;
                case "chamber":
                    {
                        double outer = p.Number("innerSize", 1) + (2 * p.Number("wallThickness", 0.12));
                        Rect(outer, outer, 0, 0, FootprintZone.Underground);
                        double cover = p.Number("coverSize", 0.6);
                        Rect(cover, cover); // access cover must stay free at the surface
                        break;
                    }

                case "tank":
                    {
                        bool underground = p.Text("installation", "underground") == "underground";
                        Rect(p.Number("length", 3), p.Number("width", 1.5), 0, 0, underground ? FootprintZone.Underground : FootprintZone.Surface);
                        break;
                    }

                case "shelter":
                    Rect(p.Number("width", 3), p.Number("depth", 1.5));
                    break;
                case "parking_bay":
                    Rect(p.Number("width", 2.5), p.Number("length", 5), 0, p.Number("length", 5) / 2, FootprintZone.Marking);
                    break;
                case "sign_post":
                    if (p.Number("postCount", 1) >= 1)
                    {
                        double width = p.Number("postCount", 1) >= 2 ? p.Number("plateWidth", 0.6) : p.Number("postDiameter", 0.076);
                        Rect(width, p.Number("postDiameter", 0.076));
                    }

                    break;
                case "swing":
                    Rect(p.Number("width", 3.4), p.Number("frameSpread", 1.8));
                    break;
                case "solar_array":
                    {
                        double rows = Math.Max(1, p.Number("rows", 1));
                        double pitch = p.Number("rowSpacing", 3.5);
                        double tilt = p.Number("tilt", 25) * Math.PI / 180;
                        double depth = ((rows - 1) * pitch) + (p.Number("moduleLength", 1.7) * Math.Cos(tilt));
                        double width = Math.Max(1, p.Number("modulesPerRow", 1)) * p.Number("moduleWidth", 1.13);
                        Rect(width, depth, width / 2, depth / 2);
                        break;
                    }

                case "vawt":
                    Rect(p.Number("mastDiameter", 0.15), p.Number("mastDiameter", 0.15));
                    break;
                case "fountain":
                    Rect(p.Number("basinDiameter", 3), p.Number("basinDiameter", 3));
                    break;
                case "boulder":
                    Rect(p.Number("length", 1.2), p.Number("width", 0.9));
                    break;
                case "gate":
                    {
                        double post = p.Number("postSize", 0.1);
                        double half = (p.Number("width", 1) / 2) + (post / 2);
                        Rect(post, post, -half, 0);
                        Rect(post, post, half, 0);
                        break;
                    }

                default:
                    // shrub, arrow_marking, marker, circle_zone: no hard footprint
                    break;
            }

            return list;
        }
    }
}
