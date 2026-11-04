namespace Avalonia.Controls;

internal static partial class DataGridDiagnostics
{
    public const string ActivitySourceName = "ProDataGrid.Diagnostic.Source";
    public const string MeterName = "ProDataGrid.Diagnostic.Meter";
    public const string AppContextSwitchName = "ProDataGrid.Diagnostics.IsEnabled";

    public static class Meters
    {
        public const string MillisecondsUnit = "ms";
        public const string RowsUnit = "{row}";
        public const string ColumnsUnit = "{column}";
        public const string CellsUnit = "{cell}";
        public const string ClipsUnit = "{clip}";
        public const string LinesUnit = "{line}";
        public const string LayoutsUnit = "{layout}";
        public const string OperationsUnit = "{operation}";
        public const string SelectionUnit = "{selection}";

        public const string DataGridRefreshTimeName = "prodatagrid.refresh.time";
        public const string DataGridRefreshTimeDescription = "Duration of DataGrid refresh pass (rows and columns).";

        public const string RowsRefreshTimeName = "prodatagrid.rows.refresh.time";
        public const string RowsRefreshTimeDescription = "Duration of DataGrid row refresh pass.";

        public const string RowsDisplayUpdateTimeName = "prodatagrid.rows.display.update.time";
        public const string RowsDisplayUpdateTimeDescription = "Duration of updating displayed rows during scrolling/virtualization.";

        public const string RowsRetargetEligibilityTimeName = "prodatagrid.rows.retarget.eligibility.time";
        public const string RowsRetargetEligibilityTimeDescription = "Duration of validating the default virtual-row retarget pipeline and calculating its target range.";

        public const string RowsRetargetValidationTimeName = "prodatagrid.rows.retarget.validation.time";
        public const string RowsRetargetValidationTimeDescription = "Duration of validating realized rows before an in-place virtual-row retarget.";

        public const string RowsRetargetBindTimeName = "prodatagrid.rows.retarget.bind.time";
        public const string RowsRetargetBindTimeDescription = "Duration of rebinding and applying state to an in-place virtual-row retarget batch.";

        public const string RowsRetargetApplyTimeName = "prodatagrid.rows.retarget.apply.time";
        public const string RowsRetargetApplyTimeDescription = "Duration of applying item, index, slot, validation, and selection state to a validated virtual-row retarget batch.";

        public const string RowsRetargetChildIndexTimeName = "prodatagrid.rows.retarget.child-index.time";
        public const string RowsRetargetChildIndexTimeDescription = "Duration of invalidating logical child indexes after a virtual-row retarget batch.";

        public const string RowsRetargetLayoutValidityTimeName = "prodatagrid.rows.retarget.layout-validity.time";
        public const string RowsRetargetLayoutValidityTimeDescription = "Duration of validating measure and arrange state after a virtual-row retarget batch.";

        public const string RowsRetargetMeasureReusedCountName = "prodatagrid.rows.retarget.measure.reused.count";
        public const string RowsRetargetMeasureReusedCountDescription = "Number of retargeted rows whose validated fixed-height measure geometry was reused.";

        public const string RowsRetargetArrangeReusedCountName = "prodatagrid.rows.retarget.arrange.reused.count";
        public const string RowsRetargetArrangeReusedCountDescription = "Number of retargeted rows whose unchanged arrangement geometry was reused.";

        public const string RowsPresenterViewportChangedTimeName = "prodatagrid.rows.presenter.viewport.changed.time";
        public const string RowsPresenterViewportChangedTimeDescription = "Duration of handling a rows presenter viewport change notification.";

        public const string RowsScrollSlotsByHeightTimeName = "prodatagrid.rows.scroll.slots.by.height.time";
        public const string RowsScrollSlotsByHeightTimeDescription = "Duration of updating displayed rows for a logical scroll offset change.";

