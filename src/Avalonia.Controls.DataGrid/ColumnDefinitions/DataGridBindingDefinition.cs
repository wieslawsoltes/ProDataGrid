// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridBindingDefinition
    {
        private readonly CompiledBindingPath _path;
        private readonly Type _itemType;
        private readonly Type _valueType;
        private readonly IDataGridColumnValueAccessor _valueAccessor;

        public BindingMode? Mode { get; set; }

        public BindingPriority? Priority { get; set; }

        public UpdateSourceTrigger? UpdateSourceTrigger { get; set; }

        public int? Delay { get; set; }

        public IValueConverter Converter { get; set; }

        public CultureInfo ConverterCulture { get; set; }

        public object ConverterParameter { get; set; }

        public string StringFormat { get; set; }

        public object FallbackValue { get; set; }

        public object TargetNullValue { get; set; }

        internal IDataGridColumnValueAccessor ValueAccessor => _valueAccessor;

        internal Type ValueType => _valueType;

        internal Type ItemType => _itemType;

        private DataGridBindingDefinition(
            CompiledBindingPath path,
            Type itemType,
            Type valueType,
            IDataGridColumnValueAccessor valueAccessor)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _itemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
            _valueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            _valueAccessor = valueAccessor ?? throw new ArgumentNullException(nameof(valueAccessor));
        }

#if NET8_0_OR_GREATER
        [RequiresDynamicCode(
            "Expression-based column bindings require dynamic code generation. " +
            "Use DataGridBindingDefinition.Create with a prebuilt CompiledBindingPath or property info for AOT.")]
#endif
        public static DataGridBindingDefinition Create<TItem, TValue>(
            Expression<Func<TItem, TValue>> expression,
            Action<TItem, TValue> setter = null)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

#if NET6_0_OR_GREATER
            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                throw new NotSupportedException(
                    "Expression-based column bindings require dynamic code generation. " +
                    "Use DataGridBindingDefinition.Create with a prebuilt CompiledBindingPath or property info for AOT.");
            }
#endif

            var path = DataGridCompiledBindingPathBuilder.Build(expression);
            var getter = expression.Compile();
            var accessor = new DataGridColumnValueAccessor<TItem, TValue>(getter, setter ?? TryCreateSetter(expression));
            return new DataGridBindingDefinition(path, typeof(TItem), typeof(TValue), accessor);
        }

        public static DataGridBindingDefinition Create<TItem, TValue>(
            CompiledBindingPath path,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (getter == null)
            {
                throw new ArgumentNullException(nameof(getter));
            }

            var accessor = new DataGridColumnValueAccessor<TItem, TValue>(getter, setter);
            return new DataGridBindingDefinition(path, typeof(TItem), typeof(TValue), accessor);
        }

        public static DataGridBindingDefinition Create<TItem, TValue>(
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null)
        {
            var path = BuildPath(property);
            return Create(path, getter, setter);
        }

        public static DataGridBindingDefinition CreateCached<TItem, TValue>(
            IPropertyInfo property,
            Func<TItem, TValue> getter,
            Action<TItem, TValue> setter = null)
        {
            var path = DataGridCompiledBindingPathCache.GetOrCreate(property);
            return Create(path, getter, setter);
        }

        internal BindingBase CreateBinding()
        {
            var binding = new CompiledBindingExtension
            {
                Path = _path,
                DataType = _itemType,
                Converter = Converter,
                ConverterCulture = ConverterCulture,
                ConverterParameter = ConverterParameter,
                StringFormat = StringFormat,
                FallbackValue = FallbackValue,
                TargetNullValue = TargetNullValue
            };

            if (Mode.HasValue)
            {
                binding.Mode = Mode.Value;
            }

            if (Priority.HasValue)
            {
                binding.Priority = Priority.Value;
            }

            if (UpdateSourceTrigger.HasValue)
            {
                binding.UpdateSourceTrigger = UpdateSourceTrigger.Value;
            }

            if (Delay.HasValue)
            {
                binding.Delay = Delay.Value;
            }

            return binding;
        }

        internal static CompiledBindingPath BuildPath(IPropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            Func<WeakReference<object>, IPropertyInfo, IPropertyAccessor> accessorFactory = property is AvaloniaProperty
                ? PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor
                : PropertyInfoAccessorFactory.CreateInpcPropertyAccessor;

            return new CompiledBindingPathBuilder()
                .Property(property, accessorFactory)
                .Build();
        }

        private static Action<TItem, TValue> TryCreateSetter<TItem, TValue>(Expression<Func<TItem, TValue>> expression)
        {
            var body = expression.Body;
            if (body is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            if (body is MemberExpression member && member.Member is System.Reflection.PropertyInfo property)
            {
                if (property.SetMethod == null)
                {
                    return null;
                }

                var target = expression.Parameters[0];
                var value = Expression.Parameter(typeof(TValue), "value");
                Expression valueExpression = value;
                if (property.PropertyType != typeof(TValue))
                {
                    valueExpression = Expression.Convert(value, property.PropertyType);
                }

                var assign = Expression.Assign(member, valueExpression);
                return Expression.Lambda<Action<TItem, TValue>>(assign, target, value).Compile();
            }

            if (body is IndexExpression index && index.Indexer?.SetMethod != null)
            {
                var target = expression.Parameters[0];
                var value = Expression.Parameter(typeof(TValue), "value");
                Expression valueExpression = value;
                if (index.Type != typeof(TValue))
                {
                    valueExpression = Expression.Convert(value, index.Type);
                }

                var assign = Expression.Assign(index, valueExpression);
                return Expression.Lambda<Action<TItem, TValue>>(assign, target, value).Compile();
            }

            return null;
        }
    }
}
