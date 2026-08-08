// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataGridSample.Models;

namespace DataGridSample.Services;

public sealed class GeneratedRemoteOrderQueryProvider : IDataGridQueryProvider<GeneratedRemoteOrder, int>
{
    private readonly IReadOnlyList<GeneratedRemoteOrder> _source = CreateSource();
    private readonly object _gate = new();
    private TaskCompletionSource<bool> _slowRequestStarted = CreateCompletion();
    private DataGridRemoteQuery<GeneratedRemoteOrder>? _lastQuery;
    private int _callCount;
    private int _cancellationCount;
    private int _slowNextRequest;
    private int _failNextRequest;

    public int CallCount => Volatile.Read(ref _callCount);

    public int CancellationCount => Volatile.Read(ref _cancellationCount);

    public DataGridRemoteQuery<GeneratedRemoteOrder>? LastQuery
    {
        get
        {
            lock (_gate)
            {
                return _lastQuery;
            }
        }
    }

    public void MakeNextRequestSlowAndCancellationResistant()
    {
        lock (_gate)
        {
            _slowRequestStarted = CreateCompletion();
            Volatile.Write(ref _slowNextRequest, 1);
        }
    }

    public void FailNextRequest() => Volatile.Write(ref _failNextRequest, 1);

    public Task WaitForSlowRequestAsync(CancellationToken cancellationToken = default)
    {
        Task task;
        lock (_gate)
        {
            task = _slowRequestStarted.Task;
        }
        return task.WaitAsync(cancellationToken);
    }

    public async ValueTask<DataGridQueryPage<GeneratedRemoteOrder, int>> ExecuteAsync(
        DataGridRemoteQuery<GeneratedRemoteOrder> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Interlocked.Increment(ref _callCount);
        lock (_gate)
        {
            _lastQuery = query;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => Interlocked.Increment(ref ((GeneratedRemoteOrderQueryProvider)state!)._cancellationCount),
            this);

        if (Interlocked.Exchange(ref _slowNextRequest, 0) != 0)
        {
            TaskCompletionSource<bool> started;
            lock (_gate)
            {
                started = _slowRequestStarted;
            }
            started.TrySetResult(true);
            await Task.Delay(TimeSpan.FromMilliseconds(160)).ConfigureAwait(false);
        }
        else
        {
            await Task.Delay(TimeSpan.FromMilliseconds(8), cancellationToken).ConfigureAwait(false);
        }

        if (Interlocked.Exchange(ref _failNextRequest, 0) != 0)
        {
            throw new InvalidOperationException("The deterministic remote service rejected this request.");
        }

        Func<GeneratedRemoteOrder, bool> filter = GeneratedRemoteOrderSchema.Instance.CreateFilterPredicate(query.Filtering);
        Func<GeneratedRemoteOrder, bool> search = GeneratedRemoteOrderSchema.Instance.CreateSearchPredicate(query.Searching);
        IComparer<GeneratedRemoteOrder> comparer = GeneratedRemoteOrderSchema.Instance.CreateSortComparer(query.Sorting);
        var matches = new List<GeneratedRemoteOrder>(_source.Count);
        for (int index = 0; index < _source.Count; index++)
        {
            GeneratedRemoteOrder item = _source[index];
            if (filter(item) && search(item))
            {
                matches.Add(item);
            }
        }
        matches.Sort(comparer);

        int offset = ResolveOffset(query.Page);
        int available = Math.Max(0, matches.Count - offset);
        int count = Math.Min(query.Page.Size, available);
        var pageItems = new List<GeneratedRemoteOrder>(count);
        for (int index = 0; index < count; index++)
        {
            pageItems.Add(matches[offset + index]);
        }

        int nextOffset = offset + count;
        bool hasMore = nextOffset < matches.Count;
        return new DataGridQueryPage<GeneratedRemoteOrder, int>(
            query.Revision,
            pageItems,
            matches.Count,
            hasMore ? nextOffset.ToString(CultureInfo.InvariantCulture) : null,
            hasMore);
    }

    private static int ResolveOffset(DataGridPageRequest page)
    {
        if (page.Mode == DataGridPageMode.Offset)
        {
            return page.Offset;
        }
        return int.TryParse(page.Cursor, NumberStyles.None, CultureInfo.InvariantCulture, out int offset)
            ? Math.Max(0, offset)
            : 0;
    }

    private static IReadOnlyList<GeneratedRemoteOrder> CreateSource()
    {
        string[] customers = ["Alpine", "Contoso", "Fabrikam", "Northwind", "Tailspin", "Wide World"];
        string[] regions = ["Europe", "North America", "Asia Pacific"];
        string[] statuses = ["Pending", "Approved", "Shipped", "Held"];
        var result = new List<GeneratedRemoteOrder>(64);
        DateTimeOffset origin = new(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        for (int id = 1; id <= 64; id++)
        {
            result.Add(new GeneratedRemoteOrder
            {
                Id = id,
                Customer = $"{customers[(id - 1) % customers.Length]} {id:00}",
                Region = regions[(id - 1) % regions.Length],
                OrderStatus = statuses[(id - 1) % statuses.Length],
                Total = 40m + id * 13.75m,
                UpdatedAt = origin.AddMinutes(id * 17)
            });
        }
        return result;
    }

    private static TaskCompletionSource<bool> CreateCompletion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
