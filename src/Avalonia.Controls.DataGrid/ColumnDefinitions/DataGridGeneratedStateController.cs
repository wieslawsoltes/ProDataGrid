// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.Controls
{
    /// <summary>Migrates a generated persisted state between schema versions.</summary>
    /// <param name="fromVersion">The payload schema version.</param>
    /// <param name="toVersion">The current schema version.</param>
    /// <param name="state">The mutable persisted state, which may be replaced.</param>
    /// <returns><see langword="true"/> when migration succeeded.</returns>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    delegate bool DataGridGeneratedStateMigration(
        int fromVersion,
        int toVersion,
        ref DataGridPersistedState state);

    /// <summary>Describes stable generated state identity, supported sections, and column-key aliases.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedStateDescriptor
    {
        private readonly Dictionary<string, string> _columnAliases;

        /// <summary>Initializes generated state metadata.</summary>
        public DataGridGeneratedStateDescriptor(
            string schemaId,
            string schemaHash,
            int version,
            DataGridStateSections sections = DataGridStateSections.All,
            IReadOnlyDictionary<string, string> columnAliases = null)
        {
            if (string.IsNullOrWhiteSpace(schemaId))
            {
                throw new ArgumentException("Schema ID cannot be empty.", nameof(schemaId));
            }
            if (string.IsNullOrWhiteSpace(schemaHash))
            {
                throw new ArgumentException("Schema hash cannot be empty.", nameof(schemaHash));
            }
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            SchemaId = schemaId;
            SchemaHash = schemaHash;
            Version = version;
            Sections = sections;
            _columnAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            if (columnAliases != null)
            {
                foreach (KeyValuePair<string, string> alias in columnAliases)
                {
                    if (string.IsNullOrWhiteSpace(alias.Key) || string.IsNullOrWhiteSpace(alias.Value))
                    {
                        throw new ArgumentException("Column aliases cannot contain empty keys or values.", nameof(columnAliases));
                    }
                    if (!_columnAliases.TryAdd(alias.Key, alias.Value))
                    {
                        throw new ArgumentException("Duplicate generated column alias '" + alias.Key + "'.", nameof(columnAliases));
                    }
                }
            }
        }

        /// <summary>Gets the stable schema ID.</summary>
        public string SchemaId { get; }

        /// <summary>Gets the deterministic schema hash.</summary>
        public string SchemaHash { get; }

        /// <summary>Gets the user-controlled state version.</summary>
        public int Version { get; }

        /// <summary>Gets sections supported by the generated adapter.</summary>
        public DataGridStateSections Sections { get; }

        /// <summary>Gets old-to-current column-key mappings.</summary>
        public IReadOnlyDictionary<string, string> ColumnAliases => _columnAliases;
    }

    /// <summary>Wraps persisted DataGrid state with generated schema identity.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedStateEnvelope
    {
        /// <summary>Gets or sets the stable schema ID.</summary>
        public string SchemaId { get; set; }

        /// <summary>Gets or sets the deterministic schema hash captured with the state.</summary>
        public string SchemaHash { get; set; }

        /// <summary>Gets or sets the generated schema version.</summary>
        public int SchemaVersion { get; set; }

        /// <summary>Gets or sets the persisted DataGrid state.</summary>
        public DataGridPersistedState State { get; set; }
    }

    /// <summary>
    /// Captures, validates, aliases, migrates, serializes, and restores state for one generated schema.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedStateController
    {
        private readonly DataGridGeneratedStateMigration _migration;
#if DATAGRID_INTERNAL
        private static readonly JsonSerializerOptions s_envelopeSerializerOptions = CreateEnvelopeSerializerOptions();
#endif

        /// <summary>Initializes a generated state controller.</summary>
        public DataGridGeneratedStateController(
            DataGridGeneratedStateDescriptor descriptor,
            DataGridStateOptions stateOptions,
            DataGridGeneratedStateMigration migration = null,
            IDataGridStateSerializer stateSerializer = null,
            DataGridStatePersistenceOptions persistenceOptions = null)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            StateOptions = stateOptions ?? throw new ArgumentNullException(nameof(stateOptions));
            _migration = migration;
            StateSerializer = stateSerializer;
            PersistenceOptions = persistenceOptions;
        }

        /// <summary>Gets the generated schema metadata.</summary>
        public DataGridGeneratedStateDescriptor Descriptor { get; }

        /// <summary>Gets the generated item/column key options.</summary>
        public DataGridStateOptions StateOptions { get; }

        /// <summary>Gets the optional custom state serializer.</summary>
        public IDataGridStateSerializer StateSerializer { get; }

        /// <summary>Gets optional persistence token behavior.</summary>
        public DataGridStatePersistenceOptions PersistenceOptions { get; }

        /// <summary>Captures selected sections and adds generated schema identity.</summary>
        public DataGridGeneratedStateEnvelope Capture(
            DataGrid grid,
            DataGridStateSections sections = DataGridStateSections.All)
        {
            DataGridStateSections effectiveSections = sections & Descriptor.Sections;
            DataGridPersistedState state = DataGridStatePersistence.CaptureState(
                grid ?? throw new ArgumentNullException(nameof(grid)),
                effectiveSections,
                StateOptions,
                PersistenceOptions);
            state.Version = Descriptor.Version;
            return new DataGridGeneratedStateEnvelope
            {
                SchemaId = Descriptor.SchemaId,
                SchemaHash = Descriptor.SchemaHash,
                SchemaVersion = Descriptor.Version,
                State = state
            };
        }

        /// <summary>Validates, migrates, and restores selected sections.</summary>
        public void Restore(
            DataGrid grid,
            DataGridGeneratedStateEnvelope envelope,
            DataGridStateSections sections = DataGridStateSections.All)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            DataGridPersistedState state = Prepare(envelope);
            DataGridStateSections effectiveSections = sections & Descriptor.Sections;
            if ((effectiveSections & DataGridStateSections.Selection) != 0 &&
                grid.Selection is global::Avalonia.Controls.DataGridSelection.IdentitySelectionModel identitySelection)
            {
                identitySelection.SupersedePendingIdentityRestore();
            }
            DataGridStatePersistence.RestoreState(
                grid,
                state,
                effectiveSections,
                StateOptions,
                PersistenceOptions);
            if ((effectiveSections & DataGridStateSections.Selection) != 0 &&
                grid.Selection is global::Avalonia.Controls.DataGridSelection.IdentitySelectionModel restoredIdentitySelection)
            {
                restoredIdentitySelection.SupersedePendingIdentityRestore();
            }
        }

        /// <summary>Validates and migrates an envelope without accessing a DataGrid.</summary>
        public DataGridPersistedState Prepare(DataGridGeneratedStateEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }
            if (!string.Equals(envelope.SchemaId, Descriptor.SchemaId, StringComparison.Ordinal))
            {
                throw new DataGridStatePersistenceException(
                    "Generated state schema ID '" + envelope.SchemaId + "' does not match '" + Descriptor.SchemaId + "'.");
            }
            if (envelope.State == null)
            {
                throw new DataGridStatePersistenceException("Generated state envelope has no state payload.");
            }
            if (envelope.SchemaVersion <= 0 || envelope.SchemaVersion > Descriptor.Version)
            {
                throw new DataGridStatePersistenceException(
                    "Generated state version '" + envelope.SchemaVersion + "' cannot be restored by version '" + Descriptor.Version + "'.");
            }

            DataGridPersistedState state = envelope.State;
            ApplyColumnAliases(state, Descriptor.ColumnAliases);
            bool versionChanged = envelope.SchemaVersion != Descriptor.Version;
            if (versionChanged)
            {
                if (_migration == null || !_migration(envelope.SchemaVersion, Descriptor.Version, ref state) || state == null)
                {
                    throw new DataGridStatePersistenceException(
                        "Generated state migration from version '" + envelope.SchemaVersion + "' to '" + Descriptor.Version + "' failed.");
                }
            }
            else if (!string.Equals(envelope.SchemaHash, Descriptor.SchemaHash, StringComparison.Ordinal) &&
                     Descriptor.ColumnAliases.Count == 0)
            {
                throw new DataGridStatePersistenceException(
                    "Generated state schema hash changed without a version migration or column alias.");
            }

            state.Version = Descriptor.Version;
            state.Sections &= Descriptor.Sections;
            return state;
        }

        /// <summary>Serializes a generated envelope with source-generated JSON metadata.</summary>
        public string SerializeToString(DataGridGeneratedStateEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }
