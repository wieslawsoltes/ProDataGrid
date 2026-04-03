// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartValueFormatterTests
    {
        [Fact]
        public void ChartValueFormatter_Rounds_To_Configured_Maximum_Fraction_Digits()
        {
            var format = new ChartValueFormat
            {
                MinimumFractionDigits = 0,
                MaximumFractionDigits = 2,
                RoundingMode = MidpointRounding.AwayFromZero,
                Culture = CultureInfo.InvariantCulture
            };

            var result = ChartValueFormatter.Format(79.14999999999999d, format);

            Assert.Equal("79.15", result);
        }

        [Fact]
        public void ChartValueFormatter_Honors_Minimum_And_Maximum_Fraction_Digits_With_Grouping()
        {
            var format = new ChartValueFormat
            {
                MinimumFractionDigits = 2,
                MaximumFractionDigits = 4,
                RoundingMode = MidpointRounding.AwayFromZero,
                UseGrouping = true,
                Culture = CultureInfo.InvariantCulture
            };

            Assert.Equal("12,345.20", ChartValueFormatter.Format(12345.2d, format));
            Assert.Equal("12,345.2346", ChartValueFormatter.Format(12345.23456d, format));
        }

        [Fact]
        public void ChartValueFormatter_Applies_Custom_Format_And_Affixes()
        {
            var format = new ChartValueFormat
            {
                FormatString = "0.00",
                MaximumFractionDigits = 2,
                Prefix = "$",
                Suffix = " USD",
                RoundingMode = MidpointRounding.AwayFromZero,
                Culture = CultureInfo.InvariantCulture
            };

            var result = ChartValueFormatter.Format(12.345d, format);

            Assert.Equal("$12.35 USD", result);
        }
    }
}
