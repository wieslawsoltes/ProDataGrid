using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.Generated;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Diagnostics;

public sealed class SourceGenerationMigrationTests
{
    [Fact]
    public void Registry_exposes_every_generated_ProDiagnostics_grid_schema()
    {
        Assert.Equal(6, ProDiagnosticsGeneratedSchemas.Schemas.Count);
        Assert.True(ProDiagnosticsGeneratedSchemas.TryGetSchema(typeof(AssetEntryViewModel), out _));
        Assert.True(ProDiagnosticsGeneratedSchemas.TryGetSchema(typeof(PropertyViewModel), out _));
        Assert.True(ProDiagnosticsGeneratedSchemas.TryGetSchema(typeof(ResourceEntryViewModel), out _));
        Assert.True(ProDiagnosticsGeneratedSchemas.TryGetSchema(typeof(ResourceReferenceEntryViewModel), out _));
        Assert.True(ProDiagnosticsGeneratedSchemas.TryGetSchema(typeof(ResourceTreeNode), out _));
        Assert.True(ProDiagnosticsGeneratedSchemas.TryGetSchema(typeof(TreeNode), out _));
        Assert.All(
            ProDiagnosticsGeneratedSchemas.Schemas,
            static schema => Assert.All(schema.Manifest.Fields, static field => Assert.NotNull(field.Accessor)));
    }

    [Fact]
    public void Asset_schema_reproduces_the_complete_column_contract()
    {
        var schema = ProDiagnosticsGeneratedSchemas.Schemas.Single(
            static candidate => candidate.Manifest.ItemType == typeof(AssetEntryViewModel));

        Assert.Equal(new[] { "name", "assembly", "path", "kind", "extension" },
            schema.Manifest.Fields.Select(static field => field.ColumnKey));
        Assert.All(schema.Manifest.Fields, static field => Assert.NotNull(field.Accessor));
    }

    [Fact]
    public void Hierarchical_schema_exposes_the_canonical_item_field()
    {
        var schema = ProDiagnosticsGeneratedSchemas.Schemas.Single(
            static candidate => candidate.Manifest.ItemType == typeof(TreeNode));
        var field = Assert.Single(schema.Manifest.Fields);

        Assert.Equal("visual", field.ColumnKey);
        Assert.Equal(nameof(TreeNode.Item), field.PropertyName);
        Assert.Equal(typeof(TreeNode), field.ValueType);
    }

    [Fact]
    public void Generated_collection_views_use_compiled_sorting_and_group_fields()
    {
        AssetEntryViewModel[] assets =
        [
            new(new Uri("avares://Demo/z.png"), "Zeta", "z.png", AssetKind.Image),
            new(new Uri("avares://Demo/a.png"), "Alpha", "a.png", AssetKind.Image)
        ];
        var view = AssetEntryGridSchema.CreateCollectionView(assets);

        AssetEntryGridSchema.ApplyCollectionViewSorting(
            view,
            [AssetEntryGridSchema.AssemblyName.Ascending(), AssetEntryGridSchema.AssetPath.Ascending()]);

        Assert.Equal(new[] { "Alpha", "Zeta" }, view.Cast<AssetEntryViewModel>().Select(static asset => asset.AssemblyName));
        Assert.All(view.SortDescriptions, static description => Assert.False(description.HasPropertyPath));
        Assert.Equal("group", Assert.Single(PropertyGridSchema.GroupFields).ColumnKey);
        Assert.Equal("type", Assert.Single(ResourceEntryGridSchema.GroupFields).ColumnKey);
    }

    [AvaloniaFact]
    public void Generated_view_registry_creates_registered_Xaml_view_without_reflection()
    {
        var viewModel = new HotKeyPageViewModel();

        Assert.True(ProDiagnosticsGeneratedSchemas.TryCreateView(viewModel, out Control? view));
        Assert.IsType<HotKeyPageView>(view);
        Assert.Same(viewModel, view!.DataContext);
    }

    [AvaloniaFact]
    public void Generated_hierarchical_columns_attach_and_render_in_the_existing_Xaml_view()
    {
        var root = new StackPanel { Name = "Root" };
        root.Children.Add(new Button { Name = "Child", Content = "Inspect" });
        using var mainViewModel = new MainViewModel(root);
        var treeViewModel = Assert.IsType<TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var view = new TreePageTreeView { DataContext = treeViewModel };
        var window = new Window { Content = view, Width = 640, Height = 480 };

        try
        {
            window.Show();
            window.UpdateLayout();
            view.UpdateLayout();

            Assert.True(view.IsAttachedToVisualTree());
            Control grid = view.FindControl<Control>("tree")!;
            Assert.Equal("Avalonia.Controls.DataGrid", grid.GetType().FullName);
            Assert.True(treeViewModel.FastPathOptions.StrictMode);
            Assert.Equal("visual", Assert.Single(treeViewModel.ColumnDefinitions).ColumnKey);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tree_column_grows_and_exposes_horizontal_scroll_after_descendants_expand()
    {
        var root = new StackPanel { Name = "Root" };
        root.Children.Add(new DiagnosticTreeElementWithAnIntentionallyLongTypeNameForHorizontalOverflow());

        var mainViewModel = new MainViewModel(root);
        var treeViewModel = Assert.IsType<TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var view = new TreePageTreeView { DataContext = treeViewModel };
        var window = new MainWindow
        {
            DataContext = mainViewModel,
            Content = view,
            // Keep the long descendant wider than the viewport with real font metrics.
            Width = 320,
            Height = 600
        };

        try
        {
            window.Show();
            window.UpdateLayout();

            Control grid = view.FindControl<Control>("tree")!;
            object column = Assert.Single(
                ((IEnumerable)GetRuntimeProperty(grid, "Columns")).Cast<object>());
            object width = GetRuntimeProperty(column, "Width");
            Assert.True((bool)GetRuntimeProperty(width, "IsSizeToCells"));
            double collapsedWidth = (double)GetRuntimeProperty(column, "ActualWidth");

            treeViewModel.Nodes[0].IsExpanded = true;
            window.UpdateLayout();

            object presenter = Assert.Single(
                view.GetVisualDescendants(),
                static visual => visual.GetType().FullName == "Avalonia.Controls.Primitives.DataGridRowsPresenter");
            double expandedWidth = (double)GetRuntimeProperty(column, "ActualWidth");
            var scrollable = Assert.IsAssignableFrom<Avalonia.Controls.Primitives.ILogicalScrollable>(presenter);
            Size viewport = scrollable.Viewport;
            Size extent = scrollable.Extent;

            Assert.True(
                expandedWidth > collapsedWidth,
                $"Expected the column to grow from {collapsedWidth}, but it remained {expandedWidth}; viewport={viewport.Width}, extent={extent.Width}.");
            Assert.True(expandedWidth > viewport.Width);
            Assert.True(extent.Width > viewport.Width);
            Assert.Contains(
                view.GetVisualDescendants().OfType<ScrollBar>(),
                static scrollBar => scrollBar.Orientation == Orientation.Horizontal && scrollBar.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static object GetRuntimeProperty(object instance, string propertyName) =>
        instance.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance)!;

    private sealed class DiagnosticTreeElementWithAnIntentionallyLongTypeNameForHorizontalOverflow : Control;
}