        public const string RowsScrollEstimateOffsetTimeName = "prodatagrid.rows.scroll.estimate.offset.time";
        public const string RowsScrollEstimateOffsetTimeDescription = "Duration of estimating the offset to a visible slot during logical scrolling.";

        public const string RowsMeasureTimeName = "prodatagrid.rows.measure.time";
        public const string RowsMeasureTimeDescription = "Duration of measuring displayed row elements.";

        public const string RowsArrangeTimeName = "prodatagrid.rows.arrange.time";
        public const string RowsArrangeTimeDescription = "Duration of arranging displayed and recycled row elements.";

        public const string DataGridMeasureTimeName = "prodatagrid.datagrid.measure.time";
        public const string DataGridMeasureTimeDescription = "Duration of measuring the DataGrid control.";

        public const string DataGridArrangeTimeName = "prodatagrid.datagrid.arrange.time";
        public const string DataGridArrangeTimeDescription = "Duration of arranging the DataGrid control.";

        public const string CellsMeasureTimeName = "prodatagrid.cells.measure.time";
        public const string CellsMeasureTimeDescription = "Duration of measuring a DataGrid cells presenter.";

        public const string CellsArrangeTimeName = "prodatagrid.cells.arrange.time";
        public const string CellsArrangeTimeDescription = "Duration of arranging a DataGrid cells presenter.";

        public const string RowMeasureTimeName = "prodatagrid.row.measure.time";
        public const string RowMeasureTimeDescription = "Duration of measuring a DataGrid row.";

        public const string RowArrangeTimeName = "prodatagrid.row.arrange.time";
        public const string RowArrangeTimeDescription = "Duration of arranging a DataGrid row.";

        public const string VirtualSurfaceRenderTimeName = "prodatagrid.virtual.surface.render.time";
        public const string VirtualSurfaceRenderTimeDescription = "Duration of recording one virtual cell surface render pass.";

        public const string VirtualSurfaceRenderedRowsCountName = "prodatagrid.virtual.surface.rendered.rows.count";
        public const string VirtualSurfaceRenderedRowsCountDescription = "Number of visible rows recorded by the virtual cell surface.";

        public const string VirtualSurfaceRenderedCellsCountName = "prodatagrid.virtual.surface.rendered.cells.count";
        public const string VirtualSurfaceRenderedCellsCountDescription = "Number of visible cells recorded by the virtual cell surface.";

        public const string VirtualSurfaceClipCountName = "prodatagrid.virtual.surface.clip.count";
        public const string VirtualSurfaceClipCountDescription = "Number of partial-cell clip scopes recorded by the virtual cell surface.";

        public const string VirtualSurfaceVerticalGridLineCountName = "prodatagrid.virtual.surface.vertical-grid-line.count";
        public const string VirtualSurfaceVerticalGridLineCountDescription = "Number of vertical grid-line draw operations recorded by the virtual cell surface.";

        public const string VirtualSurfaceTextLayoutCacheHitCountName = "prodatagrid.virtual.surface.text-layout.cache-hit.count";
        public const string VirtualSurfaceTextLayoutCacheHitCountDescription = "Number of virtual cell text layouts reused from the cache.";

        public const string VirtualSurfaceTextLayoutCacheMissCountName = "prodatagrid.virtual.surface.text-layout.cache-miss.count";
        public const string VirtualSurfaceTextLayoutCacheMissCountDescription = "Number of virtual cell text layouts created after a cache miss.";

        public const string VirtualSurfaceTextDrawOperationCountName = "prodatagrid.virtual.surface.text.draw-operation.count";
        public const string VirtualSurfaceTextDrawOperationCountDescription = "Number of text scene operations recorded by the virtual cell surface.";

        public const string VirtualSurfaceTextGlyphRunCountName = "prodatagrid.virtual.surface.text.glyph-run.count";
        public const string VirtualSurfaceTextGlyphRunCountDescription = "Number of immutable text glyph runs referenced by the virtual cell surface.";

