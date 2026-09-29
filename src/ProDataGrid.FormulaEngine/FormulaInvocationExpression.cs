// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    /// <summary>A call whose target is a formula value, including a returned lambda.</summary>
    public sealed class FormulaInvocationExpression : FormulaExpression
    {
        /// <summary>Creates an invocation and takes an immutable snapshot of its arguments.</summary>
        public FormulaInvocationExpression(FormulaExpression target, IReadOnlyList<FormulaExpression> arguments)
            : base(FormulaExpressionKind.Invocation)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            var snapshot = new FormulaExpression[arguments.Count];
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i] = arguments[i] ?? throw new ArgumentException("An argument is null.", nameof(arguments));
            Arguments = Array.AsReadOnly(snapshot);
        }

        /// <summary>Gets the expression that produces the callable value.</summary>
        public FormulaExpression Target { get; }

        /// <summary>Gets the argument expressions, including explicit omissions.</summary>
        public IReadOnlyList<FormulaExpression> Arguments { get; }
    }

    /// <summary>A syntactically omitted argument, distinct from a reference to a blank cell.</summary>
    /// <remarks>Ordinary functions still receive a blank value. Lambda calls retain omission metadata.</remarks>
    public sealed class FormulaMissingExpression : FormulaExpression
    {
        /// <summary>Creates an omitted argument expression.</summary>
        public FormulaMissingExpression() : base(FormulaExpressionKind.Missing) { }
    }
}
