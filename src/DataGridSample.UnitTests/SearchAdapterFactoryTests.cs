using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Headless.XUnit;
using DataGridSample.Adapters;
using Xunit;

namespace DataGridSample.Tests;

public class SearchAdapterFactoryTests
{
    [AvaloniaFact]
    public void Accessor_Factory_Returns_Core_Accessor_Search_Adapter()
    {
        var factory = new AccessorSearchAdapterFactory();
        var grid = new DataGrid();
        var model = new SearchModel();

        using var adapter = factory.Create(grid, model);

        Assert.IsType<DataGridAccessorSearchAdapter>(adapter);
    }

    [AvaloniaFact]
    public void Hierarchical_Factory_Returns_Core_Accessor_Search_Adapter()
    {
        var factory = new HierarchicalSearchAdapterFactory();
        var grid = new DataGrid();
        var model = new SearchModel();

        using var adapter = factory.Create(grid, model);

        Assert.IsType<DataGridAccessorSearchAdapter>(adapter);
    }
}
