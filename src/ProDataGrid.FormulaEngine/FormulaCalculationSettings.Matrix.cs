// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProDataGrid.FormulaEngine
{
    public sealed partial class FormulaCalculationSettings
    {
        private long _maximumMatrixOperationCount = 100_000_000;

        /// <summary>Gets or sets the positive per-call matrix work limit; the default is 100 million work units.</summary>
        /// <remarks>MMULT estimates rows * inner dimension * columns, MDETERM estimates n cubed,
        /// MINVERSE estimates twice n cubed, and MUNIT estimates n squared. Exceeding this host
        /// resource policy returns #NUM! before allocating matrix workspace. The units are conservative
        /// loop-work estimates, not elapsed time, exact CPU instructions or an Excel compatibility limit.
        /// MaximumArrayCellCount also bounds each matrix input snapshot and result. Neither setting is
        /// a total-workbook memory or execution quota.</remarks>
        public long MaximumMatrixOperationCount
        {
            get => _maximumMatrixOperationCount;
            set => _maximumMatrixOperationCount = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
