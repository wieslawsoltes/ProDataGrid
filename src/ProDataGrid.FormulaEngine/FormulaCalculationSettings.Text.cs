// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProDataGrid.FormulaEngine
{
    /// <summary>Controls character positions in the Excel text function family.</summary>
    public enum FormulaTextCompatibilityVersion
    {
        /// <summary>Count UTF-16 code units, preserving the engine's original behavior.</summary>
        Version1 = 1,
        /// <summary>Count valid surrogate pairs as one character; combining marks and variation selectors remain separate.</summary>
        Version2 = 2
    }

    public sealed partial class FormulaCalculationSettings
    {
        private FormulaTextCompatibilityVersion _textCompatibilityVersion = FormulaTextCompatibilityVersion.Version1;

        /// <summary>Gets or sets the text-index compatibility mode; defaults to Version1 for existing workbooks.</summary>
        /// <remarks>Affects LEN, LEFT, RIGHT, MID, FIND, SEARCH and REPLACE. This is character indexing,
        /// not grapheme segmentation, locale collation or an overall workbook compatibility version.</remarks>
        public FormulaTextCompatibilityVersion TextCompatibilityVersion
        {
            get => _textCompatibilityVersion;
            set => _textCompatibilityVersion = value == FormulaTextCompatibilityVersion.Version1 || value == FormulaTextCompatibilityVersion.Version2
                ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
