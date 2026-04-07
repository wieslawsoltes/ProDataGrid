using System;
using System.Collections.Generic;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteProtocolCompatibilityMatrixTests
{
    [Fact]
    public void RemoteProtocol_V1_Constants_AreFrozen()
    {
        Assert.Equal(1, RemoteProtocol.Version);
        Assert.Equal(6, RemoteProtocol.HeaderSizeBytes);
        Assert.Equal(16 * 1024 * 1024, RemoteProtocol.MaxFramePayloadBytes);
    }

    [Fact]
    public void RemoteMessageKind_Values_AreFrozen()
    {
        var expected = new Dictionary<RemoteMessageKind, byte>
        {
            [RemoteMessageKind.Hello] = 1,
            [RemoteMessageKind.HelloAck] = 2,
            [RemoteMessageKind.HelloReject] = 3,
            [RemoteMessageKind.KeepAlive] = 4,
            [RemoteMessageKind.Disconnect] = 5,
            [RemoteMessageKind.Request] = 6,
            [RemoteMessageKind.Response] = 7,
            [RemoteMessageKind.Stream] = 8,
            [RemoteMessageKind.Error] = 9,
        };

        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, (byte)pair.Key);
        }
    }

    [Fact]
    public void RemoteMessageSerializer_Uses_Frozen_MessageKind_Discriminators()
    {
        var sessionId = Guid.NewGuid();
        var samples = new Dictionary<IRemoteMessage, byte>
        {
            [new RemoteHelloMessage(sessionId, 1, "p", "a", "m", "10.0", "c", Array.Empty<string>())] = 1,
            [new RemoteHelloAckMessage(sessionId, 1, Array.Empty<string>())] = 2,
            [new RemoteHelloRejectMessage(sessionId, "r", "d")] = 3,
            [new RemoteKeepAliveMessage(sessionId, 1, DateTimeOffset.UnixEpoch)] = 4,
            [new RemoteDisconnectMessage(sessionId, "r")] = 5,
            [new RemoteRequestMessage(sessionId, 1, "m", "{}")] = 6,
            [new RemoteResponseMessage(sessionId, 1, true, "{}", string.Empty, string.Empty)] = 7,
            [new RemoteStreamMessage(sessionId, "metrics", 1, 0, "{}")] = 8,
            [new RemoteErrorMessage(sessionId, "e", "m", 1)] = 9,
        };

        foreach (var pair in samples)
        {
            var frame = RemoteMessageSerializer.Serialize(pair.Key);
            Assert.True(frame.Length >= RemoteProtocol.HeaderSizeBytes);
            Assert.Equal(pair.Value, frame.Span[1]);
        }
    }
}
