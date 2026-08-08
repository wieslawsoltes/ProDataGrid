// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Models;
using DataGridSample.Pages;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedEventCommandRow), ProviderName = "GeneratedEventCommandSchema")]
[GenerateDataGridView(
    typeof(GeneratedEventCommandRow),
    ViewName = "GeneratedReactiveEventCommandsPage",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.GridOnly,
    Title = "Generated ReactiveUI routed-event commands",
    AutomationId = "generated-reactive-event-commands-grid",
    RoutedEvents = DataGridGeneratedViewEventKinds.All,
    RoutedEventCommandPropertyName = nameof(GridEventCommand),
    InteractionPropertyNames = [nameof(InspectGeneratedGrid)],
    InteractionHandlerTypes = [typeof(GeneratedGridEventInteractionHandler)])]
public sealed partial class GeneratedReactiveEventCommandsViewModel : ReactiveObject
{
    private readonly ObservableCollection<GeneratedEventCommandRow> _items;

    [Reactive]
    private string _lastEvent = "Select, sort, or edit a row to execute the generated command bridge.";

    [Reactive]
    private int _eventCount;

    [Reactive]
    private bool _cancelPendingEdits;

    [Reactive]
    private bool _handleSortingRequests;

    public GeneratedReactiveEventCommandsViewModel()
    {
        _items =
        [
            new GeneratedEventCommandRow { Id = 101, Symbol = "AVLN", Desk = "Warsaw", Price = 128.40m },
            new GeneratedEventCommandRow { Id = 102, Symbol = "RXUI", Desk = "London", Price = 94.15m },
            new GeneratedEventCommandRow { Id = 103, Symbol = "GRID", Desk = "New York", Price = 211.75m }
        ];
        GridEventCommand = ReactiveCommand.Create<DataGridGeneratedViewEvent<GeneratedEventCommandRow>>(HandleGridEvent);
    }

    public ObservableCollection<GeneratedEventCommandRow> Items => _items;

    public ReactiveCommand<DataGridGeneratedViewEvent<GeneratedEventCommandRow>, RxVoid> GridEventCommand { get; }

    public Interaction<GeneratedEventCommandRow, string> InspectGeneratedGrid { get; } = new();

    public DataGridGeneratedViewEvent<GeneratedEventCommandRow>? LastEventData { get; private set; }

    private void HandleGridEvent(DataGridGeneratedViewEvent<GeneratedEventCommandRow> eventData)
    {
        LastEventData = eventData;
        EventCount++;
        LastEvent = eventData.Kind.ToString();

        GeneratedEventCommandRow? eventItem = eventData.Item ?? eventData.NewItem;
        if (eventItem is null && eventData.AddedItems.Count != 0)
        {
            eventItem = eventData.AddedItems[0];
        }
        eventItem ??= _items.Count == 0 ? null : _items[0];
        if (eventItem is not null)
        {
            eventItem.LastEvent = $"{eventData.Kind} #{EventCount}";
        }

        if (CancelPendingEdits &&
            (eventData.Kind & DataGridGeneratedViewEventKinds.Editing) != 0)
        {
            eventData.Cancel = true;
        }

        if (HandleSortingRequests && eventData.Kind == DataGridGeneratedViewEventKinds.Sorting)
        {
            eventData.Handled = true;
        }
    }
}
