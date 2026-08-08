// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls.DataGridPivoting;

namespace Avalonia.Controls
{
    /// <summary>Identifies analytics and projection roles assigned to a generated field.</summary>
    [Flags]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedAnalyticsRole
    {
        /// <summary>No analytics role.</summary>
        None = 0,
        /// <summary>Pivot row axis.</summary>
        PivotRow = 1,
        /// <summary>Pivot column axis.</summary>
        PivotColumn = 2,
        /// <summary>Pivot filter axis.</summary>
        PivotFilter = 4,
        /// <summary>Pivot value.</summary>
        PivotValue = 8,
        /// <summary>Chart category.</summary>
        ChartCategory = 16,
        /// <summary>Chart series discriminator.</summary>
        ChartSeries = 32,
        /// <summary>Chart primary value.</summary>
        ChartValue = 64,
        /// <summary>Chart X value.</summary>
        ChartXValue = 128,
        /// <summary>Chart size value.</summary>
        ChartSize = 256,
        /// <summary>Outline grouping field.</summary>
        OutlineGroup = 512,
        /// <summary>Outline detail field.</summary>
        OutlineDetail = 1024,
        /// <summary>Formula name/value field.</summary>
        Formula = 2048
    }

    /// <summary>Provides non-generic access to generated analytics metadata.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedAnalyticsField
    {
        /// <summary>Gets the stable column key.</summary>
        string ColumnKey { get; }
        /// <summary>Gets assigned roles.</summary>
        DataGridGeneratedAnalyticsRole Role { get; }
        /// <summary>Gets precedence within its role.</summary>
        int Order { get; }
        /// <summary>Gets an optional series or formula name.</summary>
        string Name { get; }
        /// <summary>Gets an optional display format.</summary>
        string Format { get; }
        /// <summary>Gets aggregate identity encoded by the declaring integration.</summary>
        int Aggregate { get; }
        /// <summary>Gets pivot display mode.</summary>
        PivotValueDisplayMode PivotDisplayMode { get; }
        /// <summary>Gets stable formula dependency keys.</summary>
        IReadOnlyList<string> Dependencies { get; }
        /// <summary>Reads a value without reflection.</summary>
        object GetValue(object item);
    }

    /// <summary>Exposes an optional direct numeric selector without changing the base analytics contract.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedNumericAnalyticsField : IDataGridGeneratedAnalyticsField
    {
        /// <summary>Gets an optional generated numeric selector for allocation-conscious chart hot paths.</summary>
        Func<object, double?> NumericValueSelector { get; }
    }

    /// <summary>Contains a typed direct getter and capability metadata for one generated field role.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The field value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedAnalyticsField<TItem, TValue> : IDataGridGeneratedNumericAnalyticsField
    {
        private readonly Func<TItem, TValue> _getter;
        private readonly string[] _dependencies;

        /// <summary>Initializes capability metadata.</summary>
        public DataGridGeneratedAnalyticsField(
            string columnKey,
            DataGridGeneratedAnalyticsRole role,
            int order,
            Func<TItem, TValue> getter,
            string name = null,
            string format = null,
            int aggregate = 0,
            PivotValueDisplayMode pivotDisplayMode = PivotValueDisplayMode.Value,
            IReadOnlyList<string> dependencies = null)
            : this(
                columnKey,
                role,
                order,
                getter,
                null,
                name,
                format,
                aggregate,
                pivotDisplayMode,
                dependencies)
        {
        }

        /// <summary>Initializes capability metadata with a direct numeric chart selector.</summary>
        public DataGridGeneratedAnalyticsField(
            string columnKey,
            DataGridGeneratedAnalyticsRole role,
            int order,
            Func<TItem, TValue> getter,
            Func<object, double?> numericValueSelector,
            string name = null,
            string format = null,
            int aggregate = 0,
            PivotValueDisplayMode pivotDisplayMode = PivotValueDisplayMode.Value,
            IReadOnlyList<string> dependencies = null)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            Role = role;
            Order = order;
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            Name = name;
            Format = format;
            Aggregate = aggregate;
            PivotDisplayMode = pivotDisplayMode;
            NumericValueSelector = numericValueSelector;
            if (dependencies == null || dependencies.Count == 0)
            {
                _dependencies = Array.Empty<string>();
            }
            else
            {
                _dependencies = new string[dependencies.Count];
                for (int index = 0; index < dependencies.Count; index++)
                {
                    _dependencies[index] = dependencies[index] ?? throw new ArgumentException("Dependencies cannot contain null.", nameof(dependencies));
                }
            }
        }

