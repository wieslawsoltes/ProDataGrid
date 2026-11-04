using System;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class VirtualSurfaceDataOperationsViewModel : ReactiveObject
{
    private readonly object _filterColumnId;
    private readonly string _filterPropertyPath;
    private string _searchQuery = string.Empty;
    private string _ownerFilterText = string.Empty;
    private bool _searchHighlightingEnabled;

    public VirtualSurfaceDataOperationsViewModel(object filterColumnId, string filterPropertyPath)
    {
        _filterColumnId = filterColumnId ?? throw new ArgumentNullException(nameof(filterColumnId));
        _filterPropertyPath = string.IsNullOrWhiteSpace(filterPropertyPath)
            ? throw new ArgumentException("A filter property path is required.", nameof(filterPropertyPath))
            : filterPropertyPath;

        SortingModel = new SortingModel();
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SearchModel = new SearchModel
        {
            HighlightMode = SearchHighlightMode.None,
            HighlightCurrent = false,
            WrapNavigation = true,
            UpdateSelectionOnNavigate = true,
        };
        FastPathOptions = new DataGridFastPathOptions
        {
            UseAccessorsOnly = true,
            ThrowOnMissingAccessor = true,
            EnableHighPerformanceSearching = true,
            HighPerformanceSearchTrackItemChanges = false,
        };

        ApplySearchCommand = ReactiveCommand.Create(ApplySearch);
        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);
        PreviousSearchResultCommand = ReactiveCommand.Create(() => SearchModel.MovePrevious());
        NextSearchResultCommand = ReactiveCommand.Create(() => SearchModel.MoveNext());
        ApplyOwnerFilterCommand = ReactiveCommand.Create(ApplyOwnerFilter);
        ClearOwnerFilterCommand = ReactiveCommand.Create(ClearOwnerFilter);
        ClearSortingCommand = ReactiveCommand.Create(ClearSorting);

        SortingModel.SortingChanged += (_, _) => RaiseOperationStateChanged();
        FilteringModel.FilteringChanged += (_, _) => RaiseOperationStateChanged();
        SearchModel.ResultsChanged += (_, _) => RaiseOperationStateChanged();
        SearchModel.CurrentChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(SearchResultSummary));
            this.RaisePropertyChanged(nameof(OperationSummary));
        };
    }

    public SortingModel SortingModel { get; }

    public FilteringModel FilteringModel { get; }

    public SearchModel SearchModel { get; }

    public DataGridFastPathOptions FastPathOptions { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set => this.RaiseAndSetIfChanged(ref _searchQuery, value ?? string.Empty);
    }

    public string OwnerFilterText
    {
        get => _ownerFilterText;
        set => this.RaiseAndSetIfChanged(ref _ownerFilterText, value ?? string.Empty);
    }

    public bool SearchHighlightingEnabled
    {
        get => _searchHighlightingEnabled;
        set
        {
            if (_searchHighlightingEnabled == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _searchHighlightingEnabled, value);
            SearchModel.HighlightMode = value
                ? SearchHighlightMode.TextAndCell
                : SearchHighlightMode.None;
            SearchModel.HighlightCurrent = value;
            this.RaisePropertyChanged(nameof(RenderingSummary));
        }
    }

    public string SearchResultSummary => SearchModel.Results.Count == 0
        ? "No search results"
        : $"{Math.Max(0, SearchModel.CurrentIndex + 1):n0} of {SearchModel.Results.Count:n0} matches";

    public string OperationSummary =>
        $"Sort keys: {SortingModel.Descriptors.Count:n0} · " +
        $"Filters: {FilteringModel.Descriptors.Count:n0} · {SearchResultSummary}";

    public string RenderingSummary =>
        SearchModel.Descriptors.Count > 0 && SearchHighlightingEnabled
            ? "Search highlighting uses the retained compatibility renderer. Disable highlights to restore the fastest rowless surface."
            : "Sorting, filtering, and non-highlighted search keep the fastest rowless virtual surface active.";

    public ReactiveCommand<RxVoid, RxVoid> ApplySearchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearSearchCommand { get; }

    public ReactiveCommand<RxVoid, bool> PreviousSearchResultCommand { get; }

    public ReactiveCommand<RxVoid, bool> NextSearchResultCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyOwnerFilterCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearOwnerFilterCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearSortingCommand { get; }

    private void ApplySearch()
    {
        ApplySearchPresentation();
        string query = SearchQuery.Trim();
        if (query.Length == 0)
        {
            SearchModel.Clear();
        }
        else
        {
            SearchModel.SetOrUpdate(new SearchDescriptor(
                query,
                matchMode: SearchMatchMode.Contains,
                termMode: SearchTermCombineMode.Any,
                scope: SearchScope.VisibleColumns,
                comparison: StringComparison.OrdinalIgnoreCase,
                normalizeWhitespace: true));
        }

        RaiseOperationStateChanged();
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        SearchModel.Clear();
        RaiseOperationStateChanged();
    }

    private void ApplySearchPresentation()
    {
        SearchModel.HighlightMode = SearchHighlightingEnabled
            ? SearchHighlightMode.TextAndCell
            : SearchHighlightMode.None;
        SearchModel.HighlightCurrent = SearchHighlightingEnabled;
        SearchModel.UpdateSelectionOnNavigate = true;
        SearchModel.WrapNavigation = true;
    }

    private void ApplyOwnerFilter()
    {
        string value = OwnerFilterText.Trim();
        if (value.Length == 0)
        {
            FilteringModel.Remove(_filterColumnId);
        }
        else
        {
            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: _filterColumnId,
                @operator: FilteringOperator.Contains,
                propertyPath: _filterPropertyPath,
                value: value,
                stringComparison: StringComparison.OrdinalIgnoreCase));
        }

        RaiseOperationStateChanged();
    }

    private void ClearOwnerFilter()
    {
        OwnerFilterText = string.Empty;
        FilteringModel.Remove(_filterColumnId);
        RaiseOperationStateChanged();
    }

    private void ClearSorting()
    {
        SortingModel.Clear();
        RaiseOperationStateChanged();
    }

    private void RaiseOperationStateChanged()
    {
        this.RaisePropertyChanged(nameof(SearchResultSummary));
        this.RaisePropertyChanged(nameof(OperationSummary));
        this.RaisePropertyChanged(nameof(RenderingSummary));
    }
}
