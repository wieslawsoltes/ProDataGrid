// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel;

namespace Avalonia.Controls.DataGridReporting
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class OutlineLayoutOptions : INotifyPropertyChanged
    {
        private string _rowHeaderLabel = "Group";
        private string _grandTotalLabel = "Grand Total";
        private string _subtotalLabelFormat = "{0} Total";
        private string? _emptyValueLabel = "(blank)";
        private bool _showGrandTotal = true;
        private bool _showSubtotals = true;
        private bool _showDetailRows = true;
        private bool _autoExpandGroups = true;
        private double _indent = 16d;
        private Func<object?, string?>? _detailLabelSelector;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string RowHeaderLabel
        {
            get => _rowHeaderLabel;
            set => SetProperty(ref _rowHeaderLabel, value, nameof(RowHeaderLabel));
        }

        public string GrandTotalLabel
        {
            get => _grandTotalLabel;
            set => SetProperty(ref _grandTotalLabel, value, nameof(GrandTotalLabel));
        }

        public string SubtotalLabelFormat
        {
            get => _subtotalLabelFormat;
            set => SetProperty(ref _subtotalLabelFormat, value, nameof(SubtotalLabelFormat));
        }

        public string? EmptyValueLabel
        {
            get => _emptyValueLabel;
            set => SetProperty(ref _emptyValueLabel, value, nameof(EmptyValueLabel));
        }

        public bool ShowGrandTotal
        {
            get => _showGrandTotal;
            set => SetProperty(ref _showGrandTotal, value, nameof(ShowGrandTotal));
        }

        public bool ShowSubtotals
        {
            get => _showSubtotals;
            set => SetProperty(ref _showSubtotals, value, nameof(ShowSubtotals));
        }

        public bool ShowDetailRows
        {
            get => _showDetailRows;
            set => SetProperty(ref _showDetailRows, value, nameof(ShowDetailRows));
        }

        public bool AutoExpandGroups
        {
            get => _autoExpandGroups;
            set => SetProperty(ref _autoExpandGroups, value, nameof(AutoExpandGroups));
        }

        public double Indent
        {
            get => _indent;
            set => SetProperty(ref _indent, value, nameof(Indent));
        }

        public Func<object?, string?>? DetailLabelSelector
        {
            get => _detailLabelSelector;
            set => SetProperty(ref _detailLabelSelector, value, nameof(DetailLabelSelector));
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
