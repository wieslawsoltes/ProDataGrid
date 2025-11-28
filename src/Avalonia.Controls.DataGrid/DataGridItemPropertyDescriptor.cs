// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Avalonia.Controls
{
    /// <summary>
    /// Abstraction over a property, supporting both reflection and component model descriptors.
    /// </summary>
    internal sealed class DataGridItemPropertyDescriptor
    {
        private DataGridItemPropertyDescriptor(
            string name,
            string? displayName,
            Type propertyType,
            bool isReadOnly,
            PropertyInfo? propertyInfo,
            PropertyDescriptor? propertyDescriptor)
        {
            Name = name;
            DisplayName = displayName;
            PropertyType = propertyType;
            IsReadOnly = isReadOnly;
            PropertyInfo = propertyInfo;
            PropertyDescriptor = propertyDescriptor;
        }

        public string Name { get; }

        public string? DisplayName { get; }

        public Type PropertyType { get; }

        public bool IsReadOnly { get; }

        public PropertyInfo? PropertyInfo { get; }

        public PropertyDescriptor? PropertyDescriptor { get; }

        public static DataGridItemPropertyDescriptor[]? CreateDescriptors(IEnumerable? items, Type? dataType)
        {
            // Prefer ITypedList (e.g. DataView) when available.
            if (items is ITypedList typedList)
            {
                var descriptors = typedList.GetItemProperties(null);
                if (descriptors != null && descriptors.Count > 0)
                {
                    return FromPropertyDescriptors(descriptors);
                }
            }

            // Fall back to TypeDescriptor for ICustomTypeDescriptor or TypeDescriptionProvider cases.
            if (dataType != null)
            {
                var descriptors = TypeDescriptor.GetProperties(dataType);
                if (descriptors != null && descriptors.Count > 0)
                {
                    return FromPropertyDescriptors(descriptors);
                }
            }

            // If we still don't have descriptors, try an instance from the sequence.
            if (items != null)
            {
                var representative = TryGetFirst(items);
                if (representative != null)
                {
                    var descriptors = TypeDescriptor.GetProperties(representative);
                    if (descriptors != null && descriptors.Count > 0)
                    {
                        return FromPropertyDescriptors(descriptors);
                    }
                }
            }

            // Last resort: public instance properties via reflection.
            if (dataType != null)
            {
                var properties = dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length > 0)
                {
                    return FromPropertyInfos(properties);
                }
            }

            return null;
        }

        private static DataGridItemPropertyDescriptor[] FromPropertyDescriptors(PropertyDescriptorCollection descriptors)
        {
            return descriptors
                .Cast<PropertyDescriptor>()
                .Select(d => new DataGridItemPropertyDescriptor(
                    d.Name,
                    d.DisplayName,
                    d.PropertyType,
                    d.IsReadOnly,
                    null,
                    d))
                .ToArray();
        }

        private static DataGridItemPropertyDescriptor[] FromPropertyInfos(PropertyInfo[] properties)
        {
            return properties
                .Select(p => new DataGridItemPropertyDescriptor(
                    p.Name,
                    p.Name,
                    p.PropertyType,
                    !p.CanWrite,
                    p,
                    null))
                .ToArray();
        }

        private static object? TryGetFirst(IEnumerable source)
        {
            var enumerator = source.GetEnumerator();
            try
            {
                if (enumerator.MoveNext())
                {
                    return enumerator.Current;
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            return null;
        }
    }
}
