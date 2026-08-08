// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls
{
    /// <summary>Identifies a generated header-menu operation.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedHeaderCommandKind
    {
        /// <summary>Sorts the field ascending.</summary>
        SortAscending,
        /// <summary>Sorts the field descending.</summary>
        SortDescending,
        /// <summary>Clears sorting for the field.</summary>
        ClearSort,
        /// <summary>Requests the field filter editor.</summary>
        ShowFilter,
        /// <summary>Clears filtering for the field.</summary>
        ClearFilter,
        /// <summary>Shows the field.</summary>
        ShowColumn,
        /// <summary>Hides the field.</summary>
        HideColumn,
        /// <summary>Pins the field to the left edge.</summary>
        PinLeft,
        /// <summary>Pins the field to the right edge.</summary>
        PinRight,
        /// <summary>Removes field pinning.</summary>
        Unpin,
        /// <summary>Freezes columns from the left edge through the field.</summary>
        FreezeThrough,
        /// <summary>Clears the frozen-column region.</summary>
        ClearFrozenColumns,
        /// <summary>Requests field autosizing.</summary>
        AutoSize,
        /// <summary>Restores the generated layout defaults.</summary>
        ResetLayout
    }

    /// <summary>Contains a stable generated header-command request.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedHeaderCommandRequest : IEquatable<DataGridGeneratedHeaderCommandRequest>
    {
        /// <summary>Initializes a request.</summary>
        public DataGridGeneratedHeaderCommandRequest(DataGridGeneratedHeaderCommandKind kind, string columnKey)
        {
            Kind = kind;
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
        }

        /// <summary>Gets the operation.</summary>
        public DataGridGeneratedHeaderCommandKind Kind { get; }

        /// <summary>Gets the stable generated field key.</summary>
        public string ColumnKey { get; }

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedHeaderCommandRequest other) =>
            Kind == other.Kind && string.Equals(ColumnKey, other.ColumnKey, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is DataGridGeneratedHeaderCommandRequest other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (ColumnKey == null ? 0 : StringComparer.Ordinal.GetHashCode(ColumnKey));
            }
        }

        /// <summary>Tests two requests for equality.</summary>
        public static bool operator ==(
            DataGridGeneratedHeaderCommandRequest left,
            DataGridGeneratedHeaderCommandRequest right) => left.Equals(right);

        /// <summary>Tests two requests for inequality.</summary>
        public static bool operator !=(
            DataGridGeneratedHeaderCommandRequest left,
            DataGridGeneratedHeaderCommandRequest right) => !left.Equals(right);
    }

    /// <summary>Executes and invalidates generated header commands.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedHeaderCommandHandler
    {
        /// <summary>Raised when command availability may have changed.</summary>
        event EventHandler StateChanged;

        /// <summary>Tests a generated request.</summary>
        bool CanExecute(DataGridGeneratedHeaderCommandRequest request);

        /// <summary>Executes a generated request.</summary>
        void Execute(DataGridGeneratedHeaderCommandRequest request);
    }

    /// <summary>
    /// Handles grid-instance operations that cannot be owned by a reflection-free ViewModel controller.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedHeaderInteraction
    {
        /// <summary>Tests a grid-instance request such as pinning or autosizing.</summary>
        bool CanExecute(DataGridGeneratedHeaderCommandRequest request);

        /// <summary>Executes a grid-instance request.</summary>
        void Execute(DataGridGeneratedHeaderCommandRequest request);
    }

    /// <summary>Provides the complete command group for one generated field.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedHeaderCommandSet : IDisposable
    {
        /// <summary>Initializes commands for a stable field key.</summary>
        public DataGridGeneratedHeaderCommandSet(string columnKey, IDataGridGeneratedHeaderCommandHandler handler)
        {
            if (columnKey == null) throw new ArgumentNullException(nameof(columnKey));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ColumnKey = columnKey;
            SortAscending = Create(handler, DataGridGeneratedHeaderCommandKind.SortAscending);
            SortDescending = Create(handler, DataGridGeneratedHeaderCommandKind.SortDescending);
            ClearSort = Create(handler, DataGridGeneratedHeaderCommandKind.ClearSort);
            ShowFilter = Create(handler, DataGridGeneratedHeaderCommandKind.ShowFilter);
            ClearFilter = Create(handler, DataGridGeneratedHeaderCommandKind.ClearFilter);
            ShowColumn = Create(handler, DataGridGeneratedHeaderCommandKind.ShowColumn);
            HideColumn = Create(handler, DataGridGeneratedHeaderCommandKind.HideColumn);
            PinLeft = Create(handler, DataGridGeneratedHeaderCommandKind.PinLeft);
            PinRight = Create(handler, DataGridGeneratedHeaderCommandKind.PinRight);
            Unpin = Create(handler, DataGridGeneratedHeaderCommandKind.Unpin);
            FreezeThrough = Create(handler, DataGridGeneratedHeaderCommandKind.FreezeThrough);
            ClearFrozenColumns = Create(handler, DataGridGeneratedHeaderCommandKind.ClearFrozenColumns);
            AutoSize = Create(handler, DataGridGeneratedHeaderCommandKind.AutoSize);
            ResetLayout = Create(handler, DataGridGeneratedHeaderCommandKind.ResetLayout);
        }

        /// <summary>Gets the stable field key.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets the ascending-sort command.</summary>
        public ICommand SortAscending { get; }
        /// <summary>Gets the descending-sort command.</summary>
        public ICommand SortDescending { get; }
        /// <summary>Gets the clear-sort command.</summary>
        public ICommand ClearSort { get; }
        /// <summary>Gets the show-filter command.</summary>
        public ICommand ShowFilter { get; }
        /// <summary>Gets the clear-filter command.</summary>
        public ICommand ClearFilter { get; }
        /// <summary>Gets the show-column command.</summary>
        public ICommand ShowColumn { get; }
        /// <summary>Gets the hide-column command.</summary>
        public ICommand HideColumn { get; }
        /// <summary>Gets the pin-left command.</summary>
        public ICommand PinLeft { get; }
        /// <summary>Gets the pin-right command.</summary>
        public ICommand PinRight { get; }
        /// <summary>Gets the unpin command.</summary>
        public ICommand Unpin { get; }
        /// <summary>Gets the freeze-through command.</summary>
        public ICommand FreezeThrough { get; }
        /// <summary>Gets the clear-frozen-columns command.</summary>
        public ICommand ClearFrozenColumns { get; }
        /// <summary>Gets the autosize command.</summary>
        public ICommand AutoSize { get; }
        /// <summary>Gets the reset-layout command.</summary>
        public ICommand ResetLayout { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            ((DataGridGeneratedHeaderCommand)SortAscending).Dispose();
            ((DataGridGeneratedHeaderCommand)SortDescending).Dispose();
            ((DataGridGeneratedHeaderCommand)ClearSort).Dispose();
            ((DataGridGeneratedHeaderCommand)ShowFilter).Dispose();
            ((DataGridGeneratedHeaderCommand)ClearFilter).Dispose();
            ((DataGridGeneratedHeaderCommand)ShowColumn).Dispose();
            ((DataGridGeneratedHeaderCommand)HideColumn).Dispose();
            ((DataGridGeneratedHeaderCommand)PinLeft).Dispose();
            ((DataGridGeneratedHeaderCommand)PinRight).Dispose();
            ((DataGridGeneratedHeaderCommand)Unpin).Dispose();
            ((DataGridGeneratedHeaderCommand)FreezeThrough).Dispose();
            ((DataGridGeneratedHeaderCommand)ClearFrozenColumns).Dispose();
            ((DataGridGeneratedHeaderCommand)AutoSize).Dispose();
            ((DataGridGeneratedHeaderCommand)ResetLayout).Dispose();
        }

        private DataGridGeneratedHeaderCommand Create(
            IDataGridGeneratedHeaderCommandHandler handler,
            DataGridGeneratedHeaderCommandKind kind) =>
            new(handler, new DataGridGeneratedHeaderCommandRequest(kind, ColumnKey));
    }

    /// <summary>
    /// Coordinates generated header commands without reflection or a DataGrid reference in the ViewModel.
    /// </summary>
    /// <typeparam name="TItem">The generated row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedHeaderCommandController<TItem> : IDataGridGeneratedHeaderCommandHandler, IDisposable
    {
        private readonly DataGridGeneratedSchemaManifest _manifest;
        private readonly DataGridGeneratedOperationController<TItem> _operations;
        private readonly DataGridGeneratedColumnLayoutController _layout;
        private readonly IDataGridGeneratedHeaderInteraction _interaction;
        private readonly Dictionary<string, DataGridGeneratedHeaderCommandSet> _commands = new(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>Initializes a generated header-command controller.</summary>
        public DataGridGeneratedHeaderCommandController(
            DataGridGeneratedSchemaManifest manifest,
            DataGridGeneratedOperationController<TItem> operations,
            DataGridGeneratedColumnLayoutController layout,
            IDataGridGeneratedHeaderInteraction interaction = null)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _interaction = interaction;
        }

        /// <inheritdoc />
        public event EventHandler StateChanged;

        /// <summary>Gets or creates the immutable command group for a generated field.</summary>
        public DataGridGeneratedHeaderCommandSet ForField(string columnKey)
        {
            ThrowIfDisposed();
            GetField(columnKey);
            if (!_commands.TryGetValue(columnKey, out DataGridGeneratedHeaderCommandSet commands))
            {
                commands = new DataGridGeneratedHeaderCommandSet(columnKey, this);
                _commands.Add(columnKey, commands);
            }

            return commands;
        }

        /// <inheritdoc />
        public bool CanExecute(DataGridGeneratedHeaderCommandRequest request)
        {
            if (_disposed || !_manifest.TryGetField(request.ColumnKey, out _)) return false;
            return request.Kind switch
            {
                DataGridGeneratedHeaderCommandKind.SortAscending or
                DataGridGeneratedHeaderCommandKind.SortDescending => HasFeature(DataGridGeneratedFeatures.Sorting),
                DataGridGeneratedHeaderCommandKind.ClearSort =>
                    HasFeature(DataGridGeneratedFeatures.Sorting) && ContainsSort(request.ColumnKey),
                DataGridGeneratedHeaderCommandKind.ShowFilter => HasFeature(DataGridGeneratedFeatures.Filtering),
                DataGridGeneratedHeaderCommandKind.ClearFilter =>
                    HasFeature(DataGridGeneratedFeatures.Filtering) && ContainsFilter(request.ColumnKey),
                DataGridGeneratedHeaderCommandKind.ShowColumn => !_layout.IsVisible(request.ColumnKey),
                DataGridGeneratedHeaderCommandKind.HideColumn =>
                    _layout.IsVisible(request.ColumnKey) && _layout.CanSetVisible(request.ColumnKey, false),
                DataGridGeneratedHeaderCommandKind.ResetLayout => true,
                _ => _interaction?.CanExecute(request) == true
            };
        }

        /// <inheritdoc />
        public void Execute(DataGridGeneratedHeaderCommandRequest request)
        {
            ThrowIfDisposed();
            DataGridGeneratedField field = GetField(request.ColumnKey);
            if (!CanExecute(request)) return;

            switch (request.Kind)
            {
                case DataGridGeneratedHeaderCommandKind.SortAscending:
                    _operations.SortingModel.Apply(new[]
                    {
                        new SortingDescriptor(field.ColumnKey, ListSortDirection.Ascending, field.PropertyName)
                    });
                    break;
                case DataGridGeneratedHeaderCommandKind.SortDescending:
                    _operations.SortingModel.Apply(new[]
                    {
                        new SortingDescriptor(field.ColumnKey, ListSortDirection.Descending, field.PropertyName)
                    });
                    break;
                case DataGridGeneratedHeaderCommandKind.ClearSort:
                    _operations.SortingModel.Remove(field.ColumnKey);
                    break;
                case DataGridGeneratedHeaderCommandKind.ShowFilter:
                    if (_operations.FilteringModel is IFilteringModelInteraction filteringInteraction)
                    {
                        filteringInteraction.RequestShowFilterFlyout(field.ColumnKey);
                    }
                    break;
                case DataGridGeneratedHeaderCommandKind.ClearFilter:
                    _operations.FilteringModel.Remove(field.ColumnKey);
                    break;
                case DataGridGeneratedHeaderCommandKind.ShowColumn:
                    _layout.SetVisible(field.ColumnKey, true);
                    break;
                case DataGridGeneratedHeaderCommandKind.HideColumn:
                    _layout.SetVisible(field.ColumnKey, false);
                    break;
                case DataGridGeneratedHeaderCommandKind.ResetLayout:
                    _layout.Reset();
                    if (_interaction?.CanExecute(request) == true) _interaction.Execute(request);
                    break;
                default:
                    _interaction.Execute(request);
                    break;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (DataGridGeneratedHeaderCommandSet commands in _commands.Values) commands.Dispose();
            StateChanged = null;
            _commands.Clear();
        }

        private bool HasFeature(DataGridGeneratedFeatures feature) => (_operations.Features & feature) == feature;

        private bool ContainsSort(string columnKey)
        {
            IReadOnlyList<SortingDescriptor> descriptors = _operations.SortingModel.Descriptors;
            for (int index = 0; index < descriptors.Count; index++)
            {
                if (Equals(descriptors[index].ColumnId, columnKey)) return true;
            }
            return false;
        }

        private bool ContainsFilter(string columnKey)
        {
            IReadOnlyList<FilteringDescriptor> descriptors = _operations.FilteringModel.Descriptors;
            for (int index = 0; index < descriptors.Count; index++)
            {
                if (Equals(descriptors[index].ColumnId, columnKey)) return true;
            }
            return false;
        }

        private DataGridGeneratedField GetField(string columnKey)
        {
            if (columnKey == null) throw new ArgumentNullException(nameof(columnKey));
            return _manifest.TryGetField(columnKey, out DataGridGeneratedField field)
                ? field
                : throw new KeyNotFoundException("Generated field '" + columnKey + "' was not found.");
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }

    internal sealed class DataGridGeneratedHeaderCommand : ICommand, IDisposable
    {
        private readonly IDataGridGeneratedHeaderCommandHandler _handler;
        private readonly DataGridGeneratedHeaderCommandRequest _request;

        public DataGridGeneratedHeaderCommand(
            IDataGridGeneratedHeaderCommandHandler handler,
            DataGridGeneratedHeaderCommandRequest request)
        {
            _handler = handler;
            _request = request;
            _handler.StateChanged += HandlerStateChanged;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _handler.CanExecute(_request);

        public void Execute(object parameter) => _handler.Execute(_request);

        public void Dispose() => _handler.StateChanged -= HandlerStateChanged;

        private void HandlerStateChanged(object sender, EventArgs eventArgs) =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
