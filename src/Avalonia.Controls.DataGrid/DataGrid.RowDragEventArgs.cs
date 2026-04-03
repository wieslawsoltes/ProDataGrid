// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
#nullable enable
#if !DATAGRID_INTERNAL
    /// <summary>
    /// Provides data for the <see cref="DataGrid.RowDragStarted"/> event.
    /// </summary>
    public
#else
    internal
#endif
    class DataGridRowDragStartedEventArgs : RoutedEventArgs
    {
        public DataGridRowDragStartedEventArgs(
            DataGridRowDragSession session,
            RoutedEvent? routedEvent = null,
            object? source = null)
            : base(routedEvent, source)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public DataGridRowDragSession Session { get; }
    }

#if !DATAGRID_INTERNAL
    /// <summary>
    /// Provides data for the <see cref="DataGrid.RowDragUpdated"/> event.
    /// </summary>
    public
#else
    internal
#endif
    class DataGridRowDragUpdatedEventArgs : RoutedEventArgs
    {
        public DataGridRowDragUpdatedEventArgs(
            DataGridRowDragSession session,
            DragEventArgs dragEventArgs,
            RoutedEvent? routedEvent = null,
            object? source = null)
            : base(routedEvent, source)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            DragEventArgs = dragEventArgs ?? throw new ArgumentNullException(nameof(dragEventArgs));
        }

        public DataGridRowDragSession Session { get; }

        public DragEventArgs DragEventArgs { get; }
    }

#if !DATAGRID_INTERNAL
    /// <summary>
    /// Provides data for the <see cref="DataGrid.RowDragCanceled"/> event.
    /// </summary>
    public
#else
    internal
#endif
    class DataGridRowDragCanceledEventArgs : RoutedEventArgs
    {
        public DataGridRowDragCanceledEventArgs(
            DataGridRowDragSession session,
            RoutedEvent? routedEvent = null,
            object? source = null)
            : base(routedEvent, source)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public DataGridRowDragSession Session { get; }
    }
}
