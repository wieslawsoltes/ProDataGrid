// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridColumnChooser : ItemsControl
    {
        public static readonly StyledProperty<DataGrid> DataGridProperty =
            AvaloniaProperty.Register<DataGridColumnChooser, DataGrid>(nameof(DataGrid));

        public static readonly StyledProperty<object> HeaderProperty =
            AvaloniaProperty.Register<DataGridColumnChooser, object>(nameof(Header));

        public static readonly StyledProperty<IDataTemplate> HeaderTemplateProperty =
            AvaloniaProperty.Register<DataGridColumnChooser, IDataTemplate>(nameof(HeaderTemplate));

        private readonly ObservableCollection<DataGridColumnChooserItem> _items;
        private DataGrid _attachedGrid;

        public DataGridColumnChooser()
        {
            _items = new ObservableCollection<DataGridColumnChooserItem>();
            ItemsSource = _items;
        }

        public DataGrid DataGrid
        {
            get => GetValue(DataGridProperty);
            set => SetValue(DataGridProperty, value);
        }

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public IDataTemplate HeaderTemplate
        {
            get => GetValue(HeaderTemplateProperty);
            set => SetValue(HeaderTemplateProperty, value);
        }

        public IReadOnlyList<DataGridColumnChooserItem> ColumnItems => _items;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (_attachedGrid == null && DataGrid != null)
            {
                AttachGrid(DataGrid);
                RebuildItems();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_attachedGrid != null)
            {
                DetachGrid(_attachedGrid);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DataGridProperty)
            {
                if (_attachedGrid != null)
                {
                    DetachGrid(_attachedGrid);
                }

                var newGrid = change.GetNewValue<DataGrid>();
                if (newGrid != null && IsAttachedToVisualTree)
                {
                    AttachGrid(newGrid);
                }

                RebuildItems();
            }
        }

        public void ShowAll()
        {
            foreach (var item in _items)
            {
                if (item.CanUserHide)
                {
                    item.IsVisible = true;
                }
            }
        }

        public void HideAll()
        {
            foreach (var item in _items)
            {
                if (item.CanUserHide)
                {
                    item.IsVisible = false;
                }
            }
        }

        private void AttachGrid(DataGrid grid)
        {
            _attachedGrid = grid;
            grid.ColumnDisplayIndexChanged += Grid_ColumnDisplayIndexChanged;
            grid.PropertyChanged += Grid_PropertyChanged;

            if (grid.ColumnsInternal is INotifyCollectionChanged notifier)
            {
                notifier.CollectionChanged += Columns_CollectionChanged;
            }
        }

        private void DetachGrid(DataGrid grid)
        {
            if (grid.ColumnsInternal is INotifyCollectionChanged notifier)
            {
                notifier.CollectionChanged -= Columns_CollectionChanged;
            }

            grid.ColumnDisplayIndexChanged -= Grid_ColumnDisplayIndexChanged;
            grid.PropertyChanged -= Grid_PropertyChanged;

            if (ReferenceEquals(_attachedGrid, grid))
            {
                _attachedGrid = null;
            }

            ClearItems();
        }

        private void Grid_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataGrid.CanUserHideColumnsProperty)
            {
                RebuildItems();
            }
        }

        private void Columns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildItems();
        }

        private void Grid_ColumnDisplayIndexChanged(object sender, DataGridColumnEventArgs e)
        {
            RebuildItems();
        }

        private void RebuildItems()
        {
            ClearItems();

            var grid = DataGrid ?? _attachedGrid;
            if (grid == null)
            {
                return;
            }

            var orderedColumns = grid.ColumnsInternal
                .Where(column => column != null && column is not DataGridFillerColumn)
                .OrderBy(column => column.DisplayIndex < 0 ? int.MaxValue : column.DisplayIndex)
                .ThenBy(column => column.Index)
                .ToList();

            foreach (var column in orderedColumns)
            {
                _items.Add(new DataGridColumnChooserItem(column));
            }
        }

        private void ClearItems()
        {
            foreach (var item in _items)
            {
                item.Dispose();
            }

            _items.Clear();
        }
    }
}
