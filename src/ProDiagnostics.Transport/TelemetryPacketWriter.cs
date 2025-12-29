using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProDiagnostics.Transport;

public sealed class TelemetryPacketWriter
{
    private readonly TelemetryBufferWriter _writer = new(2048);
    private readonly object _sync = new();

    public ReadOnlyMemory<byte> Write(TelemetryHello hello)
    {
        lock (_sync)
        {
            WriteHello(hello);
            return CopyWritten();
        }
    }

    internal PooledPayload WritePooled(TelemetryHello hello, int maxPacketBytes)
    {
        lock (_sync)
        {
            WriteHello(hello);
            return CopyWrittenPooled(maxPacketBytes);
        }
    }

    public ReadOnlyMemory<byte> Write(TelemetryActivity activity, int maxTags)
    {
        lock (_sync)
        {
            WriteActivity(activity, maxTags);
            return CopyWritten();
        }
    }

    internal PooledPayload WritePooled(TelemetryActivity activity, int maxTags, int maxPacketBytes)
    {
        lock (_sync)
        {
            WriteActivity(activity, maxTags);
            return CopyWrittenPooled(maxPacketBytes);
        }
    }

    public ReadOnlyMemory<byte> Write(TelemetryMetric metric, int maxTags)
    {
        lock (_sync)
        {
            WriteMetric(metric, maxTags);
            return CopyWritten();
        }
    }

    internal PooledPayload WritePooled(TelemetryMetric metric, int maxTags, int maxPacketBytes)
    {
        lock (_sync)
        {
            WriteMetric(metric, maxTags);
            return CopyWrittenPooled(maxPacketBytes);
        }
    }

    private void WriteHello(TelemetryHello hello)
    {
        _writer.Reset();
        WriteHeader(TelemetryMessageType.Hello, hello.SessionId, hello.Timestamp);
        _writer.WriteInt32(hello.ProcessId);
        _writer.WriteString(hello.ProcessName);
        _writer.WriteString(hello.AppName);
        _writer.WriteString(hello.MachineName);
        _writer.WriteString(hello.RuntimeVersion);
    }

    private void WriteActivity(TelemetryActivity activity, int maxTags)
    {
        _writer.Reset();
        WriteHeader(TelemetryMessageType.Activity, activity.SessionId, activity.Timestamp);
        _writer.WriteString(activity.SourceName);
        _writer.WriteString(activity.Name);
        _writer.WriteInt64(activity.StartTime.ToUnixTimeMilliseconds());
        _writer.WriteInt64(activity.Duration.Ticks);
        WriteTags(activity.Tags, maxTags);
    }

    private void WriteMetric(TelemetryMetric metric, int maxTags)
    {
        _writer.Reset();
        WriteHeader(TelemetryMessageType.Metric, metric.SessionId, metric.Timestamp);
        _writer.WriteString(metric.MeterName);
        _writer.WriteString(metric.InstrumentName);
        _writer.WriteString(metric.Description);
        _writer.WriteString(metric.Unit);
        _writer.WriteString(metric.InstrumentType);
        _writer.WriteByte((byte)metric.Value.Type);
        if (metric.Value.Type == TelemetryMetricValueType.Double)
        {
            _writer.WriteDouble(metric.Value.DoubleValue);
        }
        else
        {
            _writer.WriteInt64(metric.Value.LongValue);
        }
        WriteTags(metric.Tags, maxTags);
    }

    private void WriteHeader(TelemetryMessageType messageType, Guid sessionId, DateTimeOffset timestamp)
    {
        _writer.WriteByte(TelemetryProtocol.Version);
        _writer.WriteByte((byte)messageType);
        _writer.WriteGuid(sessionId);
        _writer.WriteInt64(timestamp.ToUnixTimeMilliseconds());
    }

    private void WriteTags(IReadOnlyList<TelemetryTag> tags, int maxTags)
    {
        if (maxTags <= 0 || tags.Count == 0)
        {
            _writer.WriteUInt16(0);
            return;
        }

        var limitedMax = Math.Min(maxTags, TelemetryProtocol.MaxTagsPerPacket);
        var count = Math.Min(tags.Count, limitedMax);
        _writer.WriteUInt16((ushort)count);
        for (var i = 0; i < count; i++)
        {
            var tag = tags[i];
            _writer.WriteString(tag.Key);
            WriteTagValue(tag.Value);
        }
    }

