namespace ProDiagnostics.Transport;

public readonly record struct TelemetryMetricValue(TelemetryMetricValueType Type, double DoubleValue, long LongValue)
{
    public static TelemetryMetricValue FromDouble(double value)
        => new(TelemetryMetricValueType.Double, value, 0);

    public static TelemetryMetricValue FromLong(long value)
        => new(TelemetryMetricValueType.Long, 0, value);

    public double AsDouble()
        => Type == TelemetryMetricValueType.Double ? DoubleValue : LongValue;
}
