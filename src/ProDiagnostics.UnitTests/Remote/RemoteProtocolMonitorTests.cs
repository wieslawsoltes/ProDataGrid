using System;
using System.Linq;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteProtocolMonitorTests
{
    [Fact]
    public void InMemoryRemoteProtocolMonitor_CapturesCounters_And_Events()
    {
        var monitor = new InMemoryRemoteProtocolMonitor(maxRecentEvents: 32);
        var connectionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        monitor.RecordConnectionAccepted("http", connectionId, "127.0.0.1");
        monitor.RecordConnectionRejected("http", "127.0.0.2", RemoteAccessDecisionCode.Forbidden, "blocked");
        monitor.RecordMessageSent("http", connectionId, "127.0.0.1", RemoteMessageKind.Request, 128);
        monitor.RecordMessageReceived("http", connectionId, "127.0.0.1", RemoteMessageKind.Response, 96);
        monitor.RecordSendFailure("http", connectionId, "127.0.0.1", RemoteMessageKind.Request, "send-failed");
        monitor.RecordReceiveFailure("http", connectionId, "127.0.0.1", "receive-failed");
        monitor.RecordStreamDropped(sessionId, RemoteStreamTopics.Metrics, 3);
        monitor.RecordStreamDispatchFailure(sessionId, RemoteStreamTopics.Metrics, "dispatch-failed");
        monitor.RecordConnectionClosed("http", connectionId, "127.0.0.1", "closed");

        var snapshot = monitor.GetSnapshot();
        Assert.Equal(1, snapshot.ConnectionsAccepted);
        Assert.Equal(1, snapshot.ConnectionsRejected);
        Assert.Equal(1, snapshot.ConnectionsClosed);
        Assert.Equal(1, snapshot.MessagesSent);
        Assert.Equal(1, snapshot.MessagesReceived);
        Assert.Equal(128, snapshot.BytesSent);
        Assert.Equal(96, snapshot.BytesReceived);
        Assert.Equal(1, snapshot.SendFailures);
        Assert.Equal(1, snapshot.ReceiveFailures);
        Assert.Equal(3, snapshot.StreamDroppedMessages);
        Assert.Equal(1, snapshot.StreamDispatchFailures);

        var sentRequestCount = snapshot.SentByKind.Single(x => x.Kind == RemoteMessageKind.Request).Count;
        var receivedResponseCount = snapshot.ReceivedByKind.Single(x => x.Kind == RemoteMessageKind.Response).Count;
        Assert.Equal(1, sentRequestCount);
        Assert.Equal(1, receivedResponseCount);
        Assert.NotEmpty(snapshot.RecentEvents);
        Assert.Equal(RemoteProtocolEventType.ConnectionClosed, snapshot.RecentEvents[^1].EventType);
    }

    [Fact]
    public void InMemoryRemoteProtocolMonitor_RecentEvents_AreBounded()
    {
        var monitor = new InMemoryRemoteProtocolMonitor(maxRecentEvents: 2);
        var connectionId = Guid.NewGuid();

        monitor.RecordConnectionAccepted("http", connectionId, "first");
        monitor.RecordMessageSent("http", connectionId, "second", RemoteMessageKind.KeepAlive, 10);
        monitor.RecordConnectionClosed("http", connectionId, "third", "done");

        var snapshot = monitor.GetSnapshot();
        Assert.Equal(2, snapshot.RecentEvents.Count);
        Assert.Equal(RemoteProtocolEventType.MessageSent, snapshot.RecentEvents[0].EventType);
        Assert.Equal(RemoteProtocolEventType.ConnectionClosed, snapshot.RecentEvents[1].EventType);
    }
}
