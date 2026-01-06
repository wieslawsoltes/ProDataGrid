// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System;
using System.Diagnostics;
using Avalonia.Automation.Peers;
using Avalonia.Reactive;
using Avalonia.Automation;

namespace Avalonia.Controls
{
    /// <summary>
    /// Represents a <see cref="T:Avalonia.Controls.DataGrid" /> row.
    /// </summary>
    [TemplatePart(DATAGRIDROW_elementBottomGridLine, typeof(Rectangle))]
    [TemplatePart(DATAGRIDROW_elementCells,          typeof(DataGridCellsPresenter))]
    [TemplatePart(DATAGRIDROW_elementDetails,        typeof(DataGridDetailsPresenter))]
    [TemplatePart(DATAGRIDROW_elementRoot,           typeof(Panel))]
    [TemplatePart(DATAGRIDROW_elementRowHeader,      typeof(DataGridRowHeader))]
    [PseudoClasses(":selected", ":editing", ":invalid", ":warning", ":info", ":current", ":pointerover", ":dragging", ":drag-over-before", ":drag-over-after", ":drag-over-inside", ":placeholder", ":searchmatch", ":searchcurrent")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    partial class DataGridRow : TemplatedControl
    {

        private const byte DATAGRIDROW_defaultMinHeight = 0;
        internal const int DATAGRIDROW_maximumHeight = 65536;
        internal const double DATAGRIDROW_minimumHeight = 0;

        private const string DATAGRIDROW_elementBottomGridLine = "PART_BottomGridLine";
        private const string DATAGRIDROW_elementCells = "PART_CellsPresenter";
        private const string DATAGRIDROW_elementDetails = "PART_DetailsPresenter";
        internal const string DATAGRIDROW_elementRoot = "PART_Root";
        internal const string DATAGRIDROW_elementRowHeader = "PART_RowHeader";

        private DataGridCellsPresenter _cellsElement;
        private DataGridCell _fillerCell;
        private DataGridRowHeader _headerElement;
        private double _lastHorizontalOffset;
        private int? _mouseOverColumnIndex;
        private DataGrid _owningGrid;
        private bool _isValid = true;
        private DataGridValidationSeverity _validationSeverity = DataGridValidationSeverity.None;
        private bool _isPlaceholder;
        private Rectangle _bottomGridLine;
        private bool _areHandlersSuspended;

        // In the case where Details scales vertically when it's arranged at a different width, we
        // get the wrong height measurement so we need to check it again after arrange
        private bool _checkDetailsContentHeight;

        // Optimal height of the details based on the Element created by the DataTemplate
        private double _detailsDesiredHeight;

        private bool _detailsLoaded;
        private bool _detailsVisibilityNotificationPending;
        private Control _detailsContent;
        private IDisposable _detailsContentSizeSubscription;
        private DataGridDetailsPresenter _detailsElement;
        private bool _isSelected;
        internal object RecycledDataContext;
        internal bool RecycledIsPlaceholder;

        // Locally cache whether or not details are visible so we don't run redundant storyboards
        // The Details Template that is actually applied to the Row
        private IDataTemplate _appliedDetailsTemplate;

        private bool? _appliedDetailsVisibility;

        /// <summary>
        /// Identifies the Header dependency property.
        /// </summary>
        public static readonly StyledProperty<object> HeaderProperty =
            AvaloniaProperty.Register<DataGridRow, object>(nameof(Header));

        /// <summary>
        /// Gets or sets the row header.
        /// </summary>
        public object Header
        {
            get { return GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DirectProperty<DataGridRow, bool> IsSelectedProperty =
            AvaloniaProperty.RegisterDirect<DataGridRow, bool>(
                nameof(IsSelected),
                o => o.IsSelected,
                (o, v) => o.IsSelected = v);

        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        }

        public static readonly DirectProperty<DataGridRow, bool> IsValidProperty =
            AvaloniaProperty.RegisterDirect<DataGridRow, bool>(
                nameof(IsValid),
                o => o.IsValid);

        public static readonly DirectProperty<DataGridRow, DataGridValidationSeverity> ValidationSeverityProperty =
            AvaloniaProperty.RegisterDirect<DataGridRow, DataGridValidationSeverity>(
                nameof(ValidationSeverity),
                o => o.ValidationSeverity);

        /// <summary>
        /// Gets a value that indicates whether the data in a row is valid.
        /// </summary>
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

        public static readonly StyledProperty<IDataTemplate> DetailsTemplateProperty =
            AvaloniaProperty.Register<DataGridRow, IDataTemplate>(nameof(DetailsTemplate));

        /// <summary>
        /// Gets or sets the template that is used to display the details section of the row.
        /// </summary>
        public IDataTemplate DetailsTemplate
        {
            get { return GetValue(DetailsTemplateProperty); }
            set { SetValue(DetailsTemplateProperty, value); }
        }

        public static readonly StyledProperty<bool> AreDetailsVisibleProperty =
            AvaloniaProperty.Register<DataGridRow, bool>(nameof(AreDetailsVisible));

        /// <summary>
        /// Gets or sets a value that indicates when the details section of the row is displayed.
        /// </summary>
        public bool AreDetailsVisible
        {
            get { return GetValue(AreDetailsVisibleProperty); }
            set { SetValue(AreDetailsVisibleProperty, value); }
        }

        public static readonly DirectProperty<DataGridRow, DataGrid> OwningGridProperty =
            AvaloniaProperty.RegisterDirect<DataGridRow, DataGrid>(
                nameof(OwningGrid),
                o => o.OwningGrid,
                (o, v) => o.OwningGrid = v);

        public static readonly DirectProperty<DataGridRow, bool> IsPlaceholderProperty =
            AvaloniaProperty.RegisterDirect<DataGridRow, bool>(
                nameof(IsPlaceholder),
                o => o.IsPlaceholder,
                (o, v) => o.IsPlaceholder = v);

        /// <summary>
        /// Gets a value indicating whether this row represents the new item placeholder.
        /// </summary>
        public bool IsPlaceholder
        {
            get => _isPlaceholder;
            internal set
            {
                if (SetAndRaise(IsPlaceholderProperty, ref _isPlaceholder, value))
                {
                    PseudoClassesHelper.Set(PseudoClasses, ":placeholder", value);
                }
            }
        }

        internal void ClearRecyclingState()
        {
            RecycledDataContext = null;
            RecycledIsPlaceholder = false;
        }

        static DataGridRow()
        {
            HeaderProperty.Changed.AddClassHandler<DataGridRow>((x, e) => x.OnHeaderChanged(e));
            IndexProperty.Changed.AddClassHandler<DataGridRow>((x, e) => x.OnIndexChanged(e));
            DetailsTemplateProperty.Changed.AddClassHandler<DataGridRow>((x, e) => x.OnDetailsTemplateChanged(e));
            AreDetailsVisibleProperty.Changed.AddClassHandler<DataGridRow>((x, e) => x.OnAreDetailsVisibleChanged(e));
            PointerPressedEvent.AddClassHandler<DataGridRow>((x, e) => x.DataGridRow_PointerPressed(e));
            IsTabStopProperty.OverrideDefaultValue<DataGridRow>(false);
            AutomationProperties.IsOffscreenBehaviorProperty.OverrideDefaultValue<DataGridRow>(IsOffscreenBehavior.FromClip);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridRow" /> class.
        /// </summary>
        public DataGridRow()
        {
            MinHeight = DATAGRIDROW_defaultMinHeight;

            Index = -1;
            IsValid = true;
            ValidationSeverity = DataGridValidationSeverity.None;
            Slot = -1;
            _mouseOverColumnIndex = null;
            _detailsDesiredHeight = double.NaN;
            _detailsLoaded = false;
            _appliedDetailsVisibility = false;
            Cells = new DataGridCellCollection(this);
            Cells.CellAdded += DataGridCellCollection_CellAdded;
            Cells.CellRemoved += DataGridCellCollection_CellRemoved;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DataGridRowAutomationPeer(this);
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

        private void OnHeaderChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_headerElement != null)
            {
                _headerElement.Content = e.NewValue;
            }
        }

        private void OnIndexChanged(AvaloniaPropertyChangedEventArgs e)
        {
            OwningGrid?.OnRowIndexChanged(this);
        }

        private void OnDetailsTemplateChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var oldValue = (IDataTemplate)e.OldValue;
            var newValue = (IDataTemplate)e.NewValue;

            if (!_areHandlersSuspended && OwningGrid != null)
            {
                IDataTemplate actualDetailsTemplate(IDataTemplate template) => (template ?? OwningGrid.RowDetailsTemplate);

                // We don't always want to apply the new Template because they might have set the same one
                // we inherited from the DataGrid
                if (actualDetailsTemplate(newValue) != actualDetailsTemplate(oldValue))
                {
                    ApplyDetailsTemplate(initializeDetailsPreferredHeight: false);
                }
            }
        }

        private void OnAreDetailsVisibleChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!_areHandlersSuspended)
            {
                if (OwningGrid == null)
                {
                    throw DataGridError.DataGrid.NoOwningGrid(this.GetType());
                }
                if (Index == -1)
                {
                    throw DataGridError.DataGridRow.InvalidRowIndexCannotCompleteOperation();
                }

                var newValue = (bool)e.NewValue;
                OwningGrid.OnRowDetailsVisibilityPropertyChanged(Index, newValue);
                SetDetailsVisibilityInternal(newValue, raiseNotification: true, animate: true);
            }
        }

