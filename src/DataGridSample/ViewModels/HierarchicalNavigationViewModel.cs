// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridNavigation;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class HierarchicalNavigationViewModel : ReactiveObject
{
    private string _status = "Select a node, then use semantic expand/collapse commands.";

    public HierarchicalNavigationViewModel()
    {
        NavigationTreeNode planning = new("Planning", "Portfolio");
        planning.Children.Add(new NavigationTreeNode("Roadmap", "Document"));
        planning.Children.Add(new NavigationTreeNode("Capacity", "Report"));
        NavigationTreeNode delivery = new("Delivery", "Portfolio");
        NavigationTreeNode release = new("Release 4.2", "Release");
        release.Children.Add(new NavigationTreeNode("Desktop", "Workstream"));
        release.Children.Add(new NavigationTreeNode("Mobile", "Workstream"));
        delivery.Children.Add(release);

        Model = new HierarchicalModel<NavigationTreeNode>(new HierarchicalOptions<NavigationTreeNode>
        {
            ItemsSelector = static node => node.Children,
            IsLeafSelector = static node => node.Children.Count == 0
        });
        Model.SetRoots([planning, delivery]);
        NavigationModel = new DataGridNavigationModel();
        NavigationModel.NavigationChanged += (_, e) =>
            Status = $"{e.Completed.Request.Command}: {(e.Completed.Handled ? "handled" : e.Completed.FailureReason.ToString())}";

        ExpandCommand = CreateCommand(DataGridNavigationCommand.Expand);
        CollapseCommand = CreateCommand(DataGridNavigationCommand.Collapse);
        ExpandSubtreeCommand = CreateCommand(DataGridNavigationCommand.ExpandAll);
        UpCommand = CreateCommand(DataGridNavigationCommand.Up);
        DownCommand = CreateCommand(DataGridNavigationCommand.Down);
    }

    public HierarchicalModel<NavigationTreeNode> Model { get; }

    public DataGridNavigationModel NavigationModel { get; }

    public ReactiveCommand<RxVoid, bool> ExpandCommand { get; }

    public ReactiveCommand<RxVoid, bool> CollapseCommand { get; }

    public ReactiveCommand<RxVoid, bool> ExpandSubtreeCommand { get; }

    public ReactiveCommand<RxVoid, bool> UpCommand { get; }

    public ReactiveCommand<RxVoid, bool> DownCommand { get; }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private ReactiveCommand<RxVoid, bool> CreateCommand(DataGridNavigationCommand command) =>
        ReactiveCommand.Create(() => NavigationModel.RequestNavigate(command));

    public sealed class NavigationTreeNode
    {
        public NavigationTreeNode(string name, string kind)
        {
            Name = name;
            Kind = kind;
        }

        public string Name { get; }

        public string Kind { get; }

        public ObservableCollection<NavigationTreeNode> Children { get; } = [];

        public override string ToString() => Name;
    }
}
