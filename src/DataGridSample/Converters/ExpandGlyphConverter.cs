// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Data.Converters;

namespace DataGridSample.Converters
{
    public class ExpandGlyphConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▾" : "▸";
            }

            return "▸";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
        {
            throw new NotSupportedException();
        }
    }
}
