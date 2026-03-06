using System;
using Avalonia.Interactivity;

namespace Avalonia.Diagnostics.Models
{
    internal class EventChainLink
    {
        public EventChainLink(object? handler, bool handled, RoutingStrategies route, string? handlerNameOverride = null)
        {
            Handler = handler;
            Handled = handled;
            Route = route;
            _handlerNameOverride = handlerNameOverride;
        }

        private readonly string? _handlerNameOverride;

        public object? Handler { get; }

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
