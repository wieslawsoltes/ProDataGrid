// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnChooser;

public class DataGridColumnChooserItemTests
{
    [AvaloniaFact]
    public void Chooser_Builds_Items_For_Inline_Columns()
    {
        var grid = new DataGrid();
        grid.Columns.Add(new DataGridTextColumn { Header = "First" });
        grid.Columns.Add(new DataGridTextColumn { Header = "Second" });

        var chooser = new DataGridColumnChooser
        {
            DataGrid = grid
        };

        Assert.Equal(2, chooser.ColumnItems.Count);
        Assert.Equal(new[] { "First", "Second" }, chooser.ColumnItems.Select(i => i.Header));
    }

    [AvaloniaFact]
    public void ChooserItem_Toggles_Definition_Visibility()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name),
            CanUserHide = true
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = GetNonFillerColumns(grid).Single();
        var item = new DataGridColumnChooserItem(column);

        item.IsVisible = false;

        Assert.True(definition.IsVisible.HasValue);
        Assert.False(definition.IsVisible!.Value);
        Assert.False(column.IsVisible);
        Assert.False(item.IsVisible);
    }

    [AvaloniaFact]
    public void ChooserItem_Updates_When_Definition_CanUserHide_Changes()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name),
            CanUserHide = true
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition> { definition }
        };

        var column = GetNonFillerColumns(grid).Single();
        var item = new DataGridColumnChooserItem(column);

        var propertyRaised = false;
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DataGridColumnChooserItem.CanUserHide))
            {
                propertyRaised = true;
            }
        };

        definition.CanUserHide = false;

        Assert.True(propertyRaised);
        Assert.False(item.CanUserHide);
    }

    private static System.Collections.Generic.List<DataGridColumn> GetNonFillerColumns(DataGrid grid)
    {
        return grid.ColumnsInternal.ItemsInternal
            .Where(column => column is not DataGridFillerColumn)
            .ToList();
    }

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
    }
}
