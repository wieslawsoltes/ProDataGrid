// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>Parses a span into a typed generated field value.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    delegate bool DataGridGeneratedTryParse<TValue>(
        ReadOnlySpan<char> text,
        IFormatProvider formatProvider,
        out TValue value);

    /// <summary>Describes a generated edit result.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedEditStatus
    {
        /// <summary>The edit was applied.</summary>
        Applied,
        /// <summary>The field is read-only or its eligibility hook rejected the edit.</summary>
        NotEditable,
        /// <summary>Text conversion failed.</summary>
        ParseFailed,
        /// <summary>Validation rejected the value.</summary>
        ValidationFailed,
        /// <summary>A newer asynchronous validation superseded this result.</summary>
        Superseded,
        /// <summary>The asynchronous edit was cancelled.</summary>
        Cancelled
    }

    /// <summary>Represents a structured generated edit outcome.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedEditResult : IEquatable<DataGridGeneratedEditResult>
    {
        /// <summary>Initializes an edit outcome.</summary>
        public DataGridGeneratedEditResult(DataGridGeneratedEditStatus status, string error = null)
        {
            Status = status;
            Error = error;
        }

        /// <summary>Gets the outcome status.</summary>
        public DataGridGeneratedEditStatus Status { get; }

        /// <summary>Gets an optional validation or conversion error.</summary>
        public string Error { get; }

        /// <summary>Gets whether the edit was applied.</summary>
        public bool IsApplied => Status == DataGridGeneratedEditStatus.Applied;

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedEditResult other) =>
            Status == other.Status && string.Equals(Error, other.Error, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridGeneratedEditResult other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine((int)Status, Error);

        /// <summary>Tests two outcomes for equality.</summary>
        public static bool operator ==(DataGridGeneratedEditResult left, DataGridGeneratedEditResult right) => left.Equals(right);

        /// <summary>Tests two outcomes for inequality.</summary>
        public static bool operator !=(DataGridGeneratedEditResult left, DataGridGeneratedEditResult right) => !left.Equals(right);
    }

    /// <summary>Provides non-generic access to a heterogeneous generated edit field collection.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedEditField<TItem>
    {
        /// <summary>Gets the stable column key.</summary>
        string ColumnKey { get; }

        /// <summary>Gets the field value type.</summary>
        Type ValueType { get; }

        /// <summary>Gets whether a setter exists.</summary>
        bool CanWrite { get; }

        /// <summary>Gets the current boxed value.</summary>
        object GetValue(TItem item);

        /// <summary>Formats the current value for editing, clipboard, or export.</summary>
        string FormatValue(TItem item, IFormatProvider formatProvider);

        /// <summary>Parses, validates, coerces, and applies text.</summary>
        DataGridGeneratedEditResult TrySetText(TItem item, ReadOnlySpan<char> text, IFormatProvider formatProvider, out object oldValue, out object newValue);

        /// <summary>Validates and applies a boxed value.</summary>
        DataGridGeneratedEditResult TrySetValue(TItem item, object value, out object oldValue, out object newValue);

        /// <summary>Runs cancellable asynchronous validation without applying the value.</summary>
        ValueTask<string> ValidateAsync(TItem item, object value, CancellationToken cancellationToken);

        /// <summary>Applies an already validated value during undo/redo.</summary>
        void SetValidatedValue(TItem item, object value);
    }

    /// <summary>Provides typed parsing, formatting, validation, coercion, eligibility, and assignment for one generated field.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The field value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedEditField<TItem, TValue> : IDataGridGeneratedEditField<TItem>
    {
        private readonly Func<TItem, TValue> _getter;
        private readonly Action<TItem, TValue> _setter;
        private readonly DataGridGeneratedTryParse<TValue> _parser;
        private readonly Func<TValue, IFormatProvider, string> _formatter;
        private readonly Func<TItem, TValue, string> _validator;
        private readonly Func<TItem, TValue, CancellationToken, ValueTask<string>> _asyncValidator;
        private readonly Func<TItem, TValue, TValue> _coerce;
        private readonly Predicate<TItem> _canEdit;

        /// <summary>Initializes a generated edit field.</summary>
        public DataGridGeneratedEditField(
            string columnKey,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter,
            DataGridGeneratedTryParse<TValue> parser,
            Func<TValue, IFormatProvider, string> formatter,
            Func<TItem, TValue, string> validator = null,
            Func<TItem, TValue, CancellationToken, ValueTask<string>> asyncValidator = null,
            Func<TItem, TValue, TValue> coerce = null,
            Predicate<TItem> canEdit = null)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter;
            _parser = parser;
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _validator = validator;
            _asyncValidator = asyncValidator;
            _coerce = coerce;
            _canEdit = canEdit;
        }

        /// <inheritdoc />
        public string ColumnKey { get; }

        /// <inheritdoc />
        public Type ValueType => typeof(TValue);

        /// <inheritdoc />
        public bool CanWrite => _setter != null;

        /// <summary>Gets whether an async validation hook exists.</summary>
        public bool HasAsyncValidator => _asyncValidator != null;

        /// <summary>Gets a typed field value.</summary>
        public TValue GetTypedValue(TItem item) => _getter(item);

        /// <summary>Formats a typed field value.</summary>
        public string Format(TValue value, IFormatProvider formatProvider) => _formatter(value, formatProvider);

        /// <summary>Attempts to parse a typed field value.</summary>
        public bool TryParse(ReadOnlySpan<char> text, IFormatProvider formatProvider, out TValue value)
        {
            if (_parser == null)
            {
                value = default;
                return false;
            }
            return _parser(text, formatProvider, out value);
        }

        /// <summary>Validates and applies a typed value.</summary>
        public DataGridGeneratedEditResult TrySetValue(TItem item, TValue value, out TValue oldValue, out TValue newValue)
        {
            oldValue = _getter(item);
            newValue = value;
            if (_setter == null || (_canEdit != null && !_canEdit(item)))
            {
                return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.NotEditable);
            }
            if (_coerce != null)
            {
                newValue = _coerce(item, newValue);
            }
            string error = _validator?.Invoke(item, newValue);
            if (!string.IsNullOrEmpty(error))
            {
                return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.ValidationFailed, error);
            }
            _setter(item, newValue);
            return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.Applied);
        }

        /// <inheritdoc />
        public object GetValue(TItem item) => _getter(item);

        /// <inheritdoc />
        public string FormatValue(TItem item, IFormatProvider formatProvider) => _formatter(_getter(item), formatProvider);

        /// <inheritdoc />
        public DataGridGeneratedEditResult TrySetText(
            TItem item,
            ReadOnlySpan<char> text,
            IFormatProvider formatProvider,
            out object oldValue,
            out object newValue)
        {
            oldValue = _getter(item);
            newValue = default(TValue);
            if (_parser == null || !_parser(text, formatProvider, out TValue parsed))
            {
                return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.ParseFailed, "The value could not be parsed.");
            }
            DataGridGeneratedEditResult result = TrySetValue(item, parsed, out TValue oldTyped, out TValue newTyped);
            oldValue = oldTyped;
            newValue = newTyped;
            return result;
        }

        /// <inheritdoc />
        public DataGridGeneratedEditResult TrySetValue(TItem item, object value, out object oldValue, out object newValue)
        {
            if (value is TValue typed)
            {
                DataGridGeneratedEditResult result = TrySetValue(item, typed, out TValue oldTyped, out TValue newTyped);
                oldValue = oldTyped;
                newValue = newTyped;
                return result;
            }
            if (value == null && default(TValue) == null)
            {
                DataGridGeneratedEditResult result = TrySetValue(item, default, out TValue oldTyped, out TValue newTyped);
                oldValue = oldTyped;
                newValue = newTyped;
                return result;
            }
            oldValue = _getter(item);
            newValue = value;
            return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.ParseFailed, "The value has an incompatible type.");
        }

        /// <inheritdoc />
        public ValueTask<string> ValidateAsync(TItem item, object value, CancellationToken cancellationToken)
        {
            if (_asyncValidator == null)
            {
                return ValueTask.FromResult<string>(null);
            }
            if (value is TValue typed)
            {
                return _asyncValidator(item, typed, cancellationToken);
            }
            if (value == null && default(TValue) == null)
            {
                return _asyncValidator(item, default, cancellationToken);
            }
            return ValueTask.FromResult("The value has an incompatible type.");
        }

        /// <inheritdoc />
        public void SetValidatedValue(TItem item, object value) => _setter(item, (TValue)value);
    }

    /// <summary>Reports an edit, undo, or redo operation.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedEditChangedEventArgs : EventArgs
    {
        internal DataGridGeneratedEditChangedEventArgs(string columnKey, bool isUndo, bool isRedo, long version)
        {
            ColumnKey = columnKey;
            IsUndo = isUndo;
            IsRedo = isRedo;
            Version = version;
        }

        /// <summary>Gets the affected column key, or an empty string for a multi-column batch.</summary>
        public string ColumnKey { get; }

        /// <summary>Gets whether this change is an undo.</summary>
        public bool IsUndo { get; }

        /// <summary>Gets whether this change is a redo.</summary>
        public bool IsRedo { get; }

        /// <summary>Gets the monotonic edit version.</summary>
        public long Version { get; }
    }

    /// <summary>Coordinates generated fields, cancellable async validation, and keyed undo/redo batches.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedEditController<TItem, TKey> : IDisposable
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly Func<TKey, TItem> _itemResolver;
        private readonly Dictionary<string, IDataGridGeneratedEditField<TItem>> _fields;
        private readonly Stack<List<EditRecord>> _undo = new();
        private readonly Stack<List<EditRecord>> _redo = new();
        private readonly Dictionary<ValidationKey, ValidationState> _validations;
        private List<EditRecord> _batch;
        private bool _disposed;

        /// <summary>Initializes a generated edit controller.</summary>
        public DataGridGeneratedEditController(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            IReadOnlyList<IDataGridGeneratedEditField<TItem>> fields,
            Func<TKey, TItem> itemResolver = null,
            IEqualityComparer<TKey> keyComparer = null)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _itemResolver = itemResolver;
            _fields = new Dictionary<string, IDataGridGeneratedEditField<TItem>>(fields?.Count ?? 0, StringComparer.Ordinal);
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }
            for (int index = 0; index < fields.Count; index++)
            {
                IDataGridGeneratedEditField<TItem> field = fields[index] ??
                    throw new ArgumentException("Edit fields cannot contain null entries.", nameof(fields));
                if (!_fields.TryAdd(field.ColumnKey, field))
                {
                    throw new ArgumentException("Duplicate generated edit field '" + field.ColumnKey + "'.", nameof(fields));
                }
            }
            _validations = new Dictionary<ValidationKey, ValidationState>(
                new ValidationKeyComparer(keyComparer ?? EqualityComparer<TKey>.Default));
        }

        /// <summary>Raised after an edit, undo, or redo is applied.</summary>
        public event EventHandler<DataGridGeneratedEditChangedEventArgs> Changed;

        /// <summary>Gets registered edit fields.</summary>
        public IReadOnlyDictionary<string, IDataGridGeneratedEditField<TItem>> Fields => _fields;

        /// <summary>Gets whether an undo batch is available.</summary>
        public bool CanUndo => _undo.Count != 0;

        /// <summary>Gets whether a redo batch is available.</summary>
        public bool CanRedo => _redo.Count != 0;

        /// <summary>Gets the monotonic edit version.</summary>
        public long Version { get; private set; }

        /// <summary>Begins an explicit multi-cell edit batch.</summary>
        public void BeginBatch()
        {
            ThrowIfDisposed();
            if (_batch != null)
            {
                throw new InvalidOperationException("A generated edit batch is already active.");
            }
            _batch = new List<EditRecord>();
        }

        /// <summary>Commits the active batch as one undo unit.</summary>
        public void CommitBatch()
        {
            ThrowIfDisposed();
            if (_batch == null)
            {
                throw new InvalidOperationException("No generated edit batch is active.");
            }
            if (_batch.Count != 0)
            {
                _undo.Push(_batch);
                _redo.Clear();
                Publish(string.Empty, false, false);
            }
            _batch = null;
        }

        /// <summary>Rolls the active batch back in reverse order.</summary>
        public void RollbackBatch()
        {
            ThrowIfDisposed();
            if (_batch == null)
            {
                throw new InvalidOperationException("No generated edit batch is active.");
            }
            ApplyRecords(_batch, useNewValue: false, reverse: true);
            _batch = null;
        }

        /// <summary>Parses and applies text through a generated field.</summary>
        public DataGridGeneratedEditResult TrySetText(
            TItem item,
            string columnKey,
            ReadOnlySpan<char> text,
            IFormatProvider formatProvider = null)
        {
            ThrowIfDisposed();
            IDataGridGeneratedEditField<TItem> field = GetField(columnKey);
            DataGridGeneratedEditResult result = field.TrySetText(
                item,
                text,
                formatProvider ?? System.Globalization.CultureInfo.CurrentCulture,
                out object oldValue,
                out object newValue);
            if (result.IsApplied)
            {
                Record(item, field, oldValue, newValue);
            }
            return result;
        }

        /// <summary>Validates and applies a typed or boxed value through a generated field.</summary>
        public DataGridGeneratedEditResult TrySetValue(TItem item, string columnKey, object value)
        {
            ThrowIfDisposed();
            IDataGridGeneratedEditField<TItem> field = GetField(columnKey);
            DataGridGeneratedEditResult result = field.TrySetValue(item, value, out object oldValue, out object newValue);
            if (result.IsApplied)
            {
                Record(item, field, oldValue, newValue);
            }
            return result;
        }

        /// <summary>Runs revisioned async validation and applies the value only when the latest request succeeds.</summary>
        public async ValueTask<DataGridGeneratedEditResult> TrySetValueAsync(
            TItem item,
            string columnKey,
            object value,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            IDataGridGeneratedEditField<TItem> field = GetField(columnKey);
            TKey itemKey = _keyAccessor.GetKey(item);
            var validationKey = new ValidationKey(itemKey, columnKey);
            ValidationState state;
            lock (_validations)
            {
                if (_validations.TryGetValue(validationKey, out ValidationState previous))
                {
                    previous.Cancellation.Cancel();
                    state = new ValidationState(previous.Revision + 1, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
                }
                else
                {
                    state = new ValidationState(1, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
                }
                _validations[validationKey] = state;
            }

            try
            {
                string error = await field.ValidateAsync(item, value, state.Cancellation.Token).ConfigureAwait(false);
                lock (_validations)
                {
                    if (!_validations.TryGetValue(validationKey, out ValidationState current) || current.Revision != state.Revision)
                    {
                        return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.Superseded);
                    }
                    _validations.Remove(validationKey);
                }
                if (!string.IsNullOrEmpty(error))
                {
                    return new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.ValidationFailed, error);
                }
                return TrySetValue(item, columnKey, value);
            }
            catch (OperationCanceledException) when (state.Cancellation.IsCancellationRequested)
            {
                lock (_validations)
                {
                    if (_validations.TryGetValue(validationKey, out ValidationState current) && current.Revision == state.Revision)
                    {
                        _validations.Remove(validationKey);
                    }
                }
                return cancellationToken.IsCancellationRequested
                    ? new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.Cancelled)
                    : new DataGridGeneratedEditResult(DataGridGeneratedEditStatus.Superseded);
            }
            finally
            {
                state.Cancellation.Dispose();
            }
        }

        /// <summary>Undoes the most recently committed edit batch.</summary>
        public bool Undo()
        {
            ThrowIfDisposed();
            if (_batch != null)
            {
                throw new InvalidOperationException("Commit or roll back the active batch before undo.");
            }
            if (!_undo.TryPop(out List<EditRecord> records))
            {
                return false;
            }
            ApplyRecords(records, useNewValue: false, reverse: true);
            _redo.Push(records);
            Publish(records.Count == 1 ? records[0].Field.ColumnKey : string.Empty, true, false);
            return true;
        }

        /// <summary>Redoes the most recently undone edit batch.</summary>
        public bool Redo()
        {
            ThrowIfDisposed();
            if (_batch != null)
            {
                throw new InvalidOperationException("Commit or roll back the active batch before redo.");
            }
            if (!_redo.TryPop(out List<EditRecord> records))
            {
                return false;
            }
            ApplyRecords(records, useNewValue: true, reverse: false);
            _undo.Push(records);
            Publish(records.Count == 1 ? records[0].Field.ColumnKey : string.Empty, false, true);
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            lock (_validations)
            {
                foreach (ValidationState state in _validations.Values)
                {
                    state.Cancellation.Cancel();
                }
                _validations.Clear();
            }
            _disposed = true;
        }

        private IDataGridGeneratedEditField<TItem> GetField(string columnKey)
        {
            if (columnKey == null)
            {
                throw new ArgumentNullException(nameof(columnKey));
            }
            return _fields.TryGetValue(columnKey, out IDataGridGeneratedEditField<TItem> field)
                ? field
                : throw new KeyNotFoundException("Generated edit field '" + columnKey + "' was not found.");
        }

        private void Record(TItem item, IDataGridGeneratedEditField<TItem> field, object oldValue, object newValue)
        {
            var record = new EditRecord(_keyAccessor.GetKey(item), item, field, oldValue, newValue);
            if (_batch != null)
            {
                _batch.Add(record);
                return;
            }
            _undo.Push(new List<EditRecord>(1) { record });
            _redo.Clear();
            Publish(field.ColumnKey, false, false);
        }

        private void ApplyRecords(List<EditRecord> records, bool useNewValue, bool reverse)
        {
            for (int offset = 0; offset < records.Count; offset++)
            {
                int index = reverse ? records.Count - 1 - offset : offset;
                EditRecord record = records[index];
                TItem item = _itemResolver != null ? _itemResolver(record.ItemKey) : record.Item;
                if (item == null)
                {
                    throw new InvalidOperationException("Generated edit undo could not resolve item key '" + record.ItemKey + "'.");
                }
                record.Field.SetValidatedValue(item, useNewValue ? record.NewValue : record.OldValue);
            }
        }

        private void Publish(string columnKey, bool isUndo, bool isRedo)
        {
            Version++;
            Changed?.Invoke(this, new DataGridGeneratedEditChangedEventArgs(columnKey, isUndo, isRedo, Version));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private sealed class EditRecord
        {
            public EditRecord(TKey itemKey, TItem item, IDataGridGeneratedEditField<TItem> field, object oldValue, object newValue)
            {
                ItemKey = itemKey;
                Item = item;
                Field = field;
                OldValue = oldValue;
                NewValue = newValue;
            }

            public TKey ItemKey { get; }
            public TItem Item { get; }
            public IDataGridGeneratedEditField<TItem> Field { get; }
            public object OldValue { get; }
            public object NewValue { get; }
        }

        private readonly struct ValidationKey
        {
            public ValidationKey(TKey itemKey, string columnKey)
            {
                ItemKey = itemKey;
                ColumnKey = columnKey;
            }

            public TKey ItemKey { get; }
            public string ColumnKey { get; }
        }

        private readonly struct ValidationState
        {
            public ValidationState(long revision, CancellationTokenSource cancellation)
            {
                Revision = revision;
                Cancellation = cancellation;
            }

            public long Revision { get; }
            public CancellationTokenSource Cancellation { get; }
        }

        private sealed class ValidationKeyComparer : IEqualityComparer<ValidationKey>
        {
            private readonly IEqualityComparer<TKey> _keyComparer;

            public ValidationKeyComparer(IEqualityComparer<TKey> keyComparer) => _keyComparer = keyComparer;

            public bool Equals(ValidationKey left, ValidationKey right) =>
                _keyComparer.Equals(left.ItemKey, right.ItemKey) &&
                string.Equals(left.ColumnKey, right.ColumnKey, StringComparison.Ordinal);

            public int GetHashCode(ValidationKey value) =>
                HashCode.Combine(_keyComparer.GetHashCode(value.ItemKey), StringComparer.Ordinal.GetHashCode(value.ColumnKey));
        }
    }
}