        /// <summary>
        /// Gets the grid that owns this row.
        /// </summary>
        public DataGrid OwningGrid
        {
            get => _owningGrid;
            internal set => SetAndRaise(OwningGridProperty, ref _owningGrid, value);
        }

        private int _index;

        public static readonly DirectProperty<DataGridRow, int> IndexProperty = AvaloniaProperty.RegisterDirect<DataGridRow, int>(
            nameof(Index), o => o.Index, (o, v) => o.Index = v);

        /// <summary>
        /// Index of the row
        /// </summary>
        public int Index
        {
            get => _index;
            internal set => SetAndRaise(IndexProperty, ref _index, value);
        }

        internal double ActualBottomGridLineHeight
        {
            get
            {
                if (_bottomGridLine != null && OwningGrid != null && OwningGrid.AreRowBottomGridLinesRequired)
                {
                    // Unfortunately, _bottomGridLine has no size yet so we can't get its actualheight
                    return DataGrid.HorizontalGridLinesThickness;
                }
                return 0;
            }
        }

        internal DataGridCellCollection Cells
        {
            get;
            private set;
        }

        internal DataGridCell FillerCell
        {
            get
            {
                if (_fillerCell == null)
                {
                    _fillerCell = new DataGridCell
                    {
                        IsVisible = false,
                        OwningRow = this
                    };
                    if (OwningGrid.CellTheme is {} cellTheme)
                    {
                        _fillerCell.SetValue(ThemeProperty, cellTheme, BindingPriority.Template);
                    }
                    if (_cellsElement != null)
                    {
                        _cellsElement.Children.Add(_fillerCell);
                    }
                }
                return _fillerCell;
            }
        }