        /// <inheritdoc />
        public string ColumnKey { get; }
        /// <inheritdoc />
        public DataGridGeneratedAnalyticsRole Role { get; }
        /// <inheritdoc />
        public int Order { get; }
        /// <inheritdoc />
        public string Name { get; }
        /// <inheritdoc />
        public string Format { get; }
        /// <inheritdoc />
        public int Aggregate { get; }
        /// <inheritdoc />
        public PivotValueDisplayMode PivotDisplayMode { get; }
        /// <inheritdoc />
        public IReadOnlyList<string> Dependencies => _dependencies;
        /// <inheritdoc />
        public Func<object, double?> NumericValueSelector { get; }
        /// <summary>Reads a typed value.</summary>
        public TValue GetTypedValue(TItem item) => _getter(item);
        /// <inheritdoc />
        public object GetValue(object item) => item is TItem typed ? _getter(typed) : null;
    }

    /// <summary>Adapts canonical generated analytics fields to reflection-free pivot fields.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridGeneratedPivotAdapter
    {
        private const DataGridGeneratedAnalyticsRole AxisRoles =
            DataGridGeneratedAnalyticsRole.PivotRow |
            DataGridGeneratedAnalyticsRole.PivotColumn |
            DataGridGeneratedAnalyticsRole.PivotFilter;

        /// <summary>Creates a pivot axis field from a generated role.</summary>
        public static PivotAxisField CreateAxisField(IDataGridGeneratedAnalyticsField field)
        {
            ArgumentNullException.ThrowIfNull(field);
            if ((field.Role & (DataGridGeneratedAnalyticsRole.PivotRow | DataGridGeneratedAnalyticsRole.PivotColumn | DataGridGeneratedAnalyticsRole.PivotFilter)) == 0)
            {
                throw new ArgumentException("The generated field does not define a pivot axis role.", nameof(field));
            }
            return new PivotAxisField
            {
                Key = field.ColumnKey,
                Header = field.Name ?? field.ColumnKey,
                ValueSelector = field.GetValue,
                StringFormat = field.Format
            };
        }

        /// <summary>Creates a pivot value field from a generated role.</summary>
        public static PivotValueField CreateValueField(IDataGridGeneratedAnalyticsField field)
        {
            ArgumentNullException.ThrowIfNull(field);
            if ((field.Role & DataGridGeneratedAnalyticsRole.PivotValue) == 0)
            {
                throw new ArgumentException("The generated field does not define a pivot value role.", nameof(field));
            }
            return new PivotValueField
            {
                Key = field.ColumnKey,
                Header = field.Name ?? field.ColumnKey,
                ValueSelector = field.GetValue,
                StringFormat = field.Format,
                AggregateType = (PivotAggregateType)field.Aggregate,
                DisplayMode = field.PivotDisplayMode
            };
        }

        /// <summary>Creates globally ordered pivot axis fields for the requested roles.</summary>
        public static IReadOnlyList<PivotAxisField> CreateAxisFields(
            IReadOnlyList<IDataGridGeneratedAnalyticsField> fields,
            DataGridGeneratedAnalyticsRole roles = AxisRoles)
        {
            ArgumentNullException.ThrowIfNull(fields);
            DataGridGeneratedAnalyticsRole requestedRoles = roles & AxisRoles;
            var matches = new List<IDataGridGeneratedAnalyticsField>();
            for (int index = 0; index < fields.Count; index++)
            {
                IDataGridGeneratedAnalyticsField field = fields[index] ??
                    throw new ArgumentException("Generated analytics fields cannot contain null entries.", nameof(fields));
                if ((field.Role & requestedRoles) != 0)
                {
                    matches.Add(field);
                }
            }

            SortFields(matches);
            var result = new PivotAxisField[matches.Count];
            for (int index = 0; index < matches.Count; index++)
            {
                result[index] = CreateAxisField(matches[index]);
            }
            return result;
        }

        /// <summary>Creates globally ordered pivot value fields.</summary>
        public static IReadOnlyList<PivotValueField> CreateValueFields(
            IReadOnlyList<IDataGridGeneratedAnalyticsField> fields)
        {
            ArgumentNullException.ThrowIfNull(fields);
            var matches = new List<IDataGridGeneratedAnalyticsField>();
            for (int index = 0; index < fields.Count; index++)
            {
                IDataGridGeneratedAnalyticsField field = fields[index] ??
                    throw new ArgumentException("Generated analytics fields cannot contain null entries.", nameof(fields));
                if ((field.Role & DataGridGeneratedAnalyticsRole.PivotValue) != 0)
                {
                    matches.Add(field);
                }
            }

            SortFields(matches);
            var result = new PivotValueField[matches.Count];
            for (int index = 0; index < matches.Count; index++)
            {
                result[index] = CreateValueField(matches[index]);
            }
            return result;
        }

        /// <summary>Creates a populated pivot model whose selectors come only from generated metadata.</summary>
        public static PivotTableModel CreateModel(
            IEnumerable items,
            IReadOnlyList<IDataGridGeneratedAnalyticsField> fields,
            Action<PivotTableModel> configure = null)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(fields);

            var model = new PivotTableModel { AutoRefresh = false };
            try
            {
                AddAxisFields(model.RowFields, fields, DataGridGeneratedAnalyticsRole.PivotRow);
                AddAxisFields(model.ColumnFields, fields, DataGridGeneratedAnalyticsRole.PivotColumn);
                AddAxisFields(model.FilterFields, fields, DataGridGeneratedAnalyticsRole.PivotFilter);

                IReadOnlyList<PivotValueField> valueFields = CreateValueFields(fields);
                for (int index = 0; index < valueFields.Count; index++)
                {
                    model.ValueFields.Add(valueFields[index]);
                }

                model.ItemsSource = items;
                configure?.Invoke(model);
                model.AutoRefresh = true;
                return model;
            }
            catch
            {
                model.Dispose();
                throw;
            }
        }

        private static void AddAxisFields(
            System.Collections.ObjectModel.ObservableCollection<PivotAxisField> destination,
            IReadOnlyList<IDataGridGeneratedAnalyticsField> fields,
            DataGridGeneratedAnalyticsRole role)
        {
            IReadOnlyList<PivotAxisField> axisFields = CreateAxisFields(fields, role);
            for (int index = 0; index < axisFields.Count; index++)
            {
                destination.Add(axisFields[index]);
            }
        }

        private static void SortFields(List<IDataGridGeneratedAnalyticsField> fields)
        {
            fields.Sort(static (left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.ColumnKey, right.ColumnKey);
            });
        }
    }
}
