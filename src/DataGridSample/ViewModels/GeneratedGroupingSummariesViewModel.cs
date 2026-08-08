// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using Avalonia.Controls;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedGroupedOrder), ProviderName = "GeneratedGroupedOrderSchema")]
[GenerateDataGridView(
    typeof(GeneratedGroupedOrder),
    ViewName = "GeneratedGroupingSummariesGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated typed grouping and summaries",
    AutomationId = "generated-grouping-summaries-grid",
    ShowTotalSummary = true,
    ShowGroupSummary = true,
    TotalSummaryPosition = DataGridSummaryRowPosition.Bottom,
    GroupSummaryPosition = DataGridGroupSummaryPosition.Footer)]
public sealed partial class GeneratedGroupingSummariesViewModel : ReactiveObject
{
    private readonly ObservableCollection<GeneratedGroupedOrder> _source = [];
    private readonly IReadOnlyList<IDataGridGeneratedSummary<GeneratedGroupedOrder>> _summaries;
    private int _nextOrderId = 2013;
    private int _revision;

    [Reactive]
    private string _status = "Two generated typed group selectors and five summary definitions are active.";

    [Reactive]
    private int _orderCount;

    [Reactive]
    private int _uniqueCustomerCount;

    [Reactive]
    private int _totalQuantity;

    [Reactive]
    private decimal _averageUnitPrice;

    [Reactive]
    private decimal _totalRevenue;

    [Reactive]
    private int _groupCount;

    public GeneratedGroupingSummariesViewModel()
    {
        GeneratedGroupedOrder[] initial = CreateInitialOrders();
        for (int index = 0; index < initial.Length; index++)
        {
            _source.Add(initial[index]);
        }

        Items = GeneratedGroupedOrderSchema.CreateCollectionView(_source, sourceIsInGroupOrder: false);
        _summaries = GeneratedGroupedOrderSchema.CreateSummaries();
        ResetGeneratedSummaries();

        AddBatchCommand = ReactiveCommand.Create(AddBatch);
        ReplaceOrderCommand = ReactiveCommand.Create(ReplaceOrder);
        RemoveOrderCommand = ReactiveCommand.Create(RemoveOrder);
        ResetCommand = ReactiveCommand.Create(Reset);
    }

    public DataGridCollectionView Items { get; }

    public IReadOnlyList<IDataGridGeneratedSummary<GeneratedGroupedOrder>> GeneratedSummaries => _summaries;

    public ReactiveCommand<RxVoid, RxVoid> AddBatchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReplaceOrderCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RemoveOrderCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetCommand { get; }

    private void AddBatch()
    {
        GeneratedGroupedOrder[] batch =
        [
            CreateOrder(_nextOrderId++, "North", "Hardware", "Stark", 14, 84m),
            CreateOrder(_nextOrderId++, "South", "Software", "Acme", 9, 132m),
            CreateOrder(_nextOrderId++, "West", "Services", "Wayne", 5, 240m)
        ];
        for (int index = 0; index < batch.Length; index++)
        {
            _source.Add(batch[index]);
            AddToGeneratedSummaries(batch[index]);
        }
        Publish("Incremental Add applied three rows without rebuilding generated aggregates.");
    }

    private void ReplaceOrder()
    {
        if (_source.Count == 0)
        {
            return;
        }

        int index = _source.Count / 2;
        GeneratedGroupedOrder previous = _source[index];
        GeneratedGroupedOrder replacement = CreateOrder(
            previous.OrderId,
            previous.Region,
            previous.Category,
            previous.Customer + " Prime",
            previous.Quantity + 7,
            previous.UnitPrice + 18m);
        _source[index] = replacement;
        for (int summaryIndex = 0; summaryIndex < _summaries.Count; summaryIndex++)
        {
            _summaries[summaryIndex].Replace(previous, replacement);
        }
        Publish($"Incremental Replace updated stable order {replacement.OrderId}.");
    }

