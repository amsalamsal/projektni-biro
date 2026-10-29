using System;
using System.Globalization;

namespace SiteDesignPro.Core.Text
{
    /// <summary>
    /// Bosnian (ijekavian) presentation helpers. User-facing text is written in Bosnian at the call site; this class
    /// formats numbers with a decimal comma for display and parses user input with either a comma or a point.
    /// Files (JSON, package, CSV for interchange) always use invariant numbers.
    /// </summary>
    public static class Bs
    {
        /// <summary>Formats a message (placeholders {0} ... receive already formatted values).</summary>
        public static string T(string text, params object?[] args) => args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);

        /// <summary>Number with a decimal comma and no thousands separator, e.g. 518,56.</summary>
        public static string Num(double value, int decimals = 2)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "-";
            }

            double r = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            if (r == 0)
            {
                r = 0; // no "-0,00"
            }

            return r.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Number with thousands grouped by a point (1.234,5) for large quantities in reports.</summary>
        public static string Grouped(double value, int decimals = 2)
        {
            string s = Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return s.Replace(",", "\u0001").Replace('.', ',').Replace('\u0001', '.');
        }

        public static string Meters(double value, int decimals = 2) => Num(value, decimals) + " m";

        public static string SquareMeters(double value, int decimals = 2) => Num(value, decimals) + " m²";

        public static string CubicMeters(double value, int decimals = 2) => Num(value, decimals) + " m³";

        public static string Percent(double value, int decimals = 1) => Num(value, decimals) + " %";

        /// <summary>Parses user input written with a decimal comma or point ("518,56", "518.56", "+0,25").</summary>
        public static bool TryParse(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string s = text!.Trim().Replace('−', '-').Replace(" ", string.Empty);
            if (s.StartsWith("±", StringComparison.Ordinal))
            {
                s = s.Substring(1);
            }

            int comma = s.IndexOf(',');
            int point = s.IndexOf('.');
            if (comma >= 0 && point >= 0)
            {
                // 1.234,56 (grouping point) or 1,234.56 (grouping comma): the later separator is the decimal one
                s = comma > point ? s.Replace(".", string.Empty).Replace(',', '.') : s.Replace(",", string.Empty);
            }
            else if (comma >= 0)
            {
                s = s.Replace(',', '.');
            }

            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>Count with the correct Bosnian noun form: 1 element, 2 elementa, 5 elemenata.</summary>
        public static string Count(int n, string one, string few, string many)
        {
            int mod10 = Math.Abs(n) % 10;
            int mod100 = Math.Abs(n) % 100;
            string word = mod10 == 1 && mod100 != 11 ? one : (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many);
            return n.ToString(CultureInfo.InvariantCulture) + " " + word;
        }

        public static string Elements(int n) => Count(n, "element", "elementa", "elemenata");
    }
}
