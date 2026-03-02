using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Diagnostics.Models;
using Avalonia.Diagnostics.Services;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Avalonia.Diagnostics.ViewModels
{
    internal sealed class EventsPageViewModel : ViewModelBase, IDisposable
    {
        private static readonly RoutedEvent[] s_builtInDefaultEvents =
        {
            Button.ClickEvent,
            InputElement.KeyDownEvent,
            InputElement.KeyUpEvent,
            InputElement.TextInputEvent,
            InputElement.PointerReleasedEvent,
            InputElement.PointerPressedEvent
        };

        private readonly HashSet<RoutedEvent> _defaultEvents;
        private readonly MainViewModel? _mainViewModel;
        private readonly BreakpointService _breakpointService;
        private readonly DataGridCollectionView _recordedEventsView;
        private FiredEvent? _selectedEvent;
        private EventTreeNodeBase? _selectedNode;
        private bool _includeBubbleRoutes = true;
        private bool _includeTunnelRoutes = true;
        private bool _includeDirectRoutes = true;
        private bool _includeHandledEvents = true;
        private bool _includeUnhandledEvents = true;
        private int _maxRecordedEvents = 100;
        private bool _autoScrollToLatest = true;

        public EventsPageViewModel(MainViewModel? mainViewModel, BreakpointService? breakpointService = null)
        {
            _mainViewModel = mainViewModel;
            _defaultEvents = new HashSet<RoutedEvent>(s_builtInDefaultEvents);
            _breakpointService = breakpointService ?? mainViewModel?.BreakpointService ?? new BreakpointService();

            Nodes = RoutedEventRegistry.Instance.GetAllRegistered()
                .GroupBy(e => e.OwnerType)
                .OrderBy(e => e.Key.Name)
                .Select(g => new EventOwnerTreeNode(g.Key, g, this))
                .ToArray();

            EventsFilter = new FilterViewModel();
            EventsFilter.RefreshFilter += (s, e) => UpdateEventFilters();
            RecordedEvents.CollectionChanged += OnRecordedEventsCollectionChanged;
            _recordedEventsView = new DataGridCollectionView(RecordedEvents)
            {
                Filter = FilterRecordedEvent
            };

            EnableDefault();
        }

        public string Name => "Events";

        public EventTreeNodeBase[] Nodes { get; }

        public ObservableCollection<FiredEvent> RecordedEvents { get; } = new ObservableCollection<FiredEvent>();

        public DataGridCollectionView RecordedEventsView => _recordedEventsView;

        public bool IncludeBubbleRoutes
        {
            get => _includeBubbleRoutes;
            set
            {
                if (RaiseAndSetIfChanged(ref _includeBubbleRoutes, value))
                {
                    RefreshRecordedEvents();
                }
            }
        }

        public bool IncludeTunnelRoutes
        {
            get => _includeTunnelRoutes;
            set
            {
                if (RaiseAndSetIfChanged(ref _includeTunnelRoutes, value))
                {
                    RefreshRecordedEvents();
                }
            }
        }

        public bool IncludeDirectRoutes
        {
            get => _includeDirectRoutes;
            set
            {
                if (RaiseAndSetIfChanged(ref _includeDirectRoutes, value))
                {
                    RefreshRecordedEvents();
                }
            }
        }

        public bool IncludeHandledEvents
        {
            get => _includeHandledEvents;
            set
            {
                if (RaiseAndSetIfChanged(ref _includeHandledEvents, value))
                {
                    RefreshRecordedEvents();
                }
            }
        }

        public bool IncludeUnhandledEvents
        {
            get => _includeUnhandledEvents;
            set
            {
                if (RaiseAndSetIfChanged(ref _includeUnhandledEvents, value))
                {
                    RefreshRecordedEvents();
                }
            }
        }

        public int MaxRecordedEvents
        {
            get => _maxRecordedEvents;
            private set => RaiseAndSetIfChanged(ref _maxRecordedEvents, value > 0 ? value : 1);
        }

        public bool AutoScrollToLatest
        {
            get => _autoScrollToLatest;
            set => RaiseAndSetIfChanged(ref _autoScrollToLatest, value);
        }

        public int TotalRecordedEvents => RecordedEvents.Count;

        public int VisibleRecordedEvents => _recordedEventsView.Count;

        public FiredEvent? SelectedEvent
        {
            get => _selectedEvent;
            set => RaiseAndSetIfChanged(ref _selectedEvent, value);
        }

        public EventTreeNodeBase? SelectedNode
        {
            get => _selectedNode;
            set => RaiseAndSetIfChanged(ref _selectedNode, value);
        }

        public FilterViewModel EventsFilter { get; }

        public void Clear()
        {
            RecordedEvents.Clear();
            SelectedEvent = null;
            RefreshRecordedEvents();
        }

        public bool SelectNextMatch()
        {
            return NavigateSelection(forward: true);
        }

        public bool SelectPreviousMatch()
        {
            return NavigateSelection(forward: false);
        }

        public bool RemoveSelectedRecord()
        {
            if (SelectedEvent is not { } selected)
            {
                return false;
            }

            var removed = RecordedEvents.Remove(selected);
            if (removed)
            {
                SelectedEvent = null;
                RefreshRecordedEvents();
            }

            return removed;
        }

        public bool ClearSelectionOrFilter()
        {
            if (SelectedEvent is not null)
            {
                SelectedEvent = null;
                return true;
            }

            if (!string.IsNullOrEmpty(EventsFilter.FilterString))
            {
                EventsFilter.FilterString = string.Empty;
                return true;
            }

            return false;
        }

        public void DisableAll()
        {
            EvaluateNodeEnabled(_ => false);
        }

        public void EnableDefault()
        {
            EvaluateNodeEnabled(node => _defaultEvents.Contains(node.Event));
        }

        public void SetOptions(DevToolsOptions options)
        {
            _defaultEvents.Clear();
            if (options.DefaultRoutedEvents is { Count: > 0 })
            {
                foreach (var routedEvent in options.DefaultRoutedEvents)
                {
                    if (routedEvent != null)
                    {
                        _defaultEvents.Add(routedEvent);
                    }
                }
            }
            else
            {
                foreach (var routedEvent in s_builtInDefaultEvents)
                {
                    _defaultEvents.Add(routedEvent);
                }
            }

            MaxRecordedEvents = options.MaxRecordedEvents;
            AutoScrollToLatest = options.AutoScrollEvents;

            TrimRecordedEvents();
            EnableDefault();
            RefreshRecordedEvents();
        }

        public void AddRecordedEvent(FiredEvent firedEvent)
        {
            if (firedEvent == null)
            {
                throw new ArgumentNullException(nameof(firedEvent));
            }

            RecordedEvents.Add(firedEvent);
            TrimRecordedEvents();
            RefreshRecordedEvents();
        }

        public void RefreshRecordedEvents()
        {
            _recordedEventsView.Refresh();
            RaisePropertyChanged(nameof(TotalRecordedEvents));
            RaisePropertyChanged(nameof(VisibleRecordedEvents));
        }

        public void RequestTreeNavigateTo(EventChainLink navTarget)
        {
            if (navTarget.Handler is Control control)
            {
                _mainViewModel?.RequestTreeNavigateTo(control, true);
            }
        }

        public void SelectEventByType(RoutedEvent evt)
        {
            foreach (var node in Nodes)
            {
                var result = FindNode(node, evt);

                if (result != null && result.IsVisible)
                {
                    ExpandParents(result);
                    SelectedNode = result;

                    break;
                }
            }

            static EventTreeNodeBase? FindNode(EventTreeNodeBase node, RoutedEvent eventType)
            {
                if (node is EventTreeNode eventNode && eventNode.Event == eventType)
                {
                    return node;
                }

                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        var result = FindNode(child, eventType);

                        if (result != null)
                        {
                            return result;
                        }
                    }
                }

                return null;
            }
        }

        private void EvaluateNodeEnabled(Func<EventTreeNode, bool> eval)
        {
            void ProcessNode(EventTreeNodeBase node)
            {
                if (node is EventTreeNode eventNode)
                {
                    node.IsEnabled = eval(eventNode);
                }

                if (node.Children != null)
                {
                    foreach (var childNode in node.Children)
                    {
                        ProcessNode(childNode);
                    }
                }
            }

            foreach (var node in Nodes)
            {
                ProcessNode(node);
            }
        }

        private void UpdateEventFilters()
        {
            foreach (var node in Nodes)
            {
                FilterNode(node, false);
            }

            bool FilterNode(EventTreeNodeBase node, bool isParentVisible)
            {
                bool matchesFilter = EventsFilter.Filter(node.Text);
                bool hasVisibleChild = false;

                if (node.Children != null)
                {
                    foreach (var childNode in node.Children)
                    {
                        hasVisibleChild |= FilterNode(childNode, matchesFilter);
                    }
                }

                node.IsVisible = hasVisibleChild || matchesFilter || isParentVisible;

                return node.IsVisible;
            }
        }

        public MainViewModel? MainView => _mainViewModel;

        public void AddGlobalEventBreakpoint(object? parameter)
        {
            if (!TryGetEvent(parameter, out var routedEvent))
            {
                return;
            }

            _breakpointService.AddEventBreakpoint(routedEvent, target: null, targetDescription: "(global)");
            _mainViewModel?.ShowBreakpoints();
        }

        public void AddSourceEventBreakpoint(object? parameter)
        {
            if (parameter is not FiredEvent firedEvent)
            {
                return;
            }

            var source = firedEvent.Source;
            _breakpointService.AddEventBreakpoint(
                firedEvent.Event,
                source,
                source != null ? DescribeTarget(source) : "(source unavailable)");
            _mainViewModel?.ShowBreakpoints();
        }

        public void AddChainEventBreakpoint(object? parameter)
        {
            if (parameter is not EventChainLink chainLink || SelectedEvent?.Event is not { } routedEvent)
            {
                return;
            }

            if (chainLink.Handler is not AvaloniaObject target)
            {
                return;
            }

            _breakpointService.AddEventBreakpoint(routedEvent, target, DescribeTarget(target));
            _mainViewModel?.ShowBreakpoints();
        }

        public void Dispose()
        {
            RecordedEvents.CollectionChanged -= OnRecordedEventsCollectionChanged;

            foreach (var node in Nodes)
            {
                DisposeNode(node);
            }
        }

        private void TrimRecordedEvents()
        {
            while (RecordedEvents.Count > MaxRecordedEvents)
            {
                var removed = RecordedEvents[0];
                RecordedEvents.RemoveAt(0);
                if (ReferenceEquals(removed, _selectedEvent))
                {
                    SelectedEvent = null;
                }
            }
        }

        private bool FilterRecordedEvent(object obj)
        {
            if (obj is not FiredEvent firedEvent)
            {
                return true;
            }

            if (firedEvent.IsHandled && !IncludeHandledEvents)
            {
                return false;
            }

            if (!firedEvent.IsHandled && !IncludeUnhandledEvents)
            {
                return false;
            }

            return IsRouteEnabled(firedEvent.ObservedRoutes);
        }

        private bool IsRouteEnabled(RoutingStrategies route)
        {
            RoutingStrategies enabledRoutes = 0;
            if (IncludeBubbleRoutes)
            {
                enabledRoutes |= RoutingStrategies.Bubble;
            }

            if (IncludeTunnelRoutes)
            {
                enabledRoutes |= RoutingStrategies.Tunnel;
            }

            if (IncludeDirectRoutes)
            {
                enabledRoutes |= RoutingStrategies.Direct;
            }

            if (enabledRoutes == 0)
            {
                return false;
            }

            return (route & enabledRoutes) != 0;
        }

        private void OnRecordedEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(TotalRecordedEvents));
            RaisePropertyChanged(nameof(VisibleRecordedEvents));
        }

        private static void ExpandParents(EventTreeNodeBase? node)
        {
            var current = node;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent;
            }
        }

        private static void DisposeNode(EventTreeNodeBase node)
        {
            if (node is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    DisposeNode(child);
                }
            }
        }

        internal void EvaluateEventBreakpoints(RoutedEvent routedEvent, object? sender, object? source)
        {
            _breakpointService.EvaluateEvent(routedEvent, sender as AvaloniaObject, source as AvaloniaObject);
        }

        private static bool TryGetEvent(object? parameter, out RoutedEvent routedEvent)
        {
            switch (parameter)
            {
                case RoutedEvent evt:
                    routedEvent = evt;
                    return true;

                case FiredEvent firedEvent:
                    routedEvent = firedEvent.Event;
                    return true;

                default:
                    routedEvent = null!;
                    return false;
            }
        }

        private static string DescribeTarget(AvaloniaObject target)
        {
            if (target is INamed named && !string.IsNullOrWhiteSpace(named.Name))
            {
                return named.Name + " (" + target.GetType().Name + ")";
            }

            return target.GetType().Name;
        }

        private bool NavigateSelection(bool forward)
        {
            var visible = _recordedEventsView.Cast<FiredEvent>().ToArray();
            if (visible.Length == 0)
            {
                SelectedEvent = null;
                return false;
            }

            var currentIndex = Array.IndexOf(visible, SelectedEvent);
            int nextIndex;
            if (currentIndex < 0)
            {
                nextIndex = forward ? 0 : visible.Length - 1;
            }
            else if (forward)
            {
                nextIndex = (currentIndex + 1) % visible.Length;
            }
            else
            {
                nextIndex = currentIndex == 0 ? visible.Length - 1 : currentIndex - 1;
            }

            SelectedEvent = visible[nextIndex];
            return true;
        }
    }
}
