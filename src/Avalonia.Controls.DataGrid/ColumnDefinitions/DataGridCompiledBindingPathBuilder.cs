// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace Avalonia.Controls
{
    internal sealed class DataGridCompiledBindingPathBuilder : ExpressionVisitor
    {
        private const string IndexerGetterName = "get_Item";
        private const string IndexerPropertyName = "Item";
        private const string MultiDimensionalArrayGetterMethodName = "Get";
        private const string StreamBindingMethodName = "StreamBinding";
        private static readonly PropertyInfo AvaloniaObjectIndexer;
        private readonly LambdaExpression _rootExpression;
        private readonly CompiledBindingPathBuilder _builder = new();
        private Expression _head;

        static DataGridCompiledBindingPathBuilder()
        {
            AvaloniaObjectIndexer = typeof(AvaloniaObject).GetProperty("Item", new[] { typeof(AvaloniaProperty) });
        }

        private DataGridCompiledBindingPathBuilder(LambdaExpression expression)
        {
            _rootExpression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

#if NET8_0_OR_GREATER
        [RequiresDynamicCode(
            "Compiled binding path construction from expressions requires dynamic code generation. " +
            "Use a prebuilt CompiledBindingPath or property info for AOT.")]
#endif
        public static CompiledBindingPath Build<TIn, TOut>(Expression<Func<TIn, TOut>> expression)
        {
            var visitor = new DataGridCompiledBindingPathBuilder(expression);
            visitor.Visit(expression);
            return visitor._builder.Build();
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.ArrayIndex)
            {
                return Visit(Expression.MakeIndex(node.Left, null, new[] { node.Right }));
            }

            throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            if (node.Indexer == AvaloniaObjectIndexer)
            {
                var property = GetValue<AvaloniaProperty>(node.Arguments[0]);
                return Add(node.Object, node, x => x.Property(property, PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor));
            }

            if (node.Object?.Type.IsArray == true)
            {
                var indexes = node.Arguments.Select(GetValue<int>).ToArray();
                return Add(node.Object, node, x => x.ArrayElement(indexes, node.Type));
            }

            if (node.Indexer?.GetMethod is not null &&
                node.Arguments.Count == 1 &&
                node.Arguments[0].Type == typeof(int))
            {
                var getMethod = node.Indexer.GetMethod;
                var setMethod = node.Indexer.SetMethod;
                var index = GetValue<int>(node.Arguments[0]);
                var indexes = new object[] { index };
                var info = new ClrPropertyInfo(
                    IndexerPropertyName,
                    CreateIndexerGetter(getMethod, indexes),
                    CreateIndexerSetter(setMethod, indexes),
                    getMethod.ReturnType);
                return Add(node.Object, node, x => x.Property(
                    info,
                    (x, i) => PropertyInfoAccessorFactory.CreateIndexerPropertyAccessor(x, i, index)));
            }

            if (node.Indexer?.GetMethod is not null)
            {
                var getMethod = node.Indexer.GetMethod;
                var setMethod = node.Indexer.SetMethod;
                var indexes = node.Arguments.Select(GetValue<object>).ToArray();
                var info = new ClrPropertyInfo(
                    IndexerPropertyName,
                    CreateIndexerGetter(getMethod, indexes),
                    CreateIndexerSetter(setMethod, indexes),
                    getMethod.ReturnType);
                return Add(node.Object, node, x => x.Property(
                    info,
                    PropertyInfoAccessorFactory.CreateInpcPropertyAccessor));
            }

            throw new ExpressionParseException(0, $"Invalid indexer in binding expression: {node.NodeType}.");
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.MemberType != MemberTypes.Property)
            {
                throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");
            }

            if (typeof(AvaloniaObject).IsAssignableFrom(node.Expression?.Type) &&
                AvaloniaPropertyRegistry.Instance.FindRegistered(node.Expression.Type, node.Member.Name) is { } avaloniaProperty)
            {
                return Add(
                    node.Expression,
                    node,
                    x => x.Property(avaloniaProperty, PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor));
            }

            var property = (PropertyInfo)node.Member;
            var info = new ClrPropertyInfo(
                property.Name,
                CreateGetter(property),
                CreateSetter(property),
                property.PropertyType);
            return Add(node.Expression, node, x => x.Property(info, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor));
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var method = node.Method;

            if (method.Name == IndexerGetterName && node.Object is not null)
            {
                var property = TryGetPropertyFromMethod(method);
                return Visit(Expression.MakeIndex(node.Object, property, node.Arguments));
            }

            if (method.Name == MultiDimensionalArrayGetterMethodName && node.Object is not null)
            {
                var indexes = node.Arguments.Select(GetValue<int>).ToArray();
                return Add(node.Object, node, x => x.ArrayElement(indexes, node.Type));
            }

            if (method.Name.StartsWith(StreamBindingMethodName) &&
                method.DeclaringType == typeof(StreamBindingExtensions))
            {
                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length != 1)
                {
                    throw new ExpressionParseException(0, $"Invalid method call in binding expression: '{node.Method.DeclaringType}.{node.Method.Name}'.");
                }

                var genericArg = genericArguments[0];
                var instance = node.Method.IsStatic ? node.Arguments[0] : node.Object;

                if (typeof(Task<>).MakeGenericType(genericArg).IsAssignableFrom(instance?.Type))
                {
                    var builderMethod = typeof(CompiledBindingPathBuilder)
                        .GetMethod(nameof(CompiledBindingPathBuilder.StreamTask))
                        .MakeGenericMethod(genericArg);
                    return Add(instance, node, x => builderMethod.Invoke(x, null));
                }

                if (typeof(IObservable<>).MakeGenericType(genericArg).IsAssignableFrom(instance?.Type))
                {
                    var builderMethod = typeof(CompiledBindingPathBuilder)
                        .GetMethod(nameof(CompiledBindingPathBuilder.StreamObservable))
                        .MakeGenericMethod(genericArg);
                    return Add(instance, node, x => builderMethod.Invoke(x, null));
                }
            }

            throw new ExpressionParseException(0, $"Invalid method call in binding expression: '{node.Method.DeclaringType}.{node.Method.Name}'.");
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _rootExpression.Parameters[0] && _head is null)
            {
                _head = node;
            }

            return base.VisitParameter(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not && node.Type == typeof(bool))
            {
                return Add(node.Operand, node, x => x.Not());
            }

            if (node.NodeType == ExpressionType.Convert)
            {
                if (node.Operand.Type.IsAssignableFrom(node.Type))
                {
                    return _head = base.VisitUnary(node);
                }
            }

            if (node.NodeType == ExpressionType.TypeAs)
            {
                return _head = base.VisitUnary(node);
            }

            throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");
        }

        protected override Expression VisitBlock(BlockExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
            => throw new ExpressionParseException(0, "Catch blocks are not allowed in binding expressions.");

        protected override Expression VisitConditional(ConditionalExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override Expression VisitDynamic(DynamicExpression node)
            => throw new ExpressionParseException(0, "Dynamic expressions are not allowed in binding expressions.");

        protected override ElementInit VisitElementInit(ElementInit node)
            => throw new ExpressionParseException(0, "Element init expressions are not valid in a binding expression.");

        protected override Expression VisitGoto(GotoExpression node)
            => throw new ExpressionParseException(0, "Goto expressions not supported in binding expressions.");

        protected override Expression VisitInvocation(InvocationExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override Expression VisitLabel(LabelExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override Expression VisitListInit(ListInitExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override Expression VisitLoop(LoopExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
            => throw new ExpressionParseException(0, "Member assignments not supported in binding expressions.");

        protected override Expression VisitSwitch(SwitchExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override Expression VisitTry(TryExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
            => throw new ExpressionParseException(0, $"Invalid expression type in binding expression: {node.NodeType}.");

        private Expression Add(Expression instance, Expression expression, Action<CompiledBindingPathBuilder> build)
        {
            var visited = Visit(instance);
            if (visited != _head)
            {
                throw new ExpressionParseException(
                    0,
                    $"Unable to parse '{expression}': expected an instance of '{_head}' but got '{visited}'.");
            }

            build(_builder);
            return _head = expression;
        }

        private static Func<object, object> CreateGetter(PropertyInfo info)
        {
            if (info.GetMethod == null)
            {
                return null;
            }

            var target = Expression.Parameter(typeof(object), "target");
            return Expression.Lambda<Func<object, object>>(
                    Expression.Convert(Expression.Call(Expression.Convert(target, info.DeclaringType), info.GetMethod), typeof(object)),
                    target)
                .Compile();
        }

        private static Action<object, object> CreateSetter(PropertyInfo info)
        {
            if (info.SetMethod == null)
            {
                return null;
            }

            var target = Expression.Parameter(typeof(object), "target");
            var value = Expression.Parameter(typeof(object), "value");
            return Expression.Lambda<Action<object, object>>(
                    Expression.Call(
                        Expression.Convert(target, info.DeclaringType),
                        info.SetMethod,
                        Expression.Convert(value, info.SetMethod.GetParameters()[0].ParameterType)),
                    target,
                    value)
                .Compile();
        }

        private static Func<object, object> CreateIndexerGetter(MethodInfo method, object[] indexes)
        {
            if (method == null)
            {
                return null;
            }

            var target = Expression.Parameter(typeof(object), "target");
            var arguments = CreateIndexArguments(method, indexes, includeValue: false, valueParameter: null);
            var call = Expression.Call(Expression.Convert(target, method.DeclaringType), method, arguments);
            return Expression.Lambda<Func<object, object>>(Expression.Convert(call, typeof(object)), target).Compile();
        }

        private static Action<object, object> CreateIndexerSetter(MethodInfo method, object[] indexes)
        {
            if (method == null)
            {
                return null;
            }

            var target = Expression.Parameter(typeof(object), "target");
            var value = Expression.Parameter(typeof(object), "value");
            var arguments = CreateIndexArguments(method, indexes, includeValue: true, valueParameter: value);
            var call = Expression.Call(Expression.Convert(target, method.DeclaringType), method, arguments);
            return Expression.Lambda<Action<object, object>>(call, target, value).Compile();
        }

        private static Expression[] CreateIndexArguments(
            MethodInfo method,
            object[] indexes,
            bool includeValue,
            ParameterExpression valueParameter)
        {
            var parameters = method.GetParameters();
            var indexCount = includeValue ? parameters.Length - 1 : parameters.Length;
            var arguments = new Expression[parameters.Length];

            for (var i = 0; i < indexCount; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var indexValue = indexes[i];
                var constant = Expression.Constant(indexValue);
                arguments[i] = constant.Type == parameterType
                    ? (Expression)constant
                    : Expression.Convert(constant, parameterType);
            }

            if (includeValue)
            {
                var valueType = parameters[parameters.Length - 1].ParameterType;
                arguments[parameters.Length - 1] = valueType == typeof(object)
                    ? valueParameter
                    : Expression.Convert(valueParameter, valueType);
            }

            return arguments;
        }

        private static T GetValue<T>(Expression expr)
        {
            if (expr is ConstantExpression constant)
            {
                return (T)constant.Value;
            }

            return Expression.Lambda<Func<T>>(expr).Compile(preferInterpretation: true)();
        }

        private static PropertyInfo TryGetPropertyFromMethod(MethodInfo method)
        {
            var type = method.DeclaringType;
            return type?.GetRuntimeProperties().FirstOrDefault(prop => prop.GetMethod == method);
        }
    }
}
