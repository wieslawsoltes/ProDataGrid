// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.Primitives;
using Avalonia.Utilities;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class DataGridColumnDefinitionContext
    {
        public DataGridColumnDefinitionContext(DataGrid grid)
        {
            Grid = grid;
        }

        public DataGrid Grid { get; }

        public T ResolveResource<T>(string key) where T : class
        {
            if (Grid == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (Grid.TryFindResource(key, out var resource) && resource is T typed)
            {
                return typed;
            }

            if (Application.Current != null && Application.Current.TryFindResource(key, out resource) && resource is T appTyped)
            {
                return appTyped;
            }

            return null;
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    abstract class DataGridColumnDefinition : INotifyPropertyChanged
    {
        private object _header;
        private string _headerTemplateKey;
        private string _headerThemeKey;
        private string _cellThemeKey;
        private string _summaryCellThemeKey;
        private string _filterThemeKey;
        private IList<string> _cellStyleClasses;
        private IList<string> _headerStyleClasses;
        private DataGridBindingDefinition _cellBackgroundBinding;
        private DataGridBindingDefinition _cellForegroundBinding;
        private HorizontalAlignment? _summaryCellHorizontalContentAlignment;
        private VerticalAlignment? _summaryCellVerticalContentAlignment;
        private bool? _canUserSort;
        private bool? _canUserHide;
        private bool? _canUserResize;
        private bool? _canUserReorder;
        private bool? _isReadOnly;
        private bool? _isVisible;
        private bool? _showFilterButton;
        private FlyoutBase _filterFlyout;
        private string _filterFlyoutKey;
        private int? _displayIndex;
        private DataGridLength? _width;
        private double? _minWidth;
        private double? _maxWidth;
        private ListSortDirection? _sortDirection;
        private string _sortMemberPath;
        private object _tag;
        private object _columnKey;
        private System.Collections.IComparer _customSortComparer;
        private IDataGridColumnValueAccessor _valueAccessor;
        private Type _valueType;
        private DataGridColumnDefinitionOptions _options;
        private int _updateNesting;
        private bool _hasPendingChange;

        public event PropertyChangedEventHandler PropertyChanged;

        public object Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        public string HeaderTemplateKey
        {
            get => _headerTemplateKey;
            set => SetProperty(ref _headerTemplateKey, value);
        }

        public string HeaderThemeKey
        {
            get => _headerThemeKey;
            set => SetProperty(ref _headerThemeKey, value);
        }

        public string CellThemeKey
        {
            get => _cellThemeKey;
            set => SetProperty(ref _cellThemeKey, value);
        }

        public string SummaryCellThemeKey
        {
            get => _summaryCellThemeKey;
            set => SetProperty(ref _summaryCellThemeKey, value);
        }

        public string FilterThemeKey
        {
            get => _filterThemeKey;
            set => SetProperty(ref _filterThemeKey, value);
        }

        public IList<string> CellStyleClasses
        {
            get => _cellStyleClasses;
            set => SetProperty(ref _cellStyleClasses, value);
        }

        public IList<string> HeaderStyleClasses
        {
            get => _headerStyleClasses;
            set => SetProperty(ref _headerStyleClasses, value);
        }

        public DataGridBindingDefinition CellBackgroundBinding
        {
            get => _cellBackgroundBinding;
            set => SetProperty(ref _cellBackgroundBinding, value);
        }

        public DataGridBindingDefinition CellForegroundBinding
        {
            get => _cellForegroundBinding;
            set => SetProperty(ref _cellForegroundBinding, value);
        }

        public HorizontalAlignment? SummaryCellHorizontalContentAlignment
        {
            get => _summaryCellHorizontalContentAlignment;
            set => SetProperty(ref _summaryCellHorizontalContentAlignment, value);
        }

        public VerticalAlignment? SummaryCellVerticalContentAlignment
        {
            get => _summaryCellVerticalContentAlignment;
            set => SetProperty(ref _summaryCellVerticalContentAlignment, value);
        }

        public bool? CanUserSort
        {
            get => _canUserSort;
            set => SetProperty(ref _canUserSort, value);
        }

        public bool? CanUserHide
        {
            get => _canUserHide;
            set => SetProperty(ref _canUserHide, value);
        }

        public bool? CanUserResize
        {
            get => _canUserResize;
            set => SetProperty(ref _canUserResize, value);
        }

        public bool? CanUserReorder
        {
            get => _canUserReorder;
            set => SetProperty(ref _canUserReorder, value);
        }

        public bool? IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        public bool? IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public bool? ShowFilterButton
        {
            get => _showFilterButton;
            set => SetProperty(ref _showFilterButton, value);
        }

        public FlyoutBase FilterFlyout
        {
            get => _filterFlyout;
            set => SetProperty(ref _filterFlyout, value);
        }

        public string FilterFlyoutKey
        {
            get => _filterFlyoutKey;
            set => SetProperty(ref _filterFlyoutKey, value);
        }

        public int? DisplayIndex
        {
            get => _displayIndex;
            set => SetProperty(ref _displayIndex, value);
        }

        public DataGridLength? Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double? MinWidth
        {
            get => _minWidth;
            set => SetProperty(ref _minWidth, value);
        }

        public double? MaxWidth
        {
            get => _maxWidth;
            set => SetProperty(ref _maxWidth, value);
        }

        public ListSortDirection? SortDirection
        {
            get => _sortDirection;
            set => SetProperty(ref _sortDirection, value);
        }

        public string SortMemberPath
        {
            get => _sortMemberPath;
            set => SetProperty(ref _sortMemberPath, value);
        }

        public object Tag
        {
            get => _tag;
            set => SetProperty(ref _tag, value);
        }

        public object ColumnKey
        {
            get => _columnKey;
            set => SetProperty(ref _columnKey, value);
        }

        public System.Collections.IComparer CustomSortComparer
        {
            get => _customSortComparer;
            set => SetProperty(ref _customSortComparer, value);
        }

        public IDataGridColumnValueAccessor ValueAccessor
        {
            get => _valueAccessor;
            set => SetProperty(ref _valueAccessor, value);
        }

        public Type ValueType
        {
            get => _valueType;
            set => SetProperty(ref _valueType, value);
        }

        public DataGridColumnDefinitionOptions Options
        {
            get => _options;
            set
            {
                if (ReferenceEquals(_options, value))
                {
                    return;
                }

                if (_options != null)
                {
                    WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridColumnDefinition>(
                        _options,
                        nameof(INotifyPropertyChanged.PropertyChanged),
                        Options_PropertyChanged);
                }

                _options = value;

                if (_options != null)
                {
                    WeakEventHandlerManager.Subscribe<DataGridColumnDefinitionOptions, PropertyChangedEventArgs, DataGridColumnDefinition>(
                        _options,
                        nameof(INotifyPropertyChanged.PropertyChanged),
                        Options_PropertyChanged);
                }

                NotifyPropertyChanged(nameof(Options));
            }
        }

        public void BeginUpdate()
        {
            _updateNesting++;
        }

        public void EndUpdate()
        {
            if (_updateNesting == 0)
            {
                throw new InvalidOperationException("EndUpdate called without a matching BeginUpdate.");
            }

            _updateNesting--;

            if (_updateNesting == 0 && _hasPendingChange)
            {
                _hasPendingChange = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        }

        internal DataGridColumn CreateColumn(DataGridColumnDefinitionContext context)
        {
            var column = CreateColumnCore();
            ApplyToColumn(column, context);
            return column;
        }

        internal void ApplyToColumn(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            ApplyBaseProperties(column, context);
            ApplyColumnProperties(column, context);
        }

        internal void ApplyPropertyChange(DataGridColumn column, DataGridColumnDefinitionContext context, string propertyName)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                ApplyToColumn(column, context);
                return;
            }

            if (!ApplyPropertyChangeCore(column, context, propertyName))
            {
                ApplyToColumn(column, context);
            }
        }

        protected abstract DataGridColumn CreateColumnCore();

        protected abstract void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context);

        protected virtual bool ApplyColumnPropertyChange(
            DataGridColumn column,
            DataGridColumnDefinitionContext context,
            string propertyName)
        {
            return false;
        }

        protected virtual void ApplyBaseProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            column.Header = Header;
            column.SortMemberPath = SortMemberPath;
            column.Tag = Tag;
            column.CustomSortComparer = CustomSortComparer;

            if (HeaderTemplateKey != null)
            {
                column.HeaderTemplate = context?.ResolveResource<IDataTemplate>(HeaderTemplateKey);
            }
            else
            {
                column.HeaderTemplate = null;
            }

            if (HeaderThemeKey != null)
            {
                column.HeaderTheme = context?.ResolveResource<ControlTheme>(HeaderThemeKey);
            }
            else
            {
                column.HeaderTheme = null;
            }

            if (CellThemeKey != null)
            {
                column.CellTheme = context?.ResolveResource<ControlTheme>(CellThemeKey);
            }
            else
            {
                column.CellTheme = null;
            }

            if (SummaryCellThemeKey != null)
            {
                column.SummaryCellTheme = context?.ResolveResource<ControlTheme>(SummaryCellThemeKey);
            }
            else
            {
                column.SummaryCellTheme = null;
            }

            if (FilterThemeKey != null)
            {
                column.FilterTheme = context?.ResolveResource<ControlTheme>(FilterThemeKey);
            }
            else
            {
                column.FilterTheme = null;
            }

            if (CellStyleClasses != null)
            {
                column.CellStyleClasses.Replace(CellStyleClasses);
            }
            else
            {
                column.CellStyleClasses.Clear();
            }

            if (HeaderStyleClasses != null)
            {
                column.HeaderStyleClasses.Replace(HeaderStyleClasses);
            }
            else
            {
                column.HeaderStyleClasses.Clear();
            }

            column.CellBackgroundBinding = CellBackgroundBinding?.CreateBinding();
            column.CellForegroundBinding = CellForegroundBinding?.CreateBinding();

            if (SummaryCellHorizontalContentAlignment.HasValue)
            {
                column.SummaryCellHorizontalContentAlignment = SummaryCellHorizontalContentAlignment.Value;
            }
            else
            {
                column.ClearValue(DataGridColumn.SummaryCellHorizontalContentAlignmentProperty);
            }

            if (SummaryCellVerticalContentAlignment.HasValue)
            {
                column.SummaryCellVerticalContentAlignment = SummaryCellVerticalContentAlignment.Value;
            }
            else
            {
                column.ClearValue(DataGridColumn.SummaryCellVerticalContentAlignmentProperty);
            }

            if (CanUserSort.HasValue)
            {
                column.CanUserSort = CanUserSort.Value;
            }

            if (CanUserHide.HasValue)
            {
                column.CanUserHide = CanUserHide.Value;
            }

            if (CanUserResize.HasValue)
            {
                column.CanUserResize = CanUserResize.Value;
            }

            if (CanUserReorder.HasValue)
            {
                column.CanUserReorder = CanUserReorder.Value;
            }

            if (IsReadOnly.HasValue)
            {
                column.IsReadOnly = IsReadOnly.Value;
            }

            if (IsVisible.HasValue)
            {
                column.IsVisible = IsVisible.Value;
            }

            if (ShowFilterButton.HasValue)
            {
                column.ShowFilterButton = ShowFilterButton.Value;
            }

            column.FilterFlyout = ResolveFilterFlyout(context);

            if (DisplayIndex.HasValue)
            {
                column.DisplayIndex = DisplayIndex.Value;
            }

            if (Width.HasValue)
            {
                column.Width = Width.Value;
            }

            if (MinWidth.HasValue)
            {
                column.MinWidth = MinWidth.Value;
            }

            if (MaxWidth.HasValue)
            {
                column.MaxWidth = MaxWidth.Value;
            }

            if (SortDirection.HasValue)
            {
                column.SortDirection = SortDirection.Value;
            }

            if (ValueAccessor != null)
            {
                DataGridColumnMetadata.SetValueAccessor(column, ValueAccessor);
            }
            else
            {
                DataGridColumnMetadata.ClearValueAccessor(column);
            }

            if (ValueType != null)
            {
                DataGridColumnMetadata.SetValueType(column, ValueType);
            }
            else
            {
                DataGridColumnMetadata.ClearValueType(column);
            }

            ApplyOptions(column);
        }

        protected virtual bool ApplyPropertyChangeCore(
            DataGridColumn column,
            DataGridColumnDefinitionContext context,
            string propertyName)
        {
            var baseHandled = ApplyBasePropertyChange(column, context, propertyName);
            var handled = baseHandled;

            if (!handled && string.Equals(propertyName, nameof(Options), StringComparison.Ordinal))
            {
                ApplyOptions(column);
                handled = true;
            }

            if (ApplyColumnPropertyChange(column, context, propertyName))
            {
                return true;
            }

            if (baseHandled)
            {
                ApplyColumnProperties(column, context);
                return true;
            }

            return handled;
        }

        private bool ApplyBasePropertyChange(DataGridColumn column, DataGridColumnDefinitionContext context, string propertyName)
        {
            switch (propertyName)
            {
                case nameof(Header):
                    column.Header = Header;
                    return true;
                case nameof(SortMemberPath):
                    column.SortMemberPath = SortMemberPath;
                    return true;
                case nameof(Tag):
                    column.Tag = Tag;
                    return true;
                case nameof(CustomSortComparer):
                    column.CustomSortComparer = CustomSortComparer;
                    return true;
                case nameof(HeaderTemplateKey):
                    column.HeaderTemplate = HeaderTemplateKey != null
                        ? context?.ResolveResource<IDataTemplate>(HeaderTemplateKey)
                        : null;
                    return true;
                case nameof(HeaderThemeKey):
                    column.HeaderTheme = HeaderThemeKey != null
                        ? context?.ResolveResource<ControlTheme>(HeaderThemeKey)
                        : null;
                    return true;
                case nameof(CellThemeKey):
                    column.CellTheme = CellThemeKey != null
                        ? context?.ResolveResource<ControlTheme>(CellThemeKey)
                        : null;
                    return true;
                case nameof(SummaryCellThemeKey):
                    column.SummaryCellTheme = SummaryCellThemeKey != null
                        ? context?.ResolveResource<ControlTheme>(SummaryCellThemeKey)
                        : null;
                    return true;
                case nameof(FilterThemeKey):
                    column.FilterTheme = FilterThemeKey != null
                        ? context?.ResolveResource<ControlTheme>(FilterThemeKey)
                        : null;
                    return true;
                case nameof(CellStyleClasses):
                    if (CellStyleClasses != null)
                    {
                        column.CellStyleClasses.Replace(CellStyleClasses);
                    }
                    else
                    {
                        column.CellStyleClasses.Clear();
                    }
                    return true;
                case nameof(HeaderStyleClasses):
                    if (HeaderStyleClasses != null)
                    {
                        column.HeaderStyleClasses.Replace(HeaderStyleClasses);
                    }
                    else
                    {
                        column.HeaderStyleClasses.Clear();
                    }
                    return true;
                case nameof(CellBackgroundBinding):
                    column.CellBackgroundBinding = CellBackgroundBinding?.CreateBinding();
                    return true;
                case nameof(CellForegroundBinding):
                    column.CellForegroundBinding = CellForegroundBinding?.CreateBinding();
                    return true;
                case nameof(SummaryCellHorizontalContentAlignment):
                    if (SummaryCellHorizontalContentAlignment.HasValue)
                    {
                        column.SummaryCellHorizontalContentAlignment = SummaryCellHorizontalContentAlignment.Value;
                    }
                    else
                    {
                        column.ClearValue(DataGridColumn.SummaryCellHorizontalContentAlignmentProperty);
                    }
                    return true;
                case nameof(SummaryCellVerticalContentAlignment):
                    if (SummaryCellVerticalContentAlignment.HasValue)
                    {
                        column.SummaryCellVerticalContentAlignment = SummaryCellVerticalContentAlignment.Value;
                    }
                    else
                    {
                        column.ClearValue(DataGridColumn.SummaryCellVerticalContentAlignmentProperty);
                    }
                    return true;
                case nameof(CanUserSort):
                    if (CanUserSort.HasValue)
                    {
                        column.CanUserSort = CanUserSort.Value;
                    }
                    return true;
                case nameof(CanUserHide):
                    if (CanUserHide.HasValue)
                    {
                        column.CanUserHide = CanUserHide.Value;
                    }
                    return true;
                case nameof(CanUserResize):
                    if (CanUserResize.HasValue)
                    {
                        column.CanUserResize = CanUserResize.Value;
                    }
                    return true;
                case nameof(CanUserReorder):
                    if (CanUserReorder.HasValue)
                    {
                        column.CanUserReorder = CanUserReorder.Value;
                    }
                    return true;
                case nameof(IsReadOnly):
                    if (IsReadOnly.HasValue)
                    {
                        column.IsReadOnly = IsReadOnly.Value;
                    }
                    return true;
                case nameof(IsVisible):
                    if (IsVisible.HasValue)
                    {
                        column.IsVisible = IsVisible.Value;
                    }
                    return true;
                case nameof(ShowFilterButton):
                    if (ShowFilterButton.HasValue)
                    {
                        column.ShowFilterButton = ShowFilterButton.Value;
                    }
                    return true;
                case nameof(FilterFlyout):
                case nameof(FilterFlyoutKey):
                    column.FilterFlyout = ResolveFilterFlyout(context);
                    return true;
                case nameof(DisplayIndex):
                    if (DisplayIndex.HasValue)
                    {
                        column.DisplayIndex = DisplayIndex.Value;
                    }
                    return true;
                case nameof(Width):
                    if (Width.HasValue)
                    {
                        column.Width = Width.Value;
                    }
                    return true;
                case nameof(MinWidth):
                    if (MinWidth.HasValue)
                    {
                        column.MinWidth = MinWidth.Value;
                    }
                    return true;
                case nameof(MaxWidth):
                    if (MaxWidth.HasValue)
                    {
                        column.MaxWidth = MaxWidth.Value;
                    }
                    return true;
                case nameof(SortDirection):
                    if (SortDirection.HasValue)
                    {
                        column.SortDirection = SortDirection.Value;
                    }
                    return true;
                case nameof(ValueAccessor):
                    if (ValueAccessor != null)
                    {
                        DataGridColumnMetadata.SetValueAccessor(column, ValueAccessor);
                    }
                    else
                    {
                        DataGridColumnMetadata.ClearValueAccessor(column);
                    }
                    return true;
                case nameof(ValueType):
                    if (ValueType != null)
                    {
                        DataGridColumnMetadata.SetValueType(column, ValueType);
                    }
                    else
                    {
                        DataGridColumnMetadata.ClearValueType(column);
                    }
                    return true;
                case nameof(ColumnKey):
                    return true;
            }

            return false;
        }

        private FlyoutBase ResolveFilterFlyout(DataGridColumnDefinitionContext context)
        {
            if (FilterFlyout != null)
            {
                return FilterFlyout;
            }

            if (FilterFlyoutKey != null)
            {
                return context?.ResolveResource<FlyoutBase>(FilterFlyoutKey);
            }

            return null;
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        private void Options_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyPropertyChanged(nameof(Options));
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            if (_updateNesting > 0)
            {
                _hasPendingChange = true;
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ApplyOptions(DataGridColumn column)
        {
            if (column == null)
            {
                return;
            }

            var options = Options;

            if (options?.IsSearchable.HasValue == true)
            {
                DataGridColumnSearch.SetIsSearchable(column, options.IsSearchable.Value);
            }
            else
            {
                column.ClearValue(DataGridColumnSearch.IsSearchableProperty);
            }

            if (options?.SearchMemberPath != null)
            {
                DataGridColumnSearch.SetSearchMemberPath(column, options.SearchMemberPath);
            }
            else
            {
                column.ClearValue(DataGridColumnSearch.SearchMemberPathProperty);
            }

            if (options?.SearchTextProvider != null)
            {
                DataGridColumnSearch.SetTextProvider(column, options.SearchTextProvider);
            }
            else
            {
                column.ClearValue(DataGridColumnSearch.TextProviderProperty);
            }

            if (options?.SearchFormatProvider != null)
            {
                DataGridColumnSearch.SetFormatProvider(column, options.SearchFormatProvider);
            }
            else
            {
                column.ClearValue(DataGridColumnSearch.FormatProviderProperty);
            }

            if (options?.FilterPredicateFactory != null)
            {
                DataGridColumnFilter.SetPredicateFactory(column, options.FilterPredicateFactory);
            }
            else
            {
                column.ClearValue(DataGridColumnFilter.PredicateFactoryProperty);
            }

            if (options?.FilterValueAccessor != null)
            {
                DataGridColumnFilter.SetValueAccessor(column, options.FilterValueAccessor);
            }
            else
            {
                column.ClearValue(DataGridColumnFilter.ValueAccessorProperty);
            }

            if (options?.SortValueAccessor != null)
            {
                DataGridColumnSort.SetValueAccessor(column, options.SortValueAccessor);
            }
            else
            {
                column.ClearValue(DataGridColumnSort.ValueAccessorProperty);
            }

            if (options?.SortValueComparer != null)
            {
                DataGridColumnSort.SetValueComparer(column, options.SortValueComparer);
            }
            else
            {
                column.ClearValue(DataGridColumnSort.ValueComparerProperty);
            }

            if (options is IDataGridColumnDefinitionSortComparerProvider comparerProvider)
            {
                if (comparerProvider.AscendingComparer != null)
                {
                    DataGridColumnSort.SetAscendingComparer(column, comparerProvider.AscendingComparer);
                }
                else
                {
                    column.ClearValue(DataGridColumnSort.AscendingComparerProperty);
                }

                if (comparerProvider.DescendingComparer != null)
                {
                    DataGridColumnSort.SetDescendingComparer(column, comparerProvider.DescendingComparer);
                }
                else
                {
                    column.ClearValue(DataGridColumnSort.DescendingComparerProperty);
                }
            }
            else
            {
                column.ClearValue(DataGridColumnSort.AscendingComparerProperty);
                column.ClearValue(DataGridColumnSort.DescendingComparerProperty);
            }
        }
    }
}
