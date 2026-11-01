// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using Avalonia.Controls.Utils;
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
        private static object? ConvertWithoutReflection(object? value, Type targetType, CultureInfo culture)
        {
            if (value == null || targetType.IsInstanceOfType(value))
            {
                return value;
            }

            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (targetType == typeof(string))
            {
                return System.Convert.ToString(value, culture);
            }

            if (!targetType.IsEnum && value is IConvertible && Type.GetTypeCode(targetType) != TypeCode.Object)
            {
                return System.Convert.ChangeType(value, targetType, culture);
            }

            throw DataGridReflectionOptions.CreateException("Custom value conversion; supply an explicit converter");
        }

        public static DataGridValueConverter Instance = new DataGridValueConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return DataGridReflectionOptions.IsEnabled
                ? DefaultValueConverter.Instance.Convert(value, targetType, parameter, culture)
                : ConvertWithoutReflection(value, targetType, culture);
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
            return DataGridReflectionOptions.IsEnabled
                ? DefaultValueConverter.Instance.ConvertBack(value, targetType, parameter, culture)
                : ConvertWithoutReflection(value, targetType, culture);
        }
    }
}
