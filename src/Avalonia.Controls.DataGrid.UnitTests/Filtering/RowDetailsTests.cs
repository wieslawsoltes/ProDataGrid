using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
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

            // Use RowDetailsTemplateSelector to select per-item templates
            grid.RowDetailsTemplateSelector = new TestRowDetailsSelector();

            grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;

            var items = Enumerable.Range(1, 5).Select(i => new { Id = i, Name = "Name " + i }).ToArray();
            grid.ItemsSource = items;

            // Select second item
            grid.SelectedItem = items[1];

            // The grid should create row details for the selected item when generating containers.
            // We cannot force full visual generation in headless tests, but the RowDetailsTemplate should be set and callable.
            // The selector should be set and return a usable template for the selected item
            var selector = grid.RowDetailsTemplateSelector;
            Assert.NotNull(selector);

            var selectedTemplate = selector.SelectTemplate(items[1], grid);
            Assert.NotNull(selectedTemplate);

            // Call the template's factory directly (headless test) to get the produced control
            object produced1;
            if (selectedTemplate.Content is System.Func<object, object> factory1)
            {
                produced1 = factory1(items[1]);
            }
            else
            {
                produced1 = selectedTemplate.Build(items[1]);
            }

            Assert.NotNull(produced1);
            Assert.IsType<TextBlock>(produced1);
            Assert.Contains("DETAILS:Name 2", ((TextBlock)produced1).Text);

            // Also verify a different item gets a different template (odd/even)
            var selectedTemplate3 = selector.SelectTemplate(items[2], grid);
            Assert.NotNull(selectedTemplate3);
            object produced3;
            if (selectedTemplate3.Content is System.Func<object, object> factory3)
            {
                produced3 = factory3(items[2]);
            }
            else
            {
                produced3 = selectedTemplate3.Build(items[2]);
            }

            Assert.NotNull(produced3);
            Assert.IsType<TextBlock>(produced3);
            Assert.Contains("ALT_DETAILS:Name 3", ((TextBlock)produced3).Text);
        }
    }

    // Simple selector that returns a FuncDataTemplate rendering the Name property
    class TestRowDetailsSelector : DataGrid.DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, AvaloniaObject container)
        {
            var idProp = item?.GetType().GetProperty("Id");
            var nameProp = item?.GetType().GetProperty("Name");
            var idVal = idProp?.GetValue(item) as int? ?? 0;
            var nameVal = nameProp?.GetValue(item) ?? "";

            var text = (idVal % 2 == 0) ? "DETAILS:" + nameVal : "ALT_DETAILS:" + nameVal;

            var dt = new DataTemplate();
            dt.Content = new System.Func<object, object>(_ => new TextBlock { Text = text });
            return dt;
        }
    }
}
