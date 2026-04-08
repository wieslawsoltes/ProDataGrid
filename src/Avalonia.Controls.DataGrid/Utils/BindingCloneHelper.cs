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

        private static bool HasImplicitSource(object? source)
        {
            return source is null || ReferenceEquals(source, AvaloniaProperty.UnsetValue);
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
