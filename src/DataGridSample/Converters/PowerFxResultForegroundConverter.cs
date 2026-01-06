using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DataGridSample.Converters
{
    public class PowerFxResultForegroundConverter : IValueConverter
    {
        private static readonly IBrush PositiveBrush = new SolidColorBrush(Color.FromRgb(39, 174, 96));
        private static readonly IBrush NegativeBrush = new SolidColorBrush(Color.FromRgb(192, 57, 43));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double result)
            {
                if (result > 0)
                {
                    return PositiveBrush;
                }

                if (result < 0)
                {
                    return NegativeBrush;
                }
            }

            return AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
