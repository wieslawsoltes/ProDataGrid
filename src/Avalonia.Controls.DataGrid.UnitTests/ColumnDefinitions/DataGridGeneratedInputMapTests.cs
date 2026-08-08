// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedInputMapTests
{
    [Fact]
    public void Spreadsheet_profile_matches_search_fill_and_history_commands()
    {
        IDataGridGeneratedInputMap map = DataGridGeneratedInputMap.Create(
            DataGridGeneratedPerformanceProfile.Spreadsheet);

        Assert.True(map.TryMatch(Key.F, KeyModifiers.Control, KeyModifiers.Control, out DataGridGeneratedInputAction search));
        Assert.Equal(DataGridGeneratedInputAction.Search, search);
        Assert.True(map.TryMatch(Key.D, KeyModifiers.Control, KeyModifiers.Control, out DataGridGeneratedInputAction fillDown));
        Assert.Equal(DataGridGeneratedInputAction.FillDown, fillDown);
        Assert.True(map.TryMatch(Key.R, KeyModifiers.Control, KeyModifiers.Control, out DataGridGeneratedInputAction fillRight));
        Assert.Equal(DataGridGeneratedInputAction.FillRight, fillRight);
        Assert.True(map.TryMatch(Key.Z, KeyModifiers.Control, KeyModifiers.Control, out DataGridGeneratedInputAction undo));
        Assert.Equal(DataGridGeneratedInputAction.Undo, undo);
        Assert.True(map.TryMatch(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, KeyModifiers.Control, out DataGridGeneratedInputAction redo));
        Assert.Equal(DataGridGeneratedInputAction.Redo, redo);
    }

    [Fact]
    public void Non_spreadsheet_profile_only_adds_search_command()
    {
        IDataGridGeneratedInputMap map = DataGridGeneratedInputMap.Create(
            DataGridGeneratedPerformanceProfile.HighFrequencyStreaming);

        Assert.NotNull(map.CreateKeyboardGestureOverrides(KeyModifiers.Meta));
        Assert.True(map.TryMatch(Key.F, KeyModifiers.Meta, KeyModifiers.Meta, out DataGridGeneratedInputAction search));
        Assert.Equal(DataGridGeneratedInputAction.Search, search);
        Assert.False(map.TryMatch(Key.D, KeyModifiers.Meta, KeyModifiers.Meta, out DataGridGeneratedInputAction action));
        Assert.Equal(DataGridGeneratedInputAction.None, action);
    }

    [Fact]
    public void Input_event_carries_typed_current_cell_state_and_handled_feedback()
    {
        var row = new TestRow("AVLN");
        var input = new DataGridGeneratedInputEvent<TestRow>(
            DataGridGeneratedInputAction.FillRight,
            Key.R,
            KeyModifiers.Control,
            row,
            rowIndex: 4,
            columnIndex: 2);

        Assert.Same(row, input.Item);
        Assert.Equal(4, input.RowIndex);
        Assert.Equal(2, input.ColumnIndex);
        Assert.True(input.Handled);

        input.Handled = false;
        Assert.False(input.Handled);
    }

    private sealed record TestRow(string Symbol);
}
