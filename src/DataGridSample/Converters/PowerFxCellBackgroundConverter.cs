using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DataGridSample.Models;

namespace DataGridSample.Converters
{
    public class PowerFxCellBackgroundConverter : IValueConverter
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(45, 255, 193, 7));
        private static readonly IBrush PositiveBrush = new SolidColorBrush(Color.FromArgb(28, 46, 204, 113));
        private static readonly IBrush NegativeBrush = new SolidColorBrush(Color.FromArgb(28, 231, 76, 60));
        private static readonly IBrush FormulaBrush = new SolidColorBrush(Color.FromArgb(26, 52, 152, 219));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PowerFxSheetCell cell)
            {
                if (cell.HasError)
                {
                    return ErrorBrush;
                }

                if (cell.NumericValue is double number)
                {
                    if (number < 0)
                    {
                        return NegativeBrush;
                    }

                    if (number >= 1000)
                    {
                        return PositiveBrush;
                    }
                }

                if (cell.IsFormula)
                {
                    return FormulaBrush;
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
