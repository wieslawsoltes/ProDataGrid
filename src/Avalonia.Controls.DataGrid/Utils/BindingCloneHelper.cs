// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Avalonia.Controls.Utils
{
    internal static class BindingCloneHelper
    {
        public static bool TryCreateExplicitBinding(BindingBase binding, out BindingBase explicitBinding)
        {
            switch (binding)
            {
                case Binding avaloniaBinding:
                    explicitBinding = CloneBinding(avaloniaBinding);
                    return true;
                case CompiledBindingExtension compiledBinding:
                    explicitBinding = CloneBinding(compiledBinding);
                    return true;
                case ReflectionBinding reflectionBinding:
                    explicitBinding = CloneBinding(reflectionBinding);
                    return true;
                case CompiledBinding compiledBinding:
                    explicitBinding = CloneBinding(compiledBinding);
                    return true;
                default:
                    explicitBinding = binding;
                    return false;
            }
        }

        public static string? GetPath(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.Path,
                ReflectionBinding reflectionBinding => reflectionBinding.Path,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.Path?.ToString(),
                CompiledBinding compiledBinding => compiledBinding.Path?.ToString(),
                _ => null
            };
        }

        public static BindingMode GetMode(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.Mode,
                ReflectionBinding reflectionBinding => reflectionBinding.Mode,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.Mode,
                CompiledBinding compiledBinding => compiledBinding.Mode,
                MultiBinding multiBinding => multiBinding.Mode,
                _ => BindingMode.Default
            };
        }

        public static bool TrySetMode(BindingBase? binding, BindingMode mode)
        {
            switch (binding)
            {
                case Binding avaloniaBinding:
                    avaloniaBinding.Mode = mode;
                    return true;
                case ReflectionBinding reflectionBinding:
                    reflectionBinding.Mode = mode;
                    return true;
                case CompiledBindingExtension compiledBindingExtension:
                    compiledBindingExtension.Mode = mode;
                    return true;
                case CompiledBinding compiledBinding:
                    compiledBinding.Mode = mode;
                    return true;
                case MultiBinding multiBinding:
                    multiBinding.Mode = mode;
                    return true;
                default:
                    return false;
            }
        }

        public static IValueConverter? GetConverter(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.Converter,
                ReflectionBinding reflectionBinding => reflectionBinding.Converter,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.Converter,
                CompiledBinding compiledBinding => compiledBinding.Converter,
                _ => null
            };
        }

        public static bool TrySetConverter(BindingBase? binding, IValueConverter? converter)
        {
            switch (binding)
            {
                case Binding avaloniaBinding:
                    avaloniaBinding.Converter = converter;
                    return true;
                case ReflectionBinding reflectionBinding:
                    reflectionBinding.Converter = converter;
                    return true;
                case CompiledBindingExtension compiledBindingExtension:
                    compiledBindingExtension.Converter = converter;
                    return true;
                case CompiledBinding compiledBinding:
                    compiledBinding.Converter = converter;
                    return true;
                default:
                    return false;
            }
        }

        public static CultureInfo? GetConverterCulture(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.ConverterCulture,
                ReflectionBinding reflectionBinding => reflectionBinding.ConverterCulture,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.ConverterCulture,
                CompiledBinding compiledBinding => compiledBinding.ConverterCulture,
                MultiBinding multiBinding => multiBinding.ConverterCulture,
                _ => null
            };
        }

        public static object? GetConverterParameter(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.ConverterParameter,
                ReflectionBinding reflectionBinding => reflectionBinding.ConverterParameter,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.ConverterParameter,
                CompiledBinding compiledBinding => compiledBinding.ConverterParameter,
                MultiBinding multiBinding => multiBinding.ConverterParameter,
                _ => null
            };
        }

        public static string? GetStringFormat(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.StringFormat,
                ReflectionBinding reflectionBinding => reflectionBinding.StringFormat,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.StringFormat,
                CompiledBinding compiledBinding => compiledBinding.StringFormat,
                MultiBinding multiBinding => multiBinding.StringFormat,
                _ => null
            };
        }

        public static bool SupportsDirectDataContextMemberWrite(BindingBase? binding)
        {
            var mode = GetMode(binding);
            var converter = GetConverter(binding);
            if (string.IsNullOrWhiteSpace(GetPath(binding)) ||
                mode == BindingMode.OneWay ||
                mode == BindingMode.OneWayToSource ||
                mode == BindingMode.OneTime ||
                (converter != null && !ReferenceEquals(converter, DataGridValueConverter.Instance)) ||
                !string.IsNullOrWhiteSpace(GetStringFormat(binding)))
            {
                return false;
            }

            return binding switch
            {
                Binding avaloniaBinding => HasImplicitSource(avaloniaBinding.Source) &&
                                           string.IsNullOrWhiteSpace(avaloniaBinding.ElementName) &&
                                           avaloniaBinding.RelativeSource is null,
                ReflectionBinding reflectionBinding => HasImplicitSource(reflectionBinding.Source) &&
                                                       string.IsNullOrWhiteSpace(reflectionBinding.ElementName) &&
                                                       reflectionBinding.RelativeSource is null,
                CompiledBindingExtension compiledBindingExtension => HasImplicitSource(compiledBindingExtension.Source),
                CompiledBinding compiledBinding => HasImplicitSource(compiledBinding.Source),
                _ => false
            };
        }

        public static bool SupportsDirectDataContextRead(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => HasImplicitSource(avaloniaBinding.Source) &&
                                           string.IsNullOrWhiteSpace(avaloniaBinding.ElementName) &&
                                           avaloniaBinding.RelativeSource is null,
                ReflectionBinding reflectionBinding => HasImplicitSource(reflectionBinding.Source) &&
                                                       string.IsNullOrWhiteSpace(reflectionBinding.ElementName) &&
                                                       reflectionBinding.RelativeSource is null,
                CompiledBindingExtension compiledBindingExtension => HasImplicitSource(compiledBindingExtension.Source),
                CompiledBinding compiledBinding => HasImplicitSource(compiledBinding.Source),
                _ => false
            };
        }

        public static bool SupportsDirectTextDataContextRead(
            BindingBase? binding,
            bool observesWrappedHierarchyItem = false)
        {
            return SupportsDirectTextDataContextRead(
                binding,
                observesWrappedHierarchyItem,
                out _);
        }

        public static bool SupportsDirectTextDataContextRead(
            BindingBase? binding,
            bool observesWrappedHierarchyItem,
            out bool requiresWrappedHierarchyItemObservation)
        {
            requiresWrappedHierarchyItemObservation = false;
            if (!SupportsDirectDataContextRead(binding) ||
                !HasSupportedDirectReadMode(binding) ||
                !HasDefaultDirectReadAnchorAndPriority(binding) ||
                !HasDefaultFallbackValues(binding) ||
                HasReadDelay(binding) ||
                !HasObservableDirectReadPath(
                    binding,
                    observesWrappedHierarchyItem,
                    out requiresWrappedHierarchyItemObservation))
            {
                return false;
            }

            // Arbitrary converters can depend on binding-engine behavior such as UnsetValue,
            // DoNothing, target-null handling, or a target property type. Keep those bindings on
            // Avalonia's retained binding path. The grid's default converter is known to perform
            // only the standard value-to-string conversion reproduced by the typed accessor.
            var converter = GetConverter(binding);
            if (converter == null || ReferenceEquals(converter, DataGridValueConverter.Instance))
            {
                return true;
            }

            requiresWrappedHierarchyItemObservation = false;
            return false;
        }

        private static bool HasObservableDirectReadPath(
            BindingBase? binding,
            bool observesWrappedHierarchyItem,
            out bool requiresWrappedHierarchyItemObservation)
        {
            requiresWrappedHierarchyItemObservation = false;
            var path = GetPath(binding);
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            path = path.Trim();
            if (path.IndexOf('.') < 0 && path.IndexOf('[') < 0)
            {
                return true;
            }

            const string wrappedItemPrefix = "Item.";
            requiresWrappedHierarchyItemObservation = observesWrappedHierarchyItem &&
                                                       path.StartsWith(
                                                           wrappedItemPrefix,
                                                           System.StringComparison.Ordinal) &&
                                                       path.IndexOf('.', wrappedItemPrefix.Length) < 0 &&
                                                       path.IndexOf('[', wrappedItemPrefix.Length) < 0;
            return requiresWrappedHierarchyItemObservation;
        }

        public static bool SupportsDirectRawDataContextRead(BindingBase? binding)
        {
            if (!SupportsDirectTextDataContextRead(binding) ||
                !string.IsNullOrWhiteSpace(GetStringFormat(binding)))
            {
                return false;
            }

            var converter = GetConverter(binding);
            return converter == null || ReferenceEquals(converter, DataGridValueConverter.Instance);
        }

        private static bool HasImplicitSource(object? source)
        {
            return source is null || ReferenceEquals(source, AvaloniaProperty.UnsetValue);
        }

        private static bool HasSupportedDirectReadMode(BindingBase? binding)
        {
            var mode = GetMode(binding);
            return mode == BindingMode.Default ||
                   mode == BindingMode.OneWay ||
                   mode == BindingMode.TwoWay;
        }

        private static bool HasReadDelay(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding => avaloniaBinding.Delay > 0,
                ReflectionBinding reflectionBinding => reflectionBinding.Delay > 0,
                CompiledBindingExtension compiledBindingExtension => compiledBindingExtension.Delay > 0,
                CompiledBinding compiledBinding => compiledBinding.Delay > 0,
                _ => false
            };
        }

        private static bool HasDefaultDirectReadAnchorAndPriority(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding =>
                    avaloniaBinding.Priority == BindingPriority.LocalValue &&
                    avaloniaBinding.DefaultAnchor is null,
                ReflectionBinding reflectionBinding =>
                    reflectionBinding.Priority == BindingPriority.LocalValue &&
                    reflectionBinding.DefaultAnchor is null,
                CompiledBindingExtension compiledBindingExtension =>
                    compiledBindingExtension.Priority == BindingPriority.LocalValue &&
                    compiledBindingExtension.DefaultAnchor is null,
                CompiledBinding compiledBinding =>
                    compiledBinding.Priority == BindingPriority.LocalValue &&
                    compiledBinding.DefaultAnchor is null,
                _ => false
            };
        }

        private static bool HasDefaultFallbackValues(BindingBase? binding)
        {
            return binding switch
            {
                Binding avaloniaBinding =>
                    ReferenceEquals(avaloniaBinding.FallbackValue, AvaloniaProperty.UnsetValue) &&
                    ReferenceEquals(avaloniaBinding.TargetNullValue, AvaloniaProperty.UnsetValue),
                ReflectionBinding reflectionBinding =>
                    ReferenceEquals(reflectionBinding.FallbackValue, AvaloniaProperty.UnsetValue) &&
                    ReferenceEquals(reflectionBinding.TargetNullValue, AvaloniaProperty.UnsetValue),
                CompiledBindingExtension compiledBindingExtension =>
                    ReferenceEquals(compiledBindingExtension.FallbackValue, AvaloniaProperty.UnsetValue) &&
                    ReferenceEquals(compiledBindingExtension.TargetNullValue, AvaloniaProperty.UnsetValue),
                CompiledBinding compiledBinding =>
                    ReferenceEquals(compiledBinding.FallbackValue, AvaloniaProperty.UnsetValue) &&
                    ReferenceEquals(compiledBinding.TargetNullValue, AvaloniaProperty.UnsetValue),
                _ => false
            };
        }

        private static Binding CloneBinding(Binding source)
        {
            return new Binding
            {
                Path = source.Path,
                ElementName = source.ElementName,
                RelativeSource = source.RelativeSource,
                Source = source.Source,
                TypeResolver = source.TypeResolver,
                Delay = source.Delay,
                Converter = source.Converter,
                ConverterCulture = source.ConverterCulture,
                ConverterParameter = source.ConverterParameter,
                FallbackValue = source.FallbackValue,
                TargetNullValue = source.TargetNullValue,
                Mode = source.Mode,
                Priority = source.Priority,
                StringFormat = source.StringFormat,
                DefaultAnchor = source.DefaultAnchor,
                NameScope = source.NameScope,
                UpdateSourceTrigger = UpdateSourceTrigger.Explicit
            };
        }

        private static ReflectionBinding CloneBinding(ReflectionBinding source)
        {
            return new ReflectionBinding(source.Path)
            {
                ElementName = source.ElementName,
                RelativeSource = source.RelativeSource,
                Source = source.Source,
                TypeResolver = source.TypeResolver,
                Delay = source.Delay,
                Converter = source.Converter,
                ConverterCulture = source.ConverterCulture,
                ConverterParameter = source.ConverterParameter,
                FallbackValue = source.FallbackValue,
                TargetNullValue = source.TargetNullValue,
                Mode = source.Mode,
                Priority = source.Priority,
                StringFormat = source.StringFormat,
                UpdateSourceTrigger = UpdateSourceTrigger.Explicit
            };
        }

        private static CompiledBindingExtension CloneBinding(CompiledBindingExtension source)
        {
            return new CompiledBindingExtension
            {
                Path = source.Path,
                Delay = source.Delay,
                Converter = source.Converter,
                ConverterCulture = source.ConverterCulture,
                ConverterParameter = source.ConverterParameter,
                FallbackValue = source.FallbackValue,
                TargetNullValue = source.TargetNullValue,
                Mode = source.Mode,
                Priority = source.Priority,
                StringFormat = source.StringFormat,
                Source = source.Source,
                DataType = source.DataType,
                DefaultAnchor = source.DefaultAnchor,
                NameScope = source.NameScope,
                UpdateSourceTrigger = UpdateSourceTrigger.Explicit
            };
        }

        private static CompiledBinding CloneBinding(CompiledBinding source)
        {
            return new CompiledBinding
            {
                Path = source.Path,
                Delay = source.Delay,
                Converter = source.Converter,
                ConverterCulture = source.ConverterCulture,
                ConverterParameter = source.ConverterParameter,
                FallbackValue = source.FallbackValue,
                TargetNullValue = source.TargetNullValue,
                Mode = source.Mode,
                Priority = source.Priority,
                StringFormat = source.StringFormat,
                Source = source.Source,
                UpdateSourceTrigger = UpdateSourceTrigger.Explicit
            };
        }
    }
}
