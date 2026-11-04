// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia.Controls.DataGridHierarchical;

namespace Avalonia.Controls;

/// <summary>
/// Creates the outer row, cell, and column-header containers realized by a <see cref="DataGrid"/>.
/// </summary>
/// <remarks>
/// Columns continue to own display content, editors, value access, validation, and semantic data
/// operations. A realization factory replaces only the surrounding virtualized containers.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
class DataGridRealizationFactory
{
    /// <summary>
    /// Gets the shared factory that creates the standard ProDataGrid containers.
    /// </summary>
    public static DataGridRealizationFactory Default { get; } = new();

    /// <summary>
    /// Creates a row container for the supplied realization context.
    /// </summary>
    /// <param name="context">The row realization request.</param>
    /// <returns>A row container owned and prepared by the requesting grid.</returns>
    public virtual DataGridRow CreateRow(DataGridRowRealizationContext context) =>
        context.CreateDefaultContainer();

    /// <summary>
    /// Gets the recycling key required by a row realization request.
    /// Return <see langword="null"/> to disable row recycling for the request.
    /// </summary>
    /// <param name="context">The row realization request.</param>
    /// <returns>The requested row recycling key, or <see langword="null"/>.</returns>
    public virtual object? GetRowRecyclingKey(DataGridRowRealizationContext context) =>
        typeof(DataGridRow);

    /// <summary>
    /// Gets the recycling key supplied by an existing row container.
    /// Return <see langword="null"/> to prevent the row from entering a recycle pool.
    /// </summary>
    /// <param name="row">The row being considered for recycling.</param>
    /// <returns>The row's recycling key, or <see langword="null"/>.</returns>
    public virtual object? GetRowRecyclingKey(DataGridRow row) => row.GetType();

    /// <summary>
    /// Creates a cell container for the supplied realization context.
    /// </summary>
    /// <param name="context">The cell realization request.</param>
    /// <returns>A cell container owned and prepared by the requesting grid.</returns>
    public virtual DataGridCell CreateCell(DataGridCellRealizationContext context) =>
        context.CreateDefaultContainer();

    /// <summary>
    /// Gets the recycling key required by a cell realization request.
    /// Return <see langword="null"/> when the cell must be recreated as its row is reused.
    /// </summary>
    /// <param name="context">The cell realization request.</param>
    /// <returns>The requested cell recycling key, or <see langword="null"/>.</returns>
    public virtual object? GetCellRecyclingKey(DataGridCellRealizationContext context) =>
        context.Column.GetType();

    /// <summary>
    /// Gets the recycling key supplied by an existing cell container.
    /// Return <see langword="null"/> to prevent reuse with another row assignment.
    /// </summary>
    /// <param name="cell">The cell being considered for reuse.</param>
    /// <returns>The cell's recycling key, or <see langword="null"/>.</returns>
    public virtual object? GetCellRecyclingKey(DataGridCell cell) =>
        cell.OwningColumn?.GetType();

    /// <summary>
    /// Creates a column-header container for the supplied realization context.
    /// </summary>
    /// <param name="context">The column-header realization request.</param>
    /// <returns>A column-header container owned and prepared by the requesting grid.</returns>
    public virtual DataGridColumnHeader CreateColumnHeader(DataGridColumnHeaderRealizationContext context) =>
        context.CreateDefaultContainer();

    /// <summary>
    /// Gets the recycling key required by a column-header realization request.
    /// Return <see langword="null"/> when the cached header must be recreated.
    /// </summary>
    /// <param name="context">The column-header realization request.</param>
    /// <returns>The requested column-header recycling key, or <see langword="null"/>.</returns>
    public virtual object? GetColumnHeaderRecyclingKey(DataGridColumnHeaderRealizationContext context) =>
        context.Column.GetType();

    /// <summary>
    /// Gets the recycling key supplied by an existing column-header container.
    /// Return <see langword="null"/> to prevent reuse for a changed column context.
    /// </summary>
    /// <param name="header">The column header being considered for reuse.</param>
    /// <returns>The column header's recycling key, or <see langword="null"/>.</returns>
    public virtual object? GetColumnHeaderRecyclingKey(DataGridColumnHeader header) =>
        header.OwningColumn?.GetType();
}

/// <summary>
/// Describes a row container realization request.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
readonly record struct DataGridRowRealizationContext
{
    internal DataGridRowRealizationContext(DataGrid grid, object? dataContext, int rowIndex, int slot)
    {
        Grid = grid;
        DataContext = dataContext;
        RowIndex = rowIndex;
        Slot = slot;
        HierarchicalNode = dataContext as HierarchicalNode;
    }

    /// <summary>Gets the grid requesting the container.</summary>
    public DataGrid Grid { get; }

    /// <summary>Gets the actual row data context used by the grid.</summary>
    public object? DataContext { get; }

    /// <summary>Gets the application item represented by the row.</summary>
    public object? Item => HierarchicalNode?.Item ?? DataContext;

    /// <summary>Gets hierarchy metadata when hierarchical rows are enabled.</summary>
    public HierarchicalNode? HierarchicalNode { get; }

    /// <summary>Gets the flattened row index.</summary>
    public int RowIndex { get; }

    /// <summary>Gets the grid slot assigned to the row.</summary>
    public int Slot { get; }

    /// <summary>Creates the standard row container.</summary>
    /// <returns>A new standard row container.</returns>
    public DataGridRow CreateDefaultContainer() => new();
}

/// <summary>
/// Describes a cell container realization request.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
readonly record struct DataGridCellRealizationContext
{
    internal DataGridCellRealizationContext(DataGrid grid, DataGridRow row, DataGridColumn column)
    {
        Grid = grid;
        Row = row;
        Column = column;
        DataContext = row.DataContext!;
        HierarchicalNode = row.DataContext as HierarchicalNode;
    }

    /// <summary>Gets the grid requesting the container.</summary>
    public DataGrid Grid { get; }

    /// <summary>Gets the row that will own the cell.</summary>
    public DataGridRow Row { get; }

    /// <summary>Gets the column that will own the cell.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Gets the actual row data context used by the grid.</summary>
    public object? DataContext { get; }

    /// <summary>Gets the application item represented by the row.</summary>
    public object? Item => HierarchicalNode?.Item ?? DataContext;

    /// <summary>Gets hierarchy metadata when hierarchical rows are enabled.</summary>
    public HierarchicalNode? HierarchicalNode { get; }

    /// <summary>Gets the flattened row index.</summary>
    public int RowIndex => Row.Index;

    /// <summary>Creates the standard container selected by the owning column.</summary>
    /// <returns>A new standard cell container.</returns>
    public DataGridCell CreateDefaultContainer() => Column.CreateCell();
}

/// <summary>
/// Describes a column-header container realization request.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
readonly record struct DataGridColumnHeaderRealizationContext
{
    internal DataGridColumnHeaderRealizationContext(DataGrid grid, DataGridColumn column)
    {
        Grid = grid;
        Column = column;
    }

    /// <summary>Gets the grid requesting the container.</summary>
    public DataGrid Grid { get; }

    /// <summary>Gets the column that will own the header.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Gets whether the column is in the left frozen region.</summary>
    public bool IsFrozenLeft => Column.IsFrozenLeft;

    /// <summary>Gets whether the column is in the right frozen region.</summary>
    public bool IsFrozenRight => Column.IsFrozenRight;

    /// <summary>Creates the standard column-header container.</summary>
    /// <returns>A new standard column-header container.</returns>
    public DataGridColumnHeader CreateDefaultContainer() => new();
}
