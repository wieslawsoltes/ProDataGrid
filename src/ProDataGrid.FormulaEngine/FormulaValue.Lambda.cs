// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProDataGrid.FormulaEngine
{
    public readonly partial struct FormulaValue
    {
        /// <summary>Wraps a lambda without boxing or allocating a payload wrapper.</summary>
        public static FormulaValue FromLambda(FormulaLambda lambda)
            => new FormulaValue(FormulaValueKind.Lambda, payload: lambda ?? throw new ArgumentNullException(nameof(lambda)));

        /// <summary>Gets the callable definition; throws when the value is not a lambda.</summary>
        public FormulaLambda AsLambda()
        {
            if (Kind != FormulaValueKind.Lambda)
                throw new InvalidOperationException($"Cannot access {Kind} as lambda.");
            return (FormulaLambda)_payload!;
        }

        /// <summary>Converts an uncalled lambda to #CALC! when publishing a worksheet result.</summary>
        /// <remarks>The low-level evaluator retains callable values for composition. Worksheet hosts should
        /// use this method at their cell-result boundary, not inside function arguments.</remarks>
        public FormulaValue ToCellResult()
        {
            if (Kind == FormulaValueKind.Lambda)
                return FromError(new FormulaError(FormulaErrorType.Calc, "A lambda must be invoked to produce a cell result."));
            if (Kind == FormulaValueKind.Array)
            {
                var array = AsArray();
                for (var row = 0; row < array.RowCount; row++)
                    for (var column = 0; column < array.ColumnCount; column++)
                        if (array.IsPresent(row, column) && array[row, column].Kind == FormulaValueKind.Lambda)
                            return FromError(new FormulaError(FormulaErrorType.Calc, "An array cannot spill callable values."));
            }
            return this;
        }
    }
}
