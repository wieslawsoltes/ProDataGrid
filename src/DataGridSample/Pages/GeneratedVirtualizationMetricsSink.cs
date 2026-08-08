// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Controls;

namespace DataGridSample.Pages;

public sealed class GeneratedVirtualizationMetricsSink : IDataGridGeneratedMetricsSink
{
    private long _measurementCount;
    private int _disposed;

    public long MeasurementCount => Interlocked.Read(ref _measurementCount);

    public string? LastMetricName { get; private set; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Record(
        in DataGridGeneratedMetricMeasurement measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        _ = tags;
        LastMetricName = measurement.Name;
        Interlocked.Increment(ref _measurementCount);
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
