// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;

namespace Avalonia.Automation.Peers;

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridItemContainerAutomationPeer : ControlAutomationPeer, ISelectionItemProvider
{
    private readonly DataGridItemContainer _container;
    private bool _lastIsSelected;

    public DataGridItemContainerAutomationPeer(DataGridItemContainer owner)
        : base(owner)
    {
        _container = owner;
        _lastIsSelected = owner.IsSelected;
        owner.PropertyChanged += OnContainerPropertyChanged;
    }

    /// <inheritdoc />
    public bool IsSelected => TryGetSelectableOwner(out _) && _container.IsSelected;

    /// <inheritdoc />
    public ISelectionProvider? SelectionContainer =>
        TryGetSelectableOwner(out DataGrid? owner)
            ? GetOrCreate(owner!).GetProvider<ISelectionProvider>()
            : null;

    /// <inheritdoc />
    public void AddToSelection()
    {
        EnsureEnabled();
        if (TryGetSelectableOwner(out _))
        {
            _container.IsSelected = true;
        }
    }

    /// <inheritdoc />
    public void RemoveFromSelection()
    {
        EnsureEnabled();
        if (TryGetSelectableOwner(out _))
        {
            _container.IsSelected = false;
        }
    }

    /// <inheritdoc />
    public void Select()
    {
        EnsureEnabled();
        if (TryGetSelectableOwner(out DataGrid? owner))
        {
            owner!.SelectRowFromAutomation(_container.Slot);
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override string? GetNameCore()
    {
        string? name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? _container.DataContext?.ToString() : name;
    }

    protected override object? GetProviderCore(Type providerType)
    {
        if (providerType == typeof(ISelectionItemProvider) && !TryGetSelectableOwner(out _))
        {
            return null;
        }

        return base.GetProviderCore(providerType);
    }

    private bool TryGetSelectableOwner(out DataGrid? owner)
    {
        owner = _container.OwningGrid;
        return owner != null && owner.UsesLayoutItemPresentation && _container.Slot >= 0;
    }

    private void OnContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != DataGridItemContainer.IsSelectedProperty && e.Property != StyledElement.DataContextProperty)
        {
            return;
        }

        bool isSelected = IsSelected;
        bool wasSelected = _lastIsSelected;
        _lastIsSelected = isSelected;
        if (wasSelected != isSelected)
        {
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, wasSelected, isSelected);
        }
    }
}
