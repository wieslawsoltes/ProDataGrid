// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridColumnMetadata
    {
        internal static readonly AttachedProperty<DataGridColumnDefinition> DefinitionProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumnMetadata, DataGridColumn, DataGridColumnDefinition>(
                "Definition");
#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly AttachedProperty<IDataGridColumnValueAccessor> ValueAccessorProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumnMetadata, DataGridColumn, IDataGridColumnValueAccessor>(
                "ValueAccessor");

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        static readonly AttachedProperty<Type> ValueTypeProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumnMetadata, DataGridColumn, Type>(
                "ValueType");

        public static IDataGridColumnValueAccessor GetValueAccessor(DataGridColumn column)
        {
            return column?.GetValue(ValueAccessorProperty);
        }

        public static void SetValueAccessor(DataGridColumn column, IDataGridColumnValueAccessor accessor)
        {
            column?.SetValue(ValueAccessorProperty, accessor);
        }

        public static void ClearValueAccessor(DataGridColumn column)
        {
            column?.ClearValue(ValueAccessorProperty);
        }

        /// <summary>
        /// Gets the accessor that sorting, filtering, searching and other data operations read the column values with.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <param name="propertyPath">
        /// The property path the data operation would otherwise resolve through reflection, or <see langword="null"/>
        /// for the sort member path of the column.
        /// </param>
        /// <returns>
        /// The value accessor of the column. When the column has none and reflection is disabled, an accessor that
        /// follows the compiled binding of the column along the same property path, if the binding has one.
        /// </returns>
        internal static IDataGridColumnValueAccessor ResolveValueAccessor(DataGridColumn column, string propertyPath = null)
        {
            var accessor = GetValueAccessor(column);
            if (accessor != null || column == null || DataGridReflectionFeature.IsEnabled)
            {
                return accessor;
            }

            if (string.IsNullOrEmpty(propertyPath))
            {
                propertyPath = column.GetSortPropertyName();
            }

            return DataGridCompiledBindingValueAccessor.Get(column, propertyPath);
        }

        internal static DataGridColumnDefinition GetDefinition(DataGridColumn column)
        {
            return column?.GetValue(DefinitionProperty);
        }

        internal static void SetDefinition(DataGridColumn column, DataGridColumnDefinition definition)
        {
            column?.SetValue(DefinitionProperty, definition);
        }

        internal static void ClearDefinition(DataGridColumn column)
        {
            column?.ClearValue(DefinitionProperty);
        }

        public static Type GetValueType(DataGridColumn column)
        {
            var accessor = GetValueAccessor(column);
            if (accessor != null)
            {
                return accessor.ValueType;
            }

            return column?.GetValue(ValueTypeProperty);
        }

        public static void SetValueType(DataGridColumn column, Type valueType)
        {
            column?.SetValue(ValueTypeProperty, valueType);
        }

        public static void ClearValueType(DataGridColumn column)
        {
            column?.ClearValue(ValueTypeProperty);
        }

        internal static object GetDefinitionKey(DataGridColumnDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return definition.ColumnKey ?? (object)definition;
        }

        internal static object GetColumnId(DataGridColumn column)
        {
            if (column == null)
            {
                return null;
            }

            if (column.ColumnKey != null)
            {
                return column.ColumnKey;
            }

            var definition = GetDefinition(column);
            if (definition != null)
            {
                return GetDefinitionKey(definition);
            }

            return column;
        }

        internal static bool MatchesColumnId(DataGridColumn column, object columnId)
        {
            if (column == null || columnId == null)
            {
                return false;
            }

            if (column.ColumnKey != null && Equals(column.ColumnKey, columnId))
            {
                return true;
            }

            if (ReferenceEquals(columnId, column))
            {
                return true;
            }

            var definition = GetDefinition(column);
            if (definition != null)
            {
                if (ReferenceEquals(columnId, definition))
                {
                    return true;
                }

                if (definition.ColumnKey != null && Equals(definition.ColumnKey, columnId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
