// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;

namespace Avalonia.Controls.DataGridBanding
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class ColumnBandHeader
    {
        public ColumnBandHeader(IReadOnlyList<string> segments)
        {
            Segments = segments ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Segments { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class ColumnBand : INotifyPropertyChanged
    {
        private string? _header;
        private DataGridColumnDefinition? _columnDefinition;

        public ColumnBand()
        {
            Children = new ObservableCollection<ColumnBand>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ColumnBand> Children { get; }

        public string? Header
        {
            get => _header;
            set => SetProperty(ref _header, value, nameof(Header));
        }

        public DataGridColumnDefinition? ColumnDefinition
        {
            get => _columnDefinition;
            set => SetProperty(ref _columnDefinition, value, nameof(ColumnDefinition));
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class ColumnBandObservableCollection<T> : ObservableCollection<T>
    {
        public void ResetWith(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            CheckReentrancy();

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class ColumnBandModel : INotifyPropertyChanged, IDisposable
    {
        private bool _autoRefresh = true;
        private int _updateNesting;
        private bool _pendingRefresh;
        private bool _isRefreshing;
        private string _headerTemplateKey = "DataGridColumnBandHeaderTemplate";
        private readonly Dictionary<ColumnBand, DataGridColumnDefinition?> _columnDefinitions = new();

        public ColumnBandModel()
        {
            Bands = new ObservableCollection<ColumnBand>();
            ColumnDefinitions = new ColumnBandObservableCollection<DataGridColumnDefinition>();

            Bands.CollectionChanged += Bands_CollectionChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ColumnBand> Bands { get; }

        public ColumnBandObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }

        public string HeaderTemplateKey
        {
            get => _headerTemplateKey;
            set
            {
                if (_headerTemplateKey == value)
                {
                    return;
                }

                _headerTemplateKey = value ?? string.Empty;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderTemplateKey)));
                RequestRefresh();
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
                    var definitions = BuildDefinitions();
                    ColumnDefinitions.ResetWith(definitions);
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
            Bands.CollectionChanged -= Bands_CollectionChanged;
            DetachAllBands(Bands);
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

        private void Bands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ColumnBand band in e.OldItems)
                {
                    DetachBand(band);
                }
            }

            if (e.NewItems != null)
            {
                foreach (ColumnBand band in e.NewItems)
                {
                    AttachBand(band);
                }
            }

            RequestRefresh();
        }

        private void AttachBand(ColumnBand band)
        {
            band.PropertyChanged += Band_PropertyChanged;
            band.Children.CollectionChanged += BandChildren_CollectionChanged;
            AttachColumnDefinition(band);
            foreach (var child in band.Children)
            {
                AttachBand(child);
            }
        }

        private void DetachBand(ColumnBand band)
        {
            band.PropertyChanged -= Band_PropertyChanged;
            band.Children.CollectionChanged -= BandChildren_CollectionChanged;
            DetachColumnDefinition(band);
            foreach (var child in band.Children)
            {
                DetachBand(child);
            }
        }

        private void DetachAllBands(IEnumerable<ColumnBand> bands)
        {
            foreach (var band in bands)
            {
                DetachBand(band);
            }
        }

        private void BandChildren_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ColumnBand band in e.OldItems)
                {
                    DetachBand(band);
                }
            }

            if (e.NewItems != null)
            {
                foreach (ColumnBand band in e.NewItems)
                {
                    AttachBand(band);
                }
            }

            RequestRefresh();
        }

        private void Band_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ColumnBand band && e.PropertyName == nameof(ColumnBand.ColumnDefinition))
            {
                UpdateColumnDefinitionSubscription(band);
            }

            if (e.PropertyName == nameof(ColumnBand.Header) || e.PropertyName == nameof(ColumnBand.ColumnDefinition))
            {
                RequestRefresh();
            }
        }

        private void AttachColumnDefinition(ColumnBand band)
        {
            var definition = band.ColumnDefinition;
            if (_columnDefinitions.TryGetValue(band, out var existing))
            {
                if (ReferenceEquals(existing, definition))
                {
                    return;
                }

                if (existing != null)
                {
                    existing.PropertyChanged -= ColumnDefinition_PropertyChanged;
                }
            }

            if (definition != null)
            {
                definition.PropertyChanged += ColumnDefinition_PropertyChanged;
            }

            _columnDefinitions[band] = definition;
        }

        private void DetachColumnDefinition(ColumnBand band)
        {
            if (_columnDefinitions.TryGetValue(band, out var definition))
            {
                if (definition != null)
                {
                    definition.PropertyChanged -= ColumnDefinition_PropertyChanged;
                }

                _columnDefinitions.Remove(band);
            }
        }

        private void UpdateColumnDefinitionSubscription(ColumnBand band)
        {
            DetachColumnDefinition(band);
            AttachColumnDefinition(band);
        }

        private void ColumnDefinition_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(DataGridColumnDefinition.Header))
            {
                RequestRefresh();
            }
        }

        private List<DataGridColumnDefinition> BuildDefinitions()
        {
            var definitions = new List<DataGridColumnDefinition>();
            foreach (var band in Bands)
            {
                AppendBand(definitions, band, Array.Empty<string>());
            }

            return definitions;
        }

        private void AppendBand(List<DataGridColumnDefinition> definitions, ColumnBand band, IReadOnlyList<string> path)
        {
            var header = band.Header;
            var nextPath = path;
            if (!string.IsNullOrEmpty(header))
            {
                var newPath = new List<string>(path.Count + 1);
                newPath.AddRange(path);
                newPath.Add(header);
                nextPath = newPath;
            }

            if (band.Children.Count > 0)
            {
                foreach (var child in band.Children)
                {
                    AppendBand(definitions, child, nextPath);
                }

                return;
            }

            if (band.ColumnDefinition == null)
            {
                return;
            }

            var segments = BuildSegments(nextPath, band.ColumnDefinition, band.Header);
            if (band.ColumnDefinition.Header is not ColumnBandHeader bandHeader ||
                !HeadersMatch(bandHeader, segments))
            {
                band.ColumnDefinition.Header = new ColumnBandHeader(segments);
            }
            if (!string.IsNullOrEmpty(_headerTemplateKey))
            {
                band.ColumnDefinition.HeaderTemplateKey = _headerTemplateKey;
            }

            definitions.Add(band.ColumnDefinition);
        }

        private static IReadOnlyList<string> BuildSegments(IReadOnlyList<string> path, DataGridColumnDefinition definition, string? bandHeader)
        {
            var segments = new List<string>(path.Count + 1);
            segments.AddRange(path);

            if (!string.IsNullOrEmpty(bandHeader))
            {
                if (segments.Count == 0 || !string.Equals(segments[segments.Count - 1], bandHeader, StringComparison.Ordinal))
                {
                    segments.Add(bandHeader);
                }
            }
            else
            {
                var headerText = GetHeaderText(definition.Header);
                if (!string.IsNullOrEmpty(headerText) &&
                    (segments.Count == 0 || !string.Equals(segments[segments.Count - 1], headerText, StringComparison.Ordinal)))
                {
                    segments.Add(headerText);
                }
            }

            return segments;
        }

        private static string GetHeaderText(object? header)
        {
            if (header == null)
            {
                return string.Empty;
            }

            if (header is ColumnBandHeader bandHeader)
            {
                return bandHeader.Segments.Count > 0 ? bandHeader.Segments[bandHeader.Segments.Count - 1] : string.Empty;
            }

            return Convert.ToString(header, CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private static bool HeadersMatch(ColumnBandHeader header, IReadOnlyList<string> segments)
        {
            if (header.Segments.Count != segments.Count)
            {
                return false;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                if (!string.Equals(header.Segments[i], segments[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class UpdateScope : IDisposable
        {
            private ColumnBandModel? _owner;

            public UpdateScope(ColumnBandModel owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.EndUpdate();
                _owner = null;
            }
        }
    }
}
