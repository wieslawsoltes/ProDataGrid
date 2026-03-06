using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Diagnostics.Models;
using Avalonia.Interactivity;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class FiredEvent : ViewModelBase
    {
        private readonly RoutedEventArgs? _eventArgs;
        private readonly RoutedEvent? _originalEvent;
        private readonly AvaloniaObject? _source;
        private readonly string _eventName;
        private readonly string? _eventOwnerType;
        private readonly string? _remoteSourceNodeId;
        private readonly string? _remoteSourceNodePath;
        private readonly RoutingStrategies? _observedRoutesOverride;
        private EventChainLink? _handledBy;

        public FiredEvent(RoutedEventArgs eventArgs, EventChainLink originator, DateTime triggerTime)
        {
            _eventArgs = eventArgs ?? throw new ArgumentNullException(nameof(eventArgs));
            Originator = originator ?? throw new ArgumentNullException(nameof(originator));
            _originalEvent = _eventArgs.RoutedEvent;
            _eventName = _originalEvent?.Name ?? string.Empty;
            _eventOwnerType = _originalEvent?.OwnerType.FullName ?? _originalEvent?.OwnerType.Name;
            _source = _eventArgs.Source as AvaloniaObject;
            AddToChain(originator);
            TriggerTime = triggerTime;
        }

        public FiredEvent(
            string? recordId,
            DateTime triggerTime,
            string eventName,
            string? eventOwnerType,
            string sourceDisplay,
            string originatorDisplay,
            string? handledByDisplay,
            RoutingStrategies observedRoutes,
            bool isHandled,
            string? sourceNodeId,
            string? sourceNodePath,
            IEnumerable<EventChainLink>? remoteEventChain = null)
        {
            RecordId = recordId;
            TriggerTime = triggerTime;
            _eventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
            _eventOwnerType = eventOwnerType;
            _remoteSourceNodeId = sourceNodeId;
            _remoteSourceNodePath = sourceNodePath;
            _observedRoutesOverride = observedRoutes;
            var chain = remoteEventChain?.ToArray();
            if (chain is { Length: > 0 })
            {
                Originator = chain[0];
                for (var i = 0; i < chain.Length; i++)
                {
                    AddToChain(chain[i]);
                }
            }
            else
            {
                Originator = new EventChainLink(handler: null, handled: false, observedRoutes, handlerNameOverride: originatorDisplay);
                AddToChain(Originator);
            }

            if (HandledBy is null && !string.IsNullOrWhiteSpace(handledByDisplay))
            {
                AddToChain(new EventChainLink(handler: null, handled: true, observedRoutes, handlerNameOverride: handledByDisplay));
            }
            else if (HandledBy is null && isHandled)
            {
                Originator.Handled = true;
                HandledBy = Originator;
            }

            SourceDisplay = sourceDisplay;
        }

        public bool IsPartOfSameEventChain(RoutedEventArgs e)
        {
            return _eventArgs is not null && e == _eventArgs && e.RoutedEvent == _originalEvent;
        }

        public string? RecordId { get; }

        public DateTime TriggerTime { get; }

        public RoutedEvent? Event => _originalEvent;

        public string EventName => _eventName;

        public string? EventOwnerType => _eventOwnerType;

        public AvaloniaObject? Source => _source;

        public string? RemoteSourceNodePath => _remoteSourceNodePath;

        public string? RemoteSourceNodeId => _remoteSourceNodeId;

        public string SourceDisplay { get; } = string.Empty;

        public RoutingStrategies ObservedRoutes
        {
            get
            {
                if (_observedRoutesOverride is { } routes)
                {
                    return routes;
                }

                RoutingStrategies accumulatedRoutes = 0;
                for (var i = 0; i < EventChain.Count; i++)
                {
                    accumulatedRoutes |= EventChain[i].Route;
                }

                return accumulatedRoutes;
            }
        }

        public bool IsHandled => HandledBy?.Handled == true;

        public ObservableCollection<EventChainLink> EventChain { get; } = new ObservableCollection<EventChainLink>();

        public string DisplayText
        {
            get
            {
                if (IsHandled)
                {
                    return $"{EventName} on {Originator.HandlerName};" + Environment.NewLine +
                           $"strategies: {ObservedRoutes}; handled by: {HandledBy!.HandlerName}";
                }

                return $"{EventName} on {Originator.HandlerName}; strategies: {ObservedRoutes}";
            }
        }

        public EventChainLink Originator { get; }

        public EventChainLink? HandledBy
        {
            get => _handledBy;
            set
            {
                if (_handledBy != value)
                {
                    _handledBy = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsHandled));
                    RaisePropertyChanged(nameof(DisplayText));
                }
            }
        }

        public void AddToChain(EventChainLink link)
        {
            if (EventChain.Count > 0)
            {
                var prevLink = EventChain[EventChain.Count - 1];

                if (prevLink.Route != link.Route)
                {
                    link.BeginsNewRoute = true;
                }
            }

            EventChain.Add(link);
            RaisePropertyChanged(nameof(ObservedRoutes));
            RaisePropertyChanged(nameof(DisplayText));

            if (HandledBy == null && link.Handled)
            {
                HandledBy = link;
            }
        }
    }
}
