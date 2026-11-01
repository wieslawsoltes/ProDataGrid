// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Sorting adapter that routes sorting descriptors into hierarchical sibling ordering.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class HierarchicalSortingAdapter : DataGridSortingAdapter
    {
        private readonly IHierarchicalModel _model;
        private readonly IComparer<object>? _defaultComparer;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        HierarchicalSortingAdapter(
            IHierarchicalModel model,
            ISortingModel sortingModel,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            DataGridFastPathOptions options,
            IComparer<object>? defaultComparer = null,
            Action? beforeViewRefresh = null,
            Action? afterViewRefresh = null)
            : base(sortingModel, columnProvider, options, beforeViewRefresh, afterViewRefresh)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _defaultComparer = defaultComparer;
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        HierarchicalSortingAdapter(
            IHierarchicalModel model,
            ISortingModel sortingModel,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            IComparer<object>? defaultComparer = null,
            Action? beforeViewRefresh = null,
            Action? afterViewRefresh = null)
            : this(model, sortingModel, columnProvider, options: null, defaultComparer, beforeViewRefresh, afterViewRefresh)
        {
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<SortingDescriptor> descriptors,
            IReadOnlyList<SortingDescriptor> previousDescriptors,
            out bool changed)
        {
            var comparer = HierarchicalSiblingComparerBuilder.Build(descriptors, _defaultComparer ?? _model.Options.SiblingComparer);
            _model.ApplySiblingComparer(comparer, recursive: true);
            changed = true;
            return true;
        }
    }

    internal static class HierarchicalSiblingComparerBuilder
    {
        public static IComparer<object>? Build(IReadOnlyList<SortingDescriptor> descriptors, IComparer<object>? defaultComparer)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return defaultComparer;
            }

            var compiled = new List<CompiledComparer>(descriptors.Count);
            foreach (var descriptor in descriptors)
            {
                var accessor = descriptor.HasComparer
                    ? Accessor.Identity
                    : Accessor.Create(descriptor.PropertyPath);

                var comparer = descriptor.HasComparer
                    ? descriptor.Comparer
                    : CreateDefaultComparer(descriptor.Culture);

                compiled.Add(new CompiledComparer(accessor, comparer, descriptor.Direction));
            }

            return Comparer<object>.Create((left, right) =>
            {
                foreach (var comparer in compiled)
                {
                    var result = comparer.Compare(left, right);
                    if (result != 0)
                    {
                        return result;
                    }
                }

                return 0;
            });
        }

        private static IComparer CreateDefaultComparer(CultureInfo? culture)
        {
            return culture != null ? new Comparer(culture) : Comparer.Default;
        }

        private readonly struct CompiledComparer
        {
            private readonly Accessor _accessor;
            private readonly IComparer _comparer;
            private readonly ListSortDirection _direction;

            public CompiledComparer(Accessor accessor, IComparer comparer, ListSortDirection direction)
            {
                _accessor = accessor;
                _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
                _direction = direction;
            }

            public int Compare(object? left, object? right)
            {
                var leftValue = _accessor.GetValue(left);
                var rightValue = _accessor.GetValue(right);

                if (ReferenceEquals(leftValue, rightValue))
                {
                    return 0;
                }

                if (leftValue is null)
                {
                    return -1;
                }

                if (rightValue is null)
                {
                    return 1;
                }

                var result = _comparer.Compare(leftValue, rightValue);
                return _direction == ListSortDirection.Descending ? -result : result;
            }
        }

        private sealed class Accessor
        {
            private readonly Func<object?, object?> _getter;

            private Accessor(Func<object?, object?> getter)
            {
                _getter = getter;
            }

            public static Accessor Identity { get; } = new Accessor(x => x);

            public object? GetValue(object? target) => _getter(target);

            public static Accessor Create(string? propertyPath)
            {
                if (string.IsNullOrWhiteSpace(propertyPath))
                {
                    return Identity;
                }

                propertyPath = propertyPath.Trim();
                const string itemPrefix = "Item.";
                string[]? fallbackParts = null;
                if (propertyPath.StartsWith(itemPrefix, StringComparison.Ordinal))
                {
                    var strippedPath = propertyPath.Substring(itemPrefix.Length);
                    if (string.IsNullOrWhiteSpace(strippedPath))
                    {
                        return Identity;
                    }

                    fallbackParts = strippedPath.Split('.');
                }

                var parts = propertyPath.Split('.');
                var cache = new Dictionary<Type, PropertyInfo[]>();

                return new Accessor(target =>
                {
                    if (target == null)
                    {
                        return null;
                    }

                    var type = target.GetType();
                    if (!cache.TryGetValue(type, out var properties))
                    {
                        properties = ResolveParts(type, parts)
                            ?? (fallbackParts != null ? ResolveParts(type, fallbackParts) : null);
                        if (properties == null)
                        {
                            return null;
                        }

                        cache[type] = properties;
                    }

                    object? current = target;
                    foreach (var property in properties)
                    {
                        if (current == null)
                        {
                            return null;
                        }

                        current = property.GetValue(current);
                    }

                    return current;
                });
            }

            private static PropertyInfo[]? ResolveParts(Type targetType, string[] parts)
            {
                var properties = new PropertyInfo[parts.Length];
                var currentType = targetType;

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Trim();
                    if (string.IsNullOrEmpty(part))
                    {
                        return null;
                    }

                    var property = GetProperty(currentType, part);
                    if (property == null)
                    {
                        return null;
                    }

                    properties[i] = property;
                    currentType = property.PropertyType;
                }

                return properties;
            }

            private static PropertyInfo? GetProperty(Type targetType, string name)
            {
                if (!DataGridReflectionOptions.IsEnabled)
                {
                    throw DataGridReflectionOptions.CreateException("Property-path hierarchy sorting");
                }

                return targetType.GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    returnType: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
            }
        }
    }
}
