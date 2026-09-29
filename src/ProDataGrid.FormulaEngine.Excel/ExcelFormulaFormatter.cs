// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using System.Text;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    public sealed class ExcelFormulaFormatter : IFormulaFormatter
    {
        public string Format(FormulaExpression expression, FormulaFormatOptions options)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var text = FormatExpression(expression, options, 0, false);
            return options.IncludeLeadingEquals ? $"={text}" : text;
        }

        private static string FormatExpression(
            FormulaExpression expression,
            FormulaFormatOptions options,
            int parentPrecedence,
            bool isRightOperand)
        {
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    var target = FormatExpression(invocation.Target, options, 0, false);
                    var invocationText = new StringBuilder();
                    invocationText.Append('(').Append(target).Append(")(");
                    for (var i = 0; i < invocation.Arguments.Count; i++)
                    {
                        if (i > 0) invocationText.Append(options.ArgumentSeparator);
                        invocationText.Append(FormatExpression(invocation.Arguments[i], options, 0, false));
                    }
                    return invocationText.Append(')').ToString();
                case FormulaExpressionKind.Literal:
                    var literal = (FormulaLiteralExpression)expression;
                    return literal.IsOmitted ? string.Empty : FormatLiteral(literal.Value, options);
                case FormulaExpressionKind.Name:
                    return ((FormulaNameExpression)expression).Name;
                case FormulaExpressionKind.Reference:
                    return FormatReference(((FormulaReferenceExpression)expression).Reference, options);
                case FormulaExpressionKind.StructuredReference:
                    return FormatStructuredReference(((FormulaStructuredReferenceExpression)expression).Reference, options);
                case FormulaExpressionKind.FunctionCall:
                    return FormatFunctionCall((FormulaFunctionCallExpression)expression, options);
                case FormulaExpressionKind.ArrayLiteral:
                    return FormatArrayLiteral((FormulaArrayExpression)expression, options);
                case FormulaExpressionKind.Unary:
                    return FormatUnaryExpression((FormulaUnaryExpression)expression, options, parentPrecedence, isRightOperand);
                case FormulaExpressionKind.Binary:
                    return FormatBinaryExpression((FormulaBinaryExpression)expression, options, parentPrecedence, isRightOperand);
                default:
                    return string.Empty;
            }
        }

        private static string FormatUnaryExpression(
            FormulaUnaryExpression expression,
            FormulaFormatOptions options,
            int parentPrecedence,
            bool isRightOperand)
        {
            var precedence = 8;
            var operandText = FormatExpression(expression.Operand, options, precedence, false);
            var text = expression.Operator switch
            {
                FormulaUnaryOperator.Percent => $"{operandText}%",
                FormulaUnaryOperator.Negate => $"-{operandText}",
                _ => $"+{operandText}"
            };

            if (NeedsParentheses(precedence, parentPrecedence, isRightOperand, isRightAssociative: false))
            {
                return $"({text})";
            }

            return text;
        }

        private static string FormatBinaryExpression(
            FormulaBinaryExpression expression,
            FormulaFormatOptions options,
            int parentPrecedence,
            bool isRightOperand)
        {
            var precedence = GetPrecedence(expression.Operator);
            var left = FormatExpression(expression.Left, options, precedence, false);
            var right = FormatExpression(expression.Right, options, precedence, true);
            var op = FormatOperator(expression.Operator);
            var text = $"{left}{op}{right}";

            if (NeedsParentheses(precedence, parentPrecedence, isRightOperand, IsRightAssociative(expression.Operator)))
            {
                return $"({text})";
            }

            return text;
        }

        private static bool NeedsParentheses(
            int precedence,
            int parentPrecedence,
            bool isRightOperand,
            bool isRightAssociative)
        {
            if (precedence < parentPrecedence)
            {
                return true;
            }

            if (precedence == parentPrecedence)
            {
                if (isRightOperand && !isRightAssociative)
                {
                    return true;
                }

                if (!isRightOperand && isRightAssociative)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatFunctionCall(FormulaFunctionCallExpression expression, FormulaFormatOptions options)
        {
            var builder = new StringBuilder();
            builder.Append(expression.Name);
            builder.Append('(');
            for (var i = 0; i < expression.Arguments.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(options.ArgumentSeparator);
                }

                builder.Append(FormatExpression(expression.Arguments[i], options, 0, false));
            }
            builder.Append(')');
            return builder.ToString();
        }

        private static string FormatArrayLiteral(FormulaArrayExpression expression, FormulaFormatOptions options)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            for (var row = 0; row < expression.RowCount; row++)
            {
                if (row > 0)
                {
                    builder.Append(';');
                }

                for (var column = 0; column < expression.ColumnCount; column++)
                {
                    if (column > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(FormatExpression(expression[row, column], options, 0, false));
                }
            }
            builder.Append('}');
            return builder.ToString();
        }

        private static string FormatReference(FormulaReference reference, FormulaFormatOptions options)
        {
            var prefix = reference.Start.Sheet.HasValue
                ? FormatSheetPrefix(reference.Start.Sheet.Value)
                : null;
            var startText = FormatReferenceAddress(reference.Start);
            if (reference.Kind == FormulaReferenceKind.Cell)
            {
                return prefix == null ? startText : $"{prefix}{startText}";
            }

            var endText = FormatReferenceAddress(reference.End);
            if (prefix != null)
            {
                return $"{prefix}{startText}:{endText}";
            }

            if (reference.End.Sheet.HasValue)
            {
                var endPrefix = FormatSheetPrefix(reference.End.Sheet.Value);
                return $"{startText}:{endPrefix}{endText}";
            }

            return $"{startText}:{endText}";

            string FormatReferenceAddress(FormulaReferenceAddress address)
            {
                return address.Mode == FormulaReferenceMode.A1
                    ? FormatA1(address)
                    : FormatR1C1(address);
            }

            string FormatA1(FormulaReferenceAddress address)
            {
                var column = ToColumnName(address.Column);
                var row = address.Row.ToString(CultureInfo.InvariantCulture);
                var colPrefix = address.ColumnIsAbsolute ? "$" : string.Empty;
                var rowPrefix = address.RowIsAbsolute ? "$" : string.Empty;
                return $"{colPrefix}{column}{rowPrefix}{row}";
            }

            string FormatR1C1(FormulaReferenceAddress address)
            {
                var rowPart = FormatR1C1Coordinate(address.Row, address.RowIsAbsolute, 'R');
                var columnPart = FormatR1C1Coordinate(address.Column, address.ColumnIsAbsolute, 'C');
                return $"{rowPart}{columnPart}";
            }
        }

        private static string FormatStructuredReference(FormulaStructuredReference reference, FormulaFormatOptions options)
        {
            var scopePart = reference.Scope switch
            {
                FormulaStructuredReferenceScope.All => "#All",
                FormulaStructuredReferenceScope.Headers => "#Headers",
                FormulaStructuredReferenceScope.Data => "#Data",
                FormulaStructuredReferenceScope.Totals => "#Totals",
                FormulaStructuredReferenceScope.ThisRow => "#This Row",
                _ => string.Empty
            };

            var columnPart = reference.ColumnStart ?? string.Empty;
            if (reference.IsColumnRange)
            {
                columnPart = $"{reference.ColumnStart}:{reference.ColumnEnd}";
            }

            var prefix = reference.TableName ?? string.Empty;
            var sheetPrefix = reference.Sheet.HasValue ? $"{FormatSheetPrefix(reference.Sheet.Value)}" : string.Empty;

            if (!string.IsNullOrWhiteSpace(scopePart))
            {
                return $"{sheetPrefix}{prefix}[[{scopePart}],[{columnPart}]]";
            }

            return $"{sheetPrefix}{prefix}[{columnPart}]";
        }

        private static string FormatLiteral(FormulaValue value, FormulaFormatOptions options)
        {
            return value.Kind switch
            {
                FormulaValueKind.Number => FormatNumber(value.AsNumber(), options.DecimalSeparator),
                FormulaValueKind.Text => $"\"{EscapeString(value.AsText())}\"",
                FormulaValueKind.Boolean => value.AsBoolean() ? "TRUE" : "FALSE",
                FormulaValueKind.Error => FormatError(value.AsError()),
                FormulaValueKind.Blank => "\"\"",
                _ => string.Empty
            };
        }

        private static string FormatNumber(double value, char decimalSeparator)
        {
            var text = value.ToString("G15", CultureInfo.InvariantCulture);
            return decimalSeparator == '.'
                ? text
                : text.Replace('.', decimalSeparator);
        }

        private static string EscapeString(string text)
        {
            return text.Replace("\"", "\"\"");
        }

        private static string FormatError(FormulaError error)
        {
            return error.Type switch
            {
                FormulaErrorType.Div0 => "#DIV/0!",
                FormulaErrorType.NA => "#N/A",
                FormulaErrorType.Name => "#NAME?",
                FormulaErrorType.Null => "#NULL!",
                FormulaErrorType.Num => "#NUM!",
                FormulaErrorType.Ref => "#REF!",
                FormulaErrorType.Value => "#VALUE!",
                FormulaErrorType.Spill => "#SPILL!",
                FormulaErrorType.Calc => "#CALC!",
                _ => "#VALUE!"
            };
        }

        private static string FormatOperator(FormulaBinaryOperator op)
        {
            return op switch
            {
                FormulaBinaryOperator.Add => "+",
                FormulaBinaryOperator.Subtract => "-",
                FormulaBinaryOperator.Multiply => "*",
                FormulaBinaryOperator.Divide => "/",
                FormulaBinaryOperator.Power => "^",
                FormulaBinaryOperator.Concat => "&",
                FormulaBinaryOperator.Equal => "=",
                FormulaBinaryOperator.NotEqual => "<>",
                FormulaBinaryOperator.Less => "<",
                FormulaBinaryOperator.LessOrEqual => "<=",
                FormulaBinaryOperator.Greater => ">",
                FormulaBinaryOperator.GreaterOrEqual => ">=",
                FormulaBinaryOperator.Union => ",",
                FormulaBinaryOperator.Intersection => " ",
                _ => string.Empty
            };
        }

        private static int GetPrecedence(FormulaBinaryOperator op)
        {
            return op switch
            {
                FormulaBinaryOperator.Equal => 1,
                FormulaBinaryOperator.NotEqual => 1,
                FormulaBinaryOperator.Less => 1,
                FormulaBinaryOperator.LessOrEqual => 1,
                FormulaBinaryOperator.Greater => 1,
                FormulaBinaryOperator.GreaterOrEqual => 1,
                FormulaBinaryOperator.Concat => 2,
                FormulaBinaryOperator.Add => 3,
                FormulaBinaryOperator.Subtract => 3,
                FormulaBinaryOperator.Multiply => 4,
                FormulaBinaryOperator.Divide => 4,
                FormulaBinaryOperator.Power => 5,
                FormulaBinaryOperator.Union => 6,
                FormulaBinaryOperator.Intersection => 7,
                _ => 0
            };
        }

        private static bool IsRightAssociative(FormulaBinaryOperator op)
        {
            return op == FormulaBinaryOperator.Power;
        }

        private static string FormatSheetPrefix(FormulaSheetReference sheet)
        {
            var startName = sheet.StartSheetName ?? string.Empty;
            if (!sheet.IsRange)
            {
                return $"{FormatSheetToken(sheet.WorkbookName, startName)}!";
            }

            var endName = sheet.EndSheetName ?? startName;
            var startToken = FormatSheetToken(sheet.WorkbookName, startName);
            var endToken = FormatSheetToken(null, endName);
            return $"{startToken}:{endToken}!";
        }

        private static string FormatSheetToken(string? workbookName, string sheetName)
        {
            var token = sheetName;
            if (!string.IsNullOrWhiteSpace(workbookName))
            {
                token = $"[{workbookName}]{sheetName}";
            }

            if (!RequiresQuoting(token))
            {
                return token;
            }

            return $"'{token.Replace("'", "''")}'";
        }

        private static bool RequiresQuoting(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static string ToColumnName(int column)
        {
            var value = column;
            var builder = new StringBuilder();
            while (value > 0)
            {
                value--;
                var ch = (char)('A' + (value % 26));
                builder.Insert(0, ch);
                value /= 26;
            }
            return builder.ToString();
        }

        private static string FormatR1C1Coordinate(int value, bool absolute, char axis)
        {
            if (absolute)
            {
                return $"{axis}{value.ToString(CultureInfo.InvariantCulture)}";
            }

            if (value == 0)
            {
                return axis.ToString();
            }

            return $"{axis}[{value.ToString(CultureInfo.InvariantCulture)}]";
        }
    }
}
