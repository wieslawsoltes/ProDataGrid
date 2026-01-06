// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Utilities;
using Button = Avalonia.Controls.Button;

namespace Avalonia.Controls
{
    /// <summary>
    /// Represents an individual <see cref="T:Avalonia.Controls.DataGrid" /> column header.
    /// </summary>
    [PseudoClasses(":dragIndicator", ":pressed", ":sortascending", ":sortdescending", ":filtered")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridColumnHeader : ContentControl
    {
        private enum DragMode
        {
            None = 0,
            MouseDown = 1,
            Drag = 2,
            Resize = 3,
            Reorder = 4
        }

        private const int DATAGRIDCOLUMNHEADER_resizeRegionWidth = 5;
        private const int DATAGRIDCOLUMNHEADER_columnsDragTreshold = 5;

        private bool _areHandlersSuspended;
        private static DragMode _dragMode;
        private static Point? _lastMousePositionHeaders;
        private static Cursor _originalCursor;
        private static double _originalHorizontalOffset;
        private static double _originalWidth;
        private bool _desiredSeparatorVisibility = true;
        private static Point? _dragStart;
        private static DataGridColumn _dragColumn;
        private static double _frozenColumnsWidth;
        private static double _frozenColumnsRightWidth;
        private static Lazy<Cursor> _resizeCursor = new Lazy<Cursor>(() => new Cursor(StandardCursorType.SizeWestEast));
        private DataGridColumn _owningColumn;
        private bool _suppressSortOnClick;

        /// <summary>
        /// Identifies the <see cref="LeftClick"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridColumnHeaderClickEventArgs> LeftClickEvent =
            RoutedEvent.Register<DataGridColumnHeader, DataGridColumnHeaderClickEventArgs>(nameof(LeftClick), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="HeaderPointerPressed"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<PointerPressedEventArgs> HeaderPointerPressedEvent =
            RoutedEvent.Register<DataGridColumnHeader, PointerPressedEventArgs>(nameof(HeaderPointerPressed), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="HeaderPointerReleased"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<PointerReleasedEventArgs> HeaderPointerReleasedEvent =
            RoutedEvent.Register<DataGridColumnHeader, PointerReleasedEventArgs>(nameof(HeaderPointerReleased), RoutingStrategies.Bubble);

        public static readonly StyledProperty<IBrush> SeparatorBrushProperty =
            AvaloniaProperty.Register<DataGridColumnHeader, IBrush>(nameof(SeparatorBrush));

        public IBrush SeparatorBrush
        {
            get { return GetValue(SeparatorBrushProperty); }
            set { SetValue(SeparatorBrushProperty, value); }
        }

        public static readonly StyledProperty<bool> AreSeparatorsVisibleProperty =
            AvaloniaProperty.Register<DataGridColumnHeader, bool>(
                nameof(AreSeparatorsVisible),
                defaultValue: true);

        public static readonly DirectProperty<DataGridColumnHeader, DataGridColumn> OwningColumnProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumnHeader, DataGridColumn>(
                nameof(OwningColumn),
                o => o.OwningColumn,
                (o, v) => o.OwningColumn = v);

        public static readonly StyledProperty<ControlTheme> FilterThemeProperty =
            AvaloniaProperty.Register<DataGridColumnHeader, ControlTheme>(nameof(FilterTheme));

        public static readonly StyledProperty<FlyoutBase> FilterFlyoutProperty =
            AvaloniaProperty.Register<DataGridColumnHeader, FlyoutBase>(nameof(FilterFlyout));

        public static readonly StyledProperty<bool> ShowFilterButtonProperty =
            AvaloniaProperty.Register<DataGridColumnHeader, bool>(nameof(ShowFilterButton));

        public bool AreSeparatorsVisible
        {
            get { return GetValue(AreSeparatorsVisibleProperty); }
            set { SetValue(AreSeparatorsVisibleProperty, value); }
        }

        public ControlTheme FilterTheme
        {
            get { return GetValue(FilterThemeProperty); }
            set { SetValue(FilterThemeProperty, value); }
        }

        public FlyoutBase FilterFlyout
        {
            get { return GetValue(FilterFlyoutProperty); }
            set { SetValue(FilterFlyoutProperty, value); }
        }

        public bool ShowFilterButton
        {
            get { return GetValue(ShowFilterButtonProperty); }
            set { SetValue(ShowFilterButtonProperty, value); }
        }

        static DataGridColumnHeader()
        {
            AreSeparatorsVisibleProperty.Changed.AddClassHandler<DataGridColumnHeader>((x, e) => x.OnAreSeparatorsVisibleChanged(e));
            FilterFlyoutProperty.Changed.AddClassHandler<DataGridColumnHeader>((x, e) => x.OnFilterFlyoutChanged(e));
            ShowFilterButtonProperty.Changed.AddClassHandler<DataGridColumnHeader>((x, e) => x.OnShowFilterButtonChanged(e));
            PressedMixin.Attach<DataGridColumnHeader>();
            IsTabStopProperty.OverrideDefaultValue<DataGridColumnHeader>(false);
            AutomationProperties.IsOffscreenBehaviorProperty.OverrideDefaultValue<DataGridColumnHeader>(IsOffscreenBehavior.FromClip);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.Primitives.DataGridColumnHeader" /> class.
        /// </summary>
        //TODO Implement
        public DataGridColumnHeader()
        {
            PointerPressed += DataGridColumnHeader_PointerPressed;
            PointerReleased += DataGridColumnHeader_PointerReleased;
            PointerMoved += DataGridColumnHeader_PointerMoved;
            PointerEntered += DataGridColumnHeader_PointerEntered;
            PointerExited += DataGridColumnHeader_PointerExited;
        }

        private Button _filterButton;

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DataGridColumnHeaderAutomationPeer(this);
        }

        private void OnAreSeparatorsVisibleChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                _desiredSeparatorVisibility = (bool)e.NewValue;
                if (OwningGrid != null)
                {
                    UpdateSeparatorVisibility(OwningGrid.ColumnsInternal.LastVisibleColumn);
                }
                else
                {
                    UpdateSeparatorVisibility(null);
                }
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_filterButton != null)
            {
                _filterButton.Click -= FilterButton_Click;
            }

            _filterButton = e.NameScope.Find<Button>("PART_FilterButton");

            if (_filterButton != null)
            {
                _filterButton.Click += FilterButton_Click;
                ApplyFilterFlyout();
                UpdateFilterButtonVisibility();
            }
        }

        public DataGridColumn OwningColumn
        {
            get => _owningColumn;
            internal set => SetAndRaise(OwningColumnProperty, ref _owningColumn, value);
        }
        internal DataGrid OwningGrid => OwningColumn?.OwningGrid;

        internal int ColumnIndex
        {
            get
            {
                if (OwningColumn == null)
                {
                    return -1;
                }
                return OwningColumn.Index;
            }
        }

        internal bool IsResizing => _dragMode == DragMode.Resize;

        internal void SuppressSortOnClick()
        {
            _suppressSortOnClick = true;
        }

        internal ListSortDirection? CurrentSortingState
        {
            get;
            private set;
        }

        private bool IsMouseOver
        {
            get;
            set;
        }

        private bool IsPressed
        {
            get;
            set;
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
            CurrentSortingState = null;
            if (OwningGrid != null
                && OwningGrid.DataConnection != null
                && OwningGrid.DataConnection.AllowSort)
            {
                var sort = OwningColumn.GetSortDescription();
                if (sort != null)
                {
                    CurrentSortingState = sort.Direction;
                }
            }

            PseudoClassesHelper.Set(PseudoClasses, ":sortascending",
                CurrentSortingState == ListSortDirection.Ascending);
            PseudoClassesHelper.Set(PseudoClasses, ":sortdescending",
                CurrentSortingState == ListSortDirection.Descending);
            PseudoClassesHelper.Set(PseudoClasses, ":filtered",
                OwningGrid?.GetFilteringDescriptorForColumn(OwningColumn) != null);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = FilterFlyout;
            if (_filterButton != null && flyout != null)
            {
                FlyoutBase.SetAttachedFlyout(_filterButton, flyout);
                FlyoutBase.ShowAttachedFlyout(_filterButton);
            }
        }

        private void ApplyFilterFlyout()
        {
            if (_filterButton == null)
            {
                return;
            }

            var flyout = FilterFlyout;
            if (flyout != null)
            {
                FlyoutBase.SetAttachedFlyout(_filterButton, flyout);
            }
        }

        private void UpdateFilterButtonVisibility()
        {
            if (_filterButton == null)
            {
                return;
            }

            _filterButton.IsVisible = ShowFilterButton;
        }

        private void OnFilterFlyoutChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is FlyoutBase && !ShowFilterButton)
            {
                SetValue(ShowFilterButtonProperty, true);
            }

            ApplyFilterFlyout();
            UpdateFilterButtonVisibility();
        }

