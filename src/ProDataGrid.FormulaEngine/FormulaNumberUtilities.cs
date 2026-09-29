// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;

namespace ProDataGrid.FormulaEngine
{
    public static class FormulaNumberUtilities
    {
        public static bool TryParse(
            string text,
            FormulaCalculationSettings settings,
            out double number)
        {
            number = 0d;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var culture = settings.Culture ?? CultureInfo.InvariantCulture;
            var styles = NumberStyles.Float | NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite |
                         NumberStyles.AllowTrailingWhite;
            var trimmed = text.Trim();
            var percentSymbol = culture.NumberFormat.PercentSymbol ?? "%";
            var isPercent = !string.IsNullOrEmpty(percentSymbol) &&
                            trimmed.EndsWith(percentSymbol, StringComparison.Ordinal);
            if (isPercent)
            {
                trimmed = trimmed.Substring(0, trimmed.Length - percentSymbol.Length).Trim();
            }

            if (!double.TryParse(trimmed, styles, culture, out number))
            {
                return false;
            }

            if (isPercent)
            {
                number /= 100d;
            }

            return true;
        }

        /// <summary>Rounds to the requested significant decimal digits without an absolute zero threshold.</summary>
        /// <remarks>Nonpositive digits, 17 or more digits, zero and nonfinite values pass through.
        /// Midpoints round away from zero. A finite input is retained if its rounded value would overflow.
        /// This normalizes a binary64 value; it is not exact decimal arithmetic or Excel file serialization.</remarks>
        public static double ApplyPrecision(double value, int digits)
        {
            if (digits <= 0 || digits >= 17 || !double.IsFinite(value) || value == 0d)
            {
                return value;
            }

            var magnitude = Math.Abs(value);
            var exponent = (int)Math.Floor(Math.Log10(magnitude));
            var decade = Math.Pow(10d, exponent);
            // Log10 can round across a power-of-ten boundary. Correct its estimate with the
            // represented decade. The smallest subnormals have no representable lower decade.
            if (decade > magnitude) exponent--;
            else if (decade > 0 && magnitude >= decade * 10d) exponent++;

            var places = digits - 1 - exponent;
            double rounded;
            if (places >= 0 && places <= 15)
            {
                rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
            }
            else if (places < 0)
            {
                var scale = Math.Pow(10d, -places);
                rounded = Math.Round(value / scale, 0, MidpointRounding.AwayFromZero) * scale;
            }
            else
            {
                // Multiplying a tiny value, rather than dividing by a tiny decade, preserves
                // its significant digits. Split scales above 10^308 to include subnormal values.
                var scale = Math.Pow(10d, Math.Min(places, 308));
                var remainder = Math.Pow(10d, Math.Max(0, places - 308));
                rounded = (Math.Round((value * scale) * remainder, 0, MidpointRounding.AwayFromZero)
                    / remainder) / scale;
            }

            return double.IsFinite(rounded) ? rounded : value;
        }
    }
}
