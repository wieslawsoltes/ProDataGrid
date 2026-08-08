using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.Models;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ReactiveUI.Avalonia;
using Xunit;

namespace DataGridSample.Tests;

public sealed class GeneratedRowDetailsViewTests
{
    [Fact]
    public void ViewModel_and_rows_use_generated_outer_and_nested_schemas()
    {
        var viewModel = new RowDetailsSelectionViewModel();

        Assert.Equal(4, viewModel.Books.Count);
        Assert.Equal(2, viewModel.ColumnDefinitions.Count);
        Assert.Equal("Title", viewModel.ColumnDefinitions[0].ColumnKey);
        Assert.Equal("InStock", viewModel.ColumnDefinitions[1].ColumnKey);

        DataGridColumnDefinitionList authorColumns = RowDetailsAuthorSchema.Instance.CreateColumnDefinitions();
        Assert.Equal(2, authorColumns.Count);
        Assert.Equal("Name", authorColumns[0].ColumnKey);
        Assert.Equal("Contribution", authorColumns[1].ColumnKey);
    }

    [AvaloniaFact]
    public void Reactive_view_builds_and_recycles_typed_nested_row_details()
    {
        var viewModel = new RowDetailsSelectionViewModel();
        var view = new RowDetailsSelectionPage(viewModel);
        var window = new Window
        {
            Width = 900,
            Height = 640,
            Content = view
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid outerGrid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.IsAssignableFrom<ReactiveUserControl<RowDetailsSelectionViewModel>>(view);
            Assert.Same(viewModel.Books, outerGrid.ItemsSource);
            Assert.Same(viewModel.ColumnDefinitions, outerGrid.ColumnDefinitionsSource);
            Assert.Equal(DataGridRowDetailsVisibilityMode.VisibleWhenSelected, outerGrid.RowDetailsVisibilityMode);
            Assert.False(outerGrid.AreRowDetailsFrozen);

            IRecyclingDataTemplate template = Assert.IsAssignableFrom<IRecyclingDataTemplate>(outerGrid.RowDetailsTemplate);
            Control details = Assert.IsAssignableFrom<Control>(template.Build(viewModel.Books[0]));
            TextBlock summary = details.GetLogicalDescendants().OfType<TextBlock>()
                .Single(control => control.Name == "GeneratedRowDetailsSummary");
            DataGrid nestedGrid = details.GetLogicalDescendants().OfType<DataGrid>()
                .Single(control => control.Name == "GeneratedNestedDataGrid");

            Assert.Equal(viewModel.Books[0].Summary, summary.Text);
            Assert.Same(viewModel.Books[0].Authors, nestedGrid.ItemsSource);
            Assert.Equal(2, nestedGrid.ColumnDefinitionsSource!.Count);
            Assert.Equal("row-details-authors-grid-host", AutomationProperties.GetAutomationId(details));
            Assert.Equal("row-details-authors-grid", AutomationProperties.GetAutomationId(nestedGrid));

            Control recycled = Assert.IsAssignableFrom<Control>(template.Build(viewModel.Books[1], details));
            Assert.Same(details, recycled);
            Assert.Equal(viewModel.Books[1].Summary, summary.Text);
            Assert.Same(viewModel.Books[1].Authors, nestedGrid.ItemsSource);

            outerGrid.UpdateLayout();
            outerGrid.SelectedIndex = 0;
            outerGrid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(
                view.GetVisualDescendants().OfType<DataGrid>(),
                control => control.Name == "GeneratedNestedDataGrid");
        }
        finally
        {
            window.Close();
        }
    }
}
