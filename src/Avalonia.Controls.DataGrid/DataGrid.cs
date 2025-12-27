// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.Selection;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridDragDrop;
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
using System.Diagnostics.CodeAnalysis;
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
using System.Reflection;
using System.Globalization;

namespace Avalonia.Controls
{
    /// <summary>
    /// Displays data in a customizable grid.
    /// </summary>
    [TemplatePart(DATAGRID_elementBottomRightCornerHeaderName,     typeof(Visual))]
    [TemplatePart(DATAGRID_elementColumnHeadersPresenterName,      typeof(DataGridColumnHeadersPresenter))]
    [TemplatePart(DATAGRID_elementFrozenColumnScrollBarSpacerName, typeof(Control))]
    [TemplatePart(DATAGRID_elementFrozenColumnScrollBarSpacerRightName, typeof(Control))]
    [TemplatePart(DATAGRID_elementHorizontalScrollbarName,         typeof(ScrollBar))]
    [TemplatePart(DATAGRID_elementRowsPresenterName,               typeof(DataGridRowsPresenter))]
    [TemplatePart(DATAGRID_elementScrollViewerName,                typeof(ScrollViewer))]
    [TemplatePart(DATAGRID_elementTopLeftCornerHeaderName,         typeof(ContentControl))]
    [TemplatePart(DATAGRID_elementTopRightCornerHeaderName,        typeof(ContentControl))]
    [TemplatePart(DATAGRID_elementVerticalScrollbarName,           typeof(ScrollBar))]
    [PseudoClasses(":invalid", ":empty-rows", ":empty-columns")]
    [RequiresUnreferencedCode("DataGrid inspects data items via reflection and is not compatible with trimming.")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGrid : TemplatedControl
    {
        private const string DATAGRID_elementRowsPresenterName = "PART_RowsPresenter";
        private const string DATAGRID_elementColumnHeadersPresenterName = "PART_ColumnHeadersPresenter";
        private const string DATAGRID_elementFrozenColumnScrollBarSpacerName = "PART_FrozenColumnScrollBarSpacer";
        private const string DATAGRID_elementFrozenColumnScrollBarSpacerRightName = "PART_FrozenColumnScrollBarSpacerRight";
        private const string DATAGRID_elementScrollViewerName = "PART_ScrollViewer";
        private const string DATAGRID_elementTopLeftCornerHeaderName = "PART_TopLeftCornerHeader";
        private const string DATAGRID_elementTopRightCornerHeaderName = "PART_TopRightCornerHeader";
        private const string DATAGRID_elementBottomRightCornerHeaderName = "PART_BottomRightCorner";
        private const string DATAGRID_elementTotalSummaryRowName = "PART_TotalSummaryRow";
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

        /// <summary>
        /// Invoked when preparing a row container for an item.
        /// Mirrors ItemsControl naming while using the DataGrid row pipeline.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void PrepareContainerForItemOverride(DataGridRow element, object item)
        {
            element.DataContext = item;
            element.IsPlaceholder = ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder);
            element.IsValid = true;
            element.ClearDragDropState();
        }

        /// <summary>
        /// Invoked when clearing a row container for reuse or removal.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void ClearContainerForItemOverride(DataGridRow element, object item)
        {
            var previousSuppress = _suppressSelectionUpdatesFromRows;
            _suppressSelectionUpdatesFromRows = true;
            try
            {
                element.IsSelected = false;
            }
            finally
            {
                _suppressSelectionUpdatesFromRows = previousSuppress;
            }

            element.IsPlaceholder = false;
            element.ClearDragDropState();
            element.DataContext = null;
        }

        /// <summary>
        /// Determines whether the provided item is already a DataGridRow container.
        /// </summary>
        protected virtual bool IsItemItsOwnContainerOverride(object item) => item is DataGridRow;

        /// <summary>
        /// Invoked when a row container is being virtualized/recycled.
        /// </summary>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnCleanUpVirtualizedItem(DataGridRow element)
        {
        }

        internal void NotifyRowRecycling(DataGridRow row)
        {
            row.RecycledDataContext ??= row.DataContext;
            row.RecycledIsPlaceholder = row.IsPlaceholder;
            OnCleanUpVirtualizedItem(row);
            ClearContainerForItemOverride(row, row.DataContext);
        }

        internal void NotifyRowPrepared(DataGridRow row, object item)
        {
            PrepareContainerForItemOverride(row, item);
        }

        /// <summary>
        /// Called when the rows presenter viewport changes (virtualization hook).
        /// </summary>
        /// <param name="oldViewport">Previous viewport size.</param>
        /// <param name="newViewport">New viewport size.</param>
        protected internal virtual void OnRowsPresenterViewportChanged(Size oldViewport, Size newViewport)
        {
            if (!UseLogicalScrollable || _rowsPresenter == null)
            {
                return;
            }

            var effectiveHeight = newViewport.Height;
            if (!double.IsNaN(Height) && !double.IsInfinity(Height) && Height > 0)
            {
                var headerHeight = 0.0;
                if (AreColumnHeadersVisible)
                {
                    if (_columnHeadersPresenter != null && _columnHeadersPresenter.Bounds.Height > 0)
                    {
                        headerHeight = _columnHeadersPresenter.Bounds.Height;
                    }
                    else if (!double.IsNaN(ColumnHeaderHeight))
                    {
                        headerHeight = ColumnHeaderHeight;
                    }
                }

                var constrainedHeight = Math.Max(0, Height - headerHeight);
                if (constrainedHeight > 0 && constrainedHeight < effectiveHeight)
                {
                    effectiveHeight = constrainedHeight;
                }
            }

            if (MathUtilities.AreClose(oldViewport.Height, effectiveHeight) &&
                !(MathUtilities.LessThanOrClose(effectiveHeight, 0) && DisplayData.FirstScrollingSlot != -1))
            {
                return;
            }

            if (MathUtilities.LessThanOrClose(effectiveHeight, 0))
            {
                ResetDisplayedRows();
                return;
            }

            if (SlotCount == 0 || ColumnsItemsInternal.Count == 0)
            {
                return;
            }

            if (DisplayData.FirstScrollingSlot == -1)
            {
                int firstVisibleSlot = FirstVisibleSlot;
                if (firstVisibleSlot == -1)
                {
                    return;
                }

                NegVerticalOffset = 0;
                UpdateDisplayedRows(firstVisibleSlot, effectiveHeight);
                return;
            }

            UpdateDisplayedRows(DisplayData.FirstScrollingSlot, effectiveHeight);
        }

        private INotifyCollectionChanged _topLevelGroup;

        private Visual _bottomRightCorner;
        private DataGridColumnHeadersPresenter _columnHeadersPresenter;
        private DataGridRowsPresenter _rowsPresenter;
        private ScrollViewer _scrollViewer;

        private ContentControl _topLeftCornerHeader;
        private ContentControl _topRightCornerHeader;
        private Control _frozenColumnScrollBarSpacer;
        private Control _frozenColumnScrollBarSpacerRight;

        // the sum of the widths in pixels of the scrolling columns preceding
        // the first displayed scrolling column
        private double _horizontalOffset;

        // the number of pixels of the firstDisplayedScrollingCol which are not displayed
        private double _negHorizontalOffset;
        private byte _autoGeneratingColumnOperationCount;
        private bool _areHandlersSuspended;
        private bool _autoSizingColumns;
        private IndexToValueTable<bool> _collapsedSlotsTable;
        private readonly IDataGridScrollStateManager _scrollStateManager;

        // used to store the current column during a Reset

        // this is a workaround only for the scenarios where we need it, it is not all encompassing nor always updated
        private Avalonia.Controls.DataGridSorting.ISortingModel _sortingModel;
        private Avalonia.Controls.DataGridSorting.DataGridSortingAdapter _sortingAdapter;
        private Avalonia.Controls.DataGridSorting.IDataGridSortingModelFactory _sortingModelFactory;
        private Avalonia.Controls.DataGridSorting.IDataGridSortingAdapterFactory _sortingAdapterFactory;
        private bool _syncingColumnSortDirection;
        private Avalonia.Controls.DataGridFiltering.IFilteringModel _filteringModel;
        private Avalonia.Controls.DataGridFiltering.DataGridFilteringAdapter _filteringAdapter;
        private Avalonia.Controls.DataGridFiltering.IDataGridFilteringModelFactory _filteringModelFactory;
        private Avalonia.Controls.DataGridFiltering.IDataGridFilteringAdapterFactory _filteringAdapterFactory;
        private Avalonia.Controls.DataGridSearching.ISearchModel _searchModel;
        private Avalonia.Controls.DataGridSearching.DataGridSearchAdapter _searchAdapter;
        private Avalonia.Controls.DataGridSearching.IDataGridSearchModelFactory _searchModelFactory;
        private Avalonia.Controls.DataGridSearching.IDataGridSearchAdapterFactory _searchAdapterFactory;
        private readonly Dictionary<SearchCellKey, SearchResult> _searchResultsMap = new();
        private readonly HashSet<int> _searchRowMatches = new();
        private SearchCellKey? _currentSearchCell;
        private Avalonia.Controls.DataGridHierarchical.IHierarchicalModel _hierarchicalModel;
        private Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter _hierarchicalAdapter;
        private Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalModelFactory _hierarchicalModelFactory;
        private Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalAdapterFactory _hierarchicalAdapterFactory;
        private bool _hierarchicalRowsEnabled;
        private int _hierarchicalRefreshSuppressionCount;
        private bool _pendingHierarchicalRefresh;
        private HierarchicalAnchorHint? _pendingHierarchicalAnchorHint;
        private double? _pendingHierarchicalScrollOffset;
        private IEnumerable _hierarchicalItemsSource;
        private bool _ownsHierarchicalItemsSource;
        private IDataGridRowDropHandler _rowDropHandler;
        private IDataGridRowDragDropController _rowDragDropController;
        private IDataGridRowDragDropControllerFactory _rowDragDropControllerFactory;
        private DataGridRowDragDropOptions _rowDragDropOptions;
        private IList<DataGridColumn> _boundColumns;
        private IList<DataGridColumn> _boundColumnsList;
        private bool _pendingBoundColumnsApply;
        private INotifyCollectionChanged _boundColumnsNotifications;
        private bool _syncingBoundColumns;
        private bool _syncingInternalColumns;
        private bool _twoWayColumnsAvailable;
        private int? _boundColumnsThreadId;

        // Nth row of rows 0..N that make up the RowHeightEstimate
        private int _lastEstimatedRow;
        private List<DataGridRow> _loadedRows;

        // prevents reentry into the VerticalScroll event handler
        private int? _mouseOverRowIndex;    // -1 is used for the 'new row'
        private Point? _lastPointerPosition;
        private bool _pendingPointerOverRefresh;
        private double[] _rowGroupHeightsByLevel;
        private double _rowHeaderDesiredWidth;
        private Size? _rowsPresenterAvailableSize;
        private bool _scrollingByHeight;
        private IndexToValueTable<bool> _showDetailsTable;
        private DataGridSelectedItemsCollection _selectedItems;
        private IList _selectedItemsBinding;
        private INotifyCollectionChanged _selectedItemsBindingNotifications;
        private IList<DataGridCellInfo> _selectedCellsBinding;
        private INotifyCollectionChanged _selectedCellsBindingNotifications;
        private DataGridSelectionModelAdapter _selectionModelAdapter;
        private ISelectionModel _selectionModelProxy;
        private DataGridSelection.DataGridPagedSelectionSource _pagedSelectionSource;
        private List<object> _selectionModelSnapshot;
        private bool _suppressSelectionSnapshotUpdates;
        private bool _syncingSelectionModel;
        private bool _suppressSelectionUpdatesFromRows;
        private bool _syncingSelectedItems;
        private bool _syncingSelectedCells;
        private readonly Dictionary<int, HashSet<int>> _selectedCells = new();
        private readonly AvaloniaList<DataGridCellInfo> _selectedCellsView = new();
        private DataGridCellCoordinates _cellAnchor = new DataGridCellCoordinates(-1, -1);
        private int _preferredSelectionIndex = -1;
        private IDataGridSelectionModelFactory _selectionModelFactory;
        private bool _autoScrollPending;
        private int _autoScrollRequestToken;
        private bool _autoExpandingSelection;

        // An approximation of the sum of the heights in pixels of the scrolling rows preceding
        // the first displayed scrolling row.  Since the scrolled off rows are discarded, the grid
        // does not know their actual height. The heights used for the approximation are the ones
        // set as the rows were scrolled off.
        private double _verticalOffset;

        /// <summary>
        /// Identifies the <see cref="HorizontalScroll"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridScrollEventArgs> HorizontalScrollEvent =
            RoutedEvent.Register<DataGrid, DataGridScrollEventArgs>(nameof(HorizontalScroll), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="VerticalScroll"/> routed event.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly RoutedEvent<DataGridScrollEventArgs> VerticalScrollEvent =
            RoutedEvent.Register<DataGrid, DataGridScrollEventArgs>(nameof(VerticalScroll), RoutingStrategies.Bubble);

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridScrollEventArgs> HorizontalScroll
        {
            add => AddHandler(HorizontalScrollEvent, value);
            remove => RemoveHandler(HorizontalScrollEvent, value);
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridScrollEventArgs> VerticalScroll
        {
            add => AddHandler(VerticalScrollEvent, value);
            remove => RemoveHandler(VerticalScrollEvent, value);
        }

        /// <summary>
        /// Raises the <see cref="HorizontalScroll"/> event.
        /// </summary>
        /// <param name="e">Scroll event data.</param>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnHorizontalScroll(DataGridScrollEventArgs e)
        {
            e.RoutedEvent ??= HorizontalScrollEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

        /// <summary>
        /// Raises the <see cref="VerticalScroll"/> event.
        /// </summary>
        /// <param name="e">Scroll event data.</param>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual void OnVerticalScroll(DataGridScrollEventArgs e)
        {
            e.RoutedEvent ??= VerticalScrollEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }

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
            FrozenColumnCountRightProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnFrozenColumnCountRightChanged(e));
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
            SelectionUnitProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnSelectionUnitChanged(e));
            VerticalGridLinesBrushProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnVerticalGridLinesBrushChanged(e));
            SelectedIndexProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnSelectedIndexChanged(e));
            SelectedItemProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnSelectedItemChanged(e));
            AutoScrollToSelectedItemProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAutoScrollToSelectedItemChanged(e));
            AutoExpandSelectedItemProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAutoExpandSelectedItemChanged(e));
            IsEnabledProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.DataGrid_IsEnabledChanged(e));
            Visual.IsVisibleProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnIsVisibleChanged(e));
            AreRowGroupHeadersFrozenProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAreRowGroupHeadersFrozenChanged(e));
            RowDetailsTemplateProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDetailsTemplateChanged(e));
            RowDetailsVisibilityModeProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDetailsVisibilityModeChanged(e));
            AutoGenerateColumnsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAutoGenerateColumnsChanged(e));
            RowHeightEstimatorProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowHeightEstimatorChanged(e));
            ColumnHeaderThemeProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnColumnHeaderThemeChanged(e));
            ColumnsSynchronizationModeProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnColumnsSynchronizationModeChanged(e));
            ColumnsSourceResetBehaviorProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnColumnsSourceResetBehaviorChanged(e));
            AutoGeneratedColumnsPlacementProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnAutoGeneratedColumnsPlacementChanged(e));
            ColumnsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnColumnsPropertyChanged(e));
            ColumnDisplayIndexChangedEvent.AddClassHandler<DataGrid>((x, e) => x.OnColumnDisplayIndexChangedBinding(e));
            CanUserReorderRowsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnCanUserReorderRowsChanged(e));
            RowDragHandleProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDragHandleChanged(e));
            RowDragHandleVisibleProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDragHandleVisibleChanged(e));
            RowDropHandlerProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDropHandlerChanged(e));
            RowDragDropControllerFactoryProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDragDropControllerFactoryChanged(e));
            RowDragDropOptionsProperty.Changed.AddClassHandler<DataGrid>((x, e) => x.OnRowDragDropOptionsChanged(e));

            FocusableProperty.OverrideDefaultValue<DataGrid>(true);

            DataGridTypeDescriptorPlugin.EnsureRegistered();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGrid" /> class.
        /// </summary>
        public DataGrid()
        {
            // Handle arrow keys early so grid navigation wins over XYFocus.
            KeyDown += DataGrid_KeyDownDirectional;

            //TODO: Check if override works
            GotFocus += DataGrid_GotFocus;
            LostFocus += DataGrid_LostFocus;
            AddHandler(InputElement.PointerMovedEvent, DataGrid_PointerActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(InputElement.PointerPressedEvent, DataGrid_PointerActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(InputElement.PointerReleasedEvent, DataGrid_PointerActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(InputElement.PointerExitedEvent, DataGrid_PointerExited, handledEventsToo: true);

            _loadedRows = new List<DataGridRow>();
            _lostFocusActions = new Queue<Action>();
            _selectedItems = new DataGridSelectedItemsCollection(this);
            _selectedItems.CollectionChanged += OnSelectedItemsCollectionChanged;
            _selectedCellsView.CollectionChanged += OnSelectedCellsCollectionChanged;
            RowGroupHeadersTable = new IndexToValueTable<DataGridRowGroupInfo>();
            RowGroupFootersTable = new IndexToValueTable<DataGridRowGroupInfo>();
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
            _rowDragDropOptions = new DataGridRowDragDropOptions();
            _rowDropHandler = new DataGridRowReorderHandler();
            _scrollStateManager = new ScrollStateManager(this);

            SetSortingModel(CreateSortingModel(), initializing: true);
            SetFilteringModel(CreateFilteringModel(), initializing: true);
            SetSearchModel(CreateSearchModel(), initializing: true);

            AnchorSlot = -1;
            _lastEstimatedRow = -1;
            _editingColumnIndex = -1;
            _mouseOverRowIndex = null;
            CurrentCellCoordinates = new DataGridCellCoordinates(-1, -1);

            SetSelectionModel(CreateSelectionModel(), initializing: true);

            RowGroupHeaderHeightEstimate = DATAGRID_defaultRowHeight;

            // Initialize summary service
            InitializeSummaryService();

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
            PseudoClassesHelper.Set(PseudoClasses, ":empty-columns", !ColumnsInternal.GetVisibleColumns().Any());
            PseudoClassesHelper.Set(PseudoClasses, ":empty-rows", !DataConnection.Any());
            PseudoClassesHelper.Set(PseudoClasses, ":row-drag-enabled", CanUserReorderRows);
            PseudoClassesHelper.Set(PseudoClasses, ":row-drag-handle-visible", CanUserReorderRows && RowDragHandleVisible);
            PseudoClassesHelper.Set(PseudoClasses, ":summary-top", _totalSummaryPosition == DataGridSummaryRowPosition.Top);
            PseudoClassesHelper.Set(PseudoClasses, ":summary-bottom", _totalSummaryPosition == DataGridSummaryRowPosition.Bottom);
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
        /// Gets or sets the bound columns source (IList + INotifyCollectionChanged enables TwoWay sync).
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IList<DataGridColumn> Columns
        {
            get => _boundColumns ?? ColumnsInternal;
            set => SetColumnsBindingValue(value);
        }

        /// <summary>
        /// Gets the columns collection used for inline or programmatic column definitions.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IReadOnlyList<DataGridColumn> ColumnDefinitions => ColumnsInternal;

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
        /// Gets or sets the collection of selected cells.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IList<DataGridCellInfo> SelectedCells
        {
            get
            {
                if (_selectedCellsBinding != null)
                {
                    return _selectedCellsBinding;
                }

                return _selectedCellsView;
            }
            set => SetSelectedCellsCollection(value);
        }

        /// <summary>
        /// Raised when the set of selected cells changes.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        event EventHandler<DataGridSelectedCellsChangedEventArgs> SelectedCellsChanged;

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
                    var arrangedHeight = _rowsPresenter.LastArrangeHeight;
                    if (!double.IsNaN(arrangedHeight) && arrangedHeight > 0)
                    {
                        return arrangedHeight;
                    }

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
                if (!IsAttachedToVisualTree || !IsVisible)
                {
                    return 0;
                }

                var presenterHeight = ActualRowsPresenterHeight;
                if (RowsPresenterAvailableSize is { } measuredSize &&
                    !double.IsNaN(measuredSize.Height) &&
                    !double.IsInfinity(measuredSize.Height))
                {
                    var measuredHeight = Math.Max(0, measuredSize.Height);
                    if (MathUtilities.GreaterThan(presenterHeight, 0) &&
                        MathUtilities.LessThan(presenterHeight, measuredHeight))
                    {
                        var threshold = Math.Max(RowHeightEstimate, 1);
                        if (VisualRoot is TopLevel rootLevel &&
                            !double.IsNaN(rootLevel.Height) &&
                            rootLevel.Height > 0 &&
                            Math.Abs(rootLevel.Height - rootLevel.Bounds.Height) > threshold &&
                            Math.Abs(Bounds.Height - rootLevel.Bounds.Height) <= threshold)
                        {
                            return measuredHeight;
                        }

                        return presenterHeight;
                    }

                    return measuredHeight;
                }

                if (MathUtilities.GreaterThan(presenterHeight, 0))
                {
                    return presenterHeight;
                }

                if (_scrollStateManager.PendingRestore)
                {
                    var gridHeight = Bounds.Height;
                    if (MathUtilities.GreaterThan(gridHeight, 0))
                    {
                        var headerHeight = 0.0;
                        if (AreColumnHeadersVisible)
                        {
                            if (_columnHeadersPresenter != null)
                            {
                                headerHeight = _columnHeadersPresenter.Bounds.Height;
                            }
                            else if (!double.IsNaN(ColumnHeaderHeight))
                            {
                                headerHeight = ColumnHeaderHeight;
                            }
                        }

                        return Math.Max(0, gridHeight - headerHeight);
                    }
                }

                return 0;
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

        internal IndexToValueTable<DataGridRowGroupInfo> RowGroupFootersTable
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
                        oldMouseOverRow = GetDisplayedRowForIndex(_mouseOverRowIndex.Value);
                    }

                    _mouseOverRowIndex = value;

                    // State for the old row needs to be applied after setting the new value
                    if (oldMouseOverRow != null)
                    {
                        oldMouseOverRow.ApplyState();
                    }

                    if (_mouseOverRowIndex.HasValue)
                    {
                        DataGridRow newMouseOverRow = GetDisplayedRowForIndex(_mouseOverRowIndex.Value);
                        newMouseOverRow?.ApplyState();
                    }
                }
            }
        }

        private void DataGrid_PointerActivity(object? sender, PointerEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            _lastPointerPosition = e.GetPosition(this);
        }

        private void DataGrid_PointerExited(object? sender, PointerEventArgs e)
        {
            _lastPointerPosition = null;
            RequestPointerOverRefresh();
        }

        private void RequestPointerOverRefresh()
        {
            if (_pendingPointerOverRefresh)
            {
                return;
            }

            if (_lastPointerPosition == null && _mouseOverRowIndex == null)
            {
                return;
            }

            _pendingPointerOverRefresh = true;
            LayoutUpdated += DataGrid_LayoutUpdatedPointerOverRefresh;
            Dispatcher.UIThread.Post(() =>
            {
                if (!_pendingPointerOverRefresh)
                {
                    return;
                }

                LayoutUpdated -= DataGrid_LayoutUpdatedPointerOverRefresh;
                _pendingPointerOverRefresh = false;
                RefreshPointerOverRow();
            }, DispatcherPriority.Background);
        }

        private void DataGrid_LayoutUpdatedPointerOverRefresh(object? sender, EventArgs e)
        {
            LayoutUpdated -= DataGrid_LayoutUpdatedPointerOverRefresh;
            if (!_pendingPointerOverRefresh)
            {
                return;
            }

            _pendingPointerOverRefresh = false;
            RefreshPointerOverRow();
        }

        private void RefreshPointerOverRow()
        {
            int? newRowIndex = null;
            if (IsPointerOverSelfOrDescendant() && _lastPointerPosition != null)
            {
                if (TryGetRowFromPoint(_lastPointerPosition.Value, out var row))
                {
                    newRowIndex = row.Index;
                }
            }

            if (_mouseOverRowIndex != newRowIndex)
            {
                MouseOverRowIndex = newRowIndex;
            }

            RefreshPointerOverRowStates();
        }

        private bool IsPointerOverSelfOrDescendant()
        {
            if (IsPointerOver)
            {
                return true;
            }

            if (VisualRoot is IInputRoot inputRoot && inputRoot.PointerOverElement is Visual visual)
            {
                if (visual.VisualRoot != null)
                {
                    for (var current = visual; current != null; current = current.VisualParent)
                    {
                        if (ReferenceEquals(current, this))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            return _lastPointerPosition != null && Bounds.Contains(_lastPointerPosition.Value);
        }

        private void RefreshPointerOverRowStates()
        {
            if (_rowsPresenter == null)
            {
                return;
            }

            foreach (var row in _rowsPresenter.Children.OfType<DataGridRow>())
            {
                row.ApplyState();
            }
        }

        private bool TryGetRowFromPoint(Point point, out DataGridRow? row)
        {
            row = null;

            var visual = this.GetVisualAt(point);
            if (visual is Visual hit)
            {
                row = hit.GetSelfAndVisualAncestors()
                    .OfType<DataGridRow>()
                    .FirstOrDefault(r => r.OwningGrid == this && r.IsVisible);

                if (row != null)
                {
                    var rowOrigin = row.TranslatePoint(new Point(0, 0), this);
                    if (rowOrigin == null ||
                        !new Rect(rowOrigin.Value, row.Bounds.Size).Contains(point))
                    {
                        row = null;
                    }
                }
            }

            if (row == null && _rowsPresenter != null)
            {
                var presenterPoint = this.TranslatePoint(point, _rowsPresenter) ?? point;
                foreach (var candidate in _rowsPresenter.Children.OfType<DataGridRow>())
                {
                    if (!candidate.IsVisible)
                    {
                        continue;
                    }

                    if (!candidate.Bounds.Contains(presenterPoint))
                    {
                        continue;
                    }

                    row = candidate;
                    break;
                }
            }

            if (row == null ||
                row.Index < 0 ||
                ReferenceEquals(row.DataContext, DataGridCollectionView.NewItemPlaceholder))
            {
                row = null;
                return false;
            }

            return true;
        }

        private DataGridRow GetDisplayedRowForIndex(int rowIndex)
        {
            if (rowIndex < 0)
            {
                return null;
            }

            int slot = SlotFromRowIndex(rowIndex);
            if (IsSlotVisible(slot))
            {
                if (DisplayData.GetDisplayedElement(slot) is DataGridRow row)
                {
                    return row;
                }
            }

            foreach (Control element in DisplayData.GetScrollingRows())
            {
                if (element is DataGridRow row && row.Index == rowIndex)
                {
                    return row;
                }
            }

            return null;
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

            if (!ReferenceEquals(oldEstimator, newEstimator) && _scrollStateManager.PendingRestore)
            {
                _scrollStateManager.Clear();
            }

            // Force refresh of displayed rows
            InvalidateMeasure();
        }

        /// <summary>
        /// Re-applies the grid-level column header theme to realized headers that do not override it.
        /// </summary>
        private void OnColumnHeaderThemeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (ColumnsInternal == null)
            {
                return;
            }

            foreach (var column in ColumnsInternal.GetVisibleColumns())
            {
                if (column.HeaderTheme != null || !column.HasHeaderCell)
                {
                    continue;
                }

                column.ApplyHeaderTheme(column.HeaderCell);
            }
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

        internal double GetVisibleFrozenColumnsWidthLeft()
        {
            return ColumnsInternal.GetVisibleFrozenLeftEdgedColumnsWidth();
        }

        internal double GetVisibleFrozenColumnsWidthRight()
        {
            return ColumnsInternal.GetVisibleFrozenRightEdgedColumnsWidth();
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
        #if !DATAGRID_INTERNAL
        public
        #else
        internal
        #endif
        class DisplayIndexComparer : IComparer<DataGridColumn>
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
            if (IsGroupSlot(slot))
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
            var created = _hierarchicalModelFactory?.Create();
            if (created != null)
            {
                return created;
            }

            return new Avalonia.Controls.DataGridHierarchical.HierarchicalModel();
        }

        protected virtual Avalonia.Controls.DataGridFiltering.IFilteringModel CreateFilteringModel()
        {
            return _filteringModelFactory?.Create() ?? new Avalonia.Controls.DataGridFiltering.FilteringModel();
        }

        protected virtual ISearchModel CreateSearchModel()
        {
            return _searchModelFactory?.Create() ?? new SearchModel();
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
        /// Optional factory used when creating the default search model.
        /// </summary>
        public IDataGridSearchModelFactory SearchModelFactory
        {
            get => _searchModelFactory;
            set => _searchModelFactory = value;
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
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IDataGridSortingAdapterFactory SortingAdapterFactory
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
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        Avalonia.Controls.DataGridFiltering.IDataGridFilteringAdapterFactory FilteringAdapterFactory
        {
            get => _filteringAdapterFactory;
            set => _filteringAdapterFactory = value;
        }

        /// <summary>
        /// Optional factory for creating the search adapter. Use this to plug in a custom adapter
        /// (e.g., DynamicData/server-side search) without subclassing <see cref="DataGrid"/>.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IDataGridSearchAdapterFactory SearchAdapterFactory
        {
            get => _searchAdapterFactory;
            set
            {
                if (_searchAdapterFactory == value)
                {
                    return;
                }

                _searchAdapterFactory = value;

                if (_searchModel != null)
                {
                    _searchAdapter?.Dispose();
                    _searchAdapter = CreateSearchAdapter(_searchModel);
                    UpdateSearchAdapterView();
                }
            }
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
        /// Gets or sets the search model that drives global search.
        /// </summary>
        public ISearchModel SearchModel
        {
            get => _searchModel;
            set => SetSearchModel(value);
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
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual DataGridSortingAdapter CreateSortingAdapter(ISortingModel model)
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
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual Avalonia.Controls.DataGridFiltering.DataGridFilteringAdapter CreateFilteringAdapter(Avalonia.Controls.DataGridFiltering.IFilteringModel model)
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
        /// Creates the adapter that connects the search model to the grid.
        /// </summary>
        /// <param name="model">Search model instance.</param>
        /// <returns>Adapter that will compute search results for the view.</returns>
#if !DATAGRID_INTERNAL
        protected
#else
        internal
#endif
        virtual DataGridSearchAdapter CreateSearchAdapter(ISearchModel model)
        {
            var adapter = _searchAdapterFactory?.Create(this, model)
                ?? new DataGridSearchAdapter(
                    model,
                    () => ColumnsItemsInternal);

            if (adapter == null)
            {
                throw new InvalidOperationException("Search adapter factory returned null.");
            }

            return adapter;
        }

        /// <summary>
        /// Creates the adapter that connects the hierarchical model to the grid.
        /// </summary>
        /// <param name="model">Hierarchical model instance.</param>
        /// <returns>Adapter bridging flattened hierarchy to grid gestures.</returns>
        protected virtual Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter CreateHierarchicalAdapter(Avalonia.Controls.DataGridHierarchical.IHierarchicalModel model)
        {
            var adapter = TryCreateTypedHierarchicalAdapter(model)
                ?? _hierarchicalAdapterFactory?.Create(this, model)
                ?? new Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter(model);

            if (adapter == null)
            {
                throw new InvalidOperationException("Hierarchical adapter factory returned null.");
            }

            return adapter;
        }

        private Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter? TryCreateTypedHierarchicalAdapter(Avalonia.Controls.DataGridHierarchical.IHierarchicalModel model)
        {
            if (_hierarchicalAdapterFactory == null)
            {
                return null;
            }

            var modelType = model.GetType();
            var typedModelInterface = modelType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Avalonia.Controls.DataGridHierarchical.IHierarchicalModel<>));
            if (typedModelInterface == null)
            {
                return null;
            }

            var typeArg = typedModelInterface.GenericTypeArguments[0];
            var factoryInterface = _hierarchicalAdapterFactory.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Avalonia.Controls.DataGridHierarchical.IDataGridHierarchicalAdapterFactory<>)
                    && i.GenericTypeArguments[0].IsAssignableFrom(typeArg));

            if (factoryInterface == null)
            {
                return null;
            }

            var createMethod = factoryInterface.GetMethod("Create", new[] { typeof(DataGrid), typedModelInterface });
            if (createMethod == null)
            {
                return null;
            }

            var typedAdapter = createMethod.Invoke(_hierarchicalAdapterFactory, new object[] { this, model });
            if (typedAdapter == null)
            {
                return null;
            }

            var innerProperty = typedAdapter.GetType().GetProperty("InnerAdapter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (innerProperty?.GetValue(typedAdapter) is Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter inner)
            {
                return inner;
            }

            return typedAdapter as Avalonia.Controls.DataGridHierarchical.DataGridHierarchicalAdapter;
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
                // Allow reusing a SelectionModel across DataGrid instances by retargeting its Source
                // to the new grid's view; selection indexes will remap when Source is reassigned.
                newModel.Source = null;
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
            SyncColumnSortDirectionsFromModel();
            RefreshColumnSortStates();
        }

        private void FilteringModel_FilteringChanged(object sender, Avalonia.Controls.DataGridFiltering.FilteringChangedEventArgs e)
        {
            RefreshColumnFilterStates();
        }

        private void SearchModel_ResultsChanged(object sender, SearchResultsChangedEventArgs e)
        {
            UpdateSearchResults(e.NewResults);
            TryRestorePendingSearchCurrent();
        }

        private void SearchModel_CurrentChanged(object sender, SearchCurrentChangedEventArgs e)
        {
            UpdateCurrentSearchResult(e.NewResult);
        }

        private void SearchModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ISearchModel.HighlightMode)
                || e.PropertyName == nameof(ISearchModel.HighlightCurrent))
            {
                RefreshSearchStates();
            }
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
                OnSortingChangedForSummaries();

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
                OnFilterChangedForSummaries();
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

            var hasAnchor = TryGetHierarchicalAnchorHint(e, out var anchor);
            if (!hasAnchor)
            {
                hasAnchor = TryGetHierarchicalAnchor(e, out anchor);
            }
            UpdateRowHeightEstimatorForHierarchicalChange(e.Changes);
            if (hasAnchor)
            {
                var newSlot = SlotFromRowIndex(anchor.NewRowIndex);
                if (newSlot >= 0)
                {
                    var estimator = RowHeightEstimator;
                    var estimatedNewBaseOffset = estimator != null
                        ? EstimateOffsetToVisibleSlot(newSlot, estimator)
                        : newSlot * RowHeightEstimate;
                    var delta = estimatedNewBaseOffset - anchor.EstimatedOldBaseOffset;
                    if (!ChangesAffectAnchor(e.Changes, anchor.OldRowIndex))
                    {
                        delta = 0;
                    }
                    _pendingHierarchicalScrollOffset = Math.Max(0, anchor.ContentOffset + delta - anchor.ViewportOffset);
                }
            }

            var indexMap = e.IndexMap;
            using (_hierarchicalModel?.BeginVirtualizationGuard())
            using (_rowsPresenter?.BeginVirtualizationGuard())
            {
                RemapSelectionForHierarchyChange(indexMap);
                RefreshRowsAndColumns(clearRows: false);
                RefreshSelectionFromModel();
            }

            OnCollectionChangedForSummaries(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private bool TryGetHierarchicalAnchorHint(FlattenedChangedEventArgs e, out HierarchicalAnchor anchor)
        {
            anchor = default;
            if (!_pendingHierarchicalAnchorHint.HasValue)
            {
                return false;
            }

            var hint = _pendingHierarchicalAnchorHint.Value;
            _pendingHierarchicalAnchorHint = null;

            if (!TryMapAnchorIndex(e, hint.OldRowIndex, out var newRowIndex))
            {
                return false;
            }

            anchor = new HierarchicalAnchor(
                hint.OldRowIndex,
                newRowIndex,
                hint.ViewportOffset,
                hint.ContentOffset,
                hint.EstimatedOldBaseOffset);

            return true;
        }

        private bool TryGetHierarchicalAnchor(FlattenedChangedEventArgs e, out HierarchicalAnchor anchor)
        {
            anchor = default;

            if (!UseLogicalScrollable || DisplayData.FirstScrollingSlot < 0 || e == null)
            {
                return false;
            }

            if (!TryGetVisibleAnchorFromChanges(e, out var anchorRowIndex, out var anchorViewportOffset) &&
                !TryGetVisibleAnchorFromFirstRow(out anchorRowIndex, out anchorViewportOffset))
            {
                return false;
            }

            if (!TryMapAnchorIndex(e, anchorRowIndex, out var newRowIndex))
            {
                return false;
            }

            var oldSlot = SlotFromRowIndex(anchorRowIndex);
            if (oldSlot < 0)
            {
                return false;
            }

            var estimator = RowHeightEstimator;
            var estimatedOldBaseOffset = estimator != null
                ? EstimateOffsetToVisibleSlot(oldSlot, estimator)
                : oldSlot * RowHeightEstimate;
            var currentVerticalOffset = GetEffectiveVerticalOffset();
            var contentOffset = currentVerticalOffset + anchorViewportOffset;

            anchor = new HierarchicalAnchor(anchorRowIndex, newRowIndex, anchorViewportOffset, contentOffset, estimatedOldBaseOffset);
            return true;
        }

        private double GetEffectiveVerticalOffset()
        {
            return _verticalOffset;
        }

        internal void PrepareHierarchicalAnchor(int slot)
        {
            if (!UseLogicalScrollable || slot < 0 || DisplayData.FirstScrollingSlot < 0)
            {
                return;
            }

            if (IsGroupSlot(slot))
            {
                return;
            }

            if (slot < DisplayData.FirstScrollingSlot || slot > DisplayData.LastScrollingSlot)
            {
                return;
            }

            var rowIndex = RowIndexFromSlot(slot);
            if (rowIndex < 0)
            {
                return;
            }

            if (!TryGetAnchorViewportOffset(slot, out var viewportOffset))
            {
                viewportOffset = -NegVerticalOffset;
            }

            var estimator = RowHeightEstimator;
            var estimatedOldBaseOffset = estimator != null
                ? EstimateOffsetToVisibleSlot(slot, estimator)
                : slot * RowHeightEstimate;
            var contentOffset = GetEffectiveVerticalOffset() + viewportOffset;

            _pendingHierarchicalAnchorHint = new HierarchicalAnchorHint(
                rowIndex,
                viewportOffset,
                contentOffset,
                estimatedOldBaseOffset);

            Dispatcher.UIThread.Post(() => _pendingHierarchicalAnchorHint = null, DispatcherPriority.Background);
        }

        private readonly struct HierarchicalAnchor
        {
            public HierarchicalAnchor(
                int oldRowIndex,
                int newRowIndex,
                double viewportOffset,
                double contentOffset,
                double estimatedOldBaseOffset)
            {
                OldRowIndex = oldRowIndex;
                NewRowIndex = newRowIndex;
                ViewportOffset = viewportOffset;
                ContentOffset = contentOffset;
                EstimatedOldBaseOffset = estimatedOldBaseOffset;
            }

            public int OldRowIndex { get; }

            public int NewRowIndex { get; }

            public double ViewportOffset { get; }

            public double ContentOffset { get; }

            public double EstimatedOldBaseOffset { get; }
        }

        private readonly struct HierarchicalAnchorHint
        {
            public HierarchicalAnchorHint(
                int oldRowIndex,
                double viewportOffset,
                double contentOffset,
                double estimatedOldBaseOffset)
            {
                OldRowIndex = oldRowIndex;
                ViewportOffset = viewportOffset;
                ContentOffset = contentOffset;
                EstimatedOldBaseOffset = estimatedOldBaseOffset;
            }

            public int OldRowIndex { get; }

            public double ViewportOffset { get; }

            public double ContentOffset { get; }

            public double EstimatedOldBaseOffset { get; }
        }

        private bool TryGetVisibleAnchorFromChanges(FlattenedChangedEventArgs e, out int anchorRowIndex, out double anchorViewportOffset)
        {
            anchorRowIndex = -1;
            anchorViewportOffset = 0;

            if (e?.Changes == null || e.Changes.Count == 0)
            {
                return false;
            }

            foreach (var change in e.Changes)
            {
                if (change.OldCount == change.NewCount)
                {
                    continue;
                }

                var candidateRowIndex = change.Index - 1;
                if (candidateRowIndex < 0)
                {
                    continue;
                }

                var candidateSlot = SlotFromRowIndex(candidateRowIndex);
                if (candidateSlot < DisplayData.FirstScrollingSlot || candidateSlot > DisplayData.LastScrollingSlot)
                {
                    continue;
                }

                if (IsGroupSlot(candidateSlot))
                {
                    continue;
                }

                if (!IsHierarchicalAnchorCandidate(candidateSlot, change.NewCount > change.OldCount))
                {
                    continue;
                }

                if (TryGetAnchorViewportOffset(candidateSlot, out anchorViewportOffset))
                {
                    anchorRowIndex = candidateRowIndex;
                    return true;
                }
            }

            return false;
        }

        private bool IsHierarchicalAnchorCandidate(int slot, bool isExpanding)
        {
            var row = DisplayData.GetDisplayedElement(slot) as DataGridRow;
            if (row?.DataContext is not HierarchicalNode node)
            {
                return true;
            }

            return isExpanding ? node.IsExpanded : !node.IsExpanded;
        }

        private bool TryGetVisibleAnchorFromFirstRow(out int anchorRowIndex, out double anchorViewportOffset)
        {
            anchorRowIndex = -1;
            anchorViewportOffset = 0;

            var slot = DisplayData.FirstScrollingSlot;
            if (slot < 0)
            {
                return false;
            }

            while (slot >= 0 && slot <= DisplayData.LastScrollingSlot)
            {
                if (!IsGroupSlot(slot))
                {
                    var rowIndex = RowIndexFromSlot(slot);
                    if (rowIndex >= 0 && TryGetAnchorViewportOffset(slot, out anchorViewportOffset))
                    {
                        anchorRowIndex = rowIndex;
                        return true;
                    }
                }

                slot = GetNextVisibleSlot(slot);
            }

            return false;
        }

        private bool TryGetAnchorViewportOffset(int slot, out double anchorViewportOffset)
        {
            anchorViewportOffset = 0;
            var element = DisplayData.GetDisplayedElement(slot);
            if (element is Control control)
            {
                var offset = control.Bounds.Y;
                if (!double.IsNaN(offset) && !double.IsInfinity(offset))
                {
                    if (!IsAnchorVisible(offset, control.Bounds.Height))
                    {
                        return false;
                    }

                    anchorViewportOffset = offset;
                    return true;
                }
            }

            var currentSlot = DisplayData.FirstScrollingSlot;
            var y = -NegVerticalOffset;
            while (currentSlot >= 0 && currentSlot <= DisplayData.LastScrollingSlot)
            {
                var currentElement = DisplayData.GetDisplayedElement(currentSlot);
                if (currentElement == null)
                {
                    return false;
                }

                if (currentSlot == slot)
                {
                    if (!IsAnchorVisible(y, currentElement.DesiredSize.Height))
                    {
                        return false;
                    }

                    anchorViewportOffset = y;
                    return true;
                }

                y += currentElement.DesiredSize.Height;
                currentSlot = GetNextVisibleSlot(currentSlot);
            }

            return false;
        }

        private bool IsAnchorVisible(double top, double height)
        {
            if (double.IsNaN(top) || double.IsInfinity(top))
            {
                return false;
            }

            if (double.IsNaN(height) || double.IsInfinity(height) || MathUtilities.LessThanOrClose(height, 0))
            {
                height = RowHeightEstimate;
            }

            var viewportHeight = GetAnchorViewportHeight();
            if (!viewportHeight.HasValue || MathUtilities.LessThanOrClose(viewportHeight.Value, 0))
            {
                return true;
            }

            return top + height > 0 && top < viewportHeight.Value;
        }

        private double? GetAnchorViewportHeight()
        {
            if (_rowsPresenter != null && _rowsPresenter.Viewport.Height > 0)
            {
                return _rowsPresenter.Viewport.Height;
            }

            if (RowsPresenterAvailableSize is { Height: > 0 })
            {
                return RowsPresenterAvailableSize.Value.Height;
            }

            if (_rowsPresenter != null && _rowsPresenter.Bounds.Height > 0)
            {
                return _rowsPresenter.Bounds.Height;
            }

            return null;
        }

        private bool TryMapAnchorIndex(FlattenedChangedEventArgs e, int oldRowIndex, out int newRowIndex)
        {
            newRowIndex = e.IndexMap.MapOldIndexToNew(oldRowIndex);
            if (newRowIndex < 0)
            {
                newRowIndex = GetFallbackHierarchicalIndex(oldRowIndex, e.Changes, e.IndexMap.NewCount);
            }

            return newRowIndex >= 0;
        }

        private bool ChangesAffectAnchor(IReadOnlyList<FlattenedChange> changes, int anchorRowIndex)
        {
            if (changes == null || changes.Count == 0)
            {
                return false;
            }

            foreach (var change in changes)
            {
                if (change.Index <= anchorRowIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetFallbackHierarchicalIndex(int oldRowIndex, IReadOnlyList<FlattenedChange> changes, int newCount)
        {
            if (newCount <= 0)
            {
                return -1;
            }

            if (changes != null)
            {
                foreach (var change in changes)
                {
                    if (oldRowIndex < change.Index)
                    {
                        break;
                    }

                    if (oldRowIndex < change.Index + change.OldCount)
                    {
                        var candidate = change.Index > 0 ? change.Index - 1 : 0;
                        return Math.Min(candidate, newCount - 1);
                    }
                }
            }

            return Math.Min(oldRowIndex, newCount - 1);
        }

        private void UpdateRowHeightEstimatorForHierarchicalChange(IReadOnlyList<FlattenedChange> changes)
        {
            if (RowHeightEstimator == null || changes == null || changes.Count == 0)
            {
                return;
            }

            foreach (var change in changes)
            {
                if (change.OldCount == 0 && change.NewCount == 0)
                {
                    continue;
                }

                var slot = SlotFromRowIndex(change.Index);
                if (slot < 0)
                {
                    continue;
                }

                if (change.OldCount > 0)
                {
                    RowHeightEstimator.OnItemsRemoved(slot, change.OldCount);
                }

                if (change.NewCount > 0)
                {
                    RowHeightEstimator.OnItemsInserted(slot, change.NewCount);
                }
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

                if (mapped.Count == 0 && DataConnection?.CollectionView != null)
                {
                    DataConnection.CollectionView.MoveCurrentTo(null);
                }

                UpdateSelectionSnapshot();
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

        private object? ProjectSelectionItem(object? item)
        {
            return _hierarchicalRowsEnabled ? ProjectHierarchicalSelectionItem(item) : item;
        }

        private int ResolveHierarchicalIndex(object? item)
        {
            if (item == null || _hierarchicalModel == null)
            {
                return -1;
            }

            if (item is Avalonia.Controls.DataGridHierarchical.HierarchicalNode node)
            {
                return _hierarchicalModel.IndexOf(node);
            }

            return _hierarchicalModel.IndexOf(item);
        }

        private int ResolveSelectionIndex(object? item)
        {
            if (item == null)
            {
                return -1;
            }

            return GetSelectionModelIndexOfItem(item);
        }

        private void UpdateSortingAdapterView()
        {
            _sortingAdapter?.AttachView(DataConnection?.CollectionView);
            SyncColumnSortDirectionsFromModel();
            RefreshColumnSortStates();
        }

        private void UpdateFilteringAdapterView()
        {
            _filteringAdapter?.AttachView(DataConnection?.CollectionView);
        }

        private void UpdateSearchAdapterView()
        {
            _searchAdapter?.AttachView(DataConnection?.CollectionView);
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

        internal bool TryGetSearchCellState(int rowIndex, DataGridColumn column, out bool isMatch, out bool isCurrent)
        {
            isMatch = false;
            isCurrent = false;

            if (_searchModel == null || _searchModel.HighlightMode == SearchHighlightMode.None)
            {
                return false;
            }

            if (rowIndex < 0 || column == null)
            {
                return false;
            }

            var key = new SearchCellKey(rowIndex, column);
            isMatch = _searchResultsMap.ContainsKey(key);

            if (_searchModel.HighlightCurrent && _currentSearchCell.HasValue)
            {
                isCurrent = _currentSearchCell.Value.Equals(key) && isMatch;
            }

            return isMatch || isCurrent;
        }

        private bool TryGetSearchResult(int rowIndex, DataGridColumn column, out SearchResult result)
        {
            if (rowIndex < 0 || column == null)
            {
                result = null;
                return false;
            }

            return _searchResultsMap.TryGetValue(new SearchCellKey(rowIndex, column), out result);
        }

        private void UpdateSearchResults(IReadOnlyList<SearchResult> results)
        {
            _searchResultsMap.Clear();
            _searchRowMatches.Clear();

            if (results != null)
            {
                foreach (var result in results)
                {
                    if (result == null)
                    {
                        continue;
                    }

                    var column = ResolveSearchColumn(result);
                    if (column == null)
                    {
                        continue;
                    }

                    var key = new SearchCellKey(result.RowIndex, column);
                    if (!_searchResultsMap.ContainsKey(key))
                    {
                        _searchResultsMap[key] = result;
                    }

                    _searchRowMatches.Add(result.RowIndex);
                }
            }

            RefreshSearchStates();
        }

        private void UpdateCurrentSearchResult(SearchResult result)
        {
            _currentSearchCell = BuildSearchKey(result);

            if (result == null)
            {
                RefreshSearchStates();
                return;
            }

            var column = ResolveSearchColumn(result);
            if (column == null)
            {
                RefreshSearchStates();
                return;
            }

            if (IsAttachedToVisualTree)
            {
                var slot = SlotFromRowIndex(result.RowIndex);
                if (slot >= 0 && !IsSlotOutOfBounds(slot))
                {
                    ScrollIntoView(result.Item, column);

                    if (_searchModel?.UpdateSelectionOnNavigate == true)
                    {
                        UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
                    }
                }
            }

            RefreshSearchStates();
        }

        private void RefreshSearchStates()
        {
            if (_rowsPresenter == null || _searchModel == null)
            {
                return;
            }

            var highlightMode = _searchModel.HighlightMode;
            bool highlightMatches = highlightMode != SearchHighlightMode.None;
            bool highlightCurrent = highlightMatches && _searchModel.HighlightCurrent;

            foreach (Control element in DisplayData.GetScrollingRows())
            {
                if (element is not DataGridRow row)
                {
                    continue;
                }

                UpdateSearchStatesForRow(row, highlightMode, highlightMatches, highlightCurrent);
            }
        }

        private void UpdateSearchStatesForRow(
            DataGridRow row,
            SearchHighlightMode highlightMode,
            bool highlightMatches,
            bool highlightCurrent)
        {
            if (row == null)
            {
                return;
            }

            var rowIndex = row.Index;
            bool rowHasMatch = highlightMatches && _searchRowMatches.Contains(rowIndex);
            bool rowIsCurrent = highlightCurrent && _currentSearchCell.HasValue
                && _currentSearchCell.Value.RowIndex == rowIndex
                && _searchResultsMap.ContainsKey(_currentSearchCell.Value);

            row.UpdateSearchPseudoClasses(rowHasMatch, rowIsCurrent);

            if (row.Cells == null)
            {
                return;
            }

            foreach (DataGridCell cell in row.Cells)
            {
                if (cell?.OwningColumn == null)
                {
                    continue;
                }

                SearchResult result = null;
                bool cellHasMatch = highlightMatches && TryGetSearchResult(rowIndex, cell.OwningColumn, out result);
                bool cellIsCurrent = highlightCurrent && _currentSearchCell.HasValue
                    && _currentSearchCell.Value.Equals(new SearchCellKey(rowIndex, cell.OwningColumn))
                    && cellHasMatch;

                cell.UpdatePseudoClasses();
                UpdateSearchTextPresenter(cell, result, cellIsCurrent, highlightMode);
            }
        }

        private void UpdateSearchTextPresenter(
            DataGridCell cell,
            SearchResult result,
            bool isCurrent,
            SearchHighlightMode highlightMode)
        {
            if (cell?.Content is not DataGridSearchTextBlock textBlock)
            {
                return;
            }

            textBlock.HighlightMode = highlightMode;

            if (highlightMode == SearchHighlightMode.TextAndCell && result != null && result.Matches.Count > 0)
            {
                textBlock.SearchText = result.Text;
                textBlock.SearchMatches = result.Matches;
                textBlock.IsSearchCurrent = isCurrent;
            }
            else
            {
                textBlock.SearchText = null;
                textBlock.SearchMatches = Array.Empty<SearchMatch>();
                textBlock.IsSearchCurrent = false;
            }
        }

        private SearchCellKey? BuildSearchKey(SearchResult result)
        {
            if (result == null)
            {
                return null;
            }

            var column = ResolveSearchColumn(result);
            if (column == null)
            {
                return null;
            }

            return new SearchCellKey(result.RowIndex, column);
        }

        private DataGridColumn ResolveSearchColumn(SearchResult result)
        {
            if (result == null)
            {
                return null;
            }

            if (result.ColumnId is DataGridColumn column)
            {
                return column;
            }

            if (result.ColumnId is string path)
            {
                return FindColumnBySearchPath(path);
            }

            if (result.ColumnIndex >= 0 && ColumnsItemsInternal != null && result.ColumnIndex < ColumnsItemsInternal.Count)
            {
                return ColumnsItemsInternal[result.ColumnIndex];
            }

            return null;
        }

        private DataGridColumn FindColumnBySearchPath(string path)
        {
            if (ColumnsItemsInternal == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            foreach (var column in ColumnsItemsInternal)
            {
                if (column == null)
                {
                    continue;
                }

                var searchPath = DataGridColumnSearch.GetSearchMemberPath(column);
                if (!string.IsNullOrEmpty(searchPath) &&
                    string.Equals(searchPath, path, StringComparison.Ordinal))
                {
                    return column;
                }

                var propertyPath = column.GetSortPropertyName();
                if (!string.IsNullOrEmpty(propertyPath) &&
                    string.Equals(propertyPath, path, StringComparison.Ordinal))
                {
                    return column;
                }
            }

            return null;
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

            SyncColumnSortDirectionsFromModel();
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

            bool ownsView = model != null
                ? newModel.OwnsViewFilter
                : oldModel?.OwnsViewFilter ?? newModel.OwnsViewFilter;

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

        private void SetSearchModel(ISearchModel model, bool initializing = false)
        {
            var oldModel = _searchModel;
            var newModel = model ?? CreateSearchModel();

            if (ReferenceEquals(oldModel, newModel))
            {
                return;
            }

            var highlightMode = oldModel?.HighlightMode ?? newModel.HighlightMode;
            var highlightCurrent = oldModel?.HighlightCurrent ?? newModel.HighlightCurrent;
            var updateSelection = oldModel?.UpdateSelectionOnNavigate ?? newModel.UpdateSelectionOnNavigate;
            var wrapNavigation = oldModel?.WrapNavigation ?? newModel.WrapNavigation;

            _searchAdapter?.Dispose();
            _searchAdapter = null;

            if (oldModel != null)
            {
                oldModel.ResultsChanged -= SearchModel_ResultsChanged;
                oldModel.CurrentChanged -= SearchModel_CurrentChanged;
                oldModel.PropertyChanged -= SearchModel_PropertyChanged;
            }

            _searchModel = newModel;
            _searchModel.HighlightMode = highlightMode;
            _searchModel.HighlightCurrent = highlightCurrent;
            _searchModel.UpdateSelectionOnNavigate = updateSelection;
            _searchModel.WrapNavigation = wrapNavigation;

            _searchModel.ResultsChanged += SearchModel_ResultsChanged;
            _searchModel.CurrentChanged += SearchModel_CurrentChanged;
            _searchModel.PropertyChanged += SearchModel_PropertyChanged;

            _searchAdapter = CreateSearchAdapter(_searchModel);

            if (!initializing)
            {
                UpdateSearchAdapterView();
            }

            RefreshSearchStates();
            RaisePropertyChanged(SearchModelProperty, oldModel, _searchModel);
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

        internal void OnColumnSortDirectionChanged(DataGridColumn column, ListSortDirection? direction)
        {
            if (_sortingModel == null || _syncingColumnSortDirection)
            {
                return;
            }

            if (ColumnsInternal != null &&
                (column == ColumnsInternal.FillerColumn || column == ColumnsInternal.RowGroupSpacerColumn))
            {
                return;
            }

            ApplyColumnSortDirection(column, direction);
        }

        internal void OnColumnCustomSortComparerChanged(DataGridColumn column)
        {
            if (_sortingModel == null || _syncingColumnSortDirection)
            {
                return;
            }

            var direction = GetColumnSortDirection(column);
            if (direction.HasValue || column.SortDirection.HasValue)
            {
                ApplyColumnSortDirection(column, column.SortDirection ?? direction);
                SyncColumnSortDirectionsFromModel();
            }
        }

        internal void InitializeColumnSortDirection(DataGridColumn column)
        {
            if (column == null || _sortingModel == null)
            {
                return;
            }

            var descriptor = GetSortingDescriptorForColumn(column);
            if (descriptor != null)
            {
                SetColumnSortDirectionValue(column, descriptor.Direction);
                return;
            }

            if (column.SortDirection.HasValue)
            {
                ApplyColumnSortDirection(column, column.SortDirection);
            }
            else
            {
                SetColumnSortDirectionValue(column, null);
            }
        }

        private void ApplyColumnSortDirection(DataGridColumn column, ListSortDirection? direction)
        {
            if (column == null || _sortingModel == null)
            {
                return;
            }

            if (ColumnsInternal != null &&
                (column == ColumnsInternal.FillerColumn || column == ColumnsInternal.RowGroupSpacerColumn))
            {
                return;
            }

            if (direction.HasValue)
            {
                var descriptor = CreateSortingDescriptorForColumn(column, direction.Value);
                if (descriptor == null)
                {
                    SetColumnSortDirectionValue(column, null);
                    return;
                }

                _sortingModel.SetOrUpdate(descriptor);
            }
            else
            {
                var existing = GetSortingDescriptorForColumn(column);
                if (existing != null)
                {
                    _sortingModel.Remove(existing.ColumnId);
                }
                else
                {
                    _sortingModel.Remove(column);
                }
            }
        }

        private SortingDescriptor CreateSortingDescriptorForColumn(DataGridColumn column, ListSortDirection direction)
        {
            if (column == null)
            {
                return null;
            }

            var existing = GetSortingDescriptorForColumn(column);
            var viewCulture = (DataConnection?.CollectionView as IDataGridCollectionView)?.Culture;
            var culture = viewCulture ?? existing?.Culture ?? CultureInfo.InvariantCulture;
            var comparer = column.CustomSortComparer ?? existing?.Comparer;
            var propertyPath = column.GetSortPropertyName();

            if (string.IsNullOrEmpty(propertyPath))
            {
                propertyPath = existing?.PropertyPath;
            }

            if (comparer == null && string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            var columnId = existing?.ColumnId ?? (object)column;
            return new SortingDescriptor(columnId, direction, propertyPath, comparer, culture);
        }

        private void SetColumnSortDirectionValue(DataGridColumn column, ListSortDirection? direction)
        {
            if (column == null)
            {
                return;
            }

            _syncingColumnSortDirection = true;
            try
            {
                column.SetCurrentValue(DataGridColumn.SortDirectionProperty, direction);
            }
            finally
            {
                _syncingColumnSortDirection = false;
            }
        }

        private void SyncColumnSortDirectionsFromModel()
        {
            if (_sortingModel == null || ColumnsItemsInternal == null)
            {
                return;
            }

            _syncingColumnSortDirection = true;
            try
            {
                foreach (var column in ColumnsItemsInternal)
                {
                    if (column == null ||
                        column == ColumnsInternal.FillerColumn ||
                        column == ColumnsInternal.RowGroupSpacerColumn)
                    {
                        continue;
                    }

                    var descriptor = GetSortingDescriptorForColumn(column);
                    column.SetCurrentValue(DataGridColumn.SortDirectionProperty, descriptor?.Direction);
                }
            }
            finally
            {
                _syncingColumnSortDirection = false;
            }
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
            return new DataGridSelectionModelAdapter(model, ProjectSelectionItem, ResolveSelectionIndex);
        }

        /// <summary>
        /// Maps a visual slot to a selection-model index. Override to customize mapping for grouped
        /// or hierarchical scenarios.
        /// </summary>
        protected virtual int SelectionIndexFromSlot(int slot)
        {
            if (IsGroupSlot(slot))
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

            if (_hierarchicalRowsEnabled && _hierarchicalModel != null &&
                item is not Avalonia.Controls.DataGridHierarchical.HierarchicalNode)
            {
                var hierarchicalIndex = _hierarchicalModel.IndexOf(item);
                if (hierarchicalIndex >= 0)
                {
                    return hierarchicalIndex;
                }
            }

            if (DataConnection.CollectionView is DataGridCollectionView paged && paged.PageSize > 0)
            {
                return paged.GetGlobalIndexOf(item);
            }

            return DataConnection.IndexOf(item);
        }

        private bool TryGetRowIndexFromItem(object? item, out int rowIndex)
        {
            rowIndex = -1;
            if (item == null || DataConnection == null)
            {
                return false;
            }

            rowIndex = DataConnection.IndexOf(item);
            if (rowIndex >= 0)
            {
                return true;
            }

            if (_hierarchicalRowsEnabled && _hierarchicalModel != null)
            {
                if (item is Avalonia.Controls.DataGridHierarchical.HierarchicalNode node)
                {
                    rowIndex = DataConnection.IndexOf(node);
                }
                else if (_hierarchicalModel.FindNode(item) is { } resolvedNode)
                {
                    rowIndex = DataConnection.IndexOf(resolvedNode);
                }
            }

            return rowIndex >= 0;
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
            private EventHandler<SelectionModelIndexesChangedEventArgs>? _indexesChanged;
            private EventHandler<SelectionModelSelectionChangedEventArgs>? _selectionChanged;
            private EventHandler? _lostSelection;
            private EventHandler? _sourceReset;
            private PropertyChangedEventHandler? _propertyChanged;
            private bool _lastSelectionEmpty;

            public HierarchicalSelectionProxy(
                ISelectionModel inner,
                Func<object?, object?> itemSelector,
                Func<object?, int> indexResolver)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _itemSelector = itemSelector ?? throw new ArgumentNullException(nameof(itemSelector));
                _indexResolver = indexResolver ?? throw new ArgumentNullException(nameof(indexResolver));
                _lastSelectionEmpty = inner.SelectedIndexes.Count == 0;
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
                add
                {
                    if (_indexesChanged == null)
                    {
                        _inner.IndexesChanged += InnerIndexesChanged;
                    }
                    _indexesChanged += value;
                }
                remove
                {
                    _indexesChanged -= value;
                    if (_indexesChanged == null)
                    {
                        _inner.IndexesChanged -= InnerIndexesChanged;
                    }
                }
            }

            public event EventHandler<SelectionModelSelectionChangedEventArgs>? SelectionChanged
            {
                add
                {
                    if (_selectionChanged == null)
                    {
                        _inner.SelectionChanged += InnerSelectionChanged;
                    }
                    _selectionChanged += value;
                }
                remove
                {
                    _selectionChanged -= value;
                    if (_selectionChanged == null)
                    {
                        _inner.SelectionChanged -= InnerSelectionChanged;
                    }
                }
            }

            public event EventHandler? LostSelection
            {
                add
                {
                    if (_lostSelection == null)
                    {
                        _inner.LostSelection += InnerLostSelection;
                    }
                    _lostSelection += value;
                }
                remove
                {
                    _lostSelection -= value;
                    if (_lostSelection == null)
                    {
                        _inner.LostSelection -= InnerLostSelection;
                    }
                }
            }

            public event EventHandler? SourceReset
            {
                add
                {
                    if (_sourceReset == null)
                    {
                        _inner.SourceReset += InnerSourceReset;
                    }
                    _sourceReset += value;
                }
                remove
                {
                    _sourceReset -= value;
                    if (_sourceReset == null)
                    {
                        _inner.SourceReset -= InnerSourceReset;
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged
            {
                add
                {
                    if (_propertyChanged == null)
                    {
                        _inner.PropertyChanged += InnerPropertyChanged;
                    }
                    _propertyChanged += value;
                }
                remove
                {
                    _propertyChanged -= value;
                    if (_propertyChanged == null)
                    {
                        _inner.PropertyChanged -= InnerPropertyChanged;
                    }
                }
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

            private void InnerIndexesChanged(object? sender, SelectionModelIndexesChangedEventArgs e)
            {
                _indexesChanged?.Invoke(this, e);
                _lastSelectionEmpty = _inner.SelectedIndexes.Count == 0;
            }

            private void InnerSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs e)
            {
                if (_selectionChanged != null)
                {
                    var projected = new SelectionModelSelectionChangedEventArgs<object>(
                        e.DeselectedIndexes,
                        e.SelectedIndexes,
                        ProjectSelectionItems(e.DeselectedItems),
                        ProjectSelectionItems(e.SelectedItems));
                    _selectionChanged.Invoke(this, projected);
                }
                _lastSelectionEmpty = _inner.SelectedIndexes.Count == 0;
            }

            private void InnerLostSelection(object? sender, EventArgs e)
            {
                var isEmpty = _inner.SelectedIndexes.Count == 0;
                if (!_lastSelectionEmpty && isEmpty)
                {
                    _lostSelection?.Invoke(this, e);
                }

                _lastSelectionEmpty = isEmpty;
            }

            private void InnerSourceReset(object? sender, EventArgs e)
            {
                _sourceReset?.Invoke(this, e);
                _lastSelectionEmpty = _inner.SelectedIndexes.Count == 0;
            }

            private void InnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                _propertyChanged?.Invoke(this, e);
            }

            private IReadOnlyList<object?> ProjectSelectionItems(IReadOnlyList<object?> items)
            {
                return items.Count == 0
                    ? Array.Empty<object?>()
                    : new ProjectedReadOnlyList(items, _itemSelector);
            }

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

        private readonly struct SearchCellKey : IEquatable<SearchCellKey>
        {
            public SearchCellKey(int rowIndex, DataGridColumn column)
            {
                RowIndex = rowIndex;
                Column = column;
            }

            public int RowIndex { get; }

            public DataGridColumn Column { get; }

            public bool Equals(SearchCellKey other)
            {
                return RowIndex == other.RowIndex && ReferenceEquals(Column, other.Column);
            }

            public override bool Equals(object obj)
            {
                return obj is SearchCellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RowIndex * 397) ^ (Column?.GetHashCode() ?? 0);
                }
            }
        }
    }
}