        internal bool HasBottomGridLine
        {
            get
            {
                return _bottomGridLine != null;
            }
        }

        internal bool HasHeaderCell
        {
            get
            {
                return _headerElement != null;
            }
        }

        internal DataGridRowHeader HeaderCell
        {
            get
            {
                return _headerElement;
            }
        }

        internal bool IsEditing => OwningGrid != null && OwningGrid.EditingRow == this;

        /// <summary>
        /// Layout when template is applied
        /// </summary>
        internal bool IsLayoutDelayed
        {
            get;
            private set;
        }

        internal bool IsMouseOver
        {
            get
            {
                return OwningGrid != null && OwningGrid.MouseOverRowIndex == Index;
            }
            set
            {
                if (OwningGrid != null && value != IsMouseOver)
                {
                    if (value)
                    {
                        OwningGrid.MouseOverRowIndex = Index;
                    }
                    else
                    {
                        OwningGrid.RequestPointerOverRefreshFromRow();
                    }
                }
            }
        }

        internal bool IsRecycled
        {
            get;
            private set;
        }

        internal bool IsRecyclable
        {
            get
            {
                if (OwningGrid != null)
                {
                    return OwningGrid.IsRowRecyclable(this);
                }
                return true;
            }
        }

        internal int? MouseOverColumnIndex
        {
            get
            {
                return _mouseOverColumnIndex;
            }
            set
            {
                if (_mouseOverColumnIndex != value)
                {
                    DataGridCell oldMouseOverCell = null;
                    if (_mouseOverColumnIndex != null && OwningGrid.IsSlotVisible(Slot))
                    {
                        if (_mouseOverColumnIndex > -1)
                        {
                            oldMouseOverCell = Cells[_mouseOverColumnIndex.Value];
                        }
                    }
                    _mouseOverColumnIndex = value;
                    if (oldMouseOverCell != null && IsVisible)
                    {
                        oldMouseOverCell.UpdatePseudoClasses();
                    }
                    if (_mouseOverColumnIndex != null && OwningGrid != null && OwningGrid.IsSlotVisible(Slot))
                    {
                        if (_mouseOverColumnIndex > -1)
                        {
                            Cells[_mouseOverColumnIndex.Value].UpdatePseudoClasses();
                        }
                    }
                }
            }
        }

