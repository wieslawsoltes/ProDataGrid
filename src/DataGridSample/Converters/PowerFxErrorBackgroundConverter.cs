using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DataGridSample.Converters
{
    public class PowerFxErrorBackgroundConverter : IValueConverter
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(40, 255, 193, 7));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool hasError && hasError)
            {
                return ErrorBrush;
            }

            return AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
