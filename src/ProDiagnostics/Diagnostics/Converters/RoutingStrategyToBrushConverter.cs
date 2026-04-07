using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Avalonia.Diagnostics.Converters;

internal sealed class RoutingStrategyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RoutingStrategies routingStrategies)
        {
            return Brushes.Gray;
        }

        if ((routingStrategies & RoutingStrategies.Bubble) != 0)
        {
            return Brushes.SteelBlue;
        }

        if ((routingStrategies & RoutingStrategies.Tunnel) != 0)
        {
            return Brushes.OrangeRed;
        }

        if ((routingStrategies & RoutingStrategies.Direct) != 0)
        {
            return Brushes.SeaGreen;
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
