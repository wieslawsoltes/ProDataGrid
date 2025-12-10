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
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Utilities;
using Avalonia.Threading;
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
using Avalonia.Controls.DataGridSorting;
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
        private Avalonia.Controls.DataGridSorting.ISortingModel _sortingModel;
        private Avalonia.Controls.DataGridSorting.DataGridSortingAdapter _sortingAdapter;
        private Avalonia.Controls.DataGridSorting.IDataGridSortingModelFactory _sortingModelFactory;
        private Avalonia.Controls.DataGridSorting.IDataGridSortingAdapterFactory _sortingAdapterFactory;
        private Avalonia.Controls.DataGridFiltering.IFilteringModel _filteringModel;
        private Avalonia.Controls.DataGridFiltering.DataGridFilteringAdapter _filteringAdapter;
        private Avalonia.Controls.DataGridFiltering.IDataGridFilteringModelFactory _filteringModelFactory;
        private Avalonia.Controls.DataGridFiltering.IDataGridFilteringAdapterFactory _filteringAdapterFactory;
        private Avalonia.Controls.DataGridHierarchical.IHierarchicalModel _hierarchicalModel;
        private Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter _hierarchicalAdapter;
        private Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalModelFactory _hierarchicalModelFactory;
        private Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalAdapterFactory _hierarchicalAdapterFactory;
        private bool _hierarchicalRowsEnabled;
        private int _hierarchicalRefreshSuppressionCount;
        private bool _pendingHierarchicalRefresh;
        private IEnumerable _hierarchicalItemsSource;
        private bool _ownsHierarchicalItemsSource;

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
        private ISelectionModel _selectionModelProxy;
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

            SetSortingModel(CreateSortingModel(), initializing: true);
            SetFilteringModel(CreateFilteringModel(), initializing: true);

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
            get => _selectionModelProxy ?? _selectionModelAdapter?.Model;
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

        /// <summary>
        /// Creates the default sorting model for the grid. Override or set <see cref="SortingModelFactory"/>
        /// before construction completes to supply a custom implementation.
        /// </summary>
        protected virtual ISortingModel CreateSortingModel()
        {
            return _sortingModelFactory?.Create() ?? new SortingModel();
        }

        protected virtual Avalonia.Controls.DataGridHierarchical.IHierarchicalModel CreateHierarchicalModel()
        {
            return _hierarchicalModelFactory?.Create() ?? new Avalonia.Controls.DataGridHierarchical.HierarchicalModel();
        }

        protected virtual Avalonia.Controls.DataGridFiltering.IFilteringModel CreateFilteringModel()
        {
            return _filteringModelFactory?.Create() ?? new Avalonia.Controls.DataGridFiltering.FilteringModel();
        }

        /// <summary>
        /// Optional factory used when creating the default sorting model.
        /// </summary>
        public IDataGridSortingModelFactory SortingModelFactory
        {
            get => _sortingModelFactory;
            set => _sortingModelFactory = value;
        }

        /// <summary>
        /// Optional factory used when creating the default filtering model.
        /// </summary>
        public Avalonia.Controls.DataGridFiltering.IDataGridFilteringModelFactory FilteringModelFactory
        {
            get => _filteringModelFactory;
            set => _filteringModelFactory = value;
        }

        /// <summary>
        /// Optional factory used when creating the default hierarchical model.
        /// </summary>
        public Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalModelFactory HierarchicalModelFactory
        {
            get => _hierarchicalModelFactory;
            set => _hierarchicalModelFactory = value;
        }

        /// <summary>
        /// Optional factory for creating the hierarchical adapter.
        /// </summary>
        public Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalAdapterFactory HierarchicalAdapterFactory
        {
            get => _hierarchicalAdapterFactory;
            set => _hierarchicalAdapterFactory = value;
        }

        /// <summary>
        /// Enables hierarchical row rendering through <see cref="HierarchicalModel"/>.
        /// </summary>
        public bool HierarchicalRowsEnabled
        {
            get => _hierarchicalRowsEnabled;
            set
            {
                if (_hierarchicalRowsEnabled == value)
                {
                    return;
                }

                _hierarchicalRowsEnabled = value;
                if (_hierarchicalRowsEnabled)
                {
                    EnsureHierarchicalItemsSource();
                }
                else
                {
                    DetachHierarchicalItemsSource();
                }
                UpdateSelectionProxy();
            }
        }

        /// <summary>
        /// Optional factory for creating the sorting adapter. Use this to plug in a custom adapter
        /// (e.g., DynamicData/server-side sorting) without subclassing <see cref="DataGrid"/>.
        /// </summary>
        public IDataGridSortingAdapterFactory SortingAdapterFactory
        {
            get => _sortingAdapterFactory;
            set
            {
                if (_sortingAdapterFactory == value)
                {
                    return;
                }

                _sortingAdapterFactory = value;

                if (_sortingModel != null)
                {
                    _sortingAdapter?.Dispose();
                    _sortingAdapter = CreateSortingAdapter(_sortingModel);
                    UpdateSortingAdapterView();
                }
            }
        }

        /// <summary>
        /// Optional factory for creating the filtering adapter. Use this to plug in a custom adapter
        /// (e.g., DynamicData/server-side filtering) without subclassing <see cref="DataGrid"/>.
        /// </summary>
        public Avalonia.Controls.DataGridFiltering.IDataGridFilteringAdapterFactory FilteringAdapterFactory
        {
            get => _filteringAdapterFactory;
            set => _filteringAdapterFactory = value;
        }

        /// <summary>
        /// Enables or disables multi-column sorting (Shift + click).
        /// </summary>
        public bool IsMultiSortEnabled
        {
            get => _sortingModel?.MultiSort ?? true;
            set
            {
                if (_sortingModel != null)
                {
                    _sortingModel.MultiSort = value;
                }
            }
        }

        /// <summary>
        /// Controls the sort direction cycle (two-state or three-state).
        /// </summary>
        public SortCycleMode SortCycleMode
        {
            get => _sortingModel?.CycleMode ?? SortCycleMode.AscendingDescendingNone;
            set
            {
                if (_sortingModel != null)
                {
                    _sortingModel.CycleMode = value;
                }
            }
        }

        /// <summary>
        /// When true the sorting model overwrites the view's SortDescriptions; when false it observes external changes.
        /// </summary>
        public bool OwnsSortDescriptions
        {
            get => _sortingModel?.OwnsViewSorts ?? true;
            set
            {
                if (_sortingModel != null)
                {
                    _sortingModel.OwnsViewSorts = value;
                    _sortingAdapter?.RefreshOwnership();
                }
            }
        }

        /// <summary>
        /// Gets or sets the sorting model that drives column sorting.
        /// </summary>
        public ISortingModel SortingModel
        {
            get => _sortingModel;
            set => SetSortingModel(value);
        }

        /// <summary>
        /// Gets or sets the filtering model that drives column filtering.
        /// </summary>
        public Avalonia.Controls.DataGridFiltering.IFilteringModel FilteringModel
        {
            get => _filteringModel;
            set => SetFilteringModel(value);
        }

        /// <summary>
        /// Gets or sets the hierarchical model that drives tree-like rows.
        /// </summary>
        public Avalonia.Controls.DataGridHierarchical.IHierarchicalModel HierarchicalModel
        {
            get => _hierarchicalModel ?? EnsureHierarchicalModel();
            set => SetHierarchicalModel(value);
        }

        private Avalonia.Controls.DataGridHierarchical.IHierarchicalModel EnsureHierarchicalModel()
        {
            if (_hierarchicalModel == null)
            {
                SetHierarchicalModel(null, initializing: true);
            }

            return _hierarchicalModel!;
        }

        /// <summary>
        /// Creates the adapter that connects the sorting model to the grid.
        /// </summary>
        /// <param name="model">Sorting model instance.</param>
        /// <returns>Adapter that will bridge the model to the collection view and grid.</returns>
        protected virtual DataGridSortingAdapter CreateSortingAdapter(ISortingModel model)
        {
            var adapter = _sortingAdapterFactory?.Create(this, model);

            if (adapter == null)
            {
                if (_hierarchicalRowsEnabled && _hierarchicalModel != null)
                {
                    adapter = new Avalonia.Controls.DataGridHierarchical.HierarchicalSortingAdapter(
                        _hierarchicalModel,
                        model,
                        () => ColumnsItemsInternal,
                        _hierarchicalModel.Options.SiblingComparer,
                        OnSortingAdapterApplying,
                        OnSortingAdapterApplied);
                }
                else
                {
                    adapter = new DataGridSortingAdapter(
                        model,
                        () => ColumnsItemsInternal,
                        OnSortingAdapterApplying,
                        OnSortingAdapterApplied);
                }
            }

            if (adapter == null)
            {
                throw new InvalidOperationException("Sorting adapter factory returned null.");
            }

            adapter.AttachLifecycle(OnSortingAdapterApplying, OnSortingAdapterApplied);
            return adapter;
        }

        /// <summary>
        /// Creates the adapter that connects the filtering model to the grid.
        /// </summary>
        /// <param name="model">Filtering model instance.</param>
        /// <returns>Adapter that will bridge the model to the collection view and grid.</returns>
        protected virtual Avalonia.Controls.DataGridFiltering.DataGridFilteringAdapter CreateFilteringAdapter(Avalonia.Controls.DataGridFiltering.IFilteringModel model)
        {
            var adapter = _filteringAdapterFactory?.Create(this, model)
                ?? new Avalonia.Controls.DataGridFiltering.DataGridFilteringAdapter(
                    model,
                    () => ColumnsItemsInternal,
                    OnFilteringAdapterApplying,
                    OnFilteringAdapterApplied);

            if (adapter == null)
            {
                throw new InvalidOperationException("Filtering adapter factory returned null.");
            }

            adapter.AttachLifecycle(OnFilteringAdapterApplying, OnFilteringAdapterApplied);
            return adapter;
        }

        /// <summary>
        /// Creates the adapter that connects the hierarchical model to the grid.
        /// </summary>
        /// <param name="model">Hierarchical model instance.</param>
        /// <returns>Adapter bridging flattened hierarchy to grid gestures.</returns>
        protected virtual Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter CreateHierarchicalAdapter(Avalonia.Controls.DataGridHierarchical.IHierarchicalModel model)
        {
            var adapter = _hierarchicalAdapterFactory?.Create(this, model)
                ?? new Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter(model);

            if (adapter == null)
            {
                throw new InvalidOperationException("Hierarchical adapter factory returned null.");
            }

            return adapter;
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

            UpdateSelectionProxy();

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

        private void SortingModel_SortingChanged(object sender, SortingChangedEventArgs e)
        {
            RefreshColumnSortStates();
        }

        private void FilteringModel_FilteringChanged(object sender, Avalonia.Controls.DataGridFiltering.FilteringChangedEventArgs e)
        {
            RefreshColumnFilterStates();
        }

        private void OnSortingAdapterApplying()
        {
            UpdateSelectionSnapshot();
        }

        private void OnSortingAdapterApplied()
        {
            if (DataConnection?.CollectionView != null)
            {
                RefreshRowsAndColumns(clearRows: false);
                RestoreSelectionFromSnapshot();
                RefreshSelectionFromModel();
                RefreshColumnSortStates();

                // Some custom adapters (e.g., DynamicData) push sort changes upstream and the
                // resulting collection mutations can arrive asynchronously after this callback.
                // Re-run selection restoration on the UI thread to keep selection stable if the
                // view reorders after the initial restore above.
                Dispatcher.UIThread.Post(() =>
                {
                    if (DataConnection?.CollectionView != null)
                    {
                        RestoreSelectionFromSnapshot();
                        RefreshSelectionFromModel();
                    }
                }, DispatcherPriority.Background);
            }
        }

        private void OnFilteringAdapterApplying()
        {
            UpdateSelectionSnapshot();
        }

        private void OnFilteringAdapterApplied()
        {
            if (DataConnection?.CollectionView != null)
            {
                RefreshRowsAndColumns(clearRows: false);
                RestoreSelectionFromSnapshot();
                RefreshSelectionFromModel();
                RefreshColumnSortStates();
                RefreshColumnFilterStates();
            }
        }

        private void HierarchicalAdapter_FlattenedChanged(object sender, FlattenedChangedEventArgs e)
        {
            HandleHierarchicalFlattenedChanged(e);
        }

        private void HandleHierarchicalFlattenedChanged(FlattenedChangedEventArgs e)
        {
            if (!_hierarchicalRowsEnabled)
            {
                return;
            }

            if (_hierarchicalRefreshSuppressionCount > 0)
            {
                _pendingHierarchicalRefresh = true;
                return;
            }

            var indexMap = e.IndexMap;
            using (_hierarchicalModel?.BeginVirtualizationGuard())
            using (_rowsPresenter?.BeginVirtualizationGuard())
            {
                RemapSelectionForHierarchyChange(indexMap);
                RefreshRowsAndColumns(clearRows: false);
                RefreshSelectionFromModel();
            }
        }

        private void EnsureHierarchicalItemsSource()
        {
            if (!_hierarchicalRowsEnabled || _hierarchicalModel?.ObservableFlattened == null)
            {
                DetachHierarchicalItemsSource();
                return;
            }

            var source = _hierarchicalModel.Flattened;
            var itemsSource = ItemsSource;

            if (!_ownsHierarchicalItemsSource &&
                itemsSource != null &&
                !ReferenceEquals(itemsSource, _hierarchicalItemsSource) &&
                !ReferenceEquals(itemsSource, source))
            {
                return;
            }

            _hierarchicalItemsSource = source;
            _ownsHierarchicalItemsSource = true;
            SetCurrentValue(ItemsSourceProperty, source);
        }

        private void DetachHierarchicalItemsSource()
        {
            if (_ownsHierarchicalItemsSource && ReferenceEquals(ItemsSource, _hierarchicalItemsSource))
            {
                SetCurrentValue(ItemsSourceProperty, null);
            }

            _hierarchicalItemsSource = null;
            _ownsHierarchicalItemsSource = false;
        }

        private void RunHierarchicalAction(Action action)
        {
            if (action == null)
            {
                return;
            }

            _hierarchicalRefreshSuppressionCount++;
            try
            {
                action();
            }
            finally
            {
                _hierarchicalRefreshSuppressionCount = Math.Max(0, _hierarchicalRefreshSuppressionCount - 1);
                if (_hierarchicalRefreshSuppressionCount == 0 && _pendingHierarchicalRefresh)
                {
                    _pendingHierarchicalRefresh = false;
                    if (_hierarchicalRowsEnabled)
                    {
                        RefreshRowsAndColumns(clearRows: false);
                    }
                }
            }
        }

        private void RemapSelectionForHierarchyChange(Avalonia.Controls.DataGridHierarchical.FlattenedIndexMap? indexMap)
        {
            if (indexMap == null || _selectionModelAdapter == null)
            {
                return;
            }

            var model = _selectionModelAdapter.Model;
            var selected = model.SelectedIndexes;
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            var mapped = new List<int>(selected.Count);
            var seen = new HashSet<int>();

            foreach (var index in selected)
            {
                var mappedIndex = indexMap.MapOldIndexToNew(index);
                if (mappedIndex >= 0 && seen.Add(mappedIndex))
                {
                    mapped.Add(mappedIndex);
                }
            }

            var preferredMapped = _preferredSelectionIndex >= 0
                ? indexMap.MapOldIndexToNew(_preferredSelectionIndex)
                : -1;

            var previous = PushSelectionSync();
            var source = model.Source;
            var view = DataConnection?.CollectionView as Avalonia.Collections.DataGridCollectionView;
            try
            {
                if (source != null)
                {
                    // Temporarily detach to avoid collection change callbacks while remapping.
                    model.Source = null;
                }

                using (_selectionModelAdapter.SelectedItemsView.SuppressNotifications())
                using (model.BatchUpdate())
                {
                    model.Clear();
                    foreach (var index in mapped)
                    {
                        model.Select(index);
                    }
                }

                _preferredSelectionIndex = preferredMapped >= 0
                    ? preferredMapped
                    : (mapped.Count > 0 ? mapped[0] : -1);
            }
            finally
            {
                if (source != null)
                {
                    view?.Refresh();
                    if (model.Source != source)
                    {
                        model.Source = source;
                    }
                }

                PopSelectionSync(previous);
            }
        }

        private void UpdateSelectionProxy()
        {
            if (_selectionModelAdapter?.Model != null &&
                _hierarchicalRowsEnabled &&
                _hierarchicalModel != null)
            {
                _selectionModelProxy = new HierarchicalSelectionProxy(
                    _selectionModelAdapter.Model,
                    ProjectHierarchicalSelectionItem,
                    ResolveHierarchicalIndex);
            }
            else
            {
                _selectionModelProxy = null;
            }
        }

        private object? ProjectHierarchicalSelectionItem(object? item)
        {
            return item is Avalonia.Controls.DataGridHierarchical.HierarchicalNode node
                ? node.Item
                : item;
        }

        private int ResolveHierarchicalIndex(object? item)
        {
            if (item == null || _hierarchicalModel == null)
            {
                return -1;
            }

            return _hierarchicalModel.IndexOf(item);
        }

        private void UpdateSortingAdapterView()
        {
            _sortingAdapter?.AttachView(DataConnection?.CollectionView);
            RefreshColumnSortStates();
        }

        private void UpdateFilteringAdapterView()
        {
            _filteringAdapter?.AttachView(DataConnection?.CollectionView);
        }

        internal void RefreshColumnSortStates()
        {
            if (ColumnsItemsInternal == null)
            {
                return;
            }

            foreach (DataGridColumn column in ColumnsItemsInternal)
            {
                column?.HeaderCell?.UpdatePseudoClasses();
            }
        }

        internal void RefreshColumnFilterStates()
        {
            if (ColumnsItemsInternal == null)
            {
                return;
            }

            foreach (DataGridColumn column in ColumnsItemsInternal)
            {
                column?.HeaderCell?.UpdatePseudoClasses();
            }
        }

        private void SetSortingModel(ISortingModel model, bool initializing = false)
        {
            var oldModel = _sortingModel;
            var newModel = model ?? CreateSortingModel();

            if (ReferenceEquals(_sortingModel, newModel))
            {
                return;
            }

            bool ownsViewSorts = oldModel?.OwnsViewSorts ?? newModel.OwnsViewSorts;
            bool multiSort = oldModel?.MultiSort ?? newModel.MultiSort;
            var cycleMode = oldModel?.CycleMode ?? newModel.CycleMode;

            _sortingAdapter?.Dispose();
            _sortingAdapter = null;

            if (oldModel != null)
            {
                oldModel.SortingChanged -= SortingModel_SortingChanged;
            }

            _sortingModel = newModel;
            _sortingModel.MultiSort = multiSort;
            _sortingModel.CycleMode = cycleMode;
            _sortingModel.OwnsViewSorts = ownsViewSorts;
            _sortingModel.SortingChanged += SortingModel_SortingChanged;

            _sortingAdapter = CreateSortingAdapter(_sortingModel);

            if (!initializing)
            {
                UpdateSortingAdapterView();
            }

            RaisePropertyChanged(SortingModelProperty, oldModel, _sortingModel);
        }

        private void SetFilteringModel(Avalonia.Controls.DataGridFiltering.IFilteringModel model, bool initializing = false)
        {
            var oldModel = _filteringModel;
            var newModel = model ?? CreateFilteringModel();

            if (ReferenceEquals(oldModel, newModel))
            {
                return;
            }

            bool ownsView = oldModel?.OwnsViewFilter ?? newModel.OwnsViewFilter;

            _filteringAdapter?.Dispose();
            _filteringAdapter = null;

            if (oldModel != null)
            {
                oldModel.FilteringChanged -= FilteringModel_FilteringChanged;
                oldModel.PropertyChanged -= FilteringModel_PropertyChanged;
            }

            _filteringModel = newModel;
            _filteringModel.OwnsViewFilter = ownsView;
            _filteringModel.FilteringChanged += FilteringModel_FilteringChanged;
            _filteringModel.PropertyChanged += FilteringModel_PropertyChanged;

            _filteringAdapter = CreateFilteringAdapter(_filteringModel);

            if (!initializing)
            {
                UpdateFilteringAdapterView();
            }

            RaisePropertyChanged(FilteringModelProperty, oldModel, _filteringModel);
        }

        private void FilteringModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Avalonia.Controls.DataGridFiltering.IFilteringModel.OwnsViewFilter))
            {
                _filteringAdapter?.AttachView(DataConnection?.CollectionView);
            }
        }

        private void SetHierarchicalModel(Avalonia.Controls.DataGridHierarchical.IHierarchicalModel model, bool initializing = false)
        {
            var oldModel = _hierarchicalModel;
            var newModel = model ?? CreateHierarchicalModel();

            if (ReferenceEquals(_hierarchicalModel, newModel))
            {
                return;
            }

            if (_hierarchicalAdapter != null)
            {
                _hierarchicalAdapter.FlattenedChanged -= HierarchicalAdapter_FlattenedChanged;
                _hierarchicalAdapter.Dispose();
                _hierarchicalAdapter = null;
            }

            if (_hierarchicalModel != null)
            {
                _hierarchicalModel.FlattenedChanged -= HierarchicalAdapter_FlattenedChanged;
            }

            _hierarchicalModel = newModel;

            if (_hierarchicalModel != null)
            {
                _hierarchicalModel.FlattenedChanged += HierarchicalAdapter_FlattenedChanged;
                _hierarchicalAdapter = CreateHierarchicalAdapter(_hierarchicalModel);
            }

            EnsureHierarchicalItemsSource();
            UpdateSelectionProxy();
            RaisePropertyChanged(HierarchicalModelProperty, oldModel, _hierarchicalModel);
        }

        internal SortingDescriptor GetSortingDescriptorForColumn(DataGridColumn column)
        {
            if (column == null || _sortingModel == null)
            {
                return null;
            }

            foreach (var descriptor in _sortingModel.Descriptors)
            {
                if (ReferenceEquals(descriptor.ColumnId, column))
                {
                    return descriptor;
                }

                if (!string.IsNullOrEmpty(descriptor.PropertyPath))
                {
                    var propertyName = column.GetSortPropertyName();
                    if (!string.IsNullOrEmpty(propertyName) &&
                        string.Equals(descriptor.PropertyPath, propertyName, StringComparison.Ordinal))
                    {
                        return descriptor;
                    }
                }
            }

            return null;
        }

        internal ListSortDirection? GetColumnSortDirection(DataGridColumn column)
        {
            return GetSortingDescriptorForColumn(column)?.Direction;
        }

        internal Avalonia.Controls.DataGridFiltering.FilteringDescriptor GetFilteringDescriptorForColumn(DataGridColumn column)
        {
            if (column == null || _filteringModel == null)
            {
                return null;
            }

            foreach (var descriptor in _filteringModel.Descriptors)
            {
                if (ReferenceEquals(descriptor.ColumnId, column))
                {
                    return descriptor;
                }

                if (!string.IsNullOrEmpty(descriptor.PropertyPath))
                {
                    var propertyName = column.GetSortPropertyName();
                    if (!string.IsNullOrEmpty(propertyName) &&
                        string.Equals(descriptor.PropertyPath, propertyName, StringComparison.Ordinal))
                    {
                        return descriptor;
                    }

                    var sortMemberPath = column.SortMemberPath;
                    if (!string.IsNullOrEmpty(sortMemberPath) &&
                        string.Equals(descriptor.PropertyPath, sortMemberPath, StringComparison.Ordinal))
                    {
                        return descriptor;
                    }
                }
            }

            return null;
        }

        internal void ProcessSort(DataGridColumn column, KeyModifiers keyModifiers, ListSortDirection? forcedDirection)
        {
            _sortingAdapter?.HandleHeaderClick(column, keyModifiers, forcedDirection);
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
                _selectionModelAdapter.Dispose();
                _selectionModelAdapter = null;
            }

            _selectionModelProxy = null;
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

        private sealed class HierarchicalSelectionProxy : ISelectionModel
        {
            private readonly ISelectionModel _inner;
            private readonly Func<object?, object?> _itemSelector;
            private readonly Func<object?, int> _indexResolver;

            public HierarchicalSelectionProxy(
                ISelectionModel inner,
                Func<object?, object?> itemSelector,
                Func<object?, int> indexResolver)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _itemSelector = itemSelector ?? throw new ArgumentNullException(nameof(itemSelector));
                _indexResolver = indexResolver ?? throw new ArgumentNullException(nameof(indexResolver));
            }

            public IEnumerable? Source
            {
                get => _inner.Source;
                set => _inner.Source = value;
            }

            public bool SingleSelect
            {
                get => _inner.SingleSelect;
                set => _inner.SingleSelect = value;
            }

            public int SelectedIndex
            {
                get => _inner.SelectedIndex;
                set => _inner.SelectedIndex = value;
            }

            public IReadOnlyList<int> SelectedIndexes => _inner.SelectedIndexes;

            public object? SelectedItem
            {
                get => _itemSelector(_inner.SelectedItem);
                set
                {
                    if (value != null)
                    {
                        var resolved = _indexResolver(value);
                        if (resolved >= 0)
                        {
                            _inner.SelectedIndex = resolved;
                            return;
                        }
                    }

                    _inner.SelectedItem = value;
                }
            }

            public IReadOnlyList<object?> SelectedItems =>
                new ProjectedReadOnlyList(_inner.SelectedItems, _itemSelector);

            public int AnchorIndex
            {
                get => _inner.AnchorIndex;
                set => _inner.AnchorIndex = value;
            }

            public int Count => _inner.Count;

            public event EventHandler<SelectionModelIndexesChangedEventArgs>? IndexesChanged
            {
                add => _inner.IndexesChanged += value;
                remove => _inner.IndexesChanged -= value;
            }

            public event EventHandler<SelectionModelSelectionChangedEventArgs>? SelectionChanged
            {
                add => _inner.SelectionChanged += value;
                remove => _inner.SelectionChanged -= value;
            }

            public event EventHandler? LostSelection
            {
                add => _inner.LostSelection += value;
                remove => _inner.LostSelection -= value;
            }

            public event EventHandler? SourceReset
            {
                add => _inner.SourceReset += value;
                remove => _inner.SourceReset -= value;
            }

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add => _inner.PropertyChanged += value;
                remove => _inner.PropertyChanged -= value;
            }

            public void BeginBatchUpdate() => _inner.BeginBatchUpdate();

            public void EndBatchUpdate() => _inner.EndBatchUpdate();

            public bool IsSelected(int index) => _inner.IsSelected(index);

            public void Select(int index) => _inner.Select(index);

            public void Deselect(int index) => _inner.Deselect(index);

            public void SelectRange(int start, int end) => _inner.SelectRange(start, end);

            public void DeselectRange(int start, int end) => _inner.DeselectRange(start, end);

            public void SelectAll() => _inner.SelectAll();

            public void Clear() => _inner.Clear();

            private sealed class ProjectedReadOnlyList : IReadOnlyList<object?>
            {
                private readonly IReadOnlyList<object?> _inner;
                private readonly Func<object?, object?> _selector;

                public ProjectedReadOnlyList(IReadOnlyList<object?> inner, Func<object?, object?> selector)
                {
                    _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                    _selector = selector ?? throw new ArgumentNullException(nameof(selector));
                }

                public object? this[int index] => _selector(_inner[index]);

                public int Count => _inner.Count;

                public IEnumerator<object?> GetEnumerator()
                {
                    foreach (var item in _inner)
                    {
                        yield return _selector(item);
                    }
                }

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
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
