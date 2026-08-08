// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls
{
    /// <summary>
    /// Selects generated controller capabilities while keeping optional feature work out of hot paths.
    /// </summary>
    [Flags]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedFeatures
    {
        /// <summary>No optional capability is enabled.</summary>
        None = 0,
        /// <summary>Generated columns and fast-path accessors.</summary>
        Columns = 1 << 0,
        /// <summary>Compiled sorting.</summary>
        Sorting = 1 << 1,
        /// <summary>Compiled filtering.</summary>
        Filtering = 1 << 2,
        /// <summary>Compiled searching.</summary>
        Searching = 1 << 3,
        /// <summary>Stable-key selection integration.</summary>
        Selection = 1 << 4,
        /// <summary>State capture and restoration.</summary>
        State = 1 << 5,
        /// <summary>Hierarchical item integration.</summary>
        Hierarchy = 1 << 6,
        /// <summary>Grouping integration.</summary>
        Grouping = 1 << 7,
        /// <summary>Summary integration.</summary>
        Summaries = 1 << 8,
        /// <summary>Conditional-formatting integration.</summary>
        ConditionalFormatting = 1 << 9,
        /// <summary>Editing integration.</summary>
        Editing = 1 << 10,
        /// <summary>Clipboard integration.</summary>
        Clipboard = 1 << 11,
        /// <summary>Fill integration.</summary>
        Fill = 1 << 12,
        /// <summary>Drag/drop integration.</summary>
        DragDrop = 1 << 13,
        /// <summary>Generated diagnostics.</summary>
        Diagnostics = 1 << 14,
        /// <summary>The local operation capabilities.</summary>
        Operations = Sorting | Filtering | Searching,
        /// <summary>All currently defined capabilities.</summary>
        All = (1 << 15) - 1
    }

    /// <summary>Identifies the source shape consumed by a generated controller adapter.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedSourceKind
    {
        /// <summary>A local enumerable.</summary>
        Enumerable,
        /// <summary>An observable collection.</summary>
        ObservableCollection,
        /// <summary>A DynamicData SourceList.</summary>
        DynamicDataSourceList,
        /// <summary>A DynamicData SourceCache.</summary>
        DynamicDataSourceCache,
        /// <summary>An asynchronous enumerable.</summary>
        AsyncEnumerable,
        /// <summary>A channel reader.</summary>
        ChannelReader,
        /// <summary>A remote query provider.</summary>
        Remote
    }

    /// <summary>
    /// Selects which layer executes generated sorting and filtering operations.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridOperationExecution
    {
        /// <summary>The grid collection view owns operation execution.</summary>
        View,

        /// <summary>An external reactive or streaming pipeline owns operation execution.</summary>
        ExternalPipeline,

        /// <summary>A remote query provider owns operation execution.</summary>
        Remote
    }

    /// <summary>Configures construction of a generated operation controller.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    struct DataGridGeneratedControllerOptions<TItem> : IEquatable<DataGridGeneratedControllerOptions<TItem>>
    {
        /// <summary>Initializes controller options.</summary>
        public DataGridGeneratedControllerOptions(
            DataGridOperationExecution execution,
            DataGridGeneratedFeatures features)
        {
            Execution = execution;
            Features = features;
        }

        /// <summary>Gets or sets the operation execution owner.</summary>
        public DataGridOperationExecution Execution { get; set; }

        /// <summary>Gets or sets enabled controller features.</summary>
        public DataGridGeneratedFeatures Features { get; set; }

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedControllerOptions<TItem> other) =>
            Execution == other.Execution && Features == other.Features;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridGeneratedControllerOptions<TItem> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine((int)Execution, (int)Features);

        /// <summary>Compares two option values.</summary>
        public static bool operator ==(DataGridGeneratedControllerOptions<TItem> left, DataGridGeneratedControllerOptions<TItem> right) => left.Equals(right);

        /// <summary>Compares two option values.</summary>
        public static bool operator !=(DataGridGeneratedControllerOptions<TItem> left, DataGridGeneratedControllerOptions<TItem> right) => !left.Equals(right);
    }

    /// <summary>Supplies a generated schema and validated options to a custom controller factory.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedControllerContext<TItem> : IEquatable<DataGridGeneratedControllerContext<TItem>>
    {
        /// <summary>Initializes a controller context.</summary>
        public DataGridGeneratedControllerContext(
            IDataGridGeneratedSchema<TItem> schema,
            DataGridGeneratedControllerOptions<TItem> options)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options;
        }

        /// <summary>Gets the canonical generated schema.</summary>
        public IDataGridGeneratedSchema<TItem> Schema { get; }

        /// <summary>Gets the configured controller options.</summary>
        public DataGridGeneratedControllerOptions<TItem> Options { get; }

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedControllerContext<TItem> other) =>
            ReferenceEquals(Schema, other.Schema) && Options.Equals(other.Options);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridGeneratedControllerContext<TItem> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Schema, Options);

        /// <summary>Compares two context values.</summary>
        public static bool operator ==(DataGridGeneratedControllerContext<TItem> left, DataGridGeneratedControllerContext<TItem> right) => left.Equals(right);

        /// <summary>Compares two context values.</summary>
        public static bool operator !=(DataGridGeneratedControllerContext<TItem> left, DataGridGeneratedControllerContext<TItem> right) => !left.Equals(right);
    }

    /// <summary>Creates a user-defined generated operation controller.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedControllerFactory<TItem>
    {
        /// <summary>Creates the controller for the supplied generated context.</summary>
        DataGridGeneratedOperationController<TItem> Create(
            in DataGridGeneratedControllerContext<TItem> context);
    }

    /// <summary>
    /// Identifies which compiled operation changed.
    /// </summary>
    [Flags]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedOperationChange
    {
        /// <summary>No operation changed.</summary>
        None = 0,

        /// <summary>The sort comparer changed.</summary>
        Sorting = 1,

        /// <summary>The filter predicate changed.</summary>
        Filtering = 2,

        /// <summary>The search predicate changed.</summary>
        Searching = 4,

        /// <summary>All compiled operations changed.</summary>
        All = Sorting | Filtering | Searching
    }

    /// <summary>
    /// Reports a compiled generated-operation update.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedOperationsChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes operation change arguments.
        /// </summary>
        public DataGridGeneratedOperationsChangedEventArgs(DataGridGeneratedOperationChange change, long version)
        {
            Change = change;
            Version = version;
        }

        /// <summary>Gets the operation that changed.</summary>
        public DataGridGeneratedOperationChange Change { get; }

        /// <summary>Gets the monotonic controller version after the change.</summary>
        public long Version { get; }
    }

    /// <summary>
    /// Stores a reusable immutable sorting, filtering, and searching configuration.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedOperationPreset
    {
        private readonly SortingDescriptor[] _sorting;
        private readonly FilteringDescriptor[] _filtering;
        private readonly SearchDescriptor[] _searching;

        /// <summary>Initializes a named operation preset.</summary>
        public DataGridGeneratedOperationPreset(
            string name,
            IEnumerable<SortingDescriptor> sorting = null,
            IEnumerable<FilteringDescriptor> filtering = null,
            IEnumerable<SearchDescriptor> searching = null)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Preset name cannot be empty.", nameof(name))
                : name;
            _sorting = Copy(sorting);
            _filtering = Copy(filtering);
            _searching = Copy(searching);
        }

        /// <summary>Gets the stable preset name.</summary>
        public string Name { get; }

        /// <summary>Gets the sort descriptors.</summary>
        public IReadOnlyList<SortingDescriptor> Sorting => _sorting;

        /// <summary>Gets the filter descriptors.</summary>
        public IReadOnlyList<FilteringDescriptor> Filtering => _filtering;

        /// <summary>Gets the search descriptors.</summary>
        public IReadOnlyList<SearchDescriptor> Searching => _searching;

        private static T[] Copy<T>(IEnumerable<T> source)
        {
            if (source == null)
            {
                return Array.Empty<T>();
            }

            return source is ICollection<T> collection
                ? CopyCollection(collection)
                : new List<T>(source).ToArray();
        }

        private static T[] CopyCollection<T>(ICollection<T> source)
        {
            var result = new T[source.Count];
            source.CopyTo(result, 0);
            return result;
        }
    }

    /// <summary>
    /// Owns reflection-free column, model, and compiled operation state for one grid.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedOperationController<TItem> : IDisposable
    {
        private readonly IDataGridGeneratedSchema<TItem> _schema;
        private bool _disposed;
        private int _updateNesting;
        private DataGridGeneratedOperationChange _pendingChange;

        /// <summary>
        /// Initializes a controller and creates its operation models.
        /// </summary>
        public DataGridGeneratedOperationController(
            IDataGridGeneratedSchema<TItem> schema,
            DataGridOperationExecution execution = DataGridOperationExecution.View,
            DataGridGeneratedFeatures features = DataGridGeneratedFeatures.Columns | DataGridGeneratedFeatures.Operations)
            : this(schema, new SortingModel(), new FilteringModel(), new SearchModel(), execution, features)
        {
        }

        /// <summary>
        /// Initializes a controller with caller-provided operation models.
        /// </summary>
        public DataGridGeneratedOperationController(
            IDataGridGeneratedSchema<TItem> schema,
            SortingModel sortingModel,
            FilteringModel filteringModel,
            SearchModel searchModel,
            DataGridOperationExecution execution = DataGridOperationExecution.View,
            DataGridGeneratedFeatures features = DataGridGeneratedFeatures.Columns | DataGridGeneratedFeatures.Operations)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            SortingModel = sortingModel ?? throw new ArgumentNullException(nameof(sortingModel));
            FilteringModel = filteringModel ?? throw new ArgumentNullException(nameof(filteringModel));
            SearchModel = searchModel ?? throw new ArgumentNullException(nameof(searchModel));
            Execution = execution;
            Features = features;
            bool ownsViewOperations = execution == DataGridOperationExecution.View;
            SortingModel.OwnsViewSorts = ownsViewOperations && HasFeature(DataGridGeneratedFeatures.Sorting);
            FilteringModel.OwnsViewFilter = ownsViewOperations && HasFeature(DataGridGeneratedFeatures.Filtering);

            Columns = schema.CreateColumnDefinitions();
            FastPathOptions = schema.CreateFastPathOptions();
            SortComparer = schema.CreateSortComparer(Array.Empty<SortingDescriptor>());
            FilterPredicate = schema.CreateFilterPredicate(Array.Empty<FilteringDescriptor>());
            SearchPredicate = schema.CreateSearchPredicate(Array.Empty<SearchDescriptor>());

            if (HasFeature(DataGridGeneratedFeatures.Sorting))
            {
                SortingModel.SortingChanged += OnSortingChanged;
            }

            if (HasFeature(DataGridGeneratedFeatures.Filtering))
            {
                FilteringModel.FilteringChanged += OnFilteringChanged;
            }

            if (HasFeature(DataGridGeneratedFeatures.Searching))
            {
                SearchModel.SearchChanged += OnSearchChanged;
            }
        }

        /// <summary>Gets the generated schema.</summary>
        public IDataGridGeneratedSchema<TItem> Schema => _schema;

        /// <summary>Gets the operation execution owner.</summary>
        public DataGridOperationExecution Execution { get; }

        /// <summary>Gets the capabilities enabled for this controller.</summary>
        public DataGridGeneratedFeatures Features { get; }

        /// <summary>Gets the grid-instance column definitions.</summary>
        public DataGridColumnDefinitionList Columns { get; }

        /// <summary>Gets the generated fast-path options.</summary>
        public DataGridFastPathOptions FastPathOptions { get; }

        /// <summary>Gets the sorting model.</summary>
        public SortingModel SortingModel { get; }

        /// <summary>Gets the filtering model.</summary>
        public FilteringModel FilteringModel { get; }

        /// <summary>Gets the search model.</summary>
        public SearchModel SearchModel { get; }

        /// <summary>Gets the latest compiled sort comparer.</summary>
        public IComparer<TItem> SortComparer { get; private set; }

        /// <summary>Gets the latest compiled filter predicate.</summary>
        public Func<TItem, bool> FilterPredicate { get; private set; }

        /// <summary>Gets the latest compiled search predicate.</summary>
        public Func<TItem, bool> SearchPredicate { get; private set; }

        /// <summary>Gets the monotonic compiled-operation version.</summary>
        public long Version { get; private set; }

        /// <summary>Occurs after one of the compiled operations changes.</summary>
        public event EventHandler<DataGridGeneratedOperationsChangedEventArgs> OperationsChanged;

        /// <summary>Replaces all sort descriptors.</summary>
        public void SetSorting(IEnumerable<SortingDescriptor> descriptors)
        {
            ThrowIfDisposed();
            ThrowIfFeatureDisabled(DataGridGeneratedFeatures.Sorting);
            SortingModel.Apply(descriptors ?? throw new ArgumentNullException(nameof(descriptors)));
        }

        /// <summary>Replaces all filter descriptors.</summary>
        public void SetFiltering(IEnumerable<FilteringDescriptor> descriptors)
        {
            ThrowIfDisposed();
            ThrowIfFeatureDisabled(DataGridGeneratedFeatures.Filtering);
            FilteringModel.Apply(descriptors ?? throw new ArgumentNullException(nameof(descriptors)));
        }

        /// <summary>Replaces all search descriptors.</summary>
        public void SetSearching(IEnumerable<SearchDescriptor> descriptors)
        {
            ThrowIfDisposed();
            ThrowIfFeatureDisabled(DataGridGeneratedFeatures.Searching);
            SearchModel.Apply(descriptors ?? throw new ArgumentNullException(nameof(descriptors)));
        }

        /// <summary>Applies all parts of a reusable preset as one controller revision.</summary>
        public void ApplyPreset(DataGridGeneratedOperationPreset preset)
        {
            ThrowIfDisposed();
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            using (DeferRefresh())
            {
                if (HasFeature(DataGridGeneratedFeatures.Sorting))
                {
                    SortingModel.Apply(preset.Sorting);
                }

                if (HasFeature(DataGridGeneratedFeatures.Filtering))
                {
                    FilteringModel.Apply(preset.Filtering);
                }

                if (HasFeature(DataGridGeneratedFeatures.Searching))
                {
                    SearchModel.Apply(preset.Searching);
                }
            }
        }

        /// <summary>Clears every enabled operation as one controller revision.</summary>
        public void ClearOperations()
        {
            ThrowIfDisposed();
            using (DeferRefresh())
            {
                if (HasFeature(DataGridGeneratedFeatures.Sorting))
                {
                    SortingModel.Clear();
                }

                if (HasFeature(DataGridGeneratedFeatures.Filtering))
                {
                    FilteringModel.Clear();
                }

                if (HasFeature(DataGridGeneratedFeatures.Searching))
                {
                    SearchModel.Clear();
                }
            }
        }

        /// <summary>Defers model events and publishes one combined controller revision.</summary>
        public IDisposable DeferRefresh()
        {
            ThrowIfDisposed();
            _updateNesting++;
            return new UpdateScope(this,
                HasFeature(DataGridGeneratedFeatures.Sorting) ? SortingModel.DeferRefresh() : null,
                HasFeature(DataGridGeneratedFeatures.Filtering) ? FilteringModel.DeferRefresh() : null,
                HasFeature(DataGridGeneratedFeatures.Searching) ? SearchModel.DeferRefresh() : null);
        }

        /// <summary>
        /// Releases model subscriptions. Caller-provided models are not disposed.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SortingModel.SortingChanged -= OnSortingChanged;
            FilteringModel.FilteringChanged -= OnFilteringChanged;
            SearchModel.SearchChanged -= OnSearchChanged;
        }

        private void OnSortingChanged(object sender, SortingChangedEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            SortComparer = _schema.CreateSortComparer(args.NewDescriptors);
            Publish(DataGridGeneratedOperationChange.Sorting);
        }

        private void OnFilteringChanged(object sender, FilteringChangedEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            FilterPredicate = _schema.CreateFilterPredicate(args.NewDescriptors);
            Publish(DataGridGeneratedOperationChange.Filtering);
        }

        private void OnSearchChanged(object sender, SearchChangedEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            SearchPredicate = _schema.CreateSearchPredicate(args.NewDescriptors);
            Publish(DataGridGeneratedOperationChange.Searching);
        }

        private void Publish(DataGridGeneratedOperationChange change)
        {
            if (_updateNesting > 0)
            {
                _pendingChange |= change;
                return;
            }

            Version++;
            OperationsChanged?.Invoke(this, new DataGridGeneratedOperationsChangedEventArgs(change, Version));
        }

        private void EndUpdate(IDisposable sorting, IDisposable filtering, IDisposable searching)
        {
            try
            {
                searching?.Dispose();
                filtering?.Dispose();
                sorting?.Dispose();
            }
            finally
            {
                _updateNesting--;
                if (_updateNesting == 0 && _pendingChange != DataGridGeneratedOperationChange.None)
                {
                    DataGridGeneratedOperationChange change = _pendingChange;
                    _pendingChange = DataGridGeneratedOperationChange.None;
                    Publish(change);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private bool HasFeature(DataGridGeneratedFeatures feature) => (Features & feature) == feature;

        private void ThrowIfFeatureDisabled(DataGridGeneratedFeatures feature)
        {
            if (!HasFeature(feature))
            {
                throw new InvalidOperationException("Generated controller feature '" + feature + "' is not enabled.");
            }
        }

        private sealed class UpdateScope : IDisposable
        {
            private DataGridGeneratedOperationController<TItem> _owner;
            private IDisposable _sorting;
            private IDisposable _filtering;
            private IDisposable _searching;

            public UpdateScope(
                DataGridGeneratedOperationController<TItem> owner,
                IDisposable sorting,
                IDisposable filtering,
                IDisposable searching)
            {
                _owner = owner;
                _sorting = sorting;
                _filtering = filtering;
                _searching = searching;
            }

            public void Dispose()
            {
                DataGridGeneratedOperationController<TItem> owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                IDisposable sorting = _sorting;
                IDisposable filtering = _filtering;
                IDisposable searching = _searching;
                _sorting = null;
                _filtering = null;
                _searching = null;
                owner.EndUpdate(sorting, filtering, searching);
            }
        }
    }
}
