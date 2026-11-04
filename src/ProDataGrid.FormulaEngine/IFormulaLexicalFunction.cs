// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProDataGrid.FormulaEngine
{
    /// <summary>Describes binding syntax to dependency and volatility analysis.</summary>
    public enum FormulaLexicalBindingKind
    {
        /// <summary>All arguments are ordinary value expressions.</summary>
        None,
        /// <summary>Alternating name/value bindings followed by a calculation.</summary>
        Let,
        /// <summary>Parameter declarations followed by a function body.</summary>
        Lambda
    }

    /// <summary>Optional metadata for functions whose argument syntax introduces lexical names.</summary>
    /// <remarks>Implement alongside IFormulaFunction. Runtime evaluation remains the function's
    /// responsibility. Replacing a lexical function with an ordinary function removes its special
    /// dependency traversal when the graph is constructed with the same registry.</remarks>
    public interface IFormulaLexicalFunction
    {
        /// <summary>Gets the binding semantics of the function's argument expressions.</summary>
        FormulaLexicalBindingKind BindingKind { get; }
    }
}
