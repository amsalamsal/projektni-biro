using System;
using System.Collections.Generic;
using System.Globalization;
using SiteDesignPro.Core.Text;

namespace SiteDesignPro.Core.Elevation
{
    /// <summary>How Z coordinates of drawing geometry are to be read.</summary>
    public enum DrawingZMode
    {
        /// <summary>Z = absolute elevation above the vertical datum (drawing units).</summary>
        Absolute,

        /// <summary>Z = elevation relative to building zero (±0,00).</summary>
        Relative,
    }

    /// <summary>Raised when an absolute elevation is requested but the project datum is not defined.</summary>
    public sealed class DatumNotDefinedException : InvalidOperationException
    {
        public DatumNotDefinedException()
            : base(Bs.T("Apsolutna kota nulte kote objekta (±0,00) nije definisana. Definišite je naredbom SDPDATUM."))
        {
        }
    }

    /// <summary>
    /// Vertical reference of the project. Nothing here has a built-in default elevation: every absolute value is
    /// supplied by the user. Building zero (±0,00) and the main parterre coincide unless the user explicitly
    /// configures a different parterre elevation.
    /// </summary>
    public sealed class ElevationDatum
    {
        /// <summary>Absolute elevation of building zero ±0,00 in metres above the vertical datum; null = not defined.</summary>
        public double? BuildingZeroAbsolute { get; set; }

        /// <summary>True (default) when the main parterre is at building zero.</summary>
        public bool ParterreEqualsBuildingZero { get; set; } = true;

        /// <summary>Parterre elevation relative to building zero in metres; used only when ParterreEqualsBuildingZero is false.</summary>
        public double ParterreRelative { get; set; }

        /// <summary>Default road level below the parterre in metres (positive = lower); null = not configured.</summary>
        public double? DefaultRoadOffsetBelowParterre { get; set; }

        /// <summary>Name of the vertical datum (e.g. "Trst 1900"); empty = not supplied.</summary>
        public string VerticalDatum { get; set; } = string.Empty;

        public DrawingZMode DrawingZ { get; set; } = DrawingZMode.Absolute;

        /// <summary>Named road levels (offset below parterre in metres) for projects with several road levels.</summary>
        public List<RoadLevel> RoadLevels { get; set; } = new List<RoadLevel>();

        public bool IsDefined => BuildingZeroAbsolute.HasValue;

        /// <summary>Parterre relative to building zero.</summary>
        public double ParterreRelativeElevation => ParterreEqualsBuildingZero ? 0.0 : ParterreRelative;

        public double? ParterreAbsolute => BuildingZeroAbsolute.HasValue ? BuildingZeroAbsolute.Value + ParterreRelativeElevation : (double?)null;

        /// <summary>Absolute = datum + relative. Throws when the datum is not defined (never assumed).</summary>
        public double ToAbsolute(double relative)
        {
            if (!BuildingZeroAbsolute.HasValue)
            {
                throw new DatumNotDefinedException();
            }

            return Round(BuildingZeroAbsolute.Value + relative);
        }

        public double ToRelative(double absolute)
        {
            if (!BuildingZeroAbsolute.HasValue)
            {
                throw new DatumNotDefinedException();
            }

            return Round(absolute - BuildingZeroAbsolute.Value);
        }

        /// <summary>Road level relative to building zero for an offset below the parterre (metres, positive = lower).</summary>
        public double RoadRelative(double offsetBelowParterre) => Round(ParterreRelativeElevation - offsetBelowParterre);

        /// <summary>Converts an assignment to a level relative to building zero.</summary>
        public double ResolveRelative(ElevationAssignment a)
        {
            switch (a.Mode)
            {
                case ElevationMode.Relative:
                    return Round(a.Value);
                case ElevationMode.Absolute:
                    return ToRelative(a.Value);
                case ElevationMode.ParterreOffset:
                    return Round(ParterreRelativeElevation + a.Value);
                case ElevationMode.RoadOffset:
                    return RoadRelative(a.Value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(a));
            }
        }

        /// <summary>Converts a level relative to building zero into the drawing's Z (metres) for the configured Z mode.</summary>
        public double DrawingZFromRelative(double relative) => DrawingZ == DrawingZMode.Relative ? relative : ToAbsolute(relative);

        public double RelativeFromDrawingZ(double z) => DrawingZ == DrawingZMode.Relative ? z : ToRelative(z);

