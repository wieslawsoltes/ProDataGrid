namespace ProDiagnostics.Remote.HostLoad;

internal sealed record HostLoadScenarioResult(
    string Name,
    int Samples,
    int Errors,
    double AvgMs,
    double P95Ms,
    double MaxMs)
{
    public static HostLoadScenarioResult FromSamples(string name, IEnumerable<double> values, int errors)
    {
        var materialized = values.Where(x => x >= 0).ToArray();
        if (materialized.Length == 0)
        {
            return new HostLoadScenarioResult(name, 0, errors, 0, 0, 0);
        }

        Array.Sort(materialized);
        var p95Index = (int)Math.Ceiling(materialized.Length * 0.95) - 1;
        p95Index = Math.Clamp(p95Index, 0, materialized.Length - 1);

        return new HostLoadScenarioResult(
            Name: name,
            Samples: materialized.Length,
            Errors: errors,
            AvgMs: materialized.Average(),
            P95Ms: materialized[p95Index],
            MaxMs: materialized[^1]);
    }
}
