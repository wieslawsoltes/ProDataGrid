using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.Views;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class PropertyValueEditorViewTests
{
    [AvaloniaFact]
    public void Editor_is_reused_for_same_property_type()
    {
        var view = CreateView();
        var first = CreatePropertyViewModel(new TestTarget { Flag = true }, nameof(TestTarget.Flag));
        var second = CreatePropertyViewModel(new TestTarget { Flag = false }, nameof(TestTarget.Flag));

        view.DataContext = first;
        var initialEditor = view.Content;

        view.DataContext = second;

        Assert.Same(initialEditor, view.Content);
    }

    [AvaloniaFact]
    public void Editor_changes_for_different_property_type()
    {
        var view = CreateView();
        view.DataContext = CreatePropertyViewModel(new TestTarget { Flag = true }, nameof(TestTarget.Flag));
        var initialEditor = view.Content;

        view.DataContext = CreatePropertyViewModel(new TestTarget { Name = "Hello" }, nameof(TestTarget.Name));

        Assert.NotSame(initialEditor, view.Content);
    }

    [AvaloniaFact]
    public void Readonly_state_is_applied_to_checkbox_editor()
    {
        var view = CreateView();
        view.DataContext = CreatePropertyViewModel(new TestTarget(), nameof(TestTarget.ReadOnlyFlag));

        var editor = Assert.IsType<CheckBox>(view.Content);
        Assert.False(editor.IsEnabled);
    }

    [AvaloniaFact]
    public void Property_edit_handler_is_notified_after_value_commit()
    {
        var target = new Button { Width = 24 };
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        var handler = new RecordingPropertyEditHandler();
        mainViewModel.SetOptions(new DevToolsOptions { PropertyEditHandler = handler });
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == Layoutable.WidthProperty));

        property.Value = 120d;

        Assert.NotNull(handler.Edit);
        var edit = handler.Edit!;
        Assert.Same(target, edit.InspectedObject);
        Assert.Same(target, edit.Target);
        Assert.Equal("Width", edit.PropertyName);
        Assert.Equal("Width", edit.XamlPropertyName);
        Assert.Equal(typeof(double), edit.PropertyType);
        Assert.Equal(24d, edit.OldValue);
        Assert.Equal(120d, edit.NewValue);
        Assert.Equal("24", edit.OldValueText);
        Assert.Equal("120", edit.NewValueText);
        Assert.False(edit.IsAttached);
        Assert.True(edit.IsAvaloniaProperty);
        Assert.Equal(DevToolsResourceReferenceKind.None, edit.ResourceReferenceKind);
        Assert.Null(edit.ResourceKey);
        Assert.Null(edit.ResourceKeyText);
    }

    [AvaloniaFact]
    public void Property_editor_can_apply_dynamic_resource_reference()
    {
        var target = new Button
        {
            Background = Brushes.Blue
        };
        target.Resources["AccentBrush"] = Brushes.Red;

        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        var handler = new RecordingPropertyEditHandler();
        mainViewModel.SetOptions(new DevToolsOptions { PropertyEditHandler = handler });
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == TemplatedControl.BackgroundProperty));

        var view = CreateView();
        view.DataContext = property;
        var host = Assert.IsType<DockPanel>(view.Content);
        var resourceButton = Assert.IsType<Button>(host.Children[0]);
        Assert.Equal(24, resourceButton.Width);
        Assert.NotNull(resourceButton.Content);

        var dynamicResource = new ResourceReferenceSuggestionService()
            .GetCandidates(property)
            .Single(candidate => candidate.Kind == DevToolsResourceReferenceKind.Dynamic &&
                                 candidate.KeyText == "AccentBrush");

        Assert.True(property.TrySetResourceReference(dynamicResource, out var error), error);

        Assert.NotNull(handler.Edit);
        var edit = handler.Edit!;
        Assert.Equal(DevToolsResourceReferenceKind.Dynamic, edit.ResourceReferenceKind);
        Assert.Equal("AccentBrush", edit.ResourceKey);
        Assert.Equal("AccentBrush", edit.ResourceKeyText);
        Assert.Equal("{DynamicResource AccentBrush}", edit.NewValueText);
        Assert.Equal(Brushes.Red, target.Background);

        target.Resources["AccentBrush"] = Brushes.Green;

        Assert.Equal(Brushes.Green, target.Background);
    }

    [AvaloniaFact]
    public void Property_editor_skips_unresolvable_resource_candidates()
    {
        var target = new Button
        {
            Background = Brushes.Blue
        };
        target.Resources["AccentBrush"] = Brushes.Red;
        Assert.IsType<ResourceDictionary>(target.Resources)
            .AddDeferred("BrokenBrush", _ => throw new KeyNotFoundException("Static resource 'MissingBrush' not found."));

        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == TemplatedControl.BackgroundProperty));

        var view = CreateView();
        view.DataContext = property;
        var host = Assert.IsType<DockPanel>(view.Content);
        Assert.IsType<Button>(host.Children[0]);
        var candidates = new ResourceReferenceSuggestionService()
            .GetCandidates(property)
            .ToArray();

        Assert.Contains(candidates, candidate => candidate.KeyText == "AccentBrush");
        Assert.DoesNotContain(candidates, candidate => candidate.KeyText == "BrokenBrush");
    }

    [AvaloniaFact]
    public void Resource_reference_picker_groups_by_scope_and_filters_resources()
    {
        var target = new Button { Background = Brushes.Blue };
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == TemplatedControl.BackgroundProperty));
        var candidates = new[]
        {
            new ResourceReferenceCandidate(
                "AccentBrush",
                "AccentBrush",
                Brushes.Red,
                typeof(ISolidColorBrush),
                "Application / Resources",
                null,
                DevToolsResourceReferenceKind.Static),
            new ResourceReferenceCandidate(
                "AccentBrush",
                "AccentBrush",
                Brushes.Red,
                typeof(ISolidColorBrush),
                "Application / Resources",
                null,
                DevToolsResourceReferenceKind.Dynamic),
            new ResourceReferenceCandidate(
                "PanelBrush",
                "PanelBrush",
                Brushes.Gray,
                typeof(ISolidColorBrush),
                "Application / Styles",
                "Dark",
                DevToolsResourceReferenceKind.Static)
        };
        var picker = new Avalonia.Diagnostics.ViewModels.ResourceReferencePickerViewModel(
            property,
            candidates,
            new ResourceNodeFormatter());

        Assert.Equal(2, picker.ResourceCount);
        Assert.Equal("All resources", picker.SelectedScope?.Name);
        Assert.NotNull(FindScope(picker.Scopes[0], "Application / Resources"));
        Assert.NotNull(FindScope(picker.Scopes[0], "Application / Styles"));

        picker.ResourcesFilter.FilterString = "Accent";

        Assert.Equal(1, picker.ResourceCount);
        var entry = Assert.Single(picker.ResourcesView.Cast<Avalonia.Diagnostics.ViewModels.ResourceReferenceEntryViewModel>());
        picker.SelectedResource = entry;
        Assert.True(picker.CanUseStatic);
        Assert.True(picker.CanUseDynamic);
        Assert.NotNull(picker.GetSelectedCandidate(DevToolsResourceReferenceKind.Dynamic));

        picker.ResourcesFilter.FilterString = string.Empty;
        picker.SelectedScope = FindScope(picker.Scopes[0], "Application / Styles");

        Assert.Equal(1, picker.ResourceCount);
        Assert.Equal("PanelBrush", Assert.Single(
            picker.ResourcesView.Cast<Avalonia.Diagnostics.ViewModels.ResourceReferenceEntryViewModel>()).KeyDisplay);
    }

    [AvaloniaFact]
    public void Resource_reference_picker_window_filter_updates_view_model()
    {
        var target = new Button { Background = Brushes.Blue };
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == TemplatedControl.BackgroundProperty));
        var candidates = new[]
        {
            new ResourceReferenceCandidate(
                "AccentBrush",
                "AccentBrush",
                Brushes.Red,
                typeof(ISolidColorBrush),
                "Application / Resources",
                null,
                DevToolsResourceReferenceKind.Static),
            new ResourceReferenceCandidate(
                "PanelBrush",
                "PanelBrush",
                Brushes.Gray,
                typeof(ISolidColorBrush),
                "Application / Styles",
                null,
                DevToolsResourceReferenceKind.Static)
        };
        var picker = new Avalonia.Diagnostics.ViewModels.ResourceReferencePickerViewModel(
            property,
            candidates,
            new ResourceNodeFormatter());
        var window = new ResourceReferencePickerWindow
        {
            DataContext = picker
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var filterTextBox = Assert.Single(window.GetVisualDescendants().OfType<FilterTextBox>());
            var resourcesGrid = Assert.Single(
                window.GetVisualDescendants().OfType<Control>(),
                static control => control.GetType().FullName == "Avalonia.Controls.DataGrid");
            AssertResourceGridConfiguration(resourcesGrid);
            AssertGridHasColumn(resourcesGrid, "Preview");

            filterTextBox.Text = "Accent";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Accent", picker.ResourcesFilter.FilterString);
            Assert.Equal(1, picker.ResourceCount);
            Assert.Equal("AccentBrush", Assert.Single(
                picker.ResourcesView.Cast<Avalonia.Diagnostics.ViewModels.ResourceReferenceEntryViewModel>()).KeyDisplay);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resource_reference_picker_view_completes_selected_candidate()
    {
        var target = new Button { Background = Brushes.Blue };
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == TemplatedControl.BackgroundProperty));
        var candidate = new ResourceReferenceCandidate(
            "AccentBrush",
            "AccentBrush",
            Brushes.Red,
            typeof(ISolidColorBrush),
            "Application / Resources",
            null,
            DevToolsResourceReferenceKind.Static);
        var picker = new Avalonia.Diagnostics.ViewModels.ResourceReferencePickerViewModel(
            property,
            new[] { candidate },
            new ResourceNodeFormatter());
        var view = new ResourceReferencePickerView
        {
            DataContext = picker
        };
        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            picker.SelectedResource = Assert.Single(
                picker.ResourcesView.Cast<Avalonia.Diagnostics.ViewModels.ResourceReferenceEntryViewModel>());
            ResourceReferenceCandidate? completedCandidate = null;
            view.Completed += (_, completed) => completedCandidate = completed;

            var staticButton = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "StaticResource"));
            staticButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Same(candidate, completedCandidate);
            Assert.Same(candidate, view.SelectedCandidate);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resources_page_filter_updates_view_model()
    {
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(new Button());
        var formatter = new ResourceNodeFormatter();
        using var resources = new Avalonia.Diagnostics.ViewModels.ResourcesPageViewModel(
            mainViewModel,
            Array.Empty<Avalonia.Diagnostics.ViewModels.ResourceTreeNode>(),
            new ResourceHierarchyModelFactory(),
            formatter);
        var view = new ResourcesPageView
        {
            DataContext = resources
        };
        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var filterTextBox = Assert.Single(view.GetVisualDescendants().OfType<FilterTextBox>());
            var resourcesTree = view.GetControl<Control>("resourcesTree");
            var resourcesGrid = view.GetControl<Control>("resourcesGrid");
            AssertGridAllowsColumnResize(resourcesTree);
            AssertResourceGridConfiguration(resourcesGrid);
            AssertGridHasColumn(resourcesGrid, "Preview");

            filterTextBox.Text = "Accent";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Accent", resources.ResourcesFilter.FilterString);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tree_page_grid_allows_column_resize()
    {
        var target = new Button();
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var view = new TreePageTreeView
        {
            DataContext = tree
        };
        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            AssertGridAllowsColumnResize(view.GetControl<Control>("tree"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resource_wrapped_editor_can_be_reused_for_same_property_type()
    {
        var target = new Button
        {
            Background = Brushes.Blue,
            BorderBrush = Brushes.Gray
        };
        target.Resources["AccentBrush"] = Brushes.Red;

        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var properties = tree.Details!.PropertiesView!.Cast<object>().ToArray();
        var backgroundProperty = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            properties.Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                      property.Property == TemplatedControl.BackgroundProperty));
        var borderBrushProperty = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            properties.Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                      property.Property == TemplatedControl.BorderBrushProperty));
        var view = CreateView();

        view.DataContext = backgroundProperty;
        var firstHost = Assert.IsType<DockPanel>(view.Content);
        var editor = firstHost.Children[1];

        view.DataContext = borderBrushProperty;

        var secondHost = Assert.IsType<DockPanel>(view.Content);
        Assert.Same(editor, secondHost.Children[1]);
        Assert.DoesNotContain(editor, firstHost.Children);
    }

    [AvaloniaFact]
    public void Property_edit_handler_is_applied_to_existing_property_rows()
    {
        var target = new Button { Width = 24 };
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == Layoutable.WidthProperty));
        var handler = new RecordingPropertyEditHandler();

        mainViewModel.SetOptions(new DevToolsOptions { PropertyEditHandler = handler });
        property.Value = 120d;

        Assert.NotNull(handler.Edit);
        Assert.Same(target, handler.Edit!.InspectedObject);
    }

    [AvaloniaFact]
    public void Property_edit_handler_is_not_notified_for_effective_no_op()
    {
        var target = new Button { Width = 24 };
        using var mainViewModel = new Avalonia.Diagnostics.ViewModels.MainViewModel(target);
        var handler = new RecordingPropertyEditHandler();
        mainViewModel.SetOptions(new DevToolsOptions { PropertyEditHandler = handler });
        mainViewModel.SelectControl(target);
        var tree = Assert.IsType<Avalonia.Diagnostics.ViewModels.TreePageViewModel>(
            mainViewModel.GetContent(DevToolsViewKind.CombinedTree));
        var property = Assert.IsType<Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel>(
            tree.Details!.PropertiesView!.Cast<object>()
                .Single(item => item is Avalonia.Diagnostics.ViewModels.AvaloniaPropertyViewModel property &&
                                property.Property == Layoutable.WidthProperty));

        property.Value = 24d;

        Assert.Null(handler.Edit);
    }

    private static UserControl CreateView()
    {
        var viewType = typeof(DevToolsExtensions).Assembly
            .GetType("Avalonia.Diagnostics.Views.PropertyValueEditorView", throwOnError: true);
        return (UserControl)Activator.CreateInstance(viewType!, nonPublic: true)!;
    }

    private static object CreatePropertyViewModel(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        var viewModelType = typeof(DevToolsExtensions).Assembly
            .GetType("Avalonia.Diagnostics.ViewModels.ClrPropertyViewModel", throwOnError: true);
        return Activator.CreateInstance(
            viewModelType!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { target, property! },
            culture: CultureInfo.InvariantCulture)!;
    }

    private static Avalonia.Diagnostics.ViewModels.ResourceReferenceScopeViewModel? FindScope(
        Avalonia.Diagnostics.ViewModels.ResourceReferenceScopeViewModel scope,
        string scopePath)
    {
        if (scope.ScopePath == scopePath)
        {
            return scope;
        }

        foreach (var child in scope.Children)
        {
            var result = FindScope(child, scopePath);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void AssertResourceGridConfiguration(Control grid)
    {
        AssertGridAllowsColumnResize(grid);

        var gridType = grid.GetType();
        Assert.Equal(true, gridType.GetProperty("CanUserSortColumns")!.GetValue(grid));
        Assert.Equal(false, gridType.GetProperty("OwnsSortDescriptions")!.GetValue(grid));

        var filteringModel = gridType.GetProperty("FilteringModel")!.GetValue(grid);
        Assert.NotNull(filteringModel);
        Assert.Equal(false, filteringModel!.GetType().GetProperty("OwnsViewFilter")!.GetValue(filteringModel));
    }

    private static void AssertGridAllowsColumnResize(Control grid)
    {
        var gridType = grid.GetType();
        Assert.Equal("Avalonia.Controls.DataGrid", gridType.FullName);
        Assert.Equal(true, gridType.GetProperty("CanUserResizeColumns")!.GetValue(grid));
    }

    private static void AssertGridHasColumn(Control grid, object header)
    {
        var gridType = grid.GetType();
        var columnsProperty = gridType.GetProperty(
            "Columns",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(columnsProperty);

        var columns = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            columnsProperty!.GetValue(grid));

        Assert.Contains(columns.Cast<object>(), column =>
        {
            var headerProperty = column.GetType().GetProperty(
                "Header",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return headerProperty is not null && Equals(headerProperty.GetValue(column), header);
        });
    }

    private sealed class TestTarget
    {
        public bool Flag { get; set; }

        public string? Name { get; set; }

        public bool ReadOnlyFlag => true;
    }

    private sealed class RecordingPropertyEditHandler : IDevToolsPropertyEditHandler
    {
        public DevToolsPropertyEdit? Edit { get; private set; }

        public void OnPropertyEdited(DevToolsPropertyEdit edit)
        {
            Edit = edit;
        }
    }
}
