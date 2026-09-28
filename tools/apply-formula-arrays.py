from pathlib import Path

def edit(path, old, new):
    file = Path(path)
    text = file.read_text(encoding='utf-8')
    assert text.count(old) == 1, (path, text.count(old), old[:100])
    file.write_text(text.replace(old, new), encoding='utf-8')

edit('src/ProDataGrid.FormulaEngine/FormulaWorkbook.cs',
     'public sealed class FormulaCalculationSettings', 'public sealed partial class FormulaCalculationSettings')
edit('src/ProDataGrid.FormulaEngine.Excel/ExcelDynamicArrayFunctions.cs',
     '            var array = new FormulaArray(rows, columns);',
     '''            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var array, out error))
            {
                return FormulaValue.FromError(error);
            }''')
