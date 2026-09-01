// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Threading;

namespace Avalonia.Controls.Automation.Peers;

/// <summary>
/// Represents a selected data-grid row that is outside the realized visual range.
/// </summary>
/// <remarks>
/// The peer deliberately does not create a <see cref="DataGridRow"/> or any cell visuals.
/// It projects selection and hierarchy operations directly to the owning grid and model.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridUnrealizedRowAutomationPeer : UnrealizedElementAutomationPeer,
    IExpandCollapseProvider,
    ISelectionItemProvider
{
    private readonly DataGridAutomationPeer _owner;
    private object _item;
    private HierarchicalNode? _node;
    private NodeSubscription? _nodeSubscription;
    private int _rowIndex;
    private ExpandCollapseState _lastExpandCollapseState;
    private bool _lastIsSelected;
    private bool _isExpandCollapseProviderExposed;

    internal DataGridUnrealizedRowAutomationPeer(
        DataGridAutomationPeer owner,
        object item,
        int rowIndex)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _rowIndex = rowIndex;
        AttachNode(item as HierarchicalNode);
        _lastIsSelected = IsSelected;
    }

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState =>
        TryGetActiveNode(out HierarchicalNode? node)
            ? GetExpandCollapseState(node)
            : ExpandCollapseState.LeafNode;

    /// <inheritdoc />
    public bool ShowsMenu => false;

    /// <inheritdoc />
    public bool IsSelected =>
        TryGetCurrentRowIndex(out int rowIndex) &&
        _owner.Owner.GetRowSelection(_owner.Owner.SlotFromRowIndex(rowIndex));

    /// <inheritdoc />
    public ISelectionProvider? SelectionContainer =>
        _owner.GetProvider<ISelectionProvider>();

    internal int RowIndex => _rowIndex;

    internal bool IsClaimed { get; set; }

    /// <inheritdoc />
    public void Expand()
    {
        EnsureEnabled();
        if (TryGetActiveNode(out HierarchicalNode? node))
        {
            _owner.Owner.HierarchicalModel.Expand(node!);
        }
    }

    /// <inheritdoc />
    public void Collapse()
    {
        EnsureEnabled();
        if (TryGetActiveNode(out HierarchicalNode? node))
        {
            _owner.Owner.HierarchicalModel.Collapse(node!);
        }
    }

    /// <inheritdoc />
    public void AddToSelection()
    {
        EnsureEnabled();
        SetSelected(true);
    }

    /// <inheritdoc />
    public void RemoveFromSelection()
    {
        EnsureEnabled();
        SetSelected(false);
    }

    /// <inheritdoc />
    public void Select()
    {
        EnsureEnabled();
        if (TryGetCurrentRowIndex(out int rowIndex))
        {
            _owner.Owner.SelectRowFromAutomation(_owner.Owner.SlotFromRowIndex(rowIndex));
        }
    }

    internal bool MatchesItem(object item)
    {
        if (ReferenceEquals(_item, item))
        {
            return true;
        }

        Type itemType = item.GetType();
        return (itemType.IsValueType || item is string) && _item.Equals(item);
    }

    internal void Update(object item, int rowIndex)
    {
        string? oldName = GetNameCore();
        HierarchicalNode? newNode = item as HierarchicalNode;
        if (!ReferenceEquals(_node, newNode))
        {
            AttachNode(newNode);
        }

        _item = item;
        _rowIndex = rowIndex;

        string? newName = GetNameCore();
        if (!string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            RaisePropertyChangedEvent(
                AutomationElementIdentifiers.NameProperty,
                oldName,
                newName);
        }

        RaiseSelectionChanges();
    }

    internal void OnOwnerSelectionChanged()
    {
        RaiseSelectionChanges();
    }

    /// <summary>
    /// Releases subscriptions owned by a peer that is no longer cached by the grid peer.
    /// </summary>
    /// <remarks>
    /// Automation clients may continue to hold this peer after it leaves the cache, so the
    /// projected item identity is preserved. Only event-source ownership is released.
    /// </remarks>
    internal void Release()
    {
        _nodeSubscription?.Dispose();
        _nodeSubscription = null;
    }

    protected override void BringIntoViewCore()
    {
        _owner.Owner.BringRowIntoViewForAutomation(_item);
    }

    protected override string? GetAcceleratorKeyCore() => null;

    protected override string? GetAccessKeyCore() => null;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        TryGetOwnedNode(out _)
            ? AutomationControlType.TreeItem
            : AutomationControlType.DataItem;

    protected override string? GetAutomationIdCore() => null;

    protected override string GetClassNameCore() => nameof(DataGridRow);

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string? GetNameCore()
    {
        object item = _node?.Item ?? _item;
        if (item is Control control)
        {
            string? name = AutomationProperties.GetName(control);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return item.ToString();
    }

    protected override AutomationPeer? GetParentCore() => _owner;

    protected override bool IsEnabledCore() => _owner.Owner.IsEnabled;

    protected override bool IsOffscreenCore() => true;

    protected override object? GetProviderCore(Type providerType)
    {
        if (providerType == typeof(IExpandCollapseProvider))
        {
            if (!_isExpandCollapseProviderExposed &&
                !TryGetActiveNode(out _))
            {
                return null;
            }

            _isExpandCollapseProviderExposed = true;
        }

        if (providerType == typeof(ISelectionItemProvider) &&
            (!TryGetCurrentRowIndex(out _) ||
             !DataGridAutomationPeer.SupportsRowSelection(_owner.Owner.SelectionUnit)))
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    private void SetSelected(bool value)
    {
        if (!TryGetCurrentRowIndex(out int rowIndex))
        {
            return;
        }

        DataGrid grid = _owner.Owner;
        int slot = grid.SlotFromRowIndex(rowIndex);
        using var origin = grid.BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
        if (!grid.TryPreviewSetRowSelection(slot, value, setAnchorSlot: false))
        {
            return;
        }

        using var commit = grid.BeginSelectionCommit();
        grid.SetRowSelection(slot, value, setAnchorSlot: false);
    }

    private bool TryGetCurrentRowIndex(out int rowIndex)
    {
        DataGrid grid = _owner.Owner;
        rowIndex = _rowIndex;
        if (rowIndex >= 0 &&
            rowIndex < grid.DataConnection.Count &&
            MatchesItem(grid.DataConnection.GetDataItem(rowIndex)))
        {
            return true;
        }

        rowIndex = grid.DataConnection.IndexOf(_item);
        if (rowIndex >= 0)
        {
            _rowIndex = rowIndex;
            return true;
        }

        return false;
    }

    private bool TryGetActiveNode(out HierarchicalNode? node)
    {
        return TryGetOwnedNode(out node) && node is { IsLeaf: false };
    }

    private bool TryGetOwnedNode(out HierarchicalNode? node)
    {
        node = _node;
        return node != null &&
            _owner.Owner.HierarchicalModel is { } model &&
            IsNodeOwnedByModel(node, model);
    }

    private static bool IsNodeOwnedByModel(
        HierarchicalNode node,
        IHierarchicalModel model)
    {
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

    private void AttachNode(HierarchicalNode? node)
    {
        ExpandCollapseState oldState = _lastExpandCollapseState;
        Release();

        _node = node;
        if (_node != null)
        {
            _nodeSubscription = new NodeSubscription(_node, this);
        }

        _lastExpandCollapseState = GetExpandCollapseState(_node);
        RaiseExpandCollapseChanges(oldState, _lastExpandCollapseState);
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_owner.Owner.HasModelCallbackThreadAccess())
        {
            Dispatcher.UIThread.Post(() => HandleNodePropertyChanged(sender, e));
            return;
        }

        HandleNodePropertyChanged(sender, e);
    }

    private void HandleNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _node) ||
            (!string.IsNullOrEmpty(e.PropertyName) &&
             e.PropertyName != nameof(HierarchicalNode.IsExpanded) &&
             e.PropertyName != nameof(HierarchicalNode.IsLeaf) &&
             e.PropertyName != nameof(HierarchicalNode.IsLoading)))
        {
            return;
        }

        ExpandCollapseState newState = TryGetOwnedNode(out HierarchicalNode? node)
            ? GetExpandCollapseState(node)
            : ExpandCollapseState.LeafNode;
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

    private void RaiseExpandCollapseChanges(
        ExpandCollapseState oldState,
        ExpandCollapseState newState)
    {
        if (oldState != newState)
        {
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldState,
                newState);
        }
    }

    private sealed class NodeSubscription : IDisposable
    {
        private readonly WeakReference<DataGridUnrealizedRowAutomationPeer> _subscriber;
        private HierarchicalNode? _node;

        public NodeSubscription(
            HierarchicalNode node,
            DataGridUnrealizedRowAutomationPeer subscriber)
        {
            _node = node;
            _subscriber = new WeakReference<DataGridUnrealizedRowAutomationPeer>(subscriber);
            node.PropertyChanged += OnPropertyChanged;
        }

        public void Dispose()
        {
            HierarchicalNode? node = _node;
            if (node != null)
            {
                node.PropertyChanged -= OnPropertyChanged;
                _node = null;
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_subscriber.TryGetTarget(out DataGridUnrealizedRowAutomationPeer? subscriber))
            {
                subscriber.OnNodePropertyChanged(sender, e);
            }
            else
            {
                Dispose();
            }
        }
    }
}
