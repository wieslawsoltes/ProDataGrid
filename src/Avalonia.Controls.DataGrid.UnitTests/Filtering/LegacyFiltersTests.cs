using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Data;
using Xunit;

namespace Avalonia.Controls.DataGridTests.LegacyFiltering
{
    public class LegacyFiltersTests
    {
        [Fact]
        public void ColumnFilterValue_IsTranslatedIntoFilteringDescriptor()
        {
            var grid = new DataGrid
            {
                FilteringModel = new FilteringModel(),
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding("Name")
            });

            grid.ItemsSource = new[]
            {
                new { Name = "Alpha" },
            };

            var column = grid.Columns.OfType<DataGridColumn>().First();
            column.FilterValue = "Al";

            Assert.Single(grid.FilteringModel.Descriptors);
            var descriptor = grid.FilteringModel.Descriptors.First();
            Assert.Equal(column, descriptor.ColumnId);
            Assert.Equal(FilteringOperator.Custom, descriptor.Operator);
        }

        [Fact]
        public void ClearingFilterValue_RemovesDescriptor()
        {
            var grid = new DataGrid
            {
                FilteringModel = new FilteringModel(),
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding("Name")
            });

            grid.ItemsSource = new[]
            {
                new { Name = "Beta" },
            };

            var column = grid.Columns.OfType<DataGridColumn>().First();
            column.FilterValue = "Be";
            Assert.Single(grid.FilteringModel.Descriptors);

            column.FilterValue = null;

            Assert.Empty(grid.FilteringModel.Descriptors);
        }
    }
}
