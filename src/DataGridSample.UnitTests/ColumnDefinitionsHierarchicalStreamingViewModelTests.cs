using System.ComponentModel;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Headless.XUnit;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ColumnDefinitionsHierarchicalStreamingViewModelTests
{
    [AvaloniaFact]
    public void SiblingComparer_IsDisabled_ByDefault_And_After_Clearing_Sorts()
    {
        var viewModel = new ColumnDefinitionsHierarchicalStreamingViewModel();

        Assert.Null(viewModel.Model.Options.SiblingComparer);

        var nameColumn = viewModel.ColumnDefinitions[0];
        viewModel.SortingModel.Apply(new[]
        {
            new SortingDescriptor(nameColumn, ListSortDirection.Ascending, "Item.Name")
        });

        Assert.NotNull(viewModel.Model.Options.SiblingComparer);

        viewModel.SortingModel.Clear();

        Assert.Null(viewModel.Model.Options.SiblingComparer);
    }
}
