using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace ProDiagnostics.Transport;

public static class TelemetryPacketReader
{
    public static bool TryRead(ReadOnlySpan<byte> data, out TelemetryPacket? packet)
    {
        packet = null;
        var reader = new TelemetrySpanReader(data);

        if (!reader.TryReadByte(out var version) || version != TelemetryProtocol.Version)
        {
            return false;
        }

        if (!reader.TryReadByte(out var typeByte))
        {
            return false;
        }

        if (!reader.TryReadGuid(out var sessionId))
        {
            return false;
        }

        if (!reader.TryReadInt64(out var timestampMs))
        {
            return false;
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        var messageType = (TelemetryMessageType)typeByte;

        switch (messageType)
        {
            case TelemetryMessageType.Hello:
                if (!reader.TryReadInt32(out var pid))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var processName))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var appName))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var machineName))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var runtimeVersion))
                {
                    return false;
                }

                packet = new TelemetryHello(sessionId, timestamp, pid, processName, appName, machineName, runtimeVersion);
                return true;
            case TelemetryMessageType.Activity:
                if (!reader.TryReadKeyString(out var sourceName))
                {
                    return false;
                }

                if (!reader.TryReadKeyString(out var activityName))
                {
                    return false;
                }

                if (!reader.TryReadInt64(out var startMs))
                {
                    return false;
                }

                if (!reader.TryReadInt64(out var durationTicks))
                {
                    return false;
                }

                if (!reader.TryReadTags(out var activityTags))
                {
                    return false;
                }

                DateTimeOffset startTime;
                try
                {
                    startTime = DateTimeOffset.FromUnixTimeMilliseconds(startMs);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }

                if (durationTicks < 0 || durationTicks > TimeSpan.MaxValue.Ticks)
                {
                    return false;
                }

                TimeSpan duration;
                try
                {
                    duration = TimeSpan.FromTicks(durationTicks);
                }
                catch (OverflowException)
                {
                    return false;
                }

                packet = new TelemetryActivity(
                    sessionId,
                    timestamp,
                    sourceName,
                    activityName,
                    startTime,
                    duration,
                    activityTags);
                return true;
            case TelemetryMessageType.Metric:
                if (!reader.TryReadKeyString(out var meterName))
                {
                    return false;
                }

                if (!reader.TryReadKeyString(out var instrumentName))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var description))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var unit))
                {
                    return false;
                }

                if (!reader.TryReadValueString(out var instrumentType))
                {
                    return false;
                }

                if (!reader.TryReadByte(out var metricValueType))
                {
                    return false;
                }

                TelemetryMetricValue value;
                var valueType = (TelemetryMetricValueType)metricValueType;
                if (valueType == TelemetryMetricValueType.Double)
                {
                    if (!reader.TryReadDouble(out var doubleValue))
                    {
                        return false;
                    }

                    value = TelemetryMetricValue.FromDouble(doubleValue);
                }
                else if (valueType == TelemetryMetricValueType.Long)
                {
                    if (!reader.TryReadInt64(out var longValue))
                    {
                        return false;
                    }

                    value = TelemetryMetricValue.FromLong(longValue);
                }
                else
                {
                    return false;
                }

                if (!reader.TryReadTags(out var metricTags))
                {
                    return false;
                }

                packet = new TelemetryMetric(
                    sessionId,
                    timestamp,
                    meterName,
                    instrumentName,
                    description,
                    unit,
                    instrumentType,
                    value,
                    metricTags);
                return true;
            default:
                return false;
        }
    }

    private ref struct TelemetrySpanReader
    {
        private readonly ReadOnlySpan<byte> _span;
        private int _offset;
        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public TelemetrySpanReader(ReadOnlySpan<byte> span)
        {
            _span = span;
            _offset = 0;
        }

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

        public bool TryReadDouble(out double value)
        {
            if (_offset + sizeof(double) > _span.Length)
            {
                value = 0;
                return false;
            }

            var bits = BinaryPrimitives.ReadInt64LittleEndian(_span.Slice(_offset, sizeof(double)));
            value = BitConverter.Int64BitsToDouble(bits);
            _offset += sizeof(double);
            return true;
        }

        public bool TryReadGuid(out Guid value)
        {
            if (_offset + 16 > _span.Length)
            {
                value = Guid.Empty;
                return false;
            }

            value = new Guid(_span.Slice(_offset, 16));
            _offset += 16;
            return true;
        }

        public bool TryReadKeyString(out string value)
            => TryReadString(out value, allowFallback: false);

        public bool TryReadValueString(out string value)
            => TryReadString(out value, allowFallback: true);

        private bool TryReadString(out string value, bool allowFallback)
        {
            if (!TryReadInt32(out var length))
            {
                value = string.Empty;
                return false;
            }

            if (length == 0)
            {
                value = string.Empty;
                return true;
            }

            if (length < 0 || _offset + length > _span.Length)
            {
                value = string.Empty;
                return false;
            }

            try
            {
                value = Utf8Strict.GetString(_span.Slice(_offset, length));
            }
            catch (DecoderFallbackException)
            {
                if (!allowFallback)
                {
                    value = string.Empty;
                    return false;
                }

                value = Utf8.GetString(_span.Slice(_offset, length));
            }

            _offset += length;
            return true;
        }

        public bool TryReadTags(out IReadOnlyList<TelemetryTag> tags)
        {
            tags = Array.Empty<TelemetryTag>();

            if (!TryReadUInt16(out var count))
            {
                return false;
            }

            if (count > TelemetryProtocol.MaxTagsPerPacket)
            {
                return false;
            }

            if (count == 0)
            {
                return true;
            }

            var list = new List<TelemetryTag>(count);
            for (var i = 0; i < count; i++)
            {
                if (!TryReadKeyString(out var key))
                {
                    return false;
                }

                if (!TryReadTagValue(out var value))
                {
                    return false;
                }

                list.Add(new TelemetryTag(key, value));
            }

            tags = list;
            return true;
        }

        private bool TryReadUInt16(out ushort value)
        {
            if (_offset + sizeof(ushort) > _span.Length)
            {
                value = 0;
                return false;
            }

            value = BinaryPrimitives.ReadUInt16LittleEndian(_span.Slice(_offset, sizeof(ushort)));
            _offset += sizeof(ushort);
            return true;
        }

        private bool TryReadTagValue(out object? value)
        {
            value = null;
            if (!TryReadByte(out var typeByte))
            {
                return false;
            }

            var type = (TelemetryTagValueType)typeByte;
            switch (type)
            {
                case TelemetryTagValueType.Null:
                    value = null;
                    return true;
                case TelemetryTagValueType.String:
                    if (!TryReadValueString(out var text))
                    {
                        return false;
                    }
                    value = text;
                    return true;
                case TelemetryTagValueType.Boolean:
                    if (!TryReadByte(out var flag))
                    {
                        return false;
                    }
                    value = flag != 0;
                    return true;
                case TelemetryTagValueType.Int64:
                    if (!TryReadInt64(out var longValue))
                    {
                        return false;
                    }
                    value = longValue;
                    return true;
                case TelemetryTagValueType.Double:
                    if (!TryReadDouble(out var doubleValue))
                    {
                        return false;
                    }
                    value = doubleValue;
                    return true;
                default:
                    return false;
            }
        }
    }
}
