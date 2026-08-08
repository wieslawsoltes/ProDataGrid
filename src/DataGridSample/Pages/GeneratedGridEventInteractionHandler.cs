// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Threading.Tasks;
using Avalonia.Controls;
using DataGridSample.Models;

namespace DataGridSample.Pages;

public sealed class GeneratedGridEventInteractionHandler :
    IDataGridGeneratedViewInteractionHandler<GeneratedEventCommandRow, string>
{
    public ValueTask<string> HandleAsync(
        DataGridGeneratedViewInteractionContext<GeneratedEventCommandRow> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        string response =
            $"{context.Input.Symbol}: generated view has {context.DataGrid.Columns.Count} typed columns.";
        return ValueTask.FromResult(response);
    }
}
