namespace DataGridSample.Models.SourceGenerationAssembly;

public sealed class GeneratedAssemblyRow
{
    public int Sequence { get; set; }

    public string Namespace { get; set; } = string.Empty;

    public string DiscoveryMode { get; set; } = string.Empty;

    public bool ReflectionFree { get; set; }
}
