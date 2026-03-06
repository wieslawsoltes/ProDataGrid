using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Models;
using Avalonia.Diagnostics.Services;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
        private readonly EventTreeNodeBase[] _localNodes;
        private FiredEvent? _selectedEvent;
        private EventTreeNodeBase? _selectedNode;
        private bool _includeBubbleRoutes = true;
        private bool _includeTunnelRoutes = true;
        private bool _includeDirectRoutes = true;
        private bool _includeHandledEvents = true;
        private bool _includeUnhandledEvents = true;
        private int _maxRecordedEvents = 100;
        private bool _autoScrollToLatest = true;
        private IRemoteReadOnlyDiagnosticsDomainService? _remoteReadOnly;
        private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
        private Func<AvaloniaObject?, (string Scope, string? NodePath, string? ControlName)>? _remoteTargetContextAccessor;
        private bool _isApplyingRemoteMutations;
        private bool _isRemoteRefreshScheduled;

        public EventsPageViewModel(MainViewModel? mainViewModel, BreakpointService? breakpointService = null)
        {
            _mainViewModel = mainViewModel;
            _defaultEvents = new HashSet<RoutedEvent>(s_builtInDefaultEvents);
            _breakpointService = breakpointService ?? mainViewModel?.BreakpointService ?? new BreakpointService();

            _localNodes = RoutedEventRegistry.Instance.GetAllRegistered()
                .GroupBy(e => e.OwnerType)
                .OrderBy(e => e.Key.Name)
                .Select(g => new EventOwnerTreeNode(g.Key, g, this))
                .ToArray();
            Nodes = _localNodes;

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

        public EventTreeNodeBase[] Nodes { get; private set; }

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
            if (_remoteMutation is null)
            {
                ClearRecordedEventsLocal();
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.ClearEventsAsync(),
                onSuccess: ClearRecordedEventsLocal,
                onFailure: ClearRecordedEventsLocal);
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
            if (_remoteMutation is null)
            {
                DisableAllLocal();
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.DisableAllEventsAsync(),
                onSuccess: DisableAllLocal,
                onFailure: DisableAllLocal);
        }

        public void EnableDefault()
        {
            if (_remoteMutation is null)
            {
                EnableDefaultLocal();
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.EnableDefaultEventsAsync(),
                onSuccess: EnableDefaultLocal,
                onFailure: EnableDefaultLocal);
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
            if (_remoteReadOnly is not null)
            {
                RefreshRecordedEventsLocal();
            }
            else
            {
                RefreshRecordedEvents();
            }
        }

        public void RefreshRecordedEvents()
        {
            if (_remoteReadOnly is not null)
            {
                QueueRemoteRefresh();
                return;
            }

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

        public void SelectEventByRecord(FiredEvent firedEvent)
        {
            if (firedEvent is null)
            {
                return;
            }

            SelectedEvent = firedEvent;
            if (firedEvent.Event is { } routedEvent)
            {
                SelectEventByType(routedEvent);
            }

            if (firedEvent.Source is Control sourceControl)
            {
                _mainViewModel?.RequestTreeNavigateTo(sourceControl, isVisualTree: true);
                return;
            }

            if (!string.IsNullOrWhiteSpace(firedEvent.RemoteSourceNodePath))
            {
                _mainViewModel?.RequestTreeNavigateTo(firedEvent.RemoteSourceNodePath);
            }
        }

        private void EvaluateNodeEnabled(Func<EventTreeNode, bool> evalLocal, Func<RemoteEventTreeNode, bool>? evalRemote = null)
        {
            void ProcessNode(EventTreeNodeBase node)
            {
                if (node is EventTreeNode eventNode)
                {
                    node.IsEnabled = evalLocal(eventNode);
                }
                else if (node is RemoteEventTreeNode remoteEventNode && evalRemote is not null)
                {
                    node.IsEnabled = evalRemote(remoteEventNode);
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

        internal void SetRemoteReadOnlySource(IRemoteReadOnlyDiagnosticsDomainService? readOnly, bool refreshNow = true)
        {
            _remoteReadOnly = readOnly;
            _isRemoteRefreshScheduled = false;

            if (readOnly is null)
            {
                ReplaceNodes(_localNodes);
                EnableDefaultLocal();
                RefreshRecordedEventsLocal();
                return;
            }

            DisableAllEventNodes(_localNodes);
            if (refreshNow)
            {
                QueueRemoteRefresh();
            }
        }

        internal void SetRemoteMutationSource(
            IRemoteMutationDiagnosticsDomainService? mutation,
            Func<AvaloniaObject?, (string Scope, string? NodePath, string? ControlName)>? contextAccessor)
        {
            _remoteMutation = mutation;
            _remoteTargetContextAccessor = contextAccessor;
        }

        public void AddGlobalEventBreakpoint(object? parameter)
        {
            if (!TryGetEvent(parameter, out var routedEvent))
            {
                return;
            }

            if (_remoteMutation is not null)
            {
                _ = InvokeRemoteMutationAsync(
                    mutation => mutation.AddEventBreakpointAsync(
                        new RemoteAddEventBreakpointRequest
                        {
                            Scope = "combined",
                            EventName = routedEvent.Name,
                            EventOwnerType = routedEvent.OwnerType.FullName ?? routedEvent.OwnerType.Name,
                            IsGlobal = true,
                        }),
                    fallback: () => _breakpointService.AddEventBreakpoint(routedEvent, target: null, targetDescription: "(global)"));
                _mainViewModel?.ShowBreakpoints();
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
            if (_remoteMutation is not null)
            {
                var context = ResolveRemoteTargetContext(firedEvent, source);
                var eventName = firedEvent.Event?.Name ?? firedEvent.EventName;
                var eventOwnerType = firedEvent.Event?.OwnerType.FullName ?? firedEvent.Event?.OwnerType.Name ?? firedEvent.EventOwnerType;
                if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(eventOwnerType))
                {
                    return;
                }

                _ = InvokeRemoteMutationAsync(
                    mutation => mutation.AddEventBreakpointAsync(
                        new RemoteAddEventBreakpointRequest
                        {
                            Scope = context.Scope,
                            NodePath = context.NodePath,
                            ControlName = context.ControlName,
                            EventName = eventName,
                            EventOwnerType = eventOwnerType,
                            IsGlobal = false,
                        }),
                    fallback: () =>
                    {
                        if (firedEvent.Event is { } localEvent)
                        {
                            _breakpointService.AddEventBreakpoint(
                                localEvent,
                                source,
                                source != null ? DescribeTarget(source) : "(source unavailable)");
                        }
                    });
                _mainViewModel?.ShowBreakpoints();
                return;
            }

            if (firedEvent.Event is not { } routedEvent)
            {
                return;
            }

            _breakpointService.AddEventBreakpoint(
                routedEvent,
                source,
                source != null ? DescribeTarget(source) : "(source unavailable)");
            _mainViewModel?.ShowBreakpoints();
        }

        public void AddChainEventBreakpoint(object? parameter)
        {
            if (parameter is not EventChainLink chainLink || SelectedEvent is not { } selectedEvent)
            {
                return;
            }

            if (chainLink.Handler is not AvaloniaObject target)
            {
                return;
            }

            var routedEvent = selectedEvent.Event;
            var eventName = routedEvent?.Name ?? selectedEvent.EventName;
            var eventOwnerType = routedEvent?.OwnerType.FullName ?? routedEvent?.OwnerType.Name ?? selectedEvent.EventOwnerType;
            if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(eventOwnerType))
            {
                return;
            }

            if (_remoteMutation is not null)
            {
                var context = ResolveRemoteTargetContext(target);
                _ = InvokeRemoteMutationAsync(
                    mutation => mutation.AddEventBreakpointAsync(
                        new RemoteAddEventBreakpointRequest
                        {
                            Scope = context.Scope,
                            NodePath = context.NodePath,
                            ControlName = context.ControlName,
                            EventName = eventName,
                            EventOwnerType = eventOwnerType,
                            IsGlobal = false,
                        }),
                    fallback: () =>
                    {
                        if (routedEvent is not null)
                        {
                            _breakpointService.AddEventBreakpoint(routedEvent, target, DescribeTarget(target));
                        }
                    });
                _mainViewModel?.ShowBreakpoints();
                return;
            }

            if (routedEvent is null)
            {
                return;
            }

            _breakpointService.AddEventBreakpoint(routedEvent, target, DescribeTarget(target));
            _mainViewModel?.ShowBreakpoints();
        }

        internal void NotifyEventNodeIsEnabledChanged(EventTreeNode eventNode, bool isEnabled)
        {
            NotifyEventNodeIsEnabledChanged(
                eventNode.Event.Name,
                eventNode.Event.OwnerType.FullName ?? eventNode.Event.OwnerType.Name,
                isEnabled);
        }

        internal void NotifyEventNodeIsEnabledChanged(string eventName, string? eventOwnerType, bool isEnabled)
        {
            if (_remoteMutation is null || _isApplyingRemoteMutations)
            {
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.SetEventEnabledAsync(
                    new RemoteSetEventEnabledRequest
                    {
                        EventId = string.Empty,
                        EventName = eventName,
                        EventOwnerType = eventOwnerType ?? string.Empty,
                        IsEnabled = isEnabled,
                    }),
                fallback: () => { });
        }

        internal void SetEventEnabledLocal(EventTreeNode eventNode, bool isEnabled)
        {
            RunWithoutRemoteMutations(() => eventNode.IsEnabled = isEnabled);
        }

        internal void SetEventEnabledLocal(string eventName, string? eventOwnerType, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            RunWithoutRemoteMutations(() =>
            {
                foreach (var node in Nodes)
                {
                    if (FindMatchingNode(node, eventName, eventOwnerType) is RemoteEventTreeNode remoteNode)
                    {
                        remoteNode.IsEnabled = isEnabled;
                        break;
                    }
                }
            });
        }

        internal void EnableDefaultLocal()
        {
            RunWithoutRemoteMutations(() => EvaluateNodeEnabled(
                node => _defaultEvents.Contains(node.Event),
                remoteNode => _defaultEvents.Any(
                    routedEvent =>
                        string.Equals(routedEvent.Name, remoteNode.EventName, StringComparison.Ordinal) &&
                        string.Equals(
                            routedEvent.OwnerType.FullName ?? routedEvent.OwnerType.Name,
                            remoteNode.EventOwnerType,
                            StringComparison.Ordinal))));
        }

        internal void DisableAllLocal()
        {
            RunWithoutRemoteMutations(() => EvaluateNodeEnabled(_ => false, _ => false));
        }

        internal void ClearRecordedEventsLocal()
        {
            RecordedEvents.Clear();
            SelectedEvent = null;
            RefreshRecordedEventsLocal();
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

        private void ReplaceNodes(EventTreeNodeBase[] nodes)
        {
            if (ReferenceEquals(Nodes, nodes))
            {
                return;
            }

            if (!ReferenceEquals(Nodes, _localNodes))
            {
                foreach (var node in Nodes)
                {
                    DisposeNode(node);
                }
            }

            Nodes = nodes;
            SelectedNode = null;
            RaisePropertyChanged(nameof(Nodes));
            UpdateEventFilters();
        }

        private void DisableAllEventNodes(IEnumerable<EventTreeNodeBase> nodes)
        {
            RunWithoutRemoteMutations(() =>
            {
                foreach (var node in nodes)
                {
                    DisableNode(node);
                }
            });

            static void DisableNode(EventTreeNodeBase node)
            {
                if (node.IsEnabled != null)
                {
                    node.IsEnabled = false;
                }

                if (node.Children is null)
                {
                    return;
                }

                foreach (var child in node.Children)
                {
                    DisableNode(child);
                }
            }
        }

        private void RefreshRecordedEventsLocal()
        {
            _recordedEventsView.Refresh();
            RaisePropertyChanged(nameof(TotalRecordedEvents));
            RaisePropertyChanged(nameof(VisibleRecordedEvents));
        }

        internal void ApplyRemoteStreamPayload(RemoteEventStreamPayload payload)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplyRemoteStreamPayloadCore(payload);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ApplyRemoteStreamPayloadCore(payload), DispatcherPriority.Background);
            }
        }

        private void ApplyRemoteStreamPayloadCore(RemoteEventStreamPayload payload)
        {
            if (_remoteReadOnly is null)
            {
                return;
            }

            var observedRoutes = ParseRoutingStrategies(payload.ObservedRoutes);
            var firedEvent = new FiredEvent(
                recordId: null,
                triggerTime: payload.TimestampUtc.LocalDateTime,
                eventName: payload.EventName,
                eventOwnerType: null,
                sourceDisplay: payload.Source,
                originatorDisplay: payload.Originator,
                handledByDisplay: payload.HandledBy,
                observedRoutes,
                isHandled: payload.IsHandled,
                sourceNodePath: null);
            AddRecordedEvent(firedEvent);
        }

        private void QueueRemoteRefresh()
        {
            if (_remoteReadOnly is null || _isRemoteRefreshScheduled)
            {
                return;
            }

            _isRemoteRefreshScheduled = true;
            _ = RefreshRemoteSnapshotAsync();
        }

        private async Task RefreshRemoteSnapshotAsync()
        {
            var readOnly = _remoteReadOnly;
            if (readOnly is null)
            {
                _isRemoteRefreshScheduled = false;
                return;
            }

            try
            {
                var snapshot = await readOnly.GetEventsSnapshotAsync(
                    new RemoteEventsSnapshotRequest
                    {
                        Scope = "combined",
                        IncludeRecordedEvents = true,
                    }).ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!ReferenceEquals(_remoteReadOnly, readOnly))
                    {
                        return;
                    }

                    ApplyRemoteSnapshot(snapshot);
                });
            }
            catch
            {
                // Preserve the last applied remote events snapshot when refresh fails.
            }
            finally
            {
                _isRemoteRefreshScheduled = false;
            }
        }

        private void ApplyRemoteSnapshot(RemoteEventsSnapshot snapshot)
        {
            var previousSelectedRecordId = SelectedEvent?.RecordId;
            var previousSelectedEventName = SelectedEvent?.EventName;
            var previousSelectedTriggerTime = SelectedEvent?.TriggerTime;

            var wasApplying = _isApplyingRemoteMutations;
            _isApplyingRemoteMutations = true;
            try
            {
                IncludeBubbleRoutes = snapshot.IncludeBubbleRoutes;
                IncludeTunnelRoutes = snapshot.IncludeTunnelRoutes;
                IncludeDirectRoutes = snapshot.IncludeDirectRoutes;
                IncludeHandledEvents = snapshot.IncludeHandledEvents;
                IncludeUnhandledEvents = snapshot.IncludeUnhandledEvents;
                MaxRecordedEvents = snapshot.MaxRecordedEvents > 0 ? snapshot.MaxRecordedEvents : MaxRecordedEvents;
                AutoScrollToLatest = snapshot.AutoScrollToLatest;
            }
            finally
            {
                _isApplyingRemoteMutations = wasApplying;
            }

            ReplaceNodes(BuildRemoteNodes(snapshot, this));
            RecordedEvents.Clear();

            FiredEvent? selectedEvent = null;
            for (var i = 0; i < snapshot.RecordedEvents.Count; i++)
            {
                var record = snapshot.RecordedEvents[i];
                var firedEvent = new FiredEvent(
                    record.Id,
                    record.TriggerTime.LocalDateTime,
                    record.EventName,
                    eventOwnerType: FindEventOwnerType(Nodes, record.EventName),
                    sourceDisplay: record.Source,
                    originatorDisplay: record.Originator,
                    handledByDisplay: record.HandledBy,
                    observedRoutes: ParseRoutingStrategies(record.ObservedRoutes),
                    isHandled: record.IsHandled,
                    sourceNodePath: record.SourceNodePath);
                RecordedEvents.Add(firedEvent);

                if ((previousSelectedRecordId is not null && string.Equals(previousSelectedRecordId, record.Id, StringComparison.Ordinal)) ||
                    (previousSelectedRecordId is null &&
                     previousSelectedTriggerTime.HasValue &&
                     previousSelectedTriggerTime.Value == firedEvent.TriggerTime &&
                     string.Equals(previousSelectedEventName, firedEvent.EventName, StringComparison.Ordinal)))
                {
                    selectedEvent = firedEvent;
                }
            }

            SelectedEvent = selectedEvent;
            RefreshRecordedEventsLocal();
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

                case FiredEvent { Event: not null } firedEvent:
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

        private (string Scope, string? NodePath, string? ControlName) ResolveRemoteTargetContext(AvaloniaObject? target)
        {
            if (_remoteTargetContextAccessor is not null)
            {
                return _remoteTargetContextAccessor(target);
            }

            if (_mainViewModel is not null)
            {
                return _mainViewModel.GetRemoteTargetContext(target);
            }

            return ("combined", null, (target as INamed)?.Name);
        }

        private (string Scope, string? NodePath, string? ControlName) ResolveRemoteTargetContext(FiredEvent firedEvent, AvaloniaObject? target)
        {
            if (!string.IsNullOrWhiteSpace(firedEvent.RemoteSourceNodePath))
            {
                return ("combined", firedEvent.RemoteSourceNodePath, (target as INamed)?.Name);
            }

            return ResolveRemoteTargetContext(target);
        }

        private async Task InvokeRemoteMutationAsync(
            Func<IRemoteMutationDiagnosticsDomainService, ValueTask<RemoteMutationResult>> action,
            Action? onSuccess,
            Action? onFailure)
        {
            var mutation = _remoteMutation;
            if (mutation is null)
            {
                onFailure?.Invoke();
                return;
            }

            try
            {
                await action(mutation).ConfigureAwait(false);
                onSuccess?.Invoke();
            }
            catch
            {
                onFailure?.Invoke();
            }
        }

        private Task InvokeRemoteMutationAsync(
            Func<IRemoteMutationDiagnosticsDomainService, ValueTask<RemoteMutationResult>> action,
            Action fallback)
        {
            return InvokeRemoteMutationAsync(action, onSuccess: null, onFailure: fallback);
        }

        private void RunWithoutRemoteMutations(Action action)
        {
            if (action is null)
            {
                return;
            }

            var wasApplying = _isApplyingRemoteMutations;
            _isApplyingRemoteMutations = true;
            try
            {
                action();
            }
            finally
            {
                _isApplyingRemoteMutations = wasApplying;
            }
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

        private static EventTreeNodeBase[] BuildRemoteNodes(RemoteEventsSnapshot snapshot, EventsPageViewModel owner)
        {
            if (snapshot.Nodes.Count == 0)
            {
                return Array.Empty<EventTreeNodeBase>();
            }

            var childrenByParentId = new Dictionary<string, List<RemoteEventNodeSnapshot>>(StringComparer.Ordinal);
            var roots = new List<EventTreeNodeBase>();

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                if (string.IsNullOrWhiteSpace(node.ParentId))
                {
                    continue;
                }

                if (!childrenByParentId.TryGetValue(node.ParentId, out var children))
                {
                    children = new List<RemoteEventNodeSnapshot>();
                    childrenByParentId[node.ParentId] = children;
                }

                children.Add(node);
            }

            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var root = snapshot.Nodes[i];
                if (!string.IsNullOrWhiteSpace(root.ParentId))
                {
                    continue;
                }

                roots.Add(BuildRemoteNode(root, parent: null, owner, childrenByParentId));
            }

            return roots.ToArray();
        }

        private static EventTreeNodeBase BuildRemoteNode(
            RemoteEventNodeSnapshot snapshot,
            EventTreeNodeBase? parent,
            EventsPageViewModel owner,
            IReadOnlyDictionary<string, List<RemoteEventNodeSnapshot>> childrenByParentId)
        {
            EventTreeNodeBase node = string.Equals(snapshot.NodeKind, "event", StringComparison.Ordinal)
                ? new RemoteEventTreeNode(parent, owner, snapshot)
                : new RemoteEventOwnerTreeNode(parent, snapshot);

            if (childrenByParentId.TryGetValue(snapshot.Id, out var children) && children.Count > 0)
            {
                var childNodes = new AvaloniaList<EventTreeNodeBase>(children.Count);
                for (var i = 0; i < children.Count; i++)
                {
                    childNodes.Add(BuildRemoteNode(children[i], node, owner, childrenByParentId));
                }

                if (node is RemoteEventOwnerTreeNode ownerNode)
                {
                    ownerNode.SetChildren(childNodes);
                }
            }

            return node;
        }

        private static string? FindEventOwnerType(IEnumerable<EventTreeNodeBase> nodes, string eventName)
        {
            foreach (var node in nodes)
            {
                if (FindMatchingNode(node, eventName, eventOwnerType: null) is { } matched)
                {
                    return matched.EventOwnerType;
                }
            }

            return null;
        }

        private static RemoteEventTreeNode? FindMatchingNode(EventTreeNodeBase node, string eventName, string? eventOwnerType)
        {
            if (node is RemoteEventTreeNode remoteNode &&
                string.Equals(remoteNode.EventName, eventName, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(eventOwnerType) ||
                 string.Equals(remoteNode.EventOwnerType, eventOwnerType, StringComparison.Ordinal)))
            {
                return remoteNode;
            }

            if (node.Children is null)
            {
                return null;
            }

            foreach (var child in node.Children)
            {
                if (FindMatchingNode(child, eventName, eventOwnerType) is { } match)
                {
                    return match;
                }
            }

            return null;
        }

        private static RoutingStrategies ParseRoutingStrategies(string? text)
        {
            return Enum.TryParse(text, ignoreCase: true, out RoutingStrategies routes)
                ? routes
                : RoutingStrategies.Direct;
        }

        private sealed class RemoteEventOwnerTreeNode : EventTreeNodeBase
        {
            public RemoteEventOwnerTreeNode(EventTreeNodeBase? parent, RemoteEventNodeSnapshot snapshot)
                : base(parent, snapshot.Text)
            {
                IsExpanded = snapshot.IsExpanded;
                IsVisible = snapshot.IsVisible;
                base.IsEnabled = snapshot.IsEnabled;
            }

            public void SetChildren(IAvaloniaReadOnlyList<EventTreeNodeBase> children)
            {
                Children = children;
                UpdateChecked();
            }

            public override bool? IsEnabled
            {
                get => base.IsEnabled;
                set
                {
                    if (base.IsEnabled == value)
                    {
                        return;
                    }

                    base.IsEnabled = value;
                    if (!_updateChildren || value is null || Children is null)
                    {
                        return;
                    }

                    foreach (var child in Children)
                    {
                        try
                        {
                            child._updateParent = false;
                            child.IsEnabled = value;
                        }
                        finally
                        {
                            child._updateParent = true;
                        }
                    }
                }
            }
        }

        private sealed class RemoteEventTreeNode : EventTreeNodeBase
        {
            private readonly EventsPageViewModel _owner;

            public RemoteEventTreeNode(EventTreeNodeBase? parent, EventsPageViewModel owner, RemoteEventNodeSnapshot snapshot)
                : base(parent, snapshot.Text)
            {
                _owner = owner;
                EventOwnerType = snapshot.OwnerType;
                EventName = snapshot.EventName ?? string.Empty;
                IsVisible = snapshot.IsVisible;
                IsExpanded = snapshot.IsExpanded;
                base.IsEnabled = snapshot.IsEnabled;
            }

            public string? EventOwnerType { get; }

            public string EventName { get; }

            public override bool? IsEnabled
            {
                get => base.IsEnabled;
                set
                {
                    if (base.IsEnabled == value)
                    {
                        return;
                    }

                    base.IsEnabled = value;
                    if (value is bool isEnabled)
                    {
                        _owner.NotifyEventNodeIsEnabledChanged(EventName, EventOwnerType, isEnabled);
                    }

                    if (Parent is not null && _updateParent)
                    {
                        try
                        {
                            Parent._updateChildren = false;
                            Parent.UpdateChecked();
                        }
                        finally
                        {
                            Parent._updateChildren = true;
                        }
                    }
                }
            }
        }
    }
}
