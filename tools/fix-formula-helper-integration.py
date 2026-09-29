from pathlib import Path

p=Path('src/ProDataGrid.FormulaEngine.Excel/ExcelFunctionRegistry.cs')
s=p.read_text(encoding='utf-8')
a=s.index('    internal sealed class CountFunction :')
b=s.index('    internal sealed class CountIfFunction :',a)
block=s[a:b]
for variable,indent in [('element',24),('arg',16),('value',16)]:
    pad=' '*indent
    old=pad+f'if ({variable}.Kind == FormulaValueKind.Error)\n'+pad+'{\n'+pad+f'    return {variable};\n'+pad+'}\n\n'
    assert block.count(old)==2,(variable,block.count(old))
    block=block.replace(old,'')
s=s[:a]+block+s[b:]
p.write_text(s,encoding='utf-8')

p=Path('src/ProDataGrid.FormulaEngine/WorkbookValueResolver.cs')
s=p.read_text(encoding='utf-8')
a=s.index('        private FormulaValue EvaluateCell(')
b=s.index('        private readonly struct FormulaEvaluationKey',a)
block=s[a:b]
assert block.count('return cell.Value;')==1
assert block.count('? cell.Value')==1
assert block.count('return value;')==1
block=block.replace('return cell.Value;','return ReadCellValue(cell.Value);')
block=block.replace('? cell.Value','? ReadCellValue(cell.Value)')
block=block.replace('return value;','return ReadCellValue(value);')
block+='''        private static FormulaValue ReadCellValue(FormulaValue value)
        {
            // A worksheet reference reads one cell, not the complete array retained by
            // a spill anchor. Returning the whole array nests it in range results and
            // causes streaming aggregates to omit the anchor or count it multiple times.
            if (value.Kind == FormulaValueKind.Array)
            {
                var array = value.AsArray();
                value = array.IsPresent(0, 0) ? array[0, 0] : FormulaValue.Blank;
                if (value.Kind == FormulaValueKind.Array)
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
            }
            return value.Kind == FormulaValueKind.Lambda ? value.ToCellResult() : value;
        }

'''
s=s[:a]+block+s[b:]
p.write_text(s,encoding='utf-8')

p=Path('src/ProDataGrid.FormulaEngine.UnitTests/FormulaLexicalBoundaryTests.cs')
s=p.read_text(encoding='utf-8')
assert s.count('new bool[0]')==1
p.write_text(s.replace('new bool[0]','Array.Empty<bool>()'),encoding='utf-8')
