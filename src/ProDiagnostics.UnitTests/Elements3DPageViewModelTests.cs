using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class Elements3DPageViewModelTests
{
    [AvaloniaFact]
    public void InspectRoot_Captures_Visual_Hierarchy()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var child = new Button { Name = "ChildButton" };
        root.Children.Add(child);

        var viewModel = new Elements3DPageViewModel(root, () => child);
        viewModel.InspectRoot();

        Assert.True(viewModel.NodeCount >= 2);
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("StackPanel#RootPanel"));
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("Button#ChildButton"));
    }

    [AvaloniaFact]
    public void InspectSelection_Uses_Selected_Object_Accessor()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var child = new TextBlock { Name = "ChildText", Text = "Hello" };
        root.Children.Add(child);

        var viewModel = new Elements3DPageViewModel(root, () => child);
        viewModel.InspectSelection();

        Assert.Contains("TextBlock#ChildText", viewModel.InspectedRoot);
        Assert.True(viewModel.NodeCount >= 1);
    }

    [AvaloniaFact]
    public void InspectRoot_Selects_Root_Node_And_ResetProjectionView_Restores_Defaults()
    {
        var root = new StackPanel { Name = "RootPanel" };
        root.Children.Add(new Border { Name = "Container" });

        var viewModel = new Elements3DPageViewModel(root, () => root);
        viewModel.InspectRoot();

        Assert.NotNull(viewModel.SelectedNode);
        Assert.Contains("StackPanel#RootPanel", viewModel.SelectedNodeSummary);
        Assert.True(viewModel.SelectedNode!.BoundsRect.Width >= 0);

        viewModel.DepthSpacing = 40;
        viewModel.Flat2DMaxLayersPerRow = 8;
        viewModel.Tilt = 0.2;
        viewModel.Zoom = 2;
        viewModel.OrbitYaw = 45;
        viewModel.OrbitPitch = -20;
        viewModel.OrbitRoll = 30;
        viewModel.ResetProjectionView();

        Assert.Equal(24, viewModel.DepthSpacing, 3);
        Assert.Equal(0, viewModel.Flat2DMaxLayersPerRow);
        Assert.Equal(0.55, viewModel.Tilt, 3);
        Assert.Equal(1, viewModel.Zoom, 3);
        Assert.Equal(0, viewModel.OrbitYaw, 3);
        Assert.Equal(0, viewModel.OrbitPitch, 3);
        Assert.Equal(0, viewModel.OrbitRoll, 3);
    }

    [AvaloniaFact]
    public void InspectControl_Scopes_To_Selected_Subtree()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var child = new Border { Name = "ChildBorder" };
        var grandChild = new Button { Name = "GrandChildButton" };
        child.Child = grandChild;
        root.Children.Add(child);

        var viewModel = new Elements3DPageViewModel(root, () => child);
        viewModel.InspectControl(child);

        Assert.Equal("Border#ChildBorder", viewModel.InspectedRoot);
        Assert.DoesNotContain(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("StackPanel#RootPanel"));
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("Button#GrandChildButton"));
    }

    [AvaloniaFact]
    public void ScopeSelectedNodeAsRoot_And_ResetToMainRoot_Work_As_Expected()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var child = new Border { Name = "ChildBorder" };
        child.Child = new Button { Name = "GrandChildButton" };
        root.Children.Add(child);

        var viewModel = new Elements3DPageViewModel(root, () => child);
        viewModel.InspectRoot();
        var childNode = viewModel.NodesView
            .Cast<Elements3DNodeViewModel>()
            .First(x => x.Node.Contains("Border#ChildBorder"));
        viewModel.SelectedNode = childNode;

        Assert.True(viewModel.CanScopeSelectedNodeAsRoot);
        viewModel.ScopeSelectedNodeAsRoot();

        Assert.Contains("StackPanel#RootPanel", viewModel.InspectedRoot);
        Assert.Contains("Border#ChildBorder", viewModel.InspectedRoot);
        Assert.True(viewModel.CanResetToMainRoot);
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("StackPanel#RootPanel"));
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("Border#ChildBorder"));

        viewModel.ResetToMainRoot();

        Assert.Contains("StackPanel#RootPanel", viewModel.InspectedRoot);
        Assert.False(viewModel.CanResetToMainRoot);
    }

    [AvaloniaFact]
    public void ScopeSelectedNodeAsRoot_Uses_Selected_Visual_TopMost_Ancestor_When_MainRoot_Is_Different_Tree()
    {
        var mainRoot = new StackPanel { Name = "MainRoot" };
        mainRoot.Children.Add(new TextBlock { Name = "MainChild", Text = "A" });

        var otherRoot = new StackPanel { Name = "OtherRoot" };
        var otherChild = new Border { Name = "OtherChild" };
        otherRoot.Children.Add(otherChild);

        var viewModel = new Elements3DPageViewModel(mainRoot, () => otherChild);

        // Simulate tree-selection sync driving Elements3D to a different visual tree
        // than the startup root.
        viewModel.InspectControl(otherChild);
        var selectedNode = viewModel.NodesView
            .Cast<Elements3DNodeViewModel>()
            .First(x => x.Node.Contains("Border#OtherChild"));
        viewModel.SelectedNode = selectedNode;

        viewModel.ScopeSelectedNodeAsRoot();

        Assert.Contains("StackPanel#OtherRoot", viewModel.InspectedRoot);
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("StackPanel#OtherRoot"));
        Assert.Contains(
            viewModel.NodesView.Cast<Elements3DNodeViewModel>(),
            x => x.Node.Contains("Border#OtherChild"));
    }

    [AvaloniaFact]
    public void DepthRange_Filter_Limits_Visible_Layers()
    {
        var root = new StackPanel { Name = "RootPanel" };
        var first = new Border { Name = "FirstBorder", Child = new Button { Name = "FirstButton" } };
        var second = new Border { Name = "SecondBorder", Child = new Button { Name = "SecondButton" } };
        root.Children.Add(first);
        root.Children.Add(second);

        var viewModel = new Elements3DPageViewModel(root, () => root);
        viewModel.InspectRoot();

        viewModel.MinVisibleDepth = 1;
        viewModel.MaxVisibleDepth = 1;

        Assert.NotEmpty(viewModel.VisibleNodes);
        Assert.All(viewModel.VisibleNodes, node => Assert.Equal(1, node.Depth));
    }

    [AvaloniaFact]
    public void MaxVisibleElements_Filter_Limits_Visible_Node_Count_And_Reset_Restores_Full_Range()
    {
        var root = new StackPanel { Name = "RootPanel" };
        for (var i = 0; i < 12; i++)
        {
            root.Children.Add(new Border { Name = "Border" + i });
        }

        var viewModel = new Elements3DPageViewModel(root, () => root);
        viewModel.InspectRoot();
        var fullVisibleCount = viewModel.VisibleNodeCount;
        Assert.True(fullVisibleCount > 4);

        viewModel.MaxVisibleElements = 4;
        Assert.Equal(4, viewModel.VisibleNodeCount);
        Assert.Equal(4, viewModel.VisibleNodes.Count);

        viewModel.MinVisibleDepth = 1;
        viewModel.MaxVisibleDepth = 1;
        viewModel.ResetLayerVisibilityFilters();

        Assert.Equal(viewModel.AvailableMinDepth, viewModel.MinVisibleDepth);
        Assert.Equal(viewModel.AvailableMaxDepth, viewModel.MaxVisibleDepth);
        Assert.Equal(0, viewModel.MaxVisibleElements);
        Assert.Equal(fullVisibleCount, viewModel.VisibleNodeCount);
    }

    [AvaloniaFact]
    public void ShowAllLayersInGrid_Mode_Ignores_Depth_And_MaxLayer_Limits_For_Grid_Listing()
    {
        var root = new StackPanel { Name = "RootPanel" };
        for (var i = 0; i < 4; i++)
        {
            root.Children.Add(new Border
            {
                Name = "Border" + i,
                Child = new Button { Name = "Button" + i }
            });
        }

        var viewModel = new Elements3DPageViewModel(root, () => root);
        viewModel.InspectRoot();

        viewModel.MinVisibleDepth = 2;
        viewModel.MaxVisibleDepth = 2;
        viewModel.MaxVisibleElements = 1;

        var filteredGridNodes = viewModel.GridNodesView.Cast<Elements3DNodeViewModel>().ToArray();
        Assert.Single(filteredGridNodes);
        Assert.All(filteredGridNodes, node => Assert.Equal(2, node.Depth));

        viewModel.ShowAllLayersInGrid = true;

        var allGridNodes = viewModel.GridNodesView.Cast<Elements3DNodeViewModel>().ToArray();
        Assert.True(allGridNodes.Length > filteredGridNodes.Length);
        Assert.Contains(allGridNodes, node => node.Depth == 0);
        Assert.Contains(allGridNodes, node => node.Depth == 1);
        Assert.True(viewModel.ShowExploded3DView);
    }

    [AvaloniaFact]
    public void Selection_Is_Preserved_When_Depth_Filter_Hides_Selected_Node()
    {
        var root = new StackPanel { Name = "RootPanel" };
        root.Children.Add(new Border { Name = "Border0" });
        root.Children.Add(new Border { Name = "Border1" });

        var viewModel = new Elements3DPageViewModel(root, () => root);
        viewModel.InspectRoot();
        var rootNode = viewModel.NodesView
            .Cast<Elements3DNodeViewModel>()
            .First(x => x.Node.Contains("StackPanel#RootPanel"));

        viewModel.SelectedNode = rootNode;
        viewModel.MinVisibleDepth = 1;
        viewModel.MaxVisibleDepth = 1;

        Assert.Same(rootNode, viewModel.SelectedNode);
    }
}
