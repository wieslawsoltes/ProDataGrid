using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Avalonia.Logging;

namespace Avalonia.Diagnostics.Services;

internal static class LogCaptureService
{
    private static readonly object s_gate = new();
    private static readonly List<Action<LogCaptureEvent>> s_subscribers = new();
    private static BridgedLogSink? s_bridgedSink;
    private static ILogSink? s_previousSink;

    public static IDisposable Subscribe(Action<LogCaptureEvent> onEvent)
    {
        if (onEvent is null)
        {
            throw new ArgumentNullException(nameof(onEvent));
        }

        lock (s_gate)
        {
            EnsureSinkInstalled();
            s_subscribers.Add(onEvent);
        }

        return new Subscription(onEvent);
    }

    private static void EnsureSinkInstalled()
    {
        if (s_bridgedSink is not null && ReferenceEquals(Logger.Sink, s_bridgedSink))
        {
            return;
        }

        s_previousSink = Logger.Sink;
        s_bridgedSink = new BridgedLogSink(s_previousSink, Publish);
        Logger.Sink = s_bridgedSink;
    }

    private static void Publish(LogCaptureEvent capturedEvent)
    {
        Action<LogCaptureEvent>[] subscribers;
        lock (s_gate)
        {
            subscribers = s_subscribers.ToArray();
        }

        for (var i = 0; i < subscribers.Length; i++)
        {
            try
            {
                subscribers[i](capturedEvent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LogCaptureService subscriber failed: " + ex);
            }
        }
    }

    private static void Unsubscribe(Action<LogCaptureEvent> callback)
    {
        lock (s_gate)
        {
            s_subscribers.Remove(callback);
            if (s_subscribers.Count != 0)
            {
                return;
            }

            if (s_bridgedSink is not null && ReferenceEquals(Logger.Sink, s_bridgedSink))
            {
                Logger.Sink = s_previousSink;
            }

            s_bridgedSink = null;
            s_previousSink = null;
        }
    }

    internal readonly record struct LogCaptureEvent(
        DateTimeOffset Timestamp,
        LogEventLevel Level,
        string Area,
        string Source,
        string Message);

    private sealed class BridgedLogSink : ILogSink
    {
        private readonly ILogSink? _inner;
        private readonly Action<LogCaptureEvent> _publish;
        private readonly LogEventLevel _captureMinimumLevel = LogEventLevel.Information;

        public BridgedLogSink(ILogSink? inner, Action<LogCaptureEvent> publish)
        {
            _inner = inner;
            _publish = publish;
        }

        public bool IsEnabled(LogEventLevel level, string area)
        {
            return IsInnerEnabled(level, area) || level >= _captureMinimumLevel;
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            var innerEnabled = IsInnerEnabled(level, area);
            if (innerEnabled)
            {
                _inner!.Log(level, area, source, messageTemplate);
            }

            if (IsDevToolsLog(area, source))
            {
                return;
            }

            if (innerEnabled || level >= _captureMinimumLevel)
            {
                _publish(new LogCaptureEvent(
                    DateTimeOffset.Now,
                    level,
                    area,
                    FormatSource(source),
                    messageTemplate));
            }
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            var innerEnabled = IsInnerEnabled(level, area);
            if (innerEnabled)
            {
                _inner!.Log(level, area, source, messageTemplate, propertyValues);
            }

            if (IsDevToolsLog(area, source))
            {
                return;
            }

            if (innerEnabled || level >= _captureMinimumLevel)
            {
                _publish(new LogCaptureEvent(
                    DateTimeOffset.Now,
                    level,
                    area,
                    FormatSource(source),
                    FormatMessage(messageTemplate, propertyValues)));
            }
        }

        private bool IsInnerEnabled(LogEventLevel level, string area)
        {
            return _inner?.IsEnabled(level, area) == true;
        }

        private static string FormatSource(object? source)
        {
            return source?.GetType().Name ?? "(none)";
        }

        private static string FormatMessage(string messageTemplate, object?[] propertyValues)
        {
            if (propertyValues.Length == 0)
            {
                return messageTemplate;
            }

            var builder = new StringBuilder(messageTemplate.Length + propertyValues.Length * 8);
            var valueIndex = 0;

            for (var i = 0; i < messageTemplate.Length; i++)
            {
                var c = messageTemplate[i];
                if (c == '{')
                {
                    if (i + 1 < messageTemplate.Length && messageTemplate[i + 1] == '{')
                    {
                        builder.Append('{');
                        i++;
                        continue;
                    }

                    var endIndex = FindPlaceholderEnd(messageTemplate, i + 1);
                    if (endIndex >= 0)
                    {
                        if (valueIndex < propertyValues.Length)
                        {
                            builder.Append(FormatValue(propertyValues[valueIndex++]));
                        }
                        else
                        {
                            builder.Append(messageTemplate, i, endIndex - i + 1);
                        }

                        i = endIndex;
                        continue;
                    }
                }
                else if (c == '}' && i + 1 < messageTemplate.Length && messageTemplate[i + 1] == '}')
                {
                    builder.Append('}');
                    i++;
                    continue;
                }

                builder.Append(c);
            }

            if (valueIndex < propertyValues.Length)
            {
                builder.Append(" | ");
                for (var i = valueIndex; i < propertyValues.Length; i++)
                {
                    if (i > valueIndex)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(FormatValue(propertyValues[i]));
                }
            }

            return builder.ToString();
        }

        private static int FindPlaceholderEnd(string template, int startIndex)
        {
            for (var i = startIndex; i < template.Length; i++)
            {
                if (template[i] == '}')
                {
                    return i;
                }

                if (template[i] == '{')
                {
                    return -1;
                }
            }

            return -1;
        }

        private static string FormatValue(object? value)
        {
            return value?.ToString() ?? "null";
        }

        private static bool IsDevToolsLog(string area, object? source)
        {
            if (area.StartsWith("ProDiagnostics", StringComparison.Ordinal) ||
                area.StartsWith("Avalonia.Diagnostics", StringComparison.Ordinal) ||
                area.Equals("Diagnostics", StringComparison.Ordinal))
            {
                return true;
            }

            if (source is null)
            {
                return false;
            }

            if (area.Equals("Layout", StringComparison.Ordinal)
                && source.GetType().FullName == "Avalonia.Layout.LayoutManager"
                && IsDevToolsWindowOpen())
            {
                return true;
            }

            if (IsDevToolsType(source.GetType()))
            {
                return true;
            }

            if (source is Visual visual)
            {
                var root = visual.GetVisualRoot();
                if (root is not null && IsDevToolsType(root.GetType()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDevToolsType(Type type)
        {
            var fullName = type.FullName;
            if (fullName is null)
            {
                return false;
            }

            return fullName.StartsWith("Avalonia.Diagnostics.", StringComparison.Ordinal)
                   || fullName.StartsWith("ProDiagnostics.", StringComparison.Ordinal);
        }

        private static bool IsDevToolsWindowOpen()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return false;
            }

            var windows = desktop.Windows;
            for (var i = 0; i < windows.Count; i++)
            {
                if (IsDevToolsType(windows[i].GetType()))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Action<LogCaptureEvent>? _callback;

        public Subscription(Action<LogCaptureEvent> callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            var callback = _callback;
            if (callback is null)
            {
                return;
            }

            _callback = null;
            Unsubscribe(callback);
        }
    }
}
