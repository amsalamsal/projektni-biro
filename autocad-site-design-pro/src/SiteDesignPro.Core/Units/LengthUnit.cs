using System;
using System.Globalization;

namespace SiteDesignPro.Core.Units
{
    /// <summary>Drawing length units supported by Site Design Pro.</summary>
    public enum LengthUnit
    {
        Millimeter,
        Centimeter,
        Meter,
        Inch,
        Foot,
    }

    /// <summary>
    /// Explicit unit conversion rules. The exchange package always stores metres; the drawing may use any
    /// supported unit. A numeric coordinate never implies a physical distance without its unit.
    /// </summary>
    public static class UnitConversion
    {
        public static double MetersPerUnit(LengthUnit unit) => unit switch
        {
            LengthUnit.Millimeter => 0.001,
            LengthUnit.Centimeter => 0.01,
            LengthUnit.Meter => 1.0,
            LengthUnit.Inch => 0.0254,
            LengthUnit.Foot => 0.3048,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit."),
        };

        public static string Code(LengthUnit unit) => unit switch
        {
            LengthUnit.Millimeter => "mm",
            LengthUnit.Centimeter => "cm",
            LengthUnit.Meter => "m",
            LengthUnit.Inch => "in",
            LengthUnit.Foot => "ft",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit."),
        };

        public static LengthUnit Parse(string code)
        {
            switch ((code ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "mm": case "millimeter": case "millimeters": case "millimetre": case "millimetres": return LengthUnit.Millimeter;
                case "cm": case "centimeter": case "centimeters": case "centimetre": case "centimetres": return LengthUnit.Centimeter;
                case "m": case "meter": case "meters": case "metre": case "metres": return LengthUnit.Meter;
                case "in": case "inch": case "inches": return LengthUnit.Inch;
                case "ft": case "foot": case "feet": return LengthUnit.Foot;
                default: throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Unknown length unit '{0}'.", code));
            }
        }

        /// <summary>Maps the AutoCAD INSUNITS system variable to a supported unit, or null when unsupported/unitless.</summary>
        public static LengthUnit? FromInsUnits(int insUnits) => insUnits switch
        {
            1 => LengthUnit.Inch,
            2 => LengthUnit.Foot,
            4 => LengthUnit.Millimeter,
            5 => LengthUnit.Centimeter,
            6 => LengthUnit.Meter,
            _ => null,
        };

        /// <summary>Maps a unit to the AutoCAD INSUNITS value.</summary>
        public static int ToInsUnits(LengthUnit unit) => unit switch
        {
            LengthUnit.Inch => 1,
            LengthUnit.Foot => 2,
            LengthUnit.Millimeter => 4,
            LengthUnit.Centimeter => 5,
            LengthUnit.Meter => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };

        /// <summary>Converts a length between units.</summary>
        public static double Convert(double value, LengthUnit from, LengthUnit to)
        {
            if (from == to)
            {
                return value;
            }

            return value * MetersPerUnit(from) / MetersPerUnit(to);
        }
    }

    /// <summary>Converts between drawing units and metres for one drawing.</summary>
    public readonly struct UnitScale
    {
        public UnitScale(LengthUnit drawingUnit)
        {
            DrawingUnit = drawingUnit;
            MetersPerDrawingUnit = UnitConversion.MetersPerUnit(drawingUnit);
        }

        public LengthUnit DrawingUnit { get; }

        public double MetersPerDrawingUnit { get; }

        public double DrawingUnitsPerMeter => 1.0 / MetersPerDrawingUnit;

        public double ToMeters(double drawingValue) => drawingValue * MetersPerDrawingUnit;

        public double ToDrawing(double meters) => meters / MetersPerDrawingUnit;
    }
}
