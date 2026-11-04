// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace Avalonia.Controls;

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
partial class DataGrid
{
    /// <summary>
    /// Identifies the <see cref="RealizationFactory"/> direct property.
    /// </summary>
    public static readonly DirectProperty<DataGrid, DataGridRealizationFactory> RealizationFactoryProperty =
        AvaloniaProperty.RegisterDirect<DataGrid, DataGridRealizationFactory>(
            nameof(RealizationFactory),
            grid => grid.RealizationFactory,
            (grid, value) => grid.RealizationFactory = value);

    private DataGridRealizationFactory _realizationFactory = DataGridRealizationFactory.Default;

    /// <summary>
    /// Gets or sets the factory used to create row, cell, and column-header containers.
    /// </summary>
    /// <remarks>
    /// Changing the factory invalidates cached headers and realized or recycled row/cell
    /// containers so instances created by different factories cannot share a pool.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// The assigned value is <see langword="null"/>.
    /// </exception>
    public DataGridRealizationFactory RealizationFactory
    {
        get => _realizationFactory;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!SetAndRaise(RealizationFactoryProperty, ref _realizationFactory, value))
            {
                return;
            }

            OnRealizationFactoryChanged();
        }
    }

    internal bool UsesDefaultRealizationFactory =>
        ReferenceEquals(_realizationFactory, DataGridRealizationFactory.Default);

    internal DataGridRow CreateRowContainer(object? dataContext, int rowIndex, int slot)
    {
        if (UsesDefaultRealizationFactory)
        {
            return new DataGridRow();
        }

        var context = new DataGridRowRealizationContext(this, dataContext, rowIndex, slot);
        return _realizationFactory.CreateRow(context) ??
            throw new InvalidOperationException("The realization factory returned a null row container.");
    }

    internal object? GetRowRecyclingKey(object? dataContext, int rowIndex, int slot) =>
        _realizationFactory.GetRowRecyclingKey(
            new DataGridRowRealizationContext(this, dataContext, rowIndex, slot));

    internal object? GetRowRecyclingKey(DataGridRow row) =>
        _realizationFactory.GetRowRecyclingKey(row);

    private DataGridCell CreateCellContainer(DataGridRow row, DataGridColumn column)
    {
        if (UsesDefaultRealizationFactory)
        {
            return column.CreateCell();
        }

        var context = new DataGridCellRealizationContext(this, row, column);
        return _realizationFactory.CreateCell(context) ??
            throw new InvalidOperationException("The realization factory returned a null cell container.");
    }

    private bool CanReuseCellContainer(DataGridRow row, DataGridColumn column, DataGridCell cell)
    {
        if (UsesDefaultRealizationFactory)
        {
            return true;
        }

        var context = new DataGridCellRealizationContext(this, row, column);
        object? requestedKey = _realizationFactory.GetCellRecyclingKey(context);
        object? elementKey = _realizationFactory.GetCellRecyclingKey(cell);
        return requestedKey is not null &&
               elementKey is not null &&
               EqualityComparer<object>.Default.Equals(requestedKey, elementKey);
    }

    internal DataGridColumnHeader CreateColumnHeaderContainer(DataGridColumn column)
    {
        if (UsesDefaultRealizationFactory)
        {
            return column.CreateHeader();
        }

        var context = new DataGridColumnHeaderRealizationContext(this, column);
        DataGridColumnHeader header = _realizationFactory.CreateColumnHeader(context) ??
            throw new InvalidOperationException("The realization factory returned a null column-header container.");
        column.PrepareHeaderContainer(header);
        if (column is DataGridFillerColumn)
        {
            header.IsEnabled = false;
        }
        return header;
    }

    internal bool CanReuseColumnHeaderContainer(DataGridColumn column, DataGridColumnHeader header)
    {
        if (UsesDefaultRealizationFactory)
        {
            return true;
        }

        var context = new DataGridColumnHeaderRealizationContext(this, column);
        object? requestedKey = _realizationFactory.GetColumnHeaderRecyclingKey(context);
        object? elementKey = _realizationFactory.GetColumnHeaderRecyclingKey(header);
        return requestedKey is not null &&
               elementKey is not null &&
               EqualityComparer<object>.Default.Equals(requestedKey, elementKey);
    }

    internal void ReplaceDisplayedColumnHeader(
        DataGridColumnHeader previousHeader,
        DataGridColumnHeader replacementHeader)
    {
        if (_columnHeadersPresenter == null)
        {
            return;
        }

        int index = _columnHeadersPresenter.Children.IndexOf(previousHeader);
        if (index < 0)
        {
            return;
        }

        _columnHeadersPresenter.Children.RemoveAt(index);
        _columnHeadersPresenter.Children.Insert(index, replacementHeader);
    }

    private void OnRealizationFactoryChanged()
    {
        RemoveDisplayedColumnHeaders();
        foreach (DataGridColumn column in ColumnsItemsInternal)
        {
            column.ClearElementCache();
        }
        ColumnsInternal.FillerColumn.ClearElementCache();

        if (!_measured)
        {
            DisplayData.ClearRecyclePools();
            return;
        }

        UnloadElements(recycle: false);
        RefreshRowsAndColumns(clearRows: false);
        RefreshSelectionFromModel();
        EnsureColumnHeadersPresenterChildren();
        InvalidateColumnHeadersMeasure();
        InvalidateMeasure();
    }

    internal void DiscardUnkeyedRecycledRow(DataGridRow row)
    {
        _rowsPresenter?.UnregisterAnchorCandidate(row);
        _rowsPresenter?.RemoveTrackedChild(row);
    }
}
