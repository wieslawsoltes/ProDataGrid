// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;

namespace Avalonia.Controls;

/// <summary>
/// Hosts one templated data item when a layout model uses item presentation.
/// </summary>
/// <remarks>
/// The container is recycled independently from <see cref="DataGridRow"/> and never creates
/// <see cref="DataGridCell"/> instances. Style it with the <c>:selected</c>, <c>:current</c>, and
/// <c>:pointerover</c> pseudo-classes; place item visuals in <see cref="ContentControl.Content"/>.
/// </remarks>
[PseudoClasses(":selected", ":current", ":pointerover")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridItemContainer : ContentControl
{
    private bool _isSelected;
    private bool _isCurrent;
    private IDataTemplate? _appliedTemplate;

    /// <summary>
    /// Identifies the <see cref="IsSelected"/> direct property.
    /// </summary>
    public static readonly DirectProperty<DataGridItemContainer, bool> IsSelectedProperty =
        AvaloniaProperty.RegisterDirect<DataGridItemContainer, bool>(
            nameof(IsSelected),
            container => container.IsSelected,
            (container, value) => container.IsSelected = value);

    /// <summary>
    /// Identifies the <see cref="IsCurrent"/> direct property.
    /// </summary>
    public static readonly DirectProperty<DataGridItemContainer, bool> IsCurrentProperty =
        AvaloniaProperty.RegisterDirect<DataGridItemContainer, bool>(
            nameof(IsCurrent),
            container => container.IsCurrent);

    static DataGridItemContainer()
    {
        FocusableProperty.OverrideDefaultValue<DataGridItemContainer>(false);
        IsTabStopProperty.OverrideDefaultValue<DataGridItemContainer>(false);
        PointerPressedEvent.AddClassHandler<DataGridItemContainer>(
            static (container, e) => container.OnItemPointerPressed(e));
        AutomationProperties.IsOffscreenBehaviorProperty.OverrideDefaultValue<DataGridItemContainer>(
            IsOffscreenBehavior.FromClip);
    }

    /// <summary>
    /// Gets the zero-based row index represented by this container, or <c>-1</c> while recycled.
    /// </summary>
    public int Index { get; internal set; } = -1;

    /// <summary>
    /// Gets or sets a value indicating whether the represented item is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            DataGrid? owner = OwningGrid;
            if (owner != null && Slot >= 0 && !owner.IsSelectionUpdateFromRowSuppressed)
            {
                using var origin = owner.BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);
                if (!owner.TryPreviewSetRowSelection(Slot, value, setAnchorSlot: false))
                {
                    return;
                }

                using var commit = owner.BeginSelectionCommit();
                SetSelectedCore(value);
                return;
            }

            SetSelectedCore(value);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the represented item owns the current DataGrid position.
    /// </summary>
    public bool IsCurrent => _isCurrent;

    internal DataGrid? OwningGrid { get; private set; }

    internal int Slot { get; private set; } = -1;

    internal void Prepare(
        DataGrid owner,
        int index,
        int slot,
        object item,
        IDataTemplate template)
    {
        OwningGrid = owner;
        Index = index;
        Slot = slot;
        DataContext = item;
        SetValue(AutomationProperties.PositionInSetProperty, index + 1);
        SetValue(AutomationProperties.SizeOfSetProperty, owner.DataConnection.Count);

        Control? existing = ReferenceEquals(_appliedTemplate, template) ? Content as Control : null;
        Control? content = template is IRecyclingDataTemplate recycling
            ? recycling.Build(item, existing)
            : template.Build(item);
        Content = content ?? new Control();
        _appliedTemplate = template;
        ApplyState();
    }

    internal void DetachFromDataGrid()
    {
        SetSelectedCore(false);
        SetCurrentCore(false);
        OwningGrid = null;
        Index = -1;
        Slot = -1;
        DataContext = null;
        ClearValue(AutomationProperties.PositionInSetProperty);
        ClearValue(AutomationProperties.SizeOfSetProperty);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new DataGridItemContainerAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsSelectedProperty && OwningGrid != null && Slot >= 0)
        {
            OwningGrid.SetRowSelection(Slot, change.GetNewValue<bool>(), setAnchorSlot: false);
        }

        base.OnPropertyChanged(change);
    }

    internal void ApplyState(bool? isSelectedOverride = null)
    {
        DataGrid? owner = OwningGrid;
        bool selected = isSelectedOverride ?? (owner?.IsLayoutItemSelected(Slot) == true);
        SetSelectedCore(selected);
        SetCurrentCore(owner?.CurrentSlot == Slot);
    }

    private void SetSelectedCore(bool value)
    {
        SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        PseudoClasses.Set(":selected", value);
    }

    private void SetCurrentCore(bool value)
    {
        SetAndRaise(IsCurrentProperty, ref _isCurrent, value);
        PseudoClasses.Set(":current", value);
    }

    private void OnItemPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled || OwningGrid == null || Slot < 0)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            DataGridColumn? column = OwningGrid.CurrentColumn ?? OwningGrid.ColumnsInternal.FirstVisibleNonFillerColumn;
            e.Handled = OwningGrid.UpdateStateOnMouseLeftButtonDown(
                e,
                column?.Index ?? -1,
                Slot,
                allowEdit: false);
        }
        else if (properties.IsRightButtonPressed)
        {
            DataGridColumn? column = OwningGrid.CurrentColumn ?? OwningGrid.ColumnsInternal.FirstVisibleNonFillerColumn;
            e.Handled = OwningGrid.UpdateStateOnMouseRightButtonDown(
                e,
                column?.Index ?? -1,
                Slot,
                allowEdit: false);
        }
    }
}
