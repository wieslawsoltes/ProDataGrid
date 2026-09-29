// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProDataGrid.FormulaEngine.Excel
{
    public sealed partial class ExcelFunctionRegistry
    {
        partial void RegisterExtendedDefaults()
        {
            Register(new StackArrayFunction(vertical: false));
            Register(new StackArrayFunction(vertical: true));
            Register(new SliceArrayFunction(take: true));
            Register(new SliceArrayFunction(take: false));
            Register(new ChooseAxisFunction(rows: true));
            Register(new ChooseAxisFunction(rows: false));
            Register(new ExpandFunction());
            Register(new FlattenArrayFunction(column: true));
            Register(new FlattenArrayFunction(column: false));
            Register(new WrapArrayFunction(columns: true));
            Register(new WrapArrayFunction(columns: false));
            Register(new TransposeFunction());
            Register(new SortByFunction());
            Register(new RandArrayFunction());
            RegisterAdditionalDefaults();
            RegisterScientificDefaults();
            Register(new ExclusivePercentileFunction(quartile: false));
            Register(new ExclusivePercentileFunction(quartile: true));
            RegisterProductAggregates();
            RegisterMatrixFunctions();
            RegisterBusinessCalendarFunctions();
            RegisterDescriptiveStatistics();
            RegisterBitwiseDefaults();
        }

        partial void RegisterAdditionalDefaults();
    }
}
