// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    internal enum FormulaInstructionKind
    {
        Literal,
        Name,
        Reference,
        StructuredReference,
        Unary,
        Binary,
        FunctionCall,
        LazyFunctionCall,
        ArrayLiteral,
        Invocation
    }

    internal readonly struct FormulaInstruction
    {
        public FormulaInstruction(
            FormulaInstructionKind kind,
            FormulaValue literal = default,
            FormulaReference reference = default,
            FormulaStructuredReference structuredReference = default,
            FormulaUnaryOperator unaryOperator = default,
            FormulaBinaryOperator binaryOperator = default,
            string? name = null,
            int argCount = 0,
            int rowCount = 0,
            int columnCount = 0,
            FormulaExpression[]? lazyArguments = null,
            FormulaExpression? expression = null)
        {
            Kind = kind;
            Literal = literal;
            Reference = reference;
            StructuredReference = structuredReference;
            UnaryOperator = unaryOperator;
            BinaryOperator = binaryOperator;
            Name = name;
            ArgCount = argCount;
            RowCount = rowCount;
            ColumnCount = columnCount;
            LazyArguments = lazyArguments;
            Expression = expression;
        }

        public FormulaInstructionKind Kind { get; }

        public FormulaValue Literal { get; }

        public FormulaReference Reference { get; }

        public FormulaStructuredReference StructuredReference { get; }

        public FormulaUnaryOperator UnaryOperator { get; }

        public FormulaBinaryOperator BinaryOperator { get; }

        public string? Name { get; }

        public int ArgCount { get; }

        public int RowCount { get; }

        public int ColumnCount { get; }

        public FormulaExpression[]? LazyArguments { get; }

        public FormulaExpression? Expression { get; }
    }

    internal sealed class FormulaCompiledExpression
    {
        public FormulaCompiledExpression(
            IFormulaFunctionRegistry functionRegistry,
            FormulaInstruction[] instructions,
            int maxStackDepth)
        {
            FunctionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
            Instructions = instructions ?? throw new ArgumentNullException(nameof(instructions));
            MaxStackDepth = maxStackDepth;
            FunctionRegistryVersion = (functionRegistry as IFormulaFunctionRegistryVersion)?.Version;
        }

        public IFormulaFunctionRegistry FunctionRegistry { get; }

        public FormulaInstruction[] Instructions { get; }

        public int MaxStackDepth { get; }

        private long? FunctionRegistryVersion { get; }

        public bool IsCompatibleWith(IFormulaFunctionRegistry registry)
        {
            return ReferenceEquals(FunctionRegistry, registry) &&
                FunctionRegistryVersion == (registry as IFormulaFunctionRegistryVersion)?.Version;
        }
    }

    internal sealed class FormulaExpressionCompiler
    {
        private readonly IFormulaFunctionRegistry _functionRegistry;
        private readonly List<FormulaInstruction> _instructions = new();
        private int _stackDepth;
        private int _maxStackDepth;

        public FormulaExpressionCompiler(IFormulaFunctionRegistry functionRegistry)
        {
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        public FormulaCompiledExpression Compile(FormulaExpression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            _instructions.Clear();
            _stackDepth = 0;
            _maxStackDepth = 0;

            CompileExpression(expression);

            return new FormulaCompiledExpression(_functionRegistry, _instructions.ToArray(), _maxStackDepth);
        }

        private void CompileExpression(FormulaExpression expression)
        {
            // Heap-backed frames avoid overflowing the CLR stack on long generated formulas.
            // Children are pushed in reverse so evaluation remains strictly left-to-right.
            var pending = new Stack<(FormulaExpression Expression, bool Expanded)>();
            pending.Push((expression, false));
            while (pending.Count > 0)
            {
                var frame = pending.Pop();
                var current = frame.Expression;
                if (frame.Expanded)
                {
                    switch (current.Kind)
                    {
                        case FormulaExpressionKind.Unary:
                            Emit(new FormulaInstruction(FormulaInstructionKind.Unary,
                                unaryOperator: ((FormulaUnaryExpression)current).Operator), pop: 1, push: 1);
                            break;
                        case FormulaExpressionKind.Binary:
                            Emit(new FormulaInstruction(FormulaInstructionKind.Binary,
                                binaryOperator: ((FormulaBinaryExpression)current).Operator), pop: 2, push: 1);
                            break;
                        case FormulaExpressionKind.FunctionCall:
                            var call = (FormulaFunctionCallExpression)current;
                            Emit(new FormulaInstruction(FormulaInstructionKind.FunctionCall,
                                name: call.Name, argCount: call.Arguments.Count, lazyArguments: new List<FormulaExpression>(call.Arguments).ToArray()), pop: call.Arguments.Count, push: 1);
                            break;
                        case FormulaExpressionKind.ArrayLiteral:
                            var array = (FormulaArrayExpression)current;
                            Emit(new FormulaInstruction(FormulaInstructionKind.ArrayLiteral,
                                rowCount: array.RowCount, columnCount: array.ColumnCount),
                                pop: checked(array.RowCount * array.ColumnCount), push: 1);
                            break;
                    }
                    continue;
                }

                switch (current.Kind)
                {
                    case FormulaExpressionKind.Invocation:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Invocation, expression: current), push: 1);
                        break;
                    case FormulaExpressionKind.Literal:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Literal,
                            literal: ((FormulaLiteralExpression)current).Value), push: 1);
                        break;
                    case FormulaExpressionKind.Name:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Name,
                            name: ((FormulaNameExpression)current).Name), push: 1);
                        break;
                    case FormulaExpressionKind.Reference:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Reference,
                            reference: ((FormulaReferenceExpression)current).Reference), push: 1);
                        break;
                    case FormulaExpressionKind.StructuredReference:
                        Emit(new FormulaInstruction(FormulaInstructionKind.StructuredReference,
                            structuredReference: ((FormulaStructuredReferenceExpression)current).Reference), push: 1);
                        break;
                    case FormulaExpressionKind.Unary:
                        pending.Push((current, true));
                        pending.Push((((FormulaUnaryExpression)current).Operand, false));
                        break;
                    case FormulaExpressionKind.Binary:
                        var binary = (FormulaBinaryExpression)current;
                        pending.Push((current, true));
                        pending.Push((binary.Right, false));
                        pending.Push((binary.Left, false));
                        break;
                    case FormulaExpressionKind.FunctionCall:
                        var call = (FormulaFunctionCallExpression)current;
                        if (!_functionRegistry.TryGetFunction(call.Name, out var function) || function is ILazyFormulaFunction)
                        {
                            var arguments = new FormulaExpression[call.Arguments.Count];
                            for (var i = 0; i < arguments.Length; i++)
                            {
                                arguments[i] = call.Arguments[i];
                            }
                            Emit(new FormulaInstruction(FormulaInstructionKind.LazyFunctionCall,
                                name: call.Name, lazyArguments: arguments), push: 1);
                        }
                        else
                        {
                            pending.Push((current, true));
                            for (var i = call.Arguments.Count - 1; i >= 0; i--)
                            {
                                pending.Push((call.Arguments[i], false));
                            }
                        }
                        break;
                    case FormulaExpressionKind.ArrayLiteral:
                        var array = (FormulaArrayExpression)current;
                        pending.Push((current, true));
                        for (var row = array.RowCount - 1; row >= 0; row--)
                        {
                            for (var column = array.ColumnCount - 1; column >= 0; column--)
                            {
                                pending.Push((array[row, column], false));
                            }
                        }
                        break;
                    default:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Literal,
                            literal: FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc))), push: 1);
                        break;
                }
            }
        }

        private void Emit(FormulaInstruction instruction, int pop = 0, int push = 0)
        {
            _instructions.Add(instruction);
            _stackDepth -= pop;
            if (_stackDepth < 0)
            {
                _stackDepth = 0;
            }
            _stackDepth += push;
            if (_stackDepth > _maxStackDepth)
            {
                _maxStackDepth = _stackDepth;
            }
        }
    }
}
