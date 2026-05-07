using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Diagnostics;

public class MainViewModelSelectionTests
{
    [AvaloniaFact]
    public void SelectControl_Selects_Matching_Node_In_All_Tree_Pages()
    {
        var root = new StackPanel { Name = "Root" };
        var panel = new StackPanel { Name = "Panel" };
        var button = new Button { Name = "Button" };

        panel.Children.Add(button);
        root.Children.Add(panel);

        using var viewModel = new MainViewModel(root);

        viewModel.SelectControl(button);

        AssertSelected(viewModel, DevToolsViewKind.CombinedTree, button);
        AssertSelected(viewModel, DevToolsViewKind.LogicalTree, button);
        AssertSelected(viewModel, DevToolsViewKind.VisualTree, button);
    }

    private static void AssertSelected(MainViewModel viewModel, DevToolsViewKind viewKind, Control expected)
    {
        var tree = Assert.IsType<TreePageViewModel>(viewModel.GetContent(viewKind));
        Assert.NotNull(tree.SelectedNode);
        Assert.Same(expected, tree.SelectedNode.Visual);
    }
}
