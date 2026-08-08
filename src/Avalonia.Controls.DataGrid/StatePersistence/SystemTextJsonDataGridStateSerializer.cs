// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.Controls
{
    /// <summary>
    /// Default DataGrid state serializer based on built-in System.Text.Json.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class SystemTextJsonDataGridStateSerializer : IDataGridStateSerializer
    {
#if DATAGRID_INTERNAL
        private static readonly JsonSerializerOptions s_serializerOptions = CreateSerializerOptions();
#endif

        public string FormatId => "json/system-text";

        public byte[] Serialize(DataGridPersistedState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

#if DATAGRID_INTERNAL
            return JsonSerializer.SerializeToUtf8Bytes(state, s_serializerOptions);
#else
            return JsonSerializer.SerializeToUtf8Bytes(
                state,
                DataGridPersistedStateJsonContext.Default.DataGridPersistedState);
#endif
        }

        public DataGridPersistedState Deserialize(ReadOnlySpan<byte> payload)
        {
#if DATAGRID_INTERNAL
            var state = JsonSerializer.Deserialize<DataGridPersistedState>(payload, s_serializerOptions);
#else
            var state = JsonSerializer.Deserialize(
                payload,
                DataGridPersistedStateJsonContext.Default.DataGridPersistedState);
#endif

            if (state == null)
            {
                throw new DataGridStatePersistenceException("Deserialized state payload is null.");
            }

            return state;
        }

        public string SerializeToString(DataGridPersistedState state)
        {
            return Encoding.UTF8.GetString(Serialize(state));
        }

        public DataGridPersistedState Deserialize(string payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return Deserialize(Encoding.UTF8.GetBytes(payload));
        }

#if DATAGRID_INTERNAL
        private static JsonSerializerOptions CreateSerializerOptions()
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
    }

#if !DATAGRID_INTERNAL
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Default,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false)]
    [JsonSerializable(typeof(DataGridPersistedState))]
    [JsonSerializable(typeof(DataGridGeneratedStateEnvelope))]
    [JsonSerializable(typeof(DataGridPersistedState.CellState))]
    [JsonSerializable(typeof(DataGridPersistedState.ColumnLayoutState))]
    [JsonSerializable(typeof(DataGridPersistedState.ColumnState))]
    [JsonSerializable(typeof(DataGridPersistedState.ConditionalFormattingDescriptorState))]
    [JsonSerializable(typeof(DataGridPersistedState.ConditionalFormattingState))]
    [JsonSerializable(typeof(DataGridPersistedState.DataGridLengthValue))]
    [JsonSerializable(typeof(DataGridPersistedState.FilteringDescriptorState))]
    [JsonSerializable(typeof(DataGridPersistedState.FilteringState))]
    [JsonSerializable(typeof(DataGridPersistedState.GroupDescriptionState))]
    [JsonSerializable(typeof(DataGridPersistedState.GroupState))]
    [JsonSerializable(typeof(DataGridPersistedState.GroupingState))]
    [JsonSerializable(typeof(DataGridPersistedState.HierarchicalState))]
    [JsonSerializable(typeof(DataGridPersistedState.PersistedValue))]
    [JsonSerializable(typeof(DataGridPersistedState.ScrollSample))]
    [JsonSerializable(typeof(DataGridPersistedState.ScrollState))]
    [JsonSerializable(typeof(DataGridPersistedState.SearchCurrentState))]
    [JsonSerializable(typeof(DataGridPersistedState.SearchDescriptorState))]
    [JsonSerializable(typeof(DataGridPersistedState.SearchState))]
    [JsonSerializable(typeof(DataGridPersistedState.SelectionState))]
    [JsonSerializable(typeof(DataGridPersistedState.SortingDescriptorState))]
    [JsonSerializable(typeof(DataGridPersistedState.SortingState))]
    internal sealed partial class DataGridPersistedStateJsonContext : JsonSerializerContext
    {
    }
#endif
}
