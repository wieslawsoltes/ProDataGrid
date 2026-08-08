// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Data.Core;

namespace Avalonia.Controls
{
    /// <summary>Identifies the standard definition used for a generated indexed column.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedIndexedColumnKind
    {
        /// <summary>A text column.</summary>
        Text,
        /// <summary>A numeric column.</summary>
        Numeric,
        /// <summary>A check-box column.</summary>
        CheckBox,
        /// <summary>A date-picker column.</summary>
        DatePicker,
        /// <summary>A time-picker column.</summary>
        TimePicker,
        /// <summary>A progress-bar column.</summary>
        ProgressBar,
        /// <summary>A slider column.</summary>
        Slider,
        /// <summary>A hyperlink column.</summary>
        Hyperlink,
        /// <summary>An image column.</summary>
        Image,
        /// <summary>A hierarchical text column.</summary>
        Hierarchical,
        /// <summary>A custom-drawing column.</summary>
        CustomDrawing,
        /// <summary>A formula column evaluated by the configured DataGrid formula model.</summary>
        Formula
    }

    /// <summary>Configures one column created from a generated indexed accessor family.</summary>
    /// <typeparam name="TValue">The runtime slot value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    struct DataGridGeneratedIndexedColumnOptions<TValue>
    {
        /// <summary>Gets or sets the header.</summary>
        public object Header { get; set; }
        /// <summary>Gets or sets the stable column key.</summary>
        public string ColumnKey { get; set; }
        /// <summary>Gets or sets the synthetic binding/property name.</summary>
        public string PropertyName { get; set; }
        /// <summary>Gets or sets the standard column kind.</summary>
        public DataGridGeneratedIndexedColumnKind Kind { get; set; }
        /// <summary>Gets or sets an optional compiled binding format.</summary>
        public string FormatString { get; set; }
        /// <summary>Gets or sets the structured or A1 formula used by a formula column.</summary>
        public string Formula { get; set; }
        /// <summary>Gets or sets the stable formula name used by structured references.</summary>
        public string FormulaName { get; set; }
        /// <summary>Gets or sets whether individual cells may override the column formula.</summary>
        public bool AllowCellFormulas { get; set; }
        /// <summary>Gets or sets whether editing is disabled.</summary>
        public bool IsReadOnly { get; set; }
        /// <summary>Gets or sets optional width.</summary>
        public DataGridLength? Width { get; set; }
        /// <summary>Gets or sets optional minimum width.</summary>
        public double? MinWidth { get; set; }
        /// <summary>Gets or sets optional maximum width.</summary>
        public double? MaxWidth { get; set; }
        /// <summary>Gets or sets an optional final customization callback.</summary>
        public Action<DataGridColumnDefinition> Configure { get; set; }
    }

    /// <summary>Creates reflection-free column definitions around generated method-backed accessors.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridGeneratedIndexedColumnFactory
    {
        /// <summary>Creates a typed indexed column and its cached compiled binding metadata.</summary>
        public static DataGridColumnDefinition Create<TItem, TValue>(
            int index,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter,
            in DataGridGeneratedIndexedColumnOptions<TValue> options)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

            string propertyName = string.IsNullOrWhiteSpace(options.PropertyName)
                ? "Item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : options.PropertyName;
            if (options.Kind == DataGridGeneratedIndexedColumnKind.Formula)
            {
                var formulaDefinition = new DataGridFormulaColumnDefinition
                {
                    Header = options.Header,
                    ColumnKey = options.ColumnKey ?? propertyName,
                    Formula = options.Formula,
                    FormulaName = string.IsNullOrWhiteSpace(options.FormulaName) ? propertyName : options.FormulaName,
                    FormulaValueType = typeof(TValue),
                    AllowCellFormulas = options.AllowCellFormulas,
                    IsReadOnly = options.IsReadOnly || !options.AllowCellFormulas,
                    Width = options.Width,
                    MinWidth = options.MinWidth,
                    MaxWidth = options.MaxWidth
                };
                options.Configure?.Invoke(formulaDefinition);
                return formulaDefinition;
            }

            ArgumentNullException.ThrowIfNull(getter);
            var property = new ClrPropertyInfo(
                propertyName,
                target => target is TItem item ? getter(item) : default,
                setter == null ? null : (target, value) => SetValue(target, value, setter),
                typeof(TValue));
            DataGridBindingDefinition binding = DataGridBindingDefinition.Create<TItem, TValue>(property, getter, setter);
            binding.StringFormat = options.FormatString;

            DataGridBoundColumnDefinition definition = options.Kind switch
            {
                DataGridGeneratedIndexedColumnKind.Numeric => new DataGridNumericColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.CheckBox => new DataGridCheckBoxColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.DatePicker => new DataGridDatePickerColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.TimePicker => new DataGridTimePickerColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.ProgressBar => new DataGridProgressBarColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.Slider => new DataGridSliderColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.Hyperlink => new DataGridHyperlinkColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.Image => new DataGridImageColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.Hierarchical => new DataGridHierarchicalColumnDefinition(),
                DataGridGeneratedIndexedColumnKind.CustomDrawing => new DataGridCustomDrawingColumnDefinition(),
                _ => new DataGridTextColumnDefinition()
            };
            definition.Header = options.Header;
            definition.ColumnKey = options.ColumnKey ?? propertyName;
            definition.SortMemberPath = propertyName;
            definition.Binding = binding;
            definition.IsReadOnly = options.IsReadOnly || setter == null;
            definition.Width = options.Width;
            definition.MinWidth = options.MinWidth;
            definition.MaxWidth = options.MaxWidth;
            options.Configure?.Invoke(definition);
            return definition;
        }

        private static void SetValue<TItem, TValue>(object target, object value, Action<TItem, TValue> setter)
        {
            if (target is not TItem item) return;
            if (value is TValue typed)
            {
                setter(item, typed);
                return;
            }
            if (value == null && default(TValue) == null)
            {
                setter(item, default);
            }
        }
    }
}
