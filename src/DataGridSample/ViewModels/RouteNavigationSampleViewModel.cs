// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridNavigation;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class RouteNavigationSampleViewModel : ReactiveObject
{
    private RouteRow? _selectedItem;
    private string _status = "Click a cell or press Enter to route with its full grid context.";

    public RouteNavigationSampleViewModel()
    {
        Items = new ObservableCollection<RouteRow>
        {
            new(42, "Northwind renewal", "Customer", "Ready"),
            new(73, "Warehouse exception", "Operations", "Attention"),
            new(108, "Quarterly forecast", "Finance", "Draft"),
            new(131, "Identity rollout", "Security", "Active")
        };
        SelectedItem = Items[0];

        var resolver = new DelegateDataGridRouteResolver(context =>
            context.Item is RouteRow row
                ? new DataGridRoute($"work-items/{row.Id}", row, "details")
                : null);
        RouteNavigator = new InMemoryRouteNavigator();
        RouteNavigationModel = new DataGridRouteNavigationModel(resolver, RouteNavigator);
        RouteContextFactory = new DataGridRouteContextFactory(item => ((RouteRow)item).Id);
        NavigationInputModel = CreateRouteInputModel();
        RouteNavigationModel.NavigationChanged += (_, e) =>
            Status = $"{e.Request.Kind}: {e.Result.Status} · {e.Result.CurrentRoute.Path} · key {e.Request.Context.ItemKey} · cell {e.Request.Context.Position.RowIndex}:{e.Request.Context.Position.ColumnDisplayIndex}";

        NavigateCommand = CreateRouteCommand(DataGridRouteNavigationKind.Navigate);
        ReplaceCommand = CreateRouteCommand(DataGridRouteNavigationKind.Replace);
        ResetCommand = CreateRouteCommand(DataGridRouteNavigationKind.Reset);
        BackCommand = CreateRouteCommand(DataGridRouteNavigationKind.Back);
        ForwardCommand = CreateRouteCommand(DataGridRouteNavigationKind.Forward);
    }

    public ObservableCollection<RouteRow> Items { get; }

    public DataGridRouteNavigationModel RouteNavigationModel { get; }

    public DataGridRouteContextFactory RouteContextFactory { get; }

    public DataGridNavigationInputModel NavigationInputModel { get; }

    public InMemoryRouteNavigator RouteNavigator { get; }

    public ReactiveCommand<RxVoid, bool> NavigateCommand { get; }

    public ReactiveCommand<RxVoid, bool> ReplaceCommand { get; }

    public ReactiveCommand<RxVoid, bool> ResetCommand { get; }

    public ReactiveCommand<RxVoid, bool> BackCommand { get; }

    public ReactiveCommand<RxVoid, bool> ForwardCommand { get; }

    public RouteRow? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private ReactiveCommand<RxVoid, bool> CreateRouteCommand(DataGridRouteNavigationKind kind) =>
        ReactiveCommand.Create(() => RouteNavigationModel.RequestNavigate(kind));

    private static DataGridNavigationInputModel CreateRouteInputModel() =>
        new(
            DataGridNavigationInputBinding.Pointer(
                DataGridNavigationInputKind.PointerReleased,
                DataGridNavigationPointerButton.Primary,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate),
                targetKind: DataGridNavigationInputTargetKind.Cell),
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.Enter,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate)));

    public sealed record RouteRow(int Id, string Name, string Area, string State);

    public sealed class InMemoryRouteNavigator : ReactiveObject, IDataGridRouteNavigator
    {
        private readonly List<DataGridRoute> _history = [];
        private int _index = -1;
        private string _historySummary = "History is empty";

        public DataGridRouteNavigationCapabilities Capabilities => DataGridRouteNavigationCapabilities.All;

        public string HistorySummary
        {
            get => _historySummary;
            private set => this.RaiseAndSetIfChanged(ref _historySummary, value);
        }

        public ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromResult(DataGridRouteNavigationResult.FromStatus(
                    DataGridRouteNavigationStatus.Canceled));
            }

            switch (request.Kind)
            {
                case DataGridRouteNavigationKind.Navigate:
                    if (_index + 1 < _history.Count)
                    {
                        _history.RemoveRange(_index + 1, _history.Count - _index - 1);
                    }
                    _history.Add(request.Route);
                    _index = _history.Count - 1;
                    break;
                case DataGridRouteNavigationKind.Replace:
                    if (_index < 0)
                    {
                        _history.Add(request.Route);
                        _index = 0;
                    }
                    else
                    {
                        _history[_index] = request.Route;
                    }
                    break;
                case DataGridRouteNavigationKind.Reset:
                    _history.Clear();
                    _history.Add(request.Route);
                    _index = 0;
                    break;
                case DataGridRouteNavigationKind.Back when _index > 0:
                    _index--;
                    break;
                case DataGridRouteNavigationKind.Forward when _index + 1 < _history.Count:
                    _index++;
                    break;
                default:
                    return ValueTask.FromResult(DataGridRouteNavigationResult.FromStatus(
                        DataGridRouteNavigationStatus.NotSupported));
            }

            DataGridRoute current = _history[_index];
            HistorySummary = $"{_index + 1}/{_history.Count}: {current.Path} [{current.Target}]";
            return ValueTask.FromResult(DataGridRouteNavigationResult.Success(current));
        }
    }
}
