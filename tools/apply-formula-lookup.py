from pathlib import Path

file = Path('src/ProDataGrid.FormulaEngine.Excel/ExcelLookupFunctions.cs')
text = file.read_text(encoding='utf-8')
old = 'internal static class ExcelLookupUtilities'
assert text.count(old) == 1
text = text.replace(old, 'internal static partial class ExcelLookupUtilities')
start = text.index('        public static bool TryFindXLookupIndex(')
end = text.index('        public static FormulaValue GetReturnValue(', start)
text = text[:start] + text[end:]
file.write_text(text, encoding='utf-8')

file = Path('src/ProDataGrid.FormulaEngine.Excel/ExcelDynamicArrayFunctions.cs')
text = file.read_text(encoding='utf-8')
for axis in ('Rows', 'Columns'):
    start = text.index(f'        public static bool TryBuildUnique{axis}(')
    end = text.index('        public static bool TryBuildUniqueColumns(' if axis == 'Rows' else '        private static bool TryRowsEqual(', start + 1)
    block = text[start:end]
    variable = 'unique' + axis
    marker = f'            error = default;\n            {variable} = new List<int>();'
    assert block.count(marker) == 1
    by_column = 'false' if axis == 'Rows' else 'true'
    fast = f'''            if (ExcelUniqueIndexBuilder.TryBuild(settings, array, {by_column}, exactlyOnce, out {variable}))
            {{
                error = {variable}.Count == 0 ? new FormulaError(FormulaErrorType.Calc) : default;
                return {variable}.Count > 0;
            }}

'''
    block = block.replace(marker, fast + marker)
    position = block.rindex('            return true;')
    block = block[:position] + f'''            if ({variable}.Count == 0)
            {{
                error = new FormulaError(FormulaErrorType.Calc);
                return false;
            }}

''' + block[position:]
    text = text[:start] + block + text[end:]

for axis in ('Rows', 'Columns'):
    start = text.index(f'        public static bool TrySort{axis}(')
    end = text.index('        public static bool TrySortColumns(' if axis == 'Rows' else '        private static bool TryCompareForSort(', start + 1)
    block = text[start:end]
    marker = '                return 0;\n            });'
    assert block.count(marker) == 1
    block = block.replace(marker, '                // Preserve source order for equal sort keys.\n                return left.CompareTo(right);\n            });')
    text = text[:start] + block + text[end:]
file.write_text(text, encoding='utf-8')
