using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Serializes and deserializes remote protocol messages using binary framing.
/// </summary>
public static class RemoteMessageSerializer
{
    public static ReadOnlyMemory<byte> Serialize(IRemoteMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var payloadWriter = new PayloadWriter(256);
        WritePayload(payloadWriter, message);
        var payloadLength = payloadWriter.WrittenCount;

        if (payloadLength > RemoteProtocol.MaxFramePayloadBytes)
        {
            throw new InvalidOperationException(
                "Remote message payload exceeds max frame size of " + RemoteProtocol.MaxFramePayloadBytes + " bytes.");
        }

        var frame = GC.AllocateUninitializedArray<byte>(RemoteProtocol.HeaderSizeBytes + payloadLength);
        frame[0] = RemoteProtocol.Version;
        frame[1] = (byte)message.Kind;
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(2, sizeof(int)), payloadLength);
        payloadWriter.CopyWrittenBytesTo(frame.AsSpan(RemoteProtocol.HeaderSizeBytes));
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out IRemoteMessage? message)
    {
        message = null;

        var frameReader = new PayloadReader(frame);
        if (!frameReader.TryReadByte(out var version) || version != RemoteProtocol.Version)
        {
            return false;
        }

        if (!frameReader.TryReadByte(out var kindByte) || !TryParseKind(kindByte, out var kind))
        {
            return false;
        }

        if (!frameReader.TryReadInt32(out var payloadLength) ||
            payloadLength < 0 ||
            payloadLength > RemoteProtocol.MaxFramePayloadBytes ||
            frameReader.Remaining != payloadLength ||
            !frameReader.TryReadBytes(payloadLength, out var payload))
        {
            return false;
        }

        var payloadReader = new PayloadReader(payload);
        if (!TryReadPayload(ref payloadReader, kind, out message))
        {
            return false;
        }

        return payloadReader.Remaining == 0;
    }

    private static bool TryParseKind(byte value, out RemoteMessageKind kind)
    {
        switch ((RemoteMessageKind)value)
        {
            case RemoteMessageKind.Hello:
            case RemoteMessageKind.HelloAck:
            case RemoteMessageKind.HelloReject:
            case RemoteMessageKind.KeepAlive:
            case RemoteMessageKind.Disconnect:
            case RemoteMessageKind.Request:
            case RemoteMessageKind.Response:
            case RemoteMessageKind.Stream:
            case RemoteMessageKind.Error:
                kind = (RemoteMessageKind)value;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static void WritePayload(PayloadWriter writer, IRemoteMessage message)
    {
        switch (message)
        {
            case RemoteHelloMessage hello:
                writer.WriteGuid(hello.SessionId);
                writer.WriteInt32(hello.ProcessId);
                writer.WriteString(hello.ProcessName);
                writer.WriteString(hello.ApplicationName);
                writer.WriteString(hello.MachineName);
                writer.WriteString(hello.RuntimeVersion);
                writer.WriteString(hello.ClientName);
                WriteStringList(writer, hello.RequestedFeatures);
                return;
            case RemoteHelloAckMessage helloAck:
                writer.WriteGuid(helloAck.SessionId);
                writer.WriteByte(helloAck.NegotiatedProtocolVersion);
                WriteStringList(writer, helloAck.EnabledFeatures);
                return;
            case RemoteHelloRejectMessage helloReject:
                writer.WriteGuid(helloReject.SessionId);
                writer.WriteString(helloReject.Reason);
                writer.WriteString(helloReject.Details);
                return;
            case RemoteKeepAliveMessage keepAlive:
                writer.WriteGuid(keepAlive.SessionId);
                writer.WriteInt64(keepAlive.Sequence);
                writer.WriteInt64(keepAlive.TimestampUtc.ToUnixTimeMilliseconds());
                return;
            case RemoteDisconnectMessage disconnect:
                writer.WriteGuid(disconnect.SessionId);
                writer.WriteString(disconnect.Reason);
                return;
            case RemoteRequestMessage request:
                writer.WriteGuid(request.SessionId);
                writer.WriteInt64(request.RequestId);
                writer.WriteString(request.Method);
                writer.WriteString(request.PayloadJson);
                return;
            case RemoteResponseMessage response:
                writer.WriteGuid(response.SessionId);
                writer.WriteInt64(response.RequestId);
                writer.WriteBoolean(response.IsSuccess);
                writer.WriteString(response.PayloadJson);
                writer.WriteString(response.ErrorCode);
                writer.WriteString(response.ErrorMessage);
                return;
            case RemoteStreamMessage stream:
                writer.WriteGuid(stream.SessionId);
                writer.WriteString(stream.Topic);
                writer.WriteInt64(stream.Sequence);
                writer.WriteInt32(stream.DroppedMessages);
                writer.WriteString(stream.PayloadJson);
                return;
            case RemoteErrorMessage error:
                writer.WriteGuid(error.SessionId);
                writer.WriteString(error.ErrorCode);
                writer.WriteString(error.ErrorMessage);
                writer.WriteInt64(error.RelatedRequestId);
                return;
            default:
                throw new InvalidOperationException("Unsupported remote message type: " + message.GetType().FullName);
        }
    }

    private static bool TryReadPayload(ref PayloadReader reader, RemoteMessageKind kind, out IRemoteMessage? message)
    {
        message = null;

        switch (kind)
        {
            case RemoteMessageKind.Hello:
                if (!reader.TryReadGuid(out var helloSessionId) ||
                    !reader.TryReadInt32(out var processId) ||
                    !reader.TryReadString(out var processName) ||
                    !reader.TryReadString(out var appName) ||
                    !reader.TryReadString(out var machineName) ||
                    !reader.TryReadString(out var runtimeVersion) ||
                    !reader.TryReadString(out var clientName) ||
                    !TryReadStringList(ref reader, out var requestedFeatures))
                {
                    return false;
                }

                message = new RemoteHelloMessage(
                    helloSessionId,
                    processId,
                    processName,
                    appName,
                    machineName,
                    runtimeVersion,
                    clientName,
                    requestedFeatures);
                return true;
            case RemoteMessageKind.HelloAck:
                if (!reader.TryReadGuid(out var ackSessionId) ||
                    !reader.TryReadByte(out var negotiatedVersion) ||
                    !TryReadStringList(ref reader, out var enabledFeatures))
                {
                    return false;
                }

                message = new RemoteHelloAckMessage(ackSessionId, negotiatedVersion, enabledFeatures);
                return true;
            case RemoteMessageKind.HelloReject:
                if (!reader.TryReadGuid(out var rejectSessionId) ||
                    !reader.TryReadString(out var reason) ||
                    !reader.TryReadString(out var details))
                {
                    return false;
                }

                message = new RemoteHelloRejectMessage(rejectSessionId, reason, details);
                return true;
            case RemoteMessageKind.KeepAlive:
                if (!reader.TryReadGuid(out var keepAliveSessionId) ||
                    !reader.TryReadInt64(out var sequence) ||
                    !reader.TryReadDateTimeOffset(out var keepAliveTime))
                {
                    return false;
                }

                message = new RemoteKeepAliveMessage(keepAliveSessionId, sequence, keepAliveTime);
                return true;
            case RemoteMessageKind.Disconnect:
                if (!reader.TryReadGuid(out var disconnectSessionId) ||
                    !reader.TryReadString(out var disconnectReason))
                {
                    return false;
                }

                message = new RemoteDisconnectMessage(disconnectSessionId, disconnectReason);
                return true;
            case RemoteMessageKind.Request:
                if (!reader.TryReadGuid(out var requestSessionId) ||
                    !reader.TryReadInt64(out var requestId) ||
                    !reader.TryReadString(out var method) ||
                    !reader.TryReadString(out var requestPayload))
                {
                    return false;
                }

                message = new RemoteRequestMessage(requestSessionId, requestId, method, requestPayload);
                return true;
            case RemoteMessageKind.Response:
                if (!reader.TryReadGuid(out var responseSessionId) ||
                    !reader.TryReadInt64(out var responseRequestId) ||
                    !reader.TryReadBoolean(out var isSuccess) ||
                    !reader.TryReadString(out var responsePayload) ||
                    !reader.TryReadString(out var errorCode) ||
                    !reader.TryReadString(out var errorMessage))
                {
                    return false;
                }

                message = new RemoteResponseMessage(
                    responseSessionId,
                    responseRequestId,
                    isSuccess,
                    responsePayload,
                    errorCode,
                    errorMessage);
                return true;
            case RemoteMessageKind.Stream:
                if (!reader.TryReadGuid(out var streamSessionId) ||
                    !reader.TryReadString(out var topic) ||
                    !reader.TryReadInt64(out var streamSequence) ||
                    !reader.TryReadInt32(out var droppedMessages) ||
                    !reader.TryReadString(out var streamPayload))
                {
                    return false;
                }

                message = new RemoteStreamMessage(streamSessionId, topic, streamSequence, droppedMessages, streamPayload);
                return true;
            case RemoteMessageKind.Error:
                if (!reader.TryReadGuid(out var errorSessionId) ||
                    !reader.TryReadString(out var errorCodePayload) ||
                    !reader.TryReadString(out var errorMessagePayload) ||
                    !reader.TryReadInt64(out var relatedRequestId))
                {
                    return false;
                }

                message = new RemoteErrorMessage(errorSessionId, errorCodePayload, errorMessagePayload, relatedRequestId);
                return true;
            default:
                return false;
        }
    }

    private static void WriteStringList(PayloadWriter writer, IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            writer.WriteInt32(0);
            return;
        }

        writer.WriteInt32(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteString(values[i] ?? string.Empty);
        }
    }

    private static bool TryReadStringList(ref PayloadReader reader, out IReadOnlyList<string> values)
    {
        values = Array.Empty<string>();
        if (!reader.TryReadInt32(out var count) ||
            count < 0 ||
            count > RemoteProtocol.MaxListEntries)
        {
            return false;
        }

        if (count == 0)
        {
            return true;
        }

        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (!reader.TryReadString(out result[i]))
            {
                return false;
            }
        }

        values = result;
        return true;
    }

    private sealed class PayloadWriter : IDisposable
    {
        private byte[] _buffer;
        private int _position;
        private bool _disposed;
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public PayloadWriter(int initialCapacity)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(16, initialCapacity));
        }

        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _position);
        public int WrittenCount => _position;

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteInt32(int value)
        {
            EnsureCapacity(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position, sizeof(int)), value);
            _position += sizeof(int);
        }

        public void WriteInt64(long value)
        {
            EnsureCapacity(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position, sizeof(long)), value);
            _position += sizeof(long);
        }

        public void WriteGuid(Guid value)
        {
            EnsureCapacity(16);
            value.TryWriteBytes(_buffer.AsSpan(_position, 16));
            _position += 16;
        }

        public void WriteString(string value)
        {
            var safeValue = value ?? string.Empty;
            if (safeValue.Length == 0)
            {
                WriteInt32(0);
                return;
            }

            var byteCount = Utf8.GetByteCount(safeValue);
            WriteInt32(byteCount);
            EnsureCapacity(byteCount);
            Utf8.GetBytes(safeValue, _buffer.AsSpan(_position, byteCount));
            _position += byteCount;
        }

        public void CopyWrittenBytesTo(Span<byte> destination)
        {
            if (destination.Length < _position)
            {
                throw new ArgumentException("Destination buffer is smaller than written payload.");
            }

            _buffer.AsSpan(0, _position).CopyTo(destination);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (_buffer.Length - _position >= sizeHint)
            {
                return;
            }

            var newSize = Math.Max(_buffer.Length * 2, _position + sizeHint);
            var expanded = ArrayPool<byte>.Shared.Rent(newSize);
            _buffer.AsSpan(0, _position).CopyTo(expanded.AsSpan());
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = expanded;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = Array.Empty<byte>();
            _position = 0;
        }
    }

    private ref struct PayloadReader
    {
        private readonly ReadOnlySpan<byte> _span;
        private int _offset;
        private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public PayloadReader(ReadOnlySpan<byte> span)
        {
            _span = span;
            _offset = 0;
        }

        public int Remaining => _span.Length - _offset;

        public bool TryReadByte(out byte value)
        {
            if (_offset + 1 > _span.Length)
            {
                value = 0;
                return false;
            }

            value = _span[_offset++];
            return true;
        }

        public bool TryReadBoolean(out bool value)
        {
            if (!TryReadByte(out var byteValue) || (byteValue != 0 && byteValue != 1))
            {
                value = false;
                return false;
            }

            value = byteValue == 1;
            return true;
        }

        public bool TryReadInt32(out int value)
        {
            if (_offset + sizeof(int) > _span.Length)
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(_span.Slice(_offset, sizeof(int)));
            _offset += sizeof(int);
            return true;
        }

        public bool TryReadInt64(out long value)
        {
            if (_offset + sizeof(long) > _span.Length)
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadInt64LittleEndian(_span.Slice(_offset, sizeof(long)));
            _offset += sizeof(long);
            return true;
        }

        public bool TryReadGuid(out Guid value)
        {
            if (_offset + 16 > _span.Length)
            {
                value = default;
                return false;
            }

            value = new Guid(_span.Slice(_offset, 16));
            _offset += 16;
            return true;
        }

        public bool TryReadString(out string value)
        {
            value = string.Empty;
            if (!TryReadInt32(out var byteCount) ||
                byteCount < 0 ||
                _offset + byteCount > _span.Length)
            {
                return false;
            }

            if (byteCount == 0)
            {
                return true;
            }

            try
            {
                value = Utf8Strict.GetString(_span.Slice(_offset, byteCount));
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            _offset += byteCount;
            return true;
        }

        public bool TryReadDateTimeOffset(out DateTimeOffset value)
        {
            value = default;
            if (!TryReadInt64(out var unixTimeMs))
            {
                return false;
            }

            try
            {
                value = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public bool TryReadBytes(int length, out ReadOnlySpan<byte> value)
        {
            if (length < 0 || _offset + length > _span.Length)
            {
                value = default;
                return false;
            }

            value = _span.Slice(_offset, length);
            _offset += length;
            return true;
        }
    }
}