        public const string VirtualSurfaceExpanderDrawOperationCountName = "prodatagrid.virtual.surface.expander.draw-operation.count";
        public const string VirtualSurfaceExpanderDrawOperationCountDescription = "Number of expander drawing operations recorded by the virtual cell surface.";

        public const string VirtualSurfaceValueCacheHitCountName = "prodatagrid.virtual.surface.value-cache.hit.count";
        public const string VirtualSurfaceValueCacheHitCountDescription = "Number of visible cell values reused from overlapping lightweight virtual rows.";

        public const string VirtualSurfaceValueCacheMissCountName = "prodatagrid.virtual.surface.value-cache.miss.count";
        public const string VirtualSurfaceValueCacheMissCountDescription = "Number of visible cell values resolved or formatted for new or invalidated lightweight virtual rows.";

        public const string RowGenerateTimeName = "prodatagrid.rows.generate.time";
        public const string RowGenerateTimeDescription = "Duration of row generation and preparation.";

        public const string RowGenerateAcquireTimeName = "prodatagrid.rows.generate.acquire.time";
        public const string RowGenerateAcquireTimeDescription = "Duration of locating, recycling, or creating a row container.";

        public const string RowGenerateBindTimeName = "prodatagrid.rows.generate.bind.time";
        public const string RowGenerateBindTimeDescription = "Duration of assigning a row container to its item and display slot.";

        public const string RowGeneratePrepareTimeName = "prodatagrid.rows.generate.prepare.time";
        public const string RowGeneratePrepareTimeDescription = "Duration of preparing row cells, state, and lifecycle notifications.";

        public const string RowRecycleTimeName = "prodatagrid.rows.recycle.time";
        public const string RowRecycleTimeDescription = "Duration of cleaning, detaching, hiding, and pooling a row container.";

        public const string RowRecycleCleanupTimeName = "prodatagrid.rows.recycle.cleanup.time";
        public const string RowRecycleCleanupTimeDescription = "Duration of row cleanup notifications and state reset before recycling.";

        public const string RowRecycleDetachTimeName = "prodatagrid.rows.recycle.detach.time";
        public const string RowRecycleDetachTimeDescription = "Duration of detaching and hiding a recycled row container.";

        public const string RowRecyclePoolTimeName = "prodatagrid.rows.recycle.pool.time";
        public const string RowRecyclePoolTimeDescription = "Duration of selecting and updating the row recycle pool.";

        public const string RowsDisplayElementInsertTimeName = "prodatagrid.rows.display.element.insert.time";
        public const string RowsDisplayElementInsertTimeDescription = "Duration of inserting one element into the displayed-row window.";

        public const string RowsDisplayElementAttachTimeName = "prodatagrid.rows.display.element.attach.time";
        public const string RowsDisplayElementAttachTimeDescription = "Duration of attaching one generated element to the rows presenter and registering it as an anchor.";

        public const string RowsDisplayElementMeasureTimeName = "prodatagrid.rows.display.element.measure.time";
        public const string RowsDisplayElementMeasureTimeDescription = "Duration of measuring one generated element during displayed-row insertion.";

        public const string RowsDisplayElementHeightRecordTimeName = "prodatagrid.rows.display.element.height.record.time";
        public const string RowsDisplayElementHeightRecordTimeDescription = "Duration of recording one generated element height during displayed-row insertion.";

        public const string RowsDisplayElementLoadTimeName = "prodatagrid.rows.display.element.load.time";
        public const string RowsDisplayElementLoadTimeDescription = "Duration of loading one element into the displayed-row list.";

        public const string ColumnsAutoGenerateTimeName = "prodatagrid.columns.autogen.time";
        public const string ColumnsAutoGenerateTimeDescription = "Duration of auto-generating columns.";

        public const string SelectionChangedTimeName = "prodatagrid.selection.change.time";
        public const string SelectionChangedTimeDescription = "Duration of raising SelectionChanged.";

