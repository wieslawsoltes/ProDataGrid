// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Utils;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// Template and initialization
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid
    {

        /// <summary>
        /// Builds the visual tree for the column header when a new template is applied.
        /// </summary>
        //TODO Validation UI
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // The template has changed, so we need to refresh the visuals
            _measured = false;

            if (_topLeftCornerHeader != null)
            {
                _topLeftCornerHeader.PointerPressed -= TopLeftCornerHeader_PointerPressed;
            }

            if (_selectionOverlay is DataGridSelectionOverlay oldOverlay)
            {
                oldOverlay.FillHandle = null;
            }

            if (_fillHandle != null)
            {
                _fillHandle.PointerPressed -= FillHandle_PointerPressed;
                _fillHandle.PointerMoved -= FillHandle_PointerMoved;
                _fillHandle.PointerReleased -= FillHandle_PointerReleased;
                _fillHandle.PointerCaptureLost -= FillHandle_PointerCaptureLost;
            }

            if (_columnHeadersPresenter != null)
            {
                // If we're applying a new template, we want to remove the old column headers first
                _columnHeadersPresenter.Children.Clear();
            }

            _columnHeadersPresenter = e.NameScope.Find<DataGridColumnHeadersPresenter>(DATAGRID_elementColumnHeadersPresenterName);

            if (_columnHeadersPresenter != null)
            {
                if (ColumnsInternal.FillerColumn != null)
                {
                    ColumnsInternal.FillerColumn.IsRepresented = false;
                }
                _columnHeadersPresenter.OwningGrid = this;

                // Columns were added before our Template was applied, add the ColumnHeaders now
                List<DataGridColumn> sortedInternal = new List<DataGridColumn>(ColumnsItemsInternal);
                sortedInternal.Sort(new DisplayIndexComparer());
                foreach (DataGridColumn column in sortedInternal)
                {
                    InsertDisplayedColumnHeader(column);
                }
            }

            if (_rowsPresenter != null)
            {
                // If we're applying a new template, we want to remove the old rows first
                UnloadElements(recycle: true);
                RemoveRecycledChildrenFromVisualTree();
            }

            _rowsPresenter = e.NameScope.Find<DataGridRowsPresenter>(DATAGRID_elementRowsPresenterName);

            if (_rowsPresenter != null)
            {
                _rowsPresenter.OwningGrid = this;
                InvalidateRowHeightEstimate();
                UpdateRowDetailsHeightEstimate();
            }

            _selectionOverlay = e.NameScope.Find<Canvas>(DATAGRID_elementSelectionOverlayName);
            _selectionOutline = e.NameScope.Find<Border>(DATAGRID_elementSelectionOutlineName);
            _fillHandle = e.NameScope.Find<Border>(DATAGRID_elementFillHandleName);

            if (_selectionOverlay is DataGridSelectionOverlay overlay)
            {
                overlay.FillHandle = _fillHandle;
            }

            if (_fillHandle != null)
            {
                _fillHandle.PointerPressed += FillHandle_PointerPressed;
                _fillHandle.PointerMoved += FillHandle_PointerMoved;
                _fillHandle.PointerReleased += FillHandle_PointerReleased;
                _fillHandle.PointerCaptureLost += FillHandle_PointerCaptureLost;
            }

            // Look for the ScrollViewer (used in v2 themes with ILogicalScrollable)
            _scrollViewer = e.NameScope.Find<ScrollViewer>(DATAGRID_elementScrollViewerName);

            _frozenColumnScrollBarSpacer = e.NameScope.Find<Control>(DATAGRID_elementFrozenColumnScrollBarSpacerName);
            _frozenColumnScrollBarSpacerRight = e.NameScope.Find<Control>(DATAGRID_elementFrozenColumnScrollBarSpacerRightName);

            // Setup legacy scroll bars (from DataGrid.LegacyScrolling.cs)
            SetupLegacyScrollBars(e.NameScope);

            _topLeftCornerHeader = e.NameScope.Find<ContentControl>(DATAGRID_elementTopLeftCornerHeaderName);
            EnsureTopLeftCornerHeader(); // EnsureTopLeftCornerHeader checks for a null _topLeftCornerHeader;
            _topRightCornerHeader = e.NameScope.Find<ContentControl>(DATAGRID_elementTopRightCornerHeaderName);
            _bottomRightCorner = e.NameScope.Find<Visual>(DATAGRID_elementBottomRightCornerHeaderName);

            if (_topLeftCornerHeader != null)
            {
                _topLeftCornerHeader.PointerPressed += TopLeftCornerHeader_PointerPressed;
            }

            // Setup total summary row from template
            SetupTotalSummaryRow(e.NameScope);

            TryExecutePendingAutoScroll();

            RequestSelectionOverlayRefresh();

            // Ensure row drag/drop wiring is active even if the property was set while handlers were suspended.
            if (_rowDragDropController == null && CanUserReorderRows)
            {
                RefreshRowDragDropController();
            }
        }


        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _recycledChildrenCleanupToken++;
            _recycledChildrenCleanupPending = false;
            _deferRecycledChildrenRemoval = false;
            foreach (var column in ColumnsInternal)
            {
                if (column.OwningGrid == null)
                {
                    column.OwningGrid = this;
                }
            }
            NormalizeColumnDisplayIndexesAfterDetachedMutations();
            if (_columnHeadersPresenter != null && _columnHeadersPresenter.OwningGrid == null)
            {
                _columnHeadersPresenter.OwningGrid = this;
            }
            EnsureColumnHeadersPresenterChildren();
            if (_rowsPresenter != null && _rowsPresenter.OwningGrid == null)
            {
                _rowsPresenter.OwningGrid = this;
            }
            if (_summaryService == null)
            {
                InitializeSummaryService();
                OnDataSourceChangedForSummaries();
            }
            EnsureTotalSummaryRow();
            OnDataSourceChangedForValidation();
            if (DataConnection.DataSource != null && !DataConnection.EventsWired)
            {
                DataConnection.WireEvents(DataConnection.DataSource);
                AttachExternalSubscriptions();
                UpdateSortingAdapterView();
                UpdateFilteringAdapterView();
                UpdateSearchAdapterView();
                UpdateConditionalFormattingAdapterView();
                if (_scrollStateManager.ShouldPreserveScrollState() && !_columnsChangedWhileDetached)
                {
                    InitializeElementsAfterReattach();
                }
                else
                {
                    InitializeElements(true /*recycleRows*/);
                }
            }
            else
            {
                AttachExternalSubscriptions();
            }

            _columnsChangedWhileDetached = false;

            TryRestorePendingGroupingState();

            if (_rowDragDropController == null && CanUserReorderRows)
            {
                RefreshRowDragDropController();
            }

            TryExecutePendingAutoScroll();
            UpdateKeyboardGestureSubscriptions();
        }


        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            CancelPendingLayoutRefreshes();
            CancelEdit(DataGridEditingUnit.Row, raiseEvents: false);
            DetachExternalEditingElement();
            CapturePendingGroupingState();
            _scrollStateManager.Capture(preserveOnAttach: true);
            _deferRecycledChildrenRemoval = true;
            _suppressCellContentUpdates = true;
            try
            {
                UnloadElements(recycle: true);
            }
            finally
            {
                _suppressCellContentUpdates = false;
            }

            DetachExternalSubscriptions();

            if (_rowDragDropController != null)
            {
                _rowDragDropController.Dispose();
                _rowDragDropController = null;
            }

            if (_columnHeadersPresenter != null)
            {
                RemoveDisplayedColumnHeaders();
            }

            RemoveRecycledChildrenFromVisualTree();

            EndSelectionDrag();
            DisposeDragAutoScrollTimer();
            EndFillHandleDrag(applyFill: false);
            DisposeFillAutoScrollTimer();
            CancelPendingAutoScroll();

            _validationSubscription?.Dispose();
            _validationSubscription = null;
            InvalidateCollectionValidationState(clearTracking: true);

            DetachRowGroupHandlers(resetTopLevelGroup: false);
            DetachSummaryRows();

            foreach (var column in ColumnsInternal)
            {
                column.OwningGrid = null;
            }
            if (_columnHeadersPresenter != null)
            {
                _columnHeadersPresenter.OwningGrid = null;
            }
            if (_rowsPresenter != null)
            {
                _rowsPresenter.OwningGrid = null;
            }

            // When wired to INotifyCollectionChanged, the DataGrid will be cleaned up by GC
            if (DataConnection.DataSource != null && DataConnection.EventsWired)
            {
                DataConnection.UnWireEvents(DataConnection.DataSource);
            }

            DetachAdapterViews();

            DisposeSummaryService();
            DataGridColumnHeader.ResetStaticState();
            UpdateKeyboardGestureSubscriptions();

            base.OnDetachedFromVisualTree(e);
        }

        private void RequestRecycledChildrenCleanup()
        {
            if (_recycledChildrenCleanupPending)
            {
                return;
            }

            _recycledChildrenCleanupPending = true;
            var token = ++_recycledChildrenCleanupToken;
            Dispatcher.UIThread.Post(() => PerformRecycledChildrenCleanup(token), DispatcherPriority.Background);
        }

        private void PerformRecycledChildrenCleanup(int token)
        {
            if (token != _recycledChildrenCleanupToken)
            {
                return;
            }

            _recycledChildrenCleanupPending = false;

            if (IsAttachedToVisualTree || _rowsPresenter == null)
            {
                return;
            }

            _deferRecycledChildrenRemoval = false;
            RemoveRecycledChildrenFromVisualTreeCore();
        }

        private void EnsureColumnHeadersPresenterChildren()
        {
            if (_columnHeadersPresenter == null)
            {
                return;
            }

            if (_columnHeadersPresenter.Children.Count > 0)
            {
                return;
            }

            ColumnsInternal.FillerColumn.IsRepresented = false;

            var sortedInternal = new List<DataGridColumn>(ColumnsItemsInternal);
            sortedInternal.Sort(new DisplayIndexComparer());
            foreach (var column in sortedInternal)
            {
                InsertDisplayedColumnHeader(column);
            }

            InvalidateColumnHeadersMeasure();
        }

        private void CancelPendingLayoutRefreshes()
        {
            if (_pendingPointerOverRefresh)
            {
                LayoutUpdated -= DataGrid_LayoutUpdatedPointerOverRefresh;
                _pendingPointerOverRefresh = false;
            }

            if (_pendingHierarchicalIndentationRefresh)
            {
                LayoutUpdated -= DataGrid_LayoutUpdatedHierarchicalIndentationRefresh;
                _pendingHierarchicalIndentationRefresh = false;
            }

            if (_pendingGroupingIndentationRefresh)
            {
                LayoutUpdated -= DataGrid_LayoutUpdatedGroupingIndentationRefresh;
                _pendingGroupingIndentationRefresh = false;
                _groupingIndentationRefreshQueued = false;
            }

            if (_pendingSelectionOverlayRefresh)
            {
                LayoutUpdated -= DataGrid_LayoutUpdatedSelectionOverlayRefresh;
                _pendingSelectionOverlayRefresh = false;
            }

            if (_selectionOverlayLayoutHooked)
            {
                LayoutUpdated -= DataGrid_LayoutUpdatedSelectionOverlay;
                _selectionOverlayLayoutHooked = false;
            }
        }


        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DataGridAutomationPeer(this);
        }


        internal void InitializeElements(bool recycleRows)
        {
            bool preserveScrollState = _scrollStateManager.ShouldPreserveScrollState();
            try
            {
                _noCurrentCellChangeCount++;
                var selectionSnapshot = CaptureSelectionSnapshot();

                // The underlying collection has changed and our editing row (if there is one)
                // is no longer relevant, so we should force a cancel edit.
                CancelEdit(DataGridEditingUnit.Row, raiseEvents: false);

                // Notify the estimator about the reset
                if (!preserveScrollState)
                {
                    _scrollStateManager.Clear();
                    DisplayData.PendingVerticalScrollHeight = 0;
                    _verticalOffset = 0;
                    NegVerticalOffset = 0;
                    RowHeightEstimator?.Reset();
                }

                // We want to persist selection throughout a reset, so store away the selected items
                List<object> selectedItemsCache = new List<object>(_selectedItems.SelectedItemsCache);

                var collapsedGroupsCache = RowGroupHeadersTable
                    .Where(g => !g.Value.IsVisible)
                    .Select(g => g.Value.CollectionViewGroup.Key)
                    .ToArray();

                if (recycleRows)
                {
                    RefreshRows(recycleRows, clearRows: true);
                }
                else
                {
                    RefreshRowsAndColumns(clearRows: true);
                }

                // collapse previously collapsed groups
                foreach (var g in collapsedGroupsCache)
                {
                    var item = RowGroupHeadersTable.FirstOrDefault(t => t.Value.CollectionViewGroup.Parent.GroupBy.KeysMatch(t.Value.CollectionViewGroup.Key, g));
                    if (item != null)
                    {
                        EnsureRowGroupVisibility(item.Value, false, false);
                    }
                }

                // Re-select the old items
                _selectedItems.SelectedItemsCache = selectedItemsCache;
                CoerceSelectedItem();
                if (RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.Collapsed)
                {
                    UpdateRowDetailsVisibilityMode(RowDetailsVisibilityMode);
                }

                if (_selectionModelAdapter != null &&
                    (selectionSnapshot is { Count: > 0 } ||
                     HasInvalidSelectionIndexes(_selectionModelAdapter.Model)))
                {
                    _syncingSelectionModel = true;
                    try
                    {
                        using (_selectionModelAdapter.Model.BatchUpdate())
                        {
                            _selectionModelAdapter.Model.Clear();
                            if (selectionSnapshot is { Count: > 0 })
                            {
                                foreach (var item in selectionSnapshot)
                                {
                                    int index = GetSelectionModelIndexOfItem(item);
                                    if (index >= 0)
                                    {
                                        _selectionModelAdapter.Select(index);
                                    }
                                }
                            }
                        }

                        ApplySelectionFromSelectionModel();
                        UpdateSelectionSnapshot();
                    }
                    finally
                    {
                        _syncingSelectionModel = false;
                    }
                }

                // The currently displayed rows may have incorrect visual states because of the selection change
                ApplyDisplayedRowsState(DisplayData.FirstScrollingSlot, DisplayData.LastScrollingSlot);
            }
            finally
            {
                NoCurrentCellChangeCount--;
                _scrollStateManager.ClearPreserveOnAttachFlag();
            }
        }

        private void InitializeElementsAfterReattach()
        {
            try
            {
                _noCurrentCellChangeCount++;
                CancelEdit(DataGridEditingUnit.Row, raiseEvents: false);

                if (DataConnection != null && ColumnsItemsInternal.Count > 0)
                {
                    // Fast path for tab reattach: rows are already unloaded during detach.
                    RefreshRows(recycleRows: true, clearRows: false);
                }

                CoerceSelectedItem();
                if (RowDetailsVisibilityMode != DataGridRowDetailsVisibilityMode.Collapsed)
                {
                    UpdateRowDetailsVisibilityMode(RowDetailsVisibilityMode);
                }

                ApplyDisplayedRowsState(DisplayData.FirstScrollingSlot, DisplayData.LastScrollingSlot);
            }
            finally
            {
                NoCurrentCellChangeCount--;
                _scrollStateManager.ClearPreserveOnAttachFlag();
            }
        }

    }
}
