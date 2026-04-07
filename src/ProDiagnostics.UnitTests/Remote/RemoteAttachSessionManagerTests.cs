using System;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteAttachSessionManagerTests
{
    [Fact]
    public void TryRegisterFromHello_Updates_Existing_Session_Without_Growing_Store()
    {
        var manager = new RemoteAttachSessionManager(
            new RemoteAttachSessionManagerOptions(
                SessionTimeout: TimeSpan.FromSeconds(30),
                MaxSessions: 4));

        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var firstHello = CreateHello(sessionId, processId: 100, processName: "first");
        var secondHello = CreateHello(sessionId, processId: 200, processName: "second");

        var firstRegistered = manager.TryRegisterFromHello(
            connectionId: Guid.NewGuid(),
            transportName: "http",
            remoteEndpoint: "127.0.0.1:29414",
            hello: firstHello,
            timestampUtc: now,
            out var firstSession,
            out var firstRejection);

        var secondRegistered = manager.TryRegisterFromHello(
            connectionId: Guid.NewGuid(),
            transportName: "http",
            remoteEndpoint: "127.0.0.1:29414",
            hello: secondHello,
            timestampUtc: now.AddSeconds(1),
            out var secondSession,
            out var secondRejection);

        Assert.True(firstRegistered);
        Assert.True(secondRegistered);
        Assert.Null(firstRejection);
        Assert.Null(secondRejection);
        Assert.NotNull(firstSession);
        Assert.NotNull(secondSession);
        Assert.Equal(1, manager.SessionCount);
        Assert.Equal(200, secondSession.ProcessId);
        Assert.Equal("second", secondSession.ProcessName);
    }

    [Fact]
    public void TryRegisterFromHello_Rejects_When_MaxSessions_Is_Reached()
    {
        var manager = new RemoteAttachSessionManager(
            new RemoteAttachSessionManagerOptions(
                SessionTimeout: TimeSpan.FromSeconds(30),
                MaxSessions: 1));

        var firstRegistered = manager.TryRegisterFromHello(
            connectionId: Guid.NewGuid(),
            transportName: "http",
            remoteEndpoint: "127.0.0.1:29414",
            hello: CreateHello(Guid.NewGuid(), processId: 1, processName: "first"),
            timestampUtc: DateTimeOffset.UtcNow,
            out _,
            out _);
        var secondRegistered = manager.TryRegisterFromHello(
            connectionId: Guid.NewGuid(),
            transportName: "http",
            remoteEndpoint: "127.0.0.1:29414",
            hello: CreateHello(Guid.NewGuid(), processId: 2, processName: "second"),
            timestampUtc: DateTimeOffset.UtcNow,
            out _,
            out var rejectionReason);

        Assert.True(firstRegistered);
        Assert.False(secondRegistered);
        Assert.Equal("Session capacity reached.", rejectionReason);
    }

    [Fact]
    public void EvictStaleSessions_Removes_Only_Expired_Sessions()
    {
        var manager = new RemoteAttachSessionManager(
            new RemoteAttachSessionManagerOptions(
                SessionTimeout: TimeSpan.FromSeconds(30),
                MaxSessions: 4));

        var now = DateTimeOffset.UtcNow;
        var staleSessionId = Guid.NewGuid();
        var activeSessionId = Guid.NewGuid();

        Assert.True(manager.TryRegisterFromHello(
            connectionId: Guid.NewGuid(),
            transportName: "http",
            remoteEndpoint: "127.0.0.1:29414",
            hello: CreateHello(staleSessionId, processId: 1, processName: "stale"),
            timestampUtc: now,
            out _,
            out _));
        Assert.True(manager.TryRegisterFromHello(
            connectionId: Guid.NewGuid(),
            transportName: "http",
            remoteEndpoint: "127.0.0.1:29414",
            hello: CreateHello(activeSessionId, processId: 2, processName: "active"),
            timestampUtc: now,
            out _,
            out _));

        Assert.True(manager.TryTouch(activeSessionId, now.AddSeconds(20)));
        Assert.False(manager.TryTouch(Guid.NewGuid(), now.AddSeconds(20)));

        var evictedAt40 = manager.EvictStaleSessions(now.AddSeconds(40));
        Assert.Single(evictedAt40);
        Assert.Equal(staleSessionId, evictedAt40[0].SessionId);
        Assert.Equal(1, manager.SessionCount);

        var evictedAt70 = manager.EvictStaleSessions(now.AddSeconds(70));
        Assert.Single(evictedAt70);
        Assert.Equal(activeSessionId, evictedAt70[0].SessionId);
        Assert.Equal(0, manager.SessionCount);
    }

    private static RemoteHelloMessage CreateHello(Guid sessionId, int processId, string processName)
    {
        return new RemoteHelloMessage(
            SessionId: sessionId,
            ProcessId: processId,
            ProcessName: processName,
            ApplicationName: "app",
            MachineName: "machine",
            RuntimeVersion: "runtime",
            ClientName: "client",
            RequestedFeatures: Array.Empty<string>());
    }
}
