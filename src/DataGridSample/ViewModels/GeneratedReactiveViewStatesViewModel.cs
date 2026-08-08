using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedTrade), ProviderName = "GeneratedTradeSchema")]
[GenerateDataGridView(
    typeof(GeneratedTrade),
    ViewName = "GeneratedReactiveViewStatesPage",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.GridOnly,
    Title = "Generated ReactiveUI view states",
    AutomationId = "generated-reactive-view-states-grid",
    ViewStatePropertyName = nameof(ViewState),
    ErrorMessagePropertyName = nameof(ErrorMessage),
    RetryCommandPropertyName = nameof(RetryCommand),
    LoadingText = "Loading generated trades…",
    EmptyText = "No generated trades are available.",
    ErrorText = "Generated trades could not be loaded.",
    RetryText = "Load sample trades")]
public sealed partial class GeneratedReactiveViewStatesViewModel : ReactiveObject
{
    private readonly ObservableCollection<GeneratedTrade> _items = [];

    [Reactive]
    private DataGridGeneratedViewState _viewState = DataGridGeneratedViewState.Error;

    [Reactive]
    private string? _errorMessage = "This simulated failure demonstrates a generated error projection and retry command.";

    public GeneratedReactiveViewStatesViewModel()
    {
        RetryCommand = ReactiveCommand.Create(LoadSampleTrades);
    }

    public ObservableCollection<GeneratedTrade> Items => _items;

    public ReactiveCommand<RxVoid, RxVoid> RetryCommand { get; }

    private void LoadSampleTrades()
    {
        ViewState = DataGridGeneratedViewState.Loading;
        ErrorMessage = null;
        _items.Clear();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _items.Add(new GeneratedTrade { Id = 1, Symbol = "AVLN", Desk = "Warsaw", Price = 128.40m, Quantity = 750, Timestamp = now });
        _items.Add(new GeneratedTrade { Id = 2, Symbol = "RXUI", Desk = "London", Price = 94.15m, Quantity = 1200, Timestamp = now.AddSeconds(1) });
        _items.Add(new GeneratedTrade { Id = 3, Symbol = "GRID", Desk = "New York", Price = 211.75m, Quantity = 425, Timestamp = now.AddSeconds(2) });

        ViewState = DataGridGeneratedViewState.Content;
    }
}
