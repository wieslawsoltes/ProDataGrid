// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    public sealed partial class FormulaEvaluationContext
    {
        internal FormulaLocalScope? LocalScope { get; private set; }
        internal int LambdaDepth { get; private set; }

        /// <summary>Creates a child context with a local binding without modifying the current context.</summary>
        /// <remarks>Values are captured, not reevaluated when referenced. Names are case-insensitive.</remarks>
        public FormulaEvaluationContext WithLocalValue(string name, FormulaValue value, bool omitted = false)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A name is required.", nameof(name));
            return WithScope(new FormulaLocalScope(LocalScope, name, value, omitted), LambdaDepth);
        }

        /// <summary>Captures the current lexical bindings in a reusable lambda definition.</summary>
        public FormulaLambda CreateLambda(IReadOnlyList<string> parameters, FormulaExpression body)
            => new FormulaLambda(parameters, body, LocalScope);

        /// <summary>Reports whether an expression refers to a syntactically omitted lambda argument.</summary>
        public bool IsArgumentOmitted(FormulaExpression expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            return expression is FormulaMissingExpression ||
                (expression is FormulaNameExpression name && LocalScope != null &&
                 LocalScope.TryGet(name.Name, out _, out var omitted) && omitted);
        }

        internal FormulaEvaluationContext WithoutLocals() => LocalScope == null ? this : WithScope(null, LambdaDepth);

        internal FormulaEvaluationContext WithScope(FormulaLocalScope? scope, int depth)
            => new FormulaEvaluationContext(Workbook, Worksheet, Address, FunctionRegistry)
            {
                LocalScope = scope,
                LambdaDepth = depth
            };
    }

    public sealed partial class FormulaCalculationSettings
    {
        private int _maximumLambdaDepth = 128;

        /// <summary>Gets or sets the recursive lambda-call depth limit, from 1 through 256; default 128.</summary>
        /// <remarks>Exceeding the limit returns #NUM!. This is not a general formula execution or time quota.</remarks>
        public int MaximumLambdaDepth
        {
            get => _maximumLambdaDepth;
            set => _maximumLambdaDepth = value >= 1 && value <= 256
                ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
