// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Provides property notification, invalidation batching, and change helpers for DataGrid layout models.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
abstract class DataGridLayoutModelBase : IDataGridLayoutModel
{
    private int _updateNesting;
    private DataGridLayoutInvalidationKind? _pendingInvalidation;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public event EventHandler<DataGridLayoutInvalidatedEventArgs>? LayoutInvalidated;

    /// <inheritdoc/>
    public abstract IDataGridLayoutAlgorithm CreateAlgorithm();

    /// <summary>
    /// Defers layout invalidation until the returned scope is disposed.
    /// </summary>
    /// <returns>A scope that flushes the strongest pending invalidation when disposed.</returns>
    public IDisposable DeferInvalidation()
    {
        _updateNesting++;
        return new InvalidationScope(this);
    }

    /// <summary>
    /// Sets a property value and raises model notifications when the value changed.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="invalidationKind">The layout work required by the change.</param>
    /// <param name="propertyName">The property name supplied by the compiler.</param>
    /// <returns><c>true</c> when the value changed.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        DataGridLayoutInvalidationKind invalidationKind = DataGridLayoutInvalidationKind.Measure,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Invalidate(invalidationKind);
        return true;
    }

    /// <summary>
    /// Raises a layout invalidation request.
    /// </summary>
    /// <param name="kind">The required invalidation kind.</param>
    protected void Invalidate(DataGridLayoutInvalidationKind kind)
    {
        if (_updateNesting > 0)
        {
            if (!_pendingInvalidation.HasValue || kind > _pendingInvalidation.Value)
            {
                _pendingInvalidation = kind;
            }
            return;
        }

        LayoutInvalidated?.Invoke(this, new DataGridLayoutInvalidatedEventArgs(kind));
    }

    private void EndInvalidationScope()
    {
        if (_updateNesting == 0)
        {
            return;
        }

        _updateNesting--;
        if (_updateNesting == 0 && _pendingInvalidation is { } kind)
        {
            _pendingInvalidation = null;
            LayoutInvalidated?.Invoke(this, new DataGridLayoutInvalidatedEventArgs(kind));
        }
    }

    private sealed class InvalidationScope : IDisposable
    {
        private DataGridLayoutModelBase? _owner;

        public InvalidationScope(DataGridLayoutModelBase owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            DataGridLayoutModelBase? owner = _owner;
            _owner = null;
            owner?.EndInvalidationScope();
        }
    }
}