        internal Panel RootElement
        {
            get;
            private set;
        }

        internal int Slot
        {
            get;
            set;
        }

        // Height that the row will eventually end up at after a possible details animation has completed
        internal double TargetHeight
        {
            get
            {
                if (!double.IsNaN(Height))
                {
                    return Height;
                }
                else if (_detailsElement != null && _appliedDetailsVisibility == true && _appliedDetailsTemplate != null)
                {
                    Debug.Assert(!double.IsNaN(_detailsElement.ContentHeight));
                    Debug.Assert(!double.IsNaN(_detailsDesiredHeight));
                    return DesiredSize.Height + _detailsDesiredHeight - _detailsElement.ContentHeight;
                }
                else
                {
                    return DesiredSize.Height;
                }
            }
        }

        /// <summary>
        /// Returns the index of the current row.
        /// </summary>
        /// <returns>
        /// The index of the current row.
        /// </returns>
        [Obsolete("This API is going to be removed in a future version. Use the Index property instead.")]
        public int GetIndex()
        {
            return Index;
        }

        /// <summary>
        /// Returns the row which contains the given element
        /// </summary>
        /// <param name="element">element contained in a row</param>
        /// <returns>Row that contains the element, or null if not found
        /// </returns>
        public static DataGridRow GetRowContainingElement(Control element)
        {
            // Walk up the tree to find the DataGridRow that contains the element
            Visual parent = element;
            DataGridRow row = parent as DataGridRow;
            while ((parent != null) && (row == null))
            {
                parent = parent.GetVisualParent();
                row = parent as DataGridRow;
            }
            return row;
        }



        /// <summary>
        /// Builds the visual tree for the column header when a new template is applied.
        /// </summary>
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            RootElement = e.NameScope.Find<Panel>(DATAGRIDROW_elementRoot);
            if (RootElement != null)
            {
                ApplyState();
            }

            bool updateVerticalScrollBar = false;
            if (_cellsElement != null)
            {
                // If we're applying a new template, we  want to remove the cells from the previous _cellsElement
                _cellsElement.Children.Clear();
                updateVerticalScrollBar = true;
            }

            _cellsElement = e.NameScope.Find<DataGridCellsPresenter>(DATAGRIDROW_elementCells);
            if (_cellsElement != null)
            {
                _cellsElement.OwningRow = this;
                // Cells that were already added before the Template was applied need to
                // be added to the Canvas
                if (Cells.Count > 0)
                {
                    foreach (DataGridCell cell in Cells)
                    {
                        _cellsElement.Children.Add(cell);
                    }
                }
            }

