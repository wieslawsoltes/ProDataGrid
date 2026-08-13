// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.DataGridLayouts;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Automation;

public sealed class DataGridItemContainerAutomationPeerTests
{
    [AvaloniaFact]
    public void Item_presentation_exposes_realized_selection_items_even_in_cell_selection_mode()
    {
        var items = new[] { new Item("One"), new Item("Two"), new Item("Three") };
        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            UseLogicalScrollable = true,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.Cell,
            ItemTemplate = new FuncDataTemplate<Item>(
                (item, _) => new TextBlock { Width = 100, Height = 40, Text = item.Name }),
            LayoutModel = new DataGridUniformGridLayoutModel
            {
                PresentationMode = DataGridLayoutPresentationMode.Items,
                ItemSizeEstimate = new Size(100, 40),
                MinItemWidth = 100,
                MinItemHeight = 40
            }
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });
        var window = new Window { Width = 420, Height = 180 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            window.UpdateLayout();

            DataGridItemContainer[] containers = grid.GetVisualDescendants()
                .OfType<DataGridItemContainer>()
                .Where(static container => container.Index >= 0)
                .OrderBy(static container => container.Index)
                .ToArray();
            Assert.True(
                containers.Length >= 2,
                $"realized={containers.Length}, display={grid.DisplayData.ScrollingElementCount}, " +
                $"bounds={grid.Bounds}, presenter={grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().Single().Bounds}");
            var gridPeer = Assert.IsType<DataGridAutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(grid));
            ISelectionProvider selection = Assert.IsAssignableFrom<ISelectionProvider>(
                gridPeer.GetProvider<ISelectionProvider>());
            var firstPeer = Assert.IsType<DataGridItemContainerAutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(containers[0]));
            var secondPeer = Assert.IsType<DataGridItemContainerAutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(containers[1]));
            ISelectionItemProvider first = Assert.IsAssignableFrom<ISelectionItemProvider>(
                firstPeer.GetProvider<ISelectionItemProvider>());
            ISelectionItemProvider second = Assert.IsAssignableFrom<ISelectionItemProvider>(
                secondPeer.GetProvider<ISelectionItemProvider>());

            first.Select();
            second.AddToSelection();

            Assert.True(first.IsSelected);
            Assert.True(second.IsSelected);
            Assert.True(selection.CanSelectMultiple);
            Assert.Same(selection, second.SelectionContainer);
            Assert.Equal(new AutomationPeer[] { firstPeer, secondPeer }, selection.GetSelection().ToArray());
            Assert.Equal(1, containers[0].GetValue(AutomationProperties.PositionInSetProperty));
            Assert.Equal(items.Length, containers[0].GetValue(AutomationProperties.SizeOfSetProperty));
            Assert.Equal(items[0].ToString(), firstPeer.GetName());
        }
        finally
        {
            window.Close();
        }
    }

    private sealed record Item(string Name);
}
