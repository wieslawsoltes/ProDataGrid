// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class DataGridHierarchicalColumnTests
{
    [AvaloniaFact]
    public void Presenter_Updates_Padding_From_Level_And_Indent()
    {
        var presenter = new DataGridHierarchicalPresenter
        {
            Indent = 8,
            Level = 2
        };

        Assert.Equal(new Thickness(16, 0, 0, 0), presenter.Padding);

        presenter.Indent = 10;

        Assert.Equal(new Thickness(20, 0, 0, 0), presenter.Padding);
    }

    [AvaloniaFact]
    public void Presenter_Reapplies_Padding_On_DataContext_Change()
    {
        var presenter = new DataGridHierarchicalPresenter
        {
            Indent = 8
        };

        presenter.Bind(DataGridHierarchicalPresenter.LevelProperty, new Binding(nameof(HierarchicalNode.Level)));

        var nodeA = new HierarchicalNode(new object(), level: 1);
        var nodeB = new HierarchicalNode(new object(), level: 1);

        presenter.DataContext = nodeA;
        Assert.Equal(new Thickness(8, 0, 0, 0), presenter.Padding);

        presenter.Padding = new Thickness(123, 0, 0, 0);
        presenter.DataContext = nodeB;

        Assert.Equal(new Thickness(8, 0, 0, 0), presenter.Padding);
    }

    [AvaloniaFact]
    public void Presenter_Raises_ToggleRequested_On_Click()
    {
        var presenter = new DataGridHierarchicalPresenter
        {
            Template = new FuncControlTemplate<DataGridHierarchicalPresenter>((owner, scope) =>
            {
                var toggle = new ToggleButton
                {
                    Name = "PART_Expander"
                };
                scope.Register(toggle.Name, toggle);

                return new Grid
                {
                    Children = { toggle }
                };
            })
        };

        bool raised = false;
        presenter.ToggleRequested += (_, _) => raised = true;

        presenter.ApplyTemplate();

        var toggleButton = presenter.GetTemplateChildren().OfType<ToggleButton>().Single();
        toggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(raised);
    }

    [AvaloniaFact]
    public async Task Presenter_Toggle_Preserves_UI_Context_During_Async_Expansion()
    {
        var root = new AsyncItem("root");
        root.Children.Add(new AsyncItem("child"));
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = async (item, cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                return ((AsyncItem)item).Children;
            }
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            ItemsSource = model.ObservableFlattened
        };
        var column = new TestHierarchicalColumn
        {
            Binding = new Binding(nameof(HierarchicalNode.Item))
        };
        grid.ColumnsInternal.Add(column);
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = new DataGridCell { DataContext = model.Root };
        var presenter = Assert.IsType<DataGridHierarchicalPresenter>(
            column.Generate(cell, model.Root!));
        cell.Content = presenter;
        presenter.DataContext = model.Root;

        presenter.RaiseEvent(new RoutedEventArgs(
            DataGridHierarchicalPresenter.ToggleRequestedEvent,
            presenter));

        for (var attempt = 0; attempt < 100 && model.Count < 2; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(model.Root!.IsExpanded);
        Assert.Equal(2, model.Count);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Presenter_Toggle_Handles_Cancellation(bool replaceRoot)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = async (_, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return Array.Empty<object>();
            }
        });
        model.SetRoot(new AsyncItem("root"));
        var node = model.Root!;
        var grid = new DataGrid { HierarchicalModel = model, AutoGenerateColumns = false };
        var column = new TestHierarchicalColumn();
        grid.ColumnsInternal.Add(column);
        var cell = new DataGridCell { DataContext = node };
        var presenter = Assert.IsType<DataGridHierarchicalPresenter>(column.Generate(cell, node));
        cell.Content = presenter;
        presenter.DataContext = node;
        var exceptions = new List<Exception>();
        void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs args)
        {
            exceptions.Add(args.Exception);
            args.Handled = true;
        }

        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        try
        {
            presenter.RaiseEvent(new RoutedEventArgs(DataGridHierarchicalPresenter.ToggleRequestedEvent, presenter));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(node.IsLoading);
            if (replaceRoot)
                model.SetRoot(new AsyncItem("replacement"));
            else
                presenter.RaiseEvent(new RoutedEventArgs(DataGridHierarchicalPresenter.ToggleRequestedEvent, presenter));

            for (int attempt = 0; attempt < 100 && node.IsLoading; attempt++)
                await Task.Delay(10, TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            Assert.False(node.IsLoading);
            Assert.False(node.IsExpanded);
            Assert.Equal(1, model.Count);
            Assert.Empty(exceptions);
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
        }
    }

    [AvaloniaFact]
    public void ReusedPresenterRebindsContent()
    {
        var column = new TestHierarchicalColumn
        {
            Binding = new Binding(nameof(HierarchicalNode.Item))
        };
        var firstNode = new HierarchicalNode("First", level: 0);
        var secondNode = new HierarchicalNode("Second", level: 0);
        var cell = new DataGridCell { DataContext = firstNode };
        var presenter = Assert.IsType<DataGridHierarchicalPresenter>(column.Generate(cell, firstNode));
        cell.Content = presenter;
        presenter.DataContext = firstNode;
        Assert.Equal("First", presenter.Content);

        presenter.Content = "stale";
        presenter.DataContext = secondNode;
        var reused = column.Generate(cell, secondNode);

        Assert.Same(presenter, reused);
        Assert.Equal("Second", presenter.Content);
    }

    private sealed class TestHierarchicalColumn : DataGridHierarchicalColumn
    {
        public Control Generate(DataGridCell cell, object item) => GenerateElement(cell, item);
    }

    private sealed class AsyncItem(string name)
    {
        public string Name { get; } = name;

        public List<AsyncItem> Children { get; } = [];
    }
}
