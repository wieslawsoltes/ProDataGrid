// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedDistinctValueTests
{
    [Fact]
    public void Local_provider_is_typed_ordered_and_bounded()
    {
        var provider = new DataGridGeneratedDistinctValueProvider<Row, string>("desk", static row => row.Desk);
        Row[] rows = [new("A"), new("B"), new("A"), new("C")];

        IReadOnlyList<string> values = provider.GetValues(rows, maximumResults: 2);

        Assert.Equal(["A", "B"], values);
    }

    [Fact]
    public async Task Remote_controller_exposes_loading_values_and_errors()
    {
        var provider = new Provider();
        using var controller = new DataGridGeneratedRemoteDistinctValueController<string>("desk", provider);

        Assert.True(await controller.LoadAsync("r", 5, TestContext.Current.CancellationToken));
        Assert.Equal(["Rates", "Risk"], controller.Values);
        Assert.False(controller.IsLoading);
        Assert.Null(controller.Error);
        Assert.Equal("desk", provider.Query.ColumnKey);
        Assert.Equal(5, provider.Query.MaximumResults);
    }

    [Fact]
    public async Task Latest_remote_distinct_request_wins()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new Provider(async (query, token) =>
        {
            if (query.SearchText == "first")
            {
                firstStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return [query.SearchText!];
        });
        using var controller = new DataGridGeneratedRemoteDistinctValueController<string>("desk", provider);

        ValueTask<bool> first = controller.LoadAsync("first", cancellationToken: TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(await controller.LoadAsync("second", cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await first);
        Assert.Equal(["second"], controller.Values);
    }

    private sealed record Row(string Desk);

    private sealed class Provider : IDataGridGeneratedRemoteDistinctValueProvider<string>
    {
        private readonly Func<DataGridGeneratedDistinctValueQuery, CancellationToken, ValueTask<IReadOnlyList<string>>>? _execute;

        public Provider(Func<DataGridGeneratedDistinctValueQuery, CancellationToken, ValueTask<IReadOnlyList<string>>>? execute = null) =>
            _execute = execute;

        public DataGridGeneratedDistinctValueQuery Query { get; private set; }

        public ValueTask<IReadOnlyList<string>> ExecuteAsync(DataGridGeneratedDistinctValueQuery query, CancellationToken cancellationToken)
        {
            Query = query;
            return _execute?.Invoke(query, cancellationToken) ?? ValueTask.FromResult<IReadOnlyList<string>>(["Rates", "Risk"]);
        }
    }
}
