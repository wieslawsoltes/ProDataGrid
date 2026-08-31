using System;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace Avalonia.Automation.Peers
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowAutomationPeer : ControlAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider
    {
        private readonly DataGridRow _row;
        private HierarchicalNode? _node;
        private ExpandCollapseState _lastExpandCollapseState;
        private bool _lastIsSelected;

        public DataGridRowAutomationPeer(DataGridRow owner)
            : base(owner)
        {
            _row = owner;
            _lastIsSelected = owner.IsSelected;
            _row.PropertyChanged += OnRowPropertyChanged;
            AttachNode(owner.DataContext as HierarchicalNode);
        }

        /// <inheritdoc />
        public ExpandCollapseState ExpandCollapseState =>
            TryGetActiveNode(out _, out HierarchicalNode? node)
                ? GetExpandCollapseState(node)
                : ExpandCollapseState.LeafNode;

        /// <inheritdoc />
        public bool ShowsMenu => false;

        /// <inheritdoc />
        public bool IsSelected =>
            TryGetSelectableRow(out _) && _row.IsSelected;

        /// <inheritdoc />
        public ISelectionProvider? SelectionContainer
        {
            get
            {
                if (TryGetSelectableRow(out DataGrid? grid))
                {
                    return GetOrCreate(grid!).GetProvider<ISelectionProvider>();
                }

                return null;
            }
        }

        /// <inheritdoc />
        public void Expand()
        {
            EnsureEnabled();
            if (TryGetActiveNode(out DataGrid? grid, out HierarchicalNode? node))
            {
                grid!.HierarchicalModel.Expand(node!);
            }
        }

        /// <inheritdoc />
        public void Collapse()
        {
            EnsureEnabled();
            if (TryGetActiveNode(out DataGrid? grid, out HierarchicalNode? node))
            {
                grid!.HierarchicalModel.Collapse(node!);
            }
        }

        /// <inheritdoc />
        public void AddToSelection()
        {
            EnsureEnabled();
            if (TryGetSelectableRow(out _))
            {
                _row.IsSelected = true;
            }
        }

        /// <inheritdoc />
        public void RemoveFromSelection()
        {
            EnsureEnabled();
            if (TryGetSelectableRow(out _))
            {
                _row.IsSelected = false;
            }
        }

        /// <inheritdoc />
        public void Select()
        {
            EnsureEnabled();
            if (TryGetSelectableRow(out DataGrid? grid))
            {
                grid!.SelectRowFromAutomation(_row.Slot);
            }
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return TryGetOwnedNode(out _, out _)
                ? AutomationControlType.TreeItem
                : AutomationControlType.DataItem;
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(IExpandCollapseProvider) &&
                _row.OwningGrid?.HierarchicalModel is null)
            {
                return null;
            }

            if (providerType == typeof(ISelectionItemProvider) &&
                (_row.OwningGrid is not DataGrid selectionGrid ||
                 !DataGridAutomationPeer.SupportsRowSelection(selectionGrid.SelectionUnit)))
            {
                return null;
            }

            return base.GetProviderCore(providerType);
        }

        private bool TryGetSelectableRow(out DataGrid? grid)
        {
            grid = _row.OwningGrid;
            return grid != null &&
                _row.Slot >= 0 &&
                _row.DataContext != null &&
                !ReferenceEquals(_row.DataContext, DataGridCollectionView.NewItemPlaceholder) &&
                DataGridAutomationPeer.SupportsRowSelection(grid.SelectionUnit);
        }

        private bool TryGetActiveNode(out DataGrid? grid, out HierarchicalNode? node)
        {
            return TryGetOwnedNode(out grid, out node) && node is { IsLeaf: false };
        }

        private bool TryGetOwnedNode(out DataGrid? grid, out HierarchicalNode? node)
        {
            grid = _row.OwningGrid;
            node = _node;
            return node != null &&
                grid?.HierarchicalModel is { } model &&
                IsNodeOwnedByModel(node, model);
        }

        private static bool IsNodeOwnedByModel(
            HierarchicalNode node,
            IHierarchicalModel model)
        {
            // The built-in model has an exact owner identity. Custom IHierarchicalModel
            // implementations cannot assign that internal property, so preserve the public
            // interface contract by also validating the node's root identity.
            if (node.Owner != null)
            {
                return ReferenceEquals(node.Owner, model);
            }

            HierarchicalNode root = node;
            while (root.Parent != null)
            {
                root = root.Parent;
            }

            return ReferenceEquals(root, model.Root);
        }

        private static ExpandCollapseState GetExpandCollapseState(HierarchicalNode? node)
        {
            if (node == null || node.IsLeaf)
            {
                return ExpandCollapseState.LeafNode;
            }

            if (node.IsLoading)
            {
                return ExpandCollapseState.PartiallyExpanded;
            }

            return node.IsExpanded
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed;
        }

        private void OnRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == StyledElement.DataContextProperty)
            {
                AttachNode(e.NewValue as HierarchicalNode);
                RaiseSelectionChanges();
            }
            else if (e.Property == DataGridRow.IsSelectedProperty)
            {
                RaiseSelectionChanges();
            }
        }

        private void AttachNode(HierarchicalNode? node)
        {
            ExpandCollapseState oldState = _lastExpandCollapseState;
            if (_node != null)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridRowAutomationPeer>(
                    _node,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    OnNodePropertyChanged);
            }

            _node = node;
            if (_node != null)
            {
                WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, DataGridRowAutomationPeer>(
                    _node,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    OnNodePropertyChanged);
            }

            _lastExpandCollapseState = GetExpandCollapseState(_node);
            RaiseExpandCollapseChanges(oldState, _lastExpandCollapseState);
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            DataGrid? grid = _row.OwningGrid;
            if (grid != null
                ? !grid.HasModelCallbackThreadAccess()
                : !Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => HandleNodePropertyChanged(sender, e));
                return;
            }

            HandleNodePropertyChanged(sender, e);
        }

        private void HandleNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // A row can be recycled while a worker-thread notification is queued. Do not
            // project the old node's state onto the automation identity of the new row.
            if (!ReferenceEquals(sender, _node))
            {
                return;
            }

            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(HierarchicalNode.IsExpanded) &&
                e.PropertyName != nameof(HierarchicalNode.IsLeaf) &&
                e.PropertyName != nameof(HierarchicalNode.IsLoading))
            {
                return;
            }

            ExpandCollapseState newState = GetExpandCollapseState(_node);
            ExpandCollapseState oldState = _lastExpandCollapseState;
            _lastExpandCollapseState = newState;
            RaiseExpandCollapseChanges(oldState, newState);
        }

        private void RaiseSelectionChanges()
        {
            bool newValue = IsSelected;
            bool oldValue = _lastIsSelected;
            _lastIsSelected = newValue;
            if (oldValue != newValue)
            {
                RaisePropertyChangedEvent(
                    SelectionItemPatternIdentifiers.IsSelectedProperty,
                    oldValue,
                    newValue);
            }
        }

        private void RaiseExpandCollapseChanges(ExpandCollapseState oldState, ExpandCollapseState newState)
        {
            if (oldState == newState)
            {
                return;
            }

            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldState,
                newState);
        }
    }
}
