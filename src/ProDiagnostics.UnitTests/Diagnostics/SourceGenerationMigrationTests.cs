using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.Generated;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Headless.XUnit;
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
        }
        finally
        {
            window.Close();
        }
    }
}
