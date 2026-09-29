// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    /// <summary>Translates formula text when a cell is copied to another row and column.</summary>
    public interface IFormulaFillTranslator
    {
        /// <summary>
        /// Attempts to translate one formula from a zero-based source coordinate to a zero-based target coordinate.
        /// </summary>
        bool TryTranslate(
            string formulaText,
            int sourceRow,
            int sourceColumn,
            int targetRow,
            int targetColumn,
            out string translatedFormula);
    }

    /// <summary>Represents one parsed source formula prepared for multiple fill targets.</summary>
    public interface IPreparedFormulaFillTranslation
    {
        /// <summary>Attempts to translate the prepared formula to a zero-based target coordinate.</summary>
        bool TryTranslate(int targetRow, int targetColumn, out string translatedFormula);
    }

    /// <summary>Prepares source formulas once for allocation-conscious multi-cell fills.</summary>
    public interface IPreparedFormulaFillTranslator : IFormulaFillTranslator
    {
        /// <summary>Parses and prepares one source formula, or returns <see langword="null"/> when unsupported.</summary>
        IPreparedFormulaFillTranslation? Prepare(string formulaText, int sourceRow, int sourceColumn);
    }

    /// <summary>Translates relative references in parsed formula expressions for copy/fill operations.</summary>
    public static class FormulaReferenceTranslator
    {
        /// <summary>
        /// Shifts relative A1 references by the supplied offsets. Absolute A1 references, R1C1
        /// offsets, structured references, names, and literals are preserved.
        /// </summary>
        public static FormulaExpression TranslateForCopy(
            FormulaExpression expression,
            int rowOffset,
            int columnOffset)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (rowOffset == 0 && columnOffset == 0)
            {
                return expression;
            }

            return Rewrite(expression, rowOffset, columnOffset);
        }

        private static FormulaExpression Rewrite(FormulaExpression expression, int rowOffset, int columnOffset)
        {
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    var target = Rewrite(invocation.Target, rowOffset, columnOffset);
                    var args = new FormulaExpression[invocation.Arguments.Count];
                    var changed = !ReferenceEquals(target, invocation.Target);
                    for (var i = 0; i < args.Length; i++)
                    {
                        args[i] = Rewrite(invocation.Arguments[i], rowOffset, columnOffset);
                        changed |= !ReferenceEquals(args[i], invocation.Arguments[i]);
                    }
                    return changed ? new FormulaInvocationExpression(target, args) : expression;
                case FormulaExpressionKind.Reference:
                    return RewriteReference((FormulaReferenceExpression)expression, rowOffset, columnOffset);
                case FormulaExpressionKind.Unary:
                    return RewriteUnary((FormulaUnaryExpression)expression, rowOffset, columnOffset);
                case FormulaExpressionKind.Binary:
                    return RewriteBinary((FormulaBinaryExpression)expression, rowOffset, columnOffset);
                case FormulaExpressionKind.FunctionCall:
                    return RewriteFunction((FormulaFunctionCallExpression)expression, rowOffset, columnOffset);
                case FormulaExpressionKind.ArrayLiteral:
                    return RewriteArray((FormulaArrayExpression)expression, rowOffset, columnOffset);
                default:
                    return expression;
            }
        }

        private static FormulaExpression RewriteReference(
            FormulaReferenceExpression expression,
            int rowOffset,
            int columnOffset)
        {
            FormulaReference reference = expression.Reference;
            if (!TryTranslateAddress(reference.Start, rowOffset, columnOffset, out FormulaReferenceAddress start) ||
                !TryTranslateAddress(reference.End, rowOffset, columnOffset, out FormulaReferenceAddress end))
            {
                return new FormulaLiteralExpression(FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref)));
            }

            if (start == reference.Start && end == reference.End)
            {
                return expression;
            }

            return new FormulaReferenceExpression(reference.Kind == FormulaReferenceKind.Cell
                ? new FormulaReference(start)
                : new FormulaReference(start, end));
        }

        private static bool TryTranslateAddress(
            FormulaReferenceAddress address,
            int rowOffset,
            int columnOffset,
            out FormulaReferenceAddress translated)
        {
            translated = address;
            if (address.Mode != FormulaReferenceMode.A1)
            {
                return true;
            }

            try
            {
                int row = address.RowIsAbsolute ? address.Row : checked(address.Row + rowOffset);
                int column = address.ColumnIsAbsolute ? address.Column : checked(address.Column + columnOffset);
                if (row <= 0 || column <= 0)
                {
                    return false;
                }

                translated = new FormulaReferenceAddress(
                    address.Mode,
                    row,
                    column,
                    address.RowIsAbsolute,
                    address.ColumnIsAbsolute,
                    address.Sheet);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static FormulaExpression RewriteUnary(
            FormulaUnaryExpression expression,
            int rowOffset,
            int columnOffset)
        {
            FormulaExpression operand = Rewrite(expression.Operand, rowOffset, columnOffset);
            return ReferenceEquals(operand, expression.Operand)
                ? expression
                : new FormulaUnaryExpression(expression.Operator, operand);
        }

        private static FormulaExpression RewriteBinary(
            FormulaBinaryExpression expression,
            int rowOffset,
            int columnOffset)
        {
            FormulaExpression left = Rewrite(expression.Left, rowOffset, columnOffset);
            FormulaExpression right = Rewrite(expression.Right, rowOffset, columnOffset);
            return ReferenceEquals(left, expression.Left) && ReferenceEquals(right, expression.Right)
                ? expression
                : new FormulaBinaryExpression(expression.Operator, left, right);
        }

        private static FormulaExpression RewriteFunction(
            FormulaFunctionCallExpression expression,
            int rowOffset,
            int columnOffset)
        {
            FormulaExpression[]? translated = null;
            for (int index = 0; index < expression.Arguments.Count; index++)
            {
                FormulaExpression argument = expression.Arguments[index];
                FormulaExpression next = Rewrite(argument, rowOffset, columnOffset);
                if (translated == null && !ReferenceEquals(argument, next))
                {
                    translated = new FormulaExpression[expression.Arguments.Count];
                    for (int previous = 0; previous < index; previous++)
                    {
                        translated[previous] = expression.Arguments[previous];
                    }
                }

                if (translated != null)
                {
                    translated[index] = next;
                }
            }

            return translated == null
                ? expression
                : new FormulaFunctionCallExpression(expression.Name, translated);
        }

        private static FormulaExpression RewriteArray(
            FormulaArrayExpression expression,
            int rowOffset,
            int columnOffset)
        {
            FormulaExpression[,]? translated = null;
            for (int row = 0; row < expression.RowCount; row++)
            {
                for (int column = 0; column < expression.ColumnCount; column++)
                {
                    FormulaExpression item = expression[row, column];
                    FormulaExpression next = Rewrite(item, rowOffset, columnOffset);
                    if (translated == null && !ReferenceEquals(item, next))
                    {
                        translated = CopyArray(expression);
                    }

                    if (translated != null)
                    {
                        translated[row, column] = next;
                    }
                }
            }

            return translated == null ? expression : new FormulaArrayExpression(translated);
        }

        private static FormulaExpression[,] CopyArray(FormulaArrayExpression expression)
        {
            var items = new FormulaExpression[expression.RowCount, expression.ColumnCount];
            for (int row = 0; row < expression.RowCount; row++)
            {
                for (int column = 0; column < expression.ColumnCount; column++)
                {
                    items[row, column] = expression[row, column];
                }
            }

            return items;
        }
    }
}
