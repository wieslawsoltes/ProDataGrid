// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProDataGrid.FormulaEngine.Excel
{
    public sealed partial class ExcelFunctionRegistry
    {
        /// <summary>Gets the revision of the registered function definitions.</summary>
        public long Version { get; private set; }

        // Keep additional function families separate from the legacy implementation file.
        partial void RegisterExtendedDefaults();

        partial void RegisterTextExtensions();

        partial void RegisterScientificDefaults();
    }
}