        public const string CollectionRefreshTimeName = "prodatagrid.collection.refresh.time";
        public const string CollectionRefreshTimeDescription = "Duration of refreshing the collection view.";

        public const string CollectionFilterTimeName = "prodatagrid.collection.filter.time";
        public const string CollectionFilterTimeDescription = "Duration of filtering items during refresh.";

        public const string CollectionSortTimeName = "prodatagrid.collection.sort.time";
        public const string CollectionSortTimeDescription = "Duration of sorting items during refresh.";

        public const string CollectionGroupTimeName = "prodatagrid.collection.group.time";
        public const string CollectionGroupTimeDescription = "Duration of grouping items during refresh.";

        public const string CollectionGroupTemporaryTimeName = "prodatagrid.collection.group.temporary.time";
        public const string CollectionGroupTemporaryTimeDescription = "Duration of building temporary groups for paging.";

        public const string CollectionGroupPageTimeName = "prodatagrid.collection.group.page.time";
        public const string CollectionGroupPageTimeDescription = "Duration of building page-level groups.";

        public const string RowsRealizedCountName = "prodatagrid.rows.realized.count";
        public const string RowsRealizedCountDescription = "Number of row containers realized by the DataGrid.";

        public const string RowsRecycledCountName = "prodatagrid.rows.recycled.count";
        public const string RowsRecycledCountDescription = "Number of row containers recycled by the DataGrid.";

        public const string RowsRetargetedCountName = "prodatagrid.rows.retargeted.count";
        public const string RowsRetargetedCountDescription = "Number of default virtual-surface rows rebound in place without pool recycling.";

        public const string RowsPreparedCountName = "prodatagrid.rows.prepared.count";
        public const string RowsPreparedCountDescription = "Number of row containers prepared by the DataGrid.";

        public const string RowsMeasuredCountName = "prodatagrid.rows.measured.count";
        public const string RowsMeasuredCountDescription = "Number of row elements measured by the rows presenter.";

        public const string RowsMeasureSkippedCountName = "prodatagrid.rows.measure.skipped.count";
        public const string RowsMeasureSkippedCountDescription = "Number of row elements whose valid measure state skipped measurement.";

        public const string RowsArrangedCountName = "prodatagrid.rows.arranged.count";
        public const string RowsArrangedCountDescription = "Number of row elements arranged by the rows presenter.";

        public const string RowsArrangeSkippedCountName = "prodatagrid.rows.arrange.skipped.count";
        public const string RowsArrangeSkippedCountDescription = "Number of row elements whose valid bounds skipped arrangement.";

        public const string RowsArrangeMeasureInvalidatedCountName = "prodatagrid.rows.arrange.measure.invalidated.count";
        public const string RowsArrangeMeasureInvalidatedCountDescription = "Number of presenter arrange passes that requested another measure pass.";

        public const string RowsScrollInfoChangedCountName = "prodatagrid.rows.scroll.info.changed.count";
        public const string RowsScrollInfoChangedCountDescription = "Number of rows presenter scroll info updates that changed extent, viewport, or offset.";

        public const string RowsScrollExtentChangedCountName = "prodatagrid.rows.scroll.extent.changed.count";
        public const string RowsScrollExtentChangedCountDescription = "Number of rows presenter scroll info updates that changed the scroll extent.";

        public const string RowsScrollExtentDeltaName = "prodatagrid.rows.scroll.extent.delta";
        public const string RowsScrollExtentDeltaDescription = "Absolute pixel delta of rows presenter extent updates.";

        public const string RowsScrollViewportChangedCountName = "prodatagrid.rows.scroll.viewport.changed.count";
        public const string RowsScrollViewportChangedCountDescription = "Number of rows presenter scroll info updates that changed the scroll viewport.";

