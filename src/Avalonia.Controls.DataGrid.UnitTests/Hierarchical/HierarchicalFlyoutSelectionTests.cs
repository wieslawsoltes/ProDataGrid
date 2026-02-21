// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class HierarchicalFlyoutSelectionTests
{
    private class Folder
    {
        public Folder(string name)
        {
            Name = name;
            Folders = new ObservableCollection<Folder>();
        }

        public string Name { get; set; }

        public ObservableCollection<Folder> Folders { get; }
    }

    private class FolderDestinationManagerVm
    {
        public FolderDestinationManagerVm(Folder root)
        {
            SelectionModel = new SelectionModel<HierarchicalNode> { SingleSelect = true };
            HierarchicalModel = new HierarchicalModel(new HierarchicalOptions
            {
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 3,
                VirtualizeChildren = true,
                ChildrenSelector = o => ((Folder)o).Folders,
                IsLeafSelector = _ => false
            });

            HierarchicalModel.SetRoot(root);
        }

        public SelectionModel<HierarchicalNode> SelectionModel { get; }

        public HierarchicalModel HierarchicalModel { get; }
    }

    private class HostVm
    {
        public HostVm(FolderDestinationManagerVm manager)
        {
            FolderDestinationManager = manager;
        }

        public FolderDestinationManagerVm FolderDestinationManager { get; }
    }

    [AvaloniaFact]
    public void SelectionModel_Raises_When_Grid_Hosted_In_Flyout()
    {
        var folderRoot = new Folder("root");
        var childA = new Folder("childA");
        var childB = new Folder("childB");
        folderRoot.Folders.Add(childA);
        folderRoot.Folders.Add(childB);

        var manager = new FolderDestinationManagerVm(folderRoot);
        var host = new HostVm(manager);

        var template = new FuncDataTemplate<FolderDestinationManagerVm>((vm, _) =>
        {
            var grid = new DataGrid
            {
                HierarchicalRowsEnabled = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridSelectionMode.Extended
            };

            grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
            {
                Header = "Name",
                Binding = new Binding("Item.Name")
            });

            grid.Bind(DataGrid.SelectionProperty, new Binding(nameof(FolderDestinationManagerVm.SelectionModel)) { Mode = BindingMode.OneTime });
            grid.Bind(DataGrid.HierarchicalModelProperty, new Binding(nameof(FolderDestinationManagerVm.HierarchicalModel)) { Mode = BindingMode.OneTime });

            return grid;
        });

        var contentControl = new ContentControl
        {
            [!ContentControl.ContentProperty] = new Binding(nameof(HostVm.FolderDestinationManager)),
            ContentTemplate = template,
            Margin = new Thickness(16),
            MaxWidth = 600
        };

        var flyout = new Flyout
        {
            ShowMode = FlyoutShowMode.Transient,
            Content = contentControl
        };

        var dropDown = new DropDownButton
        {
            Flyout = flyout,
            DataContext = host,
            Width = 200,
            Height = 60
        };

        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = dropDown
        };

        window.SetThemeStyles();

        window.Show();
        window.ApplyTemplate();
        window.UpdateLayout();

        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        var grid = window.GetVisualDescendants().OfType<DataGrid>().First();

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.NotNull(manager.SelectionModel.Source);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var row = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => r.DataContext is HierarchicalNode node && ReferenceEquals(node.Item, childA));
        var cell = row.GetVisualDescendants().OfType<DataGridCell>().First();

        var pointerArgs = new PointerPressedEventArgs(cell, pointer, cell, new Point(0, 0), 0, properties, KeyModifiers.None);

        bool selectionRaised = false;
        manager.SelectionModel.SelectionChanged += (_, __) => selectionRaised = true;

        cell.RaiseEvent(pointerArgs);
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        window.Close();

        Assert.True(selectionRaised);
    }

    [AvaloniaFact]
    public void SelectionModel_Raises_When_Flyout_Reopened()
    {
        var folderRoot = new Folder("root");
        var childA = new Folder("childA");
        var childB = new Folder("childB");
        var childC = new Folder("childC");
        folderRoot.Folders.Add(childA);
        folderRoot.Folders.Add(childB);
        folderRoot.Folders.Add(childC);

        var manager = new FolderDestinationManagerVm(folderRoot);
        var host = new HostVm(manager);

        var template = CreateTemplate();

        var contentControl = CreateContentControl(host, template);
        var flyout = CreateFlyout(contentControl);
        var dropDown = CreateDropDown(host, flyout);
        var window = CreateWindow(dropDown);

        window.Show();
        window.ApplyTemplate();
        window.UpdateLayout();

        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        int raised = 0;
        manager.SelectionModel.SelectionChanged += (_, __) => raised++;

        // First open/select.
        ClickFolder(window, childA);
        Assert.True(raised >= 1);

        // Hide and reopen the flyout.
        flyout.Hide();
        Dispatcher.UIThread.RunJobs();
        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        // Select a different item on the second open.
        ClickFolder(window, childB);
        window.Close();

        Assert.True(raised >= 2);
    }

    [AvaloniaFact]
    public void Closing_Flyout_With_Selected_Item_Does_Not_Leave_Invalid_CurrentSlot()
    {
        var folderRoot = new Folder("root");
        var childA = new Folder("childA");
        var childB = new Folder("childB");
        folderRoot.Folders.Add(childA);
        folderRoot.Folders.Add(childB);

        var manager = new FolderDestinationManagerVm(folderRoot);
        var host = new HostVm(manager);

        var template = CreateTemplate();

        var contentControl = CreateContentControl(host, template);
        var flyout = CreateFlyout(contentControl);
        var dropDown = CreateDropDown(host, flyout);
        var window = CreateWindow(dropDown);
        try
        {
            window.Show();
            window.ApplyTemplate();
            window.UpdateLayout();

            flyout.ShowAt(dropDown);
            Dispatcher.UIThread.RunJobs();

            ClickFolder(window, childA);

            var grid = window.GetVisualDescendants().OfType<DataGrid>().First();
            Assert.True(grid.CurrentCell.IsValid);

            flyout.Hide();
            Dispatcher.UIThread.RunJobs();

            Assert.True(
                grid.CurrentSlot == -1 || (grid.CurrentSlot >= 0 && grid.CurrentSlot < grid.SlotCount),
                $"Expected CurrentSlot to be unset or in range. CurrentSlot={grid.CurrentSlot}, SlotCount={grid.SlotCount}, CurrentColumnIndex={grid.CurrentColumnIndex}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectionModel_Raises_When_Reopened_With_Reused_Content()
    {
        var folderRoot = new Folder("root");
        var childA = new Folder("childA");
        var childB = new Folder("childB");
        folderRoot.Folders.Add(childA);
        folderRoot.Folders.Add(childB);

        var manager = new FolderDestinationManagerVm(folderRoot);
        var host = new HostVm(manager);

        var template = CreateTemplate();

        var contentControl = CreateContentControl(host, template);
        var flyout = CreateFlyout(contentControl);
        var dropDown = CreateDropDown(host, flyout);
        var window = CreateWindow(dropDown);

        window.Show();
        window.ApplyTemplate();
        window.UpdateLayout();

        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        int raised = 0;
        manager.SelectionModel.SelectionChanged += (_, __) => raised++;

        ClickFolder(window, childA);
        Assert.True(raised >= 1);

        flyout.Hide();
        Dispatcher.UIThread.RunJobs();
        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        ClickFolder(window, childB);
        window.Close();

        // Regression for https://github.com/wieslawsoltes/ProDataGrid/issues/86:
        // SelectionChanged should still raise when the same flyout content is reused.
        Assert.True(raised >= 2);
    }

    [AvaloniaFact]
    public void SelectionModel_With_Lingering_Source_Reattaches_On_Reopen()
    {
        var folderRoot = new Folder("root");
        folderRoot.Folders.Add(new Folder("childA"));

        var manager = new FolderDestinationManagerVm(folderRoot);
        var host = new HostVm(manager);

        var template = CreateTemplate();

        var contentControl = CreateContentControl(host, template);
        var flyout = CreateFlyout(contentControl);
        var dropDown = CreateDropDown(host, flyout);
        var window = CreateWindow(dropDown);

        window.Show();
        window.ApplyTemplate();
        window.UpdateLayout();

        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        // Capture that Source is set after first open.
        Assert.NotNull(manager.SelectionModel.Source);

        // Close and create a brand new flyout/grid using the same selection model (simulating sample page reopening).
        window.Close();
        Dispatcher.UIThread.RunJobs();

        var secondContentControl = CreateContentControl(host, template);
        var secondFlyout = CreateFlyout(secondContentControl);
        var secondDropDown = CreateDropDown(host, secondFlyout);
        var secondWindow = CreateWindow(secondDropDown);

        // Should not throw after Source retargeting.
        secondWindow.Show();
        secondWindow.ApplyTemplate();
        secondWindow.UpdateLayout();
        secondWindow.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_Raises_On_Reopen_After_Retarget()
    {
        var folderRoot = new Folder("root");
        var childA = new Folder("childA");
        var childB = new Folder("childB");
        folderRoot.Folders.Add(childA);
        folderRoot.Folders.Add(childB);

        var manager = new FolderDestinationManagerVm(folderRoot);
        var host = new HostVm(manager);

        var template = CreateTemplate();

        var contentControl = CreateContentControl(host, template);
        var flyout = CreateFlyout(contentControl);
        var dropDown = CreateDropDown(host, flyout);
        var window = CreateWindow(dropDown);

        window.Show();
        window.ApplyTemplate();
        window.UpdateLayout();

        flyout.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        int raised = 0;
        manager.SelectionModel.SelectionChanged += (_, __) => raised++;

        ClickFolder(window, childA);
        Assert.True(raised >= 1);

        flyout.Hide();
        Dispatcher.UIThread.RunJobs();
        window.Close();

        var secondContentControl = CreateContentControl(host, template);
        var secondFlyout = CreateFlyout(secondContentControl);
        var secondDropDown = CreateDropDown(host, secondFlyout);
        var secondWindow = CreateWindow(secondDropDown);

        secondWindow.Show();
        secondWindow.ApplyTemplate();
        secondWindow.UpdateLayout();

        secondFlyout.ShowAt(secondDropDown);
        Dispatcher.UIThread.RunJobs();

        ClickFolder(secondWindow, childB);
        secondWindow.Close();

        Assert.True(raised >= 2);
    }

    private static FuncDataTemplate<FolderDestinationManagerVm> CreateTemplate()
    {
        return new FuncDataTemplate<FolderDestinationManagerVm>((vm, _) =>
        {
            var grid = new DataGrid
            {
                HierarchicalRowsEnabled = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridSelectionMode.Extended
            };

            grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
            {
                Header = "Name",
                Binding = new Binding("Item.Name")
            });

            grid.Bind(DataGrid.SelectionProperty, new Binding(nameof(FolderDestinationManagerVm.SelectionModel)) { Mode = BindingMode.OneTime });
            grid.Bind(DataGrid.HierarchicalModelProperty, new Binding(nameof(FolderDestinationManagerVm.HierarchicalModel)) { Mode = BindingMode.OneTime });

            return grid;
        });
    }

    private static ContentControl CreateContentControl(object host, FuncDataTemplate<FolderDestinationManagerVm> template)
    {
        return new ContentControl
        {
            DataContext = host,
            [!ContentControl.ContentProperty] = new Binding(nameof(HostVm.FolderDestinationManager)),
            ContentTemplate = template,
            Margin = new Thickness(16),
            MaxWidth = 600
        };
    }

    private static Flyout CreateFlyout(Control content)
    {
        return new Flyout
        {
            ShowMode = FlyoutShowMode.Transient,
            Content = content
        };
    }

    private static DropDownButton CreateDropDown(object host, Flyout flyout)
    {
        return new DropDownButton
        {
            Flyout = flyout,
            DataContext = host,
            Width = 200,
            Height = 60
        };
    }

    private static Window CreateWindow(Control dropDown)
    {
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = dropDown
        };

        window.SetThemeStyles();

        return window;
    }

    private static void ClickFolder(Window window, Folder target)
    {
        var grid = window.GetVisualDescendants().OfType<DataGrid>().First();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var row = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => r.DataContext is HierarchicalNode node && ReferenceEquals(node.Item, target));
        var cell = row.GetVisualDescendants().OfType<DataGridCell>().First();

        var pointerArgs = new PointerPressedEventArgs(cell, pointer, cell, new Point(0, 0), 0, properties, KeyModifiers.None);
        cell.RaiseEvent(pointerArgs);
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
    }
}
