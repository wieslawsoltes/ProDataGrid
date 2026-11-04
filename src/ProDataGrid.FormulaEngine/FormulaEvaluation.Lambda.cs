// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    public sealed partial class FormulaEvaluator
    {
        /// <summary>Invokes a lambda with evaluated arguments and optional omission metadata.</summary>
        /// <remarks>Parameter bindings are snapshotted. Arguments may be reused by the caller after this method
        /// returns, including when the result is another closure. Recursion is bounded by MaximumLambdaDepth.</remarks>
        public FormulaValue InvokeLambda(FormulaLambda lambda, IReadOnlyList<FormulaValue> arguments,
            FormulaEvaluationContext context, IFormulaValueResolver resolver, IReadOnlyList<bool>? omitted = null)
        {
            if (lambda == null) throw new ArgumentNullException(nameof(lambda));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (lambda.ParameterCount != arguments.Count || (omitted != null && omitted.Count != arguments.Count))
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value, "Incorrect Parameters"));
            if (context.LambdaDepth >= context.Workbook.Settings.MaximumLambdaDepth)
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Num, "Lambda recursion limit exceeded."));

            var scope = lambda.ParameterCount == 0 ? lambda.CapturedScope :
                new FormulaLocalScope(lambda.CapturedScope, lambda.Parameters, arguments, omitted);
            return Evaluate(lambda.Body, context.WithScope(scope, context.LambdaDepth + 1), resolver);
        }

        private FormulaValue EvaluateInvocation(FormulaInvocationExpression expression,
            FormulaEvaluationContext context, IFormulaValueResolver resolver)
        {
            var target = Evaluate(expression.Target, context, resolver);
            return InvokeLambdaExpressions(target, expression.Arguments, context, resolver);
        }

        private FormulaValue InvokeLambdaExpressions(FormulaValue target, IReadOnlyList<FormulaExpression> expressions,
            FormulaEvaluationContext context, IFormulaValueResolver resolver)
        {
            if (target.Kind == FormulaValueKind.Error) return target;
            if (target.Kind != FormulaValueKind.Lambda)
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value, "The target is not callable."));
            var lambda = target.AsLambda();
            if (lambda.ParameterCount != expressions.Count)
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value, "Incorrect Parameters"));
            var values = expressions.Count == 0 ? Array.Empty<FormulaValue>() : new FormulaValue[expressions.Count];
            for (var i = 0; i < values.Length; i++) values[i] = Evaluate(expressions[i], context, resolver);
            return InvokeLambda(lambda, values, context, resolver, GetOmissions(expressions, context));
        }

        private static bool[]? GetOmissions(IReadOnlyList<FormulaExpression>? expressions, FormulaEvaluationContext context)
        {
            bool[]? result = null;
            if (expressions != null)
            {
                for (var i = 0; i < expressions.Count; i++)
                {
                    if (!context.IsArgumentOmitted(expressions[i])) continue;
                    result ??= new bool[expressions.Count];
                    result[i] = true;
                }
            }
            return result;
        }
    }
}
