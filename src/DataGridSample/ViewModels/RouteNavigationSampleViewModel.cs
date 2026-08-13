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
    private string _status = "Select a row, then navigate.";

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
        RouteNavigationModel.NavigationChanged += (_, e) =>
            Status = $"{e.Request.Kind}: {e.Result.Status} · {e.Result.CurrentRoute.Path}";

        NavigateCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Navigate));
        ReplaceCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Replace));
        ResetCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Reset));
        BackCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Back));
        ForwardCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Forward));
    }

    public ObservableCollection<RouteRow> Items { get; }

    public DataGridRouteNavigationModel RouteNavigationModel { get; }

    public InMemoryRouteNavigator RouteNavigator { get; }

    public ReactiveCommand<RxVoid, RxVoid> NavigateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReplaceCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> BackCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ForwardCommand { get; }

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

    private async Task NavigateAsync(DataGridRouteNavigationKind kind)
    {
        DataGridRouteContext context = kind is DataGridRouteNavigationKind.Back or DataGridRouteNavigationKind.Forward
            ? DataGridRouteContext.Empty
            : new DataGridRouteContext(
                SelectedItem,
                SelectedItem?.Id,
                "name",
                DataGridNavigationPosition.Unset,
                DataGridRouteNavigationOrigin.Command,
                SelectedItem != null);
        await RouteNavigationModel.NavigateAsync(kind, context);
    }

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