        /// <summary>Consistency checks of the datum itself; messages are Bosnian.</summary>
        public List<string> Problems()
        {
            var p = new List<string>();
            if (!BuildingZeroAbsolute.HasValue)
            {
                p.Add(Bs.T("Nulta kota objekta (±0,00) nema definisanu apsolutnu nadmorsku visinu."));
            }
            else if (BuildingZeroAbsolute.Value < -450 || BuildingZeroAbsolute.Value > 9000)
            {
                p.Add(Bs.T("Apsolutna kota nulte kote objekta ({0} m) je izvan realnog raspona; provjerite jedinice.", Bs.Num(BuildingZeroAbsolute.Value, 2)));
            }

            if (string.IsNullOrWhiteSpace(VerticalDatum))
            {
                p.Add(Bs.T("Visinski datum nije naveden; apsolutne kote se ne mogu povezati s referentnim sistemom."));
            }

            if (!ParterreEqualsBuildingZero && Math.Abs(ParterreRelative) < 1e-9)
            {
                p.Add(Bs.T("Parter je označen kao različit od nulte kote, ali je njegova relativna visina ±0,00."));
            }

            if (DrawingZ == DrawingZMode.Absolute && !BuildingZeroAbsolute.HasValue)
            {
                p.Add(Bs.T("Z koordinate crteža su označene kao apsolutne, a nulta kota nije definisana; relativne kote se ne mogu izračunati."));
            }

            return p;
        }

        public static double Round(double v) => Math.Round(v, 6, MidpointRounding.AwayFromZero);

        /// <summary>Formats a relative level the Bosnian way: ±0,00 / +0,25 / -0,15.</summary>
        public static string FormatRelative(double relative, int decimals = 2)
        {
            double r = Math.Round(relative, decimals, MidpointRounding.AwayFromZero);
            if (Math.Abs(r) < Math.Pow(10, -decimals) / 2)
            {
                return "±" + Bs.Num(0, decimals);
            }

            return (r > 0 ? "+" : "-") + Bs.Num(Math.Abs(r), decimals);
        }

        public static string FormatAbsolute(double absolute, int decimals = 2) => Bs.Num(absolute, decimals) + " m n.v.";

        public string Describe()
        {
            string bz = BuildingZeroAbsolute.HasValue ? FormatAbsolute(BuildingZeroAbsolute.Value) : Bs.T("nije definisano");
            string parterre = ParterreEqualsBuildingZero ? Bs.T("jednak nultoj koti") : FormatRelative(ParterreRelative);
            string road = DefaultRoadOffsetBelowParterre.HasValue ? Bs.Num(DefaultRoadOffsetBelowParterre.Value * 100, 1) + " cm " + Bs.T("ispod partera") : Bs.T("nije podešeno");
            return string.Format(CultureInfo.InvariantCulture, "±0,00 = {0} | {1}: {2} | {3}: {4} | {5}: {6} | Z: {7}",
                bz, Bs.T("Parter"), parterre, Bs.T("Saobraćajnice"), road, Bs.T("Visinski datum"),
                string.IsNullOrWhiteSpace(VerticalDatum) ? Bs.T("nije naveden") : VerticalDatum,
                DrawingZ == DrawingZMode.Absolute ? Bs.T("apsolutne kote") : Bs.T("relativne kote"));
        }
    }

    public sealed class RoadLevel
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Offset below the parterre in metres (positive = lower).</summary>
        public double OffsetBelowParterre { get; set; }
    }

    public enum ElevationMode
    {
        /// <summary>Relative to building zero.</summary>
        Relative,

        /// <summary>Absolute above the vertical datum.</summary>
        Absolute,

        /// <summary>Offset from the parterre (positive = higher).</summary>
        ParterreOffset,

        /// <summary>Offset below the parterre (positive = lower) - road levels.</summary>
        RoadOffset,
    }

    /// <summary>An elevation assigned to one specific object; stored on the element and never propagated.</summary>
    public sealed class ElevationAssignment
    {
        public ElevationMode Mode { get; set; }

        public double Value { get; set; }

        /// <summary>Where it came from: "manual", or "label:&lt;handle&gt;" for an elevation label in the DWG.</summary>
        public string Source { get; set; } = "manual";

        public string Note { get; set; } = string.Empty;

        public const string PropMode = "elevation.mode";
        public const string PropValue = "elevation.value";
        public const string PropSource = "elevation.source";
        public const string PropRelative = "elevation.relative";

        public void WriteTo(IDictionary<string, string> properties, double resolvedRelative)
        {
            properties[PropMode] = Mode.ToString();
            properties[PropValue] = Value.ToString("R", CultureInfo.InvariantCulture);
            properties[PropSource] = Source;
            properties[PropRelative] = resolvedRelative.ToString("R", CultureInfo.InvariantCulture);
        }

        public static ElevationAssignment? ReadFrom(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(PropMode, out string? mode) || !properties.TryGetValue(PropValue, out string? value))
            {
                return null;
            }

            if (!Enum.TryParse(mode, out ElevationMode m) || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                return null;
            }

            return new ElevationAssignment { Mode = m, Value = v, Source = properties.TryGetValue(PropSource, out string? s) ? s : "manual" };
        }
    }
}
