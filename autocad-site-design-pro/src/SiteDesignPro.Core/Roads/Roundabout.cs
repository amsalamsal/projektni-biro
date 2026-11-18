using System;
using System.Collections.Generic;
using SiteDesignPro.Core.Geometry;
using SiteDesignPro.Core.Model;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>Inputs of a mini roundabout (metres).</summary>
    public sealed class RoundaboutOptions
    {
        /// <summary>Centre of the roundabout.</summary>
        public Vec2 Centre { get; set; }

        /// <summary>Radius of the green central island (10–30 m).</summary>
        public double IslandRadius { get; set; } = 15;

        /// <summary>Width of the mountable apron of granite setts around the island.</summary>
        public double Apron { get; set; } = 1.2;

        /// <summary>Number of arms (0–6).</summary>
        public int Arms { get; set; } = 4;

        /// <summary>Length of the arms from the outer edge of the ring.</summary>
        public double ArmLength { get; set; } = 40;

        /// <summary>Direction of the first arm (radians, counter-clockwise from +X); the others follow evenly.</summary>
        public double FirstArmAngle { get; set; }

        /// <summary>Cross-section of the arms (and the lanes of the ring); always two-way, the median is fixed.</summary>
        public RoadSpec Arm { get; set; } = new RoadSpec();
    }

    /// <summary>One element of a roundabout: type, geometry (metres), parameters to set and name.</summary>
    public sealed class RoundaboutPart
    {
        public RoundaboutPart(string typeId, ElementGeometry geometry)
        {
            TypeId = typeId;
            Geometry = geometry;
        }

        public string TypeId { get; }

        public ElementGeometry Geometry { get; }

        public Dictionary<string, ParamValue> Parameters { get; } = new Dictionary<string, ParamValue>(StringComparer.Ordinal);

        public string Name { get; set; } = string.Empty;

        /// <summary>Role in the roundabout (ring, arm, crossing, island, …) for hosts that group the parts.</summary>
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>
    /// Mini roundabout (up to 25 km/h) as elements: green central island (radius 10–30 m) 15 cm above the asphalt with a
    /// kerb around it, truck apron of granite setts, a ring with as many lanes as enter from each arm (dashed lane line,
    /// direction arrows), arms as streets with a median 1,50 m that ends 15 m from the ring, two splitter islands of a
    /// fixed shape on both sides of the pedestrian crossing (3 m), hatched median, give-way line and triangles. The ring
    /// and the arms join the road network. Used by AutoCAD (Kružni tok) and by the SketchUp road tools.
    /// </summary>
    public static class Roundabout
    {
        /// <summary>Median of the arms: the splitter island 1,20 m at its widest and 15 cm on both sides.</summary>
        public const double SplitterMedian = 1.5;

        /// <summary>Distance from the outer edge of the ring where the median of an arm is gone.</summary>
        public const double MedianEndFromRing = 15.0;

        public const double MinIsland = 10;

        public const double MaxIsland = 30;

        public const int MaxArms = 6;

        /// <summary>The cross-section as parameters of a street element.</summary>
        public static Dictionary<string, ParamValue> StreetParameters(RoadSpec s) => new Dictionary<string, ParamValue>(StringComparer.Ordinal)
        {
            ["direction"] = ParamValue.Of(s.OneWay ? "one_way" : "two_way"),
            ["lanesLeft"] = ParamValue.Of((double)s.LanesLeft),
            ["lanesRight"] = ParamValue.Of((double)s.LanesRight),
            ["laneWidth"] = ParamValue.Of(s.LaneWidth),
            ["sidewalkLeft"] = ParamValue.Of(s.SidewalkLeft),
            ["sidewalkRight"] = ParamValue.Of(s.SidewalkRight),
            ["bikeLeft"] = ParamValue.Of(s.BikeLeft),
            ["bikeRight"] = ParamValue.Of(s.BikeRight),
            ["cornerRadius"] = ParamValue.Of(s.CornerRadius),
            ["arrows"] = ParamValue.Of(s.Arrows),
            ["arrowSpacing"] = ParamValue.Of(s.ArrowSpacing),
            ["median"] = ParamValue.Of(s.Median),
        };

        /// <summary>The clamped inputs (island radius, arms, apron) and the notes about what was clamped.</summary>
        public static List<string> Normalize(RoundaboutOptions o)
        {
            var notes = new List<string>();
            if (!Compat.IsFinite(o.IslandRadius) || o.IslandRadius < MinIsland || o.IslandRadius > MaxIsland)
            {
                o.IslandRadius = Compat.IsFinite(o.IslandRadius) ? Math.Max(MinIsland, Math.Min(MaxIsland, o.IslandRadius)) : 15;
                notes.Add("Poluprečnik srednjeg otoka je ograničen na " + Num(o.IslandRadius) + " m (mini kružni tok: 10–30 m).");
            }

            o.Apron = Compat.IsFinite(o.Apron) ? Math.Max(0, Math.Min(5, o.Apron)) : 1.2;
            o.Arms = Math.Max(0, Math.Min(MaxArms, o.Arms));
            o.ArmLength = o.Arms > 0 && Compat.IsFinite(o.ArmLength) ? Math.Max(10, Math.Min(500, o.ArmLength)) : 0;
            RoadSpec arm = o.Arm.CloneParameters();
            arm.OneWay = false;
            arm.Median = o.Arms > 0 ? SplitterMedian : 0;
            arm.MedianTo = 0;
            arm.MedianTaper = 0;
            notes.AddRange(arm.Normalize());
            o.Arm = arm;
            return notes;
        }

        /// <summary>Outer radius of the ring (carriageway edge).</summary>
        public static double OuterRadius(RoundaboutOptions o) => o.IslandRadius + o.Apron + RoadSpec.KerbWidth + (Math.Max(1, o.Arm.LanesLeft) * o.Arm.LaneWidth);

        /// <summary>All parts of the roundabout (call <see cref="Normalize"/> first).</summary>
        public static List<RoundaboutPart> Create(RoundaboutOptions o)
        {
            var parts = new List<RoundaboutPart>();
            RoadSpec arm = o.Arm;
            Vec2 c = o.Centre;
            double ri = o.IslandRadius, apron = o.Apron, armLength = o.ArmLength;
            int arms = o.Arms;
            double median = arm.Median;

            Ring Circle(double r) => new Ring(new List<Vec3> { new Vec3(c.X + r, c.Y, 0), new Vec3(c.X - r, c.Y, 0) }, new List<double> { 1.0, 1.0 });
            RoundaboutPart Add(string type, ElementGeometry g, string role, string name = "")
            {
                var p = new RoundaboutPart(type, g) { Role = role, Name = name };
                parts.Add(p);
                return p;
            }

            // the ring: as many lanes as enter from an arm, lane width of the arms; kerb 15 cm against the apron
            int lanes = Math.Max(1, arm.LanesLeft);
            double lw = arm.LaneWidth;
            double w = lanes * lw;
            double inner = ri + apron + RoadSpec.KerbWidth;
            double ro = inner + w;
            RoundaboutPart ring = Add(RoadElements.StreetType, new PathGeometry(Circle(inner + (w / 2)), true), "ring", "Kružni tok");
            var ringSpec = new RoadSpec
            {
                OneWay = true, LanesRight = lanes, LaneWidth = lw, SidewalkLeft = 0, BikeLeft = 0,
                SidewalkRight = arm.SidewalkRight, BikeRight = arm.BikeRight, CornerRadius = arm.CornerRadius, Arrows = true, ArrowSpacing = 20,
            };
            foreach (KeyValuePair<string, ParamValue> kv in StreetParameters(ringSpec))
            {
                ring.Parameters[kv.Key] = kv.Value;
            }

            // with one lane the same broken line (3 m line, 3 m gap) as on the arms runs along the middle of the ring
            if (lanes == 1)
            {
                double rc = inner + (w / 2);
                int dashes = Math.Max(4, (int)Math.Round(2 * Math.PI * rc / 6.0));
                double step = 2 * Math.PI / dashes, sweep = step / 2;
                for (int d = 0; d < dashes; d++)
                {
                    double a0 = d * step, a1 = a0 + sweep;
                    Vec3 Q(double ang) => new Vec3(c.X + (rc * Math.Cos(ang)), c.Y + (rc * Math.Sin(ang)), 0);
                    Add(RoadElements.MarkingType, new PathGeometry(new Ring(new List<Vec3> { Q(a0), Q(a1) }, new List<double> { Math.Tan(sweep / 4), 0 }), false), "ringLine", "Linija trake kružnog toka")
                        .Parameters["width"] = ParamValue.Of(0.12);
                }
            }

            // green island 15 cm above the asphalt with a kerb 15 cm around it, and the mountable apron of granite
            // setts at the level of the asphalt between the kerb and the ring
            const double islandKerb = 0.15;
            Add("landscape.lawn_area", new PolygonGeometry(Circle(ri - islandKerb)), "island", "Srednji otok kružnog toka").Parameters["topOffset"] = ParamValue.Of(0.15);
            RoundaboutPart kerb = Add("edge.kerb", new PathGeometry(Circle(ri - (islandKerb / 2)), true), "islandKerb", "Ivičnjak zelene površine kružnog toka");
            kerb.Parameters["thickness"] = ParamValue.Of(islandKerb);
            kerb.Parameters["height"] = ParamValue.Of(0.30);
            kerb.Parameters["baseOffset"] = ParamValue.Of(-0.15);
            kerb.Parameters["material"] = ParamValue.Of("concrete");
            if (apron > 0.05)
            {
                RoundaboutPart ap = Add(RoadElements.ApronType, new PolygonGeometry(Circle(ri + apron), new List<Ring> { Circle(ri) }), "apron", "Prijelazni prsten kružnog toka");
                ap.Parameters["topOffset"] = ParamValue.Of(0.0);
                ap.Parameters["material"] = ParamValue.Of("granite_setts");
            }

            for (int i = 0; i < arms; i++)
            {
                double a = o.FirstArmAngle + (i * 2 * Math.PI / arms);
                double ux = Math.Cos(a), uy = Math.Sin(a);
                Vec3 P(double along, double left) => new Vec3(c.X + ((ux * along) - (uy * left)), c.Y + ((uy * along) + (ux * left)), 0);
                PolygonGeometry Box(double s0, double s1, double y0, double y1) => new PolygonGeometry(new Ring(new List<Vec3> { P(s0, y0), P(s1, y0), P(s1, y1), P(s0, y1) }));

                // where the straight part starts: the corner arc between the arm edge and the ring ends there
                double R = arm.CornerRadius;
                double hl = arm.Extent(RoadLevel.Carriageway, true), hr = arm.Extent(RoadLevel.Carriageway, false);
                double h = Math.Max(hl, hr);
                double st = Math.Sqrt(Math.Max(0, ((ro + R) * (ro + R)) - ((h + R) * (h + R))));
                st = Math.Max(st, ro + 4);
                double m2 = median / 2;

                // behind the small splitter island the lanes of both directions come together: 15 m from the outer
                // edge of the ring there is no median any more, only one continuous centre line
                double taperFrom = st + 3.8;
                double taperTo = Math.Max(ro + MedianEndFromRing, taperFrom + 3.0);
                bool taper = median > 0 && armLength > taperTo - ro + 1;

                // the arm: a street with the median, from the ring outwards (it is joined to the ring axis)
                RoundaboutPart street = Add(RoadElements.StreetType, new PathGeometry(new Ring(new List<Vec3> { P(ro - 0.5, 0), P(ro + armLength, 0) }), false), "arm", "Krak kružnog toka " + (i + 1));
                foreach (KeyValuePair<string, ParamValue> kv in StreetParameters(arm))
                {
                    street.Parameters[kv.Key] = kv.Value;
                }

                if (taper)
                {
                    street.Parameters["medianTo"] = ParamValue.Of(taperFrom - (ro - 0.5));
                    street.Parameters["medianTaper"] = ParamValue.Of(taperTo - taperFrom);
                }

                // pedestrian crossing over the whole carriageway at the ramps (start of the straight part), 3 m wide
                Add(RoadElements.CrossingType, Box(st, st + 3, -hr, hl), "crossing", "Pješački prijelaz kružnog toka");

                // splitter islands of a fixed shape on both sides of the crossing (the refuge between them is at road
                // level): towards the ring 2,00 m long, 1,00 m wide at the crossing, 1,20 m at the end with rounded
                // corners; behind the crossing 1,00 m long, 0,60 m wide at the crossing, 0,40 m at the round end
                double bigFrom = st - 2.0;
                {
                    const double cornerR = 0.25;
                    double slope = Math.Atan2(0.1, 2.0);            // the long edges open by 10 cm over 2 m
                    double corner = (Math.PI / 2) - slope;           // inner angle at the wide end
                    double tl = cornerR / Math.Tan(corner / 2);      // tangent length of the rounding
                    double bulge = Math.Tan((Math.PI - corner) / 4);
                    double cx = Math.Cos(slope), sy = Math.Sin(slope);
                    Add("road.splitter_island", new PolygonGeometry(new Ring(
                        new List<Vec3>
                        {
                            P(st, -0.5), P(st, 0.5),
                            P(bigFrom + (tl * cx), 0.6 - (tl * sy)), P(bigFrom, 0.6 - tl),
                            P(bigFrom, -0.6 + tl), P(bigFrom + (tl * cx), -0.6 + (tl * sy)),
                        },
                        new List<double> { 0, 0, bulge, 0, bulge, 0 })), "splitter", "Razdjelni otok");
                }

                Add("road.splitter_island", new PolygonGeometry(new Ring(
                    new List<Vec3> { P(st + 3, -0.3), P(st + 3.8, -0.2), P(st + 3.8, 0.2), P(st + 3, 0.3) },
                    new List<double> { 0, 1.0, 0, 0 })), "splitter", "Razdjelni otok");

                // hatched median between the ring and the island, and a taper behind the small island
                double hw = Math.Max(0.2, m2 - 0.6);
                if (bigFrom > ro + 1.5)
                {
                    Add(RoadElements.HatchedAreaType, Box(ro + 0.8, bigFrom, -hw, hw), "hatch", "Šrafirana površina");
                }

                if (taper)
                {
                    double tw = Math.Max(0.2, m2 - 0.12);
                    Add(RoadElements.HatchedAreaType, new PolygonGeometry(new Ring(new List<Vec3> { P(taperFrom, -tw), P(taperTo - 1.0, 0), P(taperFrom, tw) })), "hatch", "Šrafirana površina");
                }
                else
                {
                    Add(RoadElements.HatchedAreaType, new PolygonGeometry(new Ring(new List<Vec3> { P(st + 4.0, -hw), P(st + 10, 0), P(st + 4.0, hw) })), "hatch", "Šrafirana površina");
                }

                // give-way line (broken, 30 cm) along the ring edge across the entry lanes (left of the arm axis:
                // right-hand traffic drives towards the ring on that side), and a triangle in every entry lane
                for (double y = m2 + 0.25; y + 0.5 <= hl - 0.1; y += 1.0)
                {
                    double s0 = Math.Sqrt(Math.Max(0, ((ro + 0.4) * (ro + 0.4)) - (y * y)));
                    double s1 = Math.Sqrt(Math.Max(0, ((ro + 0.4) * (ro + 0.4)) - ((y + 0.5) * (y + 0.5))));
                    Add(RoadElements.MarkingType, new PathGeometry(new Ring(new List<Vec3> { P(s0, y), P(s1, y + 0.5) }), false), "giveWay", "Linija ustupanja prvenstva")
                        .Parameters["width"] = ParamValue.Of(0.3);
                }

                for (int l = 0; l < arm.LanesLeft; l++)
                {
                    double yc = m2 + ((l + 0.5) * lw);
                    double sb = Math.Sqrt(Math.Max(0, (ro * ro) - (yc * yc))) + 1.5;
                    double half = Math.Min(0.75, lw * 0.3);
                    Add(RoadElements.MarkingType, new PathGeometry(new Ring(new List<Vec3> { P(sb, yc - half), P(sb, yc + half), P(sb + 3.0, yc) }), true), "triangle", "Trokut ustupanja prvenstva")
                        .Parameters["width"] = ParamValue.Of(0.12);
                }
            }

            return parts;
        }

        private static string Num(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
