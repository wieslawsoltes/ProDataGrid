namespace ProDiagnostics.Remote.HostLoad;

internal sealed record HostLoadProfile(
    string Name,
    int ControlCount,
    int StreamSessionCount,
    int StreamMessages)
{
    public static HostLoadProfile Small { get; } = new("small", 300, 2, 2_000);
    public static HostLoadProfile Medium { get; } = new("medium", 1_000, 5, 10_000);
    public static HostLoadProfile Large { get; } = new("large", 3_000, 10, 25_000);
    public static HostLoadProfile Stress { get; } = new("stress", 6_000, 20, 75_000);

    public static HostLoadProfile FromName(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "small" => Small,
            "medium" => Medium,
            "large" => Large,
            "stress" => Stress,
            _ => Small,
        };
    }
}
