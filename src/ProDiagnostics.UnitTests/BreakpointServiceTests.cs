using System;
using Avalonia.Controls;
using Avalonia.Diagnostics.Models;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class BreakpointServiceTests
{
    [AvaloniaFact]
    public void EventBreakpoint_Global_Hits_When_Source_Matches_Sender()
    {
        var service = new BreakpointService();
        var control = new BreakpointTestControl();
        var entry = service.AddEventBreakpoint(BreakpointTestControl.BubbleEvent, target: null, targetDescription: "(global)");

        service.EvaluateEvent(BreakpointTestControl.BubbleEvent, control, control);

        Assert.Equal(1, entry.HitCount);
        Assert.NotNull(entry.LastHitAt);
        Assert.Contains("BubbleEvent", entry.LastHitDetails, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void EventBreakpoint_Targeted_Only_Hits_Target()
    {
        var service = new BreakpointService();
        var target = new BreakpointTestControl();
        var other = new BreakpointTestControl();
        var entry = service.AddEventBreakpoint(BreakpointTestControl.BubbleEvent, target, "target");

        service.EvaluateEvent(BreakpointTestControl.BubbleEvent, other, other);
        Assert.Equal(0, entry.HitCount);

        service.EvaluateEvent(BreakpointTestControl.BubbleEvent, target, target);
        Assert.Equal(1, entry.HitCount);
    }

    [AvaloniaFact]
    public void EventBreakpoint_TriggerAfterHits_And_RemoveOnceHit_Work()
    {
        var service = new BreakpointService();
        var control = new BreakpointTestControl();
        var entry = service.AddEventBreakpoint(BreakpointTestControl.BubbleEvent, target: null, targetDescription: "(global)");
        entry.TriggerAfterHits = 2;
        entry.RemoveOnceHit = true;

        service.EvaluateEvent(BreakpointTestControl.BubbleEvent, control, control);
        Assert.Equal(1, entry.HitCount);
        Assert.Null(entry.LastHitAt);
        Assert.Contains(entry, service.Entries);

        service.EvaluateEvent(BreakpointTestControl.BubbleEvent, control, control);
        Assert.Equal(2, entry.HitCount);
        Assert.NotNull(entry.LastHitAt);
        Assert.DoesNotContain(entry, service.Entries);
    }

    [AvaloniaFact]
    public void PropertyBreakpoint_Hits_For_Target_And_Records_Values()
    {
        var service = new BreakpointService();
        var target = new BreakpointTestControl();
        var other = new BreakpointTestControl();
        var entry = service.AddPropertyBreakpoint(BreakpointTestControl.ValueProperty, target, "target");

        service.EvaluateProperty(BreakpointTestControl.ValueProperty, other, 1, 2);
        Assert.Equal(0, entry.HitCount);

        service.EvaluateProperty(BreakpointTestControl.ValueProperty, target, 1, 2);
        Assert.Equal(1, entry.HitCount);
        Assert.Contains("Old='1'", entry.LastHitDetails, StringComparison.Ordinal);
        Assert.Contains("New='2'", entry.LastHitDetails, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void PropertyBreakpoint_TargetSubscription_Hits_When_Property_Changes_On_Target()
    {
        var service = new BreakpointService();
        var target = new BreakpointTestControl();
        var entry = service.AddPropertyBreakpoint(BreakpointTestControl.ValueProperty, target, "target");

        target.Value = 42;

        Assert.Equal(1, entry.HitCount);
        Assert.Contains("New='42'", entry.LastHitDetails, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void PropertyBreakpoint_Remove_Detaches_Target_Subscription()
    {
        var service = new BreakpointService();
        var target = new BreakpointTestControl();
        var entry = service.AddPropertyBreakpoint(BreakpointTestControl.ValueProperty, target, "target");

        service.Remove(entry);
        target.Value = 7;

        Assert.Equal(0, entry.HitCount);
    }

    [AvaloniaFact]
    public void PropertyBreakpoint_Clear_Detaches_Target_Subscription()
    {
        var service = new BreakpointService();
        var target = new BreakpointTestControl();
        var entry = service.AddPropertyBreakpoint(BreakpointTestControl.ValueProperty, target, "target");

        service.Clear();
        target.Value = 9;

        Assert.Equal(0, entry.HitCount);
        Assert.Empty(service.Entries);
    }

    [AvaloniaFact]
    public void MainViewModel_Dispose_Clears_Breakpoints_And_Detaches_Target_Subscriptions()
    {
        var viewModel = new MainViewModel(new Window());
        var target = new BreakpointTestControl();
        var entry = viewModel.BreakpointService.AddPropertyBreakpoint(BreakpointTestControl.ValueProperty, target, "target");

        viewModel.Dispose();
        target.Value = 13;

        Assert.Equal(0, entry.HitCount);
        Assert.Empty(viewModel.BreakpointService.Entries);
    }

    [AvaloniaFact]
    public void EventsPageViewModel_AddSourceBreakpoint_Creates_Targeted_EventBreakpoint()
    {
        var service = new BreakpointService();
        var viewModel = new EventsPageViewModel(mainViewModel: null, breakpointService: service);
        var source = new BreakpointTestControl();
        var fired = CreateFiredEvent(BreakpointTestControl.BubbleEvent, source);

        viewModel.AddSourceEventBreakpoint(fired);

        var entry = Assert.Single(service.Entries);
        Assert.Equal(BreakpointKind.Event, entry.Kind);
        Assert.True(entry.MatchesTarget(source));
    }

    [AvaloniaFact]
    public void EventsPageViewModel_AddGlobalAndChainBreakpoints_Create_Expected_Targets()
    {
        var service = new BreakpointService();
        var viewModel = new EventsPageViewModel(mainViewModel: null, breakpointService: service);
        var source = new BreakpointTestControl();
        var fired = CreateFiredEvent(BreakpointTestControl.BubbleEvent, source);

        viewModel.AddGlobalEventBreakpoint(fired);
        viewModel.SelectedEvent = fired;
        viewModel.AddChainEventBreakpoint(new EventChainLink(source, handled: false, RoutingStrategies.Bubble));

        Assert.Equal(2, service.Entries.Count);
        Assert.Null(service.Entries[0].Target);
        Assert.True(service.Entries[1].MatchesTarget(source));
    }

    [AvaloniaFact]
    public void BreakpointsPageViewModel_Search_Selection_Remove_And_ClearSelectionOrFilter_Work()
    {
        var service = new BreakpointService();
        var target = new BreakpointTestControl();
        service.AddEventBreakpoint(BreakpointTestControl.BubbleEvent, target: null, targetDescription: "(global)");
        service.AddPropertyBreakpoint(BreakpointTestControl.ValueProperty, target, "target");

        using var page = new BreakpointsPageViewModel(service);
        page.BreakpointsFilter.FilterString = "target";

        Assert.True(page.SelectNextMatch());
        Assert.NotNull(page.SelectedBreakpoint);

        Assert.True(page.RemoveSelectedRecord());
        Assert.Null(page.SelectedBreakpoint);
        Assert.Single(service.Entries);

        Assert.True(page.ClearSelectionOrFilter());
        Assert.Equal(string.Empty, page.BreakpointsFilter.FilterString);
    }

    private static FiredEvent CreateFiredEvent(RoutedEvent routedEvent, BreakpointTestControl source)
    {
        var args = new RoutedEventArgs(routedEvent, source);
        var originator = new EventChainLink(source, handled: false, routedEvent.RoutingStrategies);
        return new FiredEvent(args, originator, DateTime.UtcNow);
    }

    private sealed class BreakpointTestControl : Control
    {
        public static readonly RoutedEvent<RoutedEventArgs> BubbleEvent =
            RoutedEvent.Register<BreakpointTestControl, RoutedEventArgs>(nameof(BubbleEvent), RoutingStrategies.Bubble);

        public static readonly StyledProperty<int> ValueProperty =
            AvaloniaProperty.Register<BreakpointTestControl, int>(nameof(Value));

        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }
}
