// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using Avalonia.Controls.Utils;

namespace Avalonia.Controls
{
    /// <summary>
    /// Calculator for Sum aggregate.
    /// </summary>
    internal class SumCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Sum";

        public bool SupportsIncremental => true;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            decimal sum = 0;
            bool hasValues = false;

            foreach (var item in items)
            {
                var value = accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
                if (value != null)
                {
                    try
                    {
                        sum += Convert.ToDecimal(value);
                        hasValues = true;
                    }
                    catch
                    {
                        // Skip non-numeric values
                    }
                }
            }

            return hasValues ? sum : null;
        }

        public IDataGridSummaryState? CreateState() => new SumState();

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }

        private class SumState : IDataGridSummaryState
        {
            private decimal _sum;
            private int _count;

            public void Reset()
            {
                _sum = 0;
                _count = 0;
            }

            public void Add(object? value)
            {
                if (value != null)
                {
                    try
                    {
                        _sum += Convert.ToDecimal(value);
                        _count++;
                    }
                    catch
                    {
                        // Skip non-numeric values
                    }
                }
            }

            public void Remove(object? value)
            {
                if (value != null)
                {
                    try
                    {
                        _sum -= Convert.ToDecimal(value);
                        _count--;
                    }
                    catch
                    {
                        // Skip non-numeric values
                    }
                }
            }

            public object? GetResult() => _count > 0 ? _sum : null;
        }
    }

    /// <summary>
    /// Calculator for Average aggregate.
    /// </summary>
    internal class AverageCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Average";

        public bool SupportsIncremental => true;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            decimal sum = 0;
            int count = 0;

            foreach (var item in items)
            {
                var value = accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
                if (value != null)
                {
                    try
                    {
                        sum += Convert.ToDecimal(value);
                        count++;
                    }
                    catch
                    {
                        // Skip non-numeric values
                    }
                }
            }

            return count > 0 ? sum / count : null;
        }

        public IDataGridSummaryState? CreateState() => new AverageState();

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }

        private class AverageState : IDataGridSummaryState
        {
            private decimal _sum;
            private int _count;

            public void Reset()
            {
                _sum = 0;
                _count = 0;
            }

            public void Add(object? value)
            {
                if (value != null)
                {
                    try
                    {
                        _sum += Convert.ToDecimal(value);
                        _count++;
                    }
                    catch
                    {
                        // Skip non-numeric values
                    }
                }
            }

            public void Remove(object? value)
            {
                if (value != null)
                {
                    try
                    {
                        _sum -= Convert.ToDecimal(value);
                        _count--;
                    }
                    catch
                    {
                        // Skip non-numeric values
                    }
                }
            }

            public object? GetResult() => _count > 0 ? _sum / _count : null;
        }
    }

    /// <summary>
    /// Calculator for Count aggregate.
    /// </summary>
    internal class CountCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Count";

        public bool SupportsIncremental => true;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            int count = 0;
            foreach (var _ in items)
            {
                count++;
            }
            return count;
        }

        public IDataGridSummaryState? CreateState() => new CountState();

        private class CountState : IDataGridSummaryState
        {
            private int _count;

            public void Reset() => _count = 0;

            public void Add(object? value) => _count++;

            public void Remove(object? value) => _count--;

            public object? GetResult() => _count;
        }
    }

    /// <summary>
    /// Calculator for CountDistinct aggregate.
    /// </summary>
    internal class CountDistinctCalculator : IDataGridSummaryCalculator
    {
        public string Name => "CountDistinct";

        public bool SupportsIncremental => false;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var distinctValues = new System.Collections.Generic.HashSet<object>();

            foreach (var item in items)
            {
                var value = accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
                if (value != null)
                {
                    distinctValues.Add(value);
                }
            }

            return distinctValues.Count;
        }

        public IDataGridSummaryState? CreateState() => null;

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }
    }

    /// <summary>
    /// Calculator for Min aggregate.
    /// </summary>
    internal class MinCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Min";

        public bool SupportsIncremental => false;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            object? min = null;
            IComparable? minComparable = null;

            foreach (var item in items)
            {
                var value = accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
                if (value is IComparable comparable)
                {
                    if (minComparable == null || comparable.CompareTo(minComparable) < 0)
                    {
                        min = value;
                        minComparable = comparable;
                    }
                }
            }

            return min;
        }

        public IDataGridSummaryState? CreateState() => null;

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }
    }

    /// <summary>
    /// Calculator for Max aggregate.
    /// </summary>
    internal class MaxCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Max";

        public bool SupportsIncremental => false;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            object? max = null;
            IComparable? maxComparable = null;

            foreach (var item in items)
            {
                var value = accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
                if (value is IComparable comparable)
                {
                    if (maxComparable == null || comparable.CompareTo(maxComparable) > 0)
                    {
                        max = value;
                        maxComparable = comparable;
                    }
                }
            }

            return max;
        }

        public IDataGridSummaryState? CreateState() => null;

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }
    }

    /// <summary>
    /// Calculator for First aggregate.
    /// </summary>
    internal class FirstCalculator : IDataGridSummaryCalculator
    {
        public string Name => "First";

        public bool SupportsIncremental => false;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            foreach (var item in items)
            {
                return accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
            }

            return null;
        }

        public IDataGridSummaryState? CreateState() => null;

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }
    }

    /// <summary>
    /// Calculator for Last aggregate.
    /// </summary>
    internal class LastCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Last";

        public bool SupportsIncremental => false;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null && string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            object? last = null;
            foreach (var item in items)
            {
                last = accessor != null
                    ? accessor.GetValue(item)
                    : GetPropertyValue(item, propertyName);
            }

            return last;
        }

        public IDataGridSummaryState? CreateState() => null;

        private static object? GetPropertyValue(object? item, string? propertyName)
        {
            return ReflectionPropertyPath.GetValue(item, propertyName);
        }
    }
}
