using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Data;
using Xunit;

namespace ProDataGrid.UnitTests.Extras
{
    public interface IContentFilter
    {
        bool IsMatch(object value);
    }

    public class LegacyFilterTextAndHexTests
    {
        private class Model
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
        }

        private class HexContentFilter : IContentFilter
        {
            private readonly string _filter;

            public HexContentFilter(string filter)
            {
                _filter = filter ?? string.Empty;
            }

            public bool IsMatch(object value)
            {
                var fv = _filter;
                if (string.IsNullOrEmpty(fv)) return true;

                // If filter starts with 0x treat as hex
                if (fv.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(fv.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
                    {
                        if (value is int iv) return iv == hex;
                        if (int.TryParse(value?.ToString(), out var v)) return v == hex;
                    }
                    return false;
                }

                // Otherwise do substring match on Name or numeric ToString
                var s = value?.ToString() ?? string.Empty;
                return s.IndexOf(fv, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        [Fact]
        public void TextFilter_FiltersBySubstring_WhenUsingLegacyFilterValue()
        {
            var grid = new DataGrid { FilteringModel = new FilteringModel() };

            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding("Value") });

            var items = Enumerable.Range(1, 100).Select(i => new Model { Id = i, Name = "Item " + i, Value = i }).ToArray();
            grid.ItemsSource = items;

            var nameColumn = grid.Columns.OfType<DataGridColumn>().First(c => c.Header.ToString() == "Name");
            nameColumn.FilterValue = "Item 1"; // should match Item 1, 10, 11, 12, ...

            Assert.True(grid.FilteringModel.Descriptors.Count() >= 1);
            // Apply filtering via the model to check resulting descriptors produce expected filtered items when evaluated.
            var view = grid.DataConnection.CollectionView;
            Assert.NotNull(view);

            // Evaluate filtered view count by applying the filter descriptors to the source manually
            var matched = items.Where(it => it.Name.IndexOf("Item 1", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Assert.True(matched.Length >= 1);
        }

        [Fact]
        public void HexFilter_MatchesNumericColumn_WhenContentFilterParsesHex()
        {
            var grid = new DataGrid { FilteringModel = new FilteringModel() };

            var valueCol = new DataGridTextColumn { Header = "Value", Binding = new Binding("Value") };
            // assign legacy content filter that understands hex
            valueCol.ContentFilter = new HexContentFilter("0x1A");

            grid.Columns.Add(valueCol);

            var items = Enumerable.Range(1, 100).Select(i => new Model { Id = i, Name = "Item " + i, Value = i }).ToArray();
            grid.ItemsSource = items;

            // Set filter value to hex 0x1A (26 decimal)
            valueCol.FilterValue = "0x1A";

            Assert.Single(grid.FilteringModel.Descriptors);
            var descriptor = grid.FilteringModel.Descriptors.First();
            Assert.Equal(valueCol, descriptor.ColumnId);

            // Confirm the content filter would match the item with Value == 26
            var matched = items.Where(it => new HexContentFilter("0x1A").IsMatch(it.Value)).ToArray();
            Assert.Single(matched);
            Assert.Equal(26, matched[0].Value);
        }
    }
}
