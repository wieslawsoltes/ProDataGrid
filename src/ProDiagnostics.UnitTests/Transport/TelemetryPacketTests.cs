using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Transport;

public class TelemetryPacketTests
{
    [Fact]
    public void TelemetryPacket_RoundTrips_Hello()
    {
        var sessionId = Guid.NewGuid();
        var hello = new TelemetryHello(
            sessionId,
            DateTimeOffset.UtcNow,
            4242,
            "TestHost",
            "TestApp",
            "TestMachine",
            "10.0.0");

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(hello);

        Assert.True(TelemetryPacketReader.TryRead(payload.Span, out var packet));
        var read = Assert.IsType<TelemetryHello>(packet);
        Assert.Equal(sessionId, read.SessionId);
        Assert.Equal(hello.ProcessId, read.ProcessId);
        Assert.Equal(hello.ProcessName, read.ProcessName);
        Assert.Equal(hello.AppName, read.AppName);
        Assert.Equal(hello.MachineName, read.MachineName);
        Assert.Equal(hello.RuntimeVersion, read.RuntimeVersion);
    }

    [Fact]
    public void TelemetryPacket_RoundTrips_Activity_With_Tags()
    {
        var sessionId = Guid.NewGuid();
        var tags = new[]
        {
            new TelemetryTag("Flag", true),
            new TelemetryTag("Count", 12L),
            new TelemetryTag("Label", "Sample")
        };
        var activity = new TelemetryActivity(
            sessionId,
            DateTimeOffset.UtcNow,
            "Source",
            "Activity",
            DateTimeOffset.UtcNow.AddMilliseconds(-10),
            TimeSpan.FromMilliseconds(10),
            tags);

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(activity, 8);

        Assert.True(TelemetryPacketReader.TryRead(payload.Span, out var packet));
        var read = Assert.IsType<TelemetryActivity>(packet);
        Assert.Equal(activity.Name, read.Name);
        Assert.Equal(activity.SourceName, read.SourceName);
        Assert.Equal(activity.Tags.Count, read.Tags.Count);
        Assert.Equal(tags[0], read.Tags[0]);
        Assert.Equal(tags[1], read.Tags[1]);
        Assert.Equal(tags[2], read.Tags[2]);
    }

    [Fact]
    public void TelemetryPacket_RoundTrips_Metric_Long_And_Double()
    {
        var sessionId = Guid.NewGuid();
        var tags = new[]
        {
            new TelemetryTag("Mode", "Fast"),
        };
        var longMetric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Counter",
            "desc",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(12),
            tags);

        var doubleMetric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Histogram",
            "desc",
            "ms",
            "Histogram`1",
            TelemetryMetricValue.FromDouble(4.5),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();

        var payloadLong = writer.Write(longMetric, 8);
        Assert.True(TelemetryPacketReader.TryRead(payloadLong.Span, out var packetLong));
        var readLong = Assert.IsType<TelemetryMetric>(packetLong);
        Assert.Equal(TelemetryMetricValueType.Long, readLong.Value.Type);
        Assert.Equal(12, readLong.Value.LongValue);