            _detailsElement = e.NameScope.Find<DataGridDetailsPresenter>(DATAGRIDROW_elementDetails);
            if (_detailsElement != null && OwningGrid != null)
            {
                _detailsElement.OwningRow = this;
                if (ActualDetailsVisibility && ActualDetailsTemplate != null && _appliedDetailsTemplate == null)
                {
                    // Apply the DetailsTemplate now that the row template is applied.
                    SetDetailsVisibilityInternal(ActualDetailsVisibility, raiseNotification: _detailsVisibilityNotificationPending, animate: false);
                    _detailsVisibilityNotificationPending = false;
                }
            }

            _bottomGridLine = e.NameScope.Find<Rectangle>(DATAGRIDROW_elementBottomGridLine);
            EnsureGridLines();

            _headerElement = e.NameScope.Find<DataGridRowHeader>(DATAGRIDROW_elementRowHeader);
            if (_headerElement != null)
            {
                _headerElement.Owner = this;
                if (Header != null)
                {
                    _headerElement.Content = Header;
                }
                EnsureHeaderStyleAndVisibility(null);
            }

            //The height of this row might have changed after applying a new style, so fix the vertical scroll bar
            if (OwningGrid != null && updateVerticalScrollBar)
            {
                OwningGrid.UpdateVerticalScrollBar();
            }
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            IsMouseOver = true;
        }
        protected override void OnPointerExited(PointerEventArgs e)
        {
            IsMouseOver = false;
            base.OnPointerExited(e);
        }













        private void DataGridCellCollection_CellAdded(object sender, DataGridCellEventArgs e)
        {
            _cellsElement?.Children.Add(e.Cell);
        }

        private void DataGridCellCollection_CellRemoved(object sender, DataGridCellEventArgs e)
        {
            _cellsElement?.Children.Remove(e.Cell);
        }

        private void DataGridRow_PointerPressed(PointerPressedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (OwningGrid != null)
            {
                OwningGrid.IsDoubleClickRecordsClickOnCall(this);
            }
        }


        // Returns the actual template that should be sued for Details: either explicity set on this row
        // or inherited from the DataGrid
        private IDataTemplate ActualDetailsTemplate
        {
            get
            {
                Debug.Assert(OwningGrid != null);
                return DetailsTemplate ?? OwningGrid.RowDetailsTemplate;
            }
        }

        private bool ActualDetailsVisibility
        {
            get
            {
                if (OwningGrid == null)
                {
                    throw DataGridError.DataGrid.NoOwningGrid(GetType());
                }
                if (Index == -1)
                {
                    throw DataGridError.DataGridRow.InvalidRowIndexCannotCompleteOperation();
                }
                return OwningGrid.GetRowDetailsVisibility(Index);
            }
        }




        //TODO Cleanup
        double? _previousDetailsHeight = null;





        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == DataContextProperty)
            {
                var owner = OwningGrid;
                if (owner != null && this.IsRecycled)
                {
                    var columns = owner.ColumnsItemsInternal;
                    var nc = columns.Count;
                    for (int ci = 0; ci < nc; ci++)
                    {
                        if (columns[ci] is DataGridTemplateColumn column)
                        {
                            if (column.Index >= 0 && column.Index < Cells.Count)
                            {
                                column.RefreshCellContent((Control)Cells[column.Index].Content, nameof(DataGridTemplateColumn.CellTemplate));
                            }
                        }
                    }
                }
            }
            else if (change.Property == IsSelectedProperty)
            {
                var value = change.GetNewValue<bool>();

                if (OwningGrid != null && Slot != -1)
                {
                    OwningGrid.SetRowSelection(Slot, value, false);
                }

                PseudoClassesHelper.Set(PseudoClasses, ":selected", value);
            }

            base.OnPropertyChanged(change);
        }

        internal void UpdateSearchPseudoClasses(bool isSearchMatch, bool isSearchCurrent)
        {
            PseudoClassesHelper.Set(PseudoClasses, ":searchmatch", isSearchMatch);
            PseudoClassesHelper.Set(PseudoClasses, ":searchcurrent", isSearchCurrent);
        }

    }
}
