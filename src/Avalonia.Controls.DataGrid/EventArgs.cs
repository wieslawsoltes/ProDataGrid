// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.Collections.Generic;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.ComponentModel;
using System.Diagnostics;

namespace Avalonia.Controls
{
    /// <summary>
    /// Provides data for the <see cref="E:Avalonia.Controls.DataGrid.AutoGeneratingColumn" /> event. 
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridAutoGeneratingColumnEventArgs : CancelRoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridAutoGeneratingColumnEventArgs" /> class.
        /// </summary>
        /// <param name="propertyName">The name of the property bound to the generated column.</param>
        /// <param name="propertyType">The <see cref="T:System.Type" /> of the property bound to the generated column.</param>
        /// <param name="column">The generated column.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridAutoGeneratingColumnEventArgs(string propertyName, Type propertyType, DataGridColumn column, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            Column = column;
            PropertyName = propertyName;
            PropertyType = propertyType;
        }

        /// <summary>
        /// Gets the generated column.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the name of the property bound to the generated column.
        /// </summary>
        public string PropertyName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the <see cref="T:System.Type" /> of the property bound to the generated column.
        /// </summary>
        public Type PropertyType
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:Avalonia.Controls.DataGrid.BeginningEdit" /> event.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridBeginningEditEventArgs : CancelRoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the 
        /// <see cref="T:Avalonia.Controls.DataGridBeginningEditEventArgs" /> class.
        /// </summary>
        /// <param name="column">The column that contains the cell to be edited.</param>
        /// <param name="row">The row that contains the cell to be edited.</param>
        /// <param name="editingEventArgs">Information about the user gesture that caused the cell to enter edit mode.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridBeginningEditEventArgs(DataGridColumn column,
                                              DataGridRow row,
                                              RoutedEventArgs editingEventArgs,
                                              RoutedEvent routedEvent = null,
                                              object source = null)
            : base(routedEvent, source)
        {
            this.Column = column;
            this.Row = row;
            this.EditingEventArgs = editingEventArgs;
        }

        /// <summary>
        /// Gets the column that contains the cell to be edited.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets information about the user gesture that caused the cell to enter edit mode.
        /// </summary>
        public RoutedEventArgs EditingEventArgs
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the row that contains the cell to be edited.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides information just after a cell has exited editing mode.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridCellEditEndedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Instantiates a new instance of this class.
        /// </summary>
        /// <param name="column">The column of the cell that has just exited edit mode.</param>
        /// <param name="row">The row container of the cell container that has just exited edit mode.</param>
        /// <param name="editAction">The editing action that has been taken.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridCellEditEndedEventArgs(DataGridColumn column, DataGridRow row, DataGridEditAction editAction, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            Column = column;
            Row = row;
            EditAction = editAction;
        }

        /// <summary>
        /// The column of the cell that has just exited edit mode.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            private set;
        }

        /// <summary>
        /// The edit action taken when leaving edit mode.
        /// </summary>
        public DataGridEditAction EditAction
        {
            get;
            private set;
        }

        /// <summary>
        /// The row container of the cell container that has just exited edit mode.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides information after the cell has been pressed.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridCellPointerPressedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Instantiates a new instance of this class.
        /// </summary>
        /// <param name="cell">The cell that has been pressed.</param>
        /// <param name="row">The row container of the cell that has been pressed.</param>
        /// <param name="column">The column of the cell that has been pressed.</param>
        /// <param name="e">The pointer action that has been taken.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridCellPointerPressedEventArgs(DataGridCell cell, 
                                                   DataGridRow row,
                                                   DataGridColumn column,
                                                   PointerPressedEventArgs e,
                                                   RoutedEvent routedEvent = null,
                                                   object source = null)
            : base(routedEvent, source)
        {
            Cell = cell;
            Row = row;
            Column = column;
            PointerPressedEventArgs = e;
        }

        /// <summary>
        /// The cell that has been pressed.
        /// </summary> 
        public DataGridCell Cell { get; }

        /// <summary>
        /// The row container of the cell that has been pressed.
        /// </summary> 
        public DataGridRow Row { get; }

        /// <summary>
        /// The column of the cell that has been pressed.
        /// </summary> 
        public DataGridColumn Column { get; }

        /// <summary>
        /// The pointer action that has been taken.
        /// </summary> 
        public PointerPressedEventArgs PointerPressedEventArgs { get; }
    }

    /// <summary>
    /// Provides information just before a cell exits editing mode.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridCellEditEndingEventArgs : CancelRoutedEventArgs
    {
        /// <summary>
        /// Instantiates a new instance of this class.
        /// </summary>
        /// <param name="column">The column of the cell that is about to exit edit mode.</param>
        /// <param name="row">The row container of the cell container that is about to exit edit mode.</param>
        /// <param name="editingElement">The editing element within the cell.</param>
        /// <param name="editAction">The editing action that will be taken.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridCellEditEndingEventArgs(DataGridColumn column,
                                               DataGridRow row,
                                               Control editingElement,
                                               DataGridEditAction editAction,
                                               RoutedEvent routedEvent = null,
                                               object source = null)
            : base(routedEvent, source)
        {
            Column = column;
            Row = row;
            EditingElement = editingElement;
            EditAction = editAction;
        }

        /// <summary>
        /// The column of the cell that is about to exit edit mode.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            private set;
        }

        /// <summary>
        /// The edit action to take when leaving edit mode.
        /// </summary>
        public DataGridEditAction EditAction
        {
            get;
            private set;
        }

        /// <summary>
        /// The editing element within the cell. 
        /// </summary>
        public Control EditingElement
        {
            get;
            private set;
        }

        /// <summary>
        /// The row container of the cell container that is about to exit edit mode.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }

    }

    internal class DataGridCellEventArgs : EventArgs
    {
        internal DataGridCellEventArgs(DataGridCell dataGridCell)
        {
            Debug.Assert(dataGridCell != null);
            this.Cell = dataGridCell;
        }

        internal DataGridCell Cell
        {
            get;
            private set;
        }
    }

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridSelectedCellsChangedEventArgs : EventArgs
    {
        public DataGridSelectedCellsChangedEventArgs(
            IReadOnlyList<DataGridCellInfo> addedCells,
            IReadOnlyList<DataGridCellInfo> removedCells)
        {
            AddedCells = addedCells;
            RemovedCells = removedCells;
        }

        public IReadOnlyList<DataGridCellInfo> AddedCells { get; }

        public IReadOnlyList<DataGridCellInfo> RemovedCells { get; }
    }

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridSelectedColumnsChangedEventArgs : EventArgs
    {
        public DataGridSelectedColumnsChangedEventArgs(
            IReadOnlyList<DataGridColumn> addedColumns,
            IReadOnlyList<DataGridColumn> removedColumns)
        {
            AddedColumns = addedColumns;
            RemovedColumns = removedColumns;
        }

        public IReadOnlyList<DataGridColumn> AddedColumns { get; }

        public IReadOnlyList<DataGridColumn> RemovedColumns { get; }
    }

    /// <summary>
    /// Provides data for <see cref="T:Avalonia.Controls.DataGrid" /> column-related events.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridColumnEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridColumnEventArgs" /> class.
        /// </summary>
        /// <param name="column">The column that the event occurs for.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridColumnEventArgs(DataGridColumn column, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            Column = column ?? throw new ArgumentNullException(nameof(column));
        }

        /// <summary>
        /// Gets the column that the event occurs for.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:Avalonia.Controls.DataGrid.ColumnReordering" /> event.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridColumnReorderingEventArgs : CancelRoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridColumnReorderingEventArgs" /> class.
        /// </summary>
        /// <param name="dataGridColumn"></param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridColumnReorderingEventArgs(DataGridColumn dataGridColumn, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            this.Column = dataGridColumn;
        }

        /// <summary>
        /// The column being moved.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            private set;
        }

        /// <summary>
        /// The popup indicator displayed while dragging.  If null and Handled = true, then do not display a tooltip.
        /// </summary>
        public Control DragIndicator
        {
            get;
            set;
        }

        /// <summary>
        /// UIElement to display at the insertion position.  If null and Handled = true, then do not display an insertion indicator.
        /// </summary>
        public Control DropLocationIndicator
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Provides data for <see cref="T:Avalonia.Controls.DataGrid" /> row-related events.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridRowEventArgs" /> class.
        /// </summary>
        /// <param name="dataGridRow">The row that the event occurs for.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridRowEventArgs(DataGridRow dataGridRow, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            this.Row = dataGridRow;
        }

        /// <summary>
        /// Gets the row that the event occurs for.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides information just before a row exits editing mode.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowEditEndingEventArgs : CancelRoutedEventArgs
    {
        /// <summary>
        /// Instantiates a new instance of this class.
        /// </summary>
        /// <param name="row">The row container of the cell container that is about to exit edit mode.</param>
        /// <param name="editAction">The editing action that will be taken.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridRowEditEndingEventArgs(DataGridRow row, DataGridEditAction editAction, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            this.Row = row;
            this.EditAction = editAction;
        }

        /// <summary>
        /// The editing action that will be taken.
        /// </summary>
        public DataGridEditAction EditAction
        {
            get;
            private set;
        }

        /// <summary>
        /// The row container of the cell container that is about to exit edit mode.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides information just after a row has exited edit mode.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowEditEndedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Instantiates a new instance of this class.
        /// </summary>
        /// <param name="row">The row container of the cell container that has just exited edit mode.</param>
        /// <param name="editAction">The editing action that has been taken.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridRowEditEndedEventArgs(DataGridRow row, DataGridEditAction editAction, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            this.Row = row;
            this.EditAction = editAction;
        }

        /// <summary>
        /// The editing action that has been taken.
        /// </summary>
        public DataGridEditAction EditAction
        {
            get;
            private set;
        }

        /// <summary>
        /// The row container of the cell container that has just exited edit mode.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:Avalonia.Controls.DataGrid.PreparingCellForEdit" /> event.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridPreparingCellForEditEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridPreparingCellForEditEventArgs" /> class.
        /// </summary>
        /// <param name="column">The column that contains the cell to be edited.</param>
        /// <param name="row">The row that contains the cell to be edited.</param>
        /// <param name="editingEventArgs">Information about the user gesture that caused the cell to enter edit mode.</param>
        /// <param name="editingElement">The element that the column displays for a cell in editing mode.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridPreparingCellForEditEventArgs(DataGridColumn column,
                                                     DataGridRow row,
                                                     RoutedEventArgs editingEventArgs,
                                                     Control editingElement,
                                                     RoutedEvent routedEvent = null,
                                                     object source = null)
            : base(routedEvent, source)
        {
            Column = column;
            Row = row;
            EditingEventArgs = editingEventArgs;
            EditingElement = editingElement;
        }

        /// <summary>
        /// Gets the column that contains the cell to be edited.
        /// </summary>
        public DataGridColumn Column
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the element that the column displays for a cell in editing mode.
        /// </summary>
        public Control EditingElement
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets information about the user gesture that caused the cell to enter edit mode.
        /// </summary>
        public RoutedEventArgs EditingEventArgs
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the row that contains the cell to be edited.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// EventArgs used for the DataGrid's LoadingRowGroup and UnloadingRowGroup events
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowGroupHeaderEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Constructs a DataGridRowGroupHeaderEventArgs instance
        /// </summary>
        /// <param name="rowGroupHeader">Row group header associated with this instance.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridRowGroupHeaderEventArgs(DataGridRowGroupHeader rowGroupHeader, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            RowGroupHeader = rowGroupHeader;
        }

        /// <summary>
        /// DataGridRowGroupHeader associated with this instance
        /// </summary>
        public DataGridRowGroupHeader RowGroupHeader
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="E:Avalonia.Controls.DataGrid.LoadingRowDetails" />, <see cref="E:Avalonia.Controls.DataGrid.UnloadingRowDetails" />, 
    /// and <see cref="E:Avalonia.Controls.DataGrid.RowDetailsVisibilityChanged" /> events.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowDetailsEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.DataGridRowDetailsEventArgs" /> class. 
        /// </summary>
        /// <param name="row">The row that the event occurs for.</param>
        /// <param name="detailsElement">The row details section as a framework element.</param>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">Source object that raised the event.</param>
        public DataGridRowDetailsEventArgs(DataGridRow row, Control detailsElement, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            Row = row;
            DetailsElement = detailsElement;
        }

        /// <summary>
        /// Gets the row details section as a framework element.
        /// </summary>
        public Control DetailsElement
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the row that the event occurs for.
        /// </summary>
        public DataGridRow Row
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data when the current cell changes.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridCurrentCellChangedEventArgs : RoutedEventArgs
    {
        public DataGridCurrentCellChangedEventArgs(DataGridColumn oldColumn, object oldItem, DataGridColumn newColumn, object newItem, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            OldColumn = oldColumn;
            OldItem = oldItem;
            NewColumn = newColumn;
            NewItem = newItem;
        }

        public DataGridColumn OldColumn { get; }

        public object OldItem { get; }

        public DataGridColumn NewColumn { get; }

        public object NewItem { get; }
    }

    /// <summary>
    /// Provides data for DataGrid scroll routed events.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridScrollEventArgs : RoutedEventArgs
    {
        public DataGridScrollEventArgs(ScrollEventType scrollEventType, double newValue, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            ScrollEventType = scrollEventType;
            NewValue = newValue;
        }

        public ScrollEventType ScrollEventType { get; }

        public double NewValue { get; }
    }

    /// <summary>
    /// Provides data for DataGrid column header click events.
    /// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridColumnHeaderClickEventArgs : RoutedEventArgs
    {
        public DataGridColumnHeaderClickEventArgs(KeyModifiers keyModifiers, RoutedEvent routedEvent = null, object source = null)
            : base(routedEvent, source)
        {
            KeyModifiers = keyModifiers;
        }

        public KeyModifiers KeyModifiers { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class DataGridRowDragStartingEventArgs : RoutedEventArgs
    {
        public DataGridRowDragStartingEventArgs(
            IReadOnlyList<object> items,
            IReadOnlyList<int> indices,
            IDataTransfer data,
            DragDropEffects allowedEffects,
            RoutedEvent? routedEvent = null,
            object? source = null)
            : base(routedEvent, source)
        {
            Items = items ?? Array.Empty<object>();
            Indices = indices ?? Array.Empty<int>();
            Data = data ?? throw new ArgumentNullException(nameof(data));
            AllowedEffects = allowedEffects;
        }

        public IReadOnlyList<object> Items { get; }

        public IReadOnlyList<int> Indices { get; }

        public IDataTransfer Data { get; }

        public DragDropEffects AllowedEffects { get; set; }

        public bool Cancel { get; set; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class DataGridRowDragCompletedEventArgs : RoutedEventArgs
    {
        public DataGridRowDragCompletedEventArgs(
            IReadOnlyList<object> items,
            DragDropEffects effect,
            RoutedEvent? routedEvent = null,
            object? source = null)
            : base(routedEvent, source)
        {
            Items = items ?? Array.Empty<object>();
            Effect = effect;
        }

        public IReadOnlyList<object> Items { get; }

        public DragDropEffects Effect { get; }
    }
}
