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
            RegisterPairedStatistics();
            RegisterProductAggregates();
            RegisterMatrixFunctions();
            RegisterBusinessCalendarFunctions();
            RegisterDescriptiveStatistics();
            RegisterBitwiseDefaults();
            RegisterRadixDefaults();
            RegisterRankingExtensions();
            Register(new FrequencyFunction());
            Register(new TrimMeanFunction());
            Register(new ExcelModeFunction("MODE", multiple: false));
            Register(new ExcelModeFunction("MODE.MULT", multiple: true));
            Register(new ExcelLogicalAggregateFunction("AVERAGEA", ExcelLogicalAggregateOperation.Average));
            Register(new ExcelLogicalAggregateFunction("MINA", ExcelLogicalAggregateOperation.Minimum));
            Register(new ExcelLogicalAggregateFunction("MAXA", ExcelLogicalAggregateOperation.Maximum));
        }

        partial void RegisterAdditionalDefaults();
    }
}
