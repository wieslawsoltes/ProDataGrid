using System;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessDevToolsLogCollector : IDevToolsLogCollector
{
    public static InProcessDevToolsLogCollector Instance { get; } = new();

    private InProcessDevToolsLogCollector()
    {
    }

    public string CollectorName => "in-process";

    public IDisposable Subscribe(Action<DevToolsLogEvent> onLogEvent)
    {
        if (onLogEvent is null)
        {
            throw new ArgumentNullException(nameof(onLogEvent));
        }

        return LogCaptureService.Subscribe(capturedEvent => onLogEvent(new DevToolsLogEvent(
            capturedEvent.Timestamp,
            capturedEvent.Level,
            capturedEvent.Area,
            capturedEvent.Source,
            capturedEvent.Message)));
    }
}
