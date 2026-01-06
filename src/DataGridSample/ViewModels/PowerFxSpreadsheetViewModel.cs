using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.DataGridConditionalFormatting;
using DataGridSample.Models;
using DataGridSample.Mvvm;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;

namespace DataGridSample.ViewModels
{
    public class PowerFxSpreadsheetViewModel : ObservableObject
    {
        private static readonly string[] ColumnKeys = { "A", "B", "C", "D", "E", "F" };
        private readonly Dictionary<string, string> _namedRanges = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Inputs", "A1:C3" },
            { "Summary", "D1:F1" },
            { "All", "A:F" }
        };

        private readonly RecalcEngine _engine = new();
        private readonly ParserOptions _parserOptions = new() { Culture = CultureInfo.CurrentCulture };

        public ObservableCollection<PowerFxSheetRow> Rows { get; } = new();

        public IConditionalFormattingModel ConditionalFormatting { get; }

        private PowerFxSheetCell? _selectedCell;
        private PowerFxSheetRow? _selectedRow;
        private string _selectedColumnKey = string.Empty;
        private string _selectedAddress = "No cell";
        private string _selectedDisplayText = string.Empty;
        private bool _selectedHasError;
        private string _selectedErrorMessage = string.Empty;
        private string _selectionRange = "No selection";
        private string _selectionSummary = string.Empty;
        private string _nameBoxText = string.Empty;

        public PowerFxSheetCell? SelectedCell
        {
            get => _selectedCell;
            private set
            {
                if (_selectedCell == value)
                {
                    return;
                }

                if (_selectedCell != null)
                {
                    _selectedCell.PropertyChanged -= OnSelectedCellChanged;
                }

                _selectedCell = value;

                if (_selectedCell != null)
                {
                    _selectedCell.PropertyChanged += OnSelectedCellChanged;
                }

                OnPropertyChanged(nameof(SelectedCell));
            }
        }

        public string SelectedAddress
        {
            get => _selectedAddress;
            private set => SetProperty(ref _selectedAddress, value);
        }

        public string SelectedDisplayText
        {
            get => _selectedDisplayText;
            private set => SetProperty(ref _selectedDisplayText, value);
        }

        public bool SelectedHasError
        {
            get => _selectedHasError;
            private set => SetProperty(ref _selectedHasError, value);
        }

        public string SelectedErrorMessage
        {
            get => _selectedErrorMessage;
            private set => SetProperty(ref _selectedErrorMessage, value);
        }

        public string SelectionRange
        {
            get => _selectionRange;
            private set => SetProperty(ref _selectionRange, value);
        }

        public string SelectionSummary
        {
            get => _selectionSummary;
            private set => SetProperty(ref _selectionSummary, value);
        }

        public string NameBoxText
        {
            get => _nameBoxText;
            set => SetProperty(ref _nameBoxText, value);
        }

