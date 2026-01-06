using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DataGridSample.Converters
{
    public class DeltaBackgroundConverter : IValueConverter
    {
        private static readonly IBrush PositiveBrush = new SolidColorBrush(Color.FromArgb(28, 46, 204, 113));
        private static readonly IBrush NegativeBrush = new SolidColorBrush(Color.FromArgb(28, 231, 76, 60));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double delta)
            {
                if (delta > 0)
                {
                    return PositiveBrush;
                }

                if (delta < 0)
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
