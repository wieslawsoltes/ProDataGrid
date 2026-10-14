// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
#nullable enable
using System;
using System.Collections.Generic;

namespace ProCharts;

/// <summary>Non-owning, disposable series-visibility adapter. Series indices refer to the upstream schema.</summary>
/// <remarks>
/// Configure on the same consumer thread as the ChartModel. Upstream invalidations are forwarded on their
/// original thread; use CoalescingChartDataSource before this adapter for worker producers. A series is
/// identified by its index, not its display name. Call ResetVisibility after changing the source schema.
/// Immutable snapshots and channel arrays are shared, not copied. Explicit series styles pin colors.
/// Typed hierarchies retain their hierarchy metadata and are shown/hidden as one series.
/// </remarks>
public sealed class SeriesVisibilityChartDataSource : IChartDataSource, IChartWindowInfoProvider, IDisposable
{
    private readonly IChartDataSource _source;
    private readonly HashSet<int> _hidden = new();
    private ChartDataSnapshot? _input;
    private ChartDataSnapshot? _output;
    private bool _disposed;
    private int _version;

    /// <summary>Wraps a source without taking ownership of it.</summary>
    public SeriesVisibilityChartDataSource(IChartDataSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.DataInvalidated += OnInvalidated;
    }

    /// <inheritdoc />
    public event EventHandler? DataInvalidated;
    /// <summary>The most recently requested upstream snapshot, including hidden series.</summary>
    public ChartDataSnapshot UnfilteredSnapshot => _input ?? ChartDataSnapshot.Empty;
    /// <summary>Maps displayed series indices back to upstream indices. Replaced, never mutated.</summary>
    public IReadOnlyList<int> OriginalSeriesIndices { get; private set; } = Array.Empty<int>();
    /// <summary>Gets whether an upstream series is enabled. Indices must be non-negative.</summary>
    public bool IsSeriesVisible(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        return !_hidden.Contains(index);
    }
    /// <summary>Changes visibility and notifies only when the value actually changes.</summary>
    public void SetSeriesVisible(int index, bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        bool changed = visible ? _hidden.Remove(index) : _hidden.Add(index);
        if (!changed) return;
        _output = null;
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }
    /// <summary>Shows every series, clearing visibility carried over from a previous schema.</summary>
    public void ResetVisibility()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hidden.Count == 0) return;
        _hidden.Clear();
        _output = null;
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }
    /// <inheritdoc />
    public int? GetTotalCategoryCount() => (_source as IChartWindowInfoProvider)?.GetTotalCategoryCount()
        ?? _input?.Categories.Count;
    /// <inheritdoc />
    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        ChartDataSnapshot input = _source.BuildSnapshot(request);
        if (ReferenceEquals(input, _input) && _output != null) return _output;
        _input = input;
        List<int> indices = new(input.Series.Count);
        List<ChartSeriesSnapshot> visible = new(input.Series.Count);
        for (int i = 0; i < input.Series.Count; i++)
        {
            if (_hidden.Contains(i)) continue;
            indices.Add(i);
            visible.Add(input.Series[i]);
        }
        OriginalSeriesIndices = indices.AsReadOnly();
        if (visible.Count == input.Series.Count) return _output = input;
        // A typed hierarchy has exactly one series. Keeping the original snapshot above preserves it.
        return _output = new ChartDataSnapshot(input.Categories, visible.AsReadOnly(), unchecked(++_version));
    }
    private void OnInvalidated(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _output = null;
        DataInvalidated?.Invoke(this, EventArgs.Empty);
    }
    /// <summary>Unsubscribes from the upstream source; the source itself remains usable.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.DataInvalidated -= OnInvalidated;
        _input = _output = null;
        OriginalSeriesIndices = Array.Empty<int>();
        DataInvalidated = null;
    }
}
