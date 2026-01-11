using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Layout;
using DataGridSample.Models;
using System;

namespace DataGridSample.Selectors
{
    public class RowDetailsTemplateSelector : DataGrid.DataTemplateSelector
    {
        private static readonly Binding FieldBinding = new Binding(nameof(LocalSampleDetail.Field));
        private static readonly Binding ValueBinding = new Binding(nameof(LocalSampleDetail.Value));

        public override DataTemplate SelectTemplate(object item, AvaloniaObject container)
        {
            if (item is LocalSampleItem dataItem)
            {
                return CreateTemplate(dataItem);
            }

            return null;
        }

        private static DataTemplate CreateTemplate(LocalSampleItem sample)
        {
            var dt = new DataTemplate();
            dt.Content = new Func<IServiceProvider, object>(_ =>
            {
                var even = sample.Id % 2 == 0;
                var accentBackground = even ? new SolidColorBrush(Color.FromRgb(227, 242, 253)) : new SolidColorBrush(Color.FromRgb(255, 243, 224));
                var accentBorder = even ? new SolidColorBrush(Color.FromRgb(30, 136, 229)) : new SolidColorBrush(Color.FromRgb(251, 140, 0));
                var labelBrush = even ? new SolidColorBrush(Color.FromRgb(13, 71, 161)) : new SolidColorBrush(Color.FromRgb(109, 76, 65));

                var border = new Border
                {
                    Background = accentBackground,
                    BorderBrush = accentBorder,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(12),
                    Margin = new Thickness(6, 10, 6, 6),
                };

                var containerPanel = new StackPanel
                {
                    Spacing = 10,
                };

                var titleText = new TextBlock
                {
                    Text = even ? "DETAILS VIEW" : "ALT DETAILS VIEW",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = accentBorder,
                };

                var infoText = new TextBlock
                {
                    Text = sample.Info ?? "No description available.",
                    FontSize = even ? 14 : 13,
                    FontWeight = even ? FontWeight.Normal : FontWeight.Medium,
                    Foreground = labelBrush,
                    TextWrapping = TextWrapping.Wrap,
                };

                var nameText = new TextBlock
                {
                    Text = $"Name: {sample.Name ?? "Unknown"}",
                    FontSize = 12,
                    FontStyle = even ? FontStyle.Normal : FontStyle.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(66, 66, 66))
                };

                var badge = new Border
                {
                    Background = accentBorder,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = even ? $"PRIMARY · {sample.Details.Count} rows" : $"ALTERNATE · {sample.Details.Count} rows",
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    }
                };

                containerPanel.Children.Add(titleText);
                containerPanel.Children.Add(infoText);
                containerPanel.Children.Add(nameText);
                containerPanel.Children.Add(badge);
                containerPanel.Children.Add(CreateDetailsGrid(sample, even));

                border.Child = containerPanel;

                return new TemplateResult<Control>(border, null!);
            });

            return dt;
        }

        private static DataGrid CreateDetailsGrid(LocalSampleItem sample, bool even)
        {
            var background = even ? new SolidColorBrush(Color.FromRgb(250, 250, 255)) : new SolidColorBrush(Color.FromRgb(255, 251, 241));
            var rowBackground = even ? new SolidColorBrush(Color.FromRgb(235, 245, 255)) : new SolidColorBrush(Color.FromRgb(255, 244, 229));

            var grid = new DataGrid
            {
                Margin = new Thickness(0, 6, 0, 0),
                ItemsSource = sample.Details,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                CanUserResizeColumns = false,
                CanUserSortColumns = false,
                BorderThickness = new Thickness(0),
                Background = background,
                RowBackground = rowBackground,
                Height = 150
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Field",
                Binding = FieldBinding,
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Value",
                Binding = ValueBinding,
                Width = new DataGridLength(3, DataGridLengthUnitType.Star)
            });

            return grid;
        }
    }
}
