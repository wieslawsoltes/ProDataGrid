// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using DataGridSample.Services;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedRemoteOrder), ProviderName = "GeneratedRemoteOrderSchema")]
[GenerateDataGridController(
    typeof(GeneratedRemoteOrder),
    "Orders",
    ProviderName = "GeneratedRemoteOrderSchema",
    SourceMember = nameof(_provider),
    SourceKind = DataGridGeneratedSourceKind.Remote,
    OperationExecution = DataGridOperationExecution.Remote,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Diagnostics)]
[GenerateDataGridView(
    typeof(GeneratedRemoteOrder),
    ViewName = "GeneratedRemoteQueryGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated remote query controller",
    AutomationId = "generated-remote-query-grid",
    ControllerName = "Orders",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query),
    ViewStatePropertyName = nameof(ViewState),
    ErrorMessagePropertyName = nameof(ErrorMessage),
    RetryCommandPropertyName = nameof(RetryCommand),
    LoadingText = "Loading a generated remote page…",
    EmptyText = "No remote orders match the generated query.",
    ErrorText = "The generated remote query failed.",
    RetryText = "Retry current page")]
public sealed partial class GeneratedRemoteQueryViewModel : ReactiveObject, IDisposable
{
    private const int PageSize = 8;
    private readonly GeneratedRemoteOrderQueryProvider _provider;
    private readonly ObservableCollection<GeneratedRemoteOrder> _items = [];
    private readonly CompositeDisposable _subscriptions = new();
    private bool _disposed;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "Generated descriptors are translated into immutable revisioned remote requests.";

    [Reactive]
    private DataGridGeneratedViewState _viewState = DataGridGeneratedViewState.Loading;

    [Reactive]
    private string? _errorMessage;

    [Reactive]
    private bool _isLoading;

    [Reactive]
    private int _pageIndex;

    [Reactive]
    private long _totalCount;

    [Reactive]
    private bool _hasMore;

    [Reactive]
    private long _remoteRevision;

    [Reactive]
    private int _staleResponseCount;

    [Reactive]
    private int _errorCount;

    [Reactive]
    private string _translatedField = string.Empty;

    public GeneratedRemoteQueryViewModel()
        : this(new GeneratedRemoteOrderQueryProvider())
    {
    }

    public GeneratedRemoteQueryViewModel(GeneratedRemoteOrderQueryProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        InitializeOrders(CreateOrdersController());
        InitializeOrdersRemoteQuery(CreateOrdersRemoteQueryController(
            debounce: TimeSpan.FromMilliseconds(15),
            pageCacheCapacity: 4,
            fieldNameTranslator: TranslateBackendField));
        OrdersRemoteQuery.StateChanged += RemoteQueryOnStateChanged;
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;
        SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;
        Orders.SetSorting(
        [
            GeneratedRemoteOrderSchema.UpdatedAt.Descending(),
            GeneratedRemoteOrderSchema.Id.Descending()
        ]);

        LoadFirstPageCommand = ReactiveCommand.CreateFromTask(LoadFirstPageAsync);
        PreviousPageCommand = ReactiveCommand.CreateFromTask(LoadPreviousPageAsync);
        NextPageCommand = ReactiveCommand.CreateFromTask(LoadNextPageAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        ApplyEuropeFilterCommand = ReactiveCommand.CreateFromTask(ApplyEuropeFilterAsync);
        SortTotalDescendingCommand = ReactiveCommand.CreateFromTask(SortTotalDescendingAsync);
        ClearQueryCommand = ReactiveCommand.CreateFromTask(ClearQueryAsync);
        RunStaleScenarioCommand = ReactiveCommand.CreateFromTask(RunStaleScenarioAsync);
        SimulateErrorCommand = ReactiveCommand.CreateFromTask(SimulateErrorAsync);
        RetryCommand = ReactiveCommand.CreateFromTask(RetryAsync);

        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch));

