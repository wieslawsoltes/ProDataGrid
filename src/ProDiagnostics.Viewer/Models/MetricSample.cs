using System;

namespace ProDiagnostics.Viewer.Models;

public readonly record struct MetricSample(DateTimeOffset Timestamp, double Value);
