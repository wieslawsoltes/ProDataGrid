// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Collections;
using Avalonia.Layout;
using Avalonia.Styling;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {
        private DataGridSummaryService _summaryService;
        private DataGridSummaryRow _totalSummaryRow;
        private bool _showTotalSummary;
        private bool _showGroupSummary;
        private DataGridSummaryRowPosition _totalSummaryPosition = DataGridSummaryRowPosition.Bottom;
        private DataGridGroupSummaryPosition _groupSummaryPosition = DataGridGroupSummaryPosition.Footer;
        private ControlTheme _summaryRowTheme;
        private ControlTheme _summaryCellTheme;
        private HorizontalAlignment? _summaryCellHorizontalContentAlignment;
        private VerticalAlignment? _summaryCellVerticalContentAlignment;
        private int _summaryRecalculationDelayMs = 100;

        /// <summary>
        /// Identifies the <see cref="ShowTotalSummary"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, bool> ShowTotalSummaryProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, bool>(
                nameof(ShowTotalSummary),
                o => o.ShowTotalSummary,
                (o, v) => o.ShowTotalSummary = v);

        /// <summary>
        /// Identifies the <see cref="ShowGroupSummary"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, bool> ShowGroupSummaryProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, bool>(
                nameof(ShowGroupSummary),
                o => o.ShowGroupSummary,
                (o, v) => o.ShowGroupSummary = v);

        /// <summary>
        /// Identifies the <see cref="TotalSummaryPosition"/> property.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly DirectProperty<DataGrid, DataGridSummaryRowPosition> TotalSummaryPositionProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, DataGridSummaryRowPosition>(
                nameof(TotalSummaryPosition),
                o => o.TotalSummaryPosition,
                (o, v) => o.TotalSummaryPosition = v);

        /// <summary>
        /// Identifies the <see cref="GroupSummaryPosition"/> property.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly DirectProperty<DataGrid, DataGridGroupSummaryPosition> GroupSummaryPositionProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, DataGridGroupSummaryPosition>(
                nameof(GroupSummaryPosition),
                o => o.GroupSummaryPosition,
                (o, v) => o.GroupSummaryPosition = v);

        /// <summary>
        /// Identifies the <see cref="SummaryRowTheme"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, ControlTheme> SummaryRowThemeProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, ControlTheme>(
                nameof(SummaryRowTheme),
                o => o.SummaryRowTheme,
                (o, v) => o.SummaryRowTheme = v);

        /// <summary>
        /// Identifies the <see cref="SummaryCellTheme"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, ControlTheme> SummaryCellThemeProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, ControlTheme>(
                nameof(SummaryCellTheme),
                o => o.SummaryCellTheme,
                (o, v) => o.SummaryCellTheme = v);

        /// <summary>
        /// Identifies the <see cref="SummaryCellHorizontalContentAlignment"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, HorizontalAlignment?> SummaryCellHorizontalContentAlignmentProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, HorizontalAlignment?>(
                nameof(SummaryCellHorizontalContentAlignment),
                o => o.SummaryCellHorizontalContentAlignment,
                (o, v) => o.SummaryCellHorizontalContentAlignment = v);

        /// <summary>
        /// Identifies the <see cref="SummaryCellVerticalContentAlignment"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, VerticalAlignment?> SummaryCellVerticalContentAlignmentProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, VerticalAlignment?>(
                nameof(SummaryCellVerticalContentAlignment),
                o => o.SummaryCellVerticalContentAlignment,
                (o, v) => o.SummaryCellVerticalContentAlignment = v);

        /// <summary>
        /// Identifies the <see cref="SummaryRecalculationDelayMs"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, int> SummaryRecalculationDelayMsProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, int>(
                nameof(SummaryRecalculationDelayMs),
                o => o.SummaryRecalculationDelayMs,
                (o, v) => o.SummaryRecalculationDelayMs = v);

        /// <summary>
        /// Gets or sets whether to show the total summary row.
        /// </summary>
        public bool ShowTotalSummary
        {
            get => _showTotalSummary;
            set
            {
                if (SetAndRaise(ShowTotalSummaryProperty, ref _showTotalSummary, value))
                {
                    OnShowTotalSummaryChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show group summary rows.
        /// </summary>
        public bool ShowGroupSummary
        {
            get => _showGroupSummary;
            set
            {
                if (SetAndRaise(ShowGroupSummaryProperty, ref _showGroupSummary, value))
                {
                    OnShowGroupSummaryChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the position of the total summary row.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridSummaryRowPosition TotalSummaryPosition
        {
            get => _totalSummaryPosition;
            set
            {
                if (SetAndRaise(TotalSummaryPositionProperty, ref _totalSummaryPosition, value))
                {
                    OnTotalSummaryPositionChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the position of group summary rows.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridGroupSummaryPosition GroupSummaryPosition
        {
            get => _groupSummaryPosition;
            set
            {
                if (SetAndRaise(GroupSummaryPositionProperty, ref _groupSummaryPosition, value))
                {
                    OnGroupSummaryPositionChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the theme for summary rows.
        /// </summary>
        public ControlTheme SummaryRowTheme
        {
            get => _summaryRowTheme;
            set
            {
                if (SetAndRaise(SummaryRowThemeProperty, ref _summaryRowTheme, value))
                {
                    OnSummaryRowThemeChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the theme for summary cells.
        /// </summary>
        public ControlTheme SummaryCellTheme
        {
            get => _summaryCellTheme;
            set
            {
                if (SetAndRaise(SummaryCellThemeProperty, ref _summaryCellTheme, value))
                {
                    OnSummaryCellThemeChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the default horizontal alignment for summary cell content.
        /// </summary>
        public HorizontalAlignment? SummaryCellHorizontalContentAlignment
        {
            get => _summaryCellHorizontalContentAlignment;
            set
            {
                if (SetAndRaise(SummaryCellHorizontalContentAlignmentProperty, ref _summaryCellHorizontalContentAlignment, value))
                {
                    OnSummaryCellAlignmentChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the default vertical alignment for summary cell content.
        /// </summary>
        public VerticalAlignment? SummaryCellVerticalContentAlignment
        {
            get => _summaryCellVerticalContentAlignment;
            set
            {
                if (SetAndRaise(SummaryCellVerticalContentAlignmentProperty, ref _summaryCellVerticalContentAlignment, value))
                {
                    OnSummaryCellAlignmentChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the debounce delay for summary recalculations in milliseconds.
        /// </summary>
        public int SummaryRecalculationDelayMs
        {
            get => _summaryRecalculationDelayMs;
            set
            {
                if (SetAndRaise(SummaryRecalculationDelayMsProperty, ref _summaryRecalculationDelayMs, value))
                {
                    if (_summaryService != null)
                    {
                        _summaryService.DebounceDelayMs = value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the total summary row instance.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridSummaryRow TotalSummaryRow => _totalSummaryRow;

        /// <summary>
        /// Gets the summary service.
        /// </summary>
        internal DataGridSummaryService SummaryService => _summaryService;

        /// <summary>
        /// Event raised when summary values are recalculated.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridSummaryRecalculatedEventArgs> SummaryRecalculated;

        /// <summary>
        /// Initializes the summary service.
        /// </summary>
        private void InitializeSummaryService()
        {
            _summaryService = new DataGridSummaryService(this);
            _summaryService.DebounceDelayMs = _summaryRecalculationDelayMs;
            _summaryService.SummaryRecalculated += OnSummaryServiceRecalculated;
        }

        /// <summary>
        /// Disposes the summary service.
        /// </summary>
        private void DisposeSummaryService()
        {
            if (_summaryService != null)
            {
                _summaryService.SummaryRecalculated -= OnSummaryServiceRecalculated;
                _summaryService.Dispose();
                _summaryService = null;
            }
        }

        private void OnSummaryServiceRecalculated(object sender, DataGridSummaryRecalculatedEventArgs e)
        {
            // Update the UI
            if (e.Scope == DataGridSummaryScope.Total || e.Scope == DataGridSummaryScope.Both)
            {
                _totalSummaryRow?.Recalculate();
            }

            if (e.Scope == DataGridSummaryScope.Group || e.Scope == DataGridSummaryScope.Both)
            {
                UpdateGroupSummaryRowState(e.Group);
            }

            // Raise the public event
            SummaryRecalculated?.Invoke(this, e);
        }

        private void OnShowTotalSummaryChanged()
        {
            if (_showTotalSummary)
            {
                EnsureTotalSummaryRow();
                if (_totalSummaryRow != null)
                {
                    _totalSummaryRow.IsVisible = true;
                }
                InvalidateSummaries();
            }
            else if (_totalSummaryRow != null)
            {
                _totalSummaryRow.IsVisible = false;
            }

            InvalidateMeasure();
        }

        private void OnShowGroupSummaryChanged()
        {
            if (DataConnection?.CollectionView?.IsGrouping == true)
            {
                RefreshGroupSummarySlots();
            }

            UpdateGroupSummaryRowState(null);
            InvalidateSummaries();
        }

        private void OnTotalSummaryPositionChanged()
        {
            UpdatePseudoClasses();
            if (_showTotalSummary)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        private void OnGroupSummaryPositionChanged()
        {
            if (DataConnection?.CollectionView?.IsGrouping == true)
            {
                RefreshGroupSummarySlots();
            }

            UpdateGroupSummaryRowState(null);
        }

        /// <summary>
        /// Sets up the total summary row from the template.
        /// </summary>
        private void SetupTotalSummaryRow(INameScope nameScope)
        {
            _totalSummaryRow = nameScope.Find<DataGridSummaryRow>(DATAGRID_elementTotalSummaryRowName);
            
            if (_totalSummaryRow != null)
            {
                _totalSummaryRow.OwningGrid = this;
                _totalSummaryRow.Scope = DataGridSummaryScope.Total;
                
                ApplyTotalSummaryRowTheme();

                // Apply current visibility state
                _totalSummaryRow.IsVisible = _showTotalSummary;
                
                // Force cell creation since columns may already exist
                _totalSummaryRow.EnsureCells();
                
                // Trigger initial calculation if needed
                if (_showTotalSummary)
                {
                    InvalidateSummaries();
                }
            }
        }

        /// <summary>
        /// Ensures the total summary row is configured (for programmatic use).
        /// </summary>
        private void EnsureTotalSummaryRow()
        {
            // If we have the row from template, just configure it
            if (_totalSummaryRow != null)
            {
                _totalSummaryRow.OwningGrid = this;
                _totalSummaryRow.Scope = DataGridSummaryScope.Total;

                ApplyTotalSummaryRowTheme();
                _totalSummaryRow.IsVisible = _showTotalSummary;
                _totalSummaryRow.EnsureCells();
            }
        }

        private void ApplyTotalSummaryRowTheme()
        {
            if (_totalSummaryRow == null)
            {
                return;
            }

            if (_summaryRowTheme != null)
            {
                _totalSummaryRow.Theme = _summaryRowTheme;
            }
            else
            {
                _totalSummaryRow.ClearValue(ThemeProperty);
            }
        }

        private void OnSummaryRowThemeChanged()
        {
            ApplyTotalSummaryRowTheme();
            UpdateGroupSummaryRowTheme();
        }

        private void OnSummaryCellThemeChanged()
        {
            UpdateSummaryCellAppearance();
        }

        private void OnSummaryCellAlignmentChanged()
        {
            UpdateSummaryCellAppearance();
        }

        private void RefreshGroupSummarySlots()
        {
            if (DataConnection?.CollectionView?.IsGrouping != true)
            {
                return;
            }

            var collapsedGroupsCache = RowGroupHeadersTable
                .Where(g => !g.Value.IsVisible)
                .Select(g => g.Value.CollectionViewGroup.Key)
                .ToArray();

            RefreshRowsAndColumns(clearRows: true);

            foreach (var groupKey in collapsedGroupsCache)
            {
                var item = RowGroupHeadersTable.FirstOrDefault(t => t.Value.CollectionViewGroup.Parent.GroupBy.KeysMatch(t.Value.CollectionViewGroup.Key, groupKey));
                if (item != null)
                {
                    EnsureRowGroupVisibility(item.Value, false, false);
                }
            }
        }

        private void UpdateGroupSummaryRowState(DataGridCollectionViewGroup group)
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    if (group == null || groupHeader.RowGroupInfo?.CollectionViewGroup == group)
                    {
                        groupHeader.UpdateSummaryRowState();
                    }
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    if (group == null || groupFooter.RowGroupInfo?.CollectionViewGroup == group)
                    {
                        groupFooter.UpdateSummaryRowState();
                    }
                }
            }
        }

        private void UpdateGroupSummaryRowTheme()
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.ApplySummaryRowTheme();
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.ApplySummaryRowTheme();
                }
            }
        }

        private void UpdateSummaryCellAppearance()
        {
            _totalSummaryRow?.UpdateCellAppearance();
            UpdateGroupSummaryCellAppearance();
        }

        private void UpdateGroupSummaryCellAppearance()
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.SummaryRow?.UpdateCellAppearance();
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.SummaryRow?.UpdateCellAppearance();
                }
            }
        }

        private void UpdateGroupSummaryRowOffset()
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.UpdateSummaryRowOffset();
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.UpdateSummaryRowOffset();
                }
            }
        }

        private void UpdateGroupSummaryRowLayout()
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.UpdateSummaryRowLayout();
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.UpdateCellLayout();
                }
            }
        }

        internal void UpdateSummaryRowLayout()
        {
            _totalSummaryRow?.UpdateCellLayout();
            UpdateGroupSummaryRowLayout();
        }

        private void DetachSummaryRows()
        {
            _totalSummaryRow?.DetachFromGrid();

            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.SummaryRow?.DetachFromGrid();
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.SummaryRow?.DetachFromGrid();
                }
            }
        }

        internal void OnGroupSummaryColumnAdded(DataGridColumn column, int index)
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.SummaryRow?.OnColumnAdded(column, index);
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.SummaryRow?.OnColumnAdded(column, index);
                }
            }
        }

        internal void OnGroupSummaryColumnRemoved(DataGridColumn column)
        {
            if (DisplayData == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingElements())
            {
                if (element is DataGridRowGroupHeader groupHeader)
                {
                    groupHeader.SummaryRow?.OnColumnRemoved(column);
                }
                else if (element is DataGridRowGroupFooter groupFooter)
                {
                    groupFooter.SummaryRow?.OnColumnRemoved(column);
                }
            }
        }

        /// <summary>
        /// Called when column summaries change.
        /// </summary>
        internal void OnColumnSummariesChanged(DataGridColumn column)
        {
            _summaryService?.InvalidateColumn(column);
            InvalidateSummaries();
        }

        /// <summary>
        /// Invalidates all summary values and schedules recalculation.
        /// </summary>
        public void InvalidateSummaries()
        {
            _summaryService?.ScheduleRecalculation();
        }

        /// <summary>
        /// Forces immediate recalculation of all summaries.
        /// </summary>
        public void RecalculateSummaries()
        {
            _summaryService?.RecalculateAll();
        }

        /// <summary>
        /// Gets summary values for a column.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <param name="description">The summary description.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="group">The group (for group scope).</param>
        /// <returns>The calculated value.</returns>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        object GetSummaryValue(DataGridColumn column, DataGridSummaryDescription description, DataGridSummaryScope scope, DataGridCollectionViewGroup group = null)
        {
            if (_summaryService == null) return null;

            if (scope == DataGridSummaryScope.Total)
            {
                return _summaryService.GetTotalSummaryValue(column, description);
            }
            else if (scope == DataGridSummaryScope.Group && group != null)
            {
                return _summaryService.GetGroupSummaryValue(column, description, group);
            }

            return null;
        }

        /// <summary>
        /// Called when the data source changes - hooks up summary recalculation.
        /// </summary>
        private void OnDataSourceChangedForSummaries()
        {
            _summaryService?.InvalidateAll();
            _summaryService?.ScheduleRecalculation();
        }

        /// <summary>
        /// Called when a filter is applied or removed.
        /// </summary>
        internal void OnFilterChangedForSummaries()
        {
            _summaryService?.OnFilterChanged();
        }

        /// <summary>
        /// Called when grouping changes.
        /// </summary>
        internal void OnGroupingChangedForSummaries()
        {
            _summaryService?.OnGroupingChanged();
        }

        /// <summary>
        /// Called when sorting changes.
        /// </summary>
        internal void OnSortingChangedForSummaries()
        {
            _summaryService?.InvalidateAll();
            _summaryService?.ScheduleRecalculation();
        }

        /// <summary>
        /// Called when items in the collection change.
        /// </summary>
        internal void OnCollectionChangedForSummaries(NotifyCollectionChangedEventArgs e)
        {
            _summaryService?.OnCollectionChanged(e);
        }

        /// <summary>
        /// Checks if any column has summaries defined.
        /// </summary>
        internal bool HasAnySummaries()
        {
            return Columns.Any(c => c.HasSummaries);
        }
    }
}