        TranslatedField = $"{GeneratedRemoteOrderSchema.Total.ColumnKey} → {OrdersRemoteQuery.TranslateField(GeneratedRemoteOrderSchema.Total.ColumnKey.ToString()!)}";
        Initialization = LoadPageAsync(0, useCache: true, CancellationToken.None);
    }

    public ObservableCollection<GeneratedRemoteOrder> Items => _items;

    public SortingModel SortingModel => Orders.SortingModel;

    public FilteringModel FilteringModel => Orders.FilteringModel;

    public SearchModel SearchModel => Orders.SearchModel;

    public int RequestCount => _provider.CallCount;

    public int CancellationCount => _provider.CancellationCount;

    public int PageNumber => PageIndex + 1;

    public bool IsDisposed => _disposed;

    public Task Initialization { get; }

    public ReactiveCommand<RxVoid, RxVoid> LoadFirstPageCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> PreviousPageCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> NextPageCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyEuropeFilterCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SortTotalDescendingCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearQueryCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RunStaleScenarioCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SimulateErrorCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RetryCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        OrdersRemoteQuery.StateChanged -= RemoteQueryOnStateChanged;
        _subscriptions.Dispose();
        DisposeOrders();
    }

    private Task LoadFirstPageAsync() => LoadPageAsync(0, useCache: true, CancellationToken.None);

    private Task LoadPreviousPageAsync() =>
        LoadPageAsync(Math.Max(0, PageIndex - 1), useCache: true, CancellationToken.None);

    private Task LoadNextPageAsync() =>
        LoadPageAsync(HasMore ? PageIndex + 1 : PageIndex, useCache: true, CancellationToken.None);

    private async Task RefreshAsync()
    {
        OrdersRemoteQuery.ClearCache();
        await LoadPageAsync(PageIndex, useCache: false, CancellationToken.None);
    }

    private async Task ApplyEuropeFilterAsync()
    {
        Orders.SetFiltering(
        [
            GeneratedRemoteOrderSchema.Region.EqualTo("Europe"),
            GeneratedRemoteOrderSchema.Total.GreaterThanOrEqual(250m)
        ]);
        await LoadPageAsync(0, useCache: false, CancellationToken.None);
    }

    private async Task SortTotalDescendingAsync()
    {
        Orders.SetSorting(
        [
            GeneratedRemoteOrderSchema.Total.Descending(),
            GeneratedRemoteOrderSchema.Id.Ascending()
        ]);
        await LoadPageAsync(0, useCache: false, CancellationToken.None);
    }

    private async Task ClearQueryAsync()
    {
        Query = string.Empty;
        Orders.ClearOperations();
        Orders.SetSorting([GeneratedRemoteOrderSchema.UpdatedAt.Descending()]);
        OrdersRemoteQuery.ClearCache();
        await LoadPageAsync(0, useCache: false, CancellationToken.None);
    }

    private async Task RunStaleScenarioAsync()
    {
        _provider.MakeNextRequestSlowAndCancellationResistant();
        Task<DataGridQueryPage<GeneratedRemoteOrder, int>?> slow = QueryPageAsync(PageIndex, useCache: false, CancellationToken.None);
        await _provider.WaitForSlowRequestAsync();
        DataGridQueryPage<GeneratedRemoteOrder, int>? accepted = await QueryPageAsync(PageIndex, useCache: false, CancellationToken.None);
        if (accepted != null)
        {
            ApplyPage(accepted, PageIndex);
        }
        DataGridQueryPage<GeneratedRemoteOrder, int>? stale = await slow;
        RemoteRevision = OrdersRemoteQuery.Revision;
        IsLoading = false;
        this.RaisePropertyChanged(nameof(StaleResponseCount));
        this.RaisePropertyChanged(nameof(CancellationCount));
        Status = stale == null
            ? $"Suppressed stale revision; accepted revision {RemoteRevision} after canceling the older request."
            : "The stale-response scenario unexpectedly accepted both pages.";
    }

    private async Task SimulateErrorAsync()
    {
        _provider.FailNextRequest();
        await LoadPageAsync(PageIndex, useCache: false, CancellationToken.None);
    }

    private Task RetryAsync() => LoadPageAsync(PageIndex, useCache: false, CancellationToken.None);

    private async Task LoadPageAsync(int pageIndex, bool useCache, CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsLoading = true;
        ViewState = DataGridGeneratedViewState.Loading;
        try
        {
            DataGridQueryPage<GeneratedRemoteOrder, int>? page =
                await QueryPageAsync(pageIndex, useCache, cancellationToken);
            if (page != null)
            {
                ApplyPage(page, pageIndex);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            ErrorMessage = error.Message;
            IsLoading = false;
            ViewState = DataGridGeneratedViewState.Error;
            this.RaisePropertyChanged(nameof(ErrorCount));
            Status = $"Remote query error {ErrorCount}: {error.Message}";
        }
    }

    private async Task<DataGridQueryPage<GeneratedRemoteOrder, int>?> QueryPageAsync(
        int pageIndex,
        bool useCache,
        CancellationToken cancellationToken)
    {
        string? cacheKey = useCache ? $"v{Orders.Version}:page:{pageIndex}:size:{PageSize}" : null;
        return await QueryOrdersAsync(
            DataGridPageRequest.FromOffset(pageIndex * PageSize, PageSize),
            cacheKey: cacheKey,
            cancellationToken: cancellationToken);
    }

    private void ApplyPage(DataGridQueryPage<GeneratedRemoteOrder, int> page, int pageIndex)
    {
        _items.Clear();
        for (int index = 0; index < page.Items.Count; index++)
        {
            _items.Add(page.Items[index]);
        }
        PageIndex = pageIndex;
        this.RaisePropertyChanged(nameof(PageNumber));
        TotalCount = page.TotalCount ?? page.Items.Count;
        HasMore = page.HasMore;
        RemoteRevision = page.Revision;
        IsLoading = false;
        ViewState = page.Items.Count == 0 ? DataGridGeneratedViewState.Empty : DataGridGeneratedViewState.Content;
        Status = $"Accepted remote revision {page.Revision}: page {pageIndex + 1}, {_items.Count} of {TotalCount} rows.";
        this.RaisePropertyChanged(nameof(RequestCount));
        this.RaisePropertyChanged(nameof(CancellationCount));
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Orders.SetSearching(Array.Empty<SearchDescriptor>());
            return;
        }
        Orders.SetSearching(
        [
            GeneratedRemoteOrderSchema.Customer.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedRemoteOrderSchema.Region.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedRemoteOrderSchema.OrderStatus.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
    }

    private void RemoteQueryOnStateChanged(object? sender, DataGridRemoteQueryStateChangedEventArgs e)
    {
        if (e.IsStale)
        {
            Interlocked.Increment(ref _staleResponseCount);
        }
        if (e.Error != null)
        {
            Interlocked.Increment(ref _errorCount);
        }
        RxSchedulers.MainThreadScheduler.Schedule(
            (ViewModel: this, Event: e),
            static state => state.ViewModel.ApplyRemoteQueryState(state.Event));
    }

    private void ApplyRemoteQueryState(DataGridRemoteQueryStateChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        RemoteRevision = Math.Max(RemoteRevision, e.Revision);
        IsLoading = e.IsLoading;
        this.RaisePropertyChanged(nameof(StaleResponseCount));
        this.RaisePropertyChanged(nameof(ErrorCount));
        this.RaisePropertyChanged(nameof(RequestCount));
        this.RaisePropertyChanged(nameof(CancellationCount));
    }

    private static string TranslateBackendField(string stableFieldId) => stableFieldId switch
    {
        "order-id" => "order_id",
        "order-customer" => "customer_name",
        "order-region" => "sales_region",
        "order-status" => "order_state",
        "order-total" => "gross_total",
        "order-updated" => "updated_utc",
        _ => stableFieldId
    };
}
