// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ProDataGrid.FormulaEngine
{
    public interface IFormulaValueResolver
    {
        bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value);

        bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value);
    }

    public interface IFormulaRangeValueResolver
    {
        IEnumerable<FormulaValue> EnumerateReferenceValues(
            FormulaEvaluationContext context,
            FormulaReference reference);
    }

    public interface IFormulaStructuredReferenceResolver
    {
        bool TryResolveStructuredReference(
            FormulaEvaluationContext context,
            FormulaStructuredReference reference,
            out FormulaValue value);
    }

    public static class FormulaReferenceResolver
    {
        public static bool TryResolveCell(
            FormulaReferenceAddress address,
            FormulaCellAddress origin,
            out FormulaCellAddress resolved)
        {
            resolved = default;
            string? sheetName = null;

            if (address.Sheet.HasValue)
            {
                var sheet = address.Sheet.Value;
                if (sheet.IsExternal || sheet.IsRange)
                {
                    return false;
                }

                sheetName = sheet.StartSheetName;
            }
            else
            {
                sheetName = origin.SheetName;
            }

            var row = address.Mode == FormulaReferenceMode.A1
                ? address.Row
                : address.RowIsAbsolute ? address.Row : origin.Row + address.Row;

            var column = address.Mode == FormulaReferenceMode.A1
                ? address.Column
                : address.ColumnIsAbsolute ? address.Column : origin.Column + address.Column;

            if (row <= 0 || column <= 0)
            {
                return false;
            }

            resolved = new FormulaCellAddress(sheetName, row, column);
            return true;
        }

        public static bool TryResolveRange(
            FormulaReference reference,
            FormulaCellAddress origin,
            out FormulaRangeAddress range)
        {
            range = default;
            if (!TryResolveCell(reference.Start, origin, out var start))
            {
                return false;
            }

            if (!TryResolveCell(reference.End, origin, out var end))
            {
                return false;
            }

            range = new FormulaRangeAddress(start, end);
            return true;
        }
    }

    public static class FormulaCoercion
    {
        private static readonly FormulaCalculationSettings s_invariantSettings = new FormulaCalculationSettings
        {
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            UseExcelNumberParsing = false,
            ApplyNumberPrecision = false
        };

        public static bool TryCoerceToNumber(FormulaValue value, out double number, out FormulaError error)
        {
            return TryCoerceToNumber(value, s_invariantSettings, out number, out error);
        }

        public static bool TryCoerceToNumber(
            FormulaValue value,
            FormulaCalculationSettings settings,
            out double number,
            out FormulaError error)
        {
            error = default;
            number = 0;

            switch (value.Kind)
            {
                case FormulaValueKind.Number:
                    number = value.AsNumber();
                    if (settings.ApplyNumberPrecision)
                    {
                        number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
                    }
                    return true;
                case FormulaValueKind.Boolean:
                    number = value.AsBoolean() ? 1 : 0;
                    return true;
                case FormulaValueKind.Blank:
                    number = 0;
                    return true;
                case FormulaValueKind.Text:
                    if (settings.UseExcelNumberParsing)
                    {
                        if (FormulaNumberUtilities.TryParse(value.AsText(), settings, out number))
                        {
                            if (settings.ApplyNumberPrecision)
                            {
                                number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
                            }
                            return true;
                        }
                    }
                    else if (double.TryParse(value.AsText(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number))
                    {
                        if (settings.ApplyNumberPrecision)
                        {
                            number = FormulaNumberUtilities.ApplyPrecision(number, settings.NumberPrecisionDigits);
                        }
                        return true;
                    }
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
                case FormulaValueKind.Error:
                    error = value.AsError();
                    return false;
                default:
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
            }
        }

        public static bool TryCoerceToBoolean(FormulaValue value, out bool result, out FormulaError error)
        {
            error = default;
            result = false;

            switch (value.Kind)
            {
                case FormulaValueKind.Boolean:
                    result = value.AsBoolean();
                    return true;
                case FormulaValueKind.Number:
                    result = Math.Abs(value.AsNumber()) > double.Epsilon;
                    return true;
                case FormulaValueKind.Blank:
                    result = false;
                    return true;
                case FormulaValueKind.Text:
                    if (string.Equals(value.AsText(), "TRUE", StringComparison.OrdinalIgnoreCase))
                    {
                        result = true;
                        return true;
                    }
                    if (string.Equals(value.AsText(), "FALSE", StringComparison.OrdinalIgnoreCase))
                    {
                        result = false;
                        return true;
                    }
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
                case FormulaValueKind.Error:
                    error = value.AsError();
                    return false;
                default:
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
            }
        }

        public static bool TryCoerceToText(FormulaValue value, out string text, out FormulaError error)
        {
            error = default;
            text = string.Empty;

            switch (value.Kind)
            {
                case FormulaValueKind.Text:
                    text = value.AsText();
                    return true;
                case FormulaValueKind.Number:
                    text = value.AsNumber().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                case FormulaValueKind.Boolean:
                    text = value.AsBoolean() ? "TRUE" : "FALSE";
                    return true;
                case FormulaValueKind.Blank:
                    text = string.Empty;
                    return true;
                case FormulaValueKind.Error:
                    error = value.AsError();
                    return false;
                default:
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
            }
        }

        public static FormulaValue ApplyImplicitIntersection(FormulaValue value, FormulaCellAddress address)
        {
            if (value.Kind != FormulaValueKind.Array)
            {
                return value;
            }

            var array = value.AsArray();
            if (array.RowCount == 0 || array.ColumnCount == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (array.RowCount == 1 && array.ColumnCount == 1)
            {
                return array[0, 0];
            }

            if (array.Origin == null)
            {
                return array[0, 0];
            }

            var origin = array.Origin.Value;
            var rowOffset = address.Row - origin.Row;
            var columnOffset = address.Column - origin.Column;

            if (array.RowCount == 1)
            {
                if (columnOffset < 0 || columnOffset >= array.ColumnCount)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
                if (!array.IsPresent(0, columnOffset))
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
                return array[0, columnOffset];
            }

            if (array.ColumnCount == 1)
            {
                if (rowOffset < 0 || rowOffset >= array.RowCount)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
                if (!array.IsPresent(rowOffset, 0))
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
                return array[rowOffset, 0];
            }

            if (rowOffset < 0 || rowOffset >= array.RowCount || columnOffset < 0 || columnOffset >= array.ColumnCount)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!array.IsPresent(rowOffset, columnOffset))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            return array[rowOffset, columnOffset];
        }
    }

    public sealed partial class FormulaEvaluator
    {
        private readonly ConditionalWeakTable<FormulaExpression, FormulaCompiledExpression> _compiledCache = new();
        private readonly object _compilationGate = new();

        public FormulaValue Evaluate(
            FormulaExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            var observer = context.Workbook.Settings.CalculationObserver;
            if (context.Workbook.Settings.EnableCompiledExpressions)
            {
                // The cached plan already passed the reference-operator eligibility check.
                // Do not walk the AST (or allocate a traversal stack) on every cache hit.
                if (_compiledCache.TryGetValue(expression, out var cached) && cached.IsCompatibleWith(context.FunctionRegistry))
                {
                    observer?.OnExpressionCompiled(expression, cached.Instructions.Length, TimeSpan.Zero, fromCache: true);
                    return EvaluateCompiled(cached, context, resolver);
                }

                if (!ContainsReferenceOperators(expression))
                {
                    var compiled = GetCompiledExpression(expression, context.FunctionRegistry, observer);
                    return EvaluateCompiled(compiled, context, resolver);
                }
            }

            return EvaluateCore(expression, context, resolver);
        }

        private static bool ContainsReferenceOperators(FormulaExpression expression)
        {
            var stack = new Stack<FormulaExpression>();
            stack.Push(expression);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                switch (current.Kind)
                {
                    case FormulaExpressionKind.Binary:
                        var binary = (FormulaBinaryExpression)current;
                        if (binary.Operator is FormulaBinaryOperator.Union or FormulaBinaryOperator.Intersection)
                        {
                            return true;
                        }
                        stack.Push(binary.Left);
                        stack.Push(binary.Right);
                        break;
                    case FormulaExpressionKind.Unary:
                        stack.Push(((FormulaUnaryExpression)current).Operand);
                        break;
                    case FormulaExpressionKind.FunctionCall:
                        foreach (var arg in ((FormulaFunctionCallExpression)current).Arguments)
                        {
                            stack.Push(arg);
                        }
                        break;
                    case FormulaExpressionKind.ArrayLiteral:
                        var array = (FormulaArrayExpression)current;
                        for (var row = 0; row < array.RowCount; row++)
                        {
                            for (var column = 0; column < array.ColumnCount; column++)
                            {
                                stack.Push(array[row, column]);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            return false;
        }

        private FormulaCompiledExpression GetCompiledExpression(
            FormulaExpression expression,
            IFormulaFunctionRegistry functionRegistry,
            IFormulaCalculationObserver? observer)
        {
            FormulaCompiledExpression compiled;
            var fromCache = false;
            var duration = TimeSpan.Zero;
            // Only cold compilation/publication is serialized. The common compatible
            // cache-hit path in Evaluate remains lock-free and allocation-free.
            lock (_compilationGate)
            {
                if (_compiledCache.TryGetValue(expression, out var existing) && existing.IsCompatibleWith(functionRegistry))
                {
                    compiled = existing;
                    fromCache = true;
                }
                else
                {
                    var compiler = new FormulaExpressionCompiler(functionRegistry);
                    var watch = observer != null ? System.Diagnostics.Stopwatch.StartNew() : null;
                    compiled = compiler.Compile(expression);
                    if (watch != null)
                    {
                        watch.Stop();
                        duration = watch.Elapsed;
                    }
                    _compiledCache.Remove(expression);
                    _compiledCache.Add(expression, compiled);
                }
            }

            // Publish before notifying host code; observers may reenter evaluation.
            // Host callbacks must not run while holding the compilation gate.
            observer?.OnExpressionCompiled(expression, compiled.Instructions.Length, duration, fromCache);
            return compiled;
        }

        private FormulaValue EvaluateCompiled(
            FormulaCompiledExpression compiled,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            var instructions = compiled.Instructions;
            var stack = compiled.MaxStackDepth > 0
                ? ArrayPool<FormulaValue>.Shared.Rent(compiled.MaxStackDepth)
                : Array.Empty<FormulaValue>();
            try
            {
                var sp = 0;

                foreach (var instruction in instructions)
                {
                    switch (instruction.Kind)
                    {
                        case FormulaInstructionKind.Literal:
                            var literal = instruction.Literal;
                            if (literal.Kind == FormulaValueKind.Number)
                            {
                                literal = CreateNumber(context, literal.AsNumber());
                            }
                            stack[sp++] = literal;
                            break;
                        case FormulaInstructionKind.Name:
                            stack[sp++] = EvaluateName(instruction.Name ?? string.Empty, context, resolver);
                            break;
                        case FormulaInstructionKind.Reference:
                            stack[sp++] = EvaluateReference(instruction.Reference, context, resolver);
                            break;
                        case FormulaInstructionKind.StructuredReference:
                            stack[sp++] = EvaluateStructuredReference(instruction.StructuredReference, context, resolver);
                            break;
                        case FormulaInstructionKind.Unary:
                            if (sp == 0)
                            {
                                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                            }
                            var operand = stack[--sp];
                            stack[sp++] = ApplyUnaryOperator(instruction.UnaryOperator, operand, context);
                            break;
                        case FormulaInstructionKind.Binary:
                            if (sp < 2)
                            {
                                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                            }
                            var right = stack[--sp];
                            var left = stack[--sp];
                            stack[sp++] = EvaluateBinaryOperator(instruction.BinaryOperator, left, right, context, resolver);
                            break;
                        case FormulaInstructionKind.FunctionCall:
                            if (sp < instruction.ArgCount)
                            {
                                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                            }
                            var args = instruction.ArgCount == 0 ? Array.Empty<FormulaValue>() : new FormulaValue[instruction.ArgCount];
                            for (var i = instruction.ArgCount - 1; i >= 0; i--)
                            {
                                args[i] = stack[--sp];
                            }
                            stack[sp++] = InvokeFunction(instruction.Name ?? string.Empty, args, context);
                            break;
                        case FormulaInstructionKind.LazyFunctionCall:
                            stack[sp++] = InvokeLazyFunction(
                                instruction.Name ?? string.Empty,
                                instruction.LazyArguments ?? Array.Empty<FormulaExpression>(),
                                context,
                                resolver);
                            break;
                        case FormulaInstructionKind.ArrayLiteral:
                            if (sp < instruction.RowCount * instruction.ColumnCount)
                            {
                                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                            }
                            var array = new FormulaArray(instruction.RowCount, instruction.ColumnCount);
                            for (var row = instruction.RowCount - 1; row >= 0; row--)
                            {
                                for (var column = instruction.ColumnCount - 1; column >= 0; column--)
                                {
                                    var value = stack[--sp];
                                    if (value.Kind == FormulaValueKind.Array)
                                    {
                                        value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                                    }
                                    array[row, column] = value;
                                }
                            }
                            stack[sp++] = FormulaValue.FromArray(array);
                            break;
                        default:
                            stack[sp++] = FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                            break;
                    }
                }

                if (sp != 1)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
                }

                return stack[0];
            }
            finally
            {
                if (stack.Length > 0)
                {
                    // FormulaValue can retain strings, arrays and workbook references.
                    // Each invocation rents independently, including reentrant UDF calls.
                    ArrayPool<FormulaValue>.Shared.Return(stack, clearArray: true);
                }
            }
        }

        private FormulaValue EvaluateCore(
            FormulaExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Literal:
                    var literal = ((FormulaLiteralExpression)expression).Value;
                    if (literal.Kind == FormulaValueKind.Number)
                    {
                        return CreateNumber(context, literal.AsNumber());
                    }
                    return literal;
                case FormulaExpressionKind.Name:
                    return EvaluateName((FormulaNameExpression)expression, context, resolver);
                case FormulaExpressionKind.Reference:
                    return EvaluateReference((FormulaReferenceExpression)expression, context, resolver);
                case FormulaExpressionKind.Unary:
                    return EvaluateUnary((FormulaUnaryExpression)expression, context, resolver);
                case FormulaExpressionKind.Binary:
                    return EvaluateBinary((FormulaBinaryExpression)expression, context, resolver);
                case FormulaExpressionKind.FunctionCall:
                    return EvaluateFunctionCall((FormulaFunctionCallExpression)expression, context, resolver);
                case FormulaExpressionKind.ArrayLiteral:
                    return EvaluateArrayLiteral((FormulaArrayExpression)expression, context, resolver);
                case FormulaExpressionKind.StructuredReference:
                    return EvaluateStructuredReference((FormulaStructuredReferenceExpression)expression, context, resolver);
                default:
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
            }
        }

        private FormulaValue EvaluateName(
            FormulaNameExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            return EvaluateName(expression.Name, context, resolver);
        }

        private FormulaValue EvaluateName(
            string name,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (resolver.TryResolveName(context, name, out var value))
            {
                return value;
            }

            return FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
        }

        private FormulaValue EvaluateReference(
            FormulaReferenceExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            return EvaluateReference(expression.Reference, context, resolver);
        }

        private FormulaValue EvaluateReference(
            FormulaReference reference,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (resolver.TryResolveReference(context, reference, out var value))
            {
                return value;
            }

            return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
        }

        private FormulaValue EvaluateStructuredReference(
            FormulaStructuredReferenceExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            return EvaluateStructuredReference(expression.Reference, context, resolver);
        }

        private FormulaValue EvaluateStructuredReference(
            FormulaStructuredReference reference,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (resolver is IFormulaStructuredReferenceResolver structuredResolver &&
                structuredResolver.TryResolveStructuredReference(context, reference, out var value))
            {
                return value;
            }

            return FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
        }

        private FormulaValue ApplyUnaryOperator(
            FormulaUnaryOperator op,
            FormulaValue operand,
            FormulaEvaluationContext context)
        {
            if (operand.Kind == FormulaValueKind.Error)
            {
                return operand;
            }

            if (operand.Kind == FormulaValueKind.Array)
            {
                return EvaluateUnaryArray(op, operand.AsArray(), context.Workbook.Settings);
            }

            operand = FormulaCoercion.ApplyImplicitIntersection(operand, context.Address);

            if (!FormulaCoercion.TryCoerceToNumber(operand, context.Workbook.Settings, out var number, out var error))
            {
                return FormulaValue.FromError(error);
            }

            return op switch
            {
                FormulaUnaryOperator.Negate => CreateNumber(context, -number),
                FormulaUnaryOperator.Percent => CreateNumber(context, number / 100d),
                _ => CreateNumber(context, number)
            };
        }

        private FormulaValue EvaluateBinaryOperator(
            FormulaBinaryOperator op,
            FormulaValue left,
            FormulaValue right,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (op == FormulaBinaryOperator.Union || op == FormulaBinaryOperator.Intersection)
            {
                return EvaluateReferenceOperator(op, left, right, context, resolver);
            }

            if (left.Kind == FormulaValueKind.Array || right.Kind == FormulaValueKind.Array)
            {
                return EvaluateBinaryArray(op, left, right, context);
            }

            return EvaluateBinaryScalar(op, left, right, context);
        }

        private FormulaValue EvaluateReferenceOperator(
            FormulaBinaryOperator op,
            FormulaValue left,
            FormulaValue right,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (!TryGetReferenceArray(left, context, resolver, out var leftArray, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!TryGetReferenceArray(right, context, resolver, out var rightArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (leftArray.Origin == null || rightArray.Origin == null)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var leftSheet = leftArray.Origin.Value.SheetName;
            var rightSheet = rightArray.Origin.Value.SheetName;
            if (!string.Equals(leftSheet, rightSheet, StringComparison.OrdinalIgnoreCase))
            {
                return FormulaValue.FromError(op == FormulaBinaryOperator.Intersection
                    ? new FormulaError(FormulaErrorType.Null)
                    : new FormulaError(FormulaErrorType.Value));
            }

            return op == FormulaBinaryOperator.Union
                ? EvaluateUnion(leftArray, rightArray)
                : EvaluateIntersection(leftArray, rightArray);
        }

        private bool TryGetReferenceArray(
            FormulaValue value,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver,
            out FormulaArray array,
            out FormulaError error)
        {
            error = default;
            array = null!;

            if (value.Kind == FormulaValueKind.Error)
            {
                error = value.AsError();
                return false;
            }

            if (value.Kind == FormulaValueKind.Array)
            {
                array = value.AsArray();
                if (array.Origin == null)
                {
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
                }
                return true;
            }

            if (value.Kind == FormulaValueKind.Reference)
            {
                return TryBuildReferenceArray(value.AsReference(), context, resolver, out array, out error);
            }

            error = new FormulaError(FormulaErrorType.Value);
            return false;
        }

        private FormulaValue EvaluateUnary(
            FormulaUnaryExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            var operand = EvaluateCore(expression.Operand, context, resolver);
            if (operand.Kind == FormulaValueKind.Error)
            {
                return operand;
            }

            if (operand.Kind == FormulaValueKind.Array)
            {
                return EvaluateUnaryArray(expression.Operator, operand.AsArray(), context.Workbook.Settings);
            }

            operand = FormulaCoercion.ApplyImplicitIntersection(operand, context.Address);

            if (!FormulaCoercion.TryCoerceToNumber(operand, context.Workbook.Settings, out var number, out var error))
            {
                return FormulaValue.FromError(error);
            }

            return expression.Operator switch
            {
                FormulaUnaryOperator.Negate => CreateNumber(context, -number),
                FormulaUnaryOperator.Percent => CreateNumber(context, number / 100d),
                _ => CreateNumber(context, number)
            };
        }

        private FormulaValue EvaluateBinary(
            FormulaBinaryExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (expression.Operator == FormulaBinaryOperator.Union ||
                expression.Operator == FormulaBinaryOperator.Intersection)
            {
                return EvaluateReferenceOperator(expression, context, resolver);
            }

            var left = EvaluateCore(expression.Left, context, resolver);
            if (left.Kind == FormulaValueKind.Error)
            {
                return left;
            }

            var right = EvaluateCore(expression.Right, context, resolver);
            if (right.Kind == FormulaValueKind.Error)
            {
                return right;
            }

            if (left.Kind == FormulaValueKind.Array || right.Kind == FormulaValueKind.Array)
            {
                return EvaluateBinaryArray(expression.Operator, left, right, context);
            }

            return EvaluateBinaryScalar(expression.Operator, left, right, context);
        }

        private FormulaValue EvaluateReferenceOperator(
            FormulaBinaryExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (!TryGetReferenceArray(expression.Left, context, resolver, out var leftArray, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!TryGetReferenceArray(expression.Right, context, resolver, out var rightArray, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (leftArray.Origin == null || rightArray.Origin == null)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var leftSheet = leftArray.Origin.Value.SheetName;
            var rightSheet = rightArray.Origin.Value.SheetName;
            if (!string.Equals(leftSheet, rightSheet, StringComparison.OrdinalIgnoreCase))
            {
                return FormulaValue.FromError(expression.Operator == FormulaBinaryOperator.Intersection
                    ? new FormulaError(FormulaErrorType.Null)
                    : new FormulaError(FormulaErrorType.Value));
            }

            return expression.Operator == FormulaBinaryOperator.Union
                ? EvaluateUnion(leftArray, rightArray)
                : EvaluateIntersection(leftArray, rightArray);
        }

        private bool TryGetReferenceArray(
            FormulaExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver,
            out FormulaArray array,
            out FormulaError error)
        {
            error = default;
            array = null!;

            if (expression is FormulaReferenceExpression referenceExpression)
            {
                if (!TryBuildReferenceArray(referenceExpression.Reference, context, resolver, out array, out error))
                {
                    return false;
                }

                return true;
            }

            var value = EvaluateCore(expression, context, resolver);
            if (value.Kind == FormulaValueKind.Error)
            {
                error = value.AsError();
                return false;
            }

            if (value.Kind != FormulaValueKind.Array)
            {
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }

            array = value.AsArray();
            if (array.Origin == null)
            {
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }

            return true;
        }

        private bool TryBuildReferenceArray(
            FormulaReference reference,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver,
            out FormulaArray array,
            out FormulaError error)
        {
            error = default;
            array = null!;

            var origin = new FormulaCellAddress(context.Worksheet.Name, context.Address.Row, context.Address.Column);
            FormulaRangeAddress range;
            if (reference.Kind == FormulaReferenceKind.Cell)
            {
                if (!FormulaReferenceResolver.TryResolveCell(reference.Start, origin, out var address))
                {
                    error = new FormulaError(FormulaErrorType.Ref);
                    return false;
                }

                range = new FormulaRangeAddress(address, address);
            }
            else
            {
                if (!FormulaReferenceResolver.TryResolveRange(reference, origin, out range))
                {
                    error = new FormulaError(FormulaErrorType.Ref);
                    return false;
                }
            }

            var rows = range.End.Row - range.Start.Row + 1;
            var columns = range.End.Column - range.Start.Column + 1;
            array = new FormulaArray(rows, columns, range.Start);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var address = new FormulaCellAddress(range.Start.SheetName, range.Start.Row + row, range.Start.Column + column);
                    if (!TryResolveCellValue(context, resolver, address, out var cellValue))
                    {
                        error = new FormulaError(FormulaErrorType.Ref);
                        return false;
                    }

                    array[row, column] = cellValue;
                }
            }

            return true;
        }

        private static bool TryResolveCellValue(
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver,
            FormulaCellAddress address,
            out FormulaValue value)
        {
            FormulaSheetReference? sheet = null;
            if (!string.IsNullOrWhiteSpace(address.SheetName))
            {
                sheet = new FormulaSheetReference(null, address.SheetName);
            }

            var referenceAddress = new FormulaReferenceAddress(
                FormulaReferenceMode.A1,
                address.Row,
                address.Column,
                true,
                true,
                sheet);
            var reference = new FormulaReference(referenceAddress);
            return resolver.TryResolveReference(context, reference, out value);
        }

        private static FormulaValue EvaluateUnion(FormulaArray left, FormulaArray right)
        {
            var leftOrigin = left.Origin!.Value;
            var rightOrigin = right.Origin!.Value;

            var leftEndRow = leftOrigin.Row + left.RowCount - 1;
            var leftEndColumn = leftOrigin.Column + left.ColumnCount - 1;
            var rightEndRow = rightOrigin.Row + right.RowCount - 1;
            var rightEndColumn = rightOrigin.Column + right.ColumnCount - 1;

            var startRow = Math.Min(leftOrigin.Row, rightOrigin.Row);
            var startColumn = Math.Min(leftOrigin.Column, rightOrigin.Column);
            var endRow = Math.Max(leftEndRow, rightEndRow);
            var endColumn = Math.Max(leftEndColumn, rightEndColumn);

            var rows = endRow - startRow + 1;
            var columns = endColumn - startColumn + 1;
            var origin = new FormulaCellAddress(leftOrigin.SheetName, startRow, startColumn);
            var result = new FormulaArray(rows, columns, origin, sparse: true);

            CopyInto(result, left, startRow, startColumn);
            CopyInto(result, right, startRow, startColumn);

            return FormulaValue.FromArray(result);
        }

        private static FormulaValue EvaluateIntersection(FormulaArray left, FormulaArray right)
        {
            var leftOrigin = left.Origin!.Value;
            var rightOrigin = right.Origin!.Value;

            var leftEndRow = leftOrigin.Row + left.RowCount - 1;
            var leftEndColumn = leftOrigin.Column + left.ColumnCount - 1;
            var rightEndRow = rightOrigin.Row + right.RowCount - 1;
            var rightEndColumn = rightOrigin.Column + right.ColumnCount - 1;

            var startRow = Math.Max(leftOrigin.Row, rightOrigin.Row);
            var startColumn = Math.Max(leftOrigin.Column, rightOrigin.Column);
            var endRow = Math.Min(leftEndRow, rightEndRow);
            var endColumn = Math.Min(leftEndColumn, rightEndColumn);

            if (startRow > endRow || startColumn > endColumn)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Null));
            }

            var rows = endRow - startRow + 1;
            var columns = endColumn - startColumn + 1;
            var origin = new FormulaCellAddress(leftOrigin.SheetName, startRow, startColumn);
            var result = new FormulaArray(rows, columns, origin, sparse: true);
            var presentCount = 0;

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var absoluteRow = startRow + row;
                    var absoluteColumn = startColumn + column;

                    var leftRow = absoluteRow - leftOrigin.Row;
                    var leftColumn = absoluteColumn - leftOrigin.Column;
                    var rightRow = absoluteRow - rightOrigin.Row;
                    var rightColumn = absoluteColumn - rightOrigin.Column;

                    if (!left.IsPresent(leftRow, leftColumn) || !right.IsPresent(rightRow, rightColumn))
                    {
                        continue;
                    }

                    result.SetValue(row, column, left[leftRow, leftColumn], true);
                    presentCount++;
                }
            }

            if (presentCount == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Null));
            }

            return FormulaValue.FromArray(result);
        }

        private static void CopyInto(
            FormulaArray destination,
            FormulaArray source,
            int destinationStartRow,
            int destinationStartColumn)
        {
            var sourceOrigin = source.Origin!.Value;
            for (var row = 0; row < source.RowCount; row++)
            {
                for (var column = 0; column < source.ColumnCount; column++)
                {
                    if (!source.IsPresent(row, column))
                    {
                        continue;
                    }

                    var targetRow = sourceOrigin.Row + row - destinationStartRow;
                    var targetColumn = sourceOrigin.Column + column - destinationStartColumn;
                    destination.SetValue(targetRow, targetColumn, source[row, column], true);
                }
            }
        }

        private static FormulaValue EvaluateNumericBinary(
            FormulaValue left,
            FormulaValue right,
            FormulaEvaluationContext context,
            Func<double, double, double> op)
        {
            left = FormulaCoercion.ApplyImplicitIntersection(left, context.Address);
            right = FormulaCoercion.ApplyImplicitIntersection(right, context.Address);

            if (!FormulaCoercion.TryCoerceToNumber(left, context.Workbook.Settings, out var leftNumber, out var leftError))
            {
                return FormulaValue.FromError(leftError);
            }

            if (!FormulaCoercion.TryCoerceToNumber(right, context.Workbook.Settings, out var rightNumber, out var rightError))
            {
                return FormulaValue.FromError(rightError);
            }

            return CreateNumber(context, op(leftNumber, rightNumber));
        }

        private static FormulaValue EvaluateDivide(FormulaValue left, FormulaValue right, FormulaEvaluationContext context)
        {
            left = FormulaCoercion.ApplyImplicitIntersection(left, context.Address);
            right = FormulaCoercion.ApplyImplicitIntersection(right, context.Address);

            if (!FormulaCoercion.TryCoerceToNumber(left, context.Workbook.Settings, out var leftNumber, out var leftError))
            {
                return FormulaValue.FromError(leftError);
            }

            if (!FormulaCoercion.TryCoerceToNumber(right, context.Workbook.Settings, out var rightNumber, out var rightError))
            {
                return FormulaValue.FromError(rightError);
            }

            if (Math.Abs(rightNumber) <= double.Epsilon)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            }

            return CreateNumber(context, leftNumber / rightNumber);
        }

        private static FormulaValue EvaluateConcat(FormulaValue left, FormulaValue right, FormulaCellAddress address)
        {
            left = FormulaCoercion.ApplyImplicitIntersection(left, address);
            right = FormulaCoercion.ApplyImplicitIntersection(right, address);

            if (!FormulaCoercion.TryCoerceToText(left, out var leftText, out var leftError))
            {
                return FormulaValue.FromError(leftError);
            }

            if (!FormulaCoercion.TryCoerceToText(right, out var rightText, out var rightError))
            {
                return FormulaValue.FromError(rightError);
            }

            return FormulaValue.FromText(leftText + rightText);
        }

        private static FormulaValue EvaluateComparison(
            FormulaValue left,
            FormulaValue right,
            FormulaEvaluationContext context,
            Func<int, bool> compare)
        {
            left = FormulaCoercion.ApplyImplicitIntersection(left, context.Address);
            right = FormulaCoercion.ApplyImplicitIntersection(right, context.Address);

            if (left.Kind == FormulaValueKind.Error)
            {
                return left;
            }

            if (right.Kind == FormulaValueKind.Error)
            {
                return right;
            }

            if (TryCompareNumbers(left, right, context.Workbook.Settings, out var comparison))
            {
                return FormulaValue.FromBoolean(compare(comparison));
            }

            if (!FormulaCoercion.TryCoerceToText(left, out var leftText, out var leftError))
            {
                return FormulaValue.FromError(leftError);
            }

            if (!FormulaCoercion.TryCoerceToText(right, out var rightText, out var rightError))
            {
                return FormulaValue.FromError(rightError);
            }

            comparison = string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            return FormulaValue.FromBoolean(compare(comparison));
        }

        private static bool TryCompareNumbers(
            FormulaValue left,
            FormulaValue right,
            FormulaCalculationSettings settings,
            out int comparison)
        {
            comparison = 0;

            if (!FormulaCoercion.TryCoerceToNumber(left, settings, out var leftNumber, out _))
            {
                return false;
            }

            if (!FormulaCoercion.TryCoerceToNumber(right, settings, out var rightNumber, out _))
            {
                return false;
            }

            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }

        private static FormulaValue EvaluateUnaryArray(
            FormulaUnaryOperator op,
            FormulaArray array,
            FormulaCalculationSettings settings)
        {
            var result = new FormulaArray(array.RowCount, array.ColumnCount, array.Origin, array.HasMask);
            for (var row = 0; row < array.RowCount; row++)
            {
                for (var column = 0; column < array.ColumnCount; column++)
                {
                    if (array.HasMask && !array.IsPresent(row, column))
                    {
                        result.SetValue(row, column, FormulaValue.Blank, false);
                        continue;
                    }

                    var cell = array[row, column];
                    if (cell.Kind == FormulaValueKind.Error)
                    {
                        result[row, column] = cell;
                        continue;
                    }

                    if (!FormulaCoercion.TryCoerceToNumber(cell, settings, out var number, out var error))
                    {
                        result[row, column] = FormulaValue.FromError(error);
                        continue;
                    }

                    var value = op switch
                    {
                        FormulaUnaryOperator.Negate => -number,
                        FormulaUnaryOperator.Percent => number / 100d,
                        _ => number
                    };

                    result[row, column] = FormulaValue.FromNumber(settings.ApplyNumberPrecision
                        ? FormulaNumberUtilities.ApplyPrecision(value, settings.NumberPrecisionDigits)
                        : value);
                }
            }

            return FormulaValue.FromArray(result);
        }

        private FormulaValue EvaluateArrayLiteral(
            FormulaArrayExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            var result = new FormulaArray(expression.RowCount, expression.ColumnCount);
            for (var row = 0; row < expression.RowCount; row++)
            {
                for (var column = 0; column < expression.ColumnCount; column++)
                {
                    var value = EvaluateCore(expression[row, column], context, resolver);
                    if (value.Kind == FormulaValueKind.Array)
                    {
                        result[row, column] = FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                        continue;
                    }
                    result[row, column] = value;
                }
            }

            return FormulaValue.FromArray(result);
        }

        private static FormulaValue EvaluateBinaryArray(
            FormulaBinaryOperator op,
            FormulaValue left,
            FormulaValue right,
            FormulaEvaluationContext context)
        {
            var leftArray = left.Kind == FormulaValueKind.Array ? left.AsArray() : null;
            var rightArray = right.Kind == FormulaValueKind.Array ? right.AsArray() : null;

            if (leftArray == null && rightArray == null)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
            }

            var rows = leftArray?.RowCount ?? rightArray!.RowCount;
            var columns = leftArray?.ColumnCount ?? rightArray!.ColumnCount;

            if (leftArray != null && rightArray != null &&
                (leftArray.RowCount != rightArray.RowCount || leftArray.ColumnCount != rightArray.ColumnCount))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var origin = leftArray?.Origin ?? rightArray?.Origin;
            var useMask = (leftArray?.HasMask ?? false) || (rightArray?.HasMask ?? false);
            var result = new FormulaArray(rows, columns, origin, useMask);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var leftPresent = leftArray == null || !leftArray.HasMask || leftArray.IsPresent(row, column);
                    var rightPresent = rightArray == null || !rightArray.HasMask || rightArray.IsPresent(row, column);

                    if (!leftPresent || !rightPresent)
                    {
                        result.SetValue(row, column, FormulaValue.Blank, false);
                        continue;
                    }

                    var leftValue = leftArray != null ? leftArray[row, column] : left;
                    var rightValue = rightArray != null ? rightArray[row, column] : right;
                    result[row, column] = EvaluateBinaryScalar(op, leftValue, rightValue, context);
                }
            }

            return FormulaValue.FromArray(result);
        }

        private static FormulaValue EvaluateBinaryScalar(
            FormulaBinaryOperator op,
            in FormulaValue left,
            in FormulaValue right,
            FormulaEvaluationContext context)
        {
            if (left.Kind == FormulaValueKind.Error)
            {
                return left;
            }

            if (right.Kind == FormulaValueKind.Error)
            {
                return right;
            }

            if (left.Kind == FormulaValueKind.Number && right.Kind == FormulaValueKind.Number &&
                op != FormulaBinaryOperator.Concat)
            {
                return EvaluateNumberBinary(op, left.AsNumber(), right.AsNumber(), context.Workbook.Settings);
            }

            return op switch
            {
                FormulaBinaryOperator.Add => EvaluateNumericBinary(left, right, context, (a, b) => a + b),
                FormulaBinaryOperator.Subtract => EvaluateNumericBinary(left, right, context, (a, b) => a - b),
                FormulaBinaryOperator.Multiply => EvaluateNumericBinary(left, right, context, (a, b) => a * b),
                FormulaBinaryOperator.Divide => EvaluateDivide(left, right, context),
                FormulaBinaryOperator.Power => EvaluateNumericBinary(left, right, context, Math.Pow),
                FormulaBinaryOperator.Concat => EvaluateConcat(left, right, context.Address),
                FormulaBinaryOperator.Equal => EvaluateComparison(left, right, context, (c) => c == 0),
                FormulaBinaryOperator.NotEqual => EvaluateComparison(left, right, context, (c) => c != 0),
                FormulaBinaryOperator.Less => EvaluateComparison(left, right, context, (c) => c < 0),
                FormulaBinaryOperator.LessOrEqual => EvaluateComparison(left, right, context, (c) => c <= 0),
                FormulaBinaryOperator.Greater => EvaluateComparison(left, right, context, (c) => c > 0),
                FormulaBinaryOperator.GreaterOrEqual => EvaluateComparison(left, right, context, (c) => c >= 0),
                _ => FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc))
            };
        }

        private static FormulaValue CreateNumber(FormulaEvaluationContext context, double value)
        {
            var settings = context.Workbook.Settings;
            if (settings.ApplyNumberPrecision)
            {
                value = FormulaNumberUtilities.ApplyPrecision(value, settings.NumberPrecisionDigits);
            }

            return FormulaValue.FromNumber(value);
        }

        private FormulaValue EvaluateFunctionCall(
            FormulaFunctionCallExpression expression,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (!context.FunctionRegistry.TryGetFunction(expression.Name, out var function))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
            }

            if (!ValidateArguments(function.Info, expression.Arguments.Count))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var functionContext = new FormulaFunctionContext(context);
            if (function is ILazyFormulaFunction lazyFunction)
            {
                return lazyFunction.InvokeLazy(functionContext, expression.Arguments, this, resolver);
            }

            var args = new List<FormulaValue>(expression.Arguments.Count);
            foreach (var argument in expression.Arguments)
            {
                var value = EvaluateCore(argument, context, resolver);
                args.Add(value);
            }

            return function.Invoke(functionContext, args);
        }

        private FormulaValue InvokeFunction(
            string name,
            IReadOnlyList<FormulaValue> args,
            FormulaEvaluationContext context)
        {
            if (!context.FunctionRegistry.TryGetFunction(name, out var function))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
            }

            if (!ValidateArguments(function.Info, args.Count))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var functionContext = new FormulaFunctionContext(context);
            return function.Invoke(functionContext, args);
        }

        private FormulaValue InvokeLazyFunction(
            string name,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluationContext context,
            IFormulaValueResolver resolver)
        {
            if (!context.FunctionRegistry.TryGetFunction(name, out var function))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
            }

            if (!ValidateArguments(function.Info, arguments.Count))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (function is ILazyFormulaFunction lazy)
            {
                var functionContext = new FormulaFunctionContext(context);
                return lazy.InvokeLazy(functionContext, arguments, this, resolver);
            }

            var values = new FormulaValue[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                values[i] = EvaluateCore(arguments[i], context, resolver);
            }

            return InvokeFunction(name, values, context);
        }

        private static bool ValidateArguments(FormulaFunctionInfo info, int count)
        {
            if (count < info.MinArgs)
            {
                return false;
            }

            if (!info.IsVariadic && count > info.MaxArgs)
            {
                return false;
            }

            return true;
        }
    }
}
