// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;

namespace Avalonia.Controls
{
    /// <summary>Describes generated fast-path coverage for one field.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedDiagnosticField
    {
        /// <summary>Initializes a diagnostic field descriptor.</summary>
        public DataGridGeneratedDiagnosticField(
            string columnKey,
            Type valueType,
            bool canWrite,
            bool isSearchable,
            DataGridGeneratedFilterEditorKind filterEditor,
            DataGridGeneratedAnalyticsRole analyticsRoles)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            CanWrite = canWrite;
            IsSearchable = isSearchable;
            FilterEditor = filterEditor;
            AnalyticsRoles = analyticsRoles;
        }

        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets the field value type.</summary>
        public Type ValueType { get; }
        /// <summary>Gets whether the direct accessor supports assignment.</summary>
        public bool CanWrite { get; }
        /// <summary>Gets whether generated search evaluates the field.</summary>
        public bool IsSearchable { get; }
        /// <summary>Gets the generated filter-editor profile.</summary>
        public DataGridGeneratedFilterEditorKind FilterEditor { get; }
        /// <summary>Gets combined analytics roles.</summary>
        public DataGridGeneratedAnalyticsRole AnalyticsRoles { get; }
    }

    /// <summary>Provides immutable generated schema, fast-path, fallback, and performance diagnostics.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedDiagnosticsManifest
    {
        private readonly DataGridGeneratedDiagnosticField[] _fields;
        private readonly string[] _fallbacks;
        private readonly string[] _metricNames;

        /// <summary>Initializes a generated diagnostics manifest.</summary>
        public DataGridGeneratedDiagnosticsManifest(
            string schemaId,
            string schemaHash,
            Type itemType,
            bool strict,
            bool streaming,
            DataGridGeneratedPerformanceProfile performanceProfile,
            bool hasStableKey,
            IReadOnlyList<DataGridGeneratedDiagnosticField> fields,
            IReadOnlyList<string> fallbacks = null,
            IReadOnlyList<string> metricNames = null)
        {
            SchemaId = schemaId ?? throw new ArgumentNullException(nameof(schemaId));
            SchemaHash = schemaHash ?? throw new ArgumentNullException(nameof(schemaHash));
            ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
            Strict = strict;
            Streaming = streaming;
            PerformanceProfile = performanceProfile;
            HasStableKey = hasStableKey;
            _fields = Copy(fields, nameof(fields));
            _fallbacks = fallbacks == null ? Array.Empty<string>() : Copy(fallbacks, nameof(fallbacks));
            _metricNames = metricNames == null ? Array.Empty<string>() : Copy(metricNames, nameof(metricNames));
        }

        /// <summary>Gets stable schema ID.</summary>
        public string SchemaId { get; }
        /// <summary>Gets deterministic schema hash.</summary>
        public string SchemaHash { get; }
        /// <summary>Gets item type.</summary>
        public Type ItemType { get; }
        /// <summary>Gets whether strict reflection-free generation was requested.</summary>
        public bool Strict { get; }
        /// <summary>Gets whether streaming configuration was requested.</summary>
        public bool Streaming { get; }
        /// <summary>Gets the generated performance preset.</summary>
        public DataGridGeneratedPerformanceProfile PerformanceProfile { get; }
        /// <summary>Gets whether stable identity is available.</summary>
        public bool HasStableKey { get; }
        /// <summary>Gets field-level coverage.</summary>
        public IReadOnlyList<DataGridGeneratedDiagnosticField> Fields => _fields;
        /// <summary>Gets explicitly active compatibility fallbacks.</summary>
        public IReadOnlyList<string> Fallbacks => _fallbacks;
        /// <summary>Gets renderer and generated-pipeline metric names exposed for this schema.</summary>
        public IReadOnlyList<string> MetricNames => _metricNames;
        /// <summary>Gets whether any runtime fallback is active.</summary>
        public bool HasFallbacks => _fallbacks.Length != 0;

        private static T[] Copy<T>(IReadOnlyList<T> source, string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }
}
