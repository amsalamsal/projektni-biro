using System;
using System.Collections.Generic;
using System.Globalization;
using SiteDesignPro.Core.Geometry;

namespace SiteDesignPro.Core.Roads
{
    /// <summary>
    /// One road of the network: its axis and cross-section (metres). Two-way roads have their axis between the two
    /// directions (lanes left of the axis drive against the axis direction, lanes right of it with it - right-hand
    /// traffic); one-way roads have the axis in the middle and all lanes drive along the axis direction.
    /// From the axis outwards on each side: carriageway, bike lane, kerb (15 cm), sidewalk, edge (10 cm).
    /// </summary>
    public sealed class RoadSpec
    {
        public const double KerbWidth = 0.15;
        public const double EdgeWidth = 0.10;
        public const double MaxSidewalk = 5.0;
        public const double MaxBike = 2.0;

        public string Id { get; set; } = string.Empty;

        /// <summary>Axis (centre line) points in metres; arcs already tessellated.</summary>
        public List<Vec2> Axis { get; set; } = new List<Vec2>();

        public bool Closed { get; set; }

        public bool OneWay { get; set; }

        /// <summary>Two-way: lanes left of the axis (against the axis direction). Ignored for one-way roads.</summary>
        public int LanesLeft { get; set; } = 1;

        /// <summary>Two-way: lanes right of the axis (with the axis direction). One-way: all lanes.</summary>
        public int LanesRight { get; set; } = 1;

        public double LaneWidth { get; set; } = 3.0;

        public double SidewalkLeft { get; set; } = 1.6;

        public double SidewalkRight { get; set; } = 1.6;

        public double BikeLeft { get; set; }

        public double BikeRight { get; set; }

        /// <summary>Direction arrows on the carriageway.</summary>
        public bool Arrows { get; set; } = true;

        public double ArrowSpacing { get; set; } = 40;

        /// <summary>Actual kerb radius at junctions (kerb line between road level and sidewalk).</summary>
        public double CornerRadius { get; set; } = DefaultCornerRadius;

        /// <summary>
        /// Kerb radius for residential streets (up to 40 km/h) used by passenger cars and small delivery trucks:
        /// small enough to keep pedestrians safe and crossings short, large enough for a small truck to turn.
        /// </summary>
        public const double DefaultCornerRadius = 6.0;

        public int TotalLanes => OneWay ? LanesRight : LanesLeft + LanesRight;

        public double CarriagewayLeft => OneWay ? TotalLanes * LaneWidth / 2 : LanesLeft * LaneWidth;

        public double CarriagewayRight => OneWay ? TotalLanes * LaneWidth / 2 : LanesRight * LaneWidth;

        public double CarriagewayWidth => CarriagewayLeft + CarriagewayRight;

        /// <summary>Effective corner radius on the side with the wider bike lane: the carriageway edge is concentric with the kerb.</summary>
        public double EffectiveRadius => CornerRadius + Math.Max(BikeLeft, BikeRight);

        /// <summary>Distance from the axis to the outer edge of a level on one side (0 carriageway … 4 edge strip).</summary>
        public double Extent(RoadLevel level, bool left)
        {
            double c = left ? CarriagewayLeft : CarriagewayRight;
            double b = c + (left ? BikeLeft : BikeRight);
            double k = b + KerbWidth;
            double sw = left ? SidewalkLeft : SidewalkRight;
            double s = k + sw;
            double e = s + (sw > 0 ? EdgeWidth : 0);
            switch (level)
            {
                case RoadLevel.Carriageway: return c;
                case RoadLevel.Bike: return b;
                case RoadLevel.Kerb: return k;
                case RoadLevel.Sidewalk: return s;
                default: return e;
            }
        }

