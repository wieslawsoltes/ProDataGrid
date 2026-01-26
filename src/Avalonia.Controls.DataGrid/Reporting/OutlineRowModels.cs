// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.ComponentModel;

namespace Avalonia.Controls.DataGridReporting
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum OutlineRowType
    {
        Detail,
        Subtotal,
        GrandTotal
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class OutlineRow : INotifyPropertyChanged
    {
        private readonly object?[] _cellValues;
        private readonly List<OutlineRow> _children;
        private bool _isExpanded;
        private bool _isExpandable;

        internal OutlineRow(
            OutlineRowType rowType,
            int level,
            object?[] groupPathValues,
            object?[] groupDisplayValues,
            string? label,
            double indent,
            int valueCount,
            object? item)
        {
            RowType = rowType;
            Level = level;
            GroupPathValues = groupPathValues;
            GroupDisplayValues = groupDisplayValues;
            Label = label;
            Indent = indent;
            Item = item;
            _cellValues = new object?[valueCount];
            _children = new List<OutlineRow>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public OutlineRowType RowType { get; }

        public int Level { get; }

        public object?[] GroupPathValues { get; }

        public object?[] GroupDisplayValues { get; }

        public string? Label { get; }

        public double Indent { get; }

        public object? Item { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public bool IsExpandable
        {
            get => _isExpandable;
            internal set
            {
                if (_isExpandable == value)
                {
                    return;
                }

                _isExpandable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpandable)));
            }
        }

        public IReadOnlyList<OutlineRow> Children => _children;

        public object?[] CellValues => _cellValues;

        internal List<OutlineRow> MutableChildren => _children;

        internal void SetCellValue(int index, object? value)
        {
            if (index >= 0 && index < _cellValues.Length)
            {
                _cellValues[index] = value;
            }
        }
    }
}
