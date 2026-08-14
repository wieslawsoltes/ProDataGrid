// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridNavigation;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class NavigationStateViewModel : ReactiveObject
{
    private DataGridNavigationPolicyState? _capturedState;
    private string _status = "Configure policy, capture it, change it, and restore the detached snapshot.";

    public NavigationStateViewModel()
    {
        Items = new ObservableCollection<NavigationStateRow>
        {
            new(501, "Stable row A", "alpha"),
            new(502, "Stable row B", "beta"),
            new(503, "Stable row C", "gamma")
        };
        NavigationModel = new DataGridNavigationModel();
        UseSpreadsheetPolicyCommand = ReactiveCommand.Create(UseSpreadsheetPolicy);
        UseContainedPolicyCommand = ReactiveCommand.Create(UseContainedPolicy);
        CaptureCommand = ReactiveCommand.Create(Capture);
        RestoreCommand = ReactiveCommand.Create(Restore);
    }

    public ObservableCollection<NavigationStateRow> Items { get; }

    public DataGridNavigationModel NavigationModel { get; }

    public ReactiveCommand<RxVoid, RxVoid> UseSpreadsheetPolicyCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> UseContainedPolicyCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CaptureCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RestoreCommand { get; }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private void UseSpreadsheetPolicy()
    {
        NavigationModel.HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap;
        NavigationModel.VerticalBoundaryMode = DataGridNavigationBoundaryMode.Wrap;
        NavigationModel.TabNavigationMode = DataGridTabNavigationMode.Always;
        NavigationModel.TabBoundaryMode = DataGridNavigationBoundaryMode.Wrap;
        Publish("Spreadsheet policy applied");
    }

    private void UseContainedPolicy()
    {
        NavigationModel.HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Contained;
        NavigationModel.VerticalBoundaryMode = DataGridNavigationBoundaryMode.Contained;
        NavigationModel.TabNavigationMode = DataGridTabNavigationMode.EditingOnly;
        NavigationModel.TabBoundaryMode = DataGridNavigationBoundaryMode.Exit;
        Publish("Contained policy applied");
    }

    private void Capture()
    {
        _capturedState = NavigationModel.CaptureState();
        Publish("Policy snapshot captured");
    }

    private void Restore()
    {
        if (_capturedState == null)
        {
            Status = "Capture a policy snapshot first.";
            return;
        }

        NavigationModel.RestoreState(_capturedState);
        Publish("Policy snapshot restored");
    }

    private void Publish(string action)
    {
        Status = $"{action}: horizontal={NavigationModel.HorizontalBoundaryMode}, vertical={NavigationModel.VerticalBoundaryMode}, tab={NavigationModel.TabNavigationMode}/{NavigationModel.TabBoundaryMode}";
    }

    public sealed record NavigationStateRow(int Id, string Name, string StableKey);
}
