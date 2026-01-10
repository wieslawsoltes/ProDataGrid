// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls.DataGridFiltering;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        internal void OnLegacyColumnFilterChanged(DataGridColumn column)
        {
            if (column == null || _filteringModel == null)
            {
                return;
            }

            var propertyPath = column.GetSortPropertyName();
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                propertyPath = null;
            }
            var descriptors = _filteringModel.Descriptors?.ToList() ?? new List<FilteringDescriptor>();
            descriptors.RemoveAll(descriptor =>
                ReferenceEquals(descriptor.ColumnId, column) ||
                (!string.IsNullOrEmpty(propertyPath) &&
                 string.Equals(descriptor.PropertyPath, propertyPath, StringComparison.Ordinal)));

            var descriptor = CreateLegacyFilterDescriptor(column, propertyPath);
            if (descriptor != null)
            {
                descriptors.Add(descriptor);
            }

            using (_filteringModel.DeferRefresh())
            {
                _filteringModel.Apply(descriptors);
            }
        }

        internal void ApplyLegacyColumnFilterIfNeeded(DataGridColumn column)
        {
            if (column == null)
            {
                return;
            }

            if (HasLegacyColumnFilter(column))
            {
                OnLegacyColumnFilterChanged(column);
            }
        }

        internal void RemoveLegacyColumnFilter(DataGridColumn column)
        {
            if (column == null || _filteringModel == null)
            {
                return;
            }

            var propertyPath = column.GetSortPropertyName();
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                propertyPath = null;
            }
            var descriptors = _filteringModel.Descriptors?.ToList() ?? new List<FilteringDescriptor>();
            var removed = descriptors.RemoveAll(descriptor =>
                ReferenceEquals(descriptor.ColumnId, column) ||
                (!string.IsNullOrEmpty(propertyPath) &&
                 string.Equals(descriptor.PropertyPath, propertyPath, StringComparison.Ordinal)));

            if (removed == 0)
            {
                return;
            }

            using (_filteringModel.DeferRefresh())
            {
                _filteringModel.Apply(descriptors);
            }
        }

        private static bool HasLegacyColumnFilter(DataGridColumn column)
        {
            return column != null &&
                (!string.IsNullOrWhiteSpace(column.FilterValue) || column.ContentFilter != null);
        }

        private static FilteringDescriptor CreateLegacyFilterDescriptor(DataGridColumn column, string propertyPath)
        {
            if (!HasLegacyColumnFilter(column))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                propertyPath = null;
            }
            var filterValue = column.FilterValue;
            var contentFilter = column.ContentFilter;
            MethodInfo matchMethod = null;
            if (contentFilter != null)
            {
                matchMethod = contentFilter.GetType().GetMethod("IsMatch", new[] { typeof(object) });
            }

            Func<object, bool> predicate = item =>
                ApplyLegacyFilter(item, propertyPath, filterValue, contentFilter, matchMethod);

            return new FilteringDescriptor(column, FilteringOperator.Custom, propertyPath, predicate: predicate);
        }

        private static bool ApplyLegacyFilter(
            object item,
            string propertyPath,
            string filterValue,
            object contentFilter,
            MethodInfo matchMethod)
        {
            if (item == null)
            {
                return false;
            }

            if (contentFilter != null && matchMethod != null)
            {
                try
                {
                    var cellValue = GetLegacyCellValue(item, propertyPath);
                    var result = matchMethod.Invoke(contentFilter, new[] { cellValue });
                    if (result is bool match)
                    {
                        return match;
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(filterValue))
            {
                return true;
            }

            var valueText = GetLegacyCellText(item, propertyPath);
            if (valueText == null)
            {
                return false;
            }

            return valueText.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static object GetLegacyCellValue(object item, string propertyPath)
        {
            if (!string.IsNullOrWhiteSpace(propertyPath))
            {
                var prop = item.GetType().GetProperty(propertyPath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    return prop.GetValue(item);
                }
            }

            return item;
        }

        private static string GetLegacyCellText(object item, string propertyPath)
        {
            var value = GetLegacyCellValue(item, propertyPath);
            return value?.ToString() ?? string.Empty;
        }
    }
}
