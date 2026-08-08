// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedDragDropTests
{
    [Fact]
    public async Task Controller_deduplicates_keys_and_delegates_domain_mutation()
    {
        var handler = new Handler();
        using var controller = new DataGridGeneratedDragDropController<int>(handler);

        bool applied = await controller.DropAsync(
            [1, 1, 2], 3, DataGridGeneratedDropPosition.Before,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(applied);
        Assert.Equal(DataGridGeneratedDropStatus.Applied, controller.Status);
        Assert.Equal([1, 2], handler.Request!.ItemKeys);
        Assert.Equal(3, handler.Request.TargetKey);
    }

    [Fact]
    public async Task Controller_rejects_self_and_hierarchy_cycles_before_handler()
    {
        var handler = new Handler();
        using var controller = new DataGridGeneratedDragDropController<int>(
            handler,
            isDescendant: static (source, target) => source == 1 && target == 3);

        Assert.False(await controller.DropAsync(
            [1], 1, DataGridGeneratedDropPosition.After,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await controller.DropAsync(
            [1], 3, DataGridGeneratedDropPosition.Inside,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(DataGridGeneratedDropStatus.Rejected, controller.Status);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task New_request_cancels_and_supersedes_pending_validation()
    {
        var handler = new Handler();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new DataGridGeneratedDragDropController<int>(
            handler,
            async (request, token) =>
            {
                if (request.TargetKey == 2)
                {
                    started.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                return null;
            });

        ValueTask<bool> first = controller.DropAsync(
            [1], 2, DataGridGeneratedDropPosition.Before,
            cancellationToken: TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        bool second = await controller.DropAsync(
            [1], 3, DataGridGeneratedDropPosition.Before,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(second);
        Assert.False(await first);
        Assert.Equal(3, handler.Request!.TargetKey);
    }

    private sealed class Handler : IDataGridGeneratedDropHandler<int>
    {
        public DataGridGeneratedDropRequest<int>? Request { get; private set; }

        public ValueTask ApplyAsync(DataGridGeneratedDropRequest<int> request, CancellationToken cancellationToken)
        {
            Request = request;
            return ValueTask.CompletedTask;
        }
    }
}
