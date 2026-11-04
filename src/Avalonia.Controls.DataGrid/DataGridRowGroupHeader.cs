// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Collections;
using System;
using Avalonia.LogicalTree;
using Avalonia.Reactive;

namespace Avalonia.Controls
{
    [TemplatePart(DATAGRIDROWGROUPHEADER_expanderButton,      typeof(ToggleButton))]
    [TemplatePart(DATAGRIDROWGROUPHEADER_indentSpacer,        typeof(Control))]
    [TemplatePart(DATAGRIDROWGROUPHEADER_itemCountElement,    typeof(TextBlock))]
    [TemplatePart(DATAGRIDROWGROUPHEADER_propertyNameElement, typeof(TextBlock))]
    [TemplatePart(DATAGRIDROWGROUPHEADER_elementSummaryRow,   typeof(DataGridSummaryRow))]
    [TemplatePart(DataGridRow.DATAGRIDROW_elementRoot,        typeof(Panel))]
    [TemplatePart(DataGridRow.DATAGRIDROW_elementRowHeader,   typeof(DataGridRowHeader))]
    [PseudoClasses(":pressed", ":current", ":expanded")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowGroupHeader : TemplatedControl
    {
        private const string DATAGRIDROWGROUPHEADER_expanderButton = "PART_ExpanderButton";
        private const string DATAGRIDROWGROUPHEADER_indentSpacer = "PART_IndentSpacer";
        private const string DATAGRIDROWGROUPHEADER_itemCountElement = "PART_ItemCountElement";
        private const string DATAGRIDROWGROUPHEADER_propertyNameElement = "PART_PropertyNameElement";
        private const string DATAGRIDROWGROUPHEADER_elementSummaryRow = "PART_SummaryRow";

        private bool _areIsCheckedHandlersSuspended;
        private ToggleButton _expanderButton;
        private DataGridRowHeader _headerElement;
        private Control _indentSpacer;
        private TextBlock _itemCountElement;
        private TextBlock _propertyNameElement;
        private Panel _rootElement;
        private DataGridSummaryRow _summaryRow;
        private double _totalIndent;
        private DataGrid _owningGrid;

        public static readonly StyledProperty<bool> IsItemCountVisibleProperty =
            AvaloniaProperty.Register<DataGridRowGroupHeader, bool>(nameof(IsItemCountVisible));

        /// <summary>
        /// Gets or sets a value that indicates whether the item count is visible.
        /// </summary>
        public bool IsItemCountVisible
        {
            get { return GetValue(IsItemCountVisibleProperty); }
            set { SetValue(IsItemCountVisibleProperty, value); }
        }


        public static readonly StyledProperty<string> ItemCountFormatProperty =
            AvaloniaProperty.Register<DataGridRowGroupHeader, string>(nameof(ItemCountFormat));

        /// <summary>
        /// Gets or sets a value that indicates number format of items count
        /// </summary>
        public string ItemCountFormat
        {
            get { return GetValue(ItemCountFormatProperty); }
            set { SetValue(ItemCountFormatProperty, value); }
        }


        public static readonly StyledProperty<string> PropertyNameProperty =
            AvaloniaProperty.Register<DataGridRowGroupHeader, string>(nameof(PropertyName));

        /// <summary>
        /// Gets or sets the name of the property that this <see cref="T:Avalonia.Controls.DataGrid" /> row is bound to.
        /// </summary>
        public string PropertyName
        {
            get { return GetValue(PropertyNameProperty); }
            set { SetValue(PropertyNameProperty, value); }
        }

        public static readonly StyledProperty<bool> IsPropertyNameVisibleProperty =
            AvaloniaProperty.Register<DataGridRowGroupHeader, bool>(nameof(IsPropertyNameVisible));

        /// <summary>
        /// Gets or sets a value that indicates whether the property name is visible.
        /// </summary>
        public bool IsPropertyNameVisible
        {
            get { return GetValue(IsPropertyNameVisibleProperty); }
            set { SetValue(IsPropertyNameVisibleProperty, value); }
        }

        public static readonly StyledProperty<VerticalAlignment> ContentVerticalAlignmentProperty =
            AvaloniaProperty.Register<DataGridRowGroupHeader, VerticalAlignment>(
                nameof(ContentVerticalAlignment),
                VerticalAlignment.Center);

        /// <summary>
        /// Gets or sets the vertical alignment for the row group header content.
        /// </summary>
        public VerticalAlignment ContentVerticalAlignment
        {
            get { return GetValue(ContentVerticalAlignmentProperty); }
            set { SetValue(ContentVerticalAlignmentProperty, value); }
        }

        public static readonly StyledProperty<double> SublevelIndentProperty =
            AvaloniaProperty.Register<DataGridRowGroupHeader, double>(
                nameof(SublevelIndent),
                defaultValue: DataGrid.DATAGRID_defaultRowGroupSublevelIndent,
                validate: IsValidSublevelIndent);

        public static readonly DirectProperty<DataGridRowGroupHeader, DataGrid> OwningGridProperty =
            AvaloniaProperty.RegisterDirect<DataGridRowGroupHeader, DataGrid>(
                nameof(OwningGrid),
                o => o.OwningGrid,
                (o, v) => o.OwningGrid = v);

        private static bool IsValidSublevelIndent(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        }

        /// <summary>
        /// Gets or sets a value that indicates the amount that the
        /// children of the <see cref="T:Avalonia.Controls.RowGroupHeader" /> are indented.
        /// </summary>
        public double SublevelIndent
        {
            get { return GetValue(SublevelIndentProperty); }
            set { SetValue(SublevelIndentProperty, value); }
        }

        private void OnSublevelIndentChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (OwningGrid != null)
            {
                OwningGrid.OnSublevelIndentUpdated(this, (double)e.NewValue);
            }
        }

        static DataGridRowGroupHeader()
        {
            SublevelIndentProperty.Changed.AddClassHandler<DataGridRowGroupHeader>((x,e) => x.OnSublevelIndentChanged(e));
            PressedMixin.Attach<DataGridRowGroupHeader>();
            IsTabStopProperty.OverrideDefaultValue<DataGridRowGroupHeader>(false);
        }

        /// <summary>
        /// Constructs a DataGridRowGroupHeader
        /// </summary>
        public DataGridRowGroupHeader()
        {
            AddHandler(InputElement.PointerPressedEvent, (s, e) => DataGridRowGroupHeader_PointerPressed(e));
        }

        internal DataGridRowHeader HeaderCell
        {
            get
            {
                return _headerElement;
            }
        }

        private bool IsCurrent
        {
            get
            {
                return RowGroupInfo.Slot == OwningGrid?.CurrentSlot;
            }
        }

        private bool IsMouseOver
        {
            get;
            set;
        }

        internal bool IsRecycled
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the grid that owns this row group header.
        /// </summary>
        public DataGrid OwningGrid
        {
            get => _owningGrid;
            internal set
            {
                if (SetAndRaise(OwningGridProperty, ref _owningGrid, value) && _summaryRow != null)
                {
                    _summaryRow.OwningGrid = value;
                    ApplySummaryRowTheme();
                    UpdateSummaryRowOffset();
                    UpdateSummaryRowState();
                }
            }
        }

        internal int Level
        {
            get;
            set;
        }

        internal DataGridRowGroupInfo RowGroupInfo
        {
            get;
            set;
        }

        internal DataGridSummaryRow SummaryRow => _summaryRow;

        internal double TotalIndent
        {
            set
            {
                _totalIndent = value;
                if (_indentSpacer != null)
                {
                    _indentSpacer.Width = _totalIndent;
                }
            }
        }

        private IDisposable _expanderButtonSubscription;

        private void AttachExpanderButtonSubscription()
        {
            if (_expanderButton == null || _expanderButtonSubscription != null)
            {
                return;
            }

            EnsureExpanderButtonIsChecked();
            _expanderButtonSubscription =
                _expanderButton.GetObservable(ToggleButton.IsCheckedProperty)
                               .Skip(1)
                               .Subscribe(v => OnExpanderButtonIsCheckedChanged(v));
        }

        private void DetachExpanderButtonSubscription()
        {
            _expanderButtonSubscription?.Dispose();
            _expanderButtonSubscription = null;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            _rootElement = e.NameScope.Find<Panel>(DataGridRow.DATAGRIDROW_elementRoot);

            DetachExpanderButtonSubscription();
            _expanderButton = e.NameScope.Find<ToggleButton>(DATAGRIDROWGROUPHEADER_expanderButton);
            if(_expanderButton != null)
            {
                AttachExpanderButtonSubscription();
            }

            _headerElement = e.NameScope.Find<DataGridRowHeader>(DataGridRow.DATAGRIDROW_elementRowHeader);
            if(_headerElement != null)
            {
                _headerElement.Owner = this;
                EnsureHeaderVisibility();
            }

            _indentSpacer = e.NameScope.Find<Control>(DATAGRIDROWGROUPHEADER_indentSpacer);
            if(_indentSpacer != null)
            {
                _indentSpacer.Width = _totalIndent;
            }

            _itemCountElement = e.NameScope.Find<TextBlock>(DATAGRIDROWGROUPHEADER_itemCountElement);
            _propertyNameElement = e.NameScope.Find<TextBlock>(DATAGRIDROWGROUPHEADER_propertyNameElement);
            UpdateTitleElements();

            _summaryRow = e.NameScope.Find<DataGridSummaryRow>(DATAGRIDROWGROUPHEADER_elementSummaryRow);
            if (_summaryRow != null)
            {
                _summaryRow.OwningGrid = OwningGrid;
                _summaryRow.Scope = DataGridSummaryScope.Group;
                _summaryRow.Group = RowGroupInfo?.CollectionViewGroup;
                _summaryRow.Level = Level;
                ApplySummaryRowTheme();
                UpdateSummaryRowOffset();
                UpdateSummaryRowState();
            }
        }

        internal void SetRecycledBounds(Rect bounds)
        {
            Bounds = bounds;
        }

        internal void ApplyHeaderStatus()
        {
            if (_headerElement != null && OwningGrid.AreRowHeadersVisible)
            {
                _headerElement.UpdatePseudoClasses();
            }
        }

        internal void UpdatePseudoClasses()
        {
            PseudoClassesHelper.Set(PseudoClasses, ":current", IsCurrent);

            if (RowGroupInfo?.CollectionViewGroup != null)
            {
                PseudoClassesHelper.Set(PseudoClasses, ":expanded", RowGroupInfo.IsVisible && RowGroupInfo.CollectionViewGroup.ItemCount > 0);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (OwningGrid == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            Size size = base.ArrangeOverride(finalSize);
            if (_rootElement != null)
            {
                if (OwningGrid.AreRowGroupHeadersFrozen)
                {
                    foreach (Control child in _rootElement.Children)
                    {
                        child.Clip = null;
                    }
                }
                else
                {
                    double frozenLeftEdge = 0;
                    foreach (Control child in _rootElement.Children)
                    {
                        if (DataGridFrozenGrid.GetIsFrozen(child) && child.IsVisible)
                        {
                            TranslateTransform transform = new TranslateTransform();
                            // Automatic layout rounding doesn't apply to transforms so we need to Round this
                            transform.X = Math.Round(OwningGrid.HorizontalOffset);
                            child.RenderTransform = transform;

                            double childLeftEdge = child.Translate(this, new Point(child.Bounds.Width, 0)).X - transform.X;
                            frozenLeftEdge = Math.Max(frozenLeftEdge, childLeftEdge + OwningGrid.HorizontalOffset);
                        }
                    }
                    // Clip the non-frozen elements so they don't overlap the frozen ones
                    foreach (Control child in _rootElement.Children)
                    {
                        if (!DataGridFrozenGrid.GetIsFrozen(child))
                        {
                            EnsureChildClip(child, frozenLeftEdge);
                        }
                    }
                }
            }
            return size;
        }

        internal void ClearFrozenStates()
        {
            if (_rootElement != null)
            {
                foreach (Control child in _rootElement.Children)
                {
                    child.RenderTransform = null;
                }
            }
        }

        //TODO TabStop
        private void DataGridRowGroupHeader_PointerPressed(PointerPressedEventArgs e)
        {
            if (OwningGrid == null)
            {
                return;
            }

            if (e.Handled)
            {
                return;
            }
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (OwningGrid.IsDoubleClickRecordsClickOnCall(this) && !e.Handled)
                {
                    ToggleExpandCollapse(!RowGroupInfo.IsVisible, true);
                    e.Handled = true;
                }
                else
                {
                    e.Handled = OwningGrid.UpdateStateOnMouseLeftButtonDown(e, OwningGrid.CurrentColumnIndex, RowGroupInfo.Slot, allowEdit: false);
                    if (OwningGrid.IsTabStop && OwningGrid.SuccessfullyUpdatedSelection)
                    {
                        OwningGrid.Focus();
                    }
                }
            }
            else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                e.Handled = OwningGrid.UpdateStateOnMouseRightButtonDown(e, OwningGrid.CurrentColumnIndex, RowGroupInfo.Slot, allowEdit: false);
                if (OwningGrid.SuccessfullyUpdatedSelection)
                {
                    OwningGrid.Focus();
                }
            }

        }

        private void EnsureChildClip(Visual child, double frozenLeftEdge)
        {
            double childLeftEdge = child.Translate(this, new Point(0, 0)).X;
            if (frozenLeftEdge > childLeftEdge)
            {
                double xClip = Math.Round(frozenLeftEdge - childLeftEdge);
                var rg = new RectangleGeometry();
                rg.Rect =
                    new Rect(xClip, 0,
                        Math.Max(0, child.Bounds.Width - xClip),
                        child.Bounds.Height);
                child.Clip = rg;
            }
            else
            {
                child.Clip = null;
            }
        }

        internal void EnsureExpanderButtonIsChecked()
        {
            if (_expanderButton != null && RowGroupInfo != null && RowGroupInfo.CollectionViewGroup != null &&
                RowGroupInfo.CollectionViewGroup.ItemCount != 0)
            {
                SetIsCheckedNoCallBack(RowGroupInfo.IsVisible);
            }
        }

        internal void EnsureHeaderVisibility()
        {
            if (_headerElement != null && OwningGrid != null)
            {
                _headerElement.IsVisible = OwningGrid.AreRowHeadersVisible;
            }
        }

        private void OnExpanderButtonIsCheckedChanged(bool? value)
        {
            if(!_areIsCheckedHandlersSuspended)
            {
                ToggleExpandCollapse(value ?? false, true);
            }
        }

        internal void LoadVisualsForDisplay()
        {
            // Restore visibility for group headers that were hidden during recycling
            ClearValue(Visual.IsVisibleProperty);
            
            EnsureExpanderButtonIsChecked();
            EnsureHeaderVisibility();
            if (OwningGrid != null)
            {
                var group = DataContext as DataGridCollectionViewGroup ?? RowGroupInfo?.CollectionViewGroup;
                var info = group != null ? OwningGrid.RowGroupInfoFromCollectionViewGroup(group) : RowGroupInfo;
                if (info != null)
                {
                    OwningGrid.SyncRowGroupHeaderInfo(this, info);
                }
                else
                {
                    OwningGrid.ApplyRowGroupHeaderIndent(this);
                }
            }
            UpdatePseudoClasses();
            ApplyHeaderStatus();
            ApplySummaryRowTheme();
            UpdateSummaryRowOffset();
            UpdateSummaryRowState();
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            if (IsEnabled)
            {
                IsMouseOver = true;
                UpdatePseudoClasses();
            }

            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            if (IsEnabled)
            {
                IsMouseOver = false;
                UpdatePseudoClasses();
            }

            base.OnPointerExited(e);
        }

        private void SetIsCheckedNoCallBack(bool value)
        {
            if (_expanderButton != null && _expanderButton.IsChecked != value)
            {
                _areIsCheckedHandlersSuspended = true;
                try
                {
                    _expanderButton.IsChecked = value;
                }
                finally
                {
                    _areIsCheckedHandlersSuspended = false;
                }
            }
        }

        internal void ToggleExpandCollapse(bool isVisible, bool setCurrent)
        {
            if (RowGroupInfo.CollectionViewGroup.ItemCount != 0)
            {
                if (OwningGrid == null)
                {
                    // Do these even if the OwningGrid is null in case it could improve the Designer experience for a standalone DataGridRowGroupHeader
                    RowGroupInfo.IsVisible = isVisible;
                }
                else if(RowGroupInfo.IsVisible != isVisible)
                {
                    OwningGrid.OnRowGroupHeaderToggled(this, isVisible, setCurrent);
                }

                EnsureExpanderButtonIsChecked();

                UpdatePseudoClasses();
            }
        }

        internal void UpdateTitleElements()
        {
            if (_propertyNameElement != null)
            {
                string txt;
                if (string.IsNullOrWhiteSpace(PropertyName))
                    txt = String.Empty;
                else
                    txt = String.Format("{0}:", PropertyName);
                _propertyNameElement.Text = txt;
            }
            if (_itemCountElement != null && RowGroupInfo != null && RowGroupInfo.CollectionViewGroup != null)
            {
                string formatString;
                if (RowGroupInfo.CollectionViewGroup.ItemCount == 1)
                    formatString = (ItemCountFormat == null ? "({0} Item)" : ItemCountFormat);
                else
                    formatString = (ItemCountFormat == null ? "({0} Items)" : ItemCountFormat);

                _itemCountElement.Text = String.Format(formatString, RowGroupInfo.CollectionViewGroup.ItemCount);
            }
        }

        private bool ShouldShowSummaryRow =>
            OwningGrid != null &&
            OwningGrid.ShowGroupSummary &&
            (OwningGrid.GroupSummaryPosition == DataGridGroupSummaryPosition.Header ||
             OwningGrid.GroupSummaryPosition == DataGridGroupSummaryPosition.Both);

        internal void ApplySummaryRowTheme()
        {
            if (_summaryRow == null)
            {
                return;
            }

            if (OwningGrid?.SummaryRowTheme != null)
            {
                _summaryRow.Theme = OwningGrid.SummaryRowTheme;
            }
            else
            {
                _summaryRow.ClearValue(ThemeProperty);
            }
        }

        internal void UpdateSummaryRowState()
        {
            if (_summaryRow == null)
            {
                return;
            }

            _summaryRow.Group = RowGroupInfo?.CollectionViewGroup;
            _summaryRow.Level = Level;
            _summaryRow.IsVisible = ShouldShowSummaryRow;

            if (_summaryRow.IsVisible)
            {
                if (_summaryRow.CellsPresenter != null && OwningGrid != null && _summaryRow.Cells.Count != OwningGrid.ColumnsItemsInternal.Count)
                {
                    _summaryRow.EnsureCells();
                }

                _summaryRow.Recalculate();
            }
        }

        internal void UpdateSummaryRowOffset()
        {
            if (_summaryRow == null)
            {
                return;
            }

            _summaryRow.ApplyHorizontalOffset = OwningGrid?.AreRowGroupHeadersFrozen == true;
        }

        internal void UpdateSummaryRowLayout()
        {
            _summaryRow?.UpdateCellLayout();
        }

        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);
            AttachExpanderButtonSubscription();
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            DetachExpanderButtonSubscription();
            if (OwningGrid != null && RowGroupInfo?.CollectionViewGroup is { } collectionViewGroup)
            {
                OwningGrid.RemoveReferenceFromCollectionViewGroup(collectionViewGroup);
            }

            OwningGrid = null;
        }
    }
}
