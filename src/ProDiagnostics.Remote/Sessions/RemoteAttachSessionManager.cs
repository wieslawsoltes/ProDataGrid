using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Thread-safe store for active remote attach sessions.
/// </summary>
public sealed class RemoteAttachSessionManager
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, RemoteAttachSession> _sessions = new();
    private readonly RemoteAttachSessionManagerOptions _options;

    public RemoteAttachSessionManager(RemoteAttachSessionManagerOptions options)
    {
        _options = RemoteAttachSessionManagerOptions.Normalize(options);
    }

    /// <summary>
    /// Gets normalized manager options.
    /// </summary>
    public RemoteAttachSessionManagerOptions Options => _options;

    /// <summary>
    /// Gets current session count.
    /// </summary>
    public int SessionCount
    {
        get
        {
            lock (_sync)
            {
                return _sessions.Count;
            }
        }
    }

    /// <summary>
    /// Gets a stable snapshot of active sessions.
    /// </summary>
    public IReadOnlyList<RemoteAttachSession> GetSnapshot()
    {
        lock (_sync)
        {
            return _sessions.Values
                .OrderBy(static session => session.ConnectedAtUtc)
                .ToArray();
        }
    }

    /// <summary>
    /// Tries to register or update a session from a hello handshake message.
    /// </summary>
    public bool TryRegisterFromHello(
        Guid connectionId,
        string transportName,
        string? remoteEndpoint,
        RemoteHelloMessage hello,
        DateTimeOffset timestampUtc,
        out RemoteAttachSession? session,
        out string? rejectionReason)
    {
        if (hello.SessionId == Guid.Empty)
        {
            session = null;
            rejectionReason = "Session identifier cannot be empty.";
            return false;
        }

        lock (_sync)
        {
            if (_sessions.TryGetValue(hello.SessionId, out var existing))
            {
                existing.UpdateFromHello(connectionId, remoteEndpoint, hello, timestampUtc);
                session = existing;
                rejectionReason = null;
                return true;
            }

            if (_sessions.Count >= _options.MaxSessions)
            {
                session = null;
                rejectionReason = "Session capacity reached.";
                return false;
            }

            var created = new RemoteAttachSession(
                sessionId: hello.SessionId,
                connectionId: connectionId,
                transportName: transportName,
                remoteEndpoint: remoteEndpoint,
                processId: hello.ProcessId,
                processName: hello.ProcessName,
                applicationName: hello.ApplicationName,
                machineName: hello.MachineName,
                runtimeVersion: hello.RuntimeVersion,
                clientName: hello.ClientName,
                requestedFeatures: hello.RequestedFeatures,
                connectedAtUtc: timestampUtc);

            _sessions.Add(created.SessionId, created);
            session = created;
            rejectionReason = null;
            return true;
        }
    }

    /// <summary>
    /// Updates the last-activity timestamp for a session.
    /// </summary>
    public bool TryTouch(Guid sessionId, DateTimeOffset timestampUtc)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return false;
            }

            session.Touch(timestampUtc);
            return true;
        }
    }

    /// <summary>
    /// Removes one session.
    /// </summary>
    public bool TryRemove(Guid sessionId, out RemoteAttachSession? session)
    {
        lock (_sync)
        {
            if (_sessions.Remove(sessionId, out var removed))
            {
                session = removed;
                return true;
            }
        }

        session = null;
        return false;
    }

    /// <summary>
    /// Evicts stale sessions based on configured timeout.
    /// </summary>
    public IReadOnlyList<RemoteAttachSession> EvictStaleSessions(DateTimeOffset timestampUtc)
    {
        lock (_sync)
        {
            if (_sessions.Count == 0)
            {
                return Array.Empty<RemoteAttachSession>();
            }

            var staleBefore = timestampUtc - _options.SessionTimeout;
            var staleSessions = _sessions.Values
                .Where(session => session.LastActivityUtc <= staleBefore)
                .ToArray();

            foreach (var stale in staleSessions)
            {
                _sessions.Remove(stale.SessionId);
            }

            return staleSessions;
        }
    }
}
