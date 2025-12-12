// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Styling;
using System;
using System.Diagnostics;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
    /// <summary>
    /// Row details functionality
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the <see cref="LoadingRowDetails"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowDetailsEventArgs> LoadingRowDetailsEvent =
            RoutedEvent.Register<DataGrid, DataGridRowDetailsEventArgs>(nameof(LoadingRowDetails), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="UnloadingRowDetails"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowDetailsEventArgs> UnloadingRowDetailsEvent =
            RoutedEvent.Register<DataGrid, DataGridRowDetailsEventArgs>(nameof(UnloadingRowDetails), RoutingStrategies.Bubble);

        /// <summary>
        /// Identifies the <see cref="RowDetailsVisibilityChanged"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowDetailsEventArgs> RowDetailsVisibilityChangedEvent =
            RoutedEvent.Register<DataGrid, DataGridRowDetailsEventArgs>(nameof(RowDetailsVisibilityChanged), RoutingStrategies.Bubble);

        internal void OnRowDetailsChanged()
        {
            if (!_scrollingByHeight)
            {
                // Update layout when RowDetails are expanded or collapsed, just updating the vertical scroll bar is not enough
                // since rows could be added or removed
                InvalidateMeasure();
            }
        }


        /// <summary>
        /// Raises the LoadingRowDetails for row details preparation
        /// </summary>
        protected virtual void OnLoadingRowDetails(DataGridRowDetailsEventArgs e)
        {
            LoadingOrUnloadingRow = true;
            e.RoutedEvent ??= LoadingRowDetailsEvent;
            e.Source ??= this;
            RaiseEvent(e);
            LoadingOrUnloadingRow = false;
        }


        /// <summary>
        /// Raises the UnloadingRowDetails event
        /// </summary>
        protected virtual void OnUnloadingRowDetails(DataGridRowDetailsEventArgs e)
        {
            LoadingOrUnloadingRow = true;
            e.RoutedEvent ??= UnloadingRowDetailsEvent;
            e.Source ??= this;
            RaiseEvent(e);
            LoadingOrUnloadingRow = false;
        }


        private void UpdateRowDetailsVisibilityMode(DataGridRowDetailsVisibilityMode newDetailsMode)
        {
            int itemCount = DataConnection.Count;
            if (_rowsPresenter != null && itemCount > 0)
            {
                bool newDetailsVisibility = false;
                switch (newDetailsMode)
                {
                    case DataGridRowDetailsVisibilityMode.Visible:
                        newDetailsVisibility = true;
                        _showDetailsTable.AddValues(0, itemCount, true);
                        break;
                    case DataGridRowDetailsVisibilityMode.Collapsed:
                        newDetailsVisibility = false;
                        _showDetailsTable.AddValues(0, itemCount, false);
                        break;
                    case DataGridRowDetailsVisibilityMode.VisibleWhenSelected:
                        _showDetailsTable.Clear();
                        break;
                }

                bool updated = false;
                foreach (DataGridRow row in GetAllRows())
                {
                    if (row.IsVisible)
                    {
                        if (newDetailsMode == DataGridRowDetailsVisibilityMode.VisibleWhenSelected)
                        {
                            // For VisibleWhenSelected, we need to calculate the value for each individual row
                            newDetailsVisibility = GetRowSelection(row.Slot);
                        }
                        if (row.AreDetailsVisible != newDetailsVisibility)
                        {
                            updated = true;

                            row.SetDetailsVisibilityInternal(newDetailsVisibility, raiseNotification: true, animate: false);
                        }
                    }
                }
                if (updated)
                {
                    UpdateDisplayedRows(DisplayData.FirstScrollingSlot, CellsEstimatedHeight);
                    InvalidateRowsMeasure(invalidateIndividualElements: false);
                }
            }
        }


        private void OnRowDetailsTemplateChanged(AvaloniaPropertyChangedEventArgs e)
        {

            // Update the RowDetails templates if necessary
            if (_rowsPresenter != null)
            {
                foreach (DataGridRow row in GetAllRows())
                {
                    if (GetRowDetailsVisibility(row.Index))
                    {
                        // DetailsPreferredHeight is initialized when the DetailsElement's size changes.
                        row.ApplyDetailsTemplate(initializeDetailsPreferredHeight: false);
                    }
                }
            }

            UpdateRowDetailsHeightEstimate();
            InvalidateMeasure();
        }


        private void OnRowDetailsVisibilityModeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            UpdateRowDetailsVisibilityMode((DataGridRowDetailsVisibilityMode)e.NewValue);
        }


        /// <summary>
        /// Occurs when a new row details template is applied to a row, so that you can customize
        /// the details section before it is used.
        /// </summary>
        public event EventHandler<DataGridRowDetailsEventArgs> LoadingRowDetails
        {
            add => AddHandler(LoadingRowDetailsEvent, value);
            remove => RemoveHandler(LoadingRowDetailsEvent, value);
        }


        /// <summary>
        /// Occurs when a row details element becomes available for reuse.
        /// </summary>
        public event EventHandler<DataGridRowDetailsEventArgs> UnloadingRowDetails
        {
            add => AddHandler(UnloadingRowDetailsEvent, value);
            remove => RemoveHandler(UnloadingRowDetailsEvent, value);
        }


        /// <summary>
        /// Occurs when the <see cref="P:Avalonia.Controls.DataGrid.RowDetailsVisibilityMode" />
        /// property value changes.
        /// </summary>
        public event EventHandler<DataGridRowDetailsEventArgs> RowDetailsVisibilityChanged
        {
            add => AddHandler(RowDetailsVisibilityChangedEvent, value);
            remove => RemoveHandler(RowDetailsVisibilityChangedEvent, value);
        }


        internal double RowDetailsHeightEstimate
        {
            get;
            private set;
        }

    }
}
