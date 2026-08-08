// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Avalonia.Controls
{
    /// <summary>Represents a generated column chooser entry backed by a live definition.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedColumnChoice : INotifyPropertyChanged
    {
        private readonly DataGridColumnDefinition _definition;

        internal DataGridGeneratedColumnChoice(string columnKey, DataGridColumnDefinition definition)
        {
            ColumnKey = columnKey;
            _definition = definition;
            _definition.PropertyChanged += DefinitionPropertyChanged;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>Gets stable column key.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets current header.</summary>
        public object Header => _definition.Header;
        /// <summary>Gets or sets visibility.</summary>
        public bool IsVisible
        {
            get => _definition.IsVisible ?? true;
            set => _definition.IsVisible = value;
        }
        /// <summary>Gets whether users may hide the column.</summary>
        public bool CanHide => _definition.CanUserHide ?? true;
        /// <summary>Gets current display order.</summary>
        public int? DisplayIndex => _definition.DisplayIndex;

        internal void Detach() => _definition.PropertyChanged -= DefinitionPropertyChanged;

        private void DefinitionPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(DataGridColumnDefinition.IsVisible)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            else if (eventArgs.PropertyName == nameof(DataGridColumnDefinition.Header)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
            else if (eventArgs.PropertyName == nameof(DataGridColumnDefinition.DisplayIndex)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayIndex)));
            else if (eventArgs.PropertyName == nameof(DataGridColumnDefinition.CanUserHide)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanHide)));
        }
    }

    /// <summary>Represents one immutable node in a generated column-band tree.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedBandNode
    {
        internal DataGridGeneratedBandNode(string name, int order, string columnKey, IReadOnlyList<DataGridGeneratedBandNode> children)
        {
            Name = name;
            Order = order;
            ColumnKey = columnKey;
            Children = children;
        }
        /// <summary>Gets band segment name.</summary>
        public string Name { get; }
        /// <summary>Gets order.</summary>
        public int Order { get; }
        /// <summary>Gets leaf column key, or null for a branch.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets child bands/leaves.</summary>
        public IReadOnlyList<DataGridGeneratedBandNode> Children { get; }
    }

    /// <summary>Owns generated column chooser, order, visibility, width reset, and band-tree state.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedColumnLayoutController : IDisposable
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly DataGridGeneratedColumnChoice[] _choices;
        private bool _disposed;

        /// <summary>Initializes a layout controller from generated definitions and band paths.</summary>
        public DataGridGeneratedColumnLayoutController(
            IReadOnlyList<DataGridColumnDefinition> columns,
            IReadOnlyList<DataGridGeneratedBandField> bands = null)
        {
            ArgumentNullException.ThrowIfNull(columns);
            _choices = new DataGridGeneratedColumnChoice[columns.Count];
            for (int index = 0; index < columns.Count; index++)
            {
                DataGridColumnDefinition definition = columns[index] ?? throw new ArgumentException("Columns cannot contain null.", nameof(columns));
                string key = definition.ColumnKey?.ToString() ?? throw new ArgumentException("Generated columns require stable ColumnKey values.", nameof(columns));
                if (!_entries.TryAdd(key, new Entry(definition, definition.IsVisible, definition.DisplayIndex, definition.Width)))
                {
                    throw new ArgumentException("Duplicate generated column key '" + key + "'.", nameof(columns));
                }
                _choices[index] = new DataGridGeneratedColumnChoice(key, definition);
            }
            Bands = BuildBands(bands);
        }

        /// <summary>Gets live column chooser entries.</summary>
        public IReadOnlyList<DataGridGeneratedColumnChoice> Choices => _choices;
        /// <summary>Gets immutable root band nodes.</summary>
        public IReadOnlyList<DataGridGeneratedBandNode> Bands { get; }

        /// <summary>Gets whether a column is currently visible.</summary>
        public bool IsVisible(string columnKey) => GetEntry(columnKey).Definition.IsVisible ?? true;

        /// <summary>Gets whether the generated visibility policy allows the requested value.</summary>
        public bool CanSetVisible(string columnKey, bool visible)
        {
            Entry entry = GetEntry(columnKey);
            return visible || entry.Definition.CanUserHide != false;
        }

        /// <summary>Sets visibility by stable key.</summary>
        public void SetVisible(string columnKey, bool visible)
        {
            Entry entry = GetEntry(columnKey);
            if (entry.Definition.CanUserHide == false && !visible) throw new InvalidOperationException("Column '" + columnKey + "' cannot be hidden.");
            entry.Definition.IsVisible = visible;
        }

        /// <summary>Sets display order by stable key.</summary>
        public void SetDisplayIndex(string columnKey, int displayIndex)
        {
            if (displayIndex < 0) throw new ArgumentOutOfRangeException(nameof(displayIndex));
            GetEntry(columnKey).Definition.DisplayIndex = displayIndex;
        }

        /// <summary>Restores generated visibility, order, and width defaults.</summary>
        public void Reset()
        {
            ThrowIfDisposed();
            foreach (Entry entry in _entries.Values)
            {
                entry.Definition.IsVisible = entry.IsVisible;
                entry.Definition.DisplayIndex = entry.DisplayIndex;
                entry.Definition.Width = entry.Width;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            for (int index = 0; index < _choices.Length; index++) _choices[index].Detach();
            _disposed = true;
        }

        private Entry GetEntry(string columnKey)
        {
            ThrowIfDisposed();
            if (columnKey == null) throw new ArgumentNullException(nameof(columnKey));
            return _entries.TryGetValue(columnKey, out Entry entry)
                ? entry
                : throw new KeyNotFoundException("Generated column '" + columnKey + "' was not found.");
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        private static IReadOnlyList<DataGridGeneratedBandNode> BuildBands(IReadOnlyList<DataGridGeneratedBandField> bands)
        {
            if (bands == null || bands.Count == 0) return Array.Empty<DataGridGeneratedBandNode>();
            var roots = new List<MutableNode>();
            for (int bandIndex = 0; bandIndex < bands.Count; bandIndex++)
            {
                DataGridGeneratedBandField band = bands[bandIndex];
                List<MutableNode> level = roots;
                for (int pathIndex = 0; pathIndex < band.Path.Count; pathIndex++)
                {
                    string name = band.Path[pathIndex];
                    MutableNode node = level.Find(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
                    if (node == null)
                    {
                        node = new MutableNode(name, band.Order);
                        level.Add(node);
                    }
                    level = node.Children;
                }
                level.Add(new MutableNode(band.ColumnKey, band.Order) { ColumnKey = band.ColumnKey });
            }
            return Freeze(roots);
        }

        private static IReadOnlyList<DataGridGeneratedBandNode> Freeze(List<MutableNode> nodes)
        {
            nodes.Sort(static (left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : StringComparer.Ordinal.Compare(left.Name, right.Name);
            });
            var result = new DataGridGeneratedBandNode[nodes.Count];
            for (int index = 0; index < nodes.Count; index++)
            {
                MutableNode node = nodes[index];
                result[index] = new DataGridGeneratedBandNode(node.Name, node.Order, node.ColumnKey, Freeze(node.Children));
            }
            return result;
        }

        private sealed class Entry
        {
            public Entry(DataGridColumnDefinition definition, bool? isVisible, int? displayIndex, DataGridLength? width)
            {
                Definition = definition; IsVisible = isVisible; DisplayIndex = displayIndex; Width = width;
            }
            public DataGridColumnDefinition Definition { get; }
            public bool? IsVisible { get; }
            public int? DisplayIndex { get; }
            public DataGridLength? Width { get; }
        }

        private sealed class MutableNode
        {
            public MutableNode(string name, int order) { Name = name; Order = order; }
            public string Name { get; }
            public int Order { get; }
            public string ColumnKey { get; set; }
            public List<MutableNode> Children { get; } = new();
        }
    }
}
