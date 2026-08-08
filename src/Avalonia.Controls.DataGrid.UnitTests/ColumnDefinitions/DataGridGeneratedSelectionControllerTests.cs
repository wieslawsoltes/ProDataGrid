// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedSelectionControllerTests
{
    [Fact]
    public void Keyed_selection_survives_reorder_and_unloaded_pages()
    {
        DataGridGeneratedSelectionController<Row, int> controller = CreateController();
        controller.ResetSource(new[] { new Row(1, "one"), new Row(2, "two") });
        controller.SelectKey(2);

        controller.ResetSource(new[] { new Row(3, "three") });
        Assert.Equal(new[] { 2 }, controller.SelectedItemKeys);
        Assert.Empty(controller.GetSelectedItems());

        Row replacement = new(2, "replacement");
        controller.ResetSource(new[] { replacement, new Row(1, "one") });
        Assert.Same(replacement, Assert.Single(controller.GetSelectedItems()));
    }

    [Fact]
    public void Single_profile_range_selects_only_range_end()
    {
        DataGridGeneratedSelectionController<Row, int> controller = CreateController(
            new DataGridGeneratedSelectionProfile { Mode = DataGridSelectionMode.Single });
        controller.ResetSource(new[] { new Row(1, "one"), new Row(2, "two"), new Row(3, "three") });

        controller.SelectRange(0, 2);

        Assert.Equal(new[] { 3 }, controller.SelectedItemKeys);
    }

    [Fact]
    public void Snapshot_preserves_row_column_cell_and_current_cell_keys()
    {
        DataGridGeneratedSelectionController<Row, int> source = CreateController();
        source.SelectKey(2);
        source.SelectColumn("amount");
        source.SelectCell(2, "amount");
        DataGridGeneratedSelectionSnapshot<int> snapshot = source.Capture();
        DataGridGeneratedSelectionController<Row, int> target = CreateController();
        DataGridGeneratedSelectionOrigin observed = DataGridGeneratedSelectionOrigin.Unknown;
        target.SelectionChanged += (_, args) => observed = args.Origin;

        target.Restore(snapshot);

        Assert.Equal(DataGridGeneratedSelectionOrigin.Restore, observed);
        Assert.Equal(new[] { 2 }, target.SelectedItemKeys);
        Assert.Equal(new[] { "amount" }, target.SelectedColumnKeys);
        Assert.Equal(new DataGridGeneratedCellKey<int>(2, "amount"), Assert.Single(target.SelectedCells));
        Assert.True(target.Capture().HasCurrentCell);
    }

    [Fact]
    public void Identity_model_bridge_round_trips_loaded_selection()
    {
        Row[] rows = { new(1, "one"), new(2, "two"), new(3, "three") };
        DataGridGeneratedSelectionController<Row, int> controller = CreateController();
        controller.ResetSource(rows);
        controller.SelectKey(2);
        DataGridSelection.IdentitySelectionModel model = controller.CreateIdentitySelectionModel(rows);

        Assert.Equal(1, model.SelectedIndex);
        model.Select(2);
        controller.CaptureFrom(model, DataGridGeneratedSelectionOrigin.Keyboard);

        Assert.Equal(new[] { 2, 3 }, controller.SelectedItemKeys.OrderBy(static key => key));
    }

    [Fact]
    public void Identity_model_projection_uses_the_models_filtered_and_reordered_source()
    {
        Row one = new(1, "one");
        Row two = new(2, "two");
        Row three = new(3, "three");
        DataGridGeneratedSelectionController<Row, int> controller = CreateController();
        controller.ResetSource(new[] { one, two, three });
        controller.SelectKey(2);
        controller.SelectKey(3);

        DataGridSelection.IdentitySelectionModel model =
            controller.CreateIdentitySelectionModel(new[] { three, two });

        Assert.Equal(new[] { three, two }, model.SelectedItems);
        Assert.Equal(new[] { 0, 1 }, model.SelectedIndexes);
    }

    [Fact]
    public void Selection_change_reports_explicit_origin_and_monotonic_version()
    {
        DataGridGeneratedSelectionController<Row, int> controller = CreateController();
        var changes = new List<(DataGridGeneratedSelectionOrigin Origin, long Version)>();
        controller.SelectionChanged += (_, args) => changes.Add((args.Origin, args.Version));

        controller.SelectKey(1, DataGridGeneratedSelectionOrigin.Chart);
        controller.DeselectKey(1, DataGridGeneratedSelectionOrigin.Pointer);

        Assert.Equal(
            new[]
            {
                (DataGridGeneratedSelectionOrigin.Chart, 1L),
                (DataGridGeneratedSelectionOrigin.Pointer, 2L)
            },
            changes);
    }

    private static DataGridGeneratedSelectionController<Row, int> CreateController(
        DataGridGeneratedSelectionProfile? profile = null) =>
        new(new RowKey(), profile);

    private sealed record Row(int Id, string Name);

    private sealed class RowKey : IDataGridItemKey<Row, int>
    {
        public int GetKey(Row item) => item.Id;
    }
}
