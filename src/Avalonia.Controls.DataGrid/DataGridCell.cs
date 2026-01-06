// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// Represents an individual <see cref="T:Avalonia.Controls.DataGrid" /> cell.
    /// </summary>
    [TemplatePart(DATAGRIDCELL_elementRightGridLine, typeof(Rectangle))]
    [PseudoClasses(":selected", ":row-selected", ":cell-selected", ":current", ":edited", ":invalid", ":warning", ":info", ":focus", ":searchmatch", ":searchcurrent")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridCell : ContentControl
    {
        private const string DATAGRIDCELL_elementRightGridLine = "PART_RightGridLine";

        private Rectangle _rightGridLine;
        private DataGridColumn _owningColumn;
        private DataGridRow _owningRow;

        bool _isValid = true;
        DataGridValidationSeverity _validationSeverity = DataGridValidationSeverity.None;

        public static readonly DirectProperty<DataGridCell, bool> IsValidProperty =
            AvaloniaProperty.RegisterDirect<DataGridCell, bool>(
                nameof(IsValid),
                o => o.IsValid);

        public static readonly DirectProperty<DataGridCell, DataGridValidationSeverity> ValidationSeverityProperty =
            AvaloniaProperty.RegisterDirect<DataGridCell, DataGridValidationSeverity>(
                nameof(ValidationSeverity),
                o => o.ValidationSeverity);

        public static readonly DirectProperty<DataGridCell, DataGridColumn> OwningColumnProperty =
            AvaloniaProperty.RegisterDirect<DataGridCell, DataGridColumn>(
                nameof(OwningColumn),
                o => o.OwningColumn,
                (o, v) => o.OwningColumn = v);

        public static readonly DirectProperty<DataGridCell, DataGridRow> OwningRowProperty =
            AvaloniaProperty.RegisterDirect<DataGridCell, DataGridRow>(
                nameof(OwningRow),
                o => o.OwningRow,
                (o, v) => o.OwningRow = v);

        static DataGridCell()
        {
            PointerPressedEvent.AddClassHandler<DataGridCell>(
                (x, e) => x.DataGridCell_PointerPressed(e),
                handledEventsToo: true);
            FocusableProperty.OverrideDefaultValue<DataGridCell>(true);
            IsTabStopProperty.OverrideDefaultValue<DataGridCell>(false);
            AutomationProperties.IsOffscreenBehaviorProperty.OverrideDefaultValue<DataGridCell>(IsOffscreenBehavior.FromClip);
        }
        public DataGridCell()
        { }

        public bool IsValid
        {
            get { return _isValid; }
            internal set { SetAndRaise(IsValidProperty, ref _isValid, value); }
        }

        public DataGridValidationSeverity ValidationSeverity
        {
            get { return _validationSeverity; }
            internal set { SetAndRaise(ValidationSeverityProperty, ref _validationSeverity, value); }
        }

        /// <summary>
        /// Gets the column that owns this cell.
        /// </summary>
        public DataGridColumn OwningColumn
        {
            get => _owningColumn;
            internal set
            {
                if (_owningColumn != value)
                {
                    SetAndRaise(OwningColumnProperty, ref _owningColumn, value);
                    OnOwningColumnSet(value);
                }
            }
        }
        /// <summary>
        /// Gets the row that owns this cell.
        /// </summary>
        public DataGridRow OwningRow
        {
            get => _owningRow;
            internal set => SetAndRaise(OwningRowProperty, ref _owningRow, value);
        }

        internal DataGrid OwningGrid
        {
            get { return OwningRow?.OwningGrid ?? OwningColumn?.OwningGrid; }
        }

        internal double ActualRightGridLineWidth
        {
            get { return _rightGridLine?.Bounds.Width ?? 0; }
        }

        internal int ColumnIndex
        {
            get { return OwningColumn?.Index ?? -1; }
        }

        internal int RowIndex
        {
            get { return OwningRow?.Index ?? -1; }
        }

        internal bool IsCurrent
        {
            get
            {
                return OwningGrid.CurrentColumnIndex == OwningColumn.Index &&
                       OwningGrid.CurrentSlot == OwningRow.Slot;
            }
        }

        private bool IsEdited
        {
            get
            {
                return OwningGrid.EditingRow == OwningRow &&
                       OwningGrid.EditingColumnIndex == ColumnIndex;
            }
        }

        private bool IsMouseOver
        {
            get
            {
                return OwningRow != null && OwningRow.MouseOverColumnIndex == ColumnIndex;
            }
            set
            {
                if (value != IsMouseOver)
                {
                    if (value)
                    {
                        OwningRow.MouseOverColumnIndex = ColumnIndex;
                    }
                    else
                    {
                        OwningRow.MouseOverColumnIndex = null;
                    }
                }
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DataGridCellAutomationPeer(this);
        }

        /// <summary>
        /// Builds the visual tree for the cell control when a new template is applied.
        /// </summary>
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            UpdatePseudoClasses();
            _rightGridLine = e.NameScope.Find<Rectangle>(DATAGRIDCELL_elementRightGridLine);
            if (_rightGridLine != null && OwningColumn == null)
            {
                // Turn off the right GridLine for filler cells
                _rightGridLine.IsVisible = false;
            }
            else
            {
                EnsureGridLine(null);
            }

        }
        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);

            if (OwningRow != null)
            {
                IsMouseOver = true;
            }
        }
        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            if (OwningRow != null)
            {
                IsMouseOver = false;
            }
        }

        //TODO TabStop
        private void DataGridCell_PointerPressed(PointerPressedEventArgs e)
        {
            // OwningGrid is null for TopLeftHeaderCell and TopRightHeaderCell because they have no OwningRow
            if (OwningGrid == null)
            {
                return;
            }
            OwningGrid.OnCellPointerPressed(new DataGridCellPointerPressedEventArgs(this, OwningRow, OwningColumn, e));

            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                if (OwningGrid.HierarchicalRowsEnabled && IsHierarchicalExpanderHit(e.Source))
                {
                    return;
                }

                var focusWithin = IsKeyboardFocusWithin;
                if (OwningGrid.IsTabStop && !focusWithin)
                {
                    OwningGrid.Focus();
                }

                if (OwningRow != null)
                {
                    KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out _);
                    bool isSelected = OwningGrid.SelectionUnit == DataGridSelectionUnit.FullRow
                        ? OwningGrid.GetRowSelection(OwningRow.Slot)
                        : OwningGrid.GetCellSelectionFromSlot(OwningRow.Slot, ColumnIndex);

                    bool shouldHandleSelection = !e.Handled || !focusWithin || !isSelected || ctrl;
                    if (shouldHandleSelection)
                    {
                        bool allowEdit = !e.Handled && focusWithin && isSelected && !ctrl &&
                                         OwningGrid.ShouldBeginEditOnPointer(e);
                        var handled = OwningGrid.UpdateStateOnMouseLeftButtonDown(e, ColumnIndex, OwningRow.Slot, allowEdit);
                        OwningGrid.TryBeginSelectionDrag(e, ColumnIndex, shouldHandleSelection, allowEdit);

                        // Do not handle PointerPressed with touch or pen,
                        // so we can start scroll gesture on the same event.
                        if (e.Pointer.Type != PointerType.Touch && e.Pointer.Type != PointerType.Pen)
                        {
                            e.Handled = handled;
                        }
                    }
                }
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                if (OwningGrid.IsTabStop)
                {
                    OwningGrid.Focus();
                }
                if (OwningRow != null && !e.Handled)
                {
                    e.Handled = OwningGrid.UpdateStateOnMouseRightButtonDown(e, ColumnIndex, OwningRow.Slot, !e.Handled);
                }
            }
        }

        private static bool IsHierarchicalExpanderHit(object? source)
        {
            if (source is not Visual visual)
            {
                return false;
            }

            var toggleButton = visual.GetSelfAndVisualAncestors().OfType<ToggleButton>().FirstOrDefault();
            if (toggleButton == null)
            {
                return false;
            }

            return toggleButton.GetVisualAncestors().OfType<DataGridHierarchicalPresenter>().Any();
        }

        internal void UpdatePseudoClasses()
        {
            if (OwningGrid == null || OwningColumn == null || OwningRow == null || !OwningRow.IsVisible || OwningRow.Slot == -1)
            {
                return;
            }

            bool rowSelected = OwningRow.IsSelected;
            bool cellSelected = OwningGrid.SelectionUnit != DataGridSelectionUnit.FullRow
                && OwningGrid.GetCellSelectionFromSlot(OwningRow.Slot, ColumnIndex);
            bool isSelected = OwningGrid.SelectionUnit == DataGridSelectionUnit.FullRow
                ? rowSelected
                : cellSelected;

            PseudoClassesHelper.Set(PseudoClasses, ":selected", isSelected);
            PseudoClassesHelper.Set(PseudoClasses, ":row-selected", rowSelected);
            PseudoClassesHelper.Set(PseudoClasses, ":cell-selected", cellSelected);
            PseudoClassesHelper.Set(PseudoClasses, ":current", IsCurrent);
            PseudoClassesHelper.Set(PseudoClasses, ":edited", IsEdited);
            PseudoClassesHelper.Set(PseudoClasses, ":invalid", ValidationSeverity == DataGridValidationSeverity.Error);
            PseudoClassesHelper.Set(PseudoClasses, ":warning", ValidationSeverity == DataGridValidationSeverity.Warning);
            PseudoClassesHelper.Set(PseudoClasses, ":info", ValidationSeverity == DataGridValidationSeverity.Info);
            PseudoClassesHelper.Set(PseudoClasses, ":focus", OwningGrid.IsFocused && IsCurrent);

            bool isSearchMatch = false;
            bool isSearchCurrent = false;
            if (OwningGrid != null)
            {
                OwningGrid.TryGetSearchCellState(OwningRow.Index, OwningColumn, out isSearchMatch, out isSearchCurrent);
            }

            PseudoClassesHelper.Set(PseudoClasses, ":searchmatch", isSearchMatch);
            PseudoClassesHelper.Set(PseudoClasses, ":searchcurrent", isSearchCurrent);
        }

        // Makes sure the right gridline has the proper stroke and visibility. If lastVisibleColumn is specified, the 
        // right gridline will be collapsed if this cell belongs to the lastVisibleColumn and there is no filler column
        internal void EnsureGridLine(DataGridColumn lastVisibleColumn)
        {
            if (OwningGrid != null && _rightGridLine != null)
            {
                if (OwningGrid.VerticalGridLinesBrush != null && OwningGrid.VerticalGridLinesBrush != _rightGridLine.Fill)
                {
                    _rightGridLine.Fill = OwningGrid.VerticalGridLinesBrush;
                }

                bool newVisibility =
                    (OwningGrid.GridLinesVisibility == DataGridGridLinesVisibility.Vertical || OwningGrid.GridLinesVisibility == DataGridGridLinesVisibility.All)
                        && (OwningGrid.ColumnsInternal.FillerColumn.IsActive || OwningColumn != lastVisibleColumn);

                if (newVisibility != _rightGridLine.IsVisible)
                {
                    _rightGridLine.IsVisible = newVisibility;
                }
            }
        }

        private void OnOwningColumnSet(DataGridColumn column)
        {
            if (column == null)
            {
                Classes.Clear();
                ClearValue(ThemeProperty);
            }
            else
            {
                if (Theme != column.CellTheme)
                {
                    Theme = column.CellTheme;
                }
                
                Classes.Replace(column.CellStyleClasses);
            }
        }
    }
}
