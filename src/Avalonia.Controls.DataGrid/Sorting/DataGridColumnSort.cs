// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace Avalonia.Controls.DataGridSorting
{
    /// <summary>
    /// Attached properties for sorting configuration on <see cref="DataGridColumn"/>.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridColumnSort
    {
        public static readonly AttachedProperty<IDataGridColumnValueAccessor> ValueAccessorProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumn, IDataGridColumnValueAccessor>(
                "ValueAccessor",
                typeof(DataGridColumnSort));

        public static readonly AttachedProperty<IComparer> ValueComparerProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumn, IComparer>(
                "ValueComparer",
                typeof(DataGridColumnSort));

        public static readonly AttachedProperty<IComparer> AscendingComparerProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumn, IComparer>(
                "AscendingComparer",
                typeof(DataGridColumnSort));

        public static readonly AttachedProperty<IComparer> DescendingComparerProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumn, IComparer>(
                "DescendingComparer",
                typeof(DataGridColumnSort));

        public static void SetValueAccessor(AvaloniaObject target, IDataGridColumnValueAccessor value)
        {
            target.SetValue(ValueAccessorProperty, value);
        }

        public static IDataGridColumnValueAccessor GetValueAccessor(AvaloniaObject target)
        {
            return target.GetValue(ValueAccessorProperty);
        }

        public static void SetValueComparer(AvaloniaObject target, IComparer value)
        {
            target.SetValue(ValueComparerProperty, value);
        }

        public static IComparer GetValueComparer(AvaloniaObject target)
        {
            return target.GetValue(ValueComparerProperty);
        }

        public static void SetAscendingComparer(AvaloniaObject target, IComparer value)
        {
            target.SetValue(AscendingComparerProperty, value);
        }

        public static IComparer GetAscendingComparer(AvaloniaObject target)
        {
            return target.GetValue(AscendingComparerProperty);
        }

        public static void SetDescendingComparer(AvaloniaObject target, IComparer value)
        {
            target.SetValue(DescendingComparerProperty, value);
        }

        public static IComparer GetDescendingComparer(AvaloniaObject target)
        {
            return target.GetValue(DescendingComparerProperty);
        }
    }
}