    private void RemoveOrder()
    {
        if (_source.Count == 0)
        {
            return;
        }

        GeneratedGroupedOrder removed = _source[^1];
        _source.RemoveAt(_source.Count - 1);
        for (int index = 0; index < _summaries.Count; index++)
        {
            _summaries[index].Remove(removed);
        }
        Publish($"Incremental Remove discarded order {removed.OrderId}.");
    }

    private void Reset()
    {
        _source.Clear();
        GeneratedGroupedOrder[] initial = CreateInitialOrders();
        for (int index = 0; index < initial.Length; index++)
        {
            _source.Add(initial[index]);
        }
        ResetGeneratedSummaries();
        Status = "Reset fallback rebuilt every generated aggregate from the authoritative source.";
    }

    private void AddToGeneratedSummaries(GeneratedGroupedOrder item)
    {
        for (int index = 0; index < _summaries.Count; index++)
        {
            _summaries[index].Add(item);
        }
    }

    private void ResetGeneratedSummaries()
    {
        for (int index = 0; index < _summaries.Count; index++)
        {
            _summaries[index].Reset(_source);
        }
        PublishAggregates();
    }

    private void Publish(string operation)
    {
        _revision++;
        PublishAggregates();
        Status = $"r{_revision}: {operation}";
    }

    private void PublishAggregates()
    {
        OrderCount = GetSummaryValue<int>("order-id", DataGridAggregateType.Count);
        UniqueCustomerCount = GetSummaryValue<int>("customer", DataGridAggregateType.CountDistinct);
        TotalQuantity = GetSummaryValue<int>("quantity", DataGridAggregateType.Sum);
        AverageUnitPrice = GetSummaryValue<decimal>("unit-price", DataGridAggregateType.Average);
        TotalRevenue = GetSummaryValue<decimal>("revenue", DataGridAggregateType.Sum);

        var groups = new HashSet<(string Region, string Category)>();
        for (int index = 0; index < _source.Count; index++)
        {
            GeneratedGroupedOrder row = _source[index];
            groups.Add((row.Region, row.Category));
        }
        GroupCount = groups.Count;
    }

    private T GetSummaryValue<T>(string columnKey, DataGridAggregateType aggregate)
    {
        for (int index = 0; index < _summaries.Count; index++)
        {
            IDataGridGeneratedSummary<GeneratedGroupedOrder> summary = _summaries[index];
            if (summary.Aggregate == aggregate && string.Equals(summary.ColumnKey, columnKey, StringComparison.Ordinal))
            {
                return summary.Value is T value ? value : default!;
            }
        }
        throw new InvalidOperationException($"Generated summary '{columnKey}/{aggregate}' was not found.");
    }

    private static GeneratedGroupedOrder[] CreateInitialOrders() =>
    [
        CreateOrder(2001, "North", "Hardware", "Acme", 8, 120m),
        CreateOrder(2002, "North", "Hardware", "Globex", 5, 180m),
        CreateOrder(2003, "North", "Software", "Initech", 12, 95m),
        CreateOrder(2004, "South", "Software", "Acme", 7, 210m),
        CreateOrder(2005, "South", "Services", "Umbrella", 4, 330m),
        CreateOrder(2006, "East", "Hardware", "Wayne", 11, 76m),
        CreateOrder(2007, "East", "Software", "Stark", 9, 145m),
        CreateOrder(2008, "East", "Services", "Globex", 3, 410m),
        CreateOrder(2009, "West", "Hardware", "Initech", 6, 165m),
        CreateOrder(2010, "West", "Software", "Umbrella", 10, 108m),
        CreateOrder(2011, "West", "Services", "Wayne", 2, 520m),
        CreateOrder(2012, "North", "Services", "Stark", 5, 285m)
    ];

    private static GeneratedGroupedOrder CreateOrder(
        int id,
        string region,
        string category,
        string customer,
        int quantity,
        decimal unitPrice) =>
        new()
        {
            OrderId = id,
            Region = region,
            Category = category,
            Customer = customer,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
}