        public PowerFxSpreadsheetViewModel()
        {
            Rows.Add(CreateRow(1, "1200", "950", "=A + B", "=Round(C * 1.08, 2)", "=If(D > 2000, \"High\", \"Ok\")", "=Text(D, \"0.00\")"));
            Rows.Add(CreateRow(2, "42", "17", "=A - B", "=If(C < 0, \"Loss\", \"Gain\")", "=Abs(C)", "=Text(Date(2024, 1, 1), \"yyyy-MM-dd\")"));
            Rows.Add(CreateRow(3, "3", "4", "=A * B", "=Round(C / 3, 2)", "=Concatenate(\"C=\", Text(C, \"0\"))", "=Upper(\"fx\")"));
            Rows.Add(CreateRow(4, "100", "0.15", "=A * (1 - B)", "=Round(C, 0)", "=If(B > 0.1, \"Discount\", \"No\")", "Notes"));
            Rows.Add(CreateRow(5, "50", "75", "=A - B", "=Abs(C)", "=If(D > 20, \"Alert\", \"Ok\")", "=Lower(\"POWER FX\")"));
            Rows.Add(CreateRow(6, "10", "0", "=A / B", "=Concatenate(\"A=\", Text(A, \"0\"))", "=Concatenate(\"B=\", Text(B, \"0\"))", "=If(B = 0, \"Fix B\", \"Ok\")"));

            foreach (var row in Rows)
            {
                AttachRow(row);
                EvaluateRow(row);
            }

            UpdateSelectionSummary("No selection", 0, 0, null, null);

            ConditionalFormatting = new ConditionalFormattingModel();
            ConditionalFormatting.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "sheet-error",
                    @operator: ConditionalFormattingOperator.Equals,
                    propertyPath: nameof(PowerFxSheetCell.HasError),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Cell,
                    themeKey: "PowerFxSheetErrorCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "sheet-negative",
                    @operator: ConditionalFormattingOperator.LessThan,
                    propertyPath: nameof(PowerFxSheetCell.NumericValue),
                    value: 0d,
                    valueSource: ConditionalFormattingValueSource.Cell,
                    themeKey: "PowerFxSheetNegativeCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "sheet-positive",
                    @operator: ConditionalFormattingOperator.GreaterThanOrEqual,
                    propertyPath: nameof(PowerFxSheetCell.NumericValue),
                    value: 1000d,
                    valueSource: ConditionalFormattingValueSource.Cell,
                    themeKey: "PowerFxSheetPositiveCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "sheet-formula",
                    @operator: ConditionalFormattingOperator.Equals,
                    propertyPath: nameof(PowerFxSheetCell.IsFormula),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Cell,
                    themeKey: "PowerFxSheetFormulaCellTheme")
            });
        }

        public void UpdateSelectionSummary(string range, int count, int numericCount, double? sum, double? average)
        {
            SelectionRange = range;
            SelectionSummary = BuildSelectionSummary(count, numericCount, sum, average);
            NameBoxText = range == "No selection" ? string.Empty : range;
        }

        public bool TryResolveNamedRange(string name, out string range)
        {
            return _namedRanges.TryGetValue(name, out range);
        }

        public void SelectCell(PowerFxSheetRow? row, string? columnKey)
        {
            if (row == null || string.IsNullOrWhiteSpace(columnKey))
            {
                ClearSelection();
                return;
            }

            var cell = row.GetCell(columnKey);
            if (cell == null)
            {
                ClearSelection();
                return;
            }

            _selectedRow = row;
            _selectedColumnKey = columnKey;
            SelectedCell = cell;
            UpdateSelectionMetadata();
        }

        public void ClearSelection()
        {
            _selectedRow = null;
            _selectedColumnKey = string.Empty;
            SelectedCell = null;
            UpdateSelectionMetadata();
        }

        private static PowerFxSheetRow CreateRow(int rowIndex, params string[] inputs)
        {
            var row = new PowerFxSheetRow(rowIndex);
            row.A.Input = GetInput(inputs, 0);
            row.B.Input = GetInput(inputs, 1);
            row.C.Input = GetInput(inputs, 2);
            row.D.Input = GetInput(inputs, 3);
            row.E.Input = GetInput(inputs, 4);
            row.F.Input = GetInput(inputs, 5);
            return row;
        }

        private static string GetInput(string[] inputs, int index) =>
            index >= 0 && index < inputs.Length ? inputs[index] : string.Empty;

        private void AttachRow(PowerFxSheetRow row)
        {
            foreach (var cell in row.EnumerateCells())
            {
                cell.Cell.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(PowerFxSheetCell.Input))
                    {
                        EvaluateRow(row);
                    }
                };
            }
        }

        private void OnSelectedCellChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(PowerFxSheetCell.DisplayText)
                or nameof(PowerFxSheetCell.HasError)
                or nameof(PowerFxSheetCell.ErrorMessage))
            {
                UpdateSelectionMetadata();
            }
        }

        private void UpdateSelectionMetadata()
        {
            if (_selectedRow == null || SelectedCell == null || string.IsNullOrEmpty(_selectedColumnKey))
            {
                SelectedAddress = "No cell";
                SelectedDisplayText = string.Empty;
                SelectedHasError = false;
                SelectedErrorMessage = string.Empty;
                return;
            }

            SelectedAddress = $"{_selectedColumnKey}{_selectedRow.RowIndex}";
            SelectedDisplayText = SelectedCell.DisplayText;
            SelectedHasError = SelectedCell.HasError;
            SelectedErrorMessage = SelectedCell.ErrorMessage ?? string.Empty;
        }

        private static string BuildSelectionSummary(int count, int numericCount, double? sum, double? average)
        {
            if (count <= 0)
            {
                return string.Empty;
            }

            if (numericCount <= 0)
            {
                return $"Count: {count}";
            }

            var formattedSum = sum?.ToString("0.###", CultureInfo.CurrentCulture) ?? "0";
            var formattedAverage = average?.ToString("0.###", CultureInfo.CurrentCulture) ?? "0";
            return $"Count: {count} | Numeric: {numericCount} | Sum: {formattedSum} | Avg: {formattedAverage}";
        }

        private void EvaluateRow(PowerFxSheetRow row)
        {
            var values = new Dictionary<string, FormulaValue>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in ColumnKeys)
            {
                values[key] = FormulaValue.New(0d);
            }

            var results = new Dictionary<string, CellEvaluation>(StringComparer.OrdinalIgnoreCase);

            for (var pass = 0; pass < 2; pass++)
            {
                results.Clear();
                foreach (var (key, cell) in row.EnumerateCells())
                {
                    var evaluation = EvaluateCell(cell, values);
                    values[key] = evaluation.Value;
                    results[key] = evaluation;
                }
            }

            foreach (var (key, cell) in row.EnumerateCells())
            {
                if (results.TryGetValue(key, out var evaluation))
                {
                    ApplyEvaluation(cell, evaluation);
                }
            }
        }

        private CellEvaluation EvaluateCell(PowerFxSheetCell cell, IReadOnlyDictionary<string, FormulaValue> values)
        {
            var rawInput = cell.Input ?? string.Empty;
            var trimmed = rawInput.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return new CellEvaluation(FormulaValue.New(0d), string.Empty, null, false, null, false);
            }

            if (trimmed.StartsWith("=", StringComparison.Ordinal))
            {
                var expression = trimmed.Substring(1);
                if (string.IsNullOrWhiteSpace(expression))
                {
                    return new CellEvaluation(FormulaValue.New(0d), string.Empty, null, false, null, true);
                }

                try
                {
                    var fields = values.Select(pair => new NamedValue(pair.Key, pair.Value)).ToArray();
                    var record = FormulaValue.NewRecordFromFields(fields);
                    var result = _engine.Eval(expression, record, _parserOptions);

                    if (result is ErrorValue error)
                    {
                        var message = error.Errors.FirstOrDefault()?.Message ?? "Invalid formula.";
                        return new CellEvaluation(result, "#ERROR", null, true, message, true);
                    }

                    var displayText = FormatFormulaValue(result, out var number);
                    return new CellEvaluation(result, displayText, number, false, null, true);
                }
                catch (Exception ex)
                {
                    return new CellEvaluation(FormulaValue.New(0d), "#ERROR", null, true, ex.Message, true);
                }
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out var numberValue))
            {
                var formatted = numberValue.ToString("0.###", CultureInfo.CurrentCulture);
                return new CellEvaluation(FormulaValue.New(numberValue), formatted, numberValue, false, null, false);
            }

            return new CellEvaluation(FormulaValue.New(rawInput), rawInput, null, false, null, false);
        }

        private static void ApplyEvaluation(PowerFxSheetCell cell, CellEvaluation evaluation)
        {
            cell.DisplayText = evaluation.DisplayText;
            cell.NumericValue = evaluation.NumericValue;
            cell.HasError = evaluation.HasError;
            cell.ErrorMessage = evaluation.ErrorMessage;
            cell.IsFormula = evaluation.IsFormula;
        }

        private static string FormatFormulaValue(FormulaValue value, out double? number)
        {
            switch (value)
            {
                case NumberValue numberValue:
                    number = numberValue.Value;
                    return numberValue.Value.ToString("0.###", CultureInfo.CurrentCulture);
                case DecimalValue decimalValue:
                    number = (double)decimalValue.Value;
                    return decimalValue.Value.ToString("0.###", CultureInfo.CurrentCulture);
                case BooleanValue booleanValue:
                    number = null;
                    return booleanValue.Value ? "true" : "false";
                case StringValue stringValue:
                    number = null;
                    return stringValue.Value ?? string.Empty;
                case DateTimeValue dateTimeValue:
                    number = null;
                    return dateTimeValue.Value.ToString("g", CultureInfo.CurrentCulture);
                case DateValue dateValue:
                    number = null;
                    return dateValue.Value.ToString("d", CultureInfo.CurrentCulture);
                case TimeValue timeValue:
                    number = null;
                    return timeValue.Value.ToString("g", CultureInfo.CurrentCulture);
                case BlankValue:
                    number = null;
                    return string.Empty;
                default:
                    number = null;
                    return value.ToString();
            }
        }

        private readonly struct CellEvaluation
        {
            public CellEvaluation(
                FormulaValue value,
                string displayText,
                double? numericValue,
                bool hasError,
                string? errorMessage,
                bool isFormula)
            {
                Value = value;
                DisplayText = displayText;
                NumericValue = numericValue;
                HasError = hasError;
                ErrorMessage = errorMessage;
                IsFormula = isFormula;
            }

            public FormulaValue Value { get; }

            public string DisplayText { get; }

            public double? NumericValue { get; }

            public bool HasError { get; }

            public string? ErrorMessage { get; }

            public bool IsFormula { get; }
        }
    }
}