    private ReadOnlyMemory<byte> CopyWritten()
        => _writer.WrittenMemory.Span.ToArray();

    private PooledPayload CopyWrittenPooled(int maxPacketBytes)
    {
        var span = _writer.WrittenSpan;
        if (span.IsEmpty || span.Length > maxPacketBytes)
        {
            return new PooledPayload(Array.Empty<byte>(), 0, pooled: false);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(span.Length);
        span.CopyTo(buffer.AsSpan(0, span.Length));
        return new PooledPayload(buffer, span.Length, pooled: true);
    }

    internal readonly struct PooledPayload
    {
        private readonly bool _pooled;

        public PooledPayload(byte[] buffer, int length, bool pooled)
        {
            Buffer = buffer;
            Length = length;
            _pooled = pooled;
        }

        public byte[] Buffer { get; }
        public int Length { get; }

        public void Return()
        {
            if (_pooled)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
    }

    private void WriteTagValue(object? value)
    {
        switch (value)
        {
            case null:
                _writer.WriteByte((byte)TelemetryTagValueType.Null);
                return;
            case string text:
                _writer.WriteByte((byte)TelemetryTagValueType.String);
                _writer.WriteString(text);
                return;
            case bool flag:
                _writer.WriteByte((byte)TelemetryTagValueType.Boolean);
                _writer.WriteByte(flag ? (byte)1 : (byte)0);
                return;
            case byte number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case sbyte number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case short number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case ushort number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case int number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case uint number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case long number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(number);
                return;
            case ulong number:
                _writer.WriteByte((byte)TelemetryTagValueType.Int64);
                _writer.WriteInt64(unchecked((long)number));
                return;
            case float number:
                _writer.WriteByte((byte)TelemetryTagValueType.Double);
                _writer.WriteDouble(number);
                return;
            case double number:
                _writer.WriteByte((byte)TelemetryTagValueType.Double);
                _writer.WriteDouble(number);
                return;
            case decimal number:
                _writer.WriteByte((byte)TelemetryTagValueType.String);
                _writer.WriteString(number.ToString(CultureInfo.InvariantCulture));
                return;
            default:
                _writer.WriteByte((byte)TelemetryTagValueType.String);
                _writer.WriteString(value.ToString() ?? string.Empty);
                return;
        }
    }

    private sealed class TelemetryBufferWriter
    {
        private byte[] _buffer;
        private int _position;
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public TelemetryBufferWriter(int capacity)
        {
            _buffer = new byte[capacity];
        }

        public ReadOnlyMemory<byte> WrittenMemory => new(_buffer, 0, _position);
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _position);

        public void Reset() => _position = 0;

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        public void WriteInt32(int value)
        {
            EnsureCapacity(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position, sizeof(int)), value);
            _position += sizeof(int);
        }

        public void WriteUInt16(ushort value)
        {
            EnsureCapacity(sizeof(ushort));
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position, sizeof(ushort)), value);
            _position += sizeof(ushort);
        }

        public void WriteInt64(long value)
        {
            EnsureCapacity(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position, sizeof(long)), value);
            _position += sizeof(long);
        }

        public void WriteDouble(double value)
        {
            EnsureCapacity(sizeof(double));
            var bits = BitConverter.DoubleToInt64Bits(value);
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position, sizeof(double)), bits);
            _position += sizeof(double);
        }

        public void WriteGuid(Guid value)
        {
            EnsureCapacity(16);
            value.TryWriteBytes(_buffer.AsSpan(_position, 16));
            _position += 16;
        }

        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteInt32(0);
                return;
            }

            var byteCount = Utf8.GetByteCount(value);
            WriteInt32(byteCount);
            EnsureCapacity(byteCount);
            Utf8.GetBytes(value, _buffer.AsSpan(_position, byteCount));
            _position += byteCount;
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (_buffer.Length - _position >= sizeHint)
            {
                return;
            }

            var newSize = Math.Max(_buffer.Length * 2, _position + sizeHint);
            Array.Resize(ref _buffer, newSize);
        }
    }
}