        private void OnShowFilterButtonChanged(AvaloniaPropertyChangedEventArgs e)
        {
            UpdateFilterButtonVisibility();
        }

        internal void UpdateSeparatorVisibility(DataGridColumn lastVisibleColumn)
        {
            bool newVisibility = _desiredSeparatorVisibility;

            // Collapse separator for the last column if there is no filler column
            if (OwningColumn != null &&
                OwningGrid != null &&
                _desiredSeparatorVisibility &&
                OwningColumn == lastVisibleColumn &&
                !OwningGrid.ColumnsInternal.FillerColumn.IsActive)
            {
                newVisibility = false;
            }

            // Update the public property if it has changed
            if (AreSeparatorsVisible != newVisibility)
            {
                SetValueNoCallback(AreSeparatorsVisibleProperty, newVisibility);
            }
        }

        public event EventHandler<DataGridColumnHeaderClickEventArgs> LeftClick
        {
            add => AddHandler(LeftClickEvent, value);
            remove => RemoveHandler(LeftClickEvent, value);
        }

        /// <summary>
        /// Occurs when the pointer is pressed over the column's header.
        /// </summary>
        public event EventHandler<PointerPressedEventArgs> HeaderPointerPressed
        {
            add => AddHandler(HeaderPointerPressedEvent, value);
            remove => RemoveHandler(HeaderPointerPressedEvent, value);
        }

