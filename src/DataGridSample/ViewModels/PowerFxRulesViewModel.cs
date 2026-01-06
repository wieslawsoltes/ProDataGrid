using System;
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
    public class PowerFxRulesViewModel : ObservableObject
    {
        private readonly RecalcEngine _engine = new();
        private readonly ParserOptions _parserOptions = new() { Culture = CultureInfo.CurrentCulture };

        public ObservableCollection<PowerFxRuleRow> Rows { get; } = new();

        public IConditionalFormattingModel ConditionalFormatting { get; }

        private string _formula = "Revenue - Cost";
        private string _ruleFormula = "Result < 0 || Revenue < Target";

        public string Formula
        {
            get => _formula;
            set
            {
                if (SetProperty(ref _formula, value))
                {
                    EvaluateAll();
                }
            }
        }

        public string RuleFormula
        {
            get => _ruleFormula;
            set
            {
                if (SetProperty(ref _ruleFormula, value))
                {
                    EvaluateAll();
                }
            }
        }

        public PowerFxRulesViewModel()
        {
            Rows.Add(new PowerFxRuleRow { Item = "North", Units = 120, Revenue = 2400, Cost = 1800, Target = 2100 });
            Rows.Add(new PowerFxRuleRow { Item = "South", Units = 90, Revenue = 1400, Cost = 1500, Target = 1600 });
            Rows.Add(new PowerFxRuleRow { Item = "East", Units = 160, Revenue = 3200, Cost = 2100, Target = 3000 });
            Rows.Add(new PowerFxRuleRow { Item = "West", Units = 70, Revenue = 1100, Cost = 950, Target = 1200 });
            Rows.Add(new PowerFxRuleRow { Item = "Central", Units = 130, Revenue = 2600, Cost = 1950, Target = 2400 });

            foreach (var row in Rows)
            {
                row.PropertyChanged += OnRowPropertyChanged;
            }

            EvaluateAll();

            ConditionalFormatting = new ConditionalFormattingModel();
            ConditionalFormatting.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "result-error",
                    @operator: ConditionalFormattingOperator.Equals,
                    columnId: nameof(PowerFxRuleRow.ResultText),
                    propertyPath: nameof(PowerFxRuleRow.HasError),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxRuleErrorCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "result-alert",
                    @operator: ConditionalFormattingOperator.Equals,
                    columnId: nameof(PowerFxRuleRow.ResultText),
                    propertyPath: nameof(PowerFxRuleRow.RuleHit),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxRuleAlertCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "status-error",
                    @operator: ConditionalFormattingOperator.Equals,
                    columnId: nameof(PowerFxRuleRow.Status),
                    propertyPath: nameof(PowerFxRuleRow.HasError),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxRuleErrorCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "status-alert",
                    @operator: ConditionalFormattingOperator.Equals,
                    columnId: nameof(PowerFxRuleRow.Status),
                    propertyPath: nameof(PowerFxRuleRow.RuleHit),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxRuleAlertCellTheme")
            });
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PowerFxRuleRow row)
            {
                return;
            }

            if (e.PropertyName is nameof(PowerFxRuleRow.ResultText)
                or nameof(PowerFxRuleRow.ResultNumber)
                or nameof(PowerFxRuleRow.RuleHit)
                or nameof(PowerFxRuleRow.HasError)
                or nameof(PowerFxRuleRow.ErrorMessage)
                or nameof(PowerFxRuleRow.Status))
            {
                return;
            }

            EvaluateRow(row);
        }

        private void EvaluateAll()
        {
            foreach (var row in Rows)
            {
                EvaluateRow(row);
            }
        }

        private void EvaluateRow(PowerFxRuleRow row)
        {
            if (string.IsNullOrWhiteSpace(Formula))
            {
                ApplyResult(row, FormulaValue.New(string.Empty), isError: false, errorMessage: null);
                ApplyRule(row, false, null);
                return;
            }

            try
            {
                var record = CreateRecord(row);
                var result = _engine.Eval(Formula, record, _parserOptions);

                if (result is ErrorValue error)
                {
                    var message = error.Errors.FirstOrDefault()?.Message ?? "Invalid formula.";
                    ApplyResult(row, result, isError: true, errorMessage: message);
                    ApplyRule(row, false, message);
                    return;
                }

                ApplyResult(row, result, isError: false, errorMessage: null);

                var ruleHit = EvaluateRule(row, result, out var ruleError);
                ApplyRule(row, ruleHit, ruleError);
            }
            catch (Exception ex)
            {
                ApplyResult(row, FormulaValue.New(string.Empty), isError: true, errorMessage: ex.Message);
                ApplyRule(row, false, ex.Message);
            }
        }

        private bool EvaluateRule(PowerFxRuleRow row, FormulaValue result, out string? ruleError)
        {
            ruleError = null;

            if (string.IsNullOrWhiteSpace(RuleFormula))
            {
                return false;
            }

            try
            {
                var record = CreateRecord(row, result);
                var ruleResult = _engine.Eval(RuleFormula, record, _parserOptions);

                if (ruleResult is ErrorValue error)
                {
                    ruleError = error.Errors.FirstOrDefault()?.Message ?? "Invalid rule.";
                    return false;
                }

                return TryReadBoolean(ruleResult, out var hit) && hit;
            }
            catch (Exception ex)
            {
                ruleError = ex.Message;
                return false;
            }
        }

        private static RecordValue CreateRecord(PowerFxRuleRow row, FormulaValue? result = null)
        {
            var fields = new[]
            {
                new NamedValue(nameof(PowerFxRuleRow.Item), FormulaValue.New(row.Item)),
                new NamedValue(nameof(PowerFxRuleRow.Units), FormulaValue.New(row.Units)),
                new NamedValue(nameof(PowerFxRuleRow.Revenue), FormulaValue.New(row.Revenue)),
                new NamedValue(nameof(PowerFxRuleRow.Cost), FormulaValue.New(row.Cost)),
                new NamedValue(nameof(PowerFxRuleRow.Target), FormulaValue.New(row.Target)),
                new NamedValue("Result", result ?? FormulaValue.New(string.Empty))
            };

            return FormulaValue.NewRecordFromFields(fields);
        }

        private static void ApplyResult(PowerFxRuleRow row, FormulaValue result, bool isError, string? errorMessage)
        {
            row.ErrorMessage = errorMessage;
            row.HasError = isError;

            if (isError)
            {
                row.ResultText = "#ERROR";
                row.ResultNumber = null;
                row.Status = "Error";
                return;
            }

            row.ResultText = FormatFormulaValue(result, out var number);
            row.ResultNumber = number;
            row.Status = string.Empty;
        }

        private static void ApplyRule(PowerFxRuleRow row, bool ruleHit, string? ruleError)
        {
            if (!string.IsNullOrEmpty(ruleError))
            {
                row.HasError = true;
                row.ErrorMessage = ruleError;
                row.RuleHit = false;
                row.Status = "Error";
                return;
            }

            row.RuleHit = ruleHit;
            row.Status = ruleHit ? "Alert" : "Ok";
        }

        private static bool TryReadBoolean(FormulaValue value, out bool result)
        {
            switch (value)
            {
                case BooleanValue booleanValue:
                    result = booleanValue.Value;
                    return true;
                case NumberValue numberValue:
                    result = Math.Abs(numberValue.Value) > double.Epsilon;
                    return true;
                case DecimalValue decimalValue:
                    result = decimalValue.Value != 0m;
                    return true;
                case StringValue stringValue when bool.TryParse(stringValue.Value, out var parsed):
                    result = parsed;
                    return true;
                default:
                    result = false;
                    return false;
            }
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
    }
}
