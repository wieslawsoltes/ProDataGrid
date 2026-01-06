using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DataGridSample.Models;

namespace DataGridSample.Converters
{
    public class PowerFxRuleBackgroundConverter : IValueConverter
    {
        private static readonly IBrush AlertBrush = new SolidColorBrush(Color.FromArgb(35, 52, 152, 219));
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(40, 255, 193, 7));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PowerFxRuleRow row)
            {
                if (row.HasError)
                {
                    return ErrorBrush;
                }

                if (row.RuleHit)
                {
                    return AlertBrush;
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
