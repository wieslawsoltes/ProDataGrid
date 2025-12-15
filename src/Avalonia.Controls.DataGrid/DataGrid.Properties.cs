// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Avalonia.Controls
{
    /// <summary>
    /// Styled and direct properties
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the CanUserReorderColumns dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> CanUserReorderColumnsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(CanUserReorderColumns));

        /// <summary>
        /// Gets or sets a value that indicates whether the user can change
        /// the column display order by dragging column headers with the mouse.
        /// </summary>
        public bool CanUserReorderColumns
        {
            get { return GetValue(CanUserReorderColumnsProperty); }
            set { SetValue(CanUserReorderColumnsProperty, value); }
        }

        /// <summary>
        /// Identifies the CanUserResizeColumns dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> CanUserResizeColumnsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(CanUserResizeColumns));

        /// <summary>
        /// Gets or sets a value that indicates whether the user can adjust column widths using the mouse.
        /// </summary>
        public bool CanUserResizeColumns
        {
            get { return GetValue(CanUserResizeColumnsProperty); }
            set { SetValue(CanUserResizeColumnsProperty, value); }
        }

        /// <summary>
        /// Identifies the CanUserSortColumns dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> CanUserSortColumnsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(CanUserSortColumns), true);

        /// <summary>
        /// Gets or sets a value that indicates whether the user can sort columns by clicking the column header.
        /// </summary>
        public bool CanUserSortColumns
        {
            get { return GetValue(CanUserSortColumnsProperty); }
            set { SetValue(CanUserSortColumnsProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="CanUserAddRows"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> CanUserAddRowsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(CanUserAddRows), true);

        /// <summary>
        /// Gets or sets a value that indicates whether the user can add rows.
        /// </summary>
        public bool CanUserAddRows
        {
            get { return GetValue(CanUserAddRowsProperty); }
            set { SetValue(CanUserAddRowsProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="CanUserDeleteRows"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> CanUserDeleteRowsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(CanUserDeleteRows), true);

        /// <summary>
        /// Gets or sets a value that indicates whether the user can delete rows.
        /// </summary>
        public bool CanUserDeleteRows
        {
            get { return GetValue(CanUserDeleteRowsProperty); }
            set { SetValue(CanUserDeleteRowsProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="CanUserReorderRows"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> CanUserReorderRowsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(CanUserReorderRows));

        /// <summary>
        /// Gets or sets a value that indicates whether the user can reorder rows via drag-and-drop.
        /// </summary>
        public bool CanUserReorderRows
        {
            get { return GetValue(CanUserReorderRowsProperty); }
            set { SetValue(CanUserReorderRowsProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="RowDragHandle"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<DataGridRowDragHandle> RowDragHandleProperty =
            AvaloniaProperty.Register<DataGrid, DataGridRowDragHandle>(
                nameof(RowDragHandle),
                defaultValue: DataGridRowDragHandle.RowHeader);

        /// <summary>
        /// Gets or sets the row drag handle surface.
        /// </summary>
        public DataGridRowDragHandle RowDragHandle
        {
            get { return GetValue(RowDragHandleProperty); }
            set { SetValue(RowDragHandleProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="RowDragHandleVisible"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<bool> RowDragHandleVisibleProperty =
            AvaloniaProperty.Register<DataGrid, bool>(
                nameof(RowDragHandleVisible),
                defaultValue: true);

        /// <summary>
        /// Gets or sets a value that indicates whether the drag handle grip is visible in the row header.
        /// </summary>
        public bool RowDragHandleVisible
        {
            get => GetValue(RowDragHandleVisibleProperty);
            set => SetValue(RowDragHandleVisibleProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="RowDragDropOptions"/> direct property.
        /// </summary>
        public static readonly DirectProperty<DataGrid, DataGridRowDragDropOptions> RowDragDropOptionsProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, DataGridRowDragDropOptions>(
                nameof(RowDragDropOptions),
                o => o.RowDragDropOptions,
                (o, v) => o.RowDragDropOptions = v);

        /// <summary>
        /// Gets or sets row drag/drop options.
        /// </summary>
        public DataGridRowDragDropOptions RowDragDropOptions
        {
            get => _rowDragDropOptions;
            set => SetAndRaise(RowDragDropOptionsProperty, ref _rowDragDropOptions, value ?? new DataGridRowDragDropOptions());
        }

        /// <summary>
        /// Identifies the <see cref="RowDropHandler"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<IDataGridRowDropHandler> RowDropHandlerProperty =
            AvaloniaProperty.Register<DataGrid, IDataGridRowDropHandler>(nameof(RowDropHandler));

        /// <summary>
        /// Gets or sets the handler used to perform row drops.
        /// </summary>
        public IDataGridRowDropHandler RowDropHandler
        {
            get => GetValue(RowDropHandlerProperty);
            set => SetValue(RowDropHandlerProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="RowDragDropControllerFactory"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<IDataGridRowDragDropControllerFactory> RowDragDropControllerFactoryProperty =
            AvaloniaProperty.Register<DataGrid, IDataGridRowDragDropControllerFactory>(
                nameof(RowDragDropControllerFactory));

        /// <summary>
        /// Gets or sets the controller factory used to wire drag/drop.
        /// </summary>
        public IDataGridRowDragDropControllerFactory RowDragDropControllerFactory
        {
            get => GetValue(RowDragDropControllerFactoryProperty);
            set => SetValue(RowDragDropControllerFactoryProperty, value);
        }

        /// <summary>
        /// Identifies the ColumnHeaderHeight dependency property.
        /// </summary>
        public static readonly StyledProperty<double> ColumnHeaderHeightProperty =
            AvaloniaProperty.Register<DataGrid, double>(
                nameof(ColumnHeaderHeight),
                defaultValue: double.NaN,
                validate: IsValidColumnHeaderHeight);

        private static bool IsValidColumnHeaderHeight(double value)
        {
            return double.IsNaN(value) ||
                (value >= DATAGRID_minimumColumnHeaderHeight && value <= DATAGRID_maxHeadersThickness);
        }

        /// <summary>
        /// Gets or sets the height of the column headers row.
        /// </summary>
        public double ColumnHeaderHeight
        {
            get { return GetValue(ColumnHeaderHeightProperty); }
            set { SetValue(ColumnHeaderHeightProperty, value); }
        }

        /// <summary>
        /// Identifies the ColumnWidth dependency property.
        /// </summary>
        public static readonly StyledProperty<DataGridLength> ColumnWidthProperty =
            AvaloniaProperty.Register<DataGrid, DataGridLength>(nameof(ColumnWidth), defaultValue: DataGridLength.Auto);

        /// <summary>
        /// Gets or sets the standard width or automatic sizing mode of columns in the control.
        /// </summary>
        public DataGridLength ColumnWidth
        {
            get { return GetValue(ColumnWidthProperty); }
            set { SetValue(ColumnWidthProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="RowTheme"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<ControlTheme> RowThemeProperty =
            AvaloniaProperty.Register<DataGrid, ControlTheme>(nameof(RowTheme));

        /// <summary>
        /// Gets or sets the theme applied to all rows.
        /// </summary>
        public ControlTheme RowTheme
        {
            get { return GetValue(RowThemeProperty); }
            set { SetValue(RowThemeProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="CellTheme"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<ControlTheme> CellThemeProperty =
            AvaloniaProperty.Register<DataGrid, ControlTheme>(nameof(CellTheme));

        /// <summary>
        /// Gets or sets the theme applied to all cells.
        /// </summary>
        public ControlTheme CellTheme
        {
            get { return GetValue(CellThemeProperty); }
            set { SetValue(CellThemeProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="ColumnHeaderTheme"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<ControlTheme> ColumnHeaderThemeProperty =
            AvaloniaProperty.Register<DataGrid, ControlTheme>(nameof(ColumnHeaderTheme));

        /// <summary>
        /// Gets or sets the theme applied to all column headers.
        /// </summary>
        public ControlTheme ColumnHeaderTheme
        {
            get { return GetValue(ColumnHeaderThemeProperty); }
            set { SetValue(ColumnHeaderThemeProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="ColumnHeaderFilterTheme"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<ControlTheme> ColumnHeaderFilterThemeProperty =
            AvaloniaProperty.Register<DataGrid, ControlTheme>(nameof(ColumnHeaderFilterTheme));

        /// <summary>
        /// Gets or sets the theme applied to column header filter buttons.
        /// </summary>
        public ControlTheme ColumnHeaderFilterTheme
        {
            get { return GetValue(ColumnHeaderFilterThemeProperty); }
            set { SetValue(ColumnHeaderFilterThemeProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="RowGroupTheme"/> dependency property.
        /// </summary>
        public static readonly StyledProperty<ControlTheme> RowGroupThemeProperty =
            AvaloniaProperty.Register<DataGrid, ControlTheme>(nameof(RowGroupTheme));

        /// <summary>
        /// Gets or sets the theme applied to all row groups.
        /// </summary>
        public ControlTheme RowGroupTheme
        {
            get { return GetValue(RowGroupThemeProperty); }
            set { SetValue(RowGroupThemeProperty, value); }
        }

        public static readonly StyledProperty<int> FrozenColumnCountProperty =
            AvaloniaProperty.Register<DataGrid, int>(
                nameof(FrozenColumnCount),
                validate: ValidateFrozenColumnCount);

        /// <summary>
        /// Gets or sets the number of columns that the user cannot scroll horizontally.
        /// </summary>
        public int FrozenColumnCount
        {
            get { return GetValue(FrozenColumnCountProperty); }
            set { SetValue(FrozenColumnCountProperty, value); }
        }

        private static bool ValidateFrozenColumnCount(int value) => value >= 0;

        public static readonly StyledProperty<DataGridGridLinesVisibility> GridLinesVisibilityProperty =
            AvaloniaProperty.Register<DataGrid, DataGridGridLinesVisibility>(nameof(GridLinesVisibility));

        /// <summary>
        /// Gets or sets a value that indicates which grid lines separating inner cells are shown.
        /// </summary>
        public DataGridGridLinesVisibility GridLinesVisibility
        {
            get { return GetValue(GridLinesVisibilityProperty); }
            set { SetValue(GridLinesVisibilityProperty, value); }
        }

        public static readonly StyledProperty<DataGridHeadersVisibility> HeadersVisibilityProperty =
            AvaloniaProperty.Register<DataGrid, DataGridHeadersVisibility>(nameof(HeadersVisibility));

        /// <summary>
        /// Gets or sets a value that indicates the visibility of row and column headers.
        /// </summary>
        public DataGridHeadersVisibility HeadersVisibility
        {
            get { return GetValue(HeadersVisibilityProperty); }
            set { SetValue(HeadersVisibilityProperty, value); }
        }

        public static readonly StyledProperty<IBrush> HorizontalGridLinesBrushProperty =
            AvaloniaProperty.Register<DataGrid, IBrush>(nameof(HorizontalGridLinesBrush));

        /// <summary>
        /// Gets or sets the <see cref="T:System.Windows.Media.Brush" /> that is used to paint grid lines separating rows.
        /// </summary>
        public IBrush HorizontalGridLinesBrush
        {
            get { return GetValue(HorizontalGridLinesBrushProperty); }
            set { SetValue(HorizontalGridLinesBrushProperty, value); }
        }

        public static readonly StyledProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty =
            AvaloniaProperty.Register<DataGrid, ScrollBarVisibility>(nameof(HorizontalScrollBarVisibility));

        /// <summary>
        /// Gets or sets a value that indicates how the horizontal scroll bar is displayed.
        /// </summary>
        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get { return GetValue(HorizontalScrollBarVisibilityProperty); }
            set { SetValue(HorizontalScrollBarVisibilityProperty, value); }
        }

        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(IsReadOnly));

        /// <summary>
        /// Gets or sets a value that indicates whether the user can edit the values in the control.
        /// </summary>
        public bool IsReadOnly
        {
            get { return GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public static readonly StyledProperty<bool> AreRowGroupHeadersFrozenProperty =
            AvaloniaProperty.Register<DataGrid, bool>(
                nameof(AreRowGroupHeadersFrozen),
                defaultValue: true);

        /// <summary>
        /// Gets or sets a value that indicates whether the row group header sections
        /// remain fixed at the width of the display area or can scroll horizontally.
        /// </summary>
        public bool AreRowGroupHeadersFrozen
        {
            get { return GetValue(AreRowGroupHeadersFrozenProperty); }
            set { SetValue(AreRowGroupHeadersFrozenProperty, value); }
        }

        /// <summary>
        /// Defines the <see cref="IsScrollInertiaEnabled"/> property.
        /// </summary>
        public static readonly AttachedProperty<bool> IsScrollInertiaEnabledProperty =
            ScrollViewer.IsScrollInertiaEnabledProperty.AddOwner<DataGrid>();

        /// <summary>
        /// Gets or sets whether scroll gestures should include inertia in their behavior and value.
        /// </summary>
        public bool IsScrollInertiaEnabled
        {
            get => GetValue(IsScrollInertiaEnabledProperty);
            set => SetValue(IsScrollInertiaEnabledProperty, value);
        }

        public static readonly DirectProperty<DataGrid, bool> IsValidProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, bool>(
                nameof(IsValid),
                o => o.IsValid);

        public static readonly StyledProperty<double> MaxColumnWidthProperty =
            AvaloniaProperty.Register<DataGrid, double>(
                nameof(MaxColumnWidth),
                defaultValue: DATAGRID_defaultMaxColumnWidth,
                validate: IsValidColumnWidth);

        private static bool IsValidColumnWidth(double value)
        {
            return !double.IsNaN(value) && value > 0;
        }

        /// <summary>
        /// Gets or sets the maximum width of columns in the <see cref="T:Avalonia.Controls.DataGrid" /> .
        /// </summary>
        public double MaxColumnWidth
        {
            get { return GetValue(MaxColumnWidthProperty); }
            set { SetValue(MaxColumnWidthProperty, value); }
        }

        public static readonly StyledProperty<double> MinColumnWidthProperty =
            AvaloniaProperty.Register<DataGrid, double>(
                nameof(MinColumnWidth),
                defaultValue: DATAGRID_defaultMinColumnWidth,
                validate: IsValidMinColumnWidth);

        private static bool IsValidMinColumnWidth(double value)
        {
            return !double.IsNaN(value) && !double.IsPositiveInfinity(value) && value >= 0;
        }

        /// <summary>
        /// Gets or sets the minimum width of columns in the <see cref="T:Avalonia.Controls.DataGrid" />.
        /// </summary>
        public double MinColumnWidth
        {
            get { return GetValue(MinColumnWidthProperty); }
            set { SetValue(MinColumnWidthProperty, value); }
        }

        public static readonly StyledProperty<IBrush> RowBackgroundProperty =
            AvaloniaProperty.Register<DataGrid, IBrush>(nameof(RowBackground));

        /// <summary>
        /// Gets or sets the <see cref="T:System.Windows.Media.Brush" /> that is used to paint row backgrounds.
        /// </summary>
        public IBrush RowBackground
        {
            get { return GetValue(RowBackgroundProperty); }
            set { SetValue(RowBackgroundProperty, value); }
        }

        public static readonly StyledProperty<double> RowHeightProperty =
            AvaloniaProperty.Register<DataGrid, double>(
                nameof(RowHeight),
                defaultValue: double.NaN,
                validate: IsValidRowHeight);

        private static bool IsValidRowHeight(double value)
        {
            return double.IsNaN(value) ||
                (value >= DataGridRow.DATAGRIDROW_minimumHeight &&
                 value <= DataGridRow.DATAGRIDROW_maximumHeight);
        }

        /// <summary>
        /// Gets or sets the standard height of rows in the control.
        /// </summary>
        public double RowHeight
        {
            get { return GetValue(RowHeightProperty); }
            set { SetValue(RowHeightProperty, value); }
        }

        public static readonly StyledProperty<double> RowHeaderWidthProperty =
            AvaloniaProperty.Register<DataGrid, double>(
                nameof(RowHeaderWidth),
                defaultValue: double.NaN,
                validate: IsValidRowHeaderWidth);

        private static bool IsValidRowHeaderWidth(double value)
        {
            return double.IsNaN(value) ||
                (value >= DATAGRID_minimumRowHeaderWidth &&
                 value <= DATAGRID_maxHeadersThickness);
        }

        /// <summary>
        /// Gets or sets the width of the row header column.
        /// </summary>
        public double RowHeaderWidth
        {
            get { return GetValue(RowHeaderWidthProperty); }
            set { SetValue(RowHeaderWidthProperty, value); }
        }

        public static readonly StyledProperty<DataGridSelectionMode> SelectionModeProperty =
            AvaloniaProperty.Register<DataGrid, DataGridSelectionMode>(nameof(SelectionMode));

        public static readonly StyledProperty<DataGridSelectionUnit> SelectionUnitProperty =
            AvaloniaProperty.Register<DataGrid, DataGridSelectionUnit>(
                nameof(SelectionUnit),
                defaultValue: DataGridSelectionUnit.FullRow);

        /// <summary>
        /// Gets or sets the selection behavior of the data grid.
        /// </summary>
        public DataGridSelectionMode SelectionMode
        {
            get { return GetValue(SelectionModeProperty); }
            set { SetValue(SelectionModeProperty, value); }
        }

        /// <summary>
        /// Gets or sets whether selection targets rows or cells.
        /// </summary>
        public DataGridSelectionUnit SelectionUnit
        {
            get { return GetValue(SelectionUnitProperty); }
            set { SetValue(SelectionUnitProperty, value); }
        }

        public static readonly StyledProperty<IBrush> VerticalGridLinesBrushProperty =
            AvaloniaProperty.Register<DataGrid, IBrush>(nameof(VerticalGridLinesBrush));

        /// <summary>
        /// Gets or sets the <see cref="T:System.Windows.Media.Brush" /> that is used to paint grid lines separating columns.
        /// </summary>
        public IBrush VerticalGridLinesBrush
        {
            get { return GetValue(VerticalGridLinesBrushProperty); }
            set { SetValue(VerticalGridLinesBrushProperty, value); }
        }

        public static readonly StyledProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty =
            AvaloniaProperty.Register<DataGrid, ScrollBarVisibility>(nameof(VerticalScrollBarVisibility));

        /// <summary>
        /// Gets or sets a value that indicates how the vertical scroll bar is displayed.
        /// </summary>
        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return GetValue(VerticalScrollBarVisibilityProperty); }
            set { SetValue(VerticalScrollBarVisibilityProperty, value); }
        }

        /// <summary>
        /// Defines the <see cref="RecycledContainerHidingMode"/> property.
        /// </summary>
        public static readonly StyledProperty<DataGridRecycleHidingMode> RecycledContainerHidingModeProperty =
            AvaloniaProperty.Register<DataGrid, DataGridRecycleHidingMode>(
                nameof(RecycledContainerHidingMode),
                defaultValue: DataGridRecycleHidingMode.MoveOffscreen);

        /// <summary>
        /// Gets or sets how recycled containers are hidden: either moved far offscreen or simply
        /// marked invisible. Defaults to offscreen.
        /// </summary>
        public DataGridRecycleHidingMode RecycledContainerHidingMode
        {
            get { return GetValue(RecycledContainerHidingModeProperty); }
            set { SetValue(RecycledContainerHidingModeProperty, value); }
        }

        /// <summary>
        /// Defines the <see cref="KeepRecycledContainersInVisualTree"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> KeepRecycledContainersInVisualTreeProperty =
            AvaloniaProperty.Register<DataGrid, bool>(
                nameof(KeepRecycledContainersInVisualTree),
                defaultValue: true);

        /// <summary>
        /// Gets or sets a value indicating whether recycled rows/group headers should remain in the
        /// rows presenter visual tree (arranged off-screen by default) instead of being removed.
        /// Defaults to true.
        /// </summary>
        public bool KeepRecycledContainersInVisualTree
        {
            get { return GetValue(KeepRecycledContainersInVisualTreeProperty); }
            set { SetValue(KeepRecycledContainersInVisualTreeProperty, value); }
        }

        /// <summary>
        /// Defines the <see cref="TrimRecycledContainers"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> TrimRecycledContainersProperty =
            AvaloniaProperty.Register<DataGrid, bool>(
                nameof(TrimRecycledContainers),
                defaultValue: false);

        /// <summary>
        /// Gets or sets a value indicating whether recycled rows/group headers should be trimmed
        /// to a small buffer when the viewport contracts. Defaults to false to retain larger
        /// recycle pools unless explicitly trimmed.
        /// </summary>
        public bool TrimRecycledContainers
        {
            get { return GetValue(TrimRecycledContainersProperty); }
            set { SetValue(TrimRecycledContainersProperty, value); }
        }

        /// <summary>
        /// Defines the <see cref="UseLogicalScrollable"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> UseLogicalScrollableProperty =
            AvaloniaProperty.Register<DataGrid, bool>(
                nameof(UseLogicalScrollable),
                defaultValue: false);

        /// <summary>
        /// Gets or sets a value indicating whether the DataGrid should use the new ILogicalScrollable
        /// implementation for scrolling. When true, the DataGridRowsPresenter participates in Avalonia's
        /// standard scroll contract. When false (default), uses the legacy custom ScrollBar handling.
        /// </summary>
        /// <remarks>
        /// This property is a feature flag for gradual migration to the new scrolling architecture.
        /// Setting this to true enables:
        /// - Standard ScrollViewer integration via ILogicalScrollable
        /// - Improved scroll chaining support
        /// - Better touch/inertia scrolling behavior
        /// - Potential future support for scroll anchoring
        /// 
        /// Note: Theme updates may be required for full ScrollViewer integration.
        /// </remarks>
        public bool UseLogicalScrollable
        {
            get { return GetValue(UseLogicalScrollableProperty); }
            set { SetValue(UseLogicalScrollableProperty, value); }
        }

        public static readonly StyledProperty<ITemplate<Control>> DropLocationIndicatorTemplateProperty =
            AvaloniaProperty.Register<DataGrid, ITemplate<Control>>(nameof(DropLocationIndicatorTemplate));

        /// <summary>
        /// Gets or sets the template that is used when rendering the column headers.
        /// </summary>
        public ITemplate<Control> DropLocationIndicatorTemplate
        {
            get { return GetValue(DropLocationIndicatorTemplateProperty); }
            set { SetValue(DropLocationIndicatorTemplateProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the grid should automatically scroll the
        /// selected item into view when selection changes.
        /// </summary>
        public static readonly StyledProperty<bool> AutoScrollToSelectedItemProperty =
            AvaloniaProperty.Register<DataGrid, bool>(
                nameof(AutoScrollToSelectedItem),
                defaultValue: false);

        /// <summary>
        /// Gets or sets whether the grid automatically scrolls to the selected item when selection
        /// changes.
        /// </summary>
        public bool AutoScrollToSelectedItem
        {
            get { return GetValue(AutoScrollToSelectedItemProperty); }
            set { SetValue(AutoScrollToSelectedItemProperty, value); }
        }

        private int _selectedIndex = -1;
        private object _selectedItem;
        private DataGridCellInfo _currentCell = DataGridCellInfo.Unset;

        public static readonly DirectProperty<DataGrid, int> SelectedIndexProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, int>(
                nameof(SelectedIndex),
                o => o.SelectedIndex,
                (o, v) => o.SelectedIndex = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the index of the current selection.
        /// </summary>
        /// <returns>
        /// The index of the current selection, or -1 if the selection is empty.
        /// </returns>
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set { SetAndRaise(SelectedIndexProperty, ref _selectedIndex, value); }
        }

        public static readonly DirectProperty<DataGrid, object> SelectedItemProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, object>(
                nameof(SelectedItem),
                o => o.SelectedItem,
                (o, v) => o.SelectedItem = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the data item corresponding to the selected row.
        /// </summary>
        public object SelectedItem
        {
            get { return _selectedItem; }
            set { SetAndRaise(SelectedItemProperty, ref _selectedItem, value); }
        }

        public static readonly DirectProperty<DataGrid, IList> SelectedItemsProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, IList>(
                nameof(SelectedItems),
                o => o.SelectedItems,
                (o, v) => o.SelectedItems = v,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly DirectProperty<DataGrid, IList<DataGridCellInfo>> SelectedCellsProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, IList<DataGridCellInfo>>(
                nameof(SelectedCells),
                o => o.SelectedCells,
                (o, v) => o.SelectedCells = v,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly DirectProperty<DataGrid, DataGridCellInfo> CurrentCellProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, DataGridCellInfo>(
                nameof(CurrentCell),
                o => o.CurrentCell,
                (o, v) => o.CurrentCell = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets information about the current cell.
        /// </summary>
        public DataGridCellInfo CurrentCell
        {
            get => _currentCell;
            set => SetCurrentCell(value);
        }

        /// <summary>
        /// Gets or sets the selection model that drives row selection. If not provided, a default
        /// selection model is created.
        /// </summary>
        public static readonly DirectProperty<DataGrid, ISelectionModel> SelectionProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, ISelectionModel>(
                nameof(Selection),
                o => o.Selection,
                (o, v) => o.Selection = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the sorting model that drives column sorting. If not provided, a default
        /// sorting model is created.
        /// </summary>
        public static readonly DirectProperty<DataGrid, ISortingModel> SortingModelProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, ISortingModel>(
                nameof(SortingModel),
                o => o.SortingModel,
                (o, v) => o.SortingModel = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the filtering model that drives column filtering. If not provided, a default
        /// filtering model is created.
        /// </summary>
        public static readonly DirectProperty<DataGrid, Avalonia.Controls.DataGridFiltering.IFilteringModel> FilteringModelProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, Avalonia.Controls.DataGridFiltering.IFilteringModel>(
                nameof(FilteringModel),
                o => o.FilteringModel,
                (o, v) => o.FilteringModel = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the hierarchical model that drives tree-like rows. If not provided, a default
        /// hierarchical model is created.
        /// </summary>
        public static readonly DirectProperty<DataGrid, Avalonia.Controls.DataGridHierarchical.IHierarchicalModel> HierarchicalModelProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, Avalonia.Controls.DataGridHierarchical.IHierarchicalModel>(
                nameof(HierarchicalModel),
                o => o.HierarchicalModel,
                (o, v) => o.HierarchicalModel = v,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<DataGridClipboardCopyMode> ClipboardCopyModeProperty =
            AvaloniaProperty.Register<DataGrid, DataGridClipboardCopyMode>(
                nameof(ClipboardCopyMode),
                defaultValue: DataGridClipboardCopyMode.ExcludeHeader);

        /// <summary>
        /// The property which determines how DataGrid content is copied to the Clipboard.
        /// </summary>
        public DataGridClipboardCopyMode ClipboardCopyMode
        {
            get { return GetValue(ClipboardCopyModeProperty); }
            set { SetValue(ClipboardCopyModeProperty, value); }
        }

        /// <summary>
        /// Identifies the Columns direct property; bindings to Columns are fully supported.
        /// </summary>
        public static readonly DirectProperty<DataGrid, IEnumerable<DataGridColumn>> ColumnsProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, IEnumerable<DataGridColumn>>(
                nameof(Columns),
                o => o.Columns,
                (o, v) => o.Columns = v,
                defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Identifies the ColumnsSynchronizationMode styled property.
        /// </summary>
        public static readonly StyledProperty<ColumnsSynchronizationMode> ColumnsSynchronizationModeProperty =
            AvaloniaProperty.Register<DataGrid, ColumnsSynchronizationMode>(
                nameof(ColumnsSynchronizationMode),
                ColumnsSynchronizationMode.OneWayToGrid,
                defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Identifies the ColumnsSourceResetBehavior styled property.
        /// </summary>
        public static readonly StyledProperty<ColumnsSourceResetBehavior> ColumnsSourceResetBehaviorProperty =
            AvaloniaProperty.Register<DataGrid, ColumnsSourceResetBehavior>(
                nameof(ColumnsSourceResetBehavior),
                ColumnsSourceResetBehavior.Reload,
                defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Identifies the AutoGeneratedColumnsPlacement styled property.
        /// </summary>
        public static readonly StyledProperty<AutoGeneratedColumnsPlacement> AutoGeneratedColumnsPlacementProperty =
            AvaloniaProperty.Register<DataGrid, AutoGeneratedColumnsPlacement>(
                nameof(AutoGeneratedColumnsPlacement),
                AutoGeneratedColumnsPlacement.AfterSource,
                defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Gets or sets how a bound columns collection synchronizes with the grid.
        /// </summary>
        public ColumnsSynchronizationMode ColumnsSynchronizationMode
        {
            get => GetValue(ColumnsSynchronizationModeProperty);
            set => SetValue(ColumnsSynchronizationModeProperty, value);
        }

        /// <summary>
        /// Gets or sets how the grid reacts to Reset notifications from the bound columns collection.
        /// </summary>
        public ColumnsSourceResetBehavior ColumnsSourceResetBehavior
        {
            get => GetValue(ColumnsSourceResetBehaviorProperty);
            set => SetValue(ColumnsSourceResetBehaviorProperty, value);
        }

        /// <summary>
        /// Gets or sets where auto-generated columns should be placed relative to bound columns.
        /// </summary>
        public AutoGeneratedColumnsPlacement AutoGeneratedColumnsPlacement
        {
            get => GetValue(AutoGeneratedColumnsPlacementProperty);
            set => SetValue(AutoGeneratedColumnsPlacementProperty, value);
        }

        public static readonly StyledProperty<bool> AutoGenerateColumnsProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(AutoGenerateColumns));

        /// <summary>
        /// Gets or sets a value that indicates whether columns are created
        /// automatically when the <see cref="P:Avalonia.Controls.DataGrid.ItemsSource" /> property is set.
        /// </summary>
        public bool AutoGenerateColumns
        {
            get { return GetValue(AutoGenerateColumnsProperty); }
            set { SetValue(AutoGenerateColumnsProperty, value); }
        }

        /// <summary>
        /// Identifies the ItemsSource property.
        /// </summary>
        public static readonly StyledProperty<IEnumerable> ItemsSourceProperty =
            AvaloniaProperty.Register<DataGrid, IEnumerable>(nameof(ItemsSource));

        /// <summary>
        /// Gets or sets a collection that is used to generate the content of the control.
        /// </summary>
        public IEnumerable ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly StyledProperty<bool> AreRowDetailsFrozenProperty =
            AvaloniaProperty.Register<DataGrid, bool>(nameof(AreRowDetailsFrozen));

        /// <summary>
        /// Gets or sets a value that indicates whether the row details sections remain
        /// fixed at the width of the display area or can scroll horizontally.
        /// </summary>
        public bool AreRowDetailsFrozen
        {
            get { return GetValue(AreRowDetailsFrozenProperty); }
            set { SetValue(AreRowDetailsFrozenProperty, value); }
        }

        public static readonly StyledProperty<IDataTemplate> RowDetailsTemplateProperty =
            AvaloniaProperty.Register<DataGrid, IDataTemplate>(nameof(RowDetailsTemplate));

        /// <summary>
        /// Gets or sets the template that is used to display the content of the details section of rows.
        /// </summary>
        public IDataTemplate RowDetailsTemplate
        {
            get { return GetValue(RowDetailsTemplateProperty); }
            set { SetValue(RowDetailsTemplateProperty, value); }
        }

        public static readonly StyledProperty<DataGridRowDetailsVisibilityMode> RowDetailsVisibilityModeProperty =
            AvaloniaProperty.Register<DataGrid, DataGridRowDetailsVisibilityMode>(nameof(RowDetailsVisibilityMode));

        /// <summary>
        /// Gets or sets a value that indicates when the details sections of rows are displayed.
        /// </summary>
        public DataGridRowDetailsVisibilityMode RowDetailsVisibilityMode
        {
            get { return GetValue(RowDetailsVisibilityModeProperty); }
            set { SetValue(RowDetailsVisibilityModeProperty, value); }
        }

        public static readonly DirectProperty<DataGrid, IDataGridCollectionView> CollectionViewProperty =
            AvaloniaProperty.RegisterDirect<DataGrid, IDataGridCollectionView>(nameof(CollectionView),
                o => o.CollectionView);

        /// <summary>
        /// Gets current <see cref="IDataGridCollectionView"/>.
        /// </summary>
        public IDataGridCollectionView CollectionView =>
            DataConnection.CollectionView;

        /// <summary>
        /// Identifies the RowHeightEstimator dependency property.
        /// </summary>
        public static readonly StyledProperty<IDataGridRowHeightEstimator> RowHeightEstimatorProperty =
            AvaloniaProperty.Register<DataGrid, IDataGridRowHeightEstimator>(
                nameof(RowHeightEstimator),
                defaultValue: new AdvancedRowHeightEstimator());

        /// <summary>
        /// Gets or sets the row height estimator used for scroll calculations.
        /// Setting this property allows using different algorithms for handling variable row heights.
        /// If not set, a default average-based estimator is used.
        /// </summary>
        public IDataGridRowHeightEstimator RowHeightEstimator
        {
            get { return GetValue(RowHeightEstimatorProperty); }
            set { SetValue(RowHeightEstimatorProperty, value); }
        }
    }
}
