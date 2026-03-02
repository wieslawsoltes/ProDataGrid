using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class MainViewModelViewModelsBindingsTests
{
    [AvaloniaFact]
    public void SelectedTab_ViewModelsBindings_AutoInspects_Current_Context()
    {
        var root = new Window
        {
            DataContext = new TestViewModel { Name = "Alice" }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 11;

        var content = Assert.IsType<ViewModelsBindingsPageViewModel>(viewModel.Content);
        Assert.NotEqual("(none)", content.InspectedElement);
        Assert.True(content.ViewModelCount > 0);
    }

    [AvaloniaFact]
    public void SelectedTab_ViewModelsBindings_Uses_App_TopLevel_When_Root_Is_Group()
    {
        var window = new Window
        {
            DataContext = new TestViewModel { Name = "Bob" }
        };
        var root = new TopLevelGroup(new SingleViewTopLevelGroup(window));

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 11;

        var content = Assert.IsType<ViewModelsBindingsPageViewModel>(viewModel.Content);
        Assert.NotEqual("(none)", content.InspectedElement);
        Assert.Contains("Window", content.InspectedElementType);
        Assert.True(content.ViewModelCount > 0);
    }

    [AvaloniaFact]
    public void CtrlShift_Selection_Path_Updates_ViewModelsBindings_When_Tab_Is_Not_Tree()
    {
        var button = new Button();
        var host = new StackPanel
        {
            DataContext = new TestViewModel { Name = "Carol" },
            Children = { button }
        };
        var root = new Window
        {
            Content = host
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 11;

        var content = Assert.IsType<ViewModelsBindingsPageViewModel>(viewModel.Content);
        viewModel.SelectControl(button);

        Assert.Equal("Button", content.InspectedElement);
        Assert.Contains("Button", content.InspectedElementType, StringComparison.OrdinalIgnoreCase);
        Assert.True(content.ViewModelCount > 0);
    }

    [AvaloniaFact]
    public void CtrlShift_Selection_Path_Updates_Elements3DScope_When_Tab_Is_Elements3D()
    {
        var button = new Button { Name = "ScopedButton" };
        var host = new StackPanel
        {
            Name = "HostPanel",
            Children = { button }
        };
        var root = new Window
        {
            Content = host
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 4;

        var content = Assert.IsType<Elements3DPageViewModel>(viewModel.Content);
        viewModel.SelectControl(button);

        Assert.Contains("Button#ScopedButton", content.InspectedRoot);
    }

    [AvaloniaFact]
    public void SelectedTab_Elements3D_AutoInspects_Current_Selection()
    {
        var button = new Button { Name = "ScopedButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectControl(button);
        viewModel.SelectedTab = 4;

        var content = Assert.IsType<Elements3DPageViewModel>(viewModel.Content);
        Assert.Contains("Button#ScopedButton", content.InspectedRoot);
    }

    [AvaloniaFact]
    public void SelectedTab_Styles_AutoInspects_Current_Selection()
    {
        var button = new Button { Name = "StyledButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectControl(button);
        viewModel.SelectedTab = 13;

        var content = Assert.IsType<StylesDiagnosticsPageViewModel>(viewModel.Content);
        Assert.Equal("Button#StyledButton", content.InspectedRoot);
        Assert.True(content.TreeNodeCount > 0);
    }

    [AvaloniaFact]
    public void CtrlShift_Selection_Path_Updates_StylesScope_When_Tab_Is_Styles()
    {
        var first = new Button { Name = "FirstStyledButton" };
        var second = new Button { Name = "SecondStyledButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { first, second }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 13;

        var content = Assert.IsType<StylesDiagnosticsPageViewModel>(viewModel.Content);
        viewModel.SelectControl(second);

        Assert.Equal("Button#SecondStyledButton", content.InspectedRoot);
        Assert.True(content.TreeNodeCount > 0);
    }

    [AvaloniaFact]
    public void CombinedTree_ManualSelection_Updates_Elements3D_SelectionScope()
    {
        var button = new Button { Name = "ManualScopeButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 0;

        var combinedTree = Assert.IsType<TreePageViewModel>(viewModel.Content);
        var node = FindNode(combinedTree.Nodes, button);
        Assert.NotNull(node);

        combinedTree.SelectedNode = node;

        viewModel.SelectedTab = 4;
        var content = Assert.IsType<Elements3DPageViewModel>(viewModel.Content);
        Assert.Contains("Button#ManualScopeButton", content.InspectedRoot);
    }

    [AvaloniaFact]
    public void CombinedTree_HierarchicalWrapperSelection_Updates_Elements3D_SelectionScope()
    {
        var button = new Button { Name = "WrapperScopeButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 0;

        var combinedTree = Assert.IsType<TreePageViewModel>(viewModel.Content);
        var node = FindNode(combinedTree.Nodes, button);
        Assert.NotNull(node);
        combinedTree.SelectedNodeItem = new SelectionItemWrapper(node!);

        viewModel.SelectedTab = 4;
        var content = Assert.IsType<Elements3DPageViewModel>(viewModel.Content);
        Assert.Contains("Button#WrapperScopeButton", content.InspectedRoot);
    }

    [AvaloniaFact]
    public void CombinedTree_ExplicitInterfaceSelectionWrapper_Updates_SelectedNode_And_Details()
    {
        var first = new Button { Name = "FirstWrapperButton" };
        var second = new Button { Name = "SecondWrapperButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { first, second }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 0;

        var combinedTree = Assert.IsType<TreePageViewModel>(viewModel.Content);
        var firstNode = FindNode(combinedTree.Nodes, first);
        var secondNode = FindNode(combinedTree.Nodes, second);
        Assert.NotNull(firstNode);
        Assert.NotNull(secondNode);

        combinedTree.SelectedNode = firstNode;
        combinedTree.SelectedNodeItem = new ExplicitSelectionItemWrapper(secondNode!);

        Assert.Same(secondNode, combinedTree.SelectedNode);
        Assert.NotNull(combinedTree.Details);
        Assert.Same(secondNode!.Visual, combinedTree.Details!.SelectedEntity);
    }

    [AvaloniaFact]
    public void CombinedTree_UnresolvedSelectionToken_Reasserts_Current_SelectedNodeItem()
    {
        var button = new Button { Name = "StableSelectionButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 0;

        var combinedTree = Assert.IsType<TreePageViewModel>(viewModel.Content);
        var node = FindNode(combinedTree.Nodes, button);
        Assert.NotNull(node);
        combinedTree.SelectedNode = node;

        combinedTree.SelectedNodeItem = new object();

        Assert.Same(node, combinedTree.SelectedNode);
        Assert.Same(node, combinedTree.SelectedNodeItem);
    }

    [AvaloniaFact]
    public void CombinedTree_UnresolvedSelectionToken_DoesNotClear_Elements3DScope()
    {
        var button = new Button { Name = "StableScopeButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 0;

        var combinedTree = Assert.IsType<TreePageViewModel>(viewModel.Content);
        var node = FindNode(combinedTree.Nodes, button);
        Assert.NotNull(node);
        combinedTree.SelectedNode = node;

        combinedTree.SelectedNodeItem = new object();

        viewModel.SelectedTab = 4;
        var content = Assert.IsType<Elements3DPageViewModel>(viewModel.Content);
        Assert.Contains("Button#StableScopeButton", content.InspectedRoot);
    }

    [AvaloniaFact]
    public void CombinedTree_TransientNullSelectedItem_DoesNotClear_Elements3DScope()
    {
        var button = new Button { Name = "TransientNullButton" };
        var root = new Window
        {
            Content = new StackPanel
            {
                Name = "HostPanel",
                Children = { button }
            }
        };

        using var viewModel = new MainViewModel(root);
        viewModel.SelectedTab = 0;

        var combinedTree = Assert.IsType<TreePageViewModel>(viewModel.Content);
        var node = FindNode(combinedTree.Nodes, button);
        Assert.NotNull(node);
        combinedTree.SelectedNode = node;

        combinedTree.SelectedNodeItem = null;

        viewModel.SelectedTab = 4;
        var content = Assert.IsType<Elements3DPageViewModel>(viewModel.Content);
        Assert.Contains("Button#TransientNullButton", content.InspectedRoot);
    }

    private static TreeNode? FindNode(TreeNode[] roots, AvaloniaObject target)
    {
        for (var i = 0; i < roots.Length; i++)
        {
            var found = FindNode(roots[i], target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static TreeNode? FindNode(TreeNode node, AvaloniaObject target)
    {
        if (ReferenceEquals(node.Visual, target))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindNode(child, target);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class SelectionItemWrapper
    {
        public SelectionItemWrapper(object item)
        {
            Item = item;
        }

        public object Item { get; }
    }

    private interface IExplicitSelectionItemWrapper
    {
        object Item { get; }
    }

    private sealed class ExplicitSelectionItemWrapper : IExplicitSelectionItemWrapper
    {
        private readonly object _item;

        public ExplicitSelectionItemWrapper(object item)
        {
            _item = item;
        }

        object IExplicitSelectionItemWrapper.Item => _item;
    }

    private sealed class TestViewModel
    {
        public string Name { get; set; } = string.Empty;
    }
}
