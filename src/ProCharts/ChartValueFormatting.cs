// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.ComponentModel;
using System.Globalization;

namespace ProCharts
{
    /// <summary>
    /// Defines numeric display formatting for chart values when a custom formatter delegate is not supplied.
    /// </summary>
    public sealed class ChartValueFormat : INotifyPropertyChanged, IEquatable<ChartValueFormat>
    {
        private string? _formatString;
        private int? _minimumFractionDigits;
        private int? _maximumFractionDigits;
        private MidpointRounding _roundingMode = MidpointRounding.AwayFromZero;
        private bool _useGrouping;
        private string? _prefix;
        private string? _suffix;
        private CultureInfo? _culture;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets an optional .NET numeric format string.
        /// When specified, this is applied after any configured rounding.
        /// </summary>
        public string? FormatString
        {
            get => _formatString;
            set
            {
                if (string.Equals(_formatString, value, StringComparison.Ordinal))
                {
                    return;
                }

                _formatString = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormatString)));
            }
        }

        /// <summary>
        /// Gets or sets the minimum number of fractional digits to render.
        /// </summary>
        public int? MinimumFractionDigits
        {
            get => _minimumFractionDigits;
            set
            {
                var normalized = NormalizeFractionDigits(value);
                if (_minimumFractionDigits == normalized)
                {
                    return;
                }

                _minimumFractionDigits = normalized;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinimumFractionDigits)));
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of fractional digits to render.
        /// Values are rounded using <see cref="RoundingMode"/> before formatting.
        /// </summary>
        public int? MaximumFractionDigits
        {
            get => _maximumFractionDigits;
            set
            {
                var normalized = NormalizeFractionDigits(value);
                if (_maximumFractionDigits == normalized)
                {
                    return;
                }

                _maximumFractionDigits = normalized;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaximumFractionDigits)));
            }
        }

        /// <summary>
        /// Gets or sets the midpoint rounding mode used when <see cref="MaximumFractionDigits"/> is specified.
        /// </summary>
        public MidpointRounding RoundingMode
        {
            get => _roundingMode;
            set
            {
                if (_roundingMode == value)
                {
                    return;
                }

                _roundingMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RoundingMode)));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether grouped thousands separators should be used.
        /// </summary>
        public bool UseGrouping
        {
            get => _useGrouping;
            set
            {
                if (_useGrouping == value)
                {
                    return;
                }

                _useGrouping = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseGrouping)));
            }
        }

        /// <summary>
        /// Gets or sets optional text that is prefixed to formatted values.
        /// </summary>
        public string? Prefix
        {
            get => _prefix;
            set
            {
                if (string.Equals(_prefix, value, StringComparison.Ordinal))
                {
                    return;
                }

                _prefix = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Prefix)));
            }
        }

        /// <summary>
        /// Gets or sets optional text that is appended to formatted values.
        /// </summary>
        public string? Suffix
        {
            get => _suffix;
            set
            {
                if (string.Equals(_suffix, value, StringComparison.Ordinal))
                {
                    return;
                }

                _suffix = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Suffix)));
            }
        }

        /// <summary>
        /// Gets or sets the culture used for numeric formatting.
        /// When omitted, the formatter falls back to the current culture.
        /// </summary>
        public CultureInfo? Culture
        {
            get => _culture;
            set
            {
                if (string.Equals(_culture?.Name, value?.Name, StringComparison.Ordinal))
                {
                    return;
                }

                _culture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            }
        }

        /// <inheritdoc />
        public bool Equals(ChartValueFormat? other)
        {
            return other != null &&
                   string.Equals(FormatString, other.FormatString, StringComparison.Ordinal) &&
                   MinimumFractionDigits == other.MinimumFractionDigits &&
                   MaximumFractionDigits == other.MaximumFractionDigits &&
                   RoundingMode == other.RoundingMode &&
                   UseGrouping == other.UseGrouping &&
                   string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
                   string.Equals(Suffix, other.Suffix, StringComparison.Ordinal) &&
                   string.Equals(Culture?.Name, other.Culture?.Name, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ChartValueFormat other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (FormatString?.GetHashCode() ?? 0);
                hash = (hash * 31) + (MinimumFractionDigits?.GetHashCode() ?? 0);
                hash = (hash * 31) + (MaximumFractionDigits?.GetHashCode() ?? 0);
                hash = (hash * 31) + RoundingMode.GetHashCode();
                hash = (hash * 31) + UseGrouping.GetHashCode();
                hash = (hash * 31) + (Prefix?.GetHashCode() ?? 0);
                hash = (hash * 31) + (Suffix?.GetHashCode() ?? 0);
                hash = (hash * 31) + (Culture?.Name?.GetHashCode() ?? 0);
                return hash;
            }
        }

        private static int? NormalizeFractionDigits(int? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value < 0)
            {
                return 0;
            }

            return value.Value > 15 ? 15 : value.Value;
        }
    }

    /// <summary>
    /// Formats numeric chart values using <see cref="ChartValueFormat"/>.
    /// </summary>
    public static class ChartValueFormatter
    {
        /// <summary>
        /// Formats the supplied value using the provided chart format options.
        /// </summary>
        public static string Format(double value, ChartValueFormat? format, CultureInfo? fallbackCulture = null)
        {
            var culture = format?.Culture ?? fallbackCulture ?? CultureInfo.CurrentCulture;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return value.ToString("G", culture);
            }

            if (format == null)
            {
                return value.ToString("G", culture);
            }

            var roundedValue = ApplyRounding(value, format);
            string text;
            if (!string.IsNullOrWhiteSpace(format.FormatString))
            {
                text = roundedValue.ToString(format.FormatString, culture);
            }
            else
            {
                var minimumFractionDigits = NormalizeFractionDigits(format.MinimumFractionDigits);
                var maximumFractionDigits = NormalizeMaximumFractionDigits(format.MaximumFractionDigits, minimumFractionDigits);
                if (minimumFractionDigits.HasValue || maximumFractionDigits.HasValue)
                {
                    text = roundedValue.ToString(
                        BuildPattern(minimumFractionDigits ?? 0, maximumFractionDigits ?? minimumFractionDigits ?? 0, format.UseGrouping),
                        culture);
                }
                else
                {
                    text = roundedValue.ToString("G", culture);
                }
            }

            if (!string.IsNullOrEmpty(format.Prefix))
            {
                text = format.Prefix + text;
            }

            if (!string.IsNullOrEmpty(format.Suffix))
            {
                text += format.Suffix;
            }

            return text;
        }

        private static double ApplyRounding(double value, ChartValueFormat format)
        {
            var maximumFractionDigits = NormalizeMaximumFractionDigits(format.MaximumFractionDigits, NormalizeFractionDigits(format.MinimumFractionDigits));
            if (!maximumFractionDigits.HasValue)
            {
                return value;
            }

            return Math.Round(value, maximumFractionDigits.Value, format.RoundingMode);
        }

        private static int? NormalizeFractionDigits(int? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            if (value.Value < 0)
            {
                return 0;
            }

            return value.Value > 15 ? 15 : value.Value;
        }

        private static int? NormalizeMaximumFractionDigits(int? maximumFractionDigits, int? minimumFractionDigits)
        {
            var normalizedMaximum = NormalizeFractionDigits(maximumFractionDigits);
            if (normalizedMaximum.HasValue && minimumFractionDigits.HasValue && normalizedMaximum.Value < minimumFractionDigits.Value)
            {
                return minimumFractionDigits.Value;
            }

            return normalizedMaximum ?? minimumFractionDigits;
        }

        private static string BuildPattern(int minimumFractionDigits, int maximumFractionDigits, bool useGrouping)
        {
            var integral = useGrouping ? "#,##0" : "0";
            if (maximumFractionDigits <= 0)
            {
                return integral;
            }

            var requiredDigits = minimumFractionDigits > maximumFractionDigits ? maximumFractionDigits : minimumFractionDigits;
            var optionalDigits = Math.Max(0, maximumFractionDigits - requiredDigits);
            return integral +
                   "." +
                   new string('0', requiredDigits) +
                   new string('#', optionalDigits);
        }
    }
}
