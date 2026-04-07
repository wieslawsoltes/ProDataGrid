using System;
using Avalonia.Interactivity;

namespace Avalonia.Diagnostics.Models
{
    internal class EventChainLink
    {
        public EventChainLink(
            object? handler,
            bool handled,
            RoutingStrategies route,
            string? handlerNameOverride = null,
            string? remoteNodeId = null,
            string? remoteNodePath = null,
            string? remoteHandlerType = null)
        {
            Handler = handler;
            Handled = handled;
            Route = route;
            _handlerNameOverride = handlerNameOverride;
            RemoteNodeId = remoteNodeId;
            RemoteNodePath = remoteNodePath;
            RemoteHandlerType = remoteHandlerType;
        }

        private readonly string? _handlerNameOverride;

        public object? Handler { get; }

        public string? RemoteNodeId { get; }

        public string? RemoteNodePath { get; }

        public string? RemoteHandlerType { get; }

        public bool BeginsNewRoute { get; set; }

        public string HandlerName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_handlerNameOverride))
                {
                    return _handlerNameOverride;
                }

                if (Handler is string text && !string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (Handler is INamed named && !string.IsNullOrEmpty(named.Name))
                {
                    return named.Name + " (" + Handler.GetType().Name + ")";
                }

                return Handler?.GetType().Name ?? "(unknown)";
            }
        }

        public bool Handled { get; set; }

        public RoutingStrategies Route { get; }
    }
}
