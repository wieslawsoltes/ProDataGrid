// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Input;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class NavigationModelSampleViewModel : ReactiveObject
{
    private string _status = "Focus a cell or use a command to begin.";
    private FlowDirection _flowDirection = FlowDirection.LeftToRight;

    public NavigationModelSampleViewModel()
    {
        Items = new ObservableCollection<NavigationRow>
        {
            new(1001, "Atlas", "Planning", 0.82, true),
            new(1002, "Borealis", "Active", 0.64, true),
            new(1003, "Cirrus", "Review", 0.91, false),
            new(1004, "Dorado", "Blocked", 0.35, true),
            new(1005, "Equinox", "Active", 0.73, true),
            new(1006, "Fjord", "Planning", 0.48, false),
            new(1007, "Gemini", "Review", 0.57, true),
            new(1008, "Helios", "Active", 0.88, true)
        };

        NavigationModel = new DataGridNavigationModel();
        NavigationModel.NavigationChanging += (_, e) =>
            Status = $"Resolving {e.Request.Command} from {e.Request.CurrentPosition.RowIndex}:{e.Request.CurrentPosition.ColumnDisplayIndex}";
        NavigationModel.NavigationChanged += (_, e) =>
        {
            DataGridNavigationCompleted completed = e.Completed;
            Status = completed.Moved
                ? $"{completed.Request.Command}: {completed.OldPosition.RowIndex}:{completed.OldPosition.ColumnDisplayIndex} → {completed.NewPosition.RowIndex}:{completed.NewPosition.ColumnDisplayIndex}"
                : $"{completed.Request.Command}: {completed.FailureReason}";
        };

        UpCommand = CreateNavigationCommand(DataGridNavigationCommand.Up);
        DownCommand = CreateNavigationCommand(DataGridNavigationCommand.Down);
        LeftCommand = CreateNavigationCommand(DataGridNavigationCommand.Left);
        RightCommand = CreateNavigationCommand(DataGridNavigationCommand.Right);
        PageUpCommand = CreateNavigationCommand(DataGridNavigationCommand.PageUp);
        PageDownCommand = CreateNavigationCommand(DataGridNavigationCommand.PageDown);
        RowStartCommand = CreateNavigationCommand(DataGridNavigationCommand.RowStart);
        RowEndCommand = CreateNavigationCommand(DataGridNavigationCommand.RowEnd);
        GridStartCommand = CreateNavigationCommand(DataGridNavigationCommand.GridStart);
        GridEndCommand = CreateNavigationCommand(DataGridNavigationCommand.GridEnd);
        NextCommand = CreateNavigationCommand(DataGridNavigationCommand.Next);
        PreviousCommand = CreateNavigationCommand(DataGridNavigationCommand.Previous);
        BeginEditCommand = CreateNavigationCommand(DataGridNavigationCommand.BeginEdit);
        CancelEditCommand = CreateNavigationCommand(DataGridNavigationCommand.CancelEdit);
        ExtendDownCommand = ReactiveCommand.Create(() =>
            NavigationModel.RequestNavigate(DataGridNavigationCommand.Down, KeyModifiers.Shift));
    }

    public ObservableCollection<NavigationRow> Items { get; }

    public DataGridNavigationModel NavigationModel { get; }

    public ReactiveCommand<RxVoid, bool> UpCommand { get; }

    public ReactiveCommand<RxVoid, bool> DownCommand { get; }

    public ReactiveCommand<RxVoid, bool> LeftCommand { get; }

    public ReactiveCommand<RxVoid, bool> RightCommand { get; }

    public ReactiveCommand<RxVoid, bool> PageUpCommand { get; }

    public ReactiveCommand<RxVoid, bool> PageDownCommand { get; }

    public ReactiveCommand<RxVoid, bool> RowStartCommand { get; }

    public ReactiveCommand<RxVoid, bool> RowEndCommand { get; }

    public ReactiveCommand<RxVoid, bool> GridStartCommand { get; }

    public ReactiveCommand<RxVoid, bool> GridEndCommand { get; }

    public ReactiveCommand<RxVoid, bool> NextCommand { get; }

    public ReactiveCommand<RxVoid, bool> PreviousCommand { get; }

    public ReactiveCommand<RxVoid, bool> BeginEditCommand { get; }

    public ReactiveCommand<RxVoid, bool> CancelEditCommand { get; }

    public ReactiveCommand<RxVoid, bool> ExtendDownCommand { get; }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public FlowDirection FlowDirection
    {
        get => _flowDirection;
        set => this.RaiseAndSetIfChanged(ref _flowDirection, value);
    }

    private ReactiveCommand<RxVoid, bool> CreateNavigationCommand(DataGridNavigationCommand command) =>
        ReactiveCommand.Create(() => NavigationModel.RequestNavigate(command));

    public sealed class NavigationRow
    {
        public NavigationRow(int id, string name, string status, double progress, bool enabled)
        {
            Id = id;
            Name = name;
            Status = status;
            Progress = progress;
            Enabled = enabled;
        }

        public int Id { get; set; }

        public string Name { get; set; }

        public string Status { get; set; }

        public double Progress { get; set; }

        public bool Enabled { get; set; }
    }
}
