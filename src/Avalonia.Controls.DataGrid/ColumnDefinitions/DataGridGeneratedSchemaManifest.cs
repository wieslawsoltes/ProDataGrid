// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls
{
    /// <summary>Identifies the standard filter editor suggested by generated field metadata.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedFilterEditorKind
    {
        /// <summary>Infer the editor from the value type.</summary>
        Auto,
        /// <summary>Text editor.</summary>
        Text,
        /// <summary>Numeric editor.</summary>
        Numeric,
        /// <summary>Date/time editor.</summary>
        DateTime,
        /// <summary>Boolean editor.</summary>
        Boolean,
        /// <summary>Enumeration editor.</summary>
        Enum,
        /// <summary>Minimum/maximum range editor.</summary>
        Range,
        /// <summary>Bounded distinct-values editor.</summary>
        Distinct,
        /// <summary>User-supplied editor resource or factory.</summary>
        Custom
    }

    /// <summary>Contains canonical export, remote, localization, editor, and accessibility metadata for a generated field.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedFieldMetadata
    {
        /// <summary>Initializes generated field metadata.</summary>
        public DataGridGeneratedFieldMetadata(
            string exportFormat = null,
            string exportNullText = null,
            string backendFieldName = null,
            DataGridGeneratedFilterEditorKind filterEditor = DataGridGeneratedFilterEditorKind.Auto,
            string filterEditorResourceKey = null,
            string headerResourceKey = null,
            string descriptionResourceKey = null,
            string automationId = null,
            string automationName = null,
            string automationHelpText = null,
            bool isSensitive = false,
            string header = null,
            string description = null,
            Func<IFormatProvider, string> headerProvider = null,
            Func<IFormatProvider, string> descriptionProvider = null)
        {
            ExportFormat = exportFormat;
            ExportNullText = exportNullText;
            BackendFieldName = backendFieldName;
            FilterEditor = filterEditor;
            FilterEditorResourceKey = filterEditorResourceKey;
            HeaderResourceKey = headerResourceKey;
            DescriptionResourceKey = descriptionResourceKey;
            AutomationId = automationId;
            AutomationName = automationName;
            AutomationHelpText = automationHelpText;
            IsSensitive = isSensitive;
            Header = header;
            Description = description;
            HeaderProvider = headerProvider;
            DescriptionProvider = descriptionProvider;
        }

        /// <summary>Gets the culture-aware export format.</summary>
        public string ExportFormat { get; }
        /// <summary>Gets text used when exporting null.</summary>
        public string ExportNullText { get; }
        /// <summary>Gets the optional server/backend field name.</summary>
        public string BackendFieldName { get; }
        /// <summary>Gets the suggested filter editor.</summary>
        public DataGridGeneratedFilterEditorKind FilterEditor { get; }
        /// <summary>Gets an optional custom filter editor resource key.</summary>
        public string FilterEditorResourceKey { get; }
        /// <summary>Gets an optional localized header resource key.</summary>
        public string HeaderResourceKey { get; }
        /// <summary>Gets an optional localized description resource key.</summary>
        public string DescriptionResourceKey { get; }
        /// <summary>Gets the stable automation identifier.</summary>
        public string AutomationId { get; }
        /// <summary>Gets an explicit automation name.</summary>
        public string AutomationName { get; }
        /// <summary>Gets explicit automation help text.</summary>
        public string AutomationHelpText { get; }
        /// <summary>Gets whether generic export/clipboard UI should hide the value by default.</summary>
        public bool IsSensitive { get; }
        /// <summary>Gets the invariant/fallback header text.</summary>
        public string Header { get; }
        /// <summary>Gets the invariant/fallback description text.</summary>
        public string Description { get; }
        /// <summary>Gets an optional strongly typed localized header provider.</summary>
        public Func<IFormatProvider, string> HeaderProvider { get; }
        /// <summary>Gets an optional strongly typed localized description provider.</summary>
        public Func<IFormatProvider, string> DescriptionProvider { get; }
        /// <summary>Resolves header text without resource lookup or reflection.</summary>
        public string ResolveHeader(IFormatProvider formatProvider = null) =>
            HeaderProvider?.Invoke(formatProvider ?? CultureInfo.CurrentUICulture) ?? Header ?? AutomationName ?? string.Empty;
        /// <summary>Resolves description text without resource lookup or reflection.</summary>
        public string ResolveDescription(IFormatProvider formatProvider = null) =>
            DescriptionProvider?.Invoke(formatProvider ?? CultureInfo.CurrentUICulture) ?? Description ?? AutomationHelpText ?? string.Empty;
    }

    /// <summary>
    /// Exposes the canonical, versioned manifest associated with a generated schema.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedSchemaManifestProvider
    {
        /// <summary>
        /// Gets the immutable schema manifest.
        /// </summary>
        DataGridGeneratedSchemaManifest Manifest { get; }
    }

    /// <summary>
    /// Gets a stable, strongly typed item key without reflection or boxing.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridItemKey<in TItem, out TKey>
    {
        /// <summary>
        /// Gets the stable key for an item.
        /// </summary>
        TKey GetKey(TItem item);
    }

    /// <summary>
    /// Describes one field in a generated schema using stable identifiers and its compiled accessor.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridGeneratedField
    {
        /// <summary>
        /// Initializes a generated field descriptor.
        /// </summary>
        public DataGridGeneratedField(
            int ordinal,
            string columnKey,
            string propertyName,
            Type valueType,
            IDataGridColumnValueAccessor accessor,
            bool isSearchable,
            DataGridGeneratedFieldMetadata metadata = null)
        {
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            Ordinal = ordinal;
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            Accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            IsSearchable = isSearchable;
            Metadata = metadata ?? new DataGridGeneratedFieldMetadata(automationId: columnKey);
        }

        /// <summary>
        /// Gets the deterministic zero-based field ordinal.
        /// </summary>
        public int Ordinal { get; }

        /// <summary>
        /// Gets the stable column key.
        /// </summary>
        public string ColumnKey { get; }

        /// <summary>
        /// Gets the source property name used as a compatibility descriptor alias.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets the runtime value type for diagnostics and adapter boundaries.
        /// </summary>
        public Type ValueType { get; }

        /// <summary>
        /// Gets the reflection-free value accessor.
        /// </summary>
        public IDataGridColumnValueAccessor Accessor { get; }

        /// <summary>
        /// Gets a value indicating whether global search includes this field.
        /// </summary>
        public bool IsSearchable { get; }

        /// <summary>Gets cross-feature metadata shared by generated adapters.</summary>
        public DataGridGeneratedFieldMetadata Metadata { get; }
    }

    /// <summary>
    /// Describes a generated field with its item/value types and creates type-safe operation descriptors.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The field value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridGeneratedField<TItem, TValue> : DataGridGeneratedField
    {
        /// <summary>
        /// Initializes a typed generated field descriptor.
        /// </summary>
        public DataGridGeneratedField(
            int ordinal,
            string columnKey,
            string propertyName,
            DataGridColumnValueAccessor<TItem, TValue> accessor,
            bool isSearchable,
            DataGridGeneratedFieldMetadata metadata = null)
            : base(ordinal, columnKey, propertyName, typeof(TValue), accessor, isSearchable, metadata)
        {
            TypedAccessor = accessor;
        }

        /// <summary>
        /// Gets the strongly typed reflection-free accessor.
        /// </summary>
        public DataGridColumnValueAccessor<TItem, TValue> TypedAccessor { get; }

        /// <summary>
        /// Creates an ascending sort descriptor for this field.
        /// </summary>
        public SortingDescriptor Ascending(IComparer comparer = null, CultureInfo culture = null) =>
            new SortingDescriptor(ColumnKey, ListSortDirection.Ascending, PropertyName, comparer, culture);

        /// <summary>
        /// Creates a descending sort descriptor for this field.
        /// </summary>
        public SortingDescriptor Descending(IComparer comparer = null, CultureInfo culture = null) =>
            new SortingDescriptor(ColumnKey, ListSortDirection.Descending, PropertyName, comparer, culture);

        /// <summary>
        /// Creates an equality filter descriptor with a type-checked operand.
        /// </summary>
        public FilteringDescriptor EqualTo(TValue value, CultureInfo culture = null) =>
            CreateFilter(FilteringOperator.Equals, value, culture);

        /// <summary>
        /// Creates an inequality filter descriptor with a type-checked operand.
        /// </summary>
        public FilteringDescriptor NotEqualTo(TValue value, CultureInfo culture = null) =>
            CreateFilter(FilteringOperator.NotEquals, value, culture);

        /// <summary>
        /// Creates an inclusion filter descriptor with type-checked operands.
        /// </summary>
        public FilteringDescriptor In(params TValue[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            object[] boxed = new object[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                boxed[index] = values[index];
            }

            return new FilteringDescriptor(ColumnKey, FilteringOperator.In, PropertyName, values: boxed);
        }

        /// <summary>
        /// Creates a field-scoped search descriptor.
        /// </summary>
        public SearchDescriptor Search(
            string query,
            SearchMatchMode matchMode = SearchMatchMode.Contains,
            StringComparison? comparison = null,
            CultureInfo culture = null) =>
            new SearchDescriptor(
                query,
                matchMode,
                scope: SearchScope.ExplicitColumns,
                columnIds: new object[] { ColumnKey },
                comparison: comparison,
                culture: culture);

        /// <summary>
        /// Creates a filter descriptor for this field.
        /// </summary>
        protected FilteringDescriptor CreateFilter(
            FilteringOperator @operator,
            object value,
            CultureInfo culture = null,
            StringComparison? comparison = null) =>
            new FilteringDescriptor(ColumnKey, @operator, PropertyName, value, culture: culture, stringComparison: comparison);
    }

    /// <summary>
    /// Adds ordered comparison operations to a generated field.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedComparableField<TItem, TValue> : DataGridGeneratedField<TItem, TValue>
    {
        /// <summary>
        /// Initializes a typed comparable field descriptor.
        /// </summary>
        public DataGridGeneratedComparableField(
            int ordinal,
            string columnKey,
            string propertyName,
            DataGridColumnValueAccessor<TItem, TValue> accessor,
            bool isSearchable,
            DataGridGeneratedFieldMetadata metadata = null)
            : base(ordinal, columnKey, propertyName, accessor, isSearchable, metadata)
        {
        }

        /// <summary>Creates a greater-than filter.</summary>
        public FilteringDescriptor GreaterThan(TValue value, CultureInfo culture = null) =>
            CreateFilter(FilteringOperator.GreaterThan, value, culture);

        /// <summary>Creates a greater-than-or-equal filter.</summary>
        public FilteringDescriptor GreaterThanOrEqual(TValue value, CultureInfo culture = null) =>
            CreateFilter(FilteringOperator.GreaterThanOrEqual, value, culture);

        /// <summary>Creates a less-than filter.</summary>
        public FilteringDescriptor LessThan(TValue value, CultureInfo culture = null) =>
            CreateFilter(FilteringOperator.LessThan, value, culture);

        /// <summary>Creates a less-than-or-equal filter.</summary>
        public FilteringDescriptor LessThanOrEqual(TValue value, CultureInfo culture = null) =>
            CreateFilter(FilteringOperator.LessThanOrEqual, value, culture);

        /// <summary>Creates an inclusive between filter.</summary>
        public FilteringDescriptor Between(TValue minimum, TValue maximum, CultureInfo culture = null) =>
            new FilteringDescriptor(
                ColumnKey,
                FilteringOperator.Between,
                PropertyName,
                values: new object[] { minimum, maximum },
                culture: culture);
    }

    /// <summary>
    /// Adds string-specific filter operations to a generated field.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The string field type, including its nullability annotation.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedStringField<TItem, TValue> : DataGridGeneratedField<TItem, TValue>
    {
        /// <summary>
        /// Initializes a typed string field descriptor.
        /// </summary>
        public DataGridGeneratedStringField(
            int ordinal,
            string columnKey,
            string propertyName,
            DataGridColumnValueAccessor<TItem, TValue> accessor,
            bool isSearchable,
            DataGridGeneratedFieldMetadata metadata = null)
            : base(ordinal, columnKey, propertyName, accessor, isSearchable, metadata)
        {
        }

        /// <summary>Creates a contains filter.</summary>
        public FilteringDescriptor Contains(string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase) =>
            CreateFilter(FilteringOperator.Contains, value, comparison: comparison);

        /// <summary>Creates a starts-with filter.</summary>
        public FilteringDescriptor StartsWith(string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase) =>
            CreateFilter(FilteringOperator.StartsWith, value, comparison: comparison);

        /// <summary>Creates an ends-with filter.</summary>
        public FilteringDescriptor EndsWith(string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase) =>
            CreateFilter(FilteringOperator.EndsWith, value, comparison: comparison);
    }

    /// <summary>
    /// Provides stable schema identity and the canonical generated field collection.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSchemaManifest
    {
        private readonly DataGridGeneratedField[] _fields;

        /// <summary>
        /// Initializes an immutable generated schema manifest.
        /// </summary>
        public DataGridGeneratedSchemaManifest(
            int formatVersion,
            string schemaId,
            string schemaHash,
            Type itemType,
            IReadOnlyList<DataGridGeneratedField> fields,
            string keyMemberName = null,
            Type keyType = null)
        {
            if (formatVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(formatVersion));
            }

            FormatVersion = formatVersion;
            SchemaId = schemaId ?? throw new ArgumentNullException(nameof(schemaId));
            SchemaHash = schemaHash ?? throw new ArgumentNullException(nameof(schemaHash));
            ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
            KeyMemberName = keyMemberName;
            KeyType = keyType;

            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            _fields = new DataGridGeneratedField[fields.Count];
            for (int index = 0; index < fields.Count; index++)
            {
                DataGridGeneratedField field = fields[index];
                _fields[index] = field ?? throw new ArgumentException("Fields cannot contain null entries.", nameof(fields));
                if (field.Ordinal != index)
                {
                    throw new ArgumentException("Field ordinals must be contiguous and match their manifest position.", nameof(fields));
                }
            }

            if ((KeyMemberName == null) != (KeyType == null))
            {
                throw new ArgumentException("Key member name and key type must either both be supplied or both be omitted.");
            }
        }

        /// <summary>
        /// Gets the manifest format version.
        /// </summary>
        public int FormatVersion { get; }

        /// <summary>
        /// Gets the stable schema identifier used by persistence and cross-assembly registries.
        /// </summary>
        public string SchemaId { get; }

        /// <summary>
        /// Gets the deterministic hash of the schema's compile-time shape.
        /// </summary>
        public string SchemaHash { get; }

        /// <summary>
        /// Gets the row item type.
        /// </summary>
        public Type ItemType { get; }

        /// <summary>
        /// Gets the generated fields in stable ordinal order.
        /// </summary>
        public IReadOnlyList<DataGridGeneratedField> Fields => _fields;

        /// <summary>
        /// Gets the key member name, or <see langword="null"/> when the schema has no key.
        /// </summary>
        public string KeyMemberName { get; }

        /// <summary>
        /// Gets the key type, or <see langword="null"/> when the schema has no key.
        /// </summary>
        public Type KeyType { get; }

        /// <summary>
        /// Gets a value indicating whether the schema defines stable item identity.
        /// </summary>
        public bool HasKey => KeyType != null;

        /// <summary>
        /// Resolves a generated field by stable column key or compatibility property name.
        /// </summary>
        public bool TryGetField(string key, out DataGridGeneratedField field)
        {
            if (key != null)
            {
                for (int index = 0; index < _fields.Length; index++)
                {
                    DataGridGeneratedField candidate = _fields[index];
                    if (string.Equals(candidate.ColumnKey, key, StringComparison.Ordinal) ||
                        string.Equals(candidate.PropertyName, key, StringComparison.Ordinal))
                    {
                        field = candidate;
                        return true;
                    }
                }
            }

            field = null;
            return false;
        }
    }
}
