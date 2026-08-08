// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>Identifies a requested generated drag/drop mutation.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedDropOperation { Move, Copy, Link }

    /// <summary>Identifies the target-relative drop position.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedDropPosition { Before, After, Inside }

    /// <summary>Contains a stable-key drag/drop request without prescribing domain mutation.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedDropRequest<TKey>
    {
        /// <summary>Initializes a drop request.</summary>
        public DataGridGeneratedDropRequest(
            long revision,
            IReadOnlyList<TKey> itemKeys,
            TKey targetKey,
            DataGridGeneratedDropPosition position,
            DataGridGeneratedDropOperation operation)
        {
            Revision = revision;
            ItemKeys = itemKeys ?? throw new ArgumentNullException(nameof(itemKeys));
            TargetKey = targetKey;
            Position = position;
            Operation = operation;
        }

        /// <summary>Gets the monotonic request revision.</summary>
        public long Revision { get; }
        /// <summary>Gets dragged item keys in selection order.</summary>
        public IReadOnlyList<TKey> ItemKeys { get; }
        /// <summary>Gets the target item key.</summary>
        public TKey TargetKey { get; }
        /// <summary>Gets the target-relative position.</summary>
        public DataGridGeneratedDropPosition Position { get; }
        /// <summary>Gets the requested operation.</summary>
        public DataGridGeneratedDropOperation Operation { get; }
    }

    /// <summary>Applies a generated keyed drop request in domain-owned code.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedDropHandler<TKey>
    {
        /// <summary>Applies a validated drop request.</summary>
        ValueTask ApplyAsync(DataGridGeneratedDropRequest<TKey> request, CancellationToken cancellationToken);
    }

    /// <summary>Reports generated drag/drop session status.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedDropStatus { Idle, Validating, Rejected, Applying, Applied, Cancelled, Failed }

    /// <summary>Coordinates keyed drag/drop validation, cancellation, and domain mutation requests.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedDragDropController<TKey> : IDisposable
    {
        private readonly IDataGridGeneratedDropHandler<TKey> _handler;
        private readonly Func<DataGridGeneratedDropRequest<TKey>, CancellationToken, ValueTask<string>> _validator;
        private readonly Func<TKey, TKey, bool> _isDescendant;
        private readonly IEqualityComparer<TKey> _keyComparer;
        private CancellationTokenSource _pending;
        private bool _disposed;

        /// <summary>Initializes a drag/drop controller.</summary>
        public DataGridGeneratedDragDropController(
            IDataGridGeneratedDropHandler<TKey> handler,
            Func<DataGridGeneratedDropRequest<TKey>, CancellationToken, ValueTask<string>> validator = null,
            Func<TKey, TKey, bool> isDescendant = null,
            IEqualityComparer<TKey> keyComparer = null)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _validator = validator;
            _isDescendant = isDescendant;
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        }

        /// <summary>Raised whenever observable session state changes.</summary>
        public event EventHandler StateChanged;

        /// <summary>Gets the latest revision.</summary>
        public long Revision { get; private set; }
        /// <summary>Gets current status.</summary>
        public DataGridGeneratedDropStatus Status { get; private set; }
        /// <summary>Gets a rejection or failure message.</summary>
        public string Error { get; private set; }
        /// <summary>Gets the latest request.</summary>
        public DataGridGeneratedDropRequest<TKey> Current { get; private set; }

        /// <summary>Validates and applies a new drop, superseding any pending request.</summary>
        public async ValueTask<bool> DropAsync(
            IReadOnlyList<TKey> itemKeys,
            TKey targetKey,
            DataGridGeneratedDropPosition position,
            DataGridGeneratedDropOperation operation = DataGridGeneratedDropOperation.Move,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(itemKeys);
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken token = _pending.Token;
            long revision = ++Revision;
            TKey[] keys = CopyDistinct(itemKeys);
            Current = new DataGridGeneratedDropRequest<TKey>(revision, keys, targetKey, position, operation);
            string localError = ValidateLocal(Current);
            if (localError != null)
            {
                SetState(DataGridGeneratedDropStatus.Rejected, localError);
                return false;
            }

            try
            {
                SetState(DataGridGeneratedDropStatus.Validating, null);
                string validationError = _validator == null ? null : await _validator(Current, token).ConfigureAwait(false);
                if (revision != Revision)
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(validationError))
                {
                    SetState(DataGridGeneratedDropStatus.Rejected, validationError);
                    return false;
                }
                SetState(DataGridGeneratedDropStatus.Applying, null);
                await _handler.ApplyAsync(Current, token).ConfigureAwait(false);
                if (revision != Revision)
                {
                    return false;
                }
                SetState(DataGridGeneratedDropStatus.Applied, null);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (revision == Revision)
                {
                    SetState(DataGridGeneratedDropStatus.Cancelled, null);
                }
                return false;
            }
            catch (Exception exception)
            {
                if (revision == Revision)
                {
                    SetState(DataGridGeneratedDropStatus.Failed, exception.Message);
                }
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _pending?.Cancel();
            _pending?.Dispose();
            _disposed = true;
        }

        private TKey[] CopyDistinct(IReadOnlyList<TKey> itemKeys)
        {
            var seen = new HashSet<TKey>(_keyComparer);
            var keys = new List<TKey>(itemKeys.Count);
            for (int index = 0; index < itemKeys.Count; index++)
            {
                if (seen.Add(itemKeys[index])) keys.Add(itemKeys[index]);
            }
            return keys.ToArray();
        }

        private string ValidateLocal(DataGridGeneratedDropRequest<TKey> request)
        {
            if (request.ItemKeys.Count == 0) return "At least one dragged item key is required.";
            for (int index = 0; index < request.ItemKeys.Count; index++)
            {
                TKey key = request.ItemKeys[index];
                if (_keyComparer.Equals(key, request.TargetKey)) return "An item cannot be dropped onto itself.";
                if (request.Position == DataGridGeneratedDropPosition.Inside && _isDescendant != null && _isDescendant(key, request.TargetKey))
                    return "A hierarchical item cannot be reparented into its descendant.";
            }
            return null;
        }

        private void SetState(DataGridGeneratedDropStatus status, string error)
        {
            Status = status;
            Error = error;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
