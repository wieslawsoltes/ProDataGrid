using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Represents one logical remote attach session negotiated via protocol hello.
/// </summary>
public sealed class RemoteAttachSession
{
    public RemoteAttachSession(
        Guid sessionId,
        Guid connectionId,
        string transportName,
        string? remoteEndpoint,
        int processId,
        string processName,
        string applicationName,
        string machineName,
        string runtimeVersion,
        string clientName,
        IReadOnlyList<string> requestedFeatures,
        DateTimeOffset connectedAtUtc)
    {
        SessionId = sessionId;
        ConnectionId = connectionId;
        TransportName = transportName;
        RemoteEndpoint = remoteEndpoint;
        ProcessId = processId;
        ProcessName = processName;
        ApplicationName = applicationName;
        MachineName = machineName;
        RuntimeVersion = runtimeVersion;
        ClientName = clientName;
        RequestedFeatures = requestedFeatures;
        ConnectedAtUtc = connectedAtUtc;
        LastActivityUtc = connectedAtUtc;
    }

    public Guid SessionId { get; }

    public Guid ConnectionId { get; private set; }

    public string TransportName { get; }

    public string? RemoteEndpoint { get; private set; }

    public int ProcessId { get; private set; }

    public string ProcessName { get; private set; }

    public string ApplicationName { get; private set; }

    public string MachineName { get; private set; }

    public string RuntimeVersion { get; private set; }

    public string ClientName { get; private set; }

    public IReadOnlyList<string> RequestedFeatures { get; private set; }

    public DateTimeOffset ConnectedAtUtc { get; }

    public DateTimeOffset LastActivityUtc { get; private set; }

    internal void Touch(DateTimeOffset timestampUtc)
    {
        if (timestampUtc > LastActivityUtc)
        {
            LastActivityUtc = timestampUtc;
        }
    }

    internal void UpdateFromHello(
        Guid connectionId,
        string? remoteEndpoint,
        RemoteHelloMessage hello,
        DateTimeOffset timestampUtc)
    {
        ConnectionId = connectionId;
        RemoteEndpoint = remoteEndpoint;
        ProcessId = hello.ProcessId;
        ProcessName = hello.ProcessName;
        ApplicationName = hello.ApplicationName;
        MachineName = hello.MachineName;
        RuntimeVersion = hello.RuntimeVersion;
        ClientName = hello.ClientName;
        RequestedFeatures = hello.RequestedFeatures;
        Touch(timestampUtc);
    }
}
