// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    /// <summary>
    /// Simple FilteringModel sample without DynamicData, demonstrating shared filter editor templates.
    /// </summary>
    public class FilteringModelSampleViewModel : ObservableObject
    {
        private readonly ObservableCollection<Order> _items;
        private readonly RelayCommand _clearAllCommand;

        public FilteringModelSampleViewModel()
        {
            _items = new ObservableCollection<Order>(CreateOrders());
            View = new DataGridCollectionView(_items);
            FilteringModel = new FilteringModel();

            _clearAllCommand = new RelayCommand(_ => FilteringModel.Clear(), _ => FilteringModel.Descriptors.Count > 0);
            ClearAllCommand = _clearAllCommand;
            FilteringModel.FilteringChanged += (_, __) => _clearAllCommand.RaiseCanExecuteChanged();

            CustomerFilter = new TextFilterContext(
                "Customer contains",
                apply: text => ApplyTextFilter("Customer", text),
                clear: () => ClearFilter("Customer", () => CustomerFilter.Text = string.Empty));

            TotalFilter = new NumberFilterContext(
                "Total between",
                apply: (min, max) => ApplyNumberFilter(min, max),
                clear: () => ClearFilter("Total", () =>
                {
                    TotalFilter.MinValue = null;
                    TotalFilter.MaxValue = null;
                }));

            DateFilter = new DateFilterContext(
                "Ordered between",
                apply: (from, to) => ApplyDateFilter(from, to),
                clear: () => ClearFilter("Ordered", () =>
                {
                    DateFilter.From = null;
                    DateFilter.To = null;
                }));

            StatusFilter = new EnumFilterContext(
                "Status (In)",
                new[]
                {
                    "New",
                    "Processing",
                    "Shipped",
                    "Delivered",
                    "Canceled"
                },
                apply: selected => ApplyEnumFilter("Status", selected),
                clear: () => ClearFilter("Status", () => StatusFilter.SelectNone()));
        }

        public DataGridCollectionView View { get; }

        public FilteringModel FilteringModel { get; }

        public ICommand ClearAllCommand { get; }

        public TextFilterContext CustomerFilter { get; }

        public NumberFilterContext TotalFilter { get; }

        public DateFilterContext DateFilter { get; }

        public EnumFilterContext StatusFilter { get; }

        private void ApplyTextFilter(string columnId, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                FilteringModel.Remove(columnId);
                return;
            }

            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: columnId,
                @operator: FilteringOperator.Contains,
                propertyPath: columnId,
                value: text,
                stringComparison: StringComparison.OrdinalIgnoreCase));
        }

        private void ApplyNumberFilter(double? min, double? max)
        {
            if (min == null && max == null)
            {
                FilteringModel.Remove("Total");
                return;
            }

            var lower = min ?? double.MinValue;
            var upper = max ?? double.MaxValue;

            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: "Total",
                @operator: FilteringOperator.Between,
                propertyPath: "Total",
                values: new object[] { lower, upper }));
        }

        private void ApplyDateFilter(DateTimeOffset? from, DateTimeOffset? to)
        {
            if (from == null && to == null)
            {
                FilteringModel.Remove("Ordered");
                return;
            }

            var start = from ?? DateTimeOffset.MinValue;
            var end = to ?? DateTimeOffset.MaxValue;

            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: "Ordered",
                @operator: FilteringOperator.Between,
                propertyPath: "Ordered",
                values: new object[] { start, end }));
        }

        private void ApplyEnumFilter(string columnId, IReadOnlyList<string> selected)
        {
            if (selected.Count == 0)
            {
                FilteringModel.Remove(columnId);
                return;
            }

            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: columnId,
                @operator: FilteringOperator.In,
                propertyPath: columnId,
                values: selected.Cast<object>().ToArray()));
        }

        private void ClearFilter(string columnId, Action reset)
        {
            reset();
            FilteringModel.Remove(columnId);
        }

        private static IEnumerable<Order> CreateOrders()
        {
            var now = DateTimeOffset.UtcNow.Date;
            var random = new Random(7);
            var customers = new[] { "Contoso", "Fabrikam", "Northwind", "Tailspin", "Litware" };
            var regions = new[] { "US", "EU", "APAC" };
            var statuses = new[] { "New", "Processing", "Shipped", "Delivered", "Canceled" };

            for (int i = 0; i < 40; i++)
            {
                yield return new Order(
                    customer: customers[i % customers.Length],
                    status: statuses[i % statuses.Length],
                    region: regions[i % regions.Length],
                    total: Math.Round(random.NextDouble() * 2000, 2),
                    ordered: now.AddDays(-random.Next(0, 30)));
            }
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public sealed class Order
        {
            public Order(string customer, string status, string region, double total, DateTimeOffset ordered)
            {
                Customer = customer;
                Status = status;
                Region = region;
                Total = total;
                Ordered = ordered;
            }

            public string Customer { get; }
            public string Status { get; }
            public string Region { get; }
            public double Total { get; }
            public DateTimeOffset Ordered { get; }
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public sealed class TextFilterContext : ObservableObject, Avalonia.Controls.DataGridFiltering.IFilterTextContext
    {
        private string? _text;
        private readonly Action<string?> _apply;
        private readonly Action _clear;

        public TextFilterContext(string label, Action<string?> apply, Action clear)
        {
            Label = label;
            _apply = apply;
            _clear = clear;
            ApplyCommand = new RelayCommand(_ => _apply(Text));
            ClearCommand = new RelayCommand(_ => _clear());
        }

        public string Label { get; }

        public string? Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public ICommand ApplyCommand { get; }
        public ICommand ClearCommand { get; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public sealed class NumberFilterContext : ObservableObject, Avalonia.Controls.DataGridFiltering.IFilterNumberContext
    {
        private double? _min;
        private double? _max;
        private readonly Action<double?, double?> _apply;
        private readonly Action _clear;

        public NumberFilterContext(string label, Action<double?, double?> apply, Action clear)
        {
            Label = label;
            _apply = apply;
            _clear = clear;
            ApplyCommand = new RelayCommand(_ => _apply(MinValue, MaxValue));
            ClearCommand = new RelayCommand(_ => _clear());
        }

        public string Label { get; }
        public double Minimum { get; set; } = 0;
        public double Maximum { get; set; } = 1000000;

        public double? MinValue
        {
            get => _min;
            set => SetProperty(ref _min, value);
        }

        public double? MaxValue
        {
            get => _max;
            set => SetProperty(ref _max, value);
        }

        public ICommand ApplyCommand { get; }
        public ICommand ClearCommand { get; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public sealed class DateFilterContext : ObservableObject, Avalonia.Controls.DataGridFiltering.IFilterDateContext
    {
        private DateTimeOffset? _from;
        private DateTimeOffset? _to;
        private readonly Action<DateTimeOffset?, DateTimeOffset?> _apply;
        private readonly Action _clear;

        public DateFilterContext(string label, Action<DateTimeOffset?, DateTimeOffset?> apply, Action clear)
        {
            Label = label;
            _apply = apply;
            _clear = clear;
            ApplyCommand = new RelayCommand(_ => _apply(From, To));
            ClearCommand = new RelayCommand(_ => _clear());
        }

        public string Label { get; }

        public DateTimeOffset? From
        {
            get => _from;
            set => SetProperty(ref _from, value);
        }

        public DateTimeOffset? To
        {
            get => _to;
            set => SetProperty(ref _to, value);
        }

        public ICommand ApplyCommand { get; }
        public ICommand ClearCommand { get; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public sealed class EnumFilterContext : ObservableObject, Avalonia.Controls.DataGridFiltering.IFilterEnumContext
    {
        private readonly ObservableCollection<IEnumOption> _options;
        private readonly Action<IReadOnlyList<string>> _apply;
        private readonly Action _clear;

        public EnumFilterContext(string label, IEnumerable<string> options, Action<IReadOnlyList<string>> apply, Action clear)
        {
            Label = label;
            _options = new ObservableCollection<IEnumOption>(options.Select(o => new EnumOption(o)));
            _apply = apply;
            _clear = clear;
            ApplyCommand = new RelayCommand(_ => _apply(SelectedValues));
            ClearCommand = new RelayCommand(_ =>
            {
                _clear();
                _apply(Array.Empty<string>());
            });
        }

        public string Label { get; }

        public ObservableCollection<IEnumOption> Options => _options;

        private IReadOnlyList<string> SelectedValues => _options.Where(o => o.IsSelected).Select(o => o.Display).ToArray();

        public ICommand ApplyCommand { get; }
        public ICommand ClearCommand { get; }

        public void SelectNone()
        {
            foreach (var opt in _options)
            {
                opt.IsSelected = false;
            }
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public sealed class EnumOption : ObservableObject, Avalonia.Controls.DataGridFiltering.IEnumOption
    {
        private bool _isSelected;

        public EnumOption(string display)
        {
            Display = display;
        }

        public string Display { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
