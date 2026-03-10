using System;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteDiagnosticsLoggerTests
{
    [Fact]
    public void InMemoryRemoteDiagnosticsLogger_Stores_Bounded_Entries()
    {
        var logger = new InMemoryRemoteDiagnosticsLogger(maxEntries: 2);
        logger.Log(CreateEntry("first"));
        logger.Log(CreateEntry("second"));
        logger.Log(CreateEntry("third"));

        var snapshot = logger.GetSnapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("second", snapshot[0].EventName);
        Assert.Equal("third", snapshot[1].EventName);
    }

    private static RemoteDiagnosticsLogEntry CreateEntry(string eventName)
    {
        return new RemoteDiagnosticsLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Level: RemoteDiagnosticsLogLevel.Information,
            Category: "tests.remote",
            EventName: eventName,
            TransportName: "http",
            ConnectionId: Guid.Empty,
            SessionId: Guid.Empty,
            RemoteEndpoint: null,
            MessageKind: null,
            Bytes: 0,
            Details: null,
            ExceptionType: null);
    }
}
