// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
            return DataGridReflectionOptions.IsEnabled ? CreateDescriptorsWithReflection(items, dataType) : null;
        }

        [RequiresUnreferencedCode("Item descriptor discovery requires public item properties and TypeDescriptor metadata.")]
        private static DataGridItemPropertyDescriptor[]? CreateDescriptorsWithReflection(IEnumerable? items, Type? dataType)
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

            // Prefer reflection for simple POCOs when dynamic code is available and no custom provider exists.
            if (dataType != null)
            {
                var provider = TypeDescriptor.GetProvider(dataType);
                var defaultProviderType = TypeDescriptor.GetProvider(typeof(object)).GetType();
                var hasProviderAttribute = TypeDescriptor.GetAttributes(dataType).OfType<TypeDescriptionProviderAttribute>().Any();
                var hasCustomProvider = hasProviderAttribute || (provider != null && provider.GetType() != defaultProviderType);
                var hasCustomDescriptor = typeof(ICustomTypeDescriptor).IsAssignableFrom(dataType);

                if (!hasCustomProvider && !hasCustomDescriptor)
                {
                    var properties = dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    if (properties.Length > 0)
                    {
                        return FromPropertyInfos(properties);
                    }
                }
            }

            // On AOT where TypeDescriptor metadata is commonly trimmed, fall back directly to reflection.
            if (dataType != null && !IsDynamicCodeSupported())
            {
                var properties = dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length > 0)
                {
                    return FromPropertyInfos(properties);
                }
            }

            // Fall back to TypeDescriptor for ICustomTypeDescriptor or TypeDescriptionProvider cases.
            if (dataType != null)
            {
                var descriptors = GetTypeDescriptorPropertiesSafe(dataType);
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
                    if (!IsDynamicCodeSupported())
                    {
                        var properties = representative.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        if (properties.Length > 0)
                        {
                            return FromPropertyInfos(properties);
                        }
                    }

                    var descriptors = GetTypeDescriptorPropertiesSafe(representative);
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

        private static bool IsDynamicCodeSupported()
        {
#if NET6_0_OR_GREATER
            return RuntimeFeature.IsDynamicCodeSupported;
#else
            // RuntimeFeature is unavailable on netstandard2.0; assume dynamic code is supported.
            return true;
#endif
        }

        [RequiresUnreferencedCode("TypeDescriptor requires item metadata that trimming cannot infer.")]
        private static PropertyDescriptorCollection? GetTypeDescriptorPropertiesSafe(object target)
        {
            try
            {
                return TypeDescriptor.GetProperties(target);
            }
            catch
            {
                // TypeDescriptor can fail or be trimmed in AOT; fall back to reflection path.
                return null;
            }
        }
    }
}
