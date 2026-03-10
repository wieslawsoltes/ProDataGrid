using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.Services;

internal sealed class BreakpointService
{
    private readonly AvaloniaList<BreakpointEntry> _entries = new();
    private readonly Dictionary<BreakpointEntry, PropertyWatcher> _propertyWatchers = new();

    public AvaloniaList<BreakpointEntry> Entries => _entries;

    public BreakpointEntry AddEventBreakpoint(RoutedEvent routedEvent, AvaloniaObject? target, string targetDescription)
    {
        if (routedEvent == null)
        {
            throw new ArgumentNullException(nameof(routedEvent));
        }

        return AddEntry(new BreakpointEntry(
            BreakpointKind.Event,
            routedEvent.Name,
            routedEvent,
            property: null,
            target,
            targetDescription));
    }

    public BreakpointEntry AddPropertyBreakpoint(AvaloniaProperty property, AvaloniaObject? target, string targetDescription)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        var entry = new BreakpointEntry(
            BreakpointKind.Property,
            property.Name,
            routedEvent: null,
            property,
            target,
            targetDescription);
        AddEntry(entry);
        AttachPropertyWatcher(entry);
        return entry;
    }

    public void EvaluateEvent(RoutedEvent routedEvent, AvaloniaObject? sender, AvaloniaObject? source)
    {
        if (routedEvent == null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => EvaluateEvent(routedEvent, sender, source));
            return;
        }

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!entry.IsEnabled ||
                entry.Kind != BreakpointKind.Event ||
                !ReferenceEquals(entry.RoutedEvent, routedEvent))
            {
                continue;
            }

            var isMatch = entry.Target is not null
                ? entry.MatchesTarget(sender)
                : (source is null || ReferenceEquals(sender, source));

            if (!isMatch)
            {
                continue;
            }

            var isHit = OnMatched(entry, $"Event '{routedEvent.Name}' raised on {DescribeObject(source ?? sender)}.");
            if (isHit && entry.RemoveOnceHit)
            {
                RemoveAt(i);
            }
        }
    }

    public void EvaluateProperty(AvaloniaProperty property, AvaloniaObject? sender, object? oldValue, object? newValue)
    {
        if (property == null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => EvaluateProperty(property, sender, oldValue, newValue));
            return;
        }

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (!entry.IsEnabled ||
                entry.Kind != BreakpointKind.Property ||
                !ReferenceEquals(entry.Property, property) ||
                !entry.MatchesTarget(sender))
            {
                continue;
            }

            var details =
                $"Property '{property.Name}' changed on {DescribeObject(sender)}. " +
                $"Old='{FormatValue(oldValue)}', New='{FormatValue(newValue)}'.";
            var isHit = OnMatched(entry, details);
            if (isHit && entry.RemoveOnceHit)
            {
                RemoveAt(i);
            }
        }
    }

    public void Remove(BreakpointEntry? entry)
    {
        if (entry == null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Remove(entry));
            return;
        }

        DetachPropertyWatcher(entry);
        _entries.Remove(entry);
    }

    public void Clear()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Clear);
            return;
        }

        foreach (var entry in _entries)
        {
            DetachPropertyWatcher(entry);
        }

        _entries.Clear();
    }

    private BreakpointEntry AddEntry(BreakpointEntry entry)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => _entries.Add(entry));
            return entry;
        }

        _entries.Add(entry);
        return entry;
    }

    private void AttachPropertyWatcher(BreakpointEntry entry)
    {
        if (entry == null ||
            entry.Kind != BreakpointKind.Property ||
            entry.Property == null ||
            entry.Target == null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => AttachPropertyWatcher(entry));
            return;
        }

        if (!entry.Target.TryGetTarget(out var target) || _propertyWatchers.ContainsKey(entry))
        {
            return;
        }

        EventHandler<AvaloniaPropertyChangedEventArgs> handler = (_, e) =>
        {
            if (entry.Property != null && ReferenceEquals(e.Property, entry.Property))
            {
                EvaluateProperty(e.Property, target, e.OldValue, e.NewValue);
            }
        };

        target.PropertyChanged += handler;
        _propertyWatchers[entry] = new PropertyWatcher(target, handler);
    }

    private void DetachPropertyWatcher(BreakpointEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => DetachPropertyWatcher(entry));
            return;
        }

        if (_propertyWatchers.TryGetValue(entry, out var watcher))
        {
            watcher.Target.PropertyChanged -= watcher.Handler;
            _propertyWatchers.Remove(entry);
        }
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var entry = _entries[index];
        DetachPropertyWatcher(entry);
        _entries.RemoveAt(index);
    }

    private static bool OnMatched(BreakpointEntry entry, string details)
    {
        entry.HitCount++;
        if (entry.HitCount < entry.TriggerAfterHits)
        {
            return false;
        }

        entry.LastHitAt = DateTimeOffset.Now;
        entry.LastHitDetails = details;

        if (entry.LogMessage)
        {
            var logger = Logger.TryGet(LogEventLevel.Warning, "ProDiagnostics.Breakpoints");
            if (logger.HasValue)
            {
                logger.Value.Log(source: null, messageTemplate: details);
            }
        }

        if (entry.SuspendExecution && Debugger.IsAttached)
        {
            Debugger.Break();
        }

        return true;
    }

    private static string DescribeObject(object? value)
    {
        if (value is INamed named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return named.Name + " (" + value.GetType().Name + ")";
        }

        return value?.GetType().Name ?? "(null)";
    }

    private static string FormatValue(object? value)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue))
        {
            return "(unset)";
        }

        if (value == null)
        {
            return "null";
        }

        var text = value.ToString() ?? string.Empty;
        return text.Length <= 80 ? text : text.Substring(0, 80) + "...";
    }

    private sealed class PropertyWatcher
    {
        public PropertyWatcher(AvaloniaObject target, EventHandler<AvaloniaPropertyChangedEventArgs> handler)
        {
            Target = target;
            Handler = handler;
        }

        public AvaloniaObject Target { get; }

        public EventHandler<AvaloniaPropertyChangedEventArgs> Handler { get; }
    }
}
