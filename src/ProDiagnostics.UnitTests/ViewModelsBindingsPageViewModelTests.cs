using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class ViewModelsBindingsPageViewModelTests
{
    [AvaloniaFact]
    public void InspectControl_Collects_ViewModel_Chain_And_Bound_Properties()
    {
        var host = new StackPanel();
        var textBlock = new TextBlock();
        host.Children.Add(textBlock);
        host.DataContext = new TestViewModel { Name = "Alice" };
        textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(TestViewModel.Name)));

        var diagnostics = new ViewModelsBindingsPageViewModel(selectedObjectAccessor: null);
        diagnostics.InspectControl(textBlock);

        Assert.True(diagnostics.BindingCount > 0);
        Assert.Contains(
            diagnostics.BindingEntriesView.Cast<BindingDiagnosticEntryViewModel>(),
            x => x.PropertyName == nameof(TextBlock.Text));

        Assert.True(diagnostics.ViewModelCount > 0);
        Assert.Contains(
            diagnostics.ViewModelEntriesView.Cast<ViewModelContextEntryViewModel>(),
            x => x.ViewModelType.Contains(nameof(TestViewModel)));
    }

    [AvaloniaFact]
    public void InspectSelection_Uses_Selected_Object_Accessor()
    {
        var textBlock = new TextBlock();
        textBlock.DataContext = new TestViewModel { Name = "Bob" };
        textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(TestViewModel.Name)));

        var diagnostics = new ViewModelsBindingsPageViewModel(() => textBlock);
        diagnostics.InspectSelection();

        Assert.Equal("TextBlock", diagnostics.InspectedElement);
        Assert.True(diagnostics.BindingCount > 0);
    }

    [AvaloniaFact]
    public void Clear_Resets_Entries_And_Selected_Label()
    {
        var textBlock = new TextBlock();
        textBlock.DataContext = new TestViewModel { Name = "Charlie" };
        textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(TestViewModel.Name)));

        var diagnostics = new ViewModelsBindingsPageViewModel(selectedObjectAccessor: null);
        diagnostics.InspectControl(textBlock);
        Assert.True(diagnostics.BindingCount > 0);

        diagnostics.Clear();

        Assert.Equal(0, diagnostics.BindingCount);
        Assert.Equal(0, diagnostics.ViewModelCount);
        Assert.Equal("(none)", diagnostics.InspectedElement);
    }

    private sealed class TestViewModel
    {
        public string Name { get; set; } = string.Empty;
    }
}
