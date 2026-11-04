// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    public sealed class ExcelFormulaParser : IFormulaParser
    {
        public FormulaExpression Parse(string formulaText, FormulaParseOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var tokenizer = new ExcelFormulaTokenizer(options);
            var tokens = tokenizer.Tokenize(formulaText);
            var parser = new ExcelFormulaParserCore(tokens, options);
            var expression = parser.ParseExpression();
            parser.Expect(FormulaTokenType.End);
            return expression;
        }

        private sealed class ExcelFormulaParserCore
        {
            private readonly IReadOnlyList<FormulaToken> _tokens;
            private readonly FormulaParseOptions _options;
            private int _index;

            public ExcelFormulaParserCore(IReadOnlyList<FormulaToken> tokens, FormulaParseOptions options)
            {
                _tokens = tokens;
                _options = options;
            }

            public FormulaExpression ParseExpression(int minPrecedence = 0, bool allowUnion = true)
            {
                var left = ParseUnary();

                while (true)
                {
                    if (TryParseRange(ref left))
                    {
                        continue;
                    }

                    if (!TryGetBinaryOperator(Peek(), allowUnion, _options, out var op))
                    {
                        break;
                    }

                    var precedence = GetPrecedence(op);
                    if (precedence < minPrecedence)
                    {
                        break;
                    }

                    var rightAssociative = op == FormulaBinaryOperator.Power;
                    Next();
                    var right = ParseExpression(rightAssociative ? precedence : precedence + 1, allowUnion);
                    left = new FormulaBinaryExpression(op, left, right);
                }

                return left;
            }

            private bool TryParseRange(ref FormulaExpression left)
            {
                if (Peek().Type != FormulaTokenType.Colon)
                {
                    return false;
                }

                if (left is not FormulaReferenceExpression leftRef)
                {
                    throw new FormulaParseException("Range operator requires a reference on the left side.", Peek().Start);
                }

                Next();
                var right = ParseUnary();
                if (right is not FormulaReferenceExpression rightRef)
                {
                    throw new FormulaParseException("Range operator requires a reference on the right side.", Peek().Start);
                }

                var range = new FormulaReference(leftRef.Reference.Start, rightRef.Reference.End);
                left = new FormulaReferenceExpression(range);
                return true;
            }

            private FormulaExpression ParseUnary()
            {
                var token = Peek();
                if (token.Type == FormulaTokenType.Operator)
                {
                    if (token.Text == "+")
                    {
                        Next();
                        return new FormulaUnaryExpression(FormulaUnaryOperator.Plus, ParseUnary());
                    }

                    if (token.Text == "-")
                    {
                        Next();
                        return new FormulaUnaryExpression(FormulaUnaryOperator.Negate, ParseUnary());
                    }
                }

                var expression = ParsePrimary();
                while (true)
                {
                    if (Match(FormulaTokenType.OpenParen))
                    {
                        expression = new FormulaInvocationExpression(expression, ParseArguments());
                    }
                    else if (Peek().Type == FormulaTokenType.Operator && Peek().Text == "%")
                    {
                        Next();
                        expression = new FormulaUnaryExpression(FormulaUnaryOperator.Percent, expression);
                    }
                    else break;
                }

                return expression;
            }

            private FormulaExpression ParsePrimary()
            {
                var token = Peek();
                switch (token.Type)
                {
                    case FormulaTokenType.Number:
                        Next();
                    var number = ParseNumber(token.Text, token.Start);
                        return new FormulaLiteralExpression(FormulaValue.FromNumber(number));
                    case FormulaTokenType.Text:
                        Next();
                        return new FormulaLiteralExpression(FormulaValue.FromText(token.Text));
                    case FormulaTokenType.Boolean:
                        Next();
                        var boolValue = string.Equals(token.Text, "TRUE", StringComparison.OrdinalIgnoreCase);
                        return new FormulaLiteralExpression(FormulaValue.FromBoolean(boolValue));
                    case FormulaTokenType.Error:
                        Next();
                        if (!ExcelErrorParser.TryParse(token.Text, out var error))
                        {
                            throw new FormulaParseException($"Unknown error token '{token.Text}'.", token.Start);
                        }
                        return new FormulaLiteralExpression(FormulaValue.FromError(error));
                    case FormulaTokenType.Name:
                        return ParseNameOrReference();
                    case FormulaTokenType.OpenParen:
                        Next();
                        var inner = ParseExpression();
                        Expect(FormulaTokenType.CloseParen);
                        return inner;
                    case FormulaTokenType.OpenBrace:
                        return ParseArrayLiteral();
                    default:
                        throw new FormulaParseException($"Unexpected token '{token.Text}'.", token.Start);
                }
            }

            private FormulaExpression ParseNameOrReference()
            {
                var nameToken = Expect(FormulaTokenType.Name);
                var nameText = nameToken.Text;
                FormulaSheetReference? sheet = null;

                if (TryParseSheetPrefix(nameText, out var parsedSheet, out var nextReferenceToken, out var referenceTokenStart))
                {
                    sheet = parsedSheet;
                    if (nextReferenceToken == null)
                    {
                        throw new FormulaParseException("Expected reference after sheet prefix.", nameToken.Start);
                    }

                    if (TryParseReference(nextReferenceToken, sheet, out var sheetReference))
                    {
                        return new FormulaReferenceExpression(sheetReference);
                    }

                    if (TryParseStructuredReference(nextReferenceToken, sheet, out var sheetStructuredReference))
                    {
                        return new FormulaStructuredReferenceExpression(sheetStructuredReference);
                    }

                    throw new FormulaParseException($"Invalid reference '{nextReferenceToken}'.", referenceTokenStart);
                }

                if (Match(FormulaTokenType.OpenParen))
                {
                    var args = ParseArguments();
                    return new FormulaFunctionCallExpression(nameText, args);
                }

                if (TryParseReference(nameText, sheet, out var reference))
                {
                    return new FormulaReferenceExpression(reference);
                }

                if (TryParseStructuredReference(nameText, sheet, out var structuredReference))
                {
                    return new FormulaStructuredReferenceExpression(structuredReference);
                }

                return new FormulaNameExpression(nameText);
            }

            private IReadOnlyList<FormulaExpression> ParseArguments()
            {
                var args = new List<FormulaExpression>();
                var expectValue = true;

                while (true)
                {
                    var token = Peek();
                    if (token.Type == FormulaTokenType.CloseParen)
                    {
                        Next();
                        if (expectValue && args.Count > 0)
                        {
                            args.Add(new FormulaLiteralExpression(FormulaValue.Blank, isOmitted: true));
                        }
                        return args;
                    }

                    if (token.Type == FormulaTokenType.Comma || token.Type == FormulaTokenType.Semicolon)
                    {
                        args.Add(new FormulaLiteralExpression(FormulaValue.Blank, isOmitted: true));
                        Next();
                        expectValue = true;
                        continue;
                    }

                    args.Add(ParseExpression(0, allowUnion: false));
                    expectValue = false;

                    if (Match(FormulaTokenType.Comma) || Match(FormulaTokenType.Semicolon))
                    {
                        expectValue = true;
                        continue;
                    }

                    Expect(FormulaTokenType.CloseParen);
                    return args;
                }
            }

            private FormulaExpression ParseArrayLiteral()
            {
                var open = Expect(FormulaTokenType.OpenBrace);
                if (Match(FormulaTokenType.CloseBrace))
                {
                    throw new FormulaParseException("Array literal cannot be empty.", open.Start);
                }

                var rows = new List<List<FormulaExpression>>();
                var currentRow = new List<FormulaExpression>();

                while (true)
                {
                    var element = ParseExpression(0, allowUnion: false);
                    currentRow.Add(element);

                    if (Match(FormulaTokenType.Comma))
                    {
                        continue;
                    }

                    if (Match(FormulaTokenType.Semicolon))
                    {
                        rows.Add(currentRow);
                        currentRow = new List<FormulaExpression>();
                        continue;
                    }

                    if (Match(FormulaTokenType.CloseBrace))
                    {
                        rows.Add(currentRow);
                        break;
                    }

                    throw new FormulaParseException("Expected ',', ';', or '}' in array literal.", Peek().Start);
                }

                if (rows.Count == 0 || rows[0].Count == 0)
                {
                    throw new FormulaParseException("Array literal cannot be empty.", open.Start);
                }

                var columnCount = rows[0].Count;
                for (var row = 1; row < rows.Count; row++)
                {
                    if (rows[row].Count != columnCount)
                    {
                        throw new FormulaParseException("Array literal rows must have the same length.", open.Start);
                    }
                }

                var items = new FormulaExpression[rows.Count, columnCount];
                for (var row = 0; row < rows.Count; row++)
                {
                    for (var column = 0; column < columnCount; column++)
                    {
                        items[row, column] = rows[row][column];
                    }
                }

                return new FormulaArrayExpression(items);
            }

            private bool TryParseReference(string text, FormulaSheetReference? sheet, out FormulaReference reference)
            {
                reference = default;
                if (_options.ReferenceMode == FormulaReferenceMode.A1)
                {
                    if (ExcelReferenceParser.TryParseA1(text, sheet, out var address))
                    {
                        reference = new FormulaReference(address);
                        return true;
                    }
                }
                else
                {
                    if (ExcelReferenceParser.TryParseR1C1(text, sheet, out var address))
                    {
                        reference = new FormulaReference(address);
                        return true;
                    }
                }

                return false;
            }

            private bool TryParseStructuredReference(string text, FormulaSheetReference? sheet, out FormulaStructuredReference reference)
            {
                reference = default;
                if (!ExcelStructuredReferenceParser.TryParse(text, sheet, out var parsed))
                {
                    return false;
                }

                reference = parsed;
                return true;
            }

            private bool TryParseSheetPrefix(
                string firstToken,
                out FormulaSheetReference? sheet,
                out string? nextReferenceToken,
                out int referenceTokenStart)
            {
                sheet = null;
                nextReferenceToken = null;
                referenceTokenStart = 0;

                if (Peek().Type == FormulaTokenType.Colon)
                {
                    var nextName = Peek(1);
                    var nextNext = Peek(2);
                    if (nextName.Type == FormulaTokenType.Name && nextNext.Type == FormulaTokenType.Exclamation)
                    {
                        Next();
                        var endSheetToken = Expect(FormulaTokenType.Name);
                        Expect(FormulaTokenType.Exclamation);

                        sheet = ExcelSheetNameParser.ParseSheetRange(firstToken, endSheetToken.Text);
                        var referenceToken = Expect(FormulaTokenType.Name);
                        nextReferenceToken = referenceToken.Text;
                        referenceTokenStart = referenceToken.Start;
                        return true;
                    }
                }

                if (Match(FormulaTokenType.Exclamation))
                {
                    sheet = ExcelSheetNameParser.ParseSheetReference(firstToken);
                    var referenceToken = Expect(FormulaTokenType.Name);
                    nextReferenceToken = referenceToken.Text;
                    referenceTokenStart = referenceToken.Start;
                    return true;
                }

                if (ExcelSheetNameParser.TryParseEmbeddedSheetRange(firstToken, out sheet))
                {
                    if (Match(FormulaTokenType.Exclamation))
                    {
                        var referenceToken = Expect(FormulaTokenType.Name);
                        nextReferenceToken = referenceToken.Text;
                        referenceTokenStart = referenceToken.Start;
                        return true;
                    }
                }

                sheet = null;
                return false;
            }

            private FormulaToken Peek(int offset = 0)
            {
                var index = Math.Min(_index + offset, _tokens.Count - 1);
                return _tokens[index];
            }

            private FormulaToken Next()
            {
                return _tokens[_index++];
            }

            public FormulaToken Expect(FormulaTokenType type)
            {
                var token = Next();
                if (token.Type != type)
                {
                    throw new FormulaParseException($"Expected {type} but found '{token.Text}'.", token.Start);
                }
                return token;
            }

            private bool Match(FormulaTokenType type)
            {
                if (Peek().Type != type)
                {
                    return false;
                }
                _index++;
                return true;
            }

            private static bool TryGetBinaryOperator(
                FormulaToken token,
                bool allowUnion,
                FormulaParseOptions options,
                out FormulaBinaryOperator op)
            {
                op = default;
                if (token.Type == FormulaTokenType.Intersection)
                {
                    op = FormulaBinaryOperator.Intersection;
                    return true;
                }

                if (allowUnion &&
                    ((token.Type == FormulaTokenType.Comma && options.ArgumentSeparator != ';') ||
                     (token.Type == FormulaTokenType.Semicolon && options.ArgumentSeparator == ';')))
                {
                    op = FormulaBinaryOperator.Union;
                    return true;
                }

                if (token.Type != FormulaTokenType.Operator)
                {
                    return false;
                }

                return token.Text switch
                {
                    "+" => SetOperator(out op, FormulaBinaryOperator.Add),
                    "-" => SetOperator(out op, FormulaBinaryOperator.Subtract),
                    "*" => SetOperator(out op, FormulaBinaryOperator.Multiply),
                    "/" => SetOperator(out op, FormulaBinaryOperator.Divide),
                    "^" => SetOperator(out op, FormulaBinaryOperator.Power),
                    "&" => SetOperator(out op, FormulaBinaryOperator.Concat),
                    "=" => SetOperator(out op, FormulaBinaryOperator.Equal),
                    "<>" => SetOperator(out op, FormulaBinaryOperator.NotEqual),
                    "<" => SetOperator(out op, FormulaBinaryOperator.Less),
                    "<=" => SetOperator(out op, FormulaBinaryOperator.LessOrEqual),
                    ">" => SetOperator(out op, FormulaBinaryOperator.Greater),
                    ">=" => SetOperator(out op, FormulaBinaryOperator.GreaterOrEqual),
                    _ => false
                };
            }

            private static bool SetOperator(out FormulaBinaryOperator target, FormulaBinaryOperator op)
            {
                target = op;
                return true;
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

            private double ParseNumber(string text, int position)
            {
                var normalized = text;
                if (_options.DecimalSeparator != '.')
                {
                    normalized = text.Replace(_options.DecimalSeparator, '.');
                }

                if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw new FormulaParseException($"Invalid number '{text}'.", position);
                }

                return value;
            }
        }
    }

    internal static class ExcelErrorParser
    {
        public static bool TryParse(string token, out FormulaError error)
        {
            error = default;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var normalized = token.ToUpperInvariant();
            error = normalized switch
            {
                "#DIV/0!" => new FormulaError(FormulaErrorType.Div0),
                "#N/A" => new FormulaError(FormulaErrorType.NA),
                "#NAME?" => new FormulaError(FormulaErrorType.Name),
                "#NULL!" => new FormulaError(FormulaErrorType.Null),
                "#NUM!" => new FormulaError(FormulaErrorType.Num),
                "#REF!" => new FormulaError(FormulaErrorType.Ref),
                "#VALUE!" => new FormulaError(FormulaErrorType.Value),
                "#SPILL!" => new FormulaError(FormulaErrorType.Spill),
                "#CALC!" => new FormulaError(FormulaErrorType.Calc),
                _ => default
            };

            return normalized is "#DIV/0!" or "#N/A" or "#NAME?" or "#NULL!" or "#NUM!" or "#REF!" or "#VALUE!" or "#SPILL!" or "#CALC!";
        }
    }

    internal static class ExcelReferenceParser
    {
        public static bool TryParseA1(string text, FormulaSheetReference? sheet, out FormulaReferenceAddress address)
        {
            address = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var index = 0;
            var length = text.Length;
            var columnAbsolute = false;
            var rowAbsolute = false;

            if (text[index] == '$')
            {
                columnAbsolute = true;
                index++;
            }

            var columnStart = index;
            while (index < length && char.IsLetter(text[index]))
            {
                index++;
            }

            if (columnStart == index)
            {
                return false;
            }

            var columnText = text.Substring(columnStart, index - columnStart).ToUpperInvariant();
            var column = 0;
            foreach (var ch in columnText)
            {
                column = column * 26 + (ch - 'A' + 1);
            }

            if (index < length && text[index] == '$')
            {
                rowAbsolute = true;
                index++;
            }

            var rowStart = index;
            while (index < length && char.IsDigit(text[index]))
            {
                index++;
            }

            if (rowStart == index || index != length)
            {
                return false;
            }

            if (!int.TryParse(text.Substring(rowStart, index - rowStart), out var row) || row <= 0)
            {
                return false;
            }

            address = new FormulaReferenceAddress(FormulaReferenceMode.A1, row, column, rowAbsolute, columnAbsolute, sheet);
            return true;
        }

        public static bool TryParseR1C1(string text, FormulaSheetReference? sheet, out FormulaReferenceAddress address)
        {
            address = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var index = 0;
            if (text[index] != 'R' && text[index] != 'r')
            {
                return false;
            }

            index++;
            if (!TryParseR1C1Coordinate(text, ref index, out var row, out var rowAbsolute))
            {
                return false;
            }

            if (index >= text.Length || (text[index] != 'C' && text[index] != 'c'))
            {
                return false;
            }

            index++;
            if (!TryParseR1C1Coordinate(text, ref index, out var column, out var columnAbsolute))
            {
                return false;
            }

            if (index != text.Length)
            {
                return false;
            }

            address = new FormulaReferenceAddress(FormulaReferenceMode.R1C1, row, column, rowAbsolute, columnAbsolute, sheet);
            return true;
        }

        private static bool TryParseR1C1Coordinate(string text, ref int index, out int value, out bool absolute)
        {
            value = 0;
            absolute = false;
            if (index >= text.Length)
            {
                return true;
            }

            if (text[index] == '[')
            {
                index++;
                var start = index;
                if (index < text.Length && (text[index] == '+' || text[index] == '-'))
                {
                    index++;
                }

                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }

                if (index >= text.Length || text[index] != ']')
                {
                    return false;
                }

                var span = text.Substring(start, index - start);
                if (!int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }

                absolute = false;
                index++;
                return true;
            }

            if (index < text.Length && char.IsDigit(text[index]))
            {
                var start = index;
                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }

                if (!int.TryParse(text.Substring(start, index - start), out value) || value <= 0)
                {
                    return false;
                }

                absolute = true;
                return true;
            }

            absolute = false;
            value = 0;
            return true;
        }
    }
}
