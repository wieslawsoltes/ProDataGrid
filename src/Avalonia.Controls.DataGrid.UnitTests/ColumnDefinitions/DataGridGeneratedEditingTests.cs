// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedEditingTests
{
    [Fact]
    public void Typed_field_parses_coerces_validates_and_formats_without_reflection()
    {
        Row row = new(1, 5m);
        DataGridGeneratedEditField<Row, decimal> field = CreateAmountField(
            validator: static (_, value) => value > 100m ? "too large" : null,
            coerce: static (_, value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero));

        DataGridGeneratedEditResult applied = field.TrySetText(
            row,
            "12.345".AsSpan(),
            CultureInfo.InvariantCulture,
            out object oldValue,
            out object newValue);
        DataGridGeneratedEditResult rejected = field.TrySetText(
            row,
            "101".AsSpan(),
            CultureInfo.InvariantCulture,
            out _,
            out _);

        Assert.Equal(DataGridGeneratedEditStatus.Applied, applied.Status);
        Assert.Equal(5m, oldValue);
        Assert.Equal(12.35m, newValue);
        Assert.Equal(12.35m, row.Amount);
        Assert.Equal("12.35", field.FormatValue(row, CultureInfo.InvariantCulture));
        Assert.Equal(DataGridGeneratedEditStatus.ValidationFailed, rejected.Status);
        Assert.Equal("too large", rejected.Error);
    }

    [Fact]
    public void Controller_groups_edits_into_keyed_undo_and_redo_batches()
    {
        Row first = new(1, 1m);
        Row second = new(2, 2m);
        var rows = new Dictionary<int, Row> { [1] = first, [2] = second };
        using var controller = new DataGridGeneratedEditController<Row, int>(
            new RowKey(),
            new IDataGridGeneratedEditField<Row>[] { CreateAmountField() },
            key => rows[key]);

        controller.BeginBatch();
        Assert.True(controller.TrySetText(first, "amount", "10".AsSpan(), CultureInfo.InvariantCulture).IsApplied);
        Assert.True(controller.TrySetValue(second, "amount", 20m).IsApplied);
        controller.CommitBatch();
        Assert.Equal((10m, 20m), (first.Amount, second.Amount));

        Assert.True(controller.Undo());
        Assert.Equal((1m, 2m), (first.Amount, second.Amount));
        Assert.True(controller.Redo());
        Assert.Equal((10m, 20m), (first.Amount, second.Amount));
    }

    [Fact]
    public void Eligibility_and_parse_failures_do_not_create_undo_records()
    {
        Row row = new(1, 1m) { Locked = true };
        DataGridGeneratedEditField<Row, decimal> field = CreateAmountField(canEdit: static item => !item.Locked);
        using var controller = new DataGridGeneratedEditController<Row, int>(
            new RowKey(),
            new IDataGridGeneratedEditField<Row>[] { field });

        Assert.Equal(
            DataGridGeneratedEditStatus.NotEditable,
            controller.TrySetValue(row, "amount", 2m).Status);
        Assert.Equal(
            DataGridGeneratedEditStatus.ParseFailed,
            controller.TrySetText(row, "amount", "not-number".AsSpan(), CultureInfo.InvariantCulture).Status);
        Assert.False(controller.CanUndo);
        Assert.Equal(1m, row.Amount);
    }

    [Fact]
    public async Task Async_validation_is_cancellable_revisioned_and_latest_result_wins()
    {
        Row row = new(1, 1m);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int invocation = 0;
        DataGridGeneratedEditField<Row, decimal> field = CreateAmountField(
            asyncValidator: async (_, _, cancellationToken) =>
            {
                int current = Interlocked.Increment(ref invocation);
                if (current == 1)
                {
                    firstStarted.SetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }
                return null;
            });
        using var controller = new DataGridGeneratedEditController<Row, int>(
            new RowKey(),
            new IDataGridGeneratedEditField<Row>[] { field });

        ValueTask<DataGridGeneratedEditResult> first = controller.TrySetValueAsync(
            row,
            "amount",
            2m,
            TestContext.Current.CancellationToken);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        DataGridGeneratedEditResult second = await controller.TrySetValueAsync(
            row,
            "amount",
            3m,
            TestContext.Current.CancellationToken);
        releaseFirst.TrySetResult();
        DataGridGeneratedEditResult superseded = await first;

        Assert.Equal(DataGridGeneratedEditStatus.Applied, second.Status);
        Assert.Equal(DataGridGeneratedEditStatus.Superseded, superseded.Status);
        Assert.Equal(3m, row.Amount);
    }

    private static DataGridGeneratedEditField<Row, decimal> CreateAmountField(
        Func<Row, decimal, string?>? validator = null,
        Func<Row, decimal, CancellationToken, ValueTask<string?>>? asyncValidator = null,
        Func<Row, decimal, decimal>? coerce = null,
        Predicate<Row>? canEdit = null) =>
        new(
            "amount",
            static item => item.Amount,
            static (item, value) => item.Amount = value,
            static (ReadOnlySpan<char> text, IFormatProvider provider, out decimal value) =>
                decimal.TryParse(text, NumberStyles.Number, provider, out value),
            static (value, provider) => value.ToString("0.##", provider),
            validator,
            asyncValidator,
            coerce,
            canEdit);

    private sealed class Row
    {
        public Row(int id, decimal amount)
        {
            Id = id;
            Amount = amount;
        }

        public int Id { get; }
        public decimal Amount { get; set; }
        public bool Locked { get; set; }
    }

    private sealed class RowKey : IDataGridItemKey<Row, int>
    {
        public int GetKey(Row item) => item.Id;
    }
}
