// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Utilities;
using System;
using System.Text;

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
        /// This method formats a row (specified by a DataGridRowClipboardEventArgs) into
        /// a single string to be added to the Clipboard when the DataGrid is copying its contents.
        /// </summary>
        /// <param name="e">DataGridRowClipboardEventArgs</param>
        /// <returns>The formatted string.</returns>
        private string FormatClipboardContent(DataGridRowClipboardEventArgs e)
        {
            var text = StringBuilderCache.Acquire();
            var clipboardRowContent = e.ClipboardRowContent;
            var numberOfItem = clipboardRowContent.Count;
            for (int cellIndex = 0; cellIndex < numberOfItem; cellIndex++)
            {
                var cellContent = clipboardRowContent[cellIndex].Content?.ToString();
                cellContent = cellContent?.Replace("\"", "\"\"");
                text.Append($"\"{cellContent}\"");
                if (cellIndex < numberOfItem - 1)
                {
                    text.Append('\t');
                }
                else
                {
                    text.Append('\r');
                    text.Append('\n');
                }
            }
            return StringBuilderCache.GetStringAndRelease(text);
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

            if (ctrl && !shift && !alt && ClipboardCopyMode != DataGridClipboardCopyMode.None && SelectedItems.Count > 0)
            {
                var textBuilder = StringBuilderCache.Acquire();

                if (ClipboardCopyMode == DataGridClipboardCopyMode.IncludeHeader)
                {
                    DataGridRowClipboardEventArgs headerArgs = new DataGridRowClipboardEventArgs(null, true, CopyingRowClipboardContentEvent, this);
                    foreach (DataGridColumn column in ColumnsInternal.GetVisibleColumns())
                    {
                        headerArgs.ClipboardRowContent.Add(new DataGridClipboardCellContent(null, column, column.Header));
                    }
                    OnCopyingRowClipboardContent(headerArgs);
                    textBuilder.Append(FormatClipboardContent(headerArgs));
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
                    textBuilder.Append(FormatClipboardContent(itemArgs));
                }

                string text = StringBuilderCache.GetStringAndRelease(textBuilder);

                if (!string.IsNullOrEmpty(text))
                {
                    CopyToClipboard(text);
                    return true;
                }
            }
            return false;
        }


        private async void CopyToClipboard(string text)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

            if (clipboard != null)
                await clipboard.SetTextAsync(text);
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
