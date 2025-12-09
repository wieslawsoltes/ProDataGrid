// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DataGridSample.Converters
{
    public class LevelToIndentConverter : IValueConverter
    {
        public double Indent { get; set; } = 16;

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
        {
            if (value is int level)
            {
                return new Thickness(level * Indent, 0, 0, 0);
            }

            return new Thickness(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
        {
            throw new NotSupportedException();
        }
    }
}
