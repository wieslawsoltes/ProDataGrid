// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Avalonia.Controls.DataGridReporting
{
    internal sealed class OutlineRowTypeToFontWeightConverter : IValueConverter
    {
        public FontWeight DetailWeight { get; set; } = FontWeight.Normal;

        public FontWeight SubtotalWeight { get; set; } = FontWeight.SemiBold;

        public FontWeight GrandTotalWeight { get; set; } = FontWeight.Bold;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is OutlineRowType rowType)
            {
                return rowType switch
                {
                    OutlineRowType.Subtotal => SubtotalWeight,
                    OutlineRowType.GrandTotal => GrandTotalWeight,
                    _ => DetailWeight
                };
            }

            return DetailWeight;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
