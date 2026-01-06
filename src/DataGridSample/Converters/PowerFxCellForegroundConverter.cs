using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DataGridSample.Models;

namespace DataGridSample.Converters
{
    public class PowerFxCellForegroundConverter : IValueConverter
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(176, 90, 0));
        private static readonly IBrush NegativeBrush = new SolidColorBrush(Color.FromRgb(192, 57, 43));
        private static readonly IBrush FormulaBrush = new SolidColorBrush(Color.FromRgb(52, 73, 94));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PowerFxSheetCell cell)
            {
                if (cell.HasError)
                {
                    return ErrorBrush;
                }

                if (cell.NumericValue is double number && number < 0)
                {
                    return NegativeBrush;
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
