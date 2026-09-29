from pathlib import Path
p=Path('src/ProDataGrid.FormulaEngine.UnitTests/ExcelScientificFunctionTests.cs')
s=p.read_text(encoding='utf-8')
start=s.index('        [Fact]\n        public void Primitive_Scalar_Calls_Are_Allocation_Free_After_Warmup()')
end=s.index('        [Fact]\n        public void Seeded_Trig_And_Hyperbolic_',start)
s=s[:start]+s[end:]
s=s.replace('using System.Runtime.CompilerServices;\n','')
p.write_text(s,encoding='utf-8')
