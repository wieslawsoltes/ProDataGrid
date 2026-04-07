using System;

namespace Avalonia.Diagnostics.Services;

internal interface ITransportExportService : IDisposable
{
    event EventHandler? StateChanged;

    string ProtocolName { get; }

    bool IsRunning { get; }

    string StatusText { get; }

    long SentPacketCount { get; }

    long FailedPacketCount { get; }

    TransportExportSettings CurrentSettings { get; }

    void Start(TransportExportSettings settings);

    void Stop();

    void ApplySettings(TransportExportSettings settings);
}
