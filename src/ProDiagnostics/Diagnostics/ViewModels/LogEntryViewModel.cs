using System;
using Avalonia.Logging;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class LogEntryViewModel
{
    public LogEntryViewModel(DateTimeOffset timestamp, LogEventLevel level, string area, string source, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Area = area;
        Source = source;
        Message = message;
    }

    public DateTimeOffset Timestamp { get; }

    public LogEventLevel Level { get; }

    public string Area { get; }

    public string Source { get; }

    public string Message { get; }
}
