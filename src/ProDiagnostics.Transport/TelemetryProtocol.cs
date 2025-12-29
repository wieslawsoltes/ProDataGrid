namespace ProDiagnostics.Transport;

public static class TelemetryProtocol
{
    public const byte Version = 1;
    public const int DefaultPort = 54831;
    public const int DefaultMaxTags = 32;
    public const int MaxTagsPerPacket = 256;
    public const int MaxPacketBytes = 64 * 1024;
}

public enum TelemetryMessageType : byte
{
    Hello = 1,
    Activity = 2,
    Metric = 3,
}

public enum TelemetryTagValueType : byte
{
    Null = 0,
    String = 1,
    Boolean = 2,
    Int64 = 3,
    Double = 4,
}

public enum TelemetryMetricValueType : byte
{
    Double = 1,
    Long = 2,
}
