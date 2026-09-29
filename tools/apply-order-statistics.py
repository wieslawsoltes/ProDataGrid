from pathlib import Path
p = Path('src/ProDataGrid.FormulaEngine.Excel/ExcelStatisticalFunctions.cs')
s = p.read_text(encoding='utf-8')
start = s.index('        public override FormulaValue Invoke(', s.index('    internal sealed class MedianFunction'))
end = s.index('\n    internal sealed class ModeSingleFunction', start)
s = s[:start] + '''        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => ExcelOrderStatistics.Median(context, args);
    }
''' + s[end:]
start = s.index('        public static FormulaValue OrderedStatistic(')
end = s.index('        public static FormulaValue Rank(', start)
s = s[:start] + '''        public static FormulaValue OrderedStatistic(
            FormulaCalculationSettings settings, FormulaValue dataValue, FormulaValue kValue, bool largest)
            => ExcelOrderStatistics.Ordered(settings, dataValue, kValue, largest);

        public static FormulaValue PercentileInc(FormulaCalculationSettings settings, FormulaValue dataValue, double k)
            => ExcelOrderStatistics.Percentile(settings, dataValue, k, exclusive: false);

''' + s[end:]
p.write_text(s,encoding='utf-8')
p = Path('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.ArrayFunctions.cs')
s = p.read_text(encoding='utf-8'); marker='            RegisterScientificDefaults();'
assert s.count(marker)==1
p.write_text(s.replace(marker, marker+'\n            Register(new ExclusivePercentileFunction(quartile: false));\n            Register(new ExclusivePercentileFunction(quartile: true));'),encoding='utf-8')
