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
        public const string SelectionUnit = "{selection}";

        public const string DataGridRefreshTimeName = "prodatagrid.refresh.time";
        public const string DataGridRefreshTimeDescription = "Duration of DataGrid refresh pass (rows and columns).";

        public const string RowsRefreshTimeName = "prodatagrid.rows.refresh.time";
        public const string RowsRefreshTimeDescription = "Duration of DataGrid row refresh pass.";

        public const string RowsDisplayUpdateTimeName = "prodatagrid.rows.display.update.time";
        public const string RowsDisplayUpdateTimeDescription = "Duration of updating displayed rows during scrolling/virtualization.";

        public const string RowsDisplayScanTimeName = "prodatagrid.rows.display.scan.time";
        public const string RowsDisplayScanTimeDescription = "Duration of scanning row slots to compute displayed range.";

        public const string RowsDisplayScanRealizeTimeName = "prodatagrid.rows.display.scan.realize.time";
        public const string RowsDisplayScanRealizeTimeDescription = "Duration spent realizing non-displayed slots while scanning displayed range.";

        public const string RowsDisplayTrimTimeName = "prodatagrid.rows.display.trim.time";
        public const string RowsDisplayTrimTimeDescription = "Duration of trimming displayed rows that are outside the viewport.";

        public const string RowsMeasureTimeName = "prodatagrid.rows.measure.time";
        public const string RowsMeasureTimeDescription = "Duration of measuring displayed row elements.";

        public const string RowsArrangeTimeName = "prodatagrid.rows.arrange.time";
        public const string RowsArrangeTimeDescription = "Duration of arranging displayed and recycled row elements.";

        public const string RowsDisplayReusedCountName = "prodatagrid.rows.display.reused.count";
        public const string RowsDisplayReusedCountDescription = "Number of displayed-row updates that reused current viewport rows without full recomputation.";

        public const string RowGenerateTimeName = "prodatagrid.rows.generate.time";
        public const string RowGenerateTimeDescription = "Duration of row generation and preparation.";

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

        public const string RowsPreparedCountName = "prodatagrid.rows.prepared.count";
        public const string RowsPreparedCountDescription = "Number of row containers prepared by the DataGrid.";

        public const string RowsMeasuredCountName = "prodatagrid.rows.measured.count";
        public const string RowsMeasuredCountDescription = "Number of row elements measured by the rows presenter.";

        public const string RowsArrangedCountName = "prodatagrid.rows.arranged.count";
        public const string RowsArrangedCountDescription = "Number of row elements arranged by the rows presenter.";

        public const string RowsDisplayScannedCountName = "prodatagrid.rows.display.scanned.count";
        public const string RowsDisplayScannedCountDescription = "Number of slots scanned while computing displayed rows.";

        public const string RowsDisplayRemovedCountName = "prodatagrid.rows.display.removed.count";
        public const string RowsDisplayRemovedCountDescription = "Number of displayed elements removed during viewport trimming.";

        public const string RowsMeasureSkippedCountName = "prodatagrid.rows.measure.skipped.count";
        public const string RowsMeasureSkippedCountDescription = "Number of displayed elements that skipped measurement because desired size was valid.";

        public const string RowsArrangeSkippedCountName = "prodatagrid.rows.arrange.skipped.count";
        public const string RowsArrangeSkippedCountDescription = "Number of elements that skipped arrange because bounds were unchanged.";

        public const string RowsArrangeOffscreenCountName = "prodatagrid.rows.arrange.offscreen.count";
        public const string RowsArrangeOffscreenCountDescription = "Number of elements arranged to offscreen bounds for recycling/hiding.";

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
    }
}
