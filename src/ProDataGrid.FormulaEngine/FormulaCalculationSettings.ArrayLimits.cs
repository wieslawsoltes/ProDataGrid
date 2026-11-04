// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProDataGrid.FormulaEngine
{
    public sealed partial class FormulaCalculationSettings
    {
        private int _maximumArrayCellCount = 1048576;

        /// <summary>
        /// Gets or sets the maximum number of cells allocated by SEQUENCE and the modern
        /// Excel array-shaping functions. The default is 1,048,576 cells per result.
        /// </summary>
        /// <remarks>
        /// This bounds individual function outputs, not total workbook memory. Existing
        /// reference materialization and other evaluator operations have separate costs.
        /// Excel row/column limits still apply even when this value is increased.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
        public int MaximumArrayCellCount
        {
            get => _maximumArrayCellCount;
            set => _maximumArrayCellCount = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
