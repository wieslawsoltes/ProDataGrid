// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
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
    partial class DataGridCell : ContentControl
    {
        private const string DATAGRIDCELL_elementRightGridLine = "PART_RightGridLine";

        private Rectangle _rightGridLine;
        private DataGridColumn _owningColumn;
        private DataGridRow _owningRow;
        private PointerPressedEventArgs _previewedPointerPressedEvent;
        private bool _previewedPointerAccepted;
        private IInputElement _deferredPointerFocusTarget;
        private PointerPressedEventArgs _noOpPointerPressedEvent;
        private IInputElement _noOpPointerFocusTarget;
        private bool _allowNextPointerFocus;
        private bool _suppressPointerFocusUntilRelease;
        private IBrush _cachedDirectChromeBorderBrush;
        private double _cachedDirectChromeBorderThickness;
        private Pen _cachedDirectChromeBorderPen;
        private IBrush _directChromeGridLineBrush;
        private bool _directChromeGridLineVisible;
        private bool _directChromeGridLineInitialized;
        private Control _directContentControl;

        bool _isValid = true;
        DataGridValidationSeverity _validationSeverity = DataGridValidationSeverity.None;
        private CellPseudoClassFlags _pseudoClassFlags;
        private bool _hasPseudoClassFlags;

        [Flags]
        private enum CellPseudoClassFlags
        {
            None = 0,
            Selected = 1 << 0,
            RowSelected = 1 << 1,
            CellSelected = 1 << 2,
            Current = 1 << 3,
            Edited = 1 << 4,
            Invalid = 1 << 5,
            Warning = 1 << 6,
            Info = 1 << 7,
            Focus = 1 << 8,
            SearchMatch = 1 << 9,
            SearchCurrent = 1 << 10
        }

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

        /// <summary>
        /// Defines the <see cref="UseDirectChrome"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> UseDirectChromeProperty =
            AvaloniaProperty.Register<DataGridCell, bool>(nameof(UseDirectChrome));

        /// <summary>
        /// Defines the <see cref="UseDirectContentHost"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> UseDirectContentHostProperty =
            AvaloniaProperty.Register<DataGridCell, bool>(nameof(UseDirectContentHost));

        static DataGridCell()
        {
            PointerPressedEvent.AddClassHandler<DataGridCell>(
                (x, e) => x.DataGridCell_PreviewPointerPressed(e),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            PointerPressedEvent.AddClassHandler<DataGridCell>(
                (x, e) => x.DataGridCell_PointerPressed(e),
                handledEventsToo: true);
            PointerReleasedEvent.AddClassHandler<DataGridCell>(
                (x, _) => x.ClearPointerFocusSuppression(),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            PointerCaptureLostEvent.AddClassHandler<DataGridCell>(
                (x, _) => x.ClearPointerFocusSuppression(),
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
        /// Gets or sets whether the cell paints its background, border, and vertical grid line
        /// directly instead of requiring those chrome visuals in its control template.
        /// </summary>
        /// <remarks>
        /// This is an opt-in template optimization. A theme that enables it should avoid drawing
        /// the same chrome in the template.
        /// </remarks>
        public bool UseDirectChrome
        {
            get => GetValue(UseDirectChromeProperty);
            set => SetValue(UseDirectChromeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether generated retained control content is hosted directly by the
        /// cell instead of through a nested <see cref="Avalonia.Controls.Presenters.ContentPresenter"/>.
        /// Column templates and editors remain ordinary retained Avalonia controls.
        /// </summary>
        public bool UseDirectContentHost
        {
            get => GetValue(UseDirectContentHostProperty);
            set => SetValue(UseDirectContentHostProperty, value);
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
                    ResetPseudoClassCache();
                }
            }
        }
        /// <summary>
        /// Gets the row that owns this cell.
        /// </summary>
        public DataGridRow OwningRow
        {
            get => _owningRow;
            internal set
            {
                if (_owningRow != value)
                {
                    SetAndRaise(OwningRowProperty, ref _owningRow, value);
                    ResetPseudoClassCache();
                }
            }
        }

        internal DataGrid OwningGrid
        {
            get { return OwningRow?.OwningGrid ?? OwningColumn?.OwningGrid; }
        }

        internal void InvalidateMeasureForContentChange()
        {
            InvalidateMeasure();
            foreach (Visual descendant in this.GetVisualDescendants())
            {
                if (descendant is Layoutable layoutable)
                {
                    layoutable.InvalidateMeasure();
                }
            }
        }

        internal double ActualRightGridLineWidth
        {
            get { return UseDirectChrome && _directChromeGridLineVisible ? 1d : _rightGridLine?.Bounds.Width ?? 0; }
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

        /// <inheritdoc />
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (UseDirectChrome)
            {
                DataGridCellChromeRenderer.Render(
                    this,
                    context,
                    ref _cachedDirectChromeBorderBrush,
                    ref _cachedDirectChromeBorderThickness,
                    ref _cachedDirectChromeBorderPen);
            }
        }

        /// <inheritdoc />
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseDirectChromeProperty)
            {
                ResetDirectChromeCache();
                _directChromeGridLineInitialized = false;
                InvalidateVisual();
            }
            else if (change.Property == UseDirectContentHostProperty ||
                     (UseDirectContentHost && change.Property == ContentProperty))
            {
                SyncDirectContentHost();
            }
            else if (UseDirectChrome &&
                     (change.Property == BackgroundProperty ||
                      change.Property == BorderBrushProperty ||
                      change.Property == BorderThicknessProperty ||
                      change.Property == CornerRadiusProperty))
            {
                if (change.Property == BorderBrushProperty || change.Property == BorderThicknessProperty)
                {
                    ResetDirectChromeCache();
                }

                InvalidateVisual();
            }
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

            SyncDirectContentHost();

        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!UseDirectContentHost)
            {
                return base.MeasureOverride(availableSize);
            }

            EnsureDirectContentHostAttached();
            return LayoutHelper.MeasureChild(_directContentControl, availableSize, Padding);
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!UseDirectContentHost)
            {
                return base.ArrangeOverride(finalSize);
            }

            EnsureDirectContentHostAttached();
            return LayoutHelper.ArrangeChild(_directContentControl, finalSize, Padding);
        }

        private void SyncDirectContentHost()
        {
            if (_directContentControl != null)
            {
                VisualChildren.Remove(_directContentControl);
                _directContentControl = null;
            }

            if (!UseDirectContentHost || Content is not Control content)
            {
                InvalidateMeasure();
                return;
            }

            var visualParent = content.GetVisualParent();
            if (visualParent is ContentPresenter previousPresenter)
            {
                // A cell can receive the optimized theme after the default ContentControl
                // presenter has already materialized its child. Release that obsolete host
                // before attaching the retained control directly.
                previousPresenter.Content = null;
                visualParent = content.GetVisualParent();
                if (!LogicalChildren.Contains(content))
                {
                    LogicalChildren.Add(content);
                }
            }

            if (visualParent == null)
            {
                _directContentControl = content;
                VisualChildren.Add(content);
            }
            else if (ReferenceEquals(visualParent, this))
            {
                _directContentControl = content;
            }

            InvalidateMeasure();
        }

        private void EnsureDirectContentHostAttached()
        {
            if (_directContentControl == null && Content is Control content)
            {
                _directContentControl = content;
            }

            if (_directContentControl != null && _directContentControl.GetVisualParent() == null)
            {
                if (!LogicalChildren.Contains(_directContentControl))
                {
                    LogicalChildren.Add(_directContentControl);
                }

                VisualChildren.Add(_directContentControl);
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

        protected override void OnGettingFocus(FocusChangingEventArgs e)
        {
            base.OnGettingFocus(e);

            if (e.NavigationMethod != NavigationMethod.Pointer ||
                !IsSelfOrVisualDescendant(e.NewFocusedElement) ||
                OwningGrid?.ShouldDeferPointerFocusForSelectionChanging != true)
            {
                return;
            }

            if (_allowNextPointerFocus)
            {
                _allowNextPointerFocus = false;
                return;
            }

            if (e.TryCancel() && !_suppressPointerFocusUntilRelease)
            {
                _deferredPointerFocusTarget = e.NewFocusedElement;
            }
        }

        //TODO TabStop
        private void DataGridCell_PreviewPointerPressed(PointerPressedEventArgs e)
        {
            DataGrid owningGrid = OwningGrid;
            DataGridRow owningRow = OwningRow;
            if (owningGrid == null || owningRow == null || owningRow.Slot < 0)
            {
                return;
            }

            PointerPoint point = e.GetCurrentPoint(this);
            bool isTouchLike = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
            bool isPrimaryPressed = point.Properties.IsLeftButtonPressed ||
                                    (isTouchLike && owningGrid.AllowTouchDragSelection);
            if ((isPrimaryPressed || isTouchLike) &&
                owningGrid.HierarchicalRowsEnabled &&
                IsHierarchicalExpanderHit(e))
            {
                AllowOrRestorePointerFocus(e.KeyModifiers);
                return;
            }

            if (isPrimaryPressed)
            {
                KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out _);
                bool isSelected = owningGrid.SelectionUnit == DataGridSelectionUnit.FullRow
                    ? owningGrid.GetRowSelection(owningRow.Slot)
                    : owningGrid.GetCellSelectionFromSlot(owningRow.Slot, ColumnIndex);
                bool shouldHandleSelection = !e.Handled || !IsKeyboardFocusWithin || !isSelected || ctrl;
                if (!shouldHandleSelection ||
                    owningGrid.ShouldDeferSelectionForRowDrag(ColumnIndex, owningRow.Slot, isSelected, e))
                {
                    AllowOrRestorePointerFocus(e.KeyModifiers);
                    return;
                }
            }
            bool accepted = owningGrid.TryPreviewPointerPressedSelection(
                e,
                ColumnIndex,
                owningRow.Slot,
                out bool previewed);
            if (!previewed)
            {
                _noOpPointerPressedEvent = e;
                _noOpPointerFocusTarget = _deferredPointerFocusTarget;
                AllowOrRestorePointerFocus(e.KeyModifiers);
                return;
            }

            _previewedPointerPressedEvent = e;
            _previewedPointerAccepted = accepted;

            if (accepted)
            {
                AllowOrRestorePointerFocus(e.KeyModifiers);
            }

            // Mouse focus is normally assigned before the bubbling cell handler. Marking a
            // vetoed press handled in the tunnel keeps focus and every selection-related
            // property at its pre-gesture value. Touch and pen stay unhandled so scrolling
            // can still recognize the same gesture.
            if (!accepted && !isTouchLike)
            {
                _deferredPointerFocusTarget = null;
                _allowNextPointerFocus = false;
                _suppressPointerFocusUntilRelease = true;
                e.Handled = true;
            }
        }

        private bool IsSelfOrVisualDescendant(IInputElement element)
        {
            return ReferenceEquals(element, this) ||
                   element is Visual visual && visual.GetVisualAncestors().Contains(this);
        }

        private void AllowOrRestorePointerFocus(KeyModifiers keyModifiers)
        {
            _suppressPointerFocusUntilRelease = false;
            IInputElement target = _deferredPointerFocusTarget;
            _deferredPointerFocusTarget = null;
            _allowNextPointerFocus = true;
            if (target != null)
            {
                target.Focus(NavigationMethod.Pointer, keyModifiers);
                _allowNextPointerFocus = false;
            }
        }

        private void ClearPointerFocusSuppression()
        {
            _deferredPointerFocusTarget = null;
            _allowNextPointerFocus = false;
            _noOpPointerPressedEvent = null;
            _noOpPointerFocusTarget = null;
            _suppressPointerFocusUntilRelease = false;
        }

        private void DataGridCell_PointerPressed(PointerPressedEventArgs e)
        {
            // OwningGrid is null for TopLeftHeaderCell and TopRightHeaderCell because they have no OwningRow
            if (OwningGrid == null)
            {
                return;
            }
            var owningRow = OwningRow;
            if (owningRow == null || owningRow.Slot < 0)
            {
                return;
            }

            OwningGrid.OnCellPointerPressed(new DataGridCellPointerPressedEventArgs(this, owningRow, OwningColumn, e));

            _allowNextPointerFocus = false;
            IInputElement noOpPointerFocusTarget = null;
            if (ReferenceEquals(_noOpPointerPressedEvent, e))
            {
                noOpPointerFocusTarget = _noOpPointerFocusTarget;
                _noOpPointerPressedEvent = null;
                _noOpPointerFocusTarget = null;
            }

            DataGrid.SelectionCommitScope pointerSelectionCommit = default;
            bool hasAcceptedPreview = false;
            if (ReferenceEquals(_previewedPointerPressedEvent, e))
            {
                bool accepted = _previewedPointerAccepted;
                _previewedPointerPressedEvent = null;
                _previewedPointerAccepted = false;
                if (!accepted)
                {
                    return;
                }

                hasAcceptedPreview = true;
                pointerSelectionCommit = OwningGrid.BeginSelectionCommit();
            }

            using var pointerSelectionTransaction = pointerSelectionCommit;

            var point = e.GetCurrentPoint(this);
            var isTouchLike = e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen;
            var isPrimaryPressed = point.Properties.IsLeftButtonPressed ||
                                   (isTouchLike && OwningGrid.AllowTouchDragSelection);
            if ((isPrimaryPressed || isTouchLike) &&
                OwningGrid.HierarchicalRowsEnabled &&
                IsHierarchicalExpanderHit(e))
            {
                return;
            }

            if (isPrimaryPressed)
            {
                var focusWithin = IsKeyboardFocusWithin;
                var focusGridAfterAcceptedSelection = OwningGrid.IsTabStop && !focusWithin;

                if (OwningRow != null)
                {
                    KeyboardHelper.GetMetaKeyState(this, e.KeyModifiers, out bool ctrl, out _);
                    bool isSelected = OwningGrid.SelectionUnit == DataGridSelectionUnit.FullRow
                        ? OwningGrid.GetRowSelection(OwningRow.Slot)
                        : OwningGrid.GetCellSelectionFromSlot(OwningRow.Slot, ColumnIndex);

                    bool shouldHandleSelection = !e.Handled || !focusWithin || !isSelected || ctrl;
                    if (shouldHandleSelection)
                    {
                        var deferSelectionForRowDrag =
                            OwningGrid.ShouldDeferSelectionForRowDrag(ColumnIndex, OwningRow.Slot, isSelected, e);
                        bool allowEdit = !e.Handled && focusWithin && isSelected && !ctrl &&
                                         OwningGrid.ShouldBeginEditOnPointer(e);
                        var handled = deferSelectionForRowDrag
                            ? true
                            : OwningGrid.UpdateStateOnMouseLeftButtonDown(e, ColumnIndex, OwningRow.Slot, allowEdit);
                        var selectionAccepted = deferSelectionForRowDrag || OwningGrid.SuccessfullyUpdatedSelection;
                        if (focusGridAfterAcceptedSelection && selectionAccepted)
                        {
                            OwningGrid.Focus();
                        }
                        if (selectionAccepted &&
                            !OwningGrid.ShouldSuppressSelectionDragFromRowDragHandle(ColumnIndex))
                        {
                            OwningGrid.TryBeginSelectionDrag(e, ColumnIndex, shouldHandleSelection);
                        }

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
                // Avalonia can mark the bubbling pointer event handled after the tunnel
                // preview. An accepted preview still owns exactly one commit; a veto has
                // already returned above and therefore cannot reach this branch.
                if (OwningRow != null && (!e.Handled || hasAcceptedPreview))
                {
                    e.Handled = OwningGrid.UpdateStateOnMouseRightButtonDown(e, ColumnIndex, OwningRow.Slot, !e.Handled);
                    if (OwningGrid.IsTabStop && OwningGrid.SuccessfullyUpdatedSelection)
                    {
                        OwningGrid.Focus();
                    }
                }

                // A no-op right press follows Avalonia's normal cell-focus path. Some routed
                // input hosts mark the bubble handled after the tunnel, so re-apply the focus
                // target that was deferred by OnGettingFocus instead of shifting focus to the
                // grid or leaving the previously focused cell active.
                if (noOpPointerFocusTarget != null)
                {
                    _allowNextPointerFocus = true;
                    noOpPointerFocusTarget.Focus(NavigationMethod.Pointer, e.KeyModifiers);
                    _allowNextPointerFocus = false;
                }
            }
        }

        private bool IsHierarchicalExpanderHit(PointerPressedEventArgs e)
        {
            if (this is DataGridDirectHierarchicalCell directCell &&
                directCell.IsLeanExpanderHit(e.GetPosition(directCell)))
            {
                return true;
            }

            var source = e.Source;
            if (source is not Visual visual)
            {
                return false;
            }

            var toggleButton = visual.GetSelfAndVisualAncestors().OfType<ToggleButton>().FirstOrDefault();
            if (toggleButton == null)
            {
                return false;
            }

            return toggleButton.GetVisualAncestors().Any(ancestor =>
                ancestor is DataGridHierarchicalPresenter or DataGridDirectHierarchicalCell);
        }

        internal void UpdatePseudoClasses()
        {
            var owningGrid = OwningGrid;
            var owningColumn = OwningColumn;
            var owningRow = OwningRow;

            if (owningGrid == null || owningColumn == null || owningRow == null || !owningRow.IsVisible || owningRow.Slot == -1)
            {
                ResetPseudoClassCache();
                return;
            }

            bool rowSelected = owningRow.IsSelected;
            bool cellSelected = owningGrid.SelectionUnit != DataGridSelectionUnit.FullRow
                && owningGrid.GetCellSelectionFromSlot(owningRow.Slot, ColumnIndex);
            bool isSelected = owningGrid.SelectionUnit == DataGridSelectionUnit.FullRow
                ? rowSelected
                : cellSelected;
            bool isCurrent = owningGrid.CurrentColumnIndex == owningColumn.Index &&
                             owningGrid.CurrentSlot == owningRow.Slot;
            bool isEdited = owningGrid.EditingRow == owningRow &&
                            owningGrid.EditingColumnIndex == ColumnIndex;
            bool isInvalid = ValidationSeverity == DataGridValidationSeverity.Error;
            bool isWarning = ValidationSeverity == DataGridValidationSeverity.Warning;
            bool isInfo = ValidationSeverity == DataGridValidationSeverity.Info;
            bool isFocus = owningGrid.IsFocused && isCurrent;

            owningGrid.TryGetSearchCellState(owningRow.Index, owningColumn, out bool isSearchMatch, out bool isSearchCurrent);

            var nextFlags = BuildPseudoClassFlags(
                isSelected,
                rowSelected,
                cellSelected,
                isCurrent,
                isEdited,
                isInvalid,
                isWarning,
                isInfo,
                isFocus,
                isSearchMatch,
                isSearchCurrent);

            if (_hasPseudoClassFlags && _pseudoClassFlags == nextFlags)
            {
                return;
            }

            SetPseudoClassFlag(":selected", nextFlags, CellPseudoClassFlags.Selected);
            SetPseudoClassFlag(":row-selected", nextFlags, CellPseudoClassFlags.RowSelected);
            SetPseudoClassFlag(":cell-selected", nextFlags, CellPseudoClassFlags.CellSelected);
            SetPseudoClassFlag(":current", nextFlags, CellPseudoClassFlags.Current);
            SetPseudoClassFlag(":edited", nextFlags, CellPseudoClassFlags.Edited);
            SetPseudoClassFlag(":invalid", nextFlags, CellPseudoClassFlags.Invalid);
            SetPseudoClassFlag(":warning", nextFlags, CellPseudoClassFlags.Warning);
            SetPseudoClassFlag(":info", nextFlags, CellPseudoClassFlags.Info);
            SetPseudoClassFlag(":focus", nextFlags, CellPseudoClassFlags.Focus);
            SetPseudoClassFlag(":searchmatch", nextFlags, CellPseudoClassFlags.SearchMatch);
            SetPseudoClassFlag(":searchcurrent", nextFlags, CellPseudoClassFlags.SearchCurrent);

            _pseudoClassFlags = nextFlags;
            _hasPseudoClassFlags = true;
        }

        private void ResetPseudoClassCache()
        {
            _hasPseudoClassFlags = false;
            _pseudoClassFlags = CellPseudoClassFlags.None;
        }

        private void SetPseudoClassFlag(string pseudoClass, CellPseudoClassFlags nextFlags, CellPseudoClassFlags flag)
        {
            if (!_hasPseudoClassFlags || ((_pseudoClassFlags ^ nextFlags) & flag) != 0)
            {
                PseudoClassesHelper.Set(PseudoClasses, pseudoClass, (nextFlags & flag) != 0);
            }
        }

        private static CellPseudoClassFlags BuildPseudoClassFlags(
            bool isSelected,
            bool rowSelected,
            bool cellSelected,
            bool isCurrent,
            bool isEdited,
            bool isInvalid,
            bool isWarning,
            bool isInfo,
            bool isFocus,
            bool isSearchMatch,
            bool isSearchCurrent)
        {
            var flags = CellPseudoClassFlags.None;
            if (isSelected)
            {
                flags |= CellPseudoClassFlags.Selected;
            }

            if (rowSelected)
            {
                flags |= CellPseudoClassFlags.RowSelected;
            }

            if (cellSelected)
            {
                flags |= CellPseudoClassFlags.CellSelected;
            }

            if (isCurrent)
            {
                flags |= CellPseudoClassFlags.Current;
            }

            if (isEdited)
            {
                flags |= CellPseudoClassFlags.Edited;
            }

            if (isInvalid)
            {
                flags |= CellPseudoClassFlags.Invalid;
            }

            if (isWarning)
            {
                flags |= CellPseudoClassFlags.Warning;
            }

            if (isInfo)
            {
                flags |= CellPseudoClassFlags.Info;
            }

            if (isFocus)
            {
                flags |= CellPseudoClassFlags.Focus;
            }

            if (isSearchMatch)
            {
                flags |= CellPseudoClassFlags.SearchMatch;
            }

            if (isSearchCurrent)
            {
                flags |= CellPseudoClassFlags.SearchCurrent;
            }

            return flags;
        }

        // Makes sure the right gridline has the proper stroke and visibility. If lastVisibleColumn is specified, the 
        // right gridline will be collapsed if this cell belongs to the lastVisibleColumn and there is no filler column
        internal void EnsureGridLine(DataGridColumn lastVisibleColumn)
        {
            if (OwningGrid != null && UseDirectChrome)
            {
                lastVisibleColumn ??= OwningGrid.ColumnsInternal.LastVisibleColumn;
                IBrush brush = OwningGrid.VerticalGridLinesBrush;
                bool newVisibility =
                    brush != null &&
                    (OwningGrid.GridLinesVisibility == DataGridGridLinesVisibility.Vertical ||
                     OwningGrid.GridLinesVisibility == DataGridGridLinesVisibility.All) &&
                    (OwningGrid.ColumnsInternal.FillerColumn.IsActive || OwningColumn != lastVisibleColumn);
                if (!_directChromeGridLineInitialized ||
                    !ReferenceEquals(_directChromeGridLineBrush, brush) ||
                    _directChromeGridLineVisible != newVisibility)
                {
                    _directChromeGridLineInitialized = true;
                    _directChromeGridLineBrush = brush;
                    _directChromeGridLineVisible = newVisibility;
                    InvalidateVisual();
                }

                return;
            }

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

        private void ResetDirectChromeCache()
        {
            _cachedDirectChromeBorderBrush = null;
            _cachedDirectChromeBorderPen = null;
            _cachedDirectChromeBorderThickness = 0d;
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
