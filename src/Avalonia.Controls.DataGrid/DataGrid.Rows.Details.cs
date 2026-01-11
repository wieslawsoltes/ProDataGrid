// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Diagnostics;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {
        private int GetDetailsCountInclusive(int lowerBound, int upperBound)
        {
            int indexCount = upperBound - lowerBound + 1;
            if (indexCount <= 0)
            {
                return 0;
            }
            if (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible)
            {
                // Total rows minus ones which explicitly turned details off minus the RowGroupHeaders
                int groupSlotCount = RowGroupHeadersTable.GetIndexCount(lowerBound, upperBound) +
                    RowGroupFootersTable.GetIndexCount(lowerBound, upperBound);
                return indexCount - _showDetailsTable.GetIndexCount(lowerBound, upperBound, false) - groupSlotCount;
            }
            else if (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Collapsed)
            {
                // Total rows with details explicitly turned on
                return _showDetailsTable.GetIndexCount(lowerBound, upperBound, true);
            }
            else if (RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.VisibleWhenSelected)
            {
                // Total number of remaining rows that are selected
                if (_selectionModelAdapter != null && DataConnection != null)
                {
                    int selectedCount = 0;
                    var selectedIndexes = _selectionModelAdapter.Model.SelectedIndexes;
                    for (int i = 0; i < selectedIndexes.Count; i++)
                    {
                        int slot = SlotFromSelectionIndex(selectedIndexes[i]);
                        if (slot >= lowerBound && slot <= upperBound)
                        {
                            selectedCount++;
                        }
                    }

                    return selectedCount;
                }

                return _selectedItems.GetIndexCount(lowerBound, upperBound);
            }
            Debug.Assert(false); // Shouldn't ever happen
            return 0;
        }



        private void EnsureRowDetailsVisibility(DataGridRow row, bool raiseNotification, bool animate)
        {
            // Show or hide RowDetails based on DataGrid settings
            row.SetDetailsVisibilityInternal(GetRowDetailsVisibility(row.Index), raiseNotification, animate);
        }



        private void UpdateRowDetailsHeightEstimate()
        {
            if (_rowsPresenter != null && _measured && HasRowDetailsTemplate)
            {
                object dataItem = null;
                if (VisibleSlotCount > 0)
                {
                    dataItem = DataConnection.GetDataItem(0);
                }

                var detailsTemplate = GetRowDetailsTemplate(dataItem, this);
                var detailsContent = detailsTemplate?.Build(dataItem);
                if (detailsContent != null)
                {
                    detailsContent.DataContext = dataItem;
                    _rowsPresenter.Children.Add(detailsContent);
                    detailsContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    RowDetailsHeightEstimate = detailsContent.DesiredSize.Height;
                    _rowsPresenter.Children.Remove(detailsContent);
                }
            }
        }


        internal void OnUnloadingRowDetails(DataGridRow row, Control detailsElement)
        {
            OnUnloadingRowDetails(new DataGridRowDetailsEventArgs(row, detailsElement));
        }


        internal void OnLoadingRowDetails(DataGridRow row, Control detailsElement)
        {
            OnLoadingRowDetails(new DataGridRowDetailsEventArgs(row, detailsElement));
        }



        internal void OnRowDetailsVisibilityPropertyChanged(int rowIndex, bool isVisible)
        {
            Debug.Assert(rowIndex >= 0 && rowIndex < SlotCount);

            _showDetailsTable.AddValue(rowIndex, isVisible);
        }



        internal bool GetRowDetailsVisibility(int rowIndex)
        {
            return GetRowDetailsVisibility(rowIndex, RowDetailsVisibilityMode);
        }



        internal bool GetRowDetailsVisibility(int rowIndex, DataGridRowDetailsVisibilityMode gridLevelRowDetailsVisibility)
        {
            Debug.Assert(rowIndex != -1);
            if (_showDetailsTable.Contains(rowIndex))
            {
                // The user explicitly set DetailsVisibility on a row so we should respect that
                return _showDetailsTable.GetValueAt(rowIndex);
            }
            else
            {
                return
                gridLevelRowDetailsVisibility == DataGridRowDetailsVisibilityMode.Visible ||
                (gridLevelRowDetailsVisibility == DataGridRowDetailsVisibilityMode.VisibleWhenSelected &&
                GetRowSelectionFromRowIndex(rowIndex));
            }
        }



        /// <summary>
        /// Raises the <see cref="E:Avalonia.Controls.DataGrid.RowDetailsVisibilityChanged" /> event.
        /// </summary>
        /// <param name="e">The event data.</param>
#if !DATAGRID_INTERNAL
        protected internal
#else
        internal
#endif
        virtual void OnRowDetailsVisibilityChanged(DataGridRowDetailsEventArgs e)
        {
            e.RoutedEvent ??= RowDetailsVisibilityChangedEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


    }
}
