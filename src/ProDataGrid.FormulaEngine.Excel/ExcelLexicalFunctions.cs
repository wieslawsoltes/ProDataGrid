// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class LetFunction : ExcelFunctionBase, ILazyFormulaFunction, IFormulaLexicalFunction
    {
        public LetFunction() : base("LET", new FormulaFunctionInfo(3, 253)) { }

        public FormulaLexicalBindingKind BindingKind => FormulaLexicalBindingKind.Let;

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => ExcelLexicalUtilities.IncorrectParameters();

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if ((args.Count & 1) == 0) return ExcelLexicalUtilities.IncorrectParameters();
            var scoped = context.EvaluationContext;
            for (var i = 0; i < args.Count - 1; i += 2)
            {
                if (!ExcelLexicalUtilities.TryName(args[i], lambda: false, out var name))
                    return ExcelLexicalUtilities.IncorrectParameters();
                var value = evaluator.Evaluate(args[i + 1], scoped, resolver);
                scoped = scoped.WithLocalValue(name, value, scoped.IsArgumentOmitted(args[i + 1]));
            }
            return evaluator.Evaluate(args[args.Count - 1], scoped, resolver);
        }
    }

    internal sealed class LambdaFunction : ExcelFunctionBase, ILazyFormulaFunction, IFormulaLexicalFunction
    {
        public LambdaFunction() : base("LAMBDA", new FormulaFunctionInfo(1, 254)) { }

        public FormulaLexicalBindingKind BindingKind => FormulaLexicalBindingKind.Lambda;

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => ExcelLexicalUtilities.IncorrectParameters();

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            var parameters = new string[args.Count - 1];
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!ExcelLexicalUtilities.TryName(args[i], lambda: true, out var name) || !names.Add(name))
                    return ExcelLexicalUtilities.IncorrectParameters();
                parameters[i] = name;
            }
            return FormulaValue.FromLambda(context.EvaluationContext.CreateLambda(parameters, args[args.Count - 1]));
        }
    }

    internal sealed class IsOmittedFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public IsOmittedFunction() : base("ISOMITTED", new FormulaFunctionInfo(1, 1)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => FormulaValue.FromBoolean(false);

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
            => FormulaValue.FromBoolean(context.EvaluationContext.IsArgumentOmitted(args[0]));
    }

    internal static class ExcelLexicalUtilities
    {
        public static FormulaValue IncorrectParameters()
            => FormulaValue.FromError(new FormulaError(FormulaErrorType.Value, "Incorrect Parameters"));

        public static bool TryName(FormulaExpression expression, bool lambda, out string name)
        {
            name = expression is FormulaNameExpression identifier ? identifier.Name : string.Empty;
            if (name.Length == 0 || name.Length > 255 ||
                !(char.IsLetter(name[0]) || name[0] == '_' || name[0] == '\\')) return false;
            for (var i = 1; i < name.Length; i++)
            {
                var ch = name[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || (!lambda && ch == '.'))) return false;
            }
            if (string.Equals(name, "R", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "C", StringComparison.OrdinalIgnoreCase)) return false;
            return !ExcelReferenceParser.TryParseA1(name, null, out _) &&
                !ExcelReferenceParser.TryParseR1C1(name, null, out _);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        partial void RegisterAdditionalDefaults()
        {
            Register(new LetFunction());
            Register(new LambdaFunction());
            Register(new IsOmittedFunction());
            RegisterLambdaHelpers();
        }

        partial void RegisterLambdaHelpers();
    }
}
