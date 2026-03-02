using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class LogsPageViewModelTests
{
    [AvaloniaFact]
    public void Captures_Warning_Log_When_No_App_Sink_Is_Configured()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = null;

            using var viewModel = new LogsPageViewModel();
            var token = "warning-log-capture-" + Guid.NewGuid().ToString("N");
            Emit(LogEventLevel.Warning, token);

            Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(token, StringComparison.Ordinal));
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void Captures_Information_Log_When_No_App_Sink_Is_Configured()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = null;

            using var viewModel = new LogsPageViewModel();
            var token = "information-log-capture-" + Guid.NewGuid().ToString("N");
            Emit(LogEventLevel.Information, token);

            Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(token, StringComparison.Ordinal));
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void Level_Filter_Hides_Debug_When_Disabled()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = new TestLogSink(LogEventLevel.Verbose);

            using var viewModel = new LogsPageViewModel();
            var debugToken = "debug-log-filter-" + Guid.NewGuid().ToString("N");
            var warningToken = "warning-log-filter-" + Guid.NewGuid().ToString("N");

            Emit(LogEventLevel.Debug, debugToken);
            Emit(LogEventLevel.Warning, warningToken);

            viewModel.ShowDebug = true;
            Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(debugToken, StringComparison.Ordinal));
            Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(warningToken, StringComparison.Ordinal));

            viewModel.ShowDebug = false;

            Assert.DoesNotContain(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(debugToken, StringComparison.Ordinal));
            Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(warningToken, StringComparison.Ordinal));
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void Renders_Structured_Log_Message_With_Placeholder_Values()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = null;

            using var viewModel = new LogsPageViewModel();
            var token = "structured-log-" + Guid.NewGuid().ToString("N");
            Emit(LogEventLevel.Information, token + " measure {Measure} arrange {Arrange}", 12, 34);

            var entry = Assert.Single(
                viewModel.EntriesView.Cast<LogEntryViewModel>(),
                x => x.Message.Contains(token, StringComparison.Ordinal));
            Assert.Contains("12", entry.Message, StringComparison.Ordinal);
            Assert.Contains("34", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("{Measure}", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("{Arrange}", entry.Message, StringComparison.Ordinal);
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void Excludes_DevTools_Area_Logs()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = null;

            using var viewModel = new LogsPageViewModel();
            var token = "devtools-area-log-" + Guid.NewGuid().ToString("N");
            Emit(LogEventLevel.Information, "Avalonia.Diagnostics.Internal", token);

            Assert.DoesNotContain(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(token, StringComparison.Ordinal));
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void MaxEntries_Trims_Oldest_Items()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = new TestLogSink(LogEventLevel.Warning);

            using var viewModel = new LogsPageViewModel
            {
                MaxEntries = 2
            };
            viewModel.Clear();

            var token1 = "trim-test-1-" + Guid.NewGuid().ToString("N");
            var token2 = "trim-test-2-" + Guid.NewGuid().ToString("N");
            var token3 = "trim-test-3-" + Guid.NewGuid().ToString("N");

            Emit(LogEventLevel.Warning, token1);
            Emit(LogEventLevel.Warning, token2);
            Emit(LogEventLevel.Warning, token3);

            var entries = viewModel.EntriesView.Cast<LogEntryViewModel>().ToArray();
            Assert.Equal(2, entries.Count(x => x.Message.Contains("trim-test-", StringComparison.Ordinal)));
            Assert.DoesNotContain(entries, x => x.Message.Contains(token1, StringComparison.Ordinal));
            Assert.Contains(entries, x => x.Message.Contains(token2, StringComparison.Ordinal));
            Assert.Contains(entries, x => x.Message.Contains(token3, StringComparison.Ordinal));
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void LogCaptureService_Subscriber_Exception_Does_Not_Block_Other_Subscribers()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = new TestLogSink(LogEventLevel.Warning);

            var received = false;
            using var failing = LogCaptureService.Subscribe(_ => throw new InvalidOperationException("boom"));
            using var succeeding = LogCaptureService.Subscribe(_ => received = true);

            var logger = Logger.TryGet(LogEventLevel.Warning, "SampleApp.UnitTests.LogCaptureIsolation");
            Assert.True(logger.HasValue);

            var ex = Record.Exception(() => logger.Value.Log(source: null, messageTemplate: "capture-isolation"));
            Assert.Null(ex);
            Assert.True(received);
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void Search_Selection_Remove_And_ClearSelectionOrFilter_Work()
    {
        var previousSink = Logger.Sink;
        try
        {
            Logger.Sink = new TestLogSink(LogEventLevel.Warning);

            using var viewModel = new LogsPageViewModel();
            var token1 = "logs-select-1-" + Guid.NewGuid().ToString("N");
            var token2 = "logs-select-2-" + Guid.NewGuid().ToString("N");
            Emit(LogEventLevel.Warning, token1);
            Emit(LogEventLevel.Warning, token2);
            viewModel.LogsFilter.FilterString = "logs-select";

            Assert.True(viewModel.SelectNextMatch());
            Assert.NotNull(viewModel.SelectedEntry);
            Assert.True(viewModel.RemoveSelectedRecord());
            Assert.Null(viewModel.SelectedEntry);
            Assert.Equal(1, viewModel.EntriesView.Cast<LogEntryViewModel>().Count(x => x.Message.Contains("logs-select-", StringComparison.Ordinal)));

            Assert.True(viewModel.ClearSelectionOrFilter());
            Assert.Equal(string.Empty, viewModel.LogsFilter.FilterString);
        }
        finally
        {
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaFact]
    public void SetOptions_Uses_Custom_Collector()
    {
        using var viewModel = new LogsPageViewModel();
        var collector = new TestDevToolsLogCollector("remote-collector");
        viewModel.SetOptions(new DevToolsOptions
        {
            LogCollector = collector
        });

        var token = "custom-collector-" + Guid.NewGuid().ToString("N");
        collector.Publish(new DevToolsLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Warning,
            "Tests",
            "Collector",
            token));

        Assert.Equal("remote-collector", viewModel.CollectorName);
        Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(token, StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Constructor_Supports_Replay_On_Subscribe_Collector()
    {
        var token = "replay-subscribe-" + Guid.NewGuid().ToString("N");
        var collector = new ReplayOnSubscribeLogCollector("replay", token);

        using var viewModel = new LogsPageViewModel(collector);

        Assert.Equal("replay", viewModel.CollectorName);
        Assert.Contains(viewModel.EntriesView.Cast<LogEntryViewModel>(), x => x.Message.Contains(token, StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void SetOptions_Replaces_Previous_Collector_Subscription()
    {
        using var viewModel = new LogsPageViewModel();
        var firstCollector = new TestDevToolsLogCollector("first");
        var secondCollector = new TestDevToolsLogCollector("second");

        viewModel.SetOptions(new DevToolsOptions
        {
            LogCollector = firstCollector
        });
        viewModel.SetOptions(new DevToolsOptions
        {
            LogCollector = secondCollector
        });

        var firstToken = "collector-first-" + Guid.NewGuid().ToString("N");
        var secondToken = "collector-second-" + Guid.NewGuid().ToString("N");
        firstCollector.Publish(new DevToolsLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Warning,
            "Tests",
            "FirstCollector",
            firstToken));
        secondCollector.Publish(new DevToolsLogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Warning,
            "Tests",
            "SecondCollector",
            secondToken));

        var entries = viewModel.EntriesView.Cast<LogEntryViewModel>().ToArray();
        Assert.DoesNotContain(entries, x => x.Message.Contains(firstToken, StringComparison.Ordinal));
        Assert.Contains(entries, x => x.Message.Contains(secondToken, StringComparison.Ordinal));
        Assert.Equal("second", viewModel.CollectorName);
    }

    private static void Emit(LogEventLevel level, string message)
    {
        var logger = Logger.TryGet(level, "SampleApp.UnitTests.Logs");
        Assert.True(logger.HasValue);
        logger.Value.Log(source: null, messageTemplate: message);
    }

    private static void Emit(LogEventLevel level, string message, params object?[] propertyValues)
    {
        var sink = Logger.Sink;
        Assert.NotNull(sink);
        sink!.Log(level, "SampleApp.UnitTests.Logs", source: null, messageTemplate: message, propertyValues: propertyValues);
    }

    private static void Emit(LogEventLevel level, string area, string message)
    {
        var logger = Logger.TryGet(level, area);
        Assert.True(logger.HasValue);
        logger.Value.Log(source: null, messageTemplate: message);
    }

    private sealed class TestLogSink : ILogSink
    {
        private readonly LogEventLevel _minimumLevel;

        public TestLogSink(LogEventLevel minimumLevel)
        {
            _minimumLevel = minimumLevel;
        }

        public bool IsEnabled(LogEventLevel level, string area)
        {
            return level >= _minimumLevel;
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
        }
    }

    private sealed class TestDevToolsLogCollector : IDevToolsLogCollector
    {
        private readonly List<Action<DevToolsLogEvent>> _subscribers = new();

        public TestDevToolsLogCollector(string collectorName)
        {
            CollectorName = collectorName;
        }

        public string CollectorName { get; }

        public IDisposable Subscribe(Action<DevToolsLogEvent> onLogEvent)
        {
            _subscribers.Add(onLogEvent);
            return new Subscription(_subscribers, onLogEvent);
        }

        public void Publish(DevToolsLogEvent logEvent)
        {
            var subscribers = _subscribers.ToArray();
            for (var i = 0; i < subscribers.Length; i++)
            {
                subscribers[i](logEvent);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly List<Action<DevToolsLogEvent>> _subscribers;
            private Action<DevToolsLogEvent>? _callback;

            public Subscription(List<Action<DevToolsLogEvent>> subscribers, Action<DevToolsLogEvent> callback)
            {
                _subscribers = subscribers;
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
                _subscribers.Remove(callback);
            }
        }
    }

    private sealed class ReplayOnSubscribeLogCollector : IDevToolsLogCollector
    {
        private readonly string _message;

        public ReplayOnSubscribeLogCollector(string collectorName, string message)
        {
            CollectorName = collectorName;
            _message = message;
        }

        public string CollectorName { get; }

        public IDisposable Subscribe(Action<DevToolsLogEvent> onLogEvent)
        {
            onLogEvent(new DevToolsLogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Warning,
                "Tests",
                "ReplayCollector",
                _message));
            return EmptyDisposable.Instance;
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
