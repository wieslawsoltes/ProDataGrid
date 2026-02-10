// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Controls.DataGridFiltering;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    /// <summary>
    /// Reproduces mixed numeric type bounds for FilteringOperator.Between.
    /// </summary>
    public sealed class BetweenFilterOperatorViewModel : ObservableObject
    {
        private string _filterSummary = "No active filter.";

        public BetweenFilterOperatorViewModel()
        {
            Rows = new ObservableCollection<BetweenRow>(CreateRows());
            View = new DataGridCollectionView(Rows);
            FilteringModel = new FilteringModel();

            ApplyInt32BoundsCommand = new RelayCommand(_ => ApplyBetween(5, 10));
            ApplyInt64BoundsCommand = new RelayCommand(_ => ApplyBetween(5L, 10L));
            ClearFilterCommand = new RelayCommand(_ => FilteringModel.Remove(nameof(BetweenRow.Quantity)));

            FilteringModel.FilteringChanged += (_, _) => UpdateFilterSummary();
            UpdateFilterSummary();
        }

        public ObservableCollection<BetweenRow> Rows { get; }

        public DataGridCollectionView View { get; }

        public FilteringModel FilteringModel { get; }

        public ICommand ApplyInt32BoundsCommand { get; }

        public ICommand ApplyInt64BoundsCommand { get; }

        public ICommand ClearFilterCommand { get; }

        public string FilterSummary
        {
            get => _filterSummary;
            private set => SetProperty(ref _filterSummary, value);
        }

        private void ApplyBetween(object lowerBound, object upperBound)
        {
            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: nameof(BetweenRow.Quantity),
                @operator: FilteringOperator.Between,
                propertyPath: nameof(BetweenRow.Quantity),
                values: new object[] { lowerBound, upperBound }));
        }

        private void UpdateFilterSummary()
        {
            if (FilteringModel.Descriptors.Count == 0)
            {
                FilterSummary = "No active filter.";
                return;
            }

            var descriptor = FilteringModel.Descriptors[0];
            if (descriptor.Operator == FilteringOperator.Between &&
                descriptor.Values is { Count: >= 2 })
            {
                var lower = descriptor.Values[0];
                var upper = descriptor.Values[1];
                FilterSummary =
                    $"Active: Quantity between {lower} ({lower?.GetType().Name}) and {upper} ({upper?.GetType().Name})";
                return;
            }

            FilterSummary = $"Active: {descriptor.Operator}";
        }

        private static IEnumerable<BetweenRow> CreateRows()
        {
            return new[]
            {
                new BetweenRow("Alpha", 1L),
                new BetweenRow("Beta", 4L),
                new BetweenRow("Gamma", 5L),
                new BetweenRow("Delta", 9L),
                new BetweenRow("Epsilon", 10L),
                new BetweenRow("Zeta", 12L)
            };
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public sealed class BetweenRow
        {
            public BetweenRow(string name, long quantity)
            {
                Name = name;
                Quantity = quantity;
            }

            public string Name { get; }

            public long Quantity { get; }
        }
    }
}
