// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls.DataGridDragDrop;

namespace Avalonia.Controls.DataGridDragDrop
{
#nullable enable
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridRowDropEventArgs
    {
        private DragDropEffects _requestedEffect;
        private DragDropEffects _effectiveEffect;
        private DataGridRowDragSession? _session;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridRowDropEventArgs(
            DataGrid grid,
            IList? targetList,
            IReadOnlyList<object> items,
            IReadOnlyList<int> sourceIndices,
            object? targetItem,
            int targetIndex,
            int insertIndex,
            DataGridRow? targetRow,
            DataGridRowDropPosition position,
            bool isSameGrid,
            DragDropEffects requestedEffect,
            DragEventArgs dragEventArgs)
        {
            Grid = grid;
            TargetList = targetList;
            Items = items;
            SourceIndices = sourceIndices;
            TargetItem = targetItem;
            TargetIndex = targetIndex;
            InsertIndex = insertIndex;
            TargetRow = targetRow;
            Position = position;
            IsSameGrid = isSameGrid;
            _requestedEffect = requestedEffect;
            _effectiveEffect = requestedEffect;
            DragEventArgs = dragEventArgs;
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGrid Grid { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IList? TargetList { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IReadOnlyList<object> Items { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IReadOnlyList<int> SourceIndices { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        object? TargetItem { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        int TargetIndex { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        int InsertIndex { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridRow? TargetRow { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridRowDropPosition Position { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        bool IsSameGrid { get; }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DragDropEffects RequestedEffect
        {
            get => _session?.RequestedEffect ?? _requestedEffect;
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DragDropEffects EffectiveEffect
        {
            get => _session?.EffectiveEffect ?? _effectiveEffect;
            set
            {
                _effectiveEffect = value;

                if (_session != null)
                {
                    _session.EffectiveEffect = value;
                }
            }
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridRowDragSession? Session
        {
            get => _session;
            private set => _session = value;
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        Point PointerPosition => _session?.PointerPosition ?? DragEventArgs.GetPosition(Grid);

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        KeyModifiers KeyModifiers => _session?.KeyModifiers ?? DragEventArgs.KeyModifiers;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridRow? HoveredRow => _session?.HoveredRow;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        object? HoveredItem => _session?.HoveredItem;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DragEventArgs DragEventArgs { get; }

        internal void SetSession(DataGridRowDragSession? session)
        {
            Session = session;
            if (Session != null)
            {
                Session.SetRequestedEffect(_requestedEffect);
                Session.EffectiveEffect = _effectiveEffect;
            }
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridRowDropHandler
    {
        bool Validate(DataGridRowDropEventArgs args);

        bool Execute(DataGridRowDropEventArgs args);
    }
}
