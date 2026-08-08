// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataGridSample.Models;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages;

/// <summary>Handles grid-owned state operations for the generated selection/state sample view.</summary>
public sealed class GeneratedSelectionStateInteractionHandler :
    IDataGridGeneratedViewInteractionHandler<GeneratedSelectionStateRequest, GeneratedSelectionStateResult>
{
    /// <inheritdoc />
    public ValueTask<GeneratedSelectionStateResult> HandleAsync(
        DataGridGeneratedViewInteractionContext<GeneratedSelectionStateRequest> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.View is not GeneratedSelectionStateGrid view ||
            view.DataContext is not GeneratedSelectionStateViewModel viewModel)
        {
            throw new InvalidOperationException("The generated selection/state interaction requires its declared view and ViewModel.");
        }

        GeneratedSelectionStateResult result = context.Input.Operation switch
        {
            GeneratedSelectionStateOperation.Capture => Capture(view, context.DataGrid, viewModel, legacyV1: false),
            GeneratedSelectionStateOperation.CaptureLegacyV1 => Capture(view, context.DataGrid, viewModel, legacyV1: true),
            GeneratedSelectionStateOperation.Scramble => Scramble(context.DataGrid, viewModel),
            GeneratedSelectionStateOperation.Restore => Restore(view, context.DataGrid, viewModel, context.Input.Payload),
            _ => throw new ArgumentOutOfRangeException(nameof(context.Input.Operation))
        };
        return ValueTask.FromResult(result);
    }

    private static GeneratedSelectionStateResult Capture(
        GeneratedSelectionStateGrid view,
        DataGrid grid,
        GeneratedSelectionStateViewModel viewModel,
        bool legacyV1)
    {
        DataGridGeneratedStateEnvelope envelope = view.CaptureGeneratedState();
        if (legacyV1)
        {
            envelope.SchemaVersion = 1;
            envelope.SchemaHash = "sample/generated-feature-row/v1";
            envelope.State.Version = 1;
            if (envelope.State.Columns?.Columns is { } columns)
            {
                for (int index = 0; index < columns.Count; index++)
                {
                    DataGridPersistedState.PersistedValue key = columns[index].ColumnKey;
                    if (key != null && string.Equals(key.Value, "symbol", StringComparison.Ordinal))
                    {
                        key.Value = "ticker";
                    }
                }
            }
        }

        string payload = viewModel.StateController.SerializeToString(envelope);
        string message = legacyV1
            ? "Captured a version 1 payload with the legacy 'ticker' column key."
            : "Captured all generated grid-state sections with stable item and column keys.";
        return Result(viewModel, grid, payload, message);
    }

    private static GeneratedSelectionStateResult Scramble(
        DataGrid grid,
        GeneratedSelectionStateViewModel viewModel)
    {
        viewModel.ClearGeneratedStateScenario();
        if (grid.Columns.Count > 2)
        {
            grid.Columns[0].DisplayIndex = grid.Columns.Count - 1;
            grid.Columns[1].IsVisible = false;
            grid.Columns[2].Width = new DataGridLength(240);
        }
        return Result(viewModel, grid, null, "Cleared operations and selection, then changed order, visibility, and width.");
    }

    private static GeneratedSelectionStateResult Restore(
        GeneratedSelectionStateGrid view,
        DataGrid grid,
        GeneratedSelectionStateViewModel viewModel,
        string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Capture a generated state payload before restoring it.");
        }
        DataGridGeneratedStateEnvelope envelope = viewModel.StateController.Deserialize(payload);
        view.RestoreGeneratedState(envelope);
        viewModel.SynchronizeGeneratedSelection(DataGridGeneratedSelectionOrigin.Restore);
        return Result(viewModel, grid, payload, "Restored and validated every generated grid-state section.");
    }

    private static GeneratedSelectionStateResult Result(
        GeneratedSelectionStateViewModel viewModel,
        DataGrid grid,
        string? payload,
        string message) =>
        new(payload, message, grid.Columns.Count, viewModel.SelectionController.SelectedItemKeys.Count);
}
