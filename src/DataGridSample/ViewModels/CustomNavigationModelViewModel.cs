// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridNavigation;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class CustomNavigationModelViewModel : ReactiveObject
{
    private string _status = "Down from row 1 skips the protected row; Right is blocked in the final column.";

    public CustomNavigationModelViewModel()
    {
        Items = new ObservableCollection<CustomNavigationRow>
        {
            new(1, "Inbox", "Normal"),
            new(2, "Protected approval", "Protected"),
            new(3, "Archive", "Normal"),
            new(4, "Audit", "Normal")
        };
        NavigationModel = new GuardedNavigationModel();
        NavigationModel.NavigationChanged += (_, e) =>
            Status = e.Completed.Moved
                ? $"{e.Completed.Request.Command}: moved to {e.Completed.NewPosition.RowIndex}:{e.Completed.NewPosition.ColumnDisplayIndex}"
                : $"{e.Completed.Request.Command}: {e.Completed.FailureReason}";
        DownCommand = ReactiveCommand.Create(() =>
            NavigationModel.RequestNavigate(DataGridNavigationCommand.Down));
        RightCommand = ReactiveCommand.Create(() =>
            NavigationModel.RequestNavigate(DataGridNavigationCommand.Right));
    }

    public ObservableCollection<CustomNavigationRow> Items { get; }

    public GuardedNavigationModel NavigationModel { get; }

    public ReactiveCommand<RxVoid, bool> DownCommand { get; }

    public ReactiveCommand<RxVoid, bool> RightCommand { get; }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public sealed class GuardedNavigationModel : DataGridNavigationModel
    {
        protected override DataGridNavigationResult ResolveCore(DataGridNavigationRequest request)
        {
            if (request.Command == DataGridNavigationCommand.Down &&
                request.CurrentPosition.RowIndex == 0 &&
                request.LastRowIndex >= 2)
            {
                return DataGridNavigationResult.MoveTo(new DataGridNavigationPosition(
                    rowIndex: 2,
                    request.CurrentPosition.ColumnDisplayIndex));
            }

            if (request.Command == DataGridNavigationCommand.Right &&
                request.CurrentPosition.ColumnDisplayIndex == request.LastColumnDisplayIndex)
            {
                return DataGridNavigationResult.Cancel();
            }

            return base.ResolveCore(request);
        }
    }

    public sealed record CustomNavigationRow(int Id, string Name, string Access);
}
