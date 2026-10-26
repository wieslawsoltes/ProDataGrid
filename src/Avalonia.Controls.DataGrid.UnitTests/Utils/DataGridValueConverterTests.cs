// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Globalization;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Utils;

public class DataGridValueConverterTests
{
    [Theory]
    [InlineData("12", typeof(int), 12)]
    [InlineData("12.5", typeof(double), 12.5d)]
    [InlineData(3, typeof(long), 3L)]
    [InlineData("friday", typeof(DayOfWeek), DayOfWeek.Friday)]
    [InlineData(2, typeof(DayOfWeek), DayOfWeek.Tuesday)]
    [InlineData("7", typeof(int?), 7)]
    public void TryConvertWithoutReflection_Converts_Primitives_And_Enums(object value, Type targetType, object expected)
    {
        Assert.True(DataGridValueConverter.TryConvertWithoutReflection(targetType, value, CultureInfo.InvariantCulture, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryConvertWithoutReflection_Formats_Strings_With_Culture()
    {
        Assert.True(DataGridValueConverter.TryConvertWithoutReflection(typeof(string), 1.5m, new CultureInfo("de-DE"), out var result));
        Assert.Equal("1,5", result);
    }

    [Fact]
    public void TryConvertWithoutReflection_Maps_Blank_Text_To_Null_For_Nullable_Targets()
    {
        Assert.True(DataGridValueConverter.TryConvertWithoutReflection(typeof(int?), " ", CultureInfo.InvariantCulture, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryConvertWithoutReflection_Returns_Same_Instance_When_Assignable()
    {
        var value = new object();

        Assert.True(DataGridValueConverter.TryConvertWithoutReflection(typeof(object), value, CultureInfo.InvariantCulture, out var result));
        Assert.Same(value, result);
    }

    [Theory]
    [InlineData("abc", typeof(int))]
    [InlineData("99999999999", typeof(int))]
    [InlineData("not-a-day", typeof(DayOfWeek))]
    public void TryConvertWithoutReflection_Fails_For_Invalid_Input(object value, Type targetType)
    {
        Assert.False(DataGridValueConverter.TryConvertWithoutReflection(targetType, value, CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void TryConvertWithoutReflection_Fails_For_Types_That_Need_A_TypeConverter()
    {
        Assert.False(DataGridValueConverter.TryConvertWithoutReflection(typeof(Uri), "https://example.com", CultureInfo.InvariantCulture, out _));
    }
}