        /// <summary>Clamps the inputs to the supported ranges and returns what was changed.</summary>
        public List<string> Normalize()
        {
            var notes = new List<string>();
            int Clamp(int v, int lo, int hi, string what)
            {
                int r = Math.Max(lo, Math.Min(hi, v));
                if (r != v)
                {
                    notes.Add(what + " je ograničen na " + r + ".");
                }

                return r;
            }

            double ClampD(double v, double lo, double hi, string what)
            {
                if (!Compat.IsFinite(v))
                {
                    v = lo;
                }

                double r = Math.Max(lo, Math.Min(hi, v));
                if (Math.Abs(r - v) > 1e-9)
                {
                    notes.Add(what + " je ograničena na " + r.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + " m.");
                }

                return r;
            }

            LanesLeft = Clamp(LanesLeft, 1, 3, "Broj traka lijevo");
            LanesRight = Clamp(LanesRight, 1, 3, "Broj traka desno");
            LaneWidth = ClampD(LaneWidth, 2.5, 4.5, "Širina saobraćajne trake");
            SidewalkLeft = ClampD(SidewalkLeft, 0, MaxSidewalk, "Širina lijevog trotoara");
            SidewalkRight = ClampD(SidewalkRight, 0, MaxSidewalk, "Širina desnog trotoara");
            BikeLeft = ClampD(BikeLeft, 0, MaxBike, "Širina lijeve biciklističke staze");
            BikeRight = ClampD(BikeRight, 0, MaxBike, "Širina desne biciklističke staze");
            CornerRadius = ClampD(CornerRadius, 1, 30, "Radijus ivičnjaka");
            ArrowSpacing = ClampD(ArrowSpacing, 10, 200, "Razmak strelica");
            return notes;
        }

        public RoadSpec CloneParameters() => new RoadSpec
        {
            OneWay = OneWay,
            LanesLeft = LanesLeft,
            LanesRight = LanesRight,
            LaneWidth = LaneWidth,
            SidewalkLeft = SidewalkLeft,
            SidewalkRight = SidewalkRight,
            BikeLeft = BikeLeft,
            BikeRight = BikeRight,
            Arrows = Arrows,
            ArrowSpacing = ArrowSpacing,
            CornerRadius = CornerRadius,
        };

        /// <summary>Compact text form stored on the axis in the drawing ("key=value;..." with invariant numbers).</summary>
        public string ToParameterString()
        {
            string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            return "v=1;oneway=" + (OneWay ? 1 : 0) + ";ll=" + LanesLeft + ";lr=" + LanesRight + ";lw=" + N(LaneWidth) +
                   ";sl=" + N(SidewalkLeft) + ";sr=" + N(SidewalkRight) + ";bl=" + N(BikeLeft) + ";br=" + N(BikeRight) +
                   ";arrows=" + (Arrows ? 1 : 0) + ";as=" + N(ArrowSpacing) + ";r=" + N(CornerRadius);
        }

        public static RoadSpec FromParameterString(string text)
        {
            var s = new RoadSpec();
            foreach (string part in (text ?? string.Empty).Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = part.Substring(0, eq).Trim();
                string val = part.Substring(eq + 1).Trim();
                double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
                switch (key)
                {
                    case "oneway": s.OneWay = d > 0.5; break;
                    case "ll": s.LanesLeft = (int)Math.Round(d); break;
                    case "lr": s.LanesRight = (int)Math.Round(d); break;
                    case "lw": s.LaneWidth = d; break;
                    case "sl": s.SidewalkLeft = d; break;
                    case "sr": s.SidewalkRight = d; break;
                    case "bl": s.BikeLeft = d; break;
                    case "br": s.BikeRight = d; break;
                    case "arrows": s.Arrows = d > 0.5; break;
                    case "as": s.ArrowSpacing = d; break;
                    case "r": s.CornerRadius = d; break;
                }
            }

            s.Normalize();
            return s;
        }
    }

    /// <summary>Levels of the cross-section from the axis outwards.</summary>
    public enum RoadLevel
    {
        Carriageway = 0,
        Bike = 1,
        Kerb = 2,
        Sidewalk = 3,
        Edge = 4,
    }
}
