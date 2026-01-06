// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Metadata;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System.Collections.Specialized;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    abstract partial class DataGridColumn : AvaloniaObject
    {
        internal const int DATAGRIDCOLUMN_maximumWidth = 65536;
        private const bool DATAGRIDCOLUMN_defaultIsReadOnly = false;
        private bool? _isReadOnly;
        private double? _maxWidth;
        private double? _minWidth;
        private bool _settingWidthInternally;
        private int _displayIndexWithFiller;
        private object _header;
        private IDataTemplate _headerTemplate;
        private DataGridColumnHeader _headerCell;
        private Control _editingElement;
        private ICellEditBinding _editBinding;
        private IBinding _clipboardContentBinding;
        private IBinding _cellBackgroundBinding;
        private IBinding _cellForegroundBinding;
        private ControlTheme _headerTheme;
        private ControlTheme _cellTheme;
        private ControlTheme _filterTheme;
        private Classes _cellStyleClasses;
        private Classes _headerStyleClasses;
        private bool _setWidthInternalNoCallback;
        private bool _showFilterButton;
        private FlyoutBase _filterFlyout;
        private System.Collections.IComparer _customSortComparer;


        /// <summary>
        /// Routed event raised when the column header receives a pointer press.
        /// </summary>
        public static readonly RoutedEvent<PointerPressedEventArgs> HeaderPointerPressedEvent =
            DataGridColumnHeader.HeaderPointerPressedEvent;

        /// <summary>
        /// Routed event raised when the column header receives a pointer release.
        /// </summary>
        public static readonly RoutedEvent<PointerReleasedEventArgs> HeaderPointerReleasedEvent =
            DataGridColumnHeader.HeaderPointerReleasedEvent;

        /// <summary>
        /// Occurs when the pointer is pressed over the column's header
        /// </summary>
        public event EventHandler<PointerPressedEventArgs> HeaderPointerPressed;
        /// <summary>
        /// Occurs when the pointer is released over the column's header
        /// </summary>
        public event EventHandler<PointerReleasedEventArgs> HeaderPointerReleased;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridColumn" /> class.
        /// </summary>
        protected internal DataGridColumn()
        {
            _displayIndexWithFiller = -1;
            IsInitialDesiredWidthDetermined = false;
            InheritsWidth = true;
        }

        /// <summary>
        /// Gets the <see cref="T:Avalonia.Controls.DataGrid"/> control that contains this column.
        /// </summary>
        protected internal DataGrid OwningGrid
        {
            get;
            internal set;
        }

        internal int Index
        {
            get;
            set;
        }

        internal bool? CanUserReorderInternal
        {
            get;
            set;
        }

        internal bool? CanUserResizeInternal
        {
            get;
            set;
        }

        internal bool? CanUserSortInternal
        {
            get;
            set;
        }

        internal bool ActualCanUserResize
        {
            get
            {
                if (OwningGrid == null || OwningGrid.CanUserResizeColumns == false || this is DataGridFillerColumn)
                {
                    return false;
                }
                return CanUserResizeInternal ?? true;
            }
        }

        // MaxWidth from local setting or DataGrid setting
        internal double ActualMaxWidth
        {
            get
            {
                return _maxWidth ?? OwningGrid?.MaxColumnWidth ?? double.PositiveInfinity;
            }
        }

        // MinWidth from local setting or DataGrid setting
        internal double ActualMinWidth
        {
            get
            {
                double minWidth = _minWidth ?? OwningGrid?.MinColumnWidth ?? 0;
                if (Width.IsStar)
                {
                    return Math.Max(DataGrid.DATAGRID_minimumStarColumnWidth, minWidth);
                }
                return minWidth;
            }
        }

        internal bool DisplayIndexHasChanged
        {
            get;
            set;
        }

        internal int DisplayIndexWithFiller
        {
            get { return _displayIndexWithFiller; }
            set { _displayIndexWithFiller = value; }
        }

        internal bool HasHeaderCell
        {
            get
            {
                return _headerCell != null;
            }
        }

        internal DataGridColumnHeader HeaderCell
        {
            get
            {
                if (_headerCell == null)
                {
                    _headerCell = CreateHeader();
                }
                return _headerCell;
            }
        }

        /// <summary>
        /// Tracks whether or not this column inherits its Width value from the DataGrid.
        /// </summary>
        internal bool InheritsWidth
        {
            get;
            private set;
        }

        /// <summary>
        /// When a column is initially added, we won't know its initial desired value
        /// until all rows have been measured.  We use this variable to track whether or
        /// not the column has been fully measured.
        /// </summary>
        internal bool IsInitialDesiredWidthDetermined
        {
            get;
            set;
        }

        internal double LayoutRoundedWidth
        {
            get;
            private set;
        }

        internal ICellEditBinding CellEditBinding
        {
            get => _editBinding;
        }


        /// <summary>
        /// Defines the <see cref="IsVisible"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsVisibleProperty =
             Control.IsVisibleProperty.AddOwner<DataGridColumn>();

        /// <summary>
        /// Determines whether or not this column is visible.
        /// </summary>
        public bool IsVisible
        {
            get => GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsVisibleProperty)
            {
                var wasVisible = change.GetOldValue<bool>();
                var isVisible = change.GetNewValue<bool>();
                OwningGrid?.OnColumnVisibleStateChanging(this, wasVisible, isVisible);

                if (_headerCell != null)
                {
                    _headerCell.IsVisible = isVisible;
                }

                OwningGrid?.OnColumnVisibleStateChanged(this);
                NotifyPropertyChanged(change.Property.Name);
            }
            else if (change.Property == WidthProperty)
            {
                if (!_settingWidthInternally)
                {
                    InheritsWidth = false;
                }
                if (_setWidthInternalNoCallback == false)
                {
                    var grid = OwningGrid;
                    var width = (change as AvaloniaPropertyChangedEventArgs<DataGridLength>).NewValue.Value;
                    if (grid != null)
                    {
                        var oldWidth = (change as AvaloniaPropertyChangedEventArgs<DataGridLength>).OldValue.Value;
                        if (width.IsStar != oldWidth.IsStar)
                        {
                            SetWidthInternalNoCallback(width);
                            IsInitialDesiredWidthDetermined = false;
                            grid.OnColumnWidthChanged(this);
                        }
                        else
                        {
                            Resize(oldWidth, width, false);
                        }
                    }
                    else
                    {
                        SetWidthInternalNoCallback(width);
                    }
                }
            }
            else if (change.Property == SortDirectionProperty)
            {
                OwningGrid?.OnColumnSortDirectionChanged(this, change.GetNewValue<ListSortDirection?>());
            }
        }


        /// <summary>
        /// Actual visible width after Width, MinWidth, and MaxWidth setting at the Column level and DataGrid level
        /// have been taken into account
        /// </summary>
        public double ActualWidth
        {
            get
            {
                if (OwningGrid == null || double.IsNaN(Width.DisplayValue))
                {
                    return ActualMinWidth;
                }
                return Width.DisplayValue;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the user can change the column display position by
        /// dragging the column header.
        /// </summary>
        /// <returns>
        /// true if the user can drag the column header to a new position; otherwise, false. The default is the current <see cref="P:Avalonia.Controls.DataGrid.CanUserReorderColumns" /> property value.
        /// </returns>
        public bool CanUserReorder
        {
            get
            {
                return
                    CanUserReorderInternal ??
                        OwningGrid?.CanUserReorderColumns ??
                        DataGrid.DATAGRID_defaultCanUserResizeColumns;
            }
            set
            {
                CanUserReorderInternal = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the user can adjust the column width using the mouse.
        /// </summary>
        /// <returns>
        /// true if the user can resize the column; false if the user cannot resize the column. The default is the current <see cref="P:Avalonia.Controls.DataGrid.CanUserResizeColumns" /> property value.
        /// </returns>
        public bool CanUserResize
        {
            get
            {
                return
                    CanUserResizeInternal ??
                    OwningGrid?.CanUserResizeColumns ??
                    DataGrid.DATAGRID_defaultCanUserResizeColumns;
            }
            set
            {
                CanUserResizeInternal = value;
                OwningGrid?.OnColumnCanUserResizeChanged(this);
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the user can sort the column by clicking the column header.
        /// </summary>
        /// <returns>
        /// true if the user can sort the column; false if the user cannot sort the column. The default is the current <see cref="P:Avalonia.Controls.DataGrid.CanUserSortColumns" /> property value.
        /// </returns>
        public bool CanUserSort
        {
            get
            {
                if (CanUserSortInternal.HasValue)
                {
                    return CanUserSortInternal.Value;
                }
                else if (OwningGrid != null)
                {
                    string propertyPath = GetSortPropertyName();
                    Type propertyType = OwningGrid.DataConnection.DataType.GetNestedPropertyType(propertyPath);

                    // If we can't resolve the property type (e.g. nested paths on object-typed nodes),
                    // fall back to the grid default instead of disabling sorting entirely.
                    if (propertyType == null)
                    {
                        return DataGrid.DATAGRID_defaultCanUserSortColumns;
                    }

                    // if the type is nullable, then we will compare the non-nullable type
                    if (TypeHelper.IsNullableType(propertyType))
                    {
                        propertyType = TypeHelper.GetNonNullableType(propertyType);
                    }

                    // return whether or not the property type can be compared
                    return typeof(IComparable).IsAssignableFrom(propertyType) ? true : false;
                }
                else
                {
                    return DataGrid.DATAGRID_defaultCanUserSortColumns;
                }
            }
            set
            {
                CanUserSortInternal = value;
            }
        }

        /// <summary>
        /// Gets or sets the display position of the column relative to the other columns in the <see cref="T:Avalonia.Controls.DataGrid" />.
        /// </summary>
        /// <returns>
        /// The zero-based position of the column as it is displayed in the associated <see cref="T:Avalonia.Controls.DataGrid" />. The default is the index of the corresponding <see cref="P:System.Collections.ObjectModel.Collection`1.Item(System.Int32)" /> in the <see cref="P:Avalonia.Controls.DataGrid.Columns" /> collection.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// When setting this property, the specified value is less than -1 or equal to <see cref="F:System.Int32.MaxValue" />.
        ///
        /// -or-
        ///
        /// When setting this property on a column in a <see cref="T:Avalonia.Controls.DataGrid" />, the specified value is less than zero or greater than or equal to the number of columns in the <see cref="T:Avalonia.Controls.DataGrid" />.
        /// </exception>
        /// <exception cref="T:System.InvalidOperationException">
        /// When setting this property, the <see cref="T:Avalonia.Controls.DataGrid" /> is already making <see cref="P:Avalonia.Controls.DataGridColumn.DisplayIndex" /> adjustments. For example, this exception is thrown when you attempt to set <see cref="P:Avalonia.Controls.DataGridColumn.DisplayIndex" /> in a <see cref="E:Avalonia.Controls.DataGrid.ColumnDisplayIndexChanged" /> event handler.
        ///
        /// -or-
        ///
        /// When setting this property, the specified value would result in a frozen column being displayed in the range of unfrozen columns, or an unfrozen column being displayed in the range of frozen columns.
        /// </exception>
        public int DisplayIndex
        {
            get
            {
                if (OwningGrid != null && OwningGrid.ColumnsInternal.RowGroupSpacerColumn.IsRepresented)
                {
                    return _displayIndexWithFiller - 1;
                }
                else
                {
                    return _displayIndexWithFiller;
                }
            }
            set
            {
                if (value == Int32.MaxValue)
                {
                    throw DataGridError.DataGrid.ValueMustBeLessThan(nameof(value), nameof(DisplayIndex), Int32.MaxValue);
                }
                if (OwningGrid != null)
                {
                    if (OwningGrid.ColumnsInternal.RowGroupSpacerColumn.IsRepresented)
                    {
                        value++;
                    }
                    if (_displayIndexWithFiller != value)
                    {
                        if (value < 0 || value >= OwningGrid.ColumnsItemsInternal.Count)
                        {
                            throw DataGridError.DataGrid.ValueMustBeBetween(nameof(value), nameof(DisplayIndex), 0, true, OwningGrid.ColumnDefinitions.Count, false);
                        }
                        // Will throw an error if a visible frozen column is placed inside a non-frozen area or vice-versa.
                        OwningGrid.OnColumnDisplayIndexChanging(this, value);
                        _displayIndexWithFiller = value;
                        try
                        {
                            OwningGrid.InDisplayIndexAdjustments = true;
                            OwningGrid.OnColumnDisplayIndexChanged(this);
                            OwningGrid.OnColumnDisplayIndexChanged_PostNotification();
                        }
                        finally
                        {
                            OwningGrid.InDisplayIndexAdjustments = false;
                        }
                    }
                }
                else
                {
                    if (value < -1)
                    {
                        throw DataGridError.DataGrid.ValueMustBeGreaterThanOrEqualTo(nameof(value), nameof(DisplayIndex), -1);
                    }
                    _displayIndexWithFiller = value;
                }
            }
        }

        public Classes CellStyleClasses => _cellStyleClasses ??= new();

        [AssignBinding]
        [InheritDataTypeFromItems(nameof(DataGrid.ItemsSource), AncestorType = typeof(DataGrid))]
        public IBinding CellBackgroundBinding
        {
            get => _cellBackgroundBinding;
            set
            {
                if (_cellBackgroundBinding != value)
                {
                    _cellBackgroundBinding = value;
                    NotifyPropertyChanged(nameof(CellBackgroundBinding));
                }
            }
        }

        [AssignBinding]
        [InheritDataTypeFromItems(nameof(DataGrid.ItemsSource), AncestorType = typeof(DataGrid))]
        public IBinding CellForegroundBinding
        {
            get => _cellForegroundBinding;
            set
            {
                if (_cellForegroundBinding != value)
                {
                    _cellForegroundBinding = value;
                    NotifyPropertyChanged(nameof(CellForegroundBinding));
                }
            }
        }

        public Classes HeaderStyleClasses => _headerStyleClasses ??= CreateHeaderStyleClasses();

        private Classes CreateHeaderStyleClasses()
        {
            var classes = new Classes();
            classes.CollectionChanged += HeaderStyleClassesChanged;
            return classes;
        }

        /// <summary>
        ///    Backing field for HeaderTheme property.
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, ControlTheme> HeaderThemeProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, ControlTheme>(
                nameof(HeaderTheme),
                o => o.HeaderTheme,
                (o, v) => o.HeaderTheme = v);

        /// <summary>
        ///    Gets or sets the <see cref="DataGridColumnHeader"/> theme for this column. Overrides <see cref="DataGrid.ColumnHeaderTheme"/>.
        /// </summary>
        public ControlTheme HeaderTheme
        {
            get { return _headerTheme; }
            set
            {
                if (SetAndRaise(HeaderThemeProperty, ref _headerTheme, value) && _headerCell != null)
                {
                    ApplyHeaderTheme(_headerCell);
                }
            }
        }

        private void HeaderStyleClassesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_headerCell != null)
            {
                _headerCell.Classes.Replace(HeaderStyleClasses);
            }
        }

        /// <summary>
        ///    Backing field for CellTheme property.
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, ControlTheme> CellThemeProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, ControlTheme>(
                nameof(CellTheme),
                o => o.CellTheme,
                (o, v) => o.CellTheme = v);

        /// <summary>
        ///    Gets or sets the <see cref="DataGridColumnHeader"/> cell theme.
        /// </summary>
        public ControlTheme CellTheme
        {
            get { return _cellTheme; }
            set { SetAndRaise(CellThemeProperty, ref _cellTheme, value); }
        }

        /// <summary>
        /// Backing field for FilterTheme property.
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, ControlTheme> FilterThemeProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, ControlTheme>(
                nameof(FilterTheme),
                o => o.FilterTheme,
                (o, v) => o.FilterTheme = v);

        /// <summary>
        /// Gets or sets the theme applied to the filter button inside the column header.
        /// </summary>
        public ControlTheme FilterTheme
        {
            get { return _filterTheme; }
            set
            {
                if (SetAndRaise(FilterThemeProperty, ref _filterTheme, value))
                {
                    if (_headerCell != null)
                    {
                        _headerCell.FilterTheme = value ?? OwningGrid?.ColumnHeaderFilterTheme;
                    }
                }
            }
        }

        /// <summary>
        /// Backing field for ShowFilterButton property.
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, bool> ShowFilterButtonProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, bool>(
                nameof(ShowFilterButton),
                o => o.ShowFilterButton,
                (o, v) => o.ShowFilterButton = v);

        /// <summary>
        /// Gets or sets a value indicating whether the column header should display the filter button.
        /// </summary>
        public bool ShowFilterButton
        {
            get { return _showFilterButton; }
            set
            {
                if (SetAndRaise(ShowFilterButtonProperty, ref _showFilterButton, value))
                {
                    if (_headerCell != null)
                    {
                        _headerCell.ShowFilterButton = value || _filterFlyout != null;
                    }
                }
            }
        }

        /// <summary>
        /// Backing field for FilterFlyout property.
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, FlyoutBase> FilterFlyoutProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, FlyoutBase>(
                nameof(FilterFlyout),
                o => o.FilterFlyout,
                (o, v) => o.FilterFlyout = v);

        /// <summary>
        /// Gets or sets the flyout that will be attached to the column header filter button.
        /// </summary>
        public FlyoutBase FilterFlyout
        {
            get { return _filterFlyout; }
            set
            {
                if (SetAndRaise(FilterFlyoutProperty, ref _filterFlyout, value))
                {
                    if (value != null && !_showFilterButton)
                    {
                        ShowFilterButton = true;
                    }

                    if (_headerCell != null)
                    {
                        _headerCell.FilterFlyout = value;
                        if (value != null)
                        {
                            _headerCell.ShowFilterButton = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///    Backing field for Header property
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, object> HeaderProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, object>(
                nameof(Header),
                o => o.Header,
                (o, v) => o.Header = v);

        /// <summary>
        ///    Gets or sets the <see cref="DataGridColumnHeader"/> content
        /// </summary>
        public object Header
        {
            get { return _header; }
            set { SetAndRaise(HeaderProperty, ref _header, value); }
        }

        /// <summary>
        ///    Backing field for Header property
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, IDataTemplate> HeaderTemplateProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, IDataTemplate>(
                nameof(HeaderTemplate),
                o => o.HeaderTemplate,
                (o, v) => o.HeaderTemplate = v);

        /// <summary>
        ///  Gets or sets an <see cref="IDataTemplate"/> for the <see cref="Header"/>
        /// </summary>
        public IDataTemplate HeaderTemplate
        {
            get { return _headerTemplate; }
            set { SetAndRaise(HeaderTemplateProperty, ref _headerTemplate, value); }
        }

        public bool IsAutoGenerated
        {
            get;
            internal set;
        }

        internal DataGridFrozenColumnPosition FrozenPosition
        {
            get;
            set;
        }

        public bool IsFrozen
        {
            get => FrozenPosition != DataGridFrozenColumnPosition.None;
            internal set => FrozenPosition = value ? DataGridFrozenColumnPosition.Left : DataGridFrozenColumnPosition.None;
        }

        internal bool IsFrozenLeft => FrozenPosition == DataGridFrozenColumnPosition.Left;
        internal bool IsFrozenRight => FrozenPosition == DataGridFrozenColumnPosition.Right;

        public virtual bool IsReadOnly
        {
            get
            {
                if (OwningGrid == null)
                {
                    return _isReadOnly ?? DATAGRIDCOLUMN_defaultIsReadOnly;
                }
                if (_isReadOnly != null)
                {
                    return _isReadOnly.Value || OwningGrid.IsReadOnly;
                }
                return OwningGrid.GetColumnReadOnlyState(this, DATAGRIDCOLUMN_defaultIsReadOnly);
            }
            set
            {
                if (value != _isReadOnly)
                {
                    OwningGrid?.OnColumnReadOnlyStateChanging(this, value);
                    _isReadOnly = value;
                }
            }
        }

        public double MaxWidth
        {
            get
            {
                return _maxWidth ?? double.PositiveInfinity;
            }
            set
            {
                if (value < 0)
                {
                    throw DataGridError.DataGrid.ValueMustBeGreaterThanOrEqualTo("value", "MaxWidth", 0);
                }
                if (value < ActualMinWidth)
                {
                    throw DataGridError.DataGrid.ValueMustBeGreaterThanOrEqualTo("value", "MaxWidth", "MinWidth");
                }
                if (!_maxWidth.HasValue || _maxWidth.Value != value)
                {
                    double oldValue = ActualMaxWidth;
                    _maxWidth = value;
                    if (OwningGrid != null && OwningGrid.ColumnsInternal != null)
                    {
                        OwningGrid.OnColumnMaxWidthChanged(this, oldValue);
                    }
                }
            }
        }

        public double MinWidth
        {
            get
            {
                return _minWidth ?? 0;
            }
            set
            {
                if (double.IsNaN(value))
                {
                    throw DataGridError.DataGrid.ValueCannotBeSetToNAN("MinWidth");
                }
                if (value < 0)
                {
                    throw DataGridError.DataGrid.ValueMustBeGreaterThanOrEqualTo("value", "MinWidth", 0);
                }
                if (double.IsPositiveInfinity(value))
                {
                    throw DataGridError.DataGrid.ValueCannotBeSetToInfinity("MinWidth");
                }
                if (value > ActualMaxWidth)
                {
                    throw DataGridError.DataGrid.ValueMustBeLessThanOrEqualTo("value", "MinWidth", "MaxWidth");
                }
                if (!_minWidth.HasValue || _minWidth.Value != value)
                {
                    double oldValue = ActualMinWidth;
                    _minWidth = value;
                    if (OwningGrid != null && OwningGrid.ColumnsInternal != null)
                    {
                        OwningGrid.OnColumnMinWidthChanged(this, oldValue);
                    }
                }
            }
        }

        public static readonly StyledProperty<DataGridLength> WidthProperty = AvaloniaProperty
            .Register<DataGridColumn, DataGridLength>(nameof(Width)
            , coerce: CoerceWidth
            );

        public DataGridLength Width
        {
            get => this.GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        /// <summary>
        /// The binding that will be used to get or set cell content for the clipboard.
        /// </summary>
        public virtual IBinding ClipboardContentBinding
        {
            get
            {
                return _clipboardContentBinding;
            }
            set
            {
                _clipboardContentBinding = value;
            }
        }

        /// <summary>
        /// Resolves a <see cref="ControlTheme" /> from the owning grid's resources for the given key.
        /// Returns <c>null</c> when the grid is unavailable, the resource is missing, or the value is not a theme.
        /// </summary>
        /// <param name="resourceKey">Resource key to search for.</param>
        /// <returns>The located <see cref="ControlTheme" /> or <c>null</c>.</returns>
        protected ControlTheme GetColumnControlTheme(string resourceKey)
        {
            if (OwningGrid == null || string.IsNullOrEmpty(resourceKey))
            {
                return null;
            }

            try
            {
                if (OwningGrid.TryFindResource(resourceKey, out var resource) && resource is ControlTheme theme)
                {
                    return theme;
                }
            }
            catch (KeyNotFoundException)
            {
                return null;
            }

            return null;
        }



        public Control GetCellContent(object dataItem)
        {
            dataItem = dataItem ?? throw new ArgumentNullException(nameof(dataItem));
            if (OwningGrid == null)
            {
                throw DataGridError.DataGrid.NoOwningGrid(GetType());
            }
            DataGridRow dataGridRow = OwningGrid.GetRowFromItem(dataItem);
            if (dataGridRow == null)
            {
                return null;
            }
            return GetCellContent(dataGridRow);
        }



        /// <summary>
        /// Switches the current state of sort direction
        /// </summary>
        public void Sort()
        {
            //InvokeProcessSort is already validating if sorting is possible
            _headerCell?.InvokeProcessSort(Input.KeyModifiers.None);
        }

        /// <summary>
        /// Changes the sort direction of this column
        /// </summary>
        /// <param name="direction">New sort direction</param>
        public void Sort(ListSortDirection direction)
        {
            //InvokeProcessSort is already validating if sorting is possible
            _headerCell?.InvokeProcessSort(Input.KeyModifiers.None, direction);
        }

        /// <summary>
        /// Gets or sets the current sort direction for this column.
        /// </summary>
        public static readonly StyledProperty<ListSortDirection?> SortDirectionProperty =
            AvaloniaProperty.Register<DataGridColumn, ListSortDirection?>(nameof(SortDirection));

        /// <summary>
        /// Gets or sets the current sort direction for this column. Setting this property updates
        /// the owning grid's sorting model; clearing it removes the column from sorting.
        /// </summary>
        public ListSortDirection? SortDirection
        {
            get => GetValue(SortDirectionProperty);
            set => SetValue(SortDirectionProperty, value);
        }


        /// <summary>
        /// When overridden in a derived class, gets an editing element that is bound to the column's <see cref="P:Avalonia.Controls.DataGridBoundColumn.Binding" /> property value.
        /// </summary>
        /// <param name="cell">
        /// The cell that will contain the generated element.
        /// </param>
        /// <param name="dataItem">
        /// The data item represented by the row that contains the intended cell.
        /// </param>
        /// <param name="binding">When the method returns, contains the applied binding.</param>
        /// <returns>
        /// A new editing element that is bound to the column's <see cref="P:Avalonia.Controls.DataGridBoundColumn.Binding" /> property value.
        /// </returns>
        protected abstract Control GenerateEditingElement(DataGridCell cell, object dataItem, out ICellEditBinding binding);

        /// <summary>
        /// When overridden in a derived class, gets a read-only element that is bound to the column's
        /// <see cref="P:Avalonia.Controls.DataGridBoundColumn.Binding" /> property value.
        /// </summary>
        /// <param name="cell">
        /// The cell that will contain the generated element.
        /// </param>
        /// <param name="dataItem">
        /// The data item represented by the row that contains the intended cell.
        /// </param>
        /// <returns>
        /// A new, read-only element that is bound to the column's <see cref="P:Avalonia.Controls.DataGridBoundColumn.Binding" /> property value.
        /// </returns>
        protected abstract Control GenerateElement(DataGridCell cell, object dataItem);

        /// <summary>
        /// Called by a specific column type when one of its properties changed,
        /// and its current cells need to be updated.
        /// </summary>
        /// <param name="propertyName">Indicates which property changed and caused this call</param>
        protected void NotifyPropertyChanged(string propertyName)
        {
            OwningGrid?.RefreshColumnElements(this, propertyName);
        }

        /// <summary>
        /// When overridden in a derived class, called when a cell in the column enters editing mode.
        /// </summary>
        /// <param name="editingElement">
        /// The element that the column displays for a cell in editing mode.
        /// </param>
        /// <param name="editingEventArgs">
        /// Information about the user gesture that is causing a cell to enter editing mode.
        /// </param>
        /// <returns>
        /// The unedited value.
        /// </returns>
        protected abstract object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs);

        /// <summary>
        /// Called by the DataGrid control when a column asked for its
        /// elements to be refreshed, typically because one of its properties changed.
        /// </summary>
        /// <param name="element">Indicates the element that needs to be refreshed</param>
        /// <param name="propertyName">Indicates which property changed and caused this call</param>
        protected internal virtual void RefreshCellContent(Control element, string propertyName)
        { }


















        /// <summary>
        /// Holds the name of the member to use for sorting, if not using the default.
        /// </summary>
        public string SortMemberPath
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets an object associated with this column.
        /// </summary>
        public object Tag
        {
            get;
            set;
        }

        /// <summary>
        /// Holds a Comparer to use for sorting, if not using the default.
        /// </summary>
        public System.Collections.IComparer CustomSortComparer
        {
            get => _customSortComparer;
            set
            {
                if (!Equals(_customSortComparer, value))
                {
                    _customSortComparer = value;
                    OwningGrid?.OnColumnCustomSortComparerChanged(this);
                }
            }
        }



    }

}
