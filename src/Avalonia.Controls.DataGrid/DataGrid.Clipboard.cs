// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>
    /// Clipboard operations
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {
        /// <summary>
        /// Identifies the <see cref="CopyingRowClipboardContent"/> routed event.
        /// </summary>
        public static readonly RoutedEvent<DataGridRowClipboardEventArgs> CopyingRowClipboardContentEvent =
            RoutedEvent.Register<DataGrid, DataGridRowClipboardEventArgs>(nameof(CopyingRowClipboardContent), RoutingStrategies.Bubble);

        /// <summary>
        /// This method raises the CopyingRowClipboardContent event.
        /// </summary>
        /// <param name="e">Contains the necessary information for generating the row clipboard content.</param>
        protected virtual void OnCopyingRowClipboardContent(DataGridRowClipboardEventArgs e)
        {
            e.RoutedEvent ??= CopyingRowClipboardContentEvent;
            e.Source ??= this;
            RaiseEvent(e);
        }


        /// <summary>
        /// Handles the case where a 'Copy' key ('C' or 'Insert') has been pressed.  If pressed in combination with
        /// the control key, and the necessary prerequisites are met, the DataGrid will copy its contents
        /// to the Clipboard as text.
        /// </summary>
        /// <returns>Whether or not the DataGrid handled the key press.</returns>
        private bool ProcessCopyKey(KeyModifiers modifiers)
        {
            KeyboardHelper.GetMetaKeyState(this, modifiers, out bool ctrl, out bool shift, out bool alt);

            if (ctrl && !shift && !alt && ClipboardCopyMode != DataGridClipboardCopyMode.None)
            {
                return CopySelectionToClipboard();
            }
            return false;
        }

        /// <summary>
        /// Copies the current selection to the clipboard using the configured export formats and exporter.
        /// </summary>
        /// <returns>True when data was placed on the clipboard; otherwise false.</returns>
#if !DATAGRID_INTERNAL
        public
#endif
        bool CopySelectionToClipboard()
        {
            return CopySelectionToClipboard(ClipboardExportFormats, ClipboardExporter);
        }

        /// <summary>
        /// Copies the current selection to the clipboard using the provided formats and the configured exporter.
        /// </summary>
        /// <param name="formats">The formats that should be emitted by the default exporter.</param>
        /// <returns>True when data was placed on the clipboard; otherwise false.</returns>
#if !DATAGRID_INTERNAL
        public
#endif
        bool CopySelectionToClipboard(DataGridClipboardExportFormat formats)
        {
            return CopySelectionToClipboard(formats, ClipboardExporter);
        }

        /// <summary>
        /// Copies the current selection to the clipboard using the provided formats and exporter.
        /// </summary>
        /// <param name="formats">The formats that should be emitted by the default exporter.</param>
        /// <param name="exporter">A custom exporter used to build the clipboard data.</param>
        /// <returns>True when data was placed on the clipboard; otherwise false.</returns>
#if !DATAGRID_INTERNAL
        public
#endif
        bool CopySelectionToClipboard(DataGridClipboardExportFormat formats, IDataGridClipboardExporter? exporter)
        {
            if (ClipboardCopyMode == DataGridClipboardCopyMode.None)
            {
                return false;
            }

            if (formats != DataGridClipboardExportFormat.None && !formats.HasFlag(DataGridClipboardExportFormat.Text))
            {
                formats |= DataGridClipboardExportFormat.Text;
            }

            var rows = BuildClipboardRows();
            if (rows.Count == 0)
            {
                return false;
            }

            var activeExporter = exporter ?? new DataGridClipboardExporter(ClipboardFormatExporters);
            var data = activeExporter.BuildClipboardData(
                new DataGridClipboardExportContext(
                    this,
                    rows,
                    ClipboardCopyMode,
                    formats,
                    SelectionUnit));

            if (data == null)
            {
                return false;
            }

            CopyToClipboard(data);
            return true;
        }

        private List<DataGridRowClipboardEventArgs> BuildClipboardRows()
        {
            var rows = new List<DataGridRowClipboardEventArgs>();

            if (SelectionUnit == DataGridSelectionUnit.FullRow)
            {
                if (SelectedItems.Count == 0)
                {
                    return rows;
                }

                if (ClipboardCopyMode == DataGridClipboardCopyMode.IncludeHeader)
                {
                    DataGridRowClipboardEventArgs headerArgs = new DataGridRowClipboardEventArgs(null, true, CopyingRowClipboardContentEvent, this);
                    foreach (DataGridColumn column in ColumnsInternal.GetVisibleColumns())
                    {
                        headerArgs.ClipboardRowContent.Add(new DataGridClipboardCellContent(null, column, column.Header));
                    }
                    OnCopyingRowClipboardContent(headerArgs);
                    rows.Add(headerArgs);
                }

                for (int index = 0; index < SelectedItems.Count; index++)
                {
                    object item = SelectedItems[index];
                    DataGridRowClipboardEventArgs itemArgs = new DataGridRowClipboardEventArgs(item, false, CopyingRowClipboardContentEvent, this);
                    foreach (DataGridColumn column in ColumnsInternal.GetVisibleColumns())
                    {
                        object content = column.GetCellValue(item, column.ClipboardContentBinding);
                        itemArgs.ClipboardRowContent.Add(new DataGridClipboardCellContent(item, column, content));
                    }
                    OnCopyingRowClipboardContent(itemArgs);
                    rows.Add(itemArgs);
                }
            }
            else
            {
                var validCells = SelectedCells?.Where(c => c.IsValid).ToList();
                if (validCells == null || validCells.Count == 0)
                {
                    return rows;
                }

                var orderedColumns = validCells
                    .GroupBy(c => c.Column)
                    .Select(g => g.First())
                    .OrderBy(c => c.ColumnIndex)
                    .ToList();

                if (ClipboardCopyMode == DataGridClipboardCopyMode.IncludeHeader)
                {
                    DataGridRowClipboardEventArgs headerArgs = new DataGridRowClipboardEventArgs(null, true, CopyingRowClipboardContentEvent, this);
                    foreach (var cell in orderedColumns)
                    {
                        headerArgs.ClipboardRowContent.Add(new DataGridClipboardCellContent(null, cell.Column, cell.Column.Header));
                    }
                    OnCopyingRowClipboardContent(headerArgs);
                    rows.Add(headerArgs);
                }

                foreach (var rowGroup in validCells.GroupBy(c => c.RowIndex).OrderBy(g => g.Key))
                {
                    var item = DataConnection?.GetDataItem(rowGroup.Key);
                    DataGridRowClipboardEventArgs itemArgs = new DataGridRowClipboardEventArgs(item, false, CopyingRowClipboardContentEvent, this);
                    foreach (var cell in rowGroup.OrderBy(c => c.ColumnIndex))
                    {
                        object content = cell.Column.GetCellValue(item ?? cell.Item, cell.Column.ClipboardContentBinding);
                        itemArgs.ClipboardRowContent.Add(new DataGridClipboardCellContent(item, cell.Column, content));
                    }
                    OnCopyingRowClipboardContent(itemArgs);
                    rows.Add(itemArgs);
                }
            }

            return rows;
        }


        private async void CopyToClipboard(IAsyncDataTransfer data)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

            if (clipboard == null)
            {
                return;
            }

            if (data != null)
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        await clipboard.SetDataAsync(data);
                        break;
                    }
                    catch
                    {
                        if (attempt == 2)
                        {
                            throw;
                        }

                        await Task.Delay(10);
                    }
                }
            }
        }

        private ContentControl _clipboardContentControl;


        /// <summary>
        /// This event is raised by OnCopyingRowClipboardContent method after the default row content is prepared.
        /// Event listeners can modify or add to the row clipboard content.
        /// </summary>
        public event EventHandler<DataGridRowClipboardEventArgs> CopyingRowClipboardContent
        {
            add => AddHandler(CopyingRowClipboardContentEvent, value);
            remove => RemoveHandler(CopyingRowClipboardContentEvent, value);
        }


        /// <summary>
        /// This is an empty content control that's used during the DataGrid's copy procedure
        /// to determine the value of a ClipboardContentBinding for a particular column and item.
        /// </summary>
        internal ContentControl ClipboardContentControl
        {
            get
            {
                if (_clipboardContentControl == null)
                {
                    _clipboardContentControl = new ContentControl();
                }
                return _clipboardContentControl;
            }
        }

    }
}
