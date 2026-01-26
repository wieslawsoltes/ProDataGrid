// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridPivoting;

namespace Avalonia.Controls.DataGridReporting
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class OutlineReportChangedEventArgs : EventArgs
    {
        public OutlineReportChangedEventArgs(
            IReadOnlyList<OutlineRow> rows,
            IReadOnlyList<DataGridColumnDefinition> columnDefinitions)
        {
            Rows = rows;
            ColumnDefinitions = columnDefinitions;
        }

        public IReadOnlyList<OutlineRow> Rows { get; }

        public IReadOnlyList<DataGridColumnDefinition> ColumnDefinitions { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class OutlineReportModel : INotifyPropertyChanged, IDisposable
    {
        private IEnumerable? _itemsSource;
        private INotifyCollectionChanged? _itemsNotifier;
        private bool _autoRefresh = true;
        private int _updateNesting;
        private bool _pendingRefresh;
        private bool _isRefreshing;
        private readonly PivotAggregatorRegistry _aggregators = new();
        private readonly HashSet<OutlineFieldBase> _subscribedFields = new();
        private CultureInfo _culture = CultureInfo.CurrentCulture;

        public OutlineReportModel()
        {
            GroupFields = new ObservableCollection<OutlineGroupField>();
            ValueFields = new ObservableCollection<OutlineValueField>();
            Layout = new OutlineLayoutOptions();

            Rows = new OutlineObservableCollection<OutlineRow>();
            ColumnDefinitions = new OutlineObservableCollection<DataGridColumnDefinition>();

            HierarchicalModel = new HierarchicalModel<OutlineRow>(new HierarchicalOptions<OutlineRow>
            {
                ChildrenSelector = row => row.Children,
                IsExpandedSelector = row => row.IsExpanded,
                IsExpandedSetter = (row, value) => row.IsExpanded = value
            });

            GroupFields.CollectionChanged += Fields_CollectionChanged;
            ValueFields.CollectionChanged += Fields_CollectionChanged;
            Layout.PropertyChanged += Layout_PropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<OutlineReportChangedEventArgs>? ReportChanged;

        public ObservableCollection<OutlineGroupField> GroupFields { get; }

        public ObservableCollection<OutlineValueField> ValueFields { get; }

        public OutlineLayoutOptions Layout { get; }

        public OutlineObservableCollection<OutlineRow> Rows { get; }

        public OutlineObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }

        public HierarchicalModel<OutlineRow> HierarchicalModel { get; }

        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                if (!Equals(_culture, value))
                {
                    _culture = value ?? CultureInfo.CurrentCulture;
                    RequestRefresh();
                }
            }
        }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh == value)
                {
                    return;
                }

                _autoRefresh = value;
                if (_autoRefresh && _pendingRefresh)
                {
                    Refresh();
                }
            }
        }

        public IEnumerable? ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (ReferenceEquals(_itemsSource, value))
                {
                    return;
                }

                DetachItemsNotifier();
                _itemsSource = value;
                AttachItemsNotifier();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemsSource)));
                RequestRefresh();
            }
        }

        public void Refresh()
        {
            if (_isRefreshing)
            {
                _pendingRefresh = true;
                return;
            }

            do
            {
                _pendingRefresh = false;
                _isRefreshing = true;
                try
                {
                    var builder = new OutlineReportBuilder(this, _aggregators, _culture);
                    var result = builder.Build();

                    Rows.ResetWith(result.Rows);
                    ColumnDefinitions.ResetWith(result.ColumnDefinitions);
                    HierarchicalModel.SetRoots(result.Rows);

                    ReportChanged?.Invoke(this, new OutlineReportChangedEventArgs(result.Rows, result.ColumnDefinitions));
                }
                finally
                {
                    _isRefreshing = false;
                }
            }
            while (_pendingRefresh && _autoRefresh && _updateNesting == 0);
        }

        public IDisposable DeferRefresh()
        {
            BeginUpdate();
            return new UpdateScope(this);
        }

        public void BeginUpdate()
        {
            _updateNesting++;
        }

        public void EndUpdate()
        {
            if (_updateNesting == 0)
            {
                throw new InvalidOperationException("EndUpdate called without matching BeginUpdate.");
            }

            _updateNesting--;
            if (_updateNesting == 0 && _pendingRefresh)
            {
                Refresh();
            }
        }

        public void Dispose()
        {
            DetachItemsNotifier();
            DetachAllFieldHandlers();
            GroupFields.CollectionChanged -= Fields_CollectionChanged;
            ValueFields.CollectionChanged -= Fields_CollectionChanged;
            Layout.PropertyChanged -= Layout_PropertyChanged;
        }

        private void RequestRefresh()
        {
            if (_isRefreshing)
            {
                _pendingRefresh = true;
                return;
            }

            if (!AutoRefresh)
            {
                _pendingRefresh = true;
                return;
            }

            if (_updateNesting > 0)
            {
                _pendingRefresh = true;
                return;
            }

            Refresh();
        }

        private void AttachItemsNotifier()
        {
            if (_itemsSource is INotifyCollectionChanged notifier)
            {
                _itemsNotifier = notifier;
                _itemsNotifier.CollectionChanged += Items_CollectionChanged;
            }
        }

        private void DetachItemsNotifier()
        {
            if (_itemsNotifier != null)
            {
                _itemsNotifier.CollectionChanged -= Items_CollectionChanged;
                _itemsNotifier = null;
            }
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RequestRefresh();
        }

        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RequestRefresh();
        }

        private void Fields_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ReattachAllFieldHandlers();
                RequestRefresh();
                return;
            }

            if (e.NewItems != null)
            {
                AttachFieldHandlers(CastFields(e.NewItems));
            }

            if (e.OldItems != null)
            {
                DetachFieldHandlers(CastFields(e.OldItems));
            }

            RequestRefresh();
        }

        private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RequestRefresh();
        }

        private void AttachFieldHandlers(IEnumerable<OutlineFieldBase> fields)
        {
            foreach (var field in fields)
            {
                if (_subscribedFields.Add(field))
                {
                    field.PropertyChanged += Field_PropertyChanged;
                }
            }
        }

        private void DetachFieldHandlers(IEnumerable<OutlineFieldBase> fields)
        {
            foreach (var field in fields)
            {
                if (_subscribedFields.Remove(field))
                {
                    field.PropertyChanged -= Field_PropertyChanged;
                }
            }
        }

        private static IEnumerable<OutlineFieldBase> CastFields(IEnumerable items)
        {
            foreach (var item in items)
            {
                if (item is OutlineFieldBase field)
                {
                    yield return field;
                }
            }
        }

        private void DetachAllFieldHandlers()
        {
            foreach (var field in _subscribedFields)
            {
                field.PropertyChanged -= Field_PropertyChanged;
            }

            _subscribedFields.Clear();
        }

        private void ReattachAllFieldHandlers()
        {
            DetachAllFieldHandlers();
            AttachFieldHandlers(GroupFields);
            AttachFieldHandlers(ValueFields);
        }

        private sealed class UpdateScope : IDisposable
        {
            private OutlineReportModel? _owner;

            public UpdateScope(OutlineReportModel owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.EndUpdate();
            }
        }
    }
}