#if DATAGRID_INTERNAL
            return JsonSerializer.Serialize(envelope, s_envelopeSerializerOptions);
#else
            return JsonSerializer.Serialize(
                envelope,
                DataGridPersistedStateJsonContext.Default.DataGridGeneratedStateEnvelope);
#endif
        }

        /// <summary>Deserializes a generated envelope with source-generated JSON metadata.</summary>
        public DataGridGeneratedStateEnvelope Deserialize(string payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
#if DATAGRID_INTERNAL
            return JsonSerializer.Deserialize<DataGridGeneratedStateEnvelope>(payload, s_envelopeSerializerOptions) ??
#else
            return JsonSerializer.Deserialize(
                payload,
                DataGridPersistedStateJsonContext.Default.DataGridGeneratedStateEnvelope) ??
#endif
                throw new DataGridStatePersistenceException("Deserialized generated state envelope is null.");
        }

#if DATAGRID_INTERNAL
        private static JsonSerializerOptions CreateEnvelopeSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }
#endif

        private static void ApplyColumnAliases(
            DataGridPersistedState state,
            IReadOnlyDictionary<string, string> aliases)
        {
            if (aliases.Count == 0)
            {
                return;
            }

            if (state.Columns?.Columns != null)
            {
                foreach (DataGridPersistedState.ColumnState column in state.Columns.Columns)
                {
                    Remap(column.ColumnKey, aliases);
                }
            }
            if (state.Sorting?.Descriptors != null)
            {
                foreach (DataGridPersistedState.SortingDescriptorState descriptor in state.Sorting.Descriptors)
                {
                    Remap(descriptor.ColumnId, aliases);
                }
            }
            if (state.Filtering?.Descriptors != null)
            {
                foreach (DataGridPersistedState.FilteringDescriptorState descriptor in state.Filtering.Descriptors)
                {
                    Remap(descriptor.ColumnId, aliases);
                }
            }
            if (state.Search?.Descriptors != null)
            {
                foreach (DataGridPersistedState.SearchDescriptorState descriptor in state.Search.Descriptors)
                {
                    Remap(descriptor.ColumnIds, aliases);
                }
            }
            Remap(state.Search?.Current?.ColumnKey, aliases);
            if (state.ConditionalFormatting?.Descriptors != null)
            {
                foreach (DataGridPersistedState.ConditionalFormattingDescriptorState descriptor in state.ConditionalFormatting.Descriptors)
                {
                    Remap(descriptor.ColumnId, aliases);
                }
            }
            if (state.Selection?.SelectedCells != null)
            {
                foreach (DataGridPersistedState.CellState cell in state.Selection.SelectedCells)
                {
                    Remap(cell.ColumnKey, aliases);
                }
            }
            Remap(state.Selection?.CurrentCell?.ColumnKey, aliases);
        }

        private static void Remap(
            IReadOnlyList<DataGridPersistedState.PersistedValue> values,
            IReadOnlyDictionary<string, string> aliases)
        {
            if (values == null)
            {
                return;
            }
            for (int index = 0; index < values.Count; index++)
            {
                Remap(values[index], aliases);
            }
        }

        private static void Remap(
            DataGridPersistedState.PersistedValue value,
            IReadOnlyDictionary<string, string> aliases)
        {
            if (value != null && value.Value != null && aliases.TryGetValue(value.Value, out string replacement))
            {
                value.Value = replacement;
            }
        }
    }
}
