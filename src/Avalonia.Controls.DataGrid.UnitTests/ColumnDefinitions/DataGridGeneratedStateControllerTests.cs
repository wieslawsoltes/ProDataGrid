// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedStateControllerTests
{
    [Fact]
    public void Prepare_applies_column_aliases_across_persisted_sections()
    {
        DataGridGeneratedStateController controller = CreateController(
            version: 1,
            aliases: new Dictionary<string, string> { ["old_amount"] = "amount" });
        DataGridPersistedState state = new()
        {
            Sections = DataGridStateSections.All,
            Columns = new DataGridPersistedState.ColumnLayoutState
            {
                Columns = new[]
                {
                    new DataGridPersistedState.ColumnState { ColumnKey = Value("old_amount") }
                }
            },
            Sorting = new DataGridPersistedState.SortingState
            {
                Descriptors = new[]
                {
                    new DataGridPersistedState.SortingDescriptorState { ColumnId = Value("old_amount") }
                }
            },
            Selection = new DataGridPersistedState.SelectionState
            {
                CurrentCell = new DataGridPersistedState.CellState { ColumnKey = Value("old_amount") }
            }
        };

        DataGridPersistedState prepared = controller.Prepare(Envelope(state, version: 1, hash: "different"));

        Assert.Equal("amount", Assert.Single(prepared.Columns.Columns).ColumnKey.Value);
        Assert.Equal("amount", Assert.Single(prepared.Sorting.Descriptors).ColumnId.Value);
        Assert.Equal("amount", prepared.Selection.CurrentCell.ColumnKey.Value);
    }

    [Fact]
    public void Older_state_requires_and_invokes_migration()
    {
        bool called = false;
        DataGridGeneratedStateController controller = CreateController(
            version: 3,
            migration: (int from, int to, ref DataGridPersistedState state) =>
            {
                called = from == 1 && to == 3;
                state.Sections = DataGridStateSections.Selection;
                return true;
            });

        DataGridPersistedState prepared = controller.Prepare(Envelope(new DataGridPersistedState(), version: 1));

        Assert.True(called);
        Assert.Equal(3, prepared.Version);
        Assert.Equal(DataGridStateSections.Selection, prepared.Sections);
    }

    [Fact]
    public void Schema_identity_future_version_and_unversioned_hash_changes_are_rejected()
    {
        DataGridGeneratedStateController controller = CreateController(version: 2);

        Assert.Throws<DataGridStatePersistenceException>(() =>
            controller.Prepare(new DataGridGeneratedStateEnvelope
            {
                SchemaId = "other",
                SchemaHash = "hash",
                SchemaVersion = 2,
                State = new DataGridPersistedState()
            }));
        Assert.Throws<DataGridStatePersistenceException>(() =>
            controller.Prepare(Envelope(new DataGridPersistedState(), version: 3)));
        Assert.Throws<DataGridStatePersistenceException>(() =>
            controller.Prepare(Envelope(new DataGridPersistedState(), version: 2, hash: "changed")));
    }

    [Fact]
    public void Envelope_json_uses_source_generated_metadata_and_round_trips()
    {
        DataGridGeneratedStateController controller = CreateController(version: 2);
        DataGridGeneratedStateEnvelope envelope = Envelope(
            new DataGridPersistedState { Version = 2, Sections = DataGridStateSections.Sorting },
            version: 2);

        string payload = controller.SerializeToString(envelope);
        DataGridGeneratedStateEnvelope restored = controller.Deserialize(payload);

        Assert.Equal("schema", restored.SchemaId);
        Assert.Equal("hash", restored.SchemaHash);
        Assert.Equal(2, restored.SchemaVersion);
        Assert.Equal(DataGridStateSections.Sorting, restored.State.Sections);
    }

    private static DataGridGeneratedStateController CreateController(
        int version,
        IReadOnlyDictionary<string, string>? aliases = null,
        DataGridGeneratedStateMigration? migration = null) =>
        new(
            new DataGridGeneratedStateDescriptor("schema", "hash", version, columnAliases: aliases),
            new DataGridStateOptions(),
            migration);

    private static DataGridGeneratedStateEnvelope Envelope(
        DataGridPersistedState state,
        int version,
        string hash = "hash") =>
        new()
        {
            SchemaId = "schema",
            SchemaHash = hash,
            SchemaVersion = version,
            State = state
        };

    private static DataGridPersistedState.PersistedValue Value(string value) =>
        new() { Type = typeof(string).FullName, Value = value };
}
