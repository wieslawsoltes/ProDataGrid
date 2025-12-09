// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using DynamicData.Binding;

namespace DataGridSample.Adapters
{
    /// <summary>
    /// Adapter factory that translates SortingModel descriptors into a comparer chain for DynamicData.
    /// It bypasses local SortDescriptions by overriding TryApplyModelToView.
    /// </summary>
    public sealed class DynamicDataSortingAdapterFactory : IDataGridSortingAdapterFactory
    {
        private readonly Action<string> _log;

        public DynamicDataSortingAdapterFactory(Action<string> log)
        {
            _log = log;
            SortComparer = Comparer<Deployment>.Create(static (_, _) => 0);
        }

        public IComparer<Deployment> SortComparer { get; private set; }

        public DataGridSortingAdapter Create(DataGrid grid, ISortingModel model)
        {
            return new DynamicDataSortingAdapter(model, () => grid.Columns, UpdateComparer, _log);
        }

        public void UpdateComparer(IReadOnlyList<SortingDescriptor> descriptors)
        {
            SortComparer = BuildComparer(descriptors);
            _log($"Upstream comparer updated: {Describe(descriptors)}");
        }

        private static IComparer<Deployment> BuildComparer(IReadOnlyList<SortingDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return Comparer<Deployment>.Create(static (_, _) => 0);
            }

            // If any descriptor uses a custom comparer, fall back to the manual chain so we can honor it.
            if (descriptors.Any(d => d?.Comparer != null))
            {
                return BuildManualComparer(descriptors);
            }

            SortExpressionComparer<Deployment>? comparer = null;

            foreach (var descriptor in descriptors.Where(d => d != null))
            {
                var selector = CreateSelector(descriptor);
                if (selector == null)
                {
                    continue;
                }

                comparer = comparer == null
                    ? descriptor.Direction == ListSortDirection.Ascending
                        ? SortExpressionComparer<Deployment>.Ascending(selector)
                        : SortExpressionComparer<Deployment>.Descending(selector)
                    : descriptor.Direction == ListSortDirection.Ascending
                        ? comparer.ThenByAscending(selector)
                        : comparer.ThenByDescending(selector);
            }

            if (comparer == null)
            {
                return Comparer<Deployment>.Create(static (_, _) => 0);
            }

            return comparer;
        }

        private static IComparer<Deployment> BuildManualComparer(IReadOnlyList<SortingDescriptor> descriptors)
        {
            var compiled = descriptors
                .Where(d => d != null)
                .Select(Compile)
                .Where(c => c != null)
                .ToList();

            if (compiled.Count == 0)
            {
                return Comparer<Deployment>.Create(static (_, _) => 0);
            }

            return Comparer<Deployment>.Create((left, right) =>
            {
                foreach (var entry in compiled)
                {
                    var l = entry.Getter(left);
                    var r = entry.Getter(right);

                    int result;
                    if (entry.CustomComparer != null)
                    {
                        result = entry.CustomComparer.Compare(l, r);
                    }
                    else
                    {
                        result = Comparer.DefaultInvariant.Compare(l, r);
                    }

                    if (result != 0)
                    {
                        return entry.Direction == ListSortDirection.Ascending ? result : -result;
                    }
                }

                return 0;
            });
        }

        private static CompiledComparer? Compile(SortingDescriptor descriptor)
        {
            var propertyPath = descriptor.PropertyPath ?? descriptor.ColumnId?.ToString();
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            return propertyPath switch
            {
                nameof(Deployment.Service) => new CompiledComparer(x => x.Service, descriptor.Direction, descriptor.Comparer),
                nameof(Deployment.Status) => new CompiledComparer(x => x.Status, descriptor.Direction, descriptor.Comparer),
                nameof(Deployment.Region) => new CompiledComparer(x => x.Region, descriptor.Direction, descriptor.Comparer),
                nameof(Deployment.Ring) => new CompiledComparer(x => x.Ring, descriptor.Direction, descriptor.Comparer),
                nameof(Deployment.Started) => new CompiledComparer(x => x.Started, descriptor.Direction, descriptor.Comparer),
                nameof(Deployment.ErrorRate) => new CompiledComparer(x => x.ErrorRate, descriptor.Direction, descriptor.Comparer),
                nameof(Deployment.Incidents) => new CompiledComparer(x => x.Incidents, descriptor.Direction, descriptor.Comparer),
                _ => null
            };
        }

        private static Func<Deployment, IComparable>? CreateSelector(SortingDescriptor descriptor)
        {
            var propertyPath = descriptor.PropertyPath ?? descriptor.ColumnId?.ToString();
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            return propertyPath switch
            {
                nameof(Deployment.Service) => x => x.Service,
                nameof(Deployment.Status) => x => x.Status,
                nameof(Deployment.Region) => x => x.Region,
                nameof(Deployment.Ring) => x => x.Ring,
                nameof(Deployment.Started) => x => x.Started,
                nameof(Deployment.ErrorRate) => x => x.ErrorRate,
                nameof(Deployment.Incidents) => x => x.Incidents,
                _ => null
            };
        }

        private static string Describe(IReadOnlyList<SortingDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return "(none)";
            }

            return string.Join(", ", descriptors.Where(d => d != null).Select(d =>
                $"{d.PropertyPath ?? d.ColumnId}:{(d.Direction == ListSortDirection.Ascending ? "asc" : "desc")}"));
        }

        private sealed class DynamicDataSortingAdapter : DataGridSortingAdapter
        {
            private readonly Action<IReadOnlyList<SortingDescriptor>> _update;
            private readonly Action<string> _log;

            public DynamicDataSortingAdapter(
                ISortingModel model,
                Func<IEnumerable<DataGridColumn>> columns,
                Action<IReadOnlyList<SortingDescriptor>> update,
                Action<string> log)
                : base(model, columns)
            {
                _update = update;
                _log = log;
            }

            protected override bool TryApplyModelToView(
                IReadOnlyList<SortingDescriptor> descriptors,
                IReadOnlyList<SortingDescriptor> previousDescriptors,
                out bool changed)
            {
                _update(descriptors);
                _log($"Applied to DynamicData: {Describe(descriptors)}");
                changed = true;
                return true;
            }

            private static string Describe(IReadOnlyList<SortingDescriptor> descriptors)
            {
                if (descriptors == null || descriptors.Count == 0)
                {
                    return "(none)";
                }

                return string.Join(", ", descriptors.Where(d => d != null).Select(d =>
                    $"{d.PropertyPath ?? d.ColumnId}:{(d.Direction == ListSortDirection.Ascending ? "asc" : "desc")}"));
            }
        }

        private sealed class CompiledComparer
        {
            public CompiledComparer(Func<Deployment, object?> getter, ListSortDirection direction, IComparer? customComparer)
            {
                Getter = getter;
                Direction = direction;
                CustomComparer = customComparer;
            }

            public Func<Deployment, object?> Getter { get; }

            public ListSortDirection Direction { get; }

            public IComparer? CustomComparer { get; }
        }
    }
}