        public const string RowsScrollViewportDeltaName = "prodatagrid.rows.scroll.viewport.delta";
        public const string RowsScrollViewportDeltaDescription = "Absolute pixel delta of rows presenter viewport updates.";

        public const string RowsScrollOffsetCoercedCountName = "prodatagrid.rows.scroll.offset.coerced.count";
        public const string RowsScrollOffsetCoercedCountDescription = "Number of rows presenter scroll info updates that coerced the scroll offset.";

        public const string RowsScrollInvalidatedCountName = "prodatagrid.rows.scroll.invalidated.count";
        public const string RowsScrollInvalidatedCountDescription = "Number of rows presenter scroll invalidation notifications raised.";

        public const string RowsScrollExactSlotHeightLookupCountName = "prodatagrid.rows.scroll.exact.slot.height.lookup.count";
        public const string RowsScrollExactSlotHeightLookupCountDescription = "Number of exact slot-height lookups during logical scrolling.";

        public const string RowsScrollExactSlotHeightInsertionCountName = "prodatagrid.rows.scroll.exact.slot.height.insertion.count";
        public const string RowsScrollExactSlotHeightInsertionCountDescription = "Number of exact slot-height lookups that inserted a displayed element during logical scrolling.";

        public const string RowsLogicalOffsetSynchronizedCountName = "prodatagrid.rows.logical.offset.synchronized.count";
        public const string RowsLogicalOffsetSynchronizedCountDescription = "Number of logical scroll offsets synchronized from the DataGrid to the rows presenter.";

        public const string RowsLogicalOffsetSynchronizedDeltaName = "prodatagrid.rows.logical.offset.synchronized.delta";
        public const string RowsLogicalOffsetSynchronizedDeltaDescription = "Absolute vertical offset delta corrected during logical scroll synchronization.";

        public const string ColumnsAutoGeneratedCountName = "prodatagrid.columns.autogen.count";
        public const string ColumnsAutoGeneratedCountDescription = "Number of columns auto-generated by the DataGrid.";

        public const string SelectionChangedCountName = "prodatagrid.selection.changed.count";
        public const string SelectionChangedCountDescription = "Number of SelectionChanged events raised by the DataGrid.";
    }

    public static class Tags
    {
        public const string ClearRows = nameof(ClearRows);
        public const string RecycleRows = nameof(RecycleRows);
        public const string AutoGenerateColumns = nameof(AutoGenerateColumns);
        public const string AutoGeneratedColumns = nameof(AutoGeneratedColumns);
        public const string Columns = nameof(Columns);
        public const string Rows = nameof(Rows);
        public const string SlotCount = nameof(SlotCount);
        public const string DisplayHeight = nameof(DisplayHeight);
        public const string FirstDisplayedSlot = nameof(FirstDisplayedSlot);
        public const string LastDisplayedSlot = nameof(LastDisplayedSlot);
        public const string DisplayedSlots = nameof(DisplayedSlots);
        public const string RowIndex = nameof(RowIndex);
        public const string Slot = nameof(Slot);
        public const string Source = nameof(Source);
        public const string AddedCount = nameof(AddedCount);
        public const string RemovedCount = nameof(RemovedCount);
        public const string SelectionSource = nameof(SelectionSource);
        public const string UserInitiated = nameof(UserInitiated);
        public const string SortDescriptions = nameof(SortDescriptions);
        public const string GroupDescriptions = nameof(GroupDescriptions);
        public const string FilterEnabled = nameof(FilterEnabled);
        public const string PageSize = nameof(PageSize);
        public const string PageIndex = nameof(PageIndex);
        public const string UsesLocalArray = nameof(UsesLocalArray);
        public const string IsGrouping = nameof(IsGrouping);
    }

    public static class Sources
    {
        public const string Existing = "existing";
        public const string New = "new";
        public const string Recycled = "recycled";
        public const string OwnContainer = "own-container";
        public const string Retargeted = "retargeted";
    }
}
