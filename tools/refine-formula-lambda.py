from pathlib import Path

def edit(path, old, new):
    p = Path(path)
    s = p.read_text(encoding='utf-8')
    assert s.count(old) == 1, (path, s.count(old), old[:80])
    p.write_text(s.replace(old,new),encoding='utf-8')

core='src/ProDataGrid.FormulaEngine/'
excel='src/ProDataGrid.FormulaEngine.Excel/'
edit(core+'FormulaExpressions.cs', '        Invocation,\n        Missing\n', '        Invocation\n')
edit(core+'FormulaExpressions.cs', '''        public FormulaLiteralExpression(FormulaValue value)
            : base(FormulaExpressionKind.Literal)
        {
            Value = value;
        }

        public FormulaValue Value { get; }''', '''        public FormulaLiteralExpression(FormulaValue value)
            : this(value, false)
        {
        }

        /// <summary>Creates a literal with optional syntactic argument-omission metadata.</summary>
        /// <remarks>An omitted literal must contain a blank value. The original one-argument constructor
        /// continues to create ordinary literals, including ordinary blanks.</remarks>
        public FormulaLiteralExpression(FormulaValue value, bool isOmitted)
            : base(FormulaExpressionKind.Literal)
        {
            if (isOmitted && value.Kind != FormulaValueKind.Blank)
                throw new ArgumentException("Only a blank literal may mark an omitted argument.", nameof(value));
            Value = value;
            IsOmitted = isOmitted;
        }

        public FormulaValue Value { get; }

        /// <summary>Gets whether this blank literal represents a syntactically omitted argument.</summary>
        public bool IsOmitted { get; }''')
edit(core+'FormulaInvocationExpression.cs', '''    /// <summary>A syntactically omitted argument, distinct from a reference to a blank cell.</summary>
    /// <remarks>Ordinary functions still receive a blank value. Lambda calls retain omission metadata.</remarks>
    public sealed class FormulaMissingExpression : FormulaExpression
    {
        /// <summary>Creates an omitted argument expression.</summary>
        public FormulaMissingExpression() : base(FormulaExpressionKind.Missing) { }
    }
''', '')
edit(core+'FormulaEvaluationContext.Locals.cs', 'expression is FormulaMissingExpression', 'expression is FormulaLiteralExpression { IsOmitted: true }')
p=Path(excel+'ExcelFormulaParser.cs')
s=p.read_text(encoding='utf-8')
assert s.count('new FormulaMissingExpression()')==2
p.write_text(s.replace('new FormulaMissingExpression()', 'new FormulaLiteralExpression(FormulaValue.Blank, isOmitted: true)'),encoding='utf-8')
edit(excel+'ExcelFormulaFormatter.cs', '''                case FormulaExpressionKind.Missing:
                    return string.Empty;
''', '')
edit(excel+'ExcelFormulaFormatter.cs', '                    return FormatLiteral(((FormulaLiteralExpression)expression).Value, options);', '''                    var literal = (FormulaLiteralExpression)expression;
                    return literal.IsOmitted ? string.Empty : FormatLiteral(literal.Value, options);''')
edit(core+'FormulaEvaluation.cs', '''                case FormulaExpressionKind.Missing:
                    return FormulaValue.Blank;
''','')
edit(core+'FormulaCompiledExpression.cs', '''                    case FormulaExpressionKind.Missing:
                        Emit(new FormulaInstruction(FormulaInstructionKind.Literal, literal: FormulaValue.Blank), push: 1);
                        break;
''','')
