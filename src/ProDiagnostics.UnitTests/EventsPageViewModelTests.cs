using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Diagnostics.Models;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class EventsPageViewModelTests
{
    [AvaloniaFact]
    public void AddRecordedEvent_Trims_To_MaxRecordedEvents()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        viewModel.SetOptions(new DevToolsOptions
        {
            MaxRecordedEvents = 2
        });

        var first = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        var second = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        var third = CreateFiredEvent(TestControl.BubbleEvent, handled: false);

        viewModel.AddRecordedEvent(first);
        viewModel.AddRecordedEvent(second);
        viewModel.AddRecordedEvent(third);

        Assert.Equal(2, viewModel.RecordedEvents.Count);
        Assert.DoesNotContain(first, viewModel.RecordedEvents);
        Assert.Equal(2, viewModel.VisibleRecordedEvents);
    }

    [AvaloniaFact]
    public void AddRecordedEvent_Trimming_Clears_SelectedEvent_When_Selected_Was_Removed()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        viewModel.SetOptions(new DevToolsOptions
        {
            MaxRecordedEvents = 2
        });

        var first = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        var second = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        var third = CreateFiredEvent(TestControl.BubbleEvent, handled: false);

        viewModel.AddRecordedEvent(first);
        viewModel.AddRecordedEvent(second);
        viewModel.SelectedEvent = first;

        viewModel.AddRecordedEvent(third);

        Assert.Null(viewModel.SelectedEvent);
    }

    [AvaloniaFact]
    public void RecordedEventsView_Applies_Route_And_Handled_Filters()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);

        var handledDirect = CreateFiredEvent(TestControl.DirectEvent, handled: true);
        var unhandledBubble = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        viewModel.AddRecordedEvent(handledDirect);
        viewModel.AddRecordedEvent(unhandledBubble);

        Assert.Equal(2, viewModel.VisibleRecordedEvents);

        viewModel.IncludeHandledEvents = false;
        Assert.Single(viewModel.RecordedEventsView.Cast<object>());
        Assert.Same(unhandledBubble, viewModel.RecordedEventsView.Cast<FiredEvent>().Single());

        viewModel.IncludeHandledEvents = true;
        viewModel.IncludeBubbleRoutes = false;
        Assert.Single(viewModel.RecordedEventsView.Cast<object>());
        Assert.Same(handledDirect, viewModel.RecordedEventsView.Cast<FiredEvent>().Single());
    }

    [AvaloniaFact]
    public void RecordedEventsView_Filters_Using_Observed_Routes_Not_Declared_Strategies()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        var bubbleOnlyChain = CreateFiredEvent(
            TestControl.MixedEvent,
            handled: false,
            route: RoutingStrategies.Bubble);
        viewModel.AddRecordedEvent(bubbleOnlyChain);

        viewModel.IncludeBubbleRoutes = false;
        viewModel.IncludeTunnelRoutes = true;
        viewModel.IncludeDirectRoutes = false;

        Assert.Empty(viewModel.RecordedEventsView.Cast<object>());
    }

    [AvaloniaFact]
    public void RemoteRecordedEvents_Preserve_Full_EventChain_And_Filter_Locally()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        var ownerType = TestControl.MixedEvent.OwnerType.FullName ?? TestControl.MixedEvent.OwnerType.Name;
        var remoteEvent = new FiredEvent(
            recordId: "remote-1",
            triggerTime: DateTime.UtcNow,
            eventName: TestControl.MixedEvent.Name,
            eventOwnerType: ownerType,
            sourceDisplay: "TestButton (Button)",
            originatorDisplay: "RootWindow (Window)",
            handledByDisplay: "TestButton (Button)",
            observedRoutes: RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            isHandled: true,
            sourceNodeId: "node-button",
            sourceNodePath: "0/0/0",
            remoteEventChain: new[]
            {
                new EventChainLink(handler: null, handled: false, RoutingStrategies.Tunnel, handlerNameOverride: "RootWindow (Window)", remoteNodeId: "node-window", remoteNodePath: "0"),
                new EventChainLink(handler: null, handled: false, RoutingStrategies.Bubble, handlerNameOverride: "RootPanel (Panel)", remoteNodeId: "node-panel", remoteNodePath: "0/0"),
                new EventChainLink(handler: null, handled: true, RoutingStrategies.Bubble, handlerNameOverride: "TestButton (Button)", remoteNodeId: "node-button", remoteNodePath: "0/0/0"),
            });

        viewModel.AddRecordedEvent(remoteEvent);

        var recorded = Assert.Single(viewModel.RecordedEvents);
        Assert.Equal(3, recorded.EventChain.Count);
        Assert.Equal("0/0", recorded.EventChain[1].RemoteNodePath);
        Assert.Equal("0/0/0", recorded.HandledBy?.RemoteNodePath);

        viewModel.IncludeHandledEvents = false;
        Assert.Empty(viewModel.RecordedEventsView.Cast<object>());

        viewModel.IncludeHandledEvents = true;
        viewModel.IncludeBubbleRoutes = false;
        viewModel.IncludeDirectRoutes = false;
        viewModel.IncludeTunnelRoutes = true;
        Assert.Single(viewModel.RecordedEventsView.Cast<object>());

        viewModel.IncludeTunnelRoutes = false;
        Assert.Empty(viewModel.RecordedEventsView.Cast<object>());
    }

    [AvaloniaFact]
    public void SetOptions_Uses_Custom_Default_Routed_Events()
    {
        _ = TestControl.BubbleEvent;
        _ = TestControl.DirectEvent;

        var viewModel = new EventsPageViewModel(mainViewModel: null);

        viewModel.SetOptions(new DevToolsOptions
        {
            DefaultRoutedEvents = new[] { TestControl.DirectEvent }
        });

        var directNode = FindNode(viewModel, TestControl.DirectEvent);
        var bubbleNode = FindNode(viewModel, TestControl.BubbleEvent);

        Assert.NotNull(directNode);
        Assert.NotNull(bubbleNode);
        Assert.True(directNode!.IsEnabled);
        Assert.False(bubbleNode!.IsEnabled);
    }

    [AvaloniaFact]
    public void SetOptions_Without_Custom_Defaults_Resets_To_BuiltIn_Defaults()
    {
        _ = TestControl.DirectEvent;

        var viewModel = new EventsPageViewModel(mainViewModel: null);

        viewModel.SetOptions(new DevToolsOptions
        {
            DefaultRoutedEvents = new[] { TestControl.DirectEvent }
        });

        var customNode = FindNode(viewModel, TestControl.DirectEvent);
        Assert.NotNull(customNode);
        Assert.True(customNode!.IsEnabled);

        viewModel.SetOptions(new DevToolsOptions());

        customNode = FindNode(viewModel, TestControl.DirectEvent);
        Assert.NotNull(customNode);
        Assert.False(customNode!.IsEnabled);
    }

    [AvaloniaFact]
    public void Search_Selection_And_Remove_Selected_Record_Work()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        var first = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        var second = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        viewModel.AddRecordedEvent(first);
        viewModel.AddRecordedEvent(second);

        Assert.True(viewModel.SelectNextMatch());
        Assert.Same(first, viewModel.SelectedEvent);

        Assert.True(viewModel.SelectNextMatch());
        Assert.Same(second, viewModel.SelectedEvent);

        Assert.True(viewModel.SelectPreviousMatch());
        Assert.Same(first, viewModel.SelectedEvent);

        Assert.True(viewModel.RemoveSelectedRecord());
        Assert.Null(viewModel.SelectedEvent);
        Assert.Single(viewModel.RecordedEvents);
        Assert.DoesNotContain(first, viewModel.RecordedEvents);
    }

    [AvaloniaFact]
    public void ClearSelectionOrFilter_Clears_Selection_First_Then_Filter()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        var first = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        viewModel.AddRecordedEvent(first);
        viewModel.SelectedEvent = first;
        viewModel.EventsFilter.FilterString = "bubble";

        Assert.True(viewModel.ClearSelectionOrFilter());
        Assert.Null(viewModel.SelectedEvent);
        Assert.Equal("bubble", viewModel.EventsFilter.FilterString);

        Assert.True(viewModel.ClearSelectionOrFilter());
        Assert.Equal(string.Empty, viewModel.EventsFilter.FilterString);
    }

    [AvaloniaFact]
    public void Clear_Resets_SelectedEvent()
    {
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        var first = CreateFiredEvent(TestControl.BubbleEvent, handled: false);
        viewModel.AddRecordedEvent(first);
        viewModel.SelectedEvent = first;

        viewModel.Clear();

        Assert.Null(viewModel.SelectedEvent);
        Assert.Empty(viewModel.RecordedEvents);
    }

    private static FiredEvent CreateFiredEvent(
        RoutedEvent routedEvent,
        bool handled,
        RoutingStrategies? route = null)
    {
        var args = new RoutedEventArgs(routedEvent);
        var originator = new EventChainLink(new TestControl(), handled, route ?? routedEvent.RoutingStrategies);
        return new FiredEvent(args, originator, DateTime.UtcNow);
    }

    private static EventTreeNode? FindNode(EventsPageViewModel viewModel, RoutedEvent routedEvent)
    {
        foreach (var root in viewModel.Nodes)
        {
            var match = FindNodeRecursive(root, routedEvent);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static EventTreeNode? FindNodeRecursive(EventTreeNodeBase node, RoutedEvent routedEvent)
    {
        if (node is EventTreeNode eventTreeNode && eventTreeNode.Event == routedEvent)
        {
            return eventTreeNode;
        }

        if (node.Children == null)
        {
            return null;
        }

        foreach (var child in node.Children)
        {
            var match = FindNodeRecursive(child, routedEvent);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class TestControl : Control
    {
        public static readonly RoutedEvent<RoutedEventArgs> BubbleEvent =
            RoutedEvent.Register<TestControl, RoutedEventArgs>(nameof(BubbleEvent), RoutingStrategies.Bubble);

        public static readonly RoutedEvent<RoutedEventArgs> DirectEvent =
            RoutedEvent.Register<TestControl, RoutedEventArgs>(nameof(DirectEvent), RoutingStrategies.Direct);

        public static readonly RoutedEvent<RoutedEventArgs> MixedEvent =
            RoutedEvent.Register<TestControl, RoutedEventArgs>(
                nameof(MixedEvent),
                RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
    }
}
