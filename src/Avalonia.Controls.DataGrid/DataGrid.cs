// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Avalonia.Input.Platform;
using System.ComponentModel.DataAnnotations;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Utils;
using Avalonia.Layout;
using Avalonia.Controls.Metadata;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Styling;
using Avalonia.Reactive;

namespace Avalonia.Controls
{
    /// <summary>
    /// Displays data in a customizable grid.
    /// </summary>
    [TemplatePart(DATAGRID_elementBottomRightCornerHeaderName,     typeof(Visual))]
    [TemplatePart(DATAGRID_elementColumnHeadersPresenterName,      typeof(DataGridColumnHeadersPresenter))]
    [TemplatePart(DATAGRID_elementFrozenColumnScrollBarSpacerName, typeof(Control))]
    [TemplatePart(DATAGRID_elementHorizontalScrollbarName,         typeof(ScrollBar))]
    [TemplatePart(DATAGRID_elementRowsPresenterName,               typeof(DataGridRowsPresenter))]
    [TemplatePart(DATAGRID_elementScrollViewerName,                typeof(ScrollViewer))]
    [TemplatePart(DATAGRID_elementTopLeftCornerHeaderName,         typeof(ContentControl))]
    [TemplatePart(DATAGRID_elementTopRightCornerHeaderName,        typeof(ContentControl))]
    [TemplatePart(DATAGRID_elementVerticalScrollbarName,           typeof(ScrollBar))]
    [PseudoClasses(":invalid", ":empty-rows", ":empty-columns")]
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid : TemplatedControl
    {
        private const string DATAGRID_elementRowsPresenterName = "PART_RowsPresenter";
        private const string DATAGRID_elementColumnHeadersPresenterName = "PART_ColumnHeadersPresenter";
        private const string DATAGRID_elementFrozenColumnScrollBarSpacerName = "PART_FrozenColumnScrollBarSpacer";
        private const string DATAGRID_elementScrollViewerName = "PART_ScrollViewer";
        private const string DATAGRID_elementTopLeftCornerHeaderName = "PART_TopLeftCornerHeader";
        private const string DATAGRID_elementTopRightCornerHeaderName = "PART_TopRightCornerHeader";
        private const string DATAGRID_elementBottomRightCornerHeaderName = "PART_BottomRightCorner";
        internal const bool DATAGRID_defaultCanUserReorderColumns = true;
        internal const bool DATAGRID_defaultCanUserResizeColumns = true;
        internal const bool DATAGRID_defaultCanUserSortColumns = true;

        /// <summary>
        /// The default order to use for columns when there is no <see cref="DisplayAttribute.Order"/>
        /// value available for the property.
        /// </summary>
        /// <remarks>
        /// The value of 10,000 comes from the DataAnnotations spec, allowing
        /// some properties to be ordered at the beginning and some at the end.
        /// </remarks>
        private const int DATAGRID_defaultColumnDisplayOrder = 10000;

        private const double DATAGRID_horizontalGridLinesThickness = 1;
        private const double DATAGRID_minimumRowHeaderWidth = 4;
        private const double DATAGRID_minimumColumnHeaderHeight = 4;
        internal const double DATAGRID_maximumStarColumnWidth = 10000;
        internal const double DATAGRID_minimumStarColumnWidth = 0.001;
        private const double DATAGRID_mouseWheelDelta = 50.0;
        private const double DATAGRID_maxHeadersThickness = 32768;

        private const double DATAGRID_defaultRowHeight = 22;
        internal const double DATAGRID_defaultRowGroupSublevelIndent = 20;
        private const double DATAGRID_defaultMinColumnWidth = 20;
        private const double DATAGRID_defaultMaxColumnWidth = double.PositiveInfinity;

        private INotifyCollectionChanged _topLevelGroup;

        private Visual _bottomRightCorner;
        private DataGridColumnHeadersPresenter _columnHeadersPresenter;
        private DataGridRowsPresenter _rowsPresenter;
        private ScrollViewer _scrollViewer;

        private ContentControl _topLeftCornerHeader;
        private ContentControl _topRightCornerHeader;
        private Control _frozenColumnScrollBarSpacer;

        // the sum of the widths in pixels of the scrolling columns preceding
        // the first displayed scrolling column
        private double _horizontalOffset;

        // the number of pixels of the firstDisplayedScrollingCol which are not displayed
        private double _negHorizontalOffset;
        private byte _autoGeneratingColumnOperationCount;
        private bool _areHandlersSuspended;
        private bool _autoSizingColumns;
        private IndexToValueTable<bool> _collapsedSlotsTable;

        // used to store the current column during a Reset

        // this is a workaround only for the scenarios where we need it, it is not all encompassing nor always updated

        // Nth row of rows 0..N that make up the RowHeightEstimate
        private int _lastEstimatedRow;
        private List<DataGridRow> _loadedRows;

        // prevents reentry into the VerticalScroll event handler
        private int? _mouseOverRowIndex;    // -1 is used for the 'new row'
        private double[] _rowGroupHeightsByLevel;
        private double _rowHeaderDesiredWidth;
        private Size? _rowsPresenterAvailableSize;
        private bool _scrollingByHeight;
        private IndexToValueTable<bool> _showDetailsTable;
        private DataGridSelectedItemsCollection _selectedItems;
        private IList _selectedItemsBinding;
        private INotifyCollectionChanged _selectedItemsBindingNotifications;
        private DataGridSelectionModelAdapter _selectionModelAdapter;
        private DataGridSelection.DataGridPagedSelectionSource _pagedSelectionSource;
        private List<object> _selectionModelSnapshot;
        private bool _syncingSelectionModel;
        private bool _syncingSelectedItems;
        private int _preferredSelectionIndex = -1;
        private IDataGridSelectionModelFactory _selectionModelFactory;

        // An approximation of the sum of the heights in pixels of the scrolling rows preceding
        // the first displayed scrolling row.  Since the scrolled off rows are discarded, the grid
        // does not know their actual height. The heights used for the approximation are the ones
        // set as the rows were scrolled off.
        private double _verticalOffset;
        public event EventHandler<ScrollEventArgs> HorizontalScroll;
        public event EventHandler<ScrollEventArgs> VerticalScroll;

        static DataGrid()
        {
            AffectsMeasure<DataGrid>(
                ColumnHeaderHeightProperty,
                HorizontalScrollBarVisibilityProperty,
                VerticalScrollBarVisibilityProperty);

            ItemsSourceProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnItemsSourcePropertyChanged(e));
            CanUserResizeColumnsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnCanUserResizeColumnsChanged(e));
            ColumnWidthProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnColumnWidthChanged(e));
            FrozenColumnCountProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnFrozenColumnCountChanged(e));
            GridLinesVisibilityProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnGridLinesVisibilityChanged(e));
            HeadersVisibilityProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnHeadersVisibilityChanged(e));
            HorizontalGridLinesBrushProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnHorizontalGridLinesBrushChanged(e));
            IsReadOnlyProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnIsReadOnlyChanged(e));
            MaxColumnWidthProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnMaxColumnWidthChanged(e));
            MinColumnWidthProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnMinColumnWidthChanged(e));
            CanUserAddRowsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnCanUserAddRowsChanged(e));
            CanUserDeleteRowsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnCanUserDeleteRowsChanged(e));
            RowHeightProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowHeightChanged(e));
            RowHeaderWidthProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowHeaderWidthChanged(e));
            SelectionModeProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnSelectionModeChanged(e));
            VerticalGridLinesBrushProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnVerticalGridLinesBrushChanged(e));
            SelectedIndexProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnSelectedIndexChanged(e));
            SelectedItemProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnSelectedItemChanged(e));
            IsEnabledProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.DataGrid_IsEnabledChanged(e));
            AreRowGroupHeadersFrozenProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAreRowGroupHeadersFrozenChanged(e));
            RowDetailsTemplateProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDetailsTemplateChanged(e));
            RowDetailsVisibilityModeProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDetailsVisibilityModeChanged(e));
            AutoGenerateColumnsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAutoGenerateColumnsChanged(e));
            RowHeightEstimatorProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowHeightEstimatorChanged(e));

            FocusableProperty.OverrideDefaultValue<DataGrid>(true);

            DataGridTypeDescriptorPlugin.EnsureRegistered();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGrid" /> class.
        /// </summary>
        public DataGrid()
        {
            KeyDown += DataGrid_KeyDown;
            KeyUp += DataGrid_KeyUp;

            //TODO: Check if override works
            GotFocus += DataGrid_GotFocus;
            LostFocus += DataGrid_LostFocus;

            _loadedRows = new List<DataGridRow>();
            _lostFocusActions = new Queue<Action>();
            _selectedItems = new DataGridSelectedItemsCollection(this);
            _selectedItems.CollectionChanged += OnSelectedItemsCollectionChanged;
            RowGroupHeadersTable = new IndexToValueTable<DataGridRowGroupInfo>();
            _bindingValidationErrors = new List<Exception>();

            DisplayData = new DataGridDisplayData(this);
            ColumnsInternal = CreateColumnsInstance();
            ColumnsInternal.CollectionChanged += ColumnsInternal_CollectionChanged;

            RowHeightEstimate = DATAGRID_defaultRowHeight;
            RowDetailsHeightEstimate = 0;
            _rowHeaderDesiredWidth = 0;

            DataConnection = new DataGridDataConnection(this);
            _showDetailsTable = new IndexToValueTable<bool>();
            _collapsedSlotsTable = new IndexToValueTable<bool>();

            AnchorSlot = -1;
            _lastEstimatedRow = -1;
            _editingColumnIndex = -1;
            _mouseOverRowIndex = null;
            CurrentCellCoordinates = new DataGridCellCoordinates(-1, -1);

            SetSelectionModel(CreateSelectionModel(), initializing: true);

            RowGroupHeaderHeightEstimate = DATAGRID_defaultRowHeight;

            UpdatePseudoClasses();
        }

        private void SetValueNoCallback<T>(AvaloniaProperty<T> property, T value, BindingPriority priority = BindingPriority.LocalValue)
        {
            _areHandlersSuspended = true;
            try
            {
                SetValue(property, value, priority);
            }
            finally
            {
                _areHandlersSuspended = false;
            }
        }

        internal void UpdatePseudoClasses()
        {
            PseudoClasses.Set(":empty-columns", !ColumnsInternal.GetVisibleColumns().Any());
            PseudoClasses.Set(":empty-rows", !DataConnection.Any());
        }

        private void OnCanUserAddRowsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            RefreshRowsAndColumns(clearRows: false);
            UpdatePseudoClasses();
        }

        private void OnCanUserDeleteRowsChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            UpdatePseudoClasses();
        }

        /// <summary>
        /// Gets a collection that contains all the columns in the control.
        /// </summary>
        public ObservableCollection<DataGridColumn> Columns
        {
            get
            {
                // we use a backing field here because the field's type
                // is a subclass of the property's
                return ColumnsInternal;
            }
        }

        /// <summary>
        /// Gets a list that contains the data items corresponding to the selected rows.
        /// </summary>
        public IList SelectedItems
        {
            get
            {
                if (_selectedItemsBinding != null)
                {
                    return _selectedItemsBinding;
                }

                if (_selectionModelAdapter != null)
                {
                    return _selectionModelAdapter.SelectedItemsView;
                }

                return _selectedItems;
            }
            set => SetSelectedItemsCollection(value);
        }

        /// <summary>
        /// Gets or sets the selection model that drives row selection.
        /// </summary>
        public ISelectionModel Selection
        {
            get => _selectionModelAdapter?.Model;
            set => SetSelectionModel(value);
        }

        internal DataGridColumnCollection ColumnsInternal
        {
            get;
        }

        internal double ActualRowHeaderWidth
        {
            get
            {
                if (!AreRowHeadersVisible)
                {
                    return 0;
                }
                else
                {
                    return !double.IsNaN(RowHeaderWidth) ? RowHeaderWidth : RowHeadersDesiredWidth;
                }
            }
        }

        internal double ActualRowsPresenterHeight
        {
            get
            {
                if (_rowsPresenter != null)
                {
                    return _rowsPresenter.Bounds.Height;
                }
                return 0;
            }
        }

        internal bool AreColumnHeadersVisible
        {
            get
            {
                return (HeadersVisibility & DataGridHeadersVisibility.Column) == DataGridHeadersVisibility.Column;
            }
        }

        internal bool AreRowHeadersVisible
        {
            get
            {
                return (HeadersVisibility & DataGridHeadersVisibility.Row) == DataGridHeadersVisibility.Row;
            }
        }

        /// <summary>
        /// Indicates whether or not at least one auto-sizing column is waiting for all the rows
        /// to be measured before its final width is determined.
        /// </summary>
        internal bool AutoSizingColumns
        {
            get
            {
                return _autoSizingColumns;
            }
            set
            {
                if (_autoSizingColumns && !value && ColumnsInternal != null)
                {
                    double adjustment = CellsWidth - ColumnsInternal.VisibleEdgedColumnsWidth;
                    AdjustColumnWidths(0, adjustment, false);
                    foreach (DataGridColumn column in ColumnsInternal.GetVisibleColumns())
                    {
                        column.IsInitialDesiredWidthDetermined = true;
                    }
                    ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
                    ComputeScrollBarsLayout();
                    InvalidateColumnHeadersMeasure();
                    InvalidateRowsMeasure(true);
                }
                _autoSizingColumns = value;
            }
        }

        internal double AvailableSlotElementRoom
        {
            get;
            set;
        }

        internal double CellsEstimatedHeight
        {
            get
            {
                return RowsPresenterAvailableSize?.Height ?? 0;
            }
        }

        // Width currently available for cells this value is smaller.  This width is reduced by the existence of RowHeaders
        // or a vertical scrollbar.  Layout is asynchronous so changes to the RowHeaders or the vertical scrollbar are
        // not reflected immediately
        internal double CellsWidth
        {
            get
            {
                double rowsWidth = double.PositiveInfinity;
                if (RowsPresenterAvailableSize.HasValue)
                {
                    rowsWidth = Math.Max(0, RowsPresenterAvailableSize.Value.Width - ActualRowHeaderWidth);
                }
                return double.IsPositiveInfinity(rowsWidth) ? ColumnsInternal.VisibleEdgedColumnsWidth : rowsWidth;
            }
        }

        internal DataGridColumnHeadersPresenter ColumnHeaders => _columnHeadersPresenter;

        internal List<DataGridColumn> ColumnsItemsInternal => ColumnsInternal.ItemsInternal;

        internal DataGridDataConnection DataConnection
        {
            get;
            private set;
        }

        internal DataGridDisplayData DisplayData
        {
            get;
            private set;
        }

        internal double FirstDisplayedScrollingColumnHiddenWidth => _negHorizontalOffset;

        // When the RowsPresenter's width increases, the HorizontalOffset will be incorrect until
        // the scrollbar's layout is recalculated, which doesn't occur until after the cells are measured.
        // This property exists to account for this scenario, and avoid collapsing the incorrect cells.
        internal double HorizontalAdjustment
        {
            get;
            private set;
        }

        internal static double HorizontalGridLinesThickness => DATAGRID_horizontalGridLinesThickness;

        // the sum of the widths in pixels of the scrolling columns preceding
        // the first displayed scrolling column
        internal double HorizontalOffset
        {
            get
            {
                return _horizontalOffset;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                double widthNotVisible = Math.Max(0, ColumnsInternal.VisibleEdgedColumnsWidth - CellsWidth);
                if (value > widthNotVisible)
                {
                    value = widthNotVisible;
                }
                if (value == _horizontalOffset)
                {
                    return;
                }

                SyncHorizontalScrollBarValue(value);
                _horizontalOffset = value;

                DisplayData.FirstDisplayedScrollingCol = ComputeFirstVisibleScrollingColumn();
                // update the lastTotallyDisplayedScrollingCol
                ComputeDisplayedColumns();
            }
        }

        internal IndexToValueTable<DataGridRowGroupInfo> RowGroupHeadersTable
        {
            get;
            private set;
        }

        internal bool LoadingOrUnloadingRow
        {
            get;
            private set;
        }

        internal bool InDisplayIndexAdjustments
        {
            get;
            set;
        }

        internal int? MouseOverRowIndex
        {
            get
            {
                return _mouseOverRowIndex;
            }
            set
            {
                if (_mouseOverRowIndex != value)
                {
                    DataGridRow oldMouseOverRow = null;
                    if (_mouseOverRowIndex.HasValue)
                    {
                        int oldSlot = SlotFromRowIndex(_mouseOverRowIndex.Value);
                        if (IsSlotVisible(oldSlot))
                        {
                            oldMouseOverRow = DisplayData.GetDisplayedElement(oldSlot) as DataGridRow;
                        }
                    }

                    _mouseOverRowIndex = value;

                    // State for the old row needs to be applied after setting the new value
                    if (oldMouseOverRow != null)
                    {
                        oldMouseOverRow.ApplyState();
                    }

                    if (_mouseOverRowIndex.HasValue)
                    {
                        int newSlot = SlotFromRowIndex(_mouseOverRowIndex.Value);
                        if (IsSlotVisible(newSlot))
                        {
                            DataGridRow newMouseOverRow = DisplayData.GetDisplayedElement(newSlot) as DataGridRow;
                            Debug.Assert(newMouseOverRow != null);
                            if (newMouseOverRow != null)
                            {
                                newMouseOverRow.ApplyState();
                            }
                        }
                    }
                }
            }
        }

        internal double NegVerticalOffset
        {
            get;
            private set;
        }

        internal double RowHeadersDesiredWidth
        {
            get
            {
                return _rowHeaderDesiredWidth;
            }
            set
            {
                // We only auto grow
                if (_rowHeaderDesiredWidth < value)
                {
                    double oldActualRowHeaderWidth = ActualRowHeaderWidth;
                    _rowHeaderDesiredWidth = value;
                    if (oldActualRowHeaderWidth != ActualRowHeaderWidth)
                    {
                        EnsureRowHeaderWidth();
                    }
                }
            }
        }

        internal double RowGroupHeaderHeightEstimate
        {
            get;
            private set;
        }

        internal double RowHeightEstimate
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the effective row height estimator, creating a default one if none is set.
        /// </summary>
        internal IDataGridRowHeightEstimator EffectiveRowHeightEstimator
        {
            get
            {
                var estimator = RowHeightEstimator;
                if (estimator == null)
                {
                    // Use the internal estimates directly when no custom estimator is set
                    return null;
                }
                return estimator;
            }
        }

        /// <summary>
        /// Called when the RowHeightEstimator property changes.
        /// </summary>
        private void OnRowHeightEstimatorChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var oldEstimator = e.OldValue as IDataGridRowHeightEstimator;
            var newEstimator = e.NewValue as IDataGridRowHeightEstimator;

            if (newEstimator != null)
            {
                // Initialize the new estimator with current state
                newEstimator.DefaultRowHeight = DATAGRID_defaultRowHeight;
                newEstimator.OnDataSourceChanged(SlotCount);
                
                // Sync current estimates
                if (RowHeightEstimate != DATAGRID_defaultRowHeight)
                {
                    // Transfer our current estimate to the new estimator if we've measured any rows
                }
            }

            // Force refresh of displayed rows
            InvalidateMeasure();
        }

        /// <summary>
        /// Gets the calculated total height of all rows (actual and estimated) for scroll extent.
        /// </summary>
        internal double GetEdgedRowsHeight()
        {
            return EdgedRowsHeightCalculated;
        }

        /// <summary>
        /// Gets the current vertical scroll offset.
        /// </summary>
        internal double GetVerticalOffset()
        {
            return _verticalOffset;
        }

        /// <summary>
        /// Gets the total width of all visible frozen columns.
        /// </summary>
        internal double GetVisibleFrozenColumnsWidth()
        {
            return ColumnsInternal.GetVisibleFrozenEdgedColumnsWidth();
        }

        /// <summary>
        /// Gets the total width of all visible scrolling (non-frozen) columns.
        /// </summary>
        internal double GetVisibleScrollingColumnsWidth()
        {
            return ColumnsInternal.VisibleEdgedColumnsWidth - ColumnsInternal.GetVisibleFrozenEdgedColumnsWidth();
        }

        internal Size? RowsPresenterAvailableSize
        {
            get
            {
                return _rowsPresenterAvailableSize;
            }
            set
            {
                if (_rowsPresenterAvailableSize.HasValue && value.HasValue && value.Value.Width > RowsPresenterAvailableSize.Value.Width)
                {
                    // When the available cells width increases, the horizontal offset can be incorrect.
                    // Store away an adjustment to use during the CellsPresenter's measure, so that the
                    // ShouldDisplayCell method correctly determines if a cell will be in view.
                    //
                    //     |   h. offset   |       new available cells width          |
                    //     |-------------->|----------------------------------------->|
                    //      __________________________________________________        |
                    //     |           |           |             |            |       |
                    //     |  column0  |  column1  |   column2   |  column3   |<----->|
                    //     |           |           |             |            |  adj. |
                    //
                    double adjustment = (_horizontalOffset + value.Value.Width) - ColumnsInternal.VisibleEdgedColumnsWidth;
                    HorizontalAdjustment = Math.Min(HorizontalOffset, Math.Max(0, adjustment));
                }
                else
                {
                    HorizontalAdjustment = 0;
                }
                _rowsPresenterAvailableSize = value;
            }
        }

        internal double[] RowGroupSublevelIndents
        {
            get;
            private set;
        }

        internal int SlotCount
        {
            get;
            private set;
        }

        /// <summary>
        /// Indicates whether or not to use star-sizing logic.  If the DataGrid has infinite available space,
        /// then star sizing doesn't make sense.  In this case, all star columns grow to a predefined size of
        /// 10,000 pixels in order to show the developer that star columns shouldn't be used.
        /// </summary>
        internal bool UsesStarSizing
        {
            get
            {
                if (ColumnsInternal != null)
                {
                    return ColumnsInternal.VisibleStarColumnCount > 0 &&
                        (!RowsPresenterAvailableSize.HasValue || !double.IsPositiveInfinity(RowsPresenterAvailableSize.Value.Width));
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the ScrollViewer used in v2 themes (when UseLogicalScrollable is true).
        /// </summary>
        /// <remarks>
        /// This ScrollViewer wraps the DataGridRowsPresenter and handles scrolling via
        /// ILogicalScrollable. Only available when using v2 theme templates.
        /// </remarks>
        internal ScrollViewer ScrollViewer => _scrollViewer;

        internal int VisibleSlotCount
        {
            get;
            set;
        }

        private int FirstDisplayedNonFillerColumnIndex
        {
            get
            {
                DataGridColumn column = ColumnsInternal.FirstVisibleNonFillerColumn;
                if (column != null)
                {
                    if (column.IsFrozen)
                    {
                        return column.Index;
                    }
                    else
                    {
                        if (DisplayData.FirstDisplayedScrollingCol >= column.Index)
                        {
                            return DisplayData.FirstDisplayedScrollingCol;
                        }
                        else
                        {
                            return column.Index;
                        }
                    }
                }
                return -1;
            }
        }

        /// <inheritdoc/>
        protected override void OnDataContextBeginUpdate()
        {
            base.OnDataContextBeginUpdate();

            NotifyDataContextPropertyForAllRowCells(GetAllRows(), true);
        }

        /// <inheritdoc/>
        protected override void OnDataContextEndUpdate()
        {
            base.OnDataContextEndUpdate();

            NotifyDataContextPropertyForAllRowCells(GetAllRows(), false);
        }

        /// <summary>
        /// Raises the LoadingRow event for row preparation.
        /// </summary>

        /// <summary>
        /// Raises the UnloadingRow event for row recycling.
        /// </summary>

        /// <summary>
        /// Comparator class so we can sort list by the display index
        /// </summary>
        public class DisplayIndexComparer : IComparer<DataGridColumn>
        {
            int IComparer<DataGridColumn>.Compare(DataGridColumn x, DataGridColumn y)
            {
                return (x.DisplayIndexWithFiller < y.DisplayIndexWithFiller) ? -1 : 1;
            }
        }

        internal static DataGridCell GetOwningCell(Control element)
        {
            Debug.Assert(element != null);
            DataGridCell cell = element as DataGridCell;
            while (element != null && cell == null)
            {
                element = element.Parent as Control;
                cell = element as DataGridCell;
            }
            return cell;
        }

        internal bool IsDoubleClickRecordsClickOnCall(Control element)
        {
            if (_clickedElement == element)
            {
                _clickedElement = null;
                return true;
            }
            else
            {
                _clickedElement = element;
                return false;
            }
        }

        private static void NotifyDataContextPropertyForAllRowCells(IEnumerable<DataGridRow> rowSource, bool arg2)
        {
            foreach (DataGridRow row in rowSource)
            {
                foreach (DataGridCell cell in row.Cells)
                {
                    if (cell.Content is StyledElement cellContent)
                    {
                        DataContextProperty.Notifying?.Invoke(cellContent, arg2);
                    }
                }
            }
        }

        private bool IsColumnOutOfBounds(int columnIndex)
        {
            return columnIndex >= ColumnsItemsInternal.Count || columnIndex < 0;
        }

        private bool IsInnerCellOutOfBounds(int columnIndex, int slot)
        {
            return IsColumnOutOfBounds(columnIndex) || IsSlotOutOfBounds(slot);
        }

        private bool IsInnerCellOutOfSelectionBounds(int columnIndex, int slot)
        {
            return IsColumnOutOfBounds(columnIndex) || IsSlotOutOfSelectionBounds(slot);
        }

        private bool IsSlotOutOfBounds(int slot)
        {
            return slot >= SlotCount || slot < -1 || _collapsedSlotsTable.Contains(slot);
        }

        private bool IsSlotOutOfSelectionBounds(int slot)
        {
            if (RowGroupHeadersTable.Contains(slot))
            {
                Debug.Assert(slot >= 0 && slot < SlotCount);
                return false;
            }
            else
            {
                int rowIndex = RowIndexFromSlot(slot);
                return rowIndex < 0 || rowIndex >= DataConnection.Count;
            }
        }

        /// <summary>
        /// Creates the default selection model for the grid. Override to supply a custom model or
        /// set <see cref="SelectionModelFactory"/> before construction completes.
        /// </summary>
        protected virtual ISelectionModel CreateSelectionModel()
        {
            return _selectionModelFactory?.Create() ?? new SelectionModel<object?>();
        }

        /// <summary>
        /// Optional factory used when creating the default selection model.
        /// </summary>
        public IDataGridSelectionModelFactory SelectionModelFactory
        {
            get => _selectionModelFactory;
            set => _selectionModelFactory = value;
        }

        private void SetSelectionModel(ISelectionModel model, bool initializing = false)
        {
            var newModel = model ?? CreateSelectionModel();
            var oldAdapter = _selectionModelAdapter;
            var oldModel = oldAdapter?.Model;

            if (ReferenceEquals(oldModel, newModel))
            {
                return;
            }

            if (newModel.Source != null &&
                DataConnection?.CollectionView != null &&
                !ReferenceEquals(newModel.Source, DataConnection.CollectionView))
            {
                throw new InvalidOperationException(
                    "The supplied ISelectionModel already has an assigned Source but this collection is different to the Items on the control.");
            }

            var removedItems = oldModel?.SelectedItems?.ToArray() ?? Array.Empty<object>();

            DetachSelectionModel();

            _syncingSelectionModel = true;
            try
            {
                _selectionModelAdapter = CreateSelectionModelAdapter(newModel);
                _selectionModelAdapter.Model.SingleSelect = SelectionMode == DataGridSelectionMode.Single;
                _selectionModelAdapter.Model.SelectionChanged += SelectionModel_SelectionChanged;
                _selectionModelAdapter.Model.LostSelection += SelectionModel_LostSelection;
                _selectionModelAdapter.Model.IndexesChanged += SelectionModel_IndexesChanged;
                _selectionModelAdapter.Model.PropertyChanged += SelectionModel_PropertyChanged;
                _selectionModelAdapter.Model.SourceReset += SelectionModel_SourceReset;

                UpdateSelectionModelSource();
            }
            finally
            {
                _syncingSelectionModel = false;
            }

            RaisePropertyChanged(SelectionProperty, oldModel, newModel);
            RaisePropertyChanged(
                SelectedItemsProperty,
                GetSelectedItemsViewOrBinding(oldAdapter),
                SelectedItems);

            ApplySelectionFromSelectionModel();

            if (!initializing && removedItems.Length > 0)
            {
                var args = new SelectionChangedEventArgs(
                    SelectionChangedEvent,
                    removedItems,
                    Array.Empty<object>());
                OnSelectionChanged(args);
            }
        }

        private IList GetSelectedItemsViewOrBinding(DataGridSelectionModelAdapter oldAdapter)
        {
            if (_selectedItemsBinding != null)
            {
                return _selectedItemsBinding;
            }

            if (oldAdapter != null)
            {
                return oldAdapter.SelectedItemsView;
            }

            return _selectedItems;
        }

        private void DetachSelectionModel()
        {
            if (_selectionModelAdapter != null)
            {
                _selectionModelAdapter.Model.SelectionChanged -= SelectionModel_SelectionChanged;
                _selectionModelAdapter.Model.LostSelection -= SelectionModel_LostSelection;
                _selectionModelAdapter.Model.IndexesChanged -= SelectionModel_IndexesChanged;
                _selectionModelAdapter.Model.PropertyChanged -= SelectionModel_PropertyChanged;
                _selectionModelAdapter.Model.SourceReset -= SelectionModel_SourceReset;
            }

            _selectionModelSnapshot = null;
        }

        /// <summary>
        /// Creates the adapter that wraps an <see cref="ISelectionModel"/> for this grid. Override
        /// to customize index/slot mapping or SelectedItems projection.
        /// </summary>
        /// <param name="model">The selection model instance to adapt.</param>
        protected virtual DataGridSelectionModelAdapter CreateSelectionModelAdapter(ISelectionModel model)
        {
            return new DataGridSelectionModelAdapter(model);
        }

        /// <summary>
        /// Maps a visual slot to a selection-model index. Override to customize mapping for grouped
        /// or hierarchical scenarios.
        /// </summary>
        protected virtual int SelectionIndexFromSlot(int slot)
        {
            if (RowGroupHeadersTable.Contains(slot))
            {
                return -1;
            }

            var rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return -1;
            }

            if (TryGetPagingInfo(out var pagedView, out var pageStart))
            {
                return pageStart + rowIndex;
            }

            return rowIndex;
        }

        /// <summary>
        /// Maps a selection-model index back to a visual slot. Override to customize mapping for
        /// grouped or hierarchical scenarios.
        /// </summary>
        protected virtual int SlotFromSelectionIndex(int index)
        {
            if (index < 0 || DataConnection == null)
            {
                return -1;
            }

            if (TryGetPagingInfo(out var pagedView, out var pageStart))
            {
                var localIndex = index - pageStart;
                if (localIndex < 0 || localIndex >= pagedView.Count)
                {
                    return -1;
                }

                return SlotFromRowIndex(localIndex);
            }

            if (index >= DataConnection.Count)
            {
                return -1;
            }

            return SlotFromRowIndex(index);
        }

        private bool TryGetPagingInfo(out DataGridCollectionView view, out int pageStart)
        {
            view = DataConnection?.CollectionView as DataGridCollectionView;
            if (view != null && view.PageSize > 0 && view.PageIndex >= 0)
            {
                pageStart = view.PageIndex * view.PageSize;
                return true;
            }

            pageStart = 0;
            return false;
        }

        private int GetSelectionModelIndexOfItem(object item)
        {
            if (item == null || DataConnection == null)
            {
                return -1;
            }

            if (DataConnection.CollectionView is DataGridCollectionView paged && paged.PageSize > 0)
            {
                return paged.GetGlobalIndexOf(item);
            }

            return DataConnection.IndexOf(item);
        }

        private int GetSelectionIndexFromRowIndex(int rowIndex)
        {
            if (rowIndex < 0)
            {
                return -1;
            }

            if (TryGetPagingInfo(out var pagedView, out var pageStart))
            {
                if (rowIndex >= pagedView.Count)
                {
                    return -1;
                }

                return pageStart + rowIndex;
            }

            return rowIndex;
        }

        internal bool PushSelectionSync()
        {
            var previous = _syncingSelectionModel;
            _syncingSelectionModel = true;
            return previous;
        }

        internal void PopSelectionSync(bool previous)
        {
            _syncingSelectionModel = previous;
        }

        private void RemoveDisplayedColumnHeader(DataGridColumn dataGridColumn)
        {
            if (_columnHeadersPresenter != null)
            {
                _columnHeadersPresenter.Children.Remove(dataGridColumn.HeaderCell);
            }
        }

        private void RemoveDisplayedColumnHeaders()
        {
            if (_columnHeadersPresenter != null)
            {
                _columnHeadersPresenter.Children.Clear();
            }
            ColumnsInternal.FillerColumn.IsRepresented = false;
        }
    }
}
