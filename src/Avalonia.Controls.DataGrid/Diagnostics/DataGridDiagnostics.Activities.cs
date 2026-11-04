using System.Diagnostics;

namespace Avalonia.Controls;

internal static partial class DataGridDiagnostics
{
    private static ActivitySource? s_activitySource;

    public static void InitActivitySource()
    {
        s_activitySource = new ActivitySource(ActivitySourceName);
    }

    private static Activity? StartActivity(string name) => s_activitySource?.StartActivity(name);

    public static Activity? RefreshRowsAndColumns() => StartActivity("ProDataGrid.DataGrid.RefreshRowsAndColumns");
    public static Activity? RefreshRows() => StartActivity("ProDataGrid.DataGrid.RefreshRows");
    public static Activity? HierarchicalBulkSplice() => StartActivity("ProDataGrid.DataGrid.HierarchicalBulkSplice");
    public static Activity? HierarchicalExpandAll() => StartActivity("ProDataGrid.Hierarchy.ExpandAll");
    public static Activity? HierarchicalCollapseAll() => StartActivity("ProDataGrid.Hierarchy.CollapseAll");
    public static Activity? HierarchicalFlattenedChanged() => StartActivity("ProDataGrid.DataGrid.HierarchicalFlattenedChanged");
    public static Activity? HierarchicalIndentationRefresh() => StartActivity("ProDataGrid.DataGrid.HierarchicalIndentationRefresh");
    public static Activity? HierarchicalDisplayedRowsRange() => StartActivity("ProDataGrid.DataGrid.HierarchicalDisplayedRowsRange");
    public static Activity? HierarchicalSelectionRemap() => StartActivity("ProDataGrid.DataGrid.HierarchicalSelectionRemap");
    public static Activity? UpdateDisplayedRows() => StartActivity("ProDataGrid.DataGrid.UpdateDisplayedRows");
    public static Activity? GenerateRow() => StartActivity("ProDataGrid.DataGrid.GenerateRow");
    public static Activity? AutoGenerateColumns() => StartActivity("ProDataGrid.DataGrid.AutoGenerateColumns");
    public static Activity? SelectionChanged() => StartActivity("ProDataGrid.DataGrid.SelectionChanged");

    public static Activity? CollectionRefresh() => StartActivity("ProDataGrid.CollectionView.Refresh");
    public static Activity? CollectionFilter() => StartActivity("ProDataGrid.CollectionView.Filter");
    public static Activity? CollectionSort() => StartActivity("ProDataGrid.CollectionView.Sort");
    public static Activity? CollectionGroup() => StartActivity("ProDataGrid.CollectionView.Group");
    public static Activity? CollectionGroupTemporary() => StartActivity("ProDataGrid.CollectionView.GroupTemporary");
    public static Activity? CollectionGroupPage() => StartActivity("ProDataGrid.CollectionView.GroupPage");
}
