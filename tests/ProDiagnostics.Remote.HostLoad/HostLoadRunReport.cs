namespace ProDiagnostics.Remote.HostLoad;

internal sealed record HostLoadRunReport(
    string RunId,
    string Profile,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    int RootNodeCount,
    IReadOnlyList<HostLoadScenarioResult> Scenarios,
    IReadOnlyDictionary<string, double> MetricTotals);
