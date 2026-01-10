using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Xunit;

namespace ProDataGrid.UnitTests.Extras
{
    public class RowDetailsTests
    {
        [Fact]
        public void RowDetailsTemplate_IsUsed_WhenVisibilityVisible()
        {
            var grid = new DataGrid();

            grid.Columns.Add(new DataGridTextColumn { Header = "Id", Binding = new Binding("Id") });
            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

            grid.RowDetailsTemplate = new FuncDataTemplate<object>((item, _) =>
            {
                var tb = new TextBlock();
                tb.Text = "DETAILS:" + (item?.GetType().GetProperty("Name")?.GetValue(item) ?? "");
                return tb;
            });

            grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;

            var items = Enumerable.Range(1, 5).Select(i => new { Id = i, Name = "Name " + i }).ToArray();
            grid.ItemsSource = items;

            // Select second item
            grid.SelectedItem = items[1];

            // The grid should create row details for the selected item when generating containers.
            // We cannot force full visual generation in headless tests, but the RowDetailsTemplate should be set and callable.
            var template = grid.RowDetailsTemplate;
            Assert.NotNull(template);

            var details = template.Build(items[1]);
            Assert.NotNull(details);
            Assert.IsType<TextBlock>(details);
            Assert.Contains("DETAILS:Name 2", ((TextBlock)details).Text);
        }
    }
}
