using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Avalonia.Diagnostics.Converters;

internal sealed class BoolToGridLengthConverter : IValueConverter
{
    public GridLength TrueLength { get; set; } = new(1, GridUnitType.Star);

    public GridLength FalseLength { get; set; } = new(0, GridUnitType.Pixel);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var enabled = value is bool flag && flag;
        return enabled ? TrueLength : FalseLength;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GridLength length)
        {
            return length.Value > 0;
        }

        return BindingOperations.DoNothing;
    }
}
