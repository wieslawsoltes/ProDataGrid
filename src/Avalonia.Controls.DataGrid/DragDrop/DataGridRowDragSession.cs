// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;

namespace Avalonia.Controls.DataGridDragDrop
{
#nullable enable
#if !DATAGRID_INTERNAL
    /// <summary>
    /// Represents the live state of an active row drag operation.
    /// </summary>
    public
#else
    internal
#endif
    sealed class DataGridRowDragSession : INotifyPropertyChanged
    {
        private IDataTransfer _data;
        private DragDropEffects _allowedEffects;
        private bool _isActive;
        private Point _pointerPosition;
        private KeyModifiers _keyModifiers;
        private DataGrid? _targetGrid;
        private DataGridRow? _hoveredRow;
        private object? _hoveredItem;
        private DataGridRow? _targetRow;
        private object? _targetItem;
        private int _targetIndex;
        private int _insertIndex;
        private DataGridRowDropPosition? _dropPosition;
        private DragDropEffects _requestedEffect;
        private DragDropEffects _effectiveEffect;
        private bool _isValidTarget;
        private bool _isCanceled;
        private DragDropEffects _resultEffect;
        private string? _feedbackCaption;

        internal DataGridRowDragSession(
            DataGrid sourceGrid,
            IReadOnlyList<object> items,
            IReadOnlyList<int> sourceIndices,
            bool fromSelection)
        {
            SourceGrid = sourceGrid;
            Items = items;
            SourceIndices = sourceIndices;
            FromSelection = fromSelection;
            _data = new DataTransfer();
        }

        /// <summary>
        /// Occurs when a property on the drag session changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the grid that started the drag.
        /// </summary>
        public
#else
        internal
#endif
        DataGrid SourceGrid { get; }

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the dragged items.
        /// </summary>
        public
#else
        internal
#endif
        IReadOnlyList<object> Items { get; }

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the source indices of the dragged items.
        /// </summary>
        public
#else
        internal
#endif
        IReadOnlyList<int> SourceIndices { get; }

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the data transfer payload for the drag operation.
        /// </summary>
        public
#else
        internal
#endif
        IDataTransfer Data => _data;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets a value indicating whether the drag originated from the current selection.
        /// </summary>
        public
#else
        internal
#endif
        bool FromSelection { get; }

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets or sets the drag effects allowed for the session.
        /// </summary>
        public
#else
        internal
#endif
        DragDropEffects AllowedEffects => _allowedEffects;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets a value indicating whether the session is currently active.
        /// </summary>
        public
#else
        internal
#endif
        bool IsActive => _isActive;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current pointer position relative to the target grid.
        /// </summary>
        public
#else
        internal
#endif
        Point PointerPosition => _pointerPosition;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current keyboard modifiers.
        /// </summary>
        public
#else
        internal
#endif
        KeyModifiers KeyModifiers => _keyModifiers;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the grid currently being dragged over.
        /// </summary>
        public
#else
        internal
#endif
        DataGrid? TargetGrid => _targetGrid;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the row currently under the pointer, even if it is not a valid drop target.
        /// </summary>
        public
#else
        internal
#endif
        DataGridRow? HoveredRow => _hoveredRow;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the item currently under the pointer, even if it is not a valid drop target.
        /// </summary>
        public
#else
        internal
#endif
        object? HoveredItem => _hoveredItem;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current actionable drop target row.
        /// </summary>
        public
#else
        internal
#endif
        DataGridRow? TargetRow => _targetRow;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current actionable drop target item.
        /// </summary>
        public
#else
        internal
#endif
        object? TargetItem => _targetItem;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current target index computed by the controller.
        /// </summary>
        public
#else
        internal
#endif
        int TargetIndex => _targetIndex;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current insert index computed by the controller.
        /// </summary>
        public
#else
        internal
#endif
        int InsertIndex => _insertIndex;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the current drop position relative to the target row.
        /// </summary>
        public
#else
        internal
#endif
        DataGridRowDropPosition? DropPosition => _dropPosition;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the effect requested from the current modifier state.
        /// </summary>
        public
#else
        internal
#endif
        DragDropEffects RequestedEffect => _requestedEffect;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets or sets the current effect approved by the application.
        /// </summary>
        public
#else
        internal
#endif
        DragDropEffects EffectiveEffect
        {
            get => _effectiveEffect;
            set => SetProperty(ref _effectiveEffect, value);
        }

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets a value indicating whether the current location is a valid drop target.
        /// </summary>
        public
#else
        internal
#endif
        bool IsValidTarget => _isValidTarget;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets a value indicating whether the drag ended without a committed drop.
        /// </summary>
        public
#else
        internal
#endif
        bool IsCanceled => _isCanceled;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets the final effect returned by the platform drag operation.
        /// </summary>
        public
#else
        internal
#endif
        DragDropEffects ResultEffect => _resultEffect;

#if !DATAGRID_INTERNAL
        /// <summary>
        /// Gets or sets an optional caption that can be shown by a custom drag feedback template.
        /// </summary>
        public
#else
        internal
#endif
        string? FeedbackCaption
        {
            get => _feedbackCaption;
            set => SetProperty(ref _feedbackCaption, value);
        }

        internal void SetData(IDataTransfer data) => SetProperty(ref _data, data, nameof(Data));

        internal void SetAllowedEffects(DragDropEffects allowedEffects) => SetProperty(ref _allowedEffects, allowedEffects, nameof(AllowedEffects));

        internal void SetIsActive(bool isActive) => SetProperty(ref _isActive, isActive, nameof(IsActive));

        internal void SetPointerPosition(Point pointerPosition) => SetProperty(ref _pointerPosition, pointerPosition, nameof(PointerPosition));

        internal void SetKeyModifiers(KeyModifiers keyModifiers) => SetProperty(ref _keyModifiers, keyModifiers, nameof(KeyModifiers));

        internal void SetTargetGrid(DataGrid? targetGrid) => SetProperty(ref _targetGrid, targetGrid, nameof(TargetGrid));

        internal void SetHoveredState(DataGridRow? hoveredRow, object? hoveredItem)
        {
            SetProperty(ref _hoveredRow, hoveredRow, nameof(HoveredRow));
            SetProperty(ref _hoveredItem, hoveredItem, nameof(HoveredItem));
        }

        internal void SetTargetState(
            DataGridRow? targetRow,
            object? targetItem,
            int targetIndex,
            int insertIndex,
            DataGridRowDropPosition? dropPosition)
        {
            SetProperty(ref _targetRow, targetRow, nameof(TargetRow));
            SetProperty(ref _targetItem, targetItem, nameof(TargetItem));
            SetProperty(ref _targetIndex, targetIndex, nameof(TargetIndex));
            SetProperty(ref _insertIndex, insertIndex, nameof(InsertIndex));
            SetProperty(ref _dropPosition, dropPosition, nameof(DropPosition));
        }

        internal void SetRequestedEffect(DragDropEffects requestedEffect) => SetProperty(ref _requestedEffect, requestedEffect, nameof(RequestedEffect));

        internal void SetIsValidTarget(bool isValidTarget) => SetProperty(ref _isValidTarget, isValidTarget, nameof(IsValidTarget));

        internal void SetIsCanceled(bool isCanceled) => SetProperty(ref _isCanceled, isCanceled, nameof(IsCanceled));

        internal void SetResultEffect(DragDropEffects resultEffect) => SetProperty(ref _resultEffect, resultEffect, nameof(ResultEffect));

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
