// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Interactivity;
using System;

namespace Avalonia.Controls
{
    /// <summary>
    /// Event declarations
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the <see cref="AutoGeneratingColumn"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridAutoGeneratingColumnEventArgs> AutoGeneratingColumnEvent =
            RoutedEvent.Register<DataGrid, DataGridAutoGeneratingColumnEventArgs>(nameof(AutoGeneratingColumn), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="ColumnDisplayIndexChanged"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridColumnEventArgs> ColumnDisplayIndexChangedEvent =
            RoutedEvent.Register<DataGrid, DataGridColumnEventArgs>(nameof(ColumnDisplayIndexChanged), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="ColumnReordered"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridColumnEventArgs> ColumnReorderedEvent =
            RoutedEvent.Register<DataGrid, DataGridColumnEventArgs>(nameof(ColumnReordered), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="ColumnReordering"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridColumnReorderingEventArgs> ColumnReorderingEvent =
            RoutedEvent.Register<DataGrid, DataGridColumnReorderingEventArgs>(nameof(ColumnReordering), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="LoadingRow"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowEventArgs> LoadingRowEvent =
            RoutedEvent.Register<DataGrid, DataGridRowEventArgs>(nameof(LoadingRow), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="Sorting"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridColumnEventArgs> SortingEvent =
            RoutedEvent.Register<DataGrid, DataGridColumnEventArgs>(nameof(Sorting), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="UnloadingRow"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowEventArgs> UnloadingRowEvent =
            RoutedEvent.Register<DataGrid, DataGridRowEventArgs>(nameof(UnloadingRow), RoutingStrategies.Bubble);

        /// <summary>
        /// Occurs one time for each public, non-static property in the bound data type when the
        /// <see cref="P:Avalonia.Controls.DataGrid.ItemsSource" /> property is changed and the
        /// <see cref="P:Avalonia.Controls.DataGrid.AutoGenerateColumns" /> property is true.
        /// </summary>
        public event EventHandler<DataGridAutoGeneratingColumnEventArgs> AutoGeneratingColumn
        {
            add => AddHandler(AutoGeneratingColumnEvent, value);
            remove => RemoveHandler(AutoGeneratingColumnEvent, value);
        }

        /// <summary>
        /// Occurs when the <see cref="P:Avalonia.Controls.DataGridColumn.DisplayIndex" />
        /// property of a column changes.
        /// </summary>
        public event EventHandler<DataGridColumnEventArgs> ColumnDisplayIndexChanged
        {
            add => AddHandler(ColumnDisplayIndexChangedEvent, value);
            remove => RemoveHandler(ColumnDisplayIndexChangedEvent, value);
        }

        /// <summary>
        /// Raised when column reordering ends, to allow subscribers to clean up.
        /// </summary>
        public event EventHandler<DataGridColumnEventArgs> ColumnReordered
        {
            add => AddHandler(ColumnReorderedEvent, value);
            remove => RemoveHandler(ColumnReorderedEvent, value);
        }

        /// <summary>
        /// Raised when starting a column reordering action.  Subscribers to this event can
        /// set tooltip and caret UIElements, constrain tooltip position, indicate that
        /// a preview should be shown, or cancel reordering.
        /// </summary>
        public event EventHandler<DataGridColumnReorderingEventArgs> ColumnReordering
        {
            add => AddHandler(ColumnReorderingEvent, value);
            remove => RemoveHandler(ColumnReorderingEvent, value);
        }

        /// <summary>
        /// Occurs after a <see cref="T:Avalonia.Controls.DataGridRow" />
        /// is instantiated, so that you can customize it before it is used.
        /// </summary>
        public event EventHandler<DataGridRowEventArgs> LoadingRow
        {
            add => AddHandler(LoadingRowEvent, value);
            remove => RemoveHandler(LoadingRowEvent, value);
        }

        /// <summary>
        /// Identifies the <see cref="SelectionChanged"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<SelectionChangedEventArgs> SelectionChangedEvent =
            RoutedEvent.Register<DataGrid, SelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

        /// <summary>
        /// Occurs when the <see cref="DataGridColumn"/> sorting request is triggered.
        /// </summary>
        public event EventHandler<DataGridColumnEventArgs> Sorting
        {
            add => AddHandler(SortingEvent, value);
            remove => RemoveHandler(SortingEvent, value);
        }

        /// <summary>
        /// Occurs when a <see cref="T:Avalonia.Controls.DataGridRow" />
        /// object becomes available for reuse.
        /// </summary>
        public event EventHandler<DataGridRowEventArgs> UnloadingRow
        {
            add => AddHandler(UnloadingRowEvent, value);
            remove => RemoveHandler(UnloadingRowEvent, value);
        }

        /// <summary>
        /// Raises the AutoGeneratingColumn event.
        /// </summary>
        protected virtual void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            e.RoutedEvent ??= AutoGeneratingColumnEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }
    }
}