        var payloadDouble = writer.Write(doubleMetric, 8);
        Assert.True(TelemetryPacketReader.TryRead(payloadDouble.Span, out var packetDouble));
        var readDouble = Assert.IsType<TelemetryMetric>(packetDouble);
        Assert.Equal(TelemetryMetricValueType.Double, readDouble.Value.Type);
        Assert.Equal(4.5, readDouble.Value.DoubleValue);
    }

    [Fact]
    public void TelemetryPacket_Metric_Preserves_InstrumentName_With_Whitespace()
    {
        var sessionId = Guid.NewGuid();
        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Requests Total",
            "desc",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(7),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 4);

        Assert.True(TelemetryPacketReader.TryRead(payload.Span, out var packet));
        var read = Assert.IsType<TelemetryMetric>(packet);
        Assert.Equal(metric.InstrumentName, read.InstrumentName);
        Assert.Equal(metric.Description, read.Description);
    }

    [Fact]
    public void TelemetryPacket_Writes_No_Tags_When_MaxTags_Is_Negative()
    {
        var sessionId = Guid.NewGuid();
        var activity = new TelemetryActivity(
            sessionId,
            DateTimeOffset.UtcNow,
            "Source",
            "Activity",
            DateTimeOffset.UtcNow.AddMilliseconds(-10),
            TimeSpan.FromMilliseconds(10),
            new[] { new TelemetryTag("Flag", true) });

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(activity, -1);

        Assert.True(TelemetryPacketReader.TryRead(payload.Span, out var packet));
        var read = Assert.IsType<TelemetryActivity>(packet);
        Assert.Empty(read.Tags);
    }

    [Fact]
    public void TelemetryPacket_InvalidUtf8_In_InstrumentName_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Requests Total",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 0).Span.ToArray();
        CorruptMetricString(payload, 1);

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    [Fact]
    public void TelemetryPacket_InvalidUtf8_In_Description_IsTolerated()
    {
        var sessionId = Guid.NewGuid();
        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "RequestsTotal",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 0).Span.ToArray();
        CorruptMetricString(payload, 2);

        Assert.True(TelemetryPacketReader.TryRead(payload, out var packet));
        var read = Assert.IsType<TelemetryMetric>(packet);
        Assert.Equal(metric.InstrumentName, read.InstrumentName);
        Assert.NotEmpty(read.Description);
    }

    [Fact]
    public void TelemetryPacket_InvalidMetricValueType_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Counter",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 0).Span.ToArray();
        var valueTypeOffset = LocateMetricValueTypeOffset(payload);
        payload[valueTypeOffset] = 0x7F;

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    [Fact]
    public void TelemetryPacket_InvalidTimestamp_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var hello = new TelemetryHello(
            sessionId,
            DateTimeOffset.UtcNow,
            4242,
            "TestHost",
            "TestApp",
            "TestMachine",
            "10.0.0");

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(hello).Span.ToArray();
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(1 + 1 + 16, sizeof(long)), long.MaxValue);

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    [Fact]
    public void TelemetryPacket_InvalidActivityStartTimestamp_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var activity = new TelemetryActivity(
            sessionId,
            DateTimeOffset.UtcNow,
            "Source",
            "Activity",
            DateTimeOffset.UtcNow.AddMilliseconds(-10),
            TimeSpan.FromMilliseconds(10),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(activity, 0).Span.ToArray();
        var startOffset = LocateActivityStartOffset(payload);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(startOffset, sizeof(long)), long.MaxValue);

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    [Fact]
    public void TelemetryPacket_NegativeActivityDuration_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var activity = new TelemetryActivity(
            sessionId,
            DateTimeOffset.UtcNow,
            "Source",
            "Activity",
            DateTimeOffset.UtcNow.AddMilliseconds(-10),
            TimeSpan.FromMilliseconds(10),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(activity, 0).Span.ToArray();
        var startOffset = LocateActivityStartOffset(payload);
        var durationOffset = startOffset + sizeof(long);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(durationOffset, sizeof(long)), -1);

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    [Fact]
    public void TelemetryPacket_MaxActivityDuration_IsAccepted()
    {
        var sessionId = Guid.NewGuid();
        var activity = new TelemetryActivity(
            sessionId,
            DateTimeOffset.UtcNow,
            "Source",
            "Activity",
            DateTimeOffset.UtcNow.AddMilliseconds(-10),
            TimeSpan.FromMilliseconds(10),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(activity, 0).Span.ToArray();
        var startOffset = LocateActivityStartOffset(payload);
        var durationOffset = startOffset + sizeof(long);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(durationOffset, sizeof(long)), TimeSpan.MaxValue.Ticks);

        Assert.True(TelemetryPacketReader.TryRead(payload, out var packet));
        var read = Assert.IsType<TelemetryActivity>(packet);
        Assert.Equal(TimeSpan.MaxValue.Ticks, read.Duration.Ticks);
    }

    [Fact]
    public void TelemetryPacket_TagCount_Above_Max_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Counter",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            Array.Empty<TelemetryTag>());

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 0).Span.ToArray();
        var tagCountOffset = LocateMetricTagCountOffset(payload);
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(tagCountOffset, sizeof(ushort)),
            (ushort)(TelemetryProtocol.MaxTagsPerPacket + 1));

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    [Fact]
    public void TelemetryPacket_TagCount_Is_Clamped_To_Max()
    {
        var sessionId = Guid.NewGuid();
        var tags = new List<TelemetryTag>();
        for (var i = 0; i < TelemetryProtocol.MaxTagsPerPacket + 8; i++)
        {
            tags.Add(new TelemetryTag($"Key{i}", i));
        }

        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Counter",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            tags);

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, TelemetryProtocol.MaxTagsPerPacket + 8);

        Assert.True(TelemetryPacketReader.TryRead(payload.Span, out var packet));
        var read = Assert.IsType<TelemetryMetric>(packet);
        Assert.Equal(TelemetryProtocol.MaxTagsPerPacket, read.Tags.Count);
    }

    [Fact]
    public void TelemetryPacket_InvalidUtf8_In_TagKey_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var metric = new TelemetryMetric(
            sessionId,
            DateTimeOffset.UtcNow,
            "Meter",
            "Counter",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(1),
            new[] { new TelemetryTag("Key", "Value") });

        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 1).Span.ToArray();
        CorruptMetricTagKey(payload);

        Assert.False(TelemetryPacketReader.TryRead(payload, out _));
    }

    private static void CorruptMetricString(Span<byte> payload, int stringIndex)
    {
        var offset = 0;
        offset += 1; // version
        offset += 1; // message type
        offset += 16; // session id
        offset += sizeof(long); // timestamp

        for (var i = 0; i <= stringIndex; i++)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
            offset += sizeof(int);
            if (i == stringIndex)
            {
                if (length >= 2)
                {
                    payload[offset] = 0xC3;
                    payload[offset + 1] = 0x28;
                }
                return;
            }

            offset += length;
        }
    }

    private static int LocateMetricValueTypeOffset(ReadOnlySpan<byte> payload)
    {
        var offset = 0;
        offset += 1; // version
        offset += 1; // message type
        offset += 16; // session id
        offset += sizeof(long); // timestamp

        for (var i = 0; i < 5; i++)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
            offset += sizeof(int) + length;
        }

        return offset;
    }

    private static int LocateMetricTagCountOffset(ReadOnlySpan<byte> payload)
    {
        var valueTypeOffset = LocateMetricValueTypeOffset(payload);
        var valueType = payload[valueTypeOffset];
        var valueSize = valueType == (byte)TelemetryMetricValueType.Double ? sizeof(double) : sizeof(long);
        return valueTypeOffset + 1 + valueSize;
    }

    private static void CorruptMetricTagKey(Span<byte> payload)
    {
        var tagCountOffset = LocateMetricTagCountOffset(payload);
        var offset = tagCountOffset + sizeof(ushort);
        var length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
        offset += sizeof(int);
        if (length >= 2)
        {
            payload[offset] = 0xC3;
            payload[offset + 1] = 0x28;
        }
    }

    private static int LocateActivityStartOffset(ReadOnlySpan<byte> payload)
    {
        var offset = 0;
        offset += 1; // version
        offset += 1; // message type
        offset += 16; // session id
        offset += sizeof(long); // timestamp

        offset = SkipString(payload, offset);
        offset = SkipString(payload, offset);

        return offset;
    }

    private static int SkipString(ReadOnlySpan<byte> payload, int offset)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
        return offset + sizeof(int) + length;
    }
}
