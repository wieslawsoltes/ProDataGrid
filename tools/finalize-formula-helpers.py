from pathlib import Path
p = Path('src/ProDataGrid.FormulaEngine.Excel/ExcelLambdaHelperUtilities.cs')
s = p.read_text(encoding='utf-8')
old = '''            if (value.Kind == FormulaValueKind.Lambda)
            {
                array = null!;
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }
'''
assert s.count(old) == 1
s = s.replace(old, '')
s = s.replace('''            // A single-cell result can be materialized; multi-cell or callable results would
            // create nested arrays, which the helper's rectangular result cannot represent.''', '''            // Single-cell arrays are materialized. A closure is a scalar callable payload,
            // not a nested array: keep it for composition and defer worksheet rejection to
            // ToCellResult(). Immutable invocation frames preserve each captured argument.''')
s = s.replace(' && value.Kind != FormulaValueKind.Lambda', '')
p.write_text(s, encoding='utf-8')
p = Path('src/ProDataGrid.FormulaEngine.UnitTests/ExcelLambdaHelperTests.cs')
s = p.read_text(encoding='utf-8')
old = '        [InlineData("MAP({1;2},LAMBDA(x,LAMBDA(y,x+y)))", "#CALC!")]\n'
assert s.count(old) == 1
# This assertion is strengthened in ExcelCallableArrayTests to check both intermediate
# composition and rejection at the real worksheet publication boundary.
p.write_text(s.replace(old, ''), encoding='utf-8')
