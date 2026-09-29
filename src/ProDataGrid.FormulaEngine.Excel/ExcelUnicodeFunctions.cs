// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class UnicodeTextFunction : ExcelElementwiseFunction
    {
        public UnicodeTextFunction() : base("UNICODE", 1, 1) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryText(args[0], out var text, out var error)) return FormulaValue.FromError(error);
            if (text.Length == 0) return ExcelTextUtilities.ValueError();
            if (char.IsHighSurrogate(text[0]) && text.Length > 1 && char.IsLowSurrogate(text[1]))
                return FormulaValue.FromNumber(char.ConvertToUtf32(text[0], text[1]));
            return char.IsSurrogate(text[0]) ? ExcelTextUtilities.ValueError() : FormulaValue.FromNumber(text[0]);
        }
    }

    internal sealed class UnicharTextFunction : ExcelElementwiseFunction
    {
        public UnicharTextFunction() : base("UNICHAR", 1, 1) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelTextUtilities.TryInteger(context, args[0], out var value, out var error)) return FormulaValue.FromError(error);
            if (value <= 0 || value > 0x10ffff) return ExcelTextUtilities.ValueError();
            if (value >= 0xd800 && value <= 0xdfff) return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            return FormulaValue.FromText(char.ConvertFromUtf32(value));
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        partial void RegisterTextExtensions()
        {
            Register(new FindTextFunction(wildcards: false));
            Register(new FindTextFunction(wildcards: true));
            Register(new DelimitedTextFunction(after: false));
            Register(new DelimitedTextFunction(after: true));
            Register(new ExactTextFunction());
            Register(new CleanTextFunction());
            Register(new ProperTextFunction());
            Register(new RepeatTextFunction());
            Register(new ReplaceTextFunction());
            Register(new SubstituteTextFunction());
            Register(new UnicodeTextFunction());
            Register(new UnicharTextFunction());
            Register(new TextSplitFunction());
        }
    }
}
