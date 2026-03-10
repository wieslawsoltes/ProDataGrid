using System;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
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
    public void Remote_boolean_property_uses_editable_checkbox()
    {
        var view = CreateView();
        view.DataContext = new RemotePropertyViewModel(
            name: "IsEnabled",
            group: "Properties",
            displayType: "Boolean",
            assignedTypeName: typeof(bool).AssemblyQualifiedName,
            propertyTypeName: typeof(bool).AssemblyQualifiedName,
            declaringTypeName: typeof(Button).AssemblyQualifiedName,
            priority: "LocalValue",
            isAttached: false,
            isReadOnly: false,
            valueText: "false",
            propertyKind: "avalonia",
            editorKind: "boolean");

        var editor = Assert.IsType<CheckBox>(view.Content);
        Assert.True(editor.IsEnabled);
        Assert.False(editor.IsChecked);
    }

    [AvaloniaFact]
    public void Remote_enum_property_without_resolved_type_uses_editable_combo_box()
    {
        var view = CreateView();
        view.DataContext = new RemotePropertyViewModel(
            name: "Mode",
            group: "CLR Properties",
            displayType: "SampleMode",
            assignedTypeName: "TestHost.SampleMode, Missing.Assembly",
            propertyTypeName: "TestHost.SampleMode, Missing.Assembly",
            declaringTypeName: typeof(TestTarget).AssemblyQualifiedName,
            priority: string.Empty,
            isAttached: false,
            isReadOnly: false,
            valueText: "Advanced",
            propertyKind: "clr",
            editorKind: "enum",
            enumOptions: new[] { "Basic", "Advanced" });

        var editor = Assert.IsType<ComboBox>(view.Content);
        Assert.True(editor.IsEnabled);
        Assert.Equal("Advanced", editor.SelectedItem);
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
}