        /// <summary>
        /// Occurs when the pointer is released over the column's header.
        /// </summary>
        public event EventHandler<PointerReleasedEventArgs> HeaderPointerReleased
        {
            add => AddHandler(HeaderPointerReleasedEvent, value);
            remove => RemoveHandler(HeaderPointerReleasedEvent, value);
        }

        internal void OnMouseLeftButtonUp_Click(KeyModifiers keyModifiers, ref bool handled)
        {
            var args = new DataGridColumnHeaderClickEventArgs(keyModifiers, LeftClickEvent, this);
            RaiseEvent(args);

            if (_suppressSortOnClick)
            {
                _suppressSortOnClick = false;
                handled = true;
                return;
            }

            // completed a click without dragging, so we're sorting
            InvokeProcessSort(keyModifiers);
            handled = true;
        }

        internal void InvokeProcessSort(KeyModifiers keyModifiers, ListSortDirection? forcedDirection = null)
        {
            Debug.Assert(OwningGrid != null);
            if (OwningGrid.WaitForLostFocus(() => InvokeProcessSort(keyModifiers, forcedDirection)))
            {
                return;
            }
            if (OwningGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ProcessSort(keyModifiers, forcedDirection));
            }
        }

        //TODO GroupSorting
        internal void ProcessSort(KeyModifiers keyModifiers, ListSortDirection? forcedDirection = null)
        {
            // if we can sort:
            //  - AllowUserToSortColumns and CanSort are true, and
            //  - OwningColumn is bound
            // then try to sort
            if (OwningColumn != null
                && OwningGrid != null
                && OwningGrid.EditingRow == null
                && OwningColumn != OwningGrid.ColumnsInternal.FillerColumn
                && OwningGrid.CanUserSortColumns
                && OwningColumn.CanUserSort)
            {
                var ea = new DataGridColumnEventArgs(OwningColumn);
                OwningGrid.OnColumnSorting(ea);

                if (!ea.Handled && OwningGrid.DataConnection.AllowSort && OwningGrid.DataConnection.SortDescriptions != null)
                {
                    OwningGrid.ProcessSort(OwningColumn, keyModifiers, forcedDirection);
                }
            }
        }

        private bool CanReorderColumn(DataGridColumn column)
        {
            return OwningGrid.CanUserReorderColumns
                && !(column is DataGridFillerColumn)
                && (column.CanUserReorderInternal.HasValue && column.CanUserReorderInternal.Value || !column.CanUserReorderInternal.HasValue);
        }

        /// <summary>
        /// Determines whether a column can be resized by dragging the border of its header.  If star sizing
        /// is being used, there are special conditions that can prevent a column from being resized:
        /// 1. The column is the last visible column.
        /// 2. All columns are constrained by either their maximum or minimum values.
        /// </summary>
        /// <param name="column">Column to check.</param>
        /// <returns>Whether or not the column can be resized by dragging its header.</returns>
        private static bool CanResizeColumn(DataGridColumn column)
        {
            if (column.OwningGrid != null && column.OwningGrid.ColumnsInternal != null && column.OwningGrid.UsesStarSizing &&
                (column.OwningGrid.ColumnsInternal.LastVisibleColumn == column || !MathUtilities.AreClose(column.OwningGrid.ColumnsInternal.VisibleEdgedColumnsWidth, column.OwningGrid.CellsWidth)))
            {
                return false;
            }
            return column.ActualCanUserResize;
        }

        private static bool TrySetResizeColumn(DataGridColumn column)
        {
            // If datagrid.CanUserResizeColumns == false, then the column can still override it
            if (CanResizeColumn(column))
            {
                _dragColumn = column;

                _dragMode = DragMode.Resize;

                return true;
            }
            return false;
        }

        //TODO DragDrop

        internal void OnMouseLeftButtonDown(ref bool handled, PointerPressedEventArgs args, Point mousePosition)
        {
            IsPressed = true;

            if (OwningGrid != null && OwningGrid.ColumnHeaders != null)
            {
                if (TryAutoFitColumnOnDoubleClick(args, mousePosition))
                {
                    handled = true;
                    return;
                }

                _dragMode = DragMode.MouseDown;
                _frozenColumnsWidth = OwningGrid.GetVisibleFrozenColumnsWidthLeft();
                _frozenColumnsRightWidth = OwningGrid.GetVisibleFrozenColumnsWidthRight();
                _lastMousePositionHeaders = this.Translate(OwningGrid.ColumnHeaders, mousePosition);

                double distanceFromLeft = mousePosition.X;
                double distanceFromRight = Bounds.Width - distanceFromLeft;
                DataGridColumn currentColumn = OwningColumn;
                DataGridColumn previousColumn = null;
                if (!(OwningColumn is DataGridFillerColumn))
                {
                    previousColumn = OwningGrid.ColumnsInternal.GetPreviousVisibleNonFillerColumn(currentColumn);
                }

                if (_dragMode == DragMode.MouseDown && _dragColumn == null && (distanceFromRight <= DATAGRIDCOLUMNHEADER_resizeRegionWidth))
                {
                    handled = TrySetResizeColumn(currentColumn);
                }
                else if (_dragMode == DragMode.MouseDown && _dragColumn == null && distanceFromLeft <= DATAGRIDCOLUMNHEADER_resizeRegionWidth && previousColumn != null)
                {
                    handled = TrySetResizeColumn(previousColumn);
                }

                if (_dragMode == DragMode.Resize && _dragColumn != null)
                {
                    _dragStart = _lastMousePositionHeaders;
                    _originalWidth = _dragColumn.ActualWidth;
                    _originalHorizontalOffset = OwningGrid.HorizontalOffset;

                    handled = true;
                }
            }
        }

        private bool TryAutoFitColumnOnDoubleClick(PointerPressedEventArgs args, Point mousePosition)
        {
            if (OwningGrid == null || !OwningGrid.CanUserResizeColumnsOnDoubleClick || args.ClickCount != 2)
            {
                return false;
            }

            DataGridColumn currentColumn = OwningColumn;
            if (currentColumn == null)
            {
                return false;
            }

            double distanceFromLeft = mousePosition.X;
            double distanceFromRight = Bounds.Width - distanceFromLeft;
            DataGridColumn previousColumn = null;

            if (!(currentColumn is DataGridFillerColumn))
            {
                previousColumn = OwningGrid.ColumnsInternal.GetPreviousVisibleNonFillerColumn(currentColumn);
            }

            DataGridColumn targetColumn = null;
            if (distanceFromRight <= DATAGRIDCOLUMNHEADER_resizeRegionWidth)
            {
                targetColumn = currentColumn;
            }
            else if (distanceFromLeft <= DATAGRIDCOLUMNHEADER_resizeRegionWidth)
            {
                targetColumn = previousColumn;
            }

            if (targetColumn == null || !CanResizeColumn(targetColumn))
            {
                return false;
            }

            AutoFitColumn(targetColumn);
            return true;
        }

        private void AutoFitColumn(DataGridColumn column)
        {
            if (OwningGrid == null)
            {
                return;
            }

            double desiredWidth = MeasureAutoFitWidth(column);
            if (double.IsNaN(desiredWidth) || double.IsInfinity(desiredWidth))
            {
                return;
            }

            desiredWidth = Math.Max(column.ActualMinWidth, Math.Min(column.ActualMaxWidth, desiredWidth));
            var oldWidth = column.Width;
            var newWidth = new DataGridLength(oldWidth.Value, oldWidth.UnitType, oldWidth.DesiredValue, desiredWidth);
            var originalHorizontalOffset = OwningGrid.HorizontalOffset;
            column.Resize(oldWidth, newWidth, true);
            OwningGrid.UpdateHorizontalOffset(originalHorizontalOffset);
        }

        private double MeasureAutoFitWidth(DataGridColumn column)
        {
            double desiredWidth = 0;

            var headerCell = column.HeaderCell;
            if (headerCell != null)
            {
                headerCell.Measure(new Size(column.ActualMaxWidth, double.PositiveInfinity));
                desiredWidth = Math.Max(desiredWidth, headerCell.DesiredSize.Width);
            }

            double measureHeight = OwningGrid != null && !double.IsNaN(OwningGrid.RowHeight)
                ? OwningGrid.RowHeight
                : double.PositiveInfinity;

            foreach (Control element in OwningGrid.DisplayData.GetScrollingRows())
            {
                if (element is DataGridRow row)
                {
                    int columnIndex = column.Index;
                    if (columnIndex < 0 || columnIndex >= row.Cells.Count)
                    {
                        continue;
                    }

                    var cell = row.Cells[columnIndex];
                    cell.Measure(new Size(column.ActualMaxWidth, measureHeight));
                    desiredWidth = Math.Max(desiredWidth, cell.DesiredSize.Width);
                }
            }

            return desiredWidth;
        }

        //TODO DragEvents
        //TODO MouseCapture
        internal void OnMouseLeftButtonUp(ref bool handled, PointerEventArgs args, Point mousePosition, Point mousePositionHeaders)
        {
            IsPressed = false;

            if (OwningGrid != null && OwningGrid.ColumnHeaders != null)
            {
                if (_dragMode == DragMode.MouseDown)
                {
                    OnMouseLeftButtonUp_Click(args.KeyModifiers, ref handled);
                }
                else if (_dragMode == DragMode.Reorder)
                {
                    // Find header we're hovering over
                    int targetIndex = GetReorderingTargetDisplayIndex(mousePositionHeaders);

                    if (GetFrozenPositionForDisplayIndex(targetIndex) == OwningColumn.FrozenPosition)
                    {
                        OwningColumn.DisplayIndex = targetIndex;

                        DataGridColumnEventArgs ea = new DataGridColumnEventArgs(OwningColumn);
                        OwningGrid.OnColumnReordered(ea);
                    }
                }

                SetDragCursor(mousePosition);

                // Variables that track drag mode states get reset in DataGridColumnHeader_LostMouseCapture
                args.Pointer.Capture(null);
                OnLostMouseCapture();
                _dragMode = DragMode.None;
                _suppressSortOnClick = false;
                handled = true;
            }
        }

        //TODO DragEvents
        internal void OnMouseMove(PointerEventArgs args, Point mousePosition, Point mousePositionHeaders)
        {
            var handled = args.Handled;
            if (handled || OwningGrid == null || OwningGrid.ColumnHeaders == null)
            {
                return;
            }

            Debug.Assert(OwningGrid.Parent is InputElement);

            OnMouseMove_Resize(ref handled, mousePositionHeaders);

            OnMouseMove_Reorder(ref handled, mousePosition, mousePositionHeaders);

            SetDragCursor(mousePosition);
        }

        private void DataGridColumnHeader_PointerEntered(object sender, PointerEventArgs e)
        {
            if (!IsEnabled)
            {
                return;
            }

            Point mousePosition = e.GetPosition(this);
            OnMouseEnter(mousePosition);
            UpdatePseudoClasses();
        }

        private void DataGridColumnHeader_PointerExited(object sender, PointerEventArgs e)
        {
            if (!IsEnabled)
            {
                return;
            }

            OnMouseLeave();
            UpdatePseudoClasses();
        }

        private void DataGridColumnHeader_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (OwningColumn == null || e.Handled || !IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _suppressSortOnClick = false;
            Point mousePosition = e.GetPosition(this);
            bool handled = e.Handled;
            OnMouseLeftButtonDown(ref handled, e, mousePosition);
            e.RoutedEvent = HeaderPointerPressedEvent;
            e.Source ??= this;
            RaiseEvent(e);
            e.Handled = e.Handled || handled;

            UpdatePseudoClasses();
        }

        private void DataGridColumnHeader_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (OwningColumn == null || e.Handled || !IsEnabled || e.InitialPressMouseButton != MouseButton.Left)
            {
                return;
            }

            Point mousePosition = e.GetPosition(this);
            Point mousePositionHeaders = e.GetPosition(OwningGrid.ColumnHeaders);
            bool handled = e.Handled;
            OnMouseLeftButtonUp(ref handled, e, mousePosition, mousePositionHeaders);
            e.RoutedEvent = HeaderPointerReleasedEvent;
            e.Source ??= this;
            RaiseEvent(e);
            e.Handled = e.Handled || handled;

            UpdatePseudoClasses();
        }

        private void DataGridColumnHeader_PointerMoved(object sender, PointerEventArgs e)
        {
            if (OwningGrid == null || !IsEnabled)
            {
                return;
            }

            Point mousePosition = e.GetPosition(this);
            Point mousePositionHeaders = e.GetPosition(OwningGrid.ColumnHeaders);

            OnMouseMove(e, mousePosition, mousePositionHeaders);
        }

        /// <summary>
        /// Returns the column against whose top-left the reordering caret should be positioned
        /// </summary>
        /// <param name="mousePositionHeaders">Mouse position within the ColumnHeadersPresenter</param>
        /// <param name="scroll">Whether or not to scroll horizontally when a column is dragged out of bounds</param>
        /// <param name="scrollAmount">If scroll is true, returns the horizontal amount that was scrolled</param>
        /// <returns></returns>
        private DataGridColumn GetReorderingTargetColumn(Point mousePositionHeaders, bool scroll, out double scrollAmount)
        {
            scrollAmount = 0;
            double leftEdge = OwningGrid.ColumnsInternal.RowGroupSpacerColumn.IsRepresented ? OwningGrid.ColumnsInternal.RowGroupSpacerColumn.ActualWidth : 0;
            double rightEdge = OwningGrid.CellsWidth;
            double rightFrozenStart = _frozenColumnsRightWidth > 0
                ? Math.Max(leftEdge, rightEdge - _frozenColumnsRightWidth)
                : rightEdge;

            if (OwningColumn.IsFrozenLeft)
            {
                rightEdge = Math.Min(rightEdge, Math.Max(leftEdge, _frozenColumnsWidth));
            }
            else if (OwningColumn.IsFrozenRight)
            {
                leftEdge = Math.Max(leftEdge, rightFrozenStart);
            }
            else
            {
                leftEdge = Math.Max(leftEdge, _frozenColumnsWidth);
                rightEdge = Math.Min(rightEdge, rightFrozenStart);
            }

            if (mousePositionHeaders.X < leftEdge)
            {
                if (scroll)
                {
                    double newVal = mousePositionHeaders.X - leftEdge;
                    scrollAmount = OwningGrid.TryScrollLeftForColumnDrag(-newVal);
                }
                mousePositionHeaders = mousePositionHeaders.WithX(leftEdge);
            }
            else if (mousePositionHeaders.X >= rightEdge)
            {
                if (scroll)
                {
                    double newVal = mousePositionHeaders.X - rightEdge;
                    scrollAmount = OwningGrid.TryScrollRightForColumnDrag(newVal);
                }
                mousePositionHeaders = mousePositionHeaders.WithX(rightEdge - 1);
            }

            foreach (DataGridColumn column in OwningGrid.ColumnsInternal.GetVisibleColumns())
            {
                Point mousePosition = OwningGrid.ColumnHeaders.Translate(column.HeaderCell, mousePositionHeaders);
                double columnMiddle = column.HeaderCell.Bounds.Width / 2;
                if (mousePosition.X >= 0 && mousePosition.X <= columnMiddle)
                {
                    return column;
                }
                else if (mousePosition.X > columnMiddle && mousePosition.X < column.HeaderCell.Bounds.Width)
                {
                    return OwningGrid.ColumnsInternal.GetNextVisibleColumn(column);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the display index to set the column to
        /// </summary>
        /// <param name="mousePositionHeaders">Mouse position relative to the column headers presenter</param>
        /// <returns></returns>
        private int GetReorderingTargetDisplayIndex(Point mousePositionHeaders)
        {
            DataGridColumn targetColumn = GetReorderingTargetColumn(mousePositionHeaders, false /*scroll*/, out double scrollAmount);
            if (targetColumn != null)
            {
                return targetColumn.DisplayIndex > OwningColumn.DisplayIndex ? targetColumn.DisplayIndex - 1 : targetColumn.DisplayIndex;
            }
            else
            {
                return OwningGrid.ColumnDefinitions.Count - 1;
            }
        }

        private DataGridFrozenColumnPosition GetFrozenPositionForDisplayIndex(int displayIndex)
        {
            if (OwningGrid == null)
            {
                return DataGridFrozenColumnPosition.None;
            }

            int leftCount = OwningGrid.FrozenColumnCountWithFiller;
            int rightCount = OwningGrid.FrozenColumnCountRightEffective;
            int totalColumns = OwningGrid.ColumnsInternal.DisplayIndexMap.Count;
            int rightStart = Math.Max(leftCount, totalColumns - rightCount);

            if (displayIndex < leftCount)
            {
                return DataGridFrozenColumnPosition.Left;
            }
            if (displayIndex >= rightStart)
            {
                return DataGridFrozenColumnPosition.Right;
            }
            return DataGridFrozenColumnPosition.None;
        }

        private DataGridColumn GetLastColumnForPosition(DataGridFrozenColumnPosition position)
        {
            if (OwningGrid == null)
            {
                return null;
            }

            foreach (DataGridColumn column in OwningGrid.ColumnsInternal.GetDisplayedColumns(true, c => c.IsVisible && c.FrozenPosition == position))
            {
                return column;
            }

            return null;
        }

        /// <summary>
        /// Returns true if the mouse is
        /// - to the left of the element, or within the left half of the element
        /// and
        /// - within the vertical range of the element, or ignoreVertical == true
        /// </summary>
        /// <param name="mousePosition"></param>
        /// <param name="element"></param>
        /// <param name="ignoreVertical"></param>
        /// <returns></returns>
        private bool IsReorderTargeted(Point mousePosition, Control element, bool ignoreVertical)
        {
            Point position = this.Translate(element, mousePosition);

            return (position.X < 0 || (position.X >= 0 && position.X <= element.Bounds.Width / 2))
                && (ignoreVertical || (position.Y >= 0 && position.Y <= element.Bounds.Height));
        }

        /// <summary>
        /// Resets the static DataGridColumnHeader properties when a header loses mouse capture
        /// </summary>
        private void OnLostMouseCapture()
        {
            // When we stop interacting with the column headers, we need to reset the drag mode
            // and close any popups if they are open.

            if (_dragColumn != null && _dragColumn.HeaderCell != null)
            {
                _dragColumn.HeaderCell.Cursor = _originalCursor;
            }
            _dragMode = DragMode.None;
            _dragColumn = null;
            _dragStart = null;
            _lastMousePositionHeaders = null;

            if (OwningGrid != null && OwningGrid.ColumnHeaders != null)
            {
                OwningGrid.ColumnHeaders.DragColumn = null;
                OwningGrid.ColumnHeaders.DragIndicator = null;
                OwningGrid.ColumnHeaders.DropLocationIndicator = null;
            }
        }

        /// <summary>
        /// Sets up the DataGridColumnHeader for the MouseEnter event
        /// </summary>
        /// <param name="mousePosition">mouse position relative to the DataGridColumnHeader</param>
        private void OnMouseEnter(Point mousePosition)
        {
            IsMouseOver = true;
            SetDragCursor(mousePosition);
        }

        /// <summary>
        /// Sets up the DataGridColumnHeader for the MouseLeave event
        /// </summary>
        private void OnMouseLeave()
        {
            IsMouseOver = false;
        }

        private void OnMouseMove_BeginReorder(Point mousePosition)
        {
            var dragIndicator = new DataGridColumnHeader
            {
                OwningColumn = OwningColumn,
                IsEnabled = false,
                Content = GetDragIndicatorContent(Content, ContentTemplate)
            };
            if (OwningColumn != null)
            {
                OwningColumn.ApplyHeaderTheme(dragIndicator);
                dragIndicator.Classes.Replace(OwningColumn.HeaderStyleClasses);
            }
            else if (OwningGrid?.ColumnHeaderTheme is { } columnHeaderTheme)
            {
                dragIndicator.SetValue(ThemeProperty, columnHeaderTheme, BindingPriority.Template);
            }

            dragIndicator.PseudoClasses.Add(":dragIndicator");

            Control dropLocationIndicator = OwningGrid.DropLocationIndicatorTemplate?.Build();

            // If the user didn't style the dropLocationIndicator's Height, default to the column header's height
            if (dropLocationIndicator != null && double.IsNaN(dropLocationIndicator.Height) && dropLocationIndicator is Control element)
            {
                element.Height = Bounds.Height;
            }

            // pass the caret's data template to the user for modification
            DataGridColumnReorderingEventArgs columnReorderingEventArgs = new DataGridColumnReorderingEventArgs(OwningColumn)
            {
                DropLocationIndicator = dropLocationIndicator,
                DragIndicator = dragIndicator
            };
            OwningGrid.OnColumnReordering(columnReorderingEventArgs);
            if (columnReorderingEventArgs.Cancel)
            {
                return;
            }

            // The user didn't cancel, so prepare for the reorder
            _dragColumn = OwningColumn;
            _dragMode = DragMode.Reorder;
            _dragStart = mousePosition;

            // Display the reordering thumb
            OwningGrid.ColumnHeaders.DragColumn = OwningColumn;
            OwningGrid.ColumnHeaders.DragIndicator = columnReorderingEventArgs.DragIndicator;
            OwningGrid.ColumnHeaders.DropLocationIndicator = columnReorderingEventArgs.DropLocationIndicator;

            // If the user didn't style the dragIndicator's Width, default it to the column header's width
            if (double.IsNaN(dragIndicator.Width))
            {
                dragIndicator.Width = Bounds.Width;
            }
        }

#nullable enable

        private object? GetDragIndicatorContent(object? content, IDataTemplate? dataTemplate)
        {
            if (content is ContentControl icc)
            {
                content = icc.Content;
            }

            if (content is Control control)
            {
                if (VisualRoot == null) return content;
                control.Measure(Size.Infinity);
                var rect = new Rectangle()
                {
                    Width = control.DesiredSize.Width,
                    Height = control.DesiredSize.Height,
                    Fill = new VisualBrush
                    {
                        Visual = control, Stretch = Stretch.None, AlignmentX = AlignmentX.Left,
                    }
                };
                return rect;
            }

            if (dataTemplate is not null)
            {
                return dataTemplate.Build(content);
            }
            return content;
        }

#nullable disable

        //TODO DragEvents
        private void OnMouseMove_Reorder(ref bool handled, Point mousePosition, Point mousePositionHeaders)
        {
            if (handled)
            {
                return;
            }

            //handle entry into reorder mode
            if (_dragMode == DragMode.MouseDown && _dragColumn == null && _lastMousePositionHeaders != null)
            {
                var distanceFromInitial = (Vector)(mousePositionHeaders - _lastMousePositionHeaders);
                if (distanceFromInitial.Length > DATAGRIDCOLUMNHEADER_columnsDragTreshold)
                {
                    handled = CanReorderColumn(OwningColumn);

                    if (handled)
                    {
                        OnMouseMove_BeginReorder(mousePosition);
                    }
                }
            }

            //handle reorder mode (eg, positioning of the popup)
            if (_dragMode == DragMode.Reorder && OwningGrid.ColumnHeaders.DragIndicator != null)
            {
                // Find header we're hovering over
                DataGridColumn targetColumn = GetReorderingTargetColumn(mousePositionHeaders, !OwningColumn.IsFrozen /*scroll*/, out double scrollAmount);

                OwningGrid.ColumnHeaders.DragIndicatorOffset = mousePosition.X - _dragStart.Value.X + scrollAmount;
                OwningGrid.ColumnHeaders.InvalidateArrange();

                if (OwningGrid.ColumnHeaders.DropLocationIndicator != null)
                {
                    Point targetPosition = new Point(0, 0);
                    if (targetColumn == null || targetColumn == OwningGrid.ColumnsInternal.FillerColumn || targetColumn.FrozenPosition != OwningColumn.FrozenPosition)
                    {
                        targetColumn = GetLastColumnForPosition(OwningColumn.FrozenPosition);
                        if (targetColumn != null)
                        {
                            targetPosition = targetColumn.HeaderCell.Translate(OwningGrid.ColumnHeaders, targetPosition);
                            targetPosition = targetPosition.WithX(targetPosition.X + targetColumn.ActualWidth);
                        }
                    }
                    else
                    {
                        targetPosition = targetColumn.HeaderCell.Translate(OwningGrid.ColumnHeaders, targetPosition);
                    }
                    OwningGrid.ColumnHeaders.DropLocationIndicatorOffset = targetPosition.X - scrollAmount;
                }

                handled = true;
            }
        }

        private void OnMouseMove_Resize(ref bool handled, Point mousePositionHeaders)
        {
            if (handled)
            {
                return;
            }

            if (_dragMode == DragMode.Resize && _dragColumn != null && _dragStart.HasValue)
            {
                // resize column

                double mouseDelta = mousePositionHeaders.X - _dragStart.Value.X;
                double desiredWidth = _originalWidth + mouseDelta;

                desiredWidth = Math.Max(_dragColumn.ActualMinWidth, Math.Min(_dragColumn.ActualMaxWidth, desiredWidth));
                _dragColumn.Resize(_dragColumn.Width,
                    new(_dragColumn.Width.Value, _dragColumn.Width.UnitType, _dragColumn.Width.DesiredValue, desiredWidth),
                    true);

                OwningGrid.UpdateHorizontalOffset(_originalHorizontalOffset);

                handled = true;
            }
        }

        private void SetDragCursor(Point mousePosition)
        {
            if (_dragMode != DragMode.None || OwningGrid == null || OwningColumn == null)
            {
                return;
            }

            // set mouse if we can resize column

            double distanceFromLeft = mousePosition.X;
            double distanceFromRight = Bounds.Width - distanceFromLeft;
            DataGridColumn currentColumn = OwningColumn;
            DataGridColumn previousColumn = null;

            if (!(OwningColumn is DataGridFillerColumn))
            {
                previousColumn = OwningGrid.ColumnsInternal.GetPreviousVisibleNonFillerColumn(currentColumn);
            }

            if ((distanceFromRight <= DATAGRIDCOLUMNHEADER_resizeRegionWidth && currentColumn != null && CanResizeColumn(currentColumn)) ||
                (distanceFromLeft <= DATAGRIDCOLUMNHEADER_resizeRegionWidth && previousColumn != null && CanResizeColumn(previousColumn)))
            {
                var resizeCursor = _resizeCursor.Value;
                if (Cursor != resizeCursor)
                {
                    _originalCursor = Cursor;
                    Cursor = resizeCursor;
                }
            }
            else
            {
                Cursor = _originalCursor;
            }
        }

    }

}
