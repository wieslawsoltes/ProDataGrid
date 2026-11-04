// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    /// <summary>An immutable lambda definition and its captured lexical bindings.</summary>
    /// <remarks>Create through <see cref="FormulaEvaluationContext.CreateLambda"/>. Bodies must not be
    /// mutated after creation. Captured arrays retain normal FormulaArray reference semantics.</remarks>
    public sealed class FormulaLambda
    {
        internal FormulaLambda(IReadOnlyList<string> parameters, FormulaExpression body, FormulaLocalScope? scope)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            var names = new string[parameters.Count];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < names.Length; i++)
            {
                var name = parameters[i];
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    throw new ArgumentException("Parameter names must be nonempty and distinct.", nameof(parameters));
                names[i] = name;
            }
            Parameters = Array.AsReadOnly(names);
            CapturedScope = scope;
        }

        /// <summary>Gets the immutable parameter names in declaration order.</summary>
        public IReadOnlyList<string> Parameters { get; }

        /// <summary>Gets the number of required argument positions. A position may be explicitly omitted.</summary>
        public int ParameterCount => Parameters.Count;

        /// <summary>Gets the reusable expression body.</summary>
        public FormulaExpression Body { get; }

        internal FormulaLocalScope? CapturedScope { get; }
    }

    // Immutable frames ensure closures cannot observe later calls reusing argument buffers.
    // Common one/two-parameter lambdas store values inline, avoiding a values-array allocation.
    internal sealed class FormulaLocalScope
    {
        private readonly FormulaLocalScope? _parent;
        private readonly string? _singleName;
        private readonly IReadOnlyList<string>? _names;
        private readonly FormulaValue _first;
        private readonly FormulaValue _second;
        private readonly FormulaValue[]? _remaining;
        private readonly bool _firstOmitted;
        private readonly bool _secondOmitted;
        private readonly bool[]? _remainingOmitted;

        public FormulaLocalScope(FormulaLocalScope? parent, string name, FormulaValue value, bool omitted)
        {
            _parent = parent;
            _singleName = name;
            _first = value;
            _firstOmitted = omitted;
        }

        public FormulaLocalScope(FormulaLocalScope? parent, IReadOnlyList<string> names,
            IReadOnlyList<FormulaValue> values, IReadOnlyList<bool>? omitted)
        {
            _parent = parent;
            _names = names;
            if (values.Count > 0) { _first = values[0]; _firstOmitted = omitted?[0] ?? false; }
            if (values.Count > 1) { _second = values[1]; _secondOmitted = omitted?[1] ?? false; }
            if (values.Count > 2)
            {
                _remaining = new FormulaValue[values.Count - 2];
                if (omitted != null) _remainingOmitted = new bool[values.Count - 2];
                for (var i = 2; i < values.Count; i++)
                {
                    _remaining[i - 2] = values[i];
                    if (_remainingOmitted != null) _remainingOmitted[i - 2] = omitted![i];
                }
            }
        }

        public bool TryGet(string name, out FormulaValue value, out bool omitted)
        {
            for (var scope = this; scope != null; scope = scope._parent)
            {
                if (scope._singleName != null)
                {
                    if (!string.Equals(scope._singleName, name, StringComparison.OrdinalIgnoreCase)) continue;
                    value = scope._first;
                    omitted = scope._firstOmitted;
                    return true;
                }
                var names = scope._names!;
                for (var i = 0; i < names.Count; i++)
                {
                    if (!string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                    value = i == 0 ? scope._first : i == 1 ? scope._second : scope._remaining![i - 2];
                    omitted = i == 0 ? scope._firstOmitted : i == 1 ? scope._secondOmitted : scope._remainingOmitted?[i - 2] ?? false;
                    return true;
                }
            }
            value = FormulaValue.Blank;
            omitted = false;
            return false;
        }
    }
}
