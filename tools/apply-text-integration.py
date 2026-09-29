from pathlib import Path
p=Path('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.cs')
s=p.read_text(encoding='utf-8')
start=s.index('    internal sealed class LenFunction')
end=s.index('    internal sealed class ConcatFunction', start)
b=s[start:end]
old='ExcelFunctionUtilities.CreateNumber(context, text.Length)'
assert b.count(old)==1
b=b.replace(old,'ExcelFunctionUtilities.CreateNumber(context, ExcelTextIndexing.Length(text, context.EvaluationContext.Workbook.Settings))')
old='FormulaValue.FromText(text.Substring(0, count))';assert b.count(old)==1
b=b.replace(old,'FormulaValue.FromText(ExcelTextIndexing.Prefix(text, count, context.EvaluationContext.Workbook.Settings))')
old='FormulaValue.FromText(text.Substring(text.Length - count, count))';assert b.count(old)==1
b=b.replace(old,'FormulaValue.FromText(ExcelTextIndexing.Suffix(text, count, context.EvaluationContext.Workbook.Settings))')
old='''            var startIndex = start - 1;
            if (startIndex + length > text.Length)
            {
                length = text.Length - startIndex;
            }

            return FormulaValue.FromText(text.Substring(startIndex, length));'''
assert b.count(old)==1
b=b.replace(old,'            return FormulaValue.FromText(ExcelTextIndexing.Middle(text, start, length, context.EvaluationContext.Workbook.Settings));')
s=s[:start]+b+s[end:]
p.write_text(s,encoding='utf-8')
p=Path('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.Extensions.cs');s=p.read_text(encoding='utf-8')
old='        partial void RegisterExtendedDefaults();';assert s.count(old)==1
s=s.replace(old,old+'\n\n        partial void RegisterTextExtensions();')
p.write_text(s,encoding='utf-8')
p=Path('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.cs');s=p.read_text(encoding='utf-8')
old='            RegisterExtendedDefaults();';assert s.count(old)==1
p.write_text(s.replace(old,old+'\n            RegisterTextExtensions();'),encoding='utf-8')
