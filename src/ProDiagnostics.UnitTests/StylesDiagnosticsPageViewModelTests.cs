using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class StylesDiagnosticsPageViewModelTests
{
    [AvaloniaFact]
    public void InspectSelection_Builds_Style_Resolution_Trace_For_Styling_Parent_Chain()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var child = new Button { Name = "ChildButton" };
        root.Children.Add(child);

        root.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.ContentProperty, "root")
            }
        });
        child.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.FontSizeProperty, 13d)
            }
        });

        var viewModel = new StylesDiagnosticsPageViewModel(() => child);
        viewModel.InspectSelection();
        var childEntry = viewModel.TreeEntriesView
            .Cast<StylesTreeEntryViewModel>()
            .First(x => ReferenceEquals(x.SourceObject, child));
        viewModel.SelectedTreeEntry = childEntry;

        var traceEntries = viewModel.ResolutionEntriesView
            .Cast<StyleResolutionTraceEntryViewModel>()
            .ToList();

        Assert.NotEmpty(traceEntries);
        Assert.True(viewModel.ResolutionEntryCount >= traceEntries.Count);
        Assert.Contains(traceEntries, x => x.Host.Contains("StackPanel#RootPanel"));
        Assert.Contains(traceEntries, x => x.Host.Contains("Button#ChildButton"));
        Assert.Contains(traceEntries, x => x.StyleKind == "Style");
        Assert.Contains(traceEntries, x => x.Host.Contains("StackPanel#RootPanel") && x.PropagationScope.StartsWith("Logical ancestor", System.StringComparison.Ordinal));
        Assert.Contains(traceEntries, x => x.Host.Contains("Button#ChildButton") && x.LogicalDistance == 0);
    }

    [AvaloniaFact]
    public void Clear_Resets_Style_Resolution_Trace()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var child = new Button { Name = "ChildButton" };
        root.Children.Add(child);
        child.Styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.ContentProperty, "value")
            }
        });

        var viewModel = new StylesDiagnosticsPageViewModel(() => child);
        viewModel.InspectSelection();
        var childEntry = viewModel.TreeEntriesView
            .Cast<StylesTreeEntryViewModel>()
            .First(x => ReferenceEquals(x.SourceObject, child));
        viewModel.SelectedTreeEntry = childEntry;

        Assert.True(viewModel.ResolutionEntryCount > 0);

        viewModel.Clear();

        Assert.Equal(0, viewModel.ResolutionEntryCount);
        Assert.Equal(0, viewModel.VisibleResolutionEntryCount);
    }
}
