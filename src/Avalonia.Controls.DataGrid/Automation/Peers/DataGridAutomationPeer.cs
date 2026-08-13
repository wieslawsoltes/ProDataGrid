using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls.Selection;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Automation.Peers;

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
class DataGridAutomationPeer : ControlAutomationPeer, ISelectionProvider
{
    private readonly List<DataGridUnrealizedRowAutomationPeer> _unrealizedSelectionPeers = new();
    private int _lastStructureCount = -1;
    private int _lastStructureIdentityHash;

    public DataGridAutomationPeer(DataGrid owner)
        : base(owner)
    {
        owner.PropertyChanged += OnOwnerPropertyChanged;
        owner.SelectionChanged += OnOwnerSelectionChanged;
    }

    public new DataGrid Owner => (DataGrid)base.Owner;

    /// <inheritdoc />
    public bool CanSelectMultiple =>
        SupportsRowSelection(Owner) &&
        Owner.SelectionMode == DataGridSelectionMode.Extended;

    /// <inheritdoc />
    public bool IsSelectionRequired => false;

    /// <inheritdoc />
    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        if (!SupportsRowSelection(Owner))
        {
            ReleaseAllUnrealizedPeers();
            return Array.Empty<AutomationPeer>();
        }

        for (int index = 0; index < _unrealizedSelectionPeers.Count; index++)
        {
            _unrealizedSelectionPeers[index].IsClaimed = false;
        }

        List<AutomationPeer>? selected = null;
        IReadOnlyList<int> selectedIndexes = Owner.Selection.SelectedIndexes;
        for (int index = 0; index < selectedIndexes.Count; index++)
        {
            int rowIndex = selectedIndexes[index];
            if (rowIndex < 0 || rowIndex >= Owner.DataConnection.Count)
            {
                continue;
            }

            Control? realizedElement = null;
            int slot = Owner.SlotFromRowIndex(rowIndex);
            if (slot >= Owner.DisplayData.FirstScrollingSlot &&
                slot <= Owner.DisplayData.LastScrollingSlot &&
                Owner.IsSlotVisible(slot))
            {
                realizedElement = Owner.DisplayData.GetDisplayedElement(slot);
            }

            selected ??= new List<AutomationPeer>(selectedIndexes.Count);
            if (realizedElement is DataGridRow { IsSelected: true } row && row.IsAttachedToVisualTree())
            {
                selected.Add(GetOrCreate(row));
                continue;
            }
            if (realizedElement is DataGridItemContainer { IsSelected: true } itemContainer &&
                itemContainer.IsAttachedToVisualTree())
            {
                selected.Add(GetOrCreate(itemContainer));
                continue;
            }

            object? item = Owner.DataConnection.GetDataItem(rowIndex);
            if (item == null || ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder))
            {
                continue;
            }

            DataGridUnrealizedRowAutomationPeer peer = GetOrCreateUnrealizedPeer(item, rowIndex);
            peer.IsClaimed = true;
            selected.Add(peer);
        }

        PruneUnclaimedUnrealizedPeers();
        return selected ?? (IReadOnlyList<AutomationPeer>)Array.Empty<AutomationPeer>();
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.DataGrid;
    }

    protected override object? GetProviderCore(Type providerType)
    {
        if (providerType == typeof(ISelectionProvider) &&
            !SupportsRowSelection(Owner))
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    internal void RaiseHierarchyStructureChanged()
    {
        int count = Owner.DataConnection.Count;
        int identityHash = ComputeStructureIdentityHash(count);
        if (count == _lastStructureCount &&
            identityHash == _lastStructureIdentityHash)
        {
            return;
        }

        _lastStructureCount = count;
        _lastStructureIdentityHash = identityHash;
        InvalidateChildren();
    }

    private int ComputeStructureIdentityHash(int count)
    {
        int hash = 17;
        for (int index = 0; index < count; index++)
        {
            object? item = Owner.DataConnection.GetDataItem(index);
            unchecked
            {
                hash = (hash * 31) + (item is null ? 0 : RuntimeHelpers.GetHashCode(item));
            }
        }

        return hash;
    }

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DataGrid.SelectionModeProperty ||
            e.Property == DataGrid.SelectionUnitProperty)
        {
            if (!SupportsRowSelection(Owner))
            {
                ReleaseAllUnrealizedPeers();
            }

            RaiseSelectionChanged();
        }
    }

    private void OnOwnerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        for (int index = _unrealizedSelectionPeers.Count - 1; index >= 0; index--)
        {
            DataGridUnrealizedRowAutomationPeer peer = _unrealizedSelectionPeers[index];
            peer.OnOwnerSelectionChanged();
            if (!peer.IsSelected)
            {
                peer.Release();
                _unrealizedSelectionPeers.RemoveAt(index);
            }
        }

        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);
    }

    internal static bool SupportsRowSelection(DataGridSelectionUnit selectionUnit)
    {
        return selectionUnit == DataGridSelectionUnit.FullRow ||
            selectionUnit == DataGridSelectionUnit.CellOrRowHeader ||
            selectionUnit == DataGridSelectionUnit.CellOrRowOrColumnHeader;
    }

    internal static bool SupportsRowSelection(DataGrid owner) =>
        owner.UsesLayoutItemPresentation || SupportsRowSelection(owner.SelectionUnit);

    private DataGridUnrealizedRowAutomationPeer GetOrCreateUnrealizedPeer(
        object item,
        int rowIndex)
    {
        DataGridUnrealizedRowAutomationPeer? identityMatch = null;
        for (int index = 0; index < _unrealizedSelectionPeers.Count; index++)
        {
            DataGridUnrealizedRowAutomationPeer candidate = _unrealizedSelectionPeers[index];
            if (candidate.IsClaimed || !candidate.MatchesItem(item))
            {
                continue;
            }

            if (candidate.RowIndex == rowIndex)
            {
                candidate.Update(item, rowIndex);
                return candidate;
            }

            identityMatch ??= candidate;
        }

        if (identityMatch != null)
        {
            identityMatch.Update(item, rowIndex);
            return identityMatch;
        }

        var peer = new DataGridUnrealizedRowAutomationPeer(this, item, rowIndex);
        _unrealizedSelectionPeers.Add(peer);
        return peer;
    }

    private void PruneUnclaimedUnrealizedPeers()
    {
        for (int index = _unrealizedSelectionPeers.Count - 1; index >= 0; index--)
        {
            if (!_unrealizedSelectionPeers[index].IsClaimed)
            {
                _unrealizedSelectionPeers[index].Release();
                _unrealizedSelectionPeers.RemoveAt(index);
            }
        }
    }

    private void ReleaseAllUnrealizedPeers()
    {
        for (int index = 0; index < _unrealizedSelectionPeers.Count; index++)
        {
            _unrealizedSelectionPeers[index].Release();
        }

        _unrealizedSelectionPeers.Clear();
    }
}
