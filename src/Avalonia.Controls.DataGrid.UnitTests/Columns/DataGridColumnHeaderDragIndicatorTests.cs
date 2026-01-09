// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridColumnHeaderDragIndicatorTests
{
    [AvaloniaFact]
    public void DragIndicatorContent_Uses_Header_Control_For_ContentControl_Header()
    {
        var (grid, root, column) = CreateGrid();

        try
        {
            var header = GetHeaderForColumn(grid, column);

            Assert.NotNull(header.GetVisualRoot());

            var method = typeof(DataGridColumnHeader).GetMethod(
                "GetDragIndicatorContent",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var result = method!.Invoke(header, new object?[] { header.Content, header.ContentTemplate });

            Assert.IsType<Rectangle>(result);
            Assert.IsNotType<HeaderInfo>(result);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridTemplateColumn column) CreateGrid()
    {
        var items = new ObservableCollection<Row>
        {
            new("A")
        };

        var headerInfo = new HeaderInfo("Status", "templated");
        var headerControl = new ContentControl
        {
            Content = headerInfo,
            ContentTemplate = new FuncDataTemplate<HeaderInfo>((info, _) => new TextBlock
            {
                Text = info.Title
            })
        };

        var column = new DataGridTemplateColumn
        {
            Header = headerControl,
            CellTemplate = new FuncDataTemplate<Row>((row, _) => new TextBlock
            {
                Text = row.Name
            })
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items
        };

        grid.ColumnsInternal.Add(column);

        var root = new Window
        {
            Width = 300,
            Height = 200
        };

        root.SetThemeStyles();
        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, column);
    }

    private static DataGridColumnHeader GetHeaderForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
    }

    private sealed class HeaderInfo
    {
        public HeaderInfo(string title, string detail)
        {
            Title = title;
            Detail = detail;
        }

        public string Title { get; }

        public string Detail { get; }
    }

    private sealed class Row
    {
        public Row(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
