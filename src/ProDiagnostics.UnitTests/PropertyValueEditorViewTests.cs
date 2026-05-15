using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.Services;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
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
        var picker = Assert.IsType<ComboBox>(host.Children[0]);
        var dynamicResource = picker.Items
            .Cast<ResourceReferenceCandidate>()
            .Single(candidate => candidate.Kind == DevToolsResourceReferenceKind.Dynamic &&
                                 candidate.KeyText == "AccentBrush");

        picker.SelectedItem = dynamicResource;

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
