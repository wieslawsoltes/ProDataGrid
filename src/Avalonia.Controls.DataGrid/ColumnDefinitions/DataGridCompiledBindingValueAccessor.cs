// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia.Data;
using Avalonia.Data.Core;

namespace Avalonia.Controls
{
    /// <summary>
    /// Reads and writes the value of a column through the property chain of its compiled binding.
    /// </summary>
    /// <remarks>
    /// The XAML compiler emits a getter and a setter for every property of a compiled binding path,
    /// so the accessor reaches the value without reflection. Data operations use it only when the
    /// reflection feature switch is off and the column declares no value accessor of its own.
    /// </remarks>
    internal sealed class DataGridCompiledBindingValueAccessor : IDataGridColumnValueAccessor
    {
        private static readonly AttachedProperty<DataGridCompiledBindingValueAccessor?> AccessorProperty =
            AvaloniaProperty.RegisterAttached<DataGridCompiledBindingValueAccessor, DataGridColumn, DataGridCompiledBindingValueAccessor?>(
                "Accessor");

        private readonly CompiledBinding _binding;
        private readonly IPropertyInfo[] _properties;

        private DataGridCompiledBindingValueAccessor(CompiledBinding binding, IPropertyInfo[] properties, string propertyPath)
        {
            _binding = binding;
            _properties = properties;
            PropertyPath = propertyPath;
        }

        /// <summary>
        /// Gets the binding path the accessor follows, in the form the column reports as its sort member path.
        /// </summary>
        public string PropertyPath { get; }

        public Type ItemType => typeof(object);

        public Type ValueType => _properties[_properties.Length - 1].PropertyType;

        public bool CanWrite => _properties[_properties.Length - 1].CanSet;

        public object? GetValue(object item)
        {
            object? value = item;
            for (var i = 0; i < _properties.Length; i++)
            {
                if (value == null)
                {
                    return null;
                }

                value = _properties[i].Get(value);
            }

            return value;
        }

        public void SetValue(object item, object? value)
        {
            object? target = item;
            var last = _properties.Length - 1;
            for (var i = 0; i < last; i++)
            {
                if (target == null)
                {
                    return;
                }

                target = _properties[i].Get(target);
            }

            if (target != null)
            {
                _properties[last].Set(target, value);
            }
        }

        /// <summary>
        /// Gets the accessor for the compiled binding of a column.
        /// </summary>
        /// <param name="column">The column whose binding the accessor follows.</param>
        /// <param name="propertyPath">
        /// The path the data operation reads. An accessor is returned only when the binding follows the same path,
        /// so a column with a different sort or search member path keeps its own resolution.
        /// </param>
        /// <returns>
        /// The accessor, or <see langword="null"/> when no compiled binding of the column follows the path, the binding
        /// has a source of its own, or its path contains anything other than properties.
        /// </returns>
        internal static DataGridCompiledBindingValueAccessor? Get(DataGridColumn column, string? propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            var accessor = column.GetValue(AccessorProperty);
            if (accessor != null &&
                string.Equals(accessor.PropertyPath, propertyPath, StringComparison.Ordinal) &&
                IsColumnBinding(column, accessor._binding))
            {
                return accessor;
            }

            accessor = column switch
            {
                DataGridBoundColumn boundColumn => Create(boundColumn.Binding, propertyPath),
                DataGridComboBoxColumn comboBoxColumn =>
                    Create(comboBoxColumn.SelectedValueBinding, propertyPath) ??
                    Create(comboBoxColumn.SelectedItemBinding, propertyPath) ??
                    Create(comboBoxColumn.TextBinding, propertyPath),
                _ => null
            };

            if (accessor != null)
            {
                column.SetValue(AccessorProperty, accessor);
            }

            return accessor;
        }

        private static bool IsColumnBinding(DataGridColumn column, BindingBase binding)
        {
            return column switch
            {
                DataGridBoundColumn boundColumn => ReferenceEquals(boundColumn.Binding, binding),
                DataGridComboBoxColumn comboBoxColumn =>
                    ReferenceEquals(comboBoxColumn.SelectedValueBinding, binding) ||
                    ReferenceEquals(comboBoxColumn.SelectedItemBinding, binding) ||
                    ReferenceEquals(comboBoxColumn.TextBinding, binding),
                _ => false
            };
        }

        private static DataGridCompiledBindingValueAccessor? Create(BindingBase? binding, string propertyPath)
        {
            if (binding is not CompiledBinding { Path: { } path } compiledBinding ||
                compiledBinding.Source != AvaloniaProperty.UnsetValue)
            {
                return null;
            }

            var bindingPath = path.ToString();
            if (!string.Equals(bindingPath, propertyPath, StringComparison.Ordinal))
            {
                return null;
            }

            var count = 0;
            foreach (var element in path.Elements)
            {
                if (element is not PropertyElement { Property.CanGet: true })
                {
                    return null;
                }

                count++;
            }

            if (count == 0)
            {
                return null;
            }

            var properties = new IPropertyInfo[count];
            var index = 0;
            foreach (var element in path.Elements)
            {
                properties[index++] = ((PropertyElement)element).Property;
            }

            return new DataGridCompiledBindingValueAccessor(compiledBinding, properties, bindingPath);
        }
    }
}
