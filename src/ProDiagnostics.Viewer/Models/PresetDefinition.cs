using System;
using System.Collections.Generic;

namespace ProDiagnostics.Viewer.Models;

public sealed class PresetDefinition
{
    public string Name { get; set; } = "Custom";
    public string Description { get; set; } = string.Empty;
    public string[] IncludeActivities { get; set; } = Array.Empty<string>();
    public string[] IncludeMetrics { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> ActivityAliases { get; set; } = new();
    public Dictionary<string, string> MetricAliases { get; set; } = new();
}
