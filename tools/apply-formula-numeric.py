from pathlib import Path

file = Path('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs')
text = file.read_text(encoding='utf-8')
old = 'public sealed class FormulaEvaluator'
assert text.count(old) == 1
text = text.replace(old, 'public sealed partial class FormulaEvaluator')
start = text.index('        private FormulaValue EvaluateBinary(\n')
end = text.index('        private FormulaValue EvaluateReferenceOperator(\n', start)
block = text[start:end]
switch = block.index('            return expression.Operator switch')
block = block[:switch] + '            return EvaluateBinaryScalar(expression.Operator, left, right, context);\n        }\n\n'
text = text[:start] + block + text[end:]
start = text.index('        private static FormulaValue EvaluateBinaryScalar(')
end = text.index('        private static FormulaValue CreateNumber(', start)
block = text[start:end]
block = block.replace('            FormulaValue left,\n            FormulaValue right,',
                      '            in FormulaValue left,\n            in FormulaValue right,')
marker = '            return op switch\n'
assert block.count(marker) == 1
block = block.replace(marker, '''            if (left.Kind == FormulaValueKind.Number && right.Kind == FormulaValueKind.Number &&
                op != FormulaBinaryOperator.Concat)
            {
                return EvaluateNumberBinary(op, left.AsNumber(), right.AsNumber(), context.Workbook.Settings);
            }

''' + marker)
text = text[:start] + block + text[end:]
file.write_text(text, encoding='utf-8')

file = Path('tools/formula-benchmarks/Program.cs')
text = file.read_text(encoding='utf-8')
marker = '            Measure(output, "xmatch_binary_100000",'
assert text.count(marker) == 1
position = text.index(marker)
text = text[:position] + '''            workbook.Settings.ApplyNumberPrecision = true;
            Measure(output, "cached_128_additions_precision", 1000, () => evaluator.Evaluate(arithmetic, context, resolver).AsNumber(), 129);
            workbook.Settings.ApplyNumberPrecision = false;
            var numbers = new FormulaArray(10000, 1);
            for (var i = 0; i < numbers.RowCount; i++) numbers[i, 0] = FormulaValue.FromNumber(i);
            var arrayAddition = new FormulaBinaryExpression(FormulaBinaryOperator.Add,
                new FormulaLiteralExpression(FormulaValue.FromArray(numbers)), new FormulaLiteralExpression(FormulaValue.FromNumber(3)));
            Measure(output, "array_10000_additions", 5, () => evaluator.Evaluate(arrayAddition, context, resolver).AsArray()[9999, 0].AsNumber(), 10002);
''' + text[position:]
file.write_text(text, encoding='utf-8')
