// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRealizationFactoryTests
{
    [AvaloniaFact]
    public void Factory_Creates_And_Partitions_Flat_Row_Cell_And_Header_Containers()
    {
        var items = Enumerable.Range(0, 100).Select(index => new Item(index)).ToList();
        var factory = new PartitionedFactory();
        var (window, grid) = CreateGrid(items, factory, columnCount: 3);
        grid.FrozenColumnCount = 1;
        grid.FrozenColumnCountRight = 1;

        window.Show();
        grid.UpdateLayout();

        AssertRealizedContainerTypes(grid);
        DataGridColumnHeader[] headers = grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .Where(header => header.OwningColumn is not null)
            .ToArray();
        Assert.Contains(headers, header => header is LeftHeader);
        Assert.Contains(headers, header => header is CenterHeader);
        Assert.Contains(headers, header => header is RightHeader);

        DataGridColumnHeader[] frozenHeaders = headers
            .Where(header => header is LeftHeader or RightHeader)
            .ToArray();
        grid.FrozenColumnCount = 0;
        grid.FrozenColumnCountRight = 0;
        grid.UpdateLayout();

        Assert.All(
            grid.GetVisualDescendants()
                .OfType<DataGridColumnHeader>()
                .Where(header => header.OwningColumn is not null),
            header => Assert.IsType<CenterHeader>(header));
        Assert.DoesNotContain(frozenHeaders, header => header.IsAttachedToVisualTree());

        grid.ScrollIntoView(items[80], grid.ColumnsInternal[0]);
        grid.UpdateLayout();

        AssertRealizedContainerTypes(grid);
        Assert.True(factory.RowContexts > 0);
        Assert.True(factory.CellContexts > 0);
        Assert.True(factory.HeaderContexts >= 3);
    }

    [AvaloniaFact]
    public void Factory_Receives_Hierarchy_Node_And_Application_Item()
    {
        var root = new HierarchyItem(0);
        root.Children.Add(new HierarchyItem(1));
        root.Children.Add(new HierarchyItem(2));
        root.Children[0].Children.Add(new HierarchyItem(3));
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((HierarchyItem)item).Children,
            IsLeafSelector = item => ((HierarchyItem)item).Children.Count == 0,
        });
        model.SetRoot(root);
        model.ExpandAll();
        var factory = new HierarchyFactory();
        var grid = new DataGrid
        {
            Width = 420,
            Height = 220,
            AutoGenerateColumns = false,
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            ItemsSource = model.ObservableFlattened,
            RealizationFactory = factory,
            UseLogicalScrollable = true,
            RowHeight = 24,
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(HierarchyItem.Id)),
        });
        var window = new Window { Width = 440, Height = 240 };
        window.SetThemeStyles();
        window.Content = grid;

        window.Show();
        grid.UpdateLayout();

        Assert.NotEmpty(grid.GetVisualDescendants().OfType<HierarchyRow>());
        Assert.NotEmpty(grid.GetVisualDescendants().OfType<HierarchyCell>());
        Assert.True(factory.SawHierarchicalNode);
        Assert.True(factory.SawApplicationItem);
    }

    [AvaloniaFact]
    public void Replacing_Factory_Regenerates_Containers_Without_Mixing_Pools()
    {
        var items = Enumerable.Range(0, 30).Select(index => new Item(index)).ToList();
        var firstFactory = new GenerationFactory(firstGeneration: true);
        var (window, grid) = CreateGrid(items, firstFactory, columnCount: 1);

        window.Show();
        grid.UpdateLayout();
        grid.SelectedItem = items[2];
        grid.UpdateLayout();
        FirstRow[] firstRows = grid.GetVisualDescendants().OfType<FirstRow>().ToArray();
        FirstCell[] firstCells = grid.GetVisualDescendants().OfType<FirstCell>().ToArray();
        FirstHeader[] firstHeaders = grid.GetVisualDescendants().OfType<FirstHeader>().ToArray();
        Assert.NotEmpty(firstRows);
        Assert.NotEmpty(firstCells);
        Assert.NotEmpty(firstHeaders);

        grid.RealizationFactory = new GenerationFactory(firstGeneration: false);
        grid.UpdateLayout();

        Assert.Same(items[2], grid.SelectedItem);
        Assert.NotEmpty(grid.GetVisualDescendants().OfType<SecondRow>());
        Assert.NotEmpty(grid.GetVisualDescendants().OfType<SecondCell>());
        Assert.NotEmpty(grid.GetVisualDescendants().OfType<SecondHeader>());
        Assert.DoesNotContain(firstRows, row => row.IsAttachedToVisualTree());
        Assert.DoesNotContain(firstCells, cell => cell.IsAttachedToVisualTree());
        Assert.DoesNotContain(firstHeaders, header => header.IsAttachedToVisualTree());
    }

    [AvaloniaFact]
    public void Default_Factory_Preserves_Column_Selected_Cell_Types()
    {
        var items = new[] { new Item(1) };
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            RealizationFactory = DataGridRealizationFactory.Default,
        };
        var text = new DataGridTextColumn
        {
            Binding = new Binding(nameof(Item.Id)),
            UseDirectTextCell = true,
        };
        grid.Columns.Add(text);
        var window = new Window { Width = 320, Height = 180 };
        window.SetThemeStyles();
        window.Content = grid;

        window.Show();
        grid.UpdateLayout();

        Assert.IsType<DataGridRow>(grid.GetVisualDescendants().OfType<DataGridRow>().First());
        Assert.IsType<DataGridDirectTextCell>(grid.GetVisualDescendants().OfType<DataGridCell>().First());
        Assert.IsType<DataGridColumnHeader>(text.HeaderCell);
    }

    [AvaloniaFact]
    public void Factory_Cannot_Be_Null()
    {
        var grid = new DataGrid();

        Assert.Throws<ArgumentNullException>(() => grid.RealizationFactory = null!);
        Assert.Same(DataGridRealizationFactory.Default, grid.RealizationFactory);
    }

    private static (Window Window, DataGrid Grid) CreateGrid(
        IReadOnlyList<Item> items,
        DataGridRealizationFactory factory,
        int columnCount)
    {
        var grid = new DataGrid
        {
            Width = 420,
            Height = 144,
            AutoGenerateColumns = false,
            ItemsSource = items,
            RealizationFactory = factory,
            UseLogicalScrollable = true,
            RowHeight = 24,
        };
        for (int index = 0; index < columnCount; index++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = $"Column {index}",
                Binding = new Binding(nameof(Item.Id)),
            });
        }

        var window = new Window { Width = 440, Height = 180 };
        window.SetThemeStyles();
        window.Content = grid;
        return (window, grid);
    }

    private static void AssertRealizedContainerTypes(DataGrid grid)
    {
        DataGridRow[] rows = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => row.Slot >= 0)
            .ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Item item = Assert.IsType<Item>(row.DataContext);
            if ((item.Id & 1) == 0)
            {
                Assert.IsType<EvenRow>(row);
                Assert.All(
                    Enumerable.Range(0, row.Cells.Count).Select(index => row.Cells[index]),
                    cell => Assert.IsType<EvenCell>(cell));
            }
            else
            {
                Assert.IsType<OddRow>(row);
                Assert.All(
                    Enumerable.Range(0, row.Cells.Count).Select(index => row.Cells[index]),
                    cell => Assert.IsType<OddCell>(cell));
            }
        });
    }

    private sealed record Item(int Id);

    private sealed class HierarchyItem
    {
        public HierarchyItem(int id) => Id = id;

        public int Id { get; }

        public ObservableCollection<HierarchyItem> Children { get; } = new();
    }

    private abstract class StyledRow : DataGridRow
    {
        protected override Type StyleKeyOverride => typeof(DataGridRow);
    }

    private abstract class StyledCell : DataGridCell
    {
        protected override Type StyleKeyOverride => typeof(DataGridCell);
    }

    private abstract class StyledHeader : DataGridColumnHeader
    {
        protected override Type StyleKeyOverride => typeof(DataGridColumnHeader);
    }

    private sealed class EvenRow : StyledRow;
    private sealed class OddRow : StyledRow;
    private sealed class EvenCell : StyledCell;
    private sealed class OddCell : StyledCell;
    private sealed class LeftHeader : StyledHeader;
    private sealed class CenterHeader : StyledHeader;
    private sealed class RightHeader : StyledHeader;
    private sealed class HierarchyRow : StyledRow;
    private sealed class HierarchyCell : StyledCell;
    private sealed class FirstRow : StyledRow;
    private sealed class SecondRow : StyledRow;
    private sealed class FirstCell : StyledCell;
    private sealed class SecondCell : StyledCell;
    private sealed class FirstHeader : StyledHeader;
    private sealed class SecondHeader : StyledHeader;

    private sealed class PartitionedFactory : DataGridRealizationFactory
    {
        public int RowContexts { get; private set; }
        public int CellContexts { get; private set; }
        public int HeaderContexts { get; private set; }

        public override DataGridRow CreateRow(DataGridRowRealizationContext context)
        {
            RowContexts++;
            return (((Item)context.Item).Id & 1) == 0 ? new EvenRow() : new OddRow();
        }

        public override object? GetRowRecyclingKey(DataGridRowRealizationContext context) =>
            (((Item)context.Item).Id & 1) == 0 ? typeof(EvenRow) : typeof(OddRow);

        public override object? GetRowRecyclingKey(DataGridRow row) => row.GetType();

        public override DataGridCell CreateCell(DataGridCellRealizationContext context)
        {
            CellContexts++;
            return (((Item)context.Item).Id & 1) == 0 ? new EvenCell() : new OddCell();
        }

        public override object? GetCellRecyclingKey(DataGridCellRealizationContext context) =>
            (((Item)context.Item).Id & 1) == 0 ? typeof(EvenCell) : typeof(OddCell);

        public override object? GetCellRecyclingKey(DataGridCell cell) => cell.GetType();

        public override DataGridColumnHeader CreateColumnHeader(DataGridColumnHeaderRealizationContext context)
        {
            HeaderContexts++;
            return context.IsFrozenLeft ? new LeftHeader() :
                context.IsFrozenRight ? new RightHeader() : new CenterHeader();
        }

        public override object? GetColumnHeaderRecyclingKey(DataGridColumnHeaderRealizationContext context) =>
            context.IsFrozenLeft ? typeof(LeftHeader) :
            context.IsFrozenRight ? typeof(RightHeader) : typeof(CenterHeader);

        public override object? GetColumnHeaderRecyclingKey(DataGridColumnHeader header) => header.GetType();
    }

    private sealed class HierarchyFactory : DataGridRealizationFactory
    {
        public bool SawHierarchicalNode { get; private set; }
        public bool SawApplicationItem { get; private set; }

        public override DataGridRow CreateRow(DataGridRowRealizationContext context)
        {
            SawHierarchicalNode |= context.HierarchicalNode is not null;
            SawApplicationItem |= context.Item is HierarchyItem;
            return new HierarchyRow();
        }

        public override DataGridCell CreateCell(DataGridCellRealizationContext context)
        {
            SawHierarchicalNode |= context.HierarchicalNode is not null;
            SawApplicationItem |= context.Item is HierarchyItem;
            return new HierarchyCell();
        }

        public override object? GetRowRecyclingKey(DataGridRowRealizationContext context) => typeof(HierarchyRow);
        public override object? GetRowRecyclingKey(DataGridRow row) => typeof(HierarchyRow);
        public override object? GetCellRecyclingKey(DataGridCellRealizationContext context) => typeof(HierarchyCell);
        public override object? GetCellRecyclingKey(DataGridCell cell) => typeof(HierarchyCell);
    }

    private sealed class GenerationFactory : DataGridRealizationFactory
    {
        private readonly bool _firstGeneration;

        public GenerationFactory(bool firstGeneration) => _firstGeneration = firstGeneration;

        public override DataGridRow CreateRow(DataGridRowRealizationContext context) =>
            _firstGeneration ? new FirstRow() : new SecondRow();

        public override object? GetRowRecyclingKey(DataGridRowRealizationContext context) =>
            _firstGeneration ? typeof(FirstRow) : typeof(SecondRow);

        public override object? GetRowRecyclingKey(DataGridRow row) => row.GetType();

        public override DataGridCell CreateCell(DataGridCellRealizationContext context) =>
            _firstGeneration ? new FirstCell() : new SecondCell();

        public override object? GetCellRecyclingKey(DataGridCellRealizationContext context) =>
            _firstGeneration ? typeof(FirstCell) : typeof(SecondCell);

        public override object? GetCellRecyclingKey(DataGridCell cell) => cell.GetType();

        public override DataGridColumnHeader CreateColumnHeader(DataGridColumnHeaderRealizationContext context) =>
            _firstGeneration ? new FirstHeader() : new SecondHeader();

        public override object? GetColumnHeaderRecyclingKey(DataGridColumnHeaderRealizationContext context) =>
            _firstGeneration ? typeof(FirstHeader) : typeof(SecondHeader);

        public override object? GetColumnHeaderRecyclingKey(DataGridColumnHeader header) => header.GetType();
    }
}
