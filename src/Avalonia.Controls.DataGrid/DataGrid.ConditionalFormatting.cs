// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Styling;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    partial class DataGrid
    {
        private void RefreshConditionalFormatting()
        {
            if (_rowsPresenter == null || _conditionalFormattingAdapter == null)
            {
                return;
            }

            foreach (var element in DisplayData.GetScrollingRows())
            {
                if (element is DataGridRow row)
                {
                    ApplyConditionalFormattingForRow(row);
                }
            }
        }

        internal void ApplyConditionalFormattingForRow(DataGridRow row)
        {
            if (_conditionalFormattingAdapter == null || row == null)
            {
                return;
            }

            var item = row.DataContext;
            var rowDescriptor = _conditionalFormattingAdapter.MatchRow(item, row.Index);
            ApplyConditionalFormattingToRow(row, rowDescriptor);

            if (row.Cells == null)
            {
                return;
            }

            foreach (DataGridCell cell in row.Cells)
            {
                if (cell?.OwningColumn == null)
                {
                    continue;
                }

                var descriptor = _conditionalFormattingAdapter.MatchCell(item, row.Index, cell.OwningColumn);
                ApplyConditionalFormattingToCell(cell, descriptor);
            }
        }

        private void ApplyConditionalFormattingToCell(DataGridCell cell, ConditionalFormattingDescriptor descriptor)
        {
            if (cell == null)
            {
                return;
            }

            var theme = ResolveConditionalFormattingTheme(descriptor);
            if (theme != null)
            {
                if (!ReferenceEquals(cell.Theme, theme))
                {
                    cell.Theme = theme;
                }

                return;
            }

            var columnTheme = cell.OwningColumn?.CellTheme;
            if (columnTheme != null)
            {
                if (!ReferenceEquals(cell.Theme, columnTheme))
                {
                    cell.Theme = columnTheme;
                }

                return;
            }

            cell.ClearValue(ThemeProperty);
        }

        private void ApplyConditionalFormattingToRow(DataGridRow row, ConditionalFormattingDescriptor descriptor)
        {
            if (row == null)
            {
                return;
            }

            var theme = ResolveConditionalFormattingTheme(descriptor);
            if (theme != null)
            {
                if (!ReferenceEquals(row.Theme, theme))
                {
                    row.Theme = theme;
                }

                return;
            }

            row.ClearValue(ThemeProperty);
        }

        private ControlTheme ResolveConditionalFormattingTheme(ConditionalFormattingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            if (descriptor.Theme != null)
            {
                return descriptor.Theme;
            }

            if (descriptor.ThemeKey == null)
            {
                return null;
            }

            if (this.TryFindResource(descriptor.ThemeKey, out var resource))
            {
                if (resource is ControlTheme theme)
                {
                    return theme;
                }
            }

            return null;
        }
    }
}
