// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedViewInteractionTests
{
    [Fact]
    public async Task Typed_context_is_forwarded_to_custom_handler_without_reflection()
    {
        var view = new Control();
        var dataGrid = new DataGrid();
        using var cancellation = new CancellationTokenSource();
        var context = new DataGridGeneratedViewInteractionContext<string>(
            view,
            dataGrid,
            "approve",
            cancellation.Token);
        IDataGridGeneratedViewInteractionHandler<string, bool> handler = new TestHandler();

        bool output = await handler.HandleAsync(context);

        Assert.True(output);
        Assert.Same(view, context.View);
        Assert.Same(dataGrid, context.DataGrid);
        Assert.Equal("approve", context.Input);
        Assert.Equal(cancellation.Token, context.CancellationToken);
    }

    [Fact]
    public void Context_rejects_missing_view_or_grid()
    {
        var view = new Control();
        var dataGrid = new DataGrid();

        Assert.Throws<System.ArgumentNullException>(() =>
            new DataGridGeneratedViewInteractionContext<string>(null!, dataGrid, string.Empty, default));
        Assert.Throws<System.ArgumentNullException>(() =>
            new DataGridGeneratedViewInteractionContext<string>(view, null!, string.Empty, default));
    }

    private sealed class TestHandler : IDataGridGeneratedViewInteractionHandler<string, bool>
    {
        public ValueTask<bool> HandleAsync(DataGridGeneratedViewInteractionContext<string> context) =>
            new(context.Input == "approve" && !context.CancellationToken.IsCancellationRequested);
    }
}
