// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowGroupHeaderLifecycleTests
{
    [AvaloniaFact]
    public void UpdatePseudoClasses_Does_Not_Throw_After_Detach()
    {
        var (grid, root) = CreateGroupedGrid();

        try
        {
            var header = grid.GetVisualDescendants()
                .OfType<DataGridRowGroupHeader>()
                .First();

            Assert.NotNull(header.RowGroupInfo);
            Assert.Same(grid, header.OwningGrid);

            root.Content = null;
            root.UpdateLayout();

            Assert.Null(header.OwningGrid);

            var exception = Record.Exception(() => header.UpdatePseudoClasses());

            Assert.Null(exception);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root) CreateGroupedGrid()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha", "G1"),
            new("Beta", "G1"),
            new("Gamma", "G2"),
        };

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Item.Group)));

        var root = new Window
        {
            Width = 400,
            Height = 300,
            Styles =
            {
                new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var grid = new DataGrid
        {
            ItemsSource = view,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root);
    }

    private record Item(string Name, string Group);
}
