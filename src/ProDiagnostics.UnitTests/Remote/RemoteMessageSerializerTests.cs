using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Diagnostics.Remote;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteMessageSerializerTests
{
    [Fact]
    public void RemoteMessage_RoundTrips_AllKinds()
    {
        var sessionId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var messages = new IRemoteMessage[]
        {
            new RemoteHelloMessage(
                SessionId: sessionId,
                ProcessId: 4242,
                ProcessName: "HostProcess",
                ApplicationName: "HostApp",
                MachineName: "Machine",
                RuntimeVersion: "10.0.0",
                ClientName: "prodiagnostics-client",
                RequestedFeatures: new[] { "trees", "properties", "styles" }),
            new RemoteHelloAckMessage(
                SessionId: sessionId,
                NegotiatedProtocolVersion: RemoteProtocol.Version,
                EnabledFeatures: new[] { "trees", "properties" }),
            new RemoteHelloRejectMessage(
                SessionId: sessionId,
                Reason: "NotAllowed",
                Details: "Access denied by policy."),
            new RemoteKeepAliveMessage(
                SessionId: sessionId,
                Sequence: 7,
                TimestampUtc: timestamp),
            new RemoteDisconnectMessage(
                SessionId: sessionId,
                Reason: "Client requested shutdown."),
            new RemoteRequestMessage(
                SessionId: sessionId,
                RequestId: 1001,
                Method: "tree.snapshot.get",
                PayloadJson: "{\"scope\":\"combined\"}"),
            new RemoteResponseMessage(
                SessionId: sessionId,
                RequestId: 1001,
                IsSuccess: true,
                PayloadJson: "{\"nodes\":42}",
                ErrorCode: string.Empty,
                ErrorMessage: string.Empty),
            new RemoteStreamMessage(
                SessionId: sessionId,
                Topic: "metrics",
                Sequence: 12,
                DroppedMessages: 3,
                PayloadJson: "{\"value\":1.5}"),
            new RemoteErrorMessage(
                SessionId: sessionId,
                ErrorCode: "invalid.request",
                ErrorMessage: "The payload was invalid.",
                RelatedRequestId: 1001),
        };

        foreach (var expected in messages)
        {
            var frame = RemoteMessageSerializer.Serialize(expected).ToArray();
            Assert.True(frame.Length >= RemoteProtocol.HeaderSizeBytes);
            Assert.Equal(RemoteProtocol.Version, frame[0]);
            Assert.Equal((byte)expected.Kind, frame[1]);
            Assert.Equal(
                frame.Length - RemoteProtocol.HeaderSizeBytes,
                BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(2, sizeof(int))));

            Assert.True(RemoteMessageSerializer.TryDeserialize(frame, out var actual));
            Assert.NotNull(actual);
            AssertEquivalent(expected, actual!);
        }
    }

    [Fact]
    public void RemoteMessage_Rejects_UnsupportedProtocolVersion()
    {
        var frame = RemoteMessageSerializer.Serialize(
                new RemoteDisconnectMessage(Guid.NewGuid(), "done"))
            .ToArray();
        frame[0] = unchecked((byte)(RemoteProtocol.Version + 1));

        Assert.False(RemoteMessageSerializer.TryDeserialize(frame, out _));
    }

    [Fact]
    public void RemoteMessage_Rejects_UnknownMessageKind()
    {
        var frame = RemoteMessageSerializer.Serialize(
                new RemoteDisconnectMessage(Guid.NewGuid(), "done"))
            .ToArray();
        frame[1] = 0x7F;

        Assert.False(RemoteMessageSerializer.TryDeserialize(frame, out _));
    }

    [Fact]
    public void RemoteMessage_Rejects_TruncatedFrame()
    {
        var frame = RemoteMessageSerializer.Serialize(
                new RemoteRequestMessage(Guid.NewGuid(), 1, "test.method", "{}"))
            .ToArray();
        var truncated = frame.AsSpan(0, frame.Length - 1).ToArray();

        Assert.False(RemoteMessageSerializer.TryDeserialize(truncated, out _));
    }

    [Fact]
    public void RemoteMessage_Rejects_LengthMismatch()
    {
        var frame = RemoteMessageSerializer.Serialize(
                new RemoteRequestMessage(Guid.NewGuid(), 1, "test.method", "{}"))
            .ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(2, sizeof(int)), 1_000_000);

        Assert.False(RemoteMessageSerializer.TryDeserialize(frame, out _));
    }

    [Fact]
    public void RemoteMessage_Rejects_InvalidUtf8()
    {
        var frame = RemoteMessageSerializer.Serialize(
                new RemoteHelloMessage(
                    SessionId: Guid.NewGuid(),
                    ProcessId: 42,
                    ProcessName: "Process",
                    ApplicationName: "App",
                    MachineName: "Machine",
                    RuntimeVersion: "10.0.0",
                    ClientName: "client",
                    RequestedFeatures: new[] { "trees" }))
            .ToArray();

        // Header(6) + Guid(16) + ProcessId(4) + ProcessNameLength(4) => first byte of ProcessName payload.
        var processNameBytesOffset = RemoteProtocol.HeaderSizeBytes + 16 + 4 + 4;
        frame[processNameBytesOffset] = 0xFF;

        Assert.False(RemoteMessageSerializer.TryDeserialize(frame, out _));
    }

    [Fact]
    public void RemoteMessage_Rejects_InvalidListCount()
    {
        var frame = RemoteMessageSerializer.Serialize(
                new RemoteHelloAckMessage(
                    SessionId: Guid.NewGuid(),
                    NegotiatedProtocolVersion: RemoteProtocol.Version,
                    EnabledFeatures: new[] { "trees" }))
            .ToArray();

        // Header(6) + Guid(16) + Version(1) => start of list count.
        var listCountOffset = RemoteProtocol.HeaderSizeBytes + 16 + 1;
        BinaryPrimitives.WriteInt32LittleEndian(
            frame.AsSpan(listCountOffset, sizeof(int)),
            RemoteProtocol.MaxListEntries + 1);

        Assert.False(RemoteMessageSerializer.TryDeserialize(frame, out _));
    }

    private static void AssertEquivalent(IRemoteMessage expected, IRemoteMessage actual)
    {
        Assert.Equal(expected.Kind, actual.Kind);

        switch (expected)
        {
            case RemoteHelloMessage expectedHello:
                var actualHello = Assert.IsType<RemoteHelloMessage>(actual);
                Assert.Equal(expectedHello.SessionId, actualHello.SessionId);
                Assert.Equal(expectedHello.ProcessId, actualHello.ProcessId);
                Assert.Equal(expectedHello.ProcessName, actualHello.ProcessName);
                Assert.Equal(expectedHello.ApplicationName, actualHello.ApplicationName);
                Assert.Equal(expectedHello.MachineName, actualHello.MachineName);
                Assert.Equal(expectedHello.RuntimeVersion, actualHello.RuntimeVersion);
                Assert.Equal(expectedHello.ClientName, actualHello.ClientName);
                AssertStringList(expectedHello.RequestedFeatures, actualHello.RequestedFeatures);
                return;
            case RemoteHelloAckMessage expectedHelloAck:
                var actualHelloAck = Assert.IsType<RemoteHelloAckMessage>(actual);
                Assert.Equal(expectedHelloAck.SessionId, actualHelloAck.SessionId);
                Assert.Equal(expectedHelloAck.NegotiatedProtocolVersion, actualHelloAck.NegotiatedProtocolVersion);
                AssertStringList(expectedHelloAck.EnabledFeatures, actualHelloAck.EnabledFeatures);
                return;
            case RemoteHelloRejectMessage expectedHelloReject:
                var actualHelloReject = Assert.IsType<RemoteHelloRejectMessage>(actual);
                Assert.Equal(expectedHelloReject.SessionId, actualHelloReject.SessionId);
                Assert.Equal(expectedHelloReject.Reason, actualHelloReject.Reason);
                Assert.Equal(expectedHelloReject.Details, actualHelloReject.Details);
                return;
            case RemoteKeepAliveMessage expectedKeepAlive:
                var actualKeepAlive = Assert.IsType<RemoteKeepAliveMessage>(actual);
                Assert.Equal(expectedKeepAlive.SessionId, actualKeepAlive.SessionId);
                Assert.Equal(expectedKeepAlive.Sequence, actualKeepAlive.Sequence);
                Assert.Equal(expectedKeepAlive.TimestampUtc.ToUnixTimeMilliseconds(), actualKeepAlive.TimestampUtc.ToUnixTimeMilliseconds());
                return;
            case RemoteDisconnectMessage expectedDisconnect:
                var actualDisconnect = Assert.IsType<RemoteDisconnectMessage>(actual);
                Assert.Equal(expectedDisconnect.SessionId, actualDisconnect.SessionId);
                Assert.Equal(expectedDisconnect.Reason, actualDisconnect.Reason);
                return;
            case RemoteRequestMessage expectedRequest:
                var actualRequest = Assert.IsType<RemoteRequestMessage>(actual);
                Assert.Equal(expectedRequest.SessionId, actualRequest.SessionId);
                Assert.Equal(expectedRequest.RequestId, actualRequest.RequestId);
                Assert.Equal(expectedRequest.Method, actualRequest.Method);
                Assert.Equal(expectedRequest.PayloadJson, actualRequest.PayloadJson);
                return;
            case RemoteResponseMessage expectedResponse:
                var actualResponse = Assert.IsType<RemoteResponseMessage>(actual);
                Assert.Equal(expectedResponse.SessionId, actualResponse.SessionId);
                Assert.Equal(expectedResponse.RequestId, actualResponse.RequestId);
                Assert.Equal(expectedResponse.IsSuccess, actualResponse.IsSuccess);
                Assert.Equal(expectedResponse.PayloadJson, actualResponse.PayloadJson);
                Assert.Equal(expectedResponse.ErrorCode, actualResponse.ErrorCode);
                Assert.Equal(expectedResponse.ErrorMessage, actualResponse.ErrorMessage);
                return;
            case RemoteStreamMessage expectedStream:
                var actualStream = Assert.IsType<RemoteStreamMessage>(actual);
                Assert.Equal(expectedStream.SessionId, actualStream.SessionId);
                Assert.Equal(expectedStream.Topic, actualStream.Topic);
                Assert.Equal(expectedStream.Sequence, actualStream.Sequence);
                Assert.Equal(expectedStream.DroppedMessages, actualStream.DroppedMessages);
                Assert.Equal(expectedStream.PayloadJson, actualStream.PayloadJson);
                return;
            case RemoteErrorMessage expectedError:
                var actualError = Assert.IsType<RemoteErrorMessage>(actual);
                Assert.Equal(expectedError.SessionId, actualError.SessionId);
                Assert.Equal(expectedError.ErrorCode, actualError.ErrorCode);
                Assert.Equal(expectedError.ErrorMessage, actualError.ErrorMessage);
                Assert.Equal(expectedError.RelatedRequestId, actualError.RelatedRequestId);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected), expected.GetType().FullName);
        }
    }

    private static void AssertStringList(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.True(expected.SequenceEqual(actual));
    }
}
