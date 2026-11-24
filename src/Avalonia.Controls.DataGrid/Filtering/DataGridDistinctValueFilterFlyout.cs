// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Avalonia.Controls.DataGridFiltering;

/// <summary>
/// A column filter flyout that lists distinct values, row counts, and a substring search box.
/// </summary>
/// <remarks>
/// The target column must expose an <see cref="IDataGridColumnValueAccessor"/> through
/// <see cref="DataGridColumnFilter.ValueAccessorProperty"/> or <see cref="DataGridColumnMetadata.ValueAccessorProperty"/>.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
class DataGridDistinctValueFilterFlyout : Flyout
{
    private DataGridDistinctValueFilterContext? _context;
    private IFilteringModel? _contextModel;
    private object? _contextColumnId;
    private IDataGridColumnValueAccessor? _contextAccessor;
    private System.Collections.IEqualityComparer? _contextComparer;
    private Func<object?, string>? _contextFormatter;

    /// <summary>
    /// Gets or sets an optional reflection-free value accessor that overrides the accessor registered on the column.
    /// </summary>
    public IDataGridColumnValueAccessor? ValueAccessor { get; set; }

    /// <summary>
    /// Gets or sets an optional comparer used to group values and restore selected values.
    /// </summary>
    public System.Collections.IEqualityComparer? ValueComparer { get; set; }

    /// <summary>
    /// Gets or sets an optional formatter for values shown in the popup.
    /// </summary>
    public Func<object?, string>? DisplayFormatter { get; set; }

    /// <summary>
    /// Gets the active filter context after the flyout has opened successfully.
    /// </summary>
    public DataGridDistinctValueFilterContext? Context => _context;

    /// <summary>
    /// Gets an explanation of the last initialization failure, or <see langword="null"/> when initialization succeeded.
    /// </summary>
    public string? LastError { get; private set; }

    /// <inheritdoc/>
    protected override void OnOpening(CancelEventArgs args)
    {
        DataGridColumnHeader? header = FindColumnHeader(Target);
        DataGridColumn? column = header?.OwningColumn;
        DataGrid? grid = column?.OwningGrid;
        if (column == null || grid == null)
        {
            LastError = "The distinct-value filter must be opened from a DataGrid column header.";
            args.Cancel = true;
            base.OnOpening(args);
            return;
        }

        IDataGridColumnValueAccessor? accessor = ValueAccessor ??
            DataGridColumnFilter.GetValueAccessor(column) ??
            DataGridColumnMetadata.ResolveValueAccessor(column);
        if (accessor == null)
        {
            LastError = "The column must provide an IDataGridColumnValueAccessor for reflection-free distinct-value filtering.";
            args.Cancel = true;
            base.OnOpening(args);
            return;
        }

        object columnId = DataGridColumnMetadata.GetColumnId(column);
        IFilteringModel filteringModel = grid.FilteringModel;
        if (_context == null ||
            !ReferenceEquals(_contextModel, filteringModel) ||
            !Equals(_contextColumnId, columnId) ||
            !ReferenceEquals(_contextAccessor, accessor) ||
            !ReferenceEquals(_contextComparer, ValueComparer) ||
            !ReferenceEquals(_contextFormatter, DisplayFormatter))
        {
            string label = Convert.ToString(column.Header) ?? "Values";
            _context = new DataGridDistinctValueFilterContext(
                filteringModel,
                columnId,
                accessor,
                label,
                column.GetSortPropertyName(),
                ValueComparer,
                DisplayFormatter);
            _contextModel = filteringModel;
            _contextColumnId = columnId;
            _contextAccessor = accessor;
            _contextComparer = ValueComparer;
            _contextFormatter = DisplayFormatter;
        }

        IEnumerable? source = grid.ItemsSource;
        if (source is IDataGridCollectionView collectionView)
        {
            source = collectionView.SourceCollection;
        }

        _context.Refresh(source);
        Content = _context;
        ResolveThemeResources(grid);
        LastError = null;
        base.OnOpening(args);
    }

    private void ResolveThemeResources(DataGrid grid)
    {
        if (ContentTemplate == null &&
            grid.TryFindResource("DataGridFilterDistinctValuesEditorTemplate", out object? templateResource) &&
            templateResource is IDataTemplate template)
        {
            ContentTemplate = template;
        }

        if (FlyoutPresenterTheme == null &&
            grid.TryFindResource("DataGridFilterFlyoutPresenterTheme", out object? themeResource) &&
            themeResource is ControlTheme theme)
        {
            FlyoutPresenterTheme = theme;
        }
    }

    private static DataGridColumnHeader? FindColumnHeader(Control? target)
    {
        Visual? current = target;
        while (current != null)
        {
            if (current is DataGridColumnHeader header)
            {
                return header;
            }

            current = current.GetVisualParent();
        }

        return null;
    }
}
