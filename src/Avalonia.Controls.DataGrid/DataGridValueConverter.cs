// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Avalonia.Controls
{
    internal class DataGridValueConverter : IValueConverter
    {
        public static DataGridValueConverter Instance = new DataGridValueConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Text cells convert strings to strings; both conversion paths return the value unchanged.
            if (targetType == typeof(string) && (value == null || value is string))
            {
                return value;
            }

            if (DataGridReflectionFeature.IsEnabled)
            {
                return DefaultValueConverter.Instance.Convert(value, targetType, parameter, culture);
            }

            return ConvertWithoutReflection(value, targetType, culture);
        }


        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (targetType.IsNullableType())
            {
                var strValue = value as string;

                // This suppresses a warning saying that we should use String.IsNullOrEmpty instead of a string
                // comparison, but in this case we want to explicitly check for Empty and not Null.
#pragma warning disable CA1820
                if (strValue == string.Empty)
#pragma warning restore CA1820
                {
                    return null;
                }
            }
            if (DataGridReflectionFeature.IsEnabled)
            {
                return DefaultValueConverter.Instance.ConvertBack(value, targetType, parameter, culture);
            }

            return ConvertWithoutReflection(value, targetType, culture);
        }

        private static object? ConvertWithoutReflection(object? value, Type targetType, CultureInfo culture)
        {
            if (value == null)
            {
                return targetType.IsValueType ? AvaloniaProperty.UnsetValue : null;
            }

            if (TryConvertWithoutReflection(targetType, value, culture, out var result))
            {
                return result;
            }

            var message = TypeUtilities.IsNumeric(targetType)
                ? $"'{value}' is not a valid number."
                : $"Could not convert '{value}' to '{targetType.Name}'.";
            return new BindingNotification(new InvalidCastException(message), BindingErrorType.Error);
        }

        /// <summary>
        /// Converts between primitives, enums and strings without TypeConverter lookups, for use when
        /// reflection is disabled. Types that need a TypeConverter must use a compiled binding converter.
        /// </summary>
        internal static bool TryConvertWithoutReflection(Type targetType, object value, CultureInfo culture, out object? result)
        {
            if (targetType.IsInstanceOfType(value))
            {
                result = value;
                return true;
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingType.IsInstanceOfType(value))
            {
                result = value;
                return true;
            }

            try
            {
                if (underlyingType == typeof(string))
                {
                    result = value is IFormattable formattable
                        ? formattable.ToString(null, culture)
                        : value.ToString();
                    return true;
                }

                if (underlyingType.IsEnum)
                {
                    if (value is string text)
                    {
                        result = Enum.Parse(underlyingType, text, ignoreCase: true);
                        return true;
                    }

                    if (value is IConvertible)
                    {
                        result = Enum.ToObject(underlyingType, value);
                        return true;
                    }
                }
                else if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(underlyingType))
                {
                    if (value is string text && string.IsNullOrWhiteSpace(text) && underlyingType != targetType)
                    {
                        result = null;
                        return true;
                    }

                    result = System.Convert.ChangeType(value, underlyingType, culture);
                    return true;
                }
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
            }

            result = null;
            return false;
        }
    }
}
