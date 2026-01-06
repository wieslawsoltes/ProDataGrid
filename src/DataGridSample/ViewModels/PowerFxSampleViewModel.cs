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
    public class PowerFxSampleViewModel : ObservableObject
    {
        private readonly RecalcEngine _engine = new();
        private readonly ParserOptions _parserOptions = new();

        public ObservableCollection<PowerFxRow> Rows { get; } = new();

        public IConditionalFormattingModel ConditionalFormatting { get; }

        public PowerFxSampleViewModel()
        {
            Rows.Add(new PowerFxRow
            {
                Name = "Hardware",
                Quantity = 3,
                UnitPrice = 120,
                Discount = 0.05,
                Tax = 7.5,
                Formula = "Quantity * UnitPrice * (1 - Discount) + Tax"
            });
            Rows.Add(new PowerFxRow
            {
                Name = "Service",
                Quantity = 2,
                UnitPrice = 75,
                Discount = 0.15,
                Tax = 0,
                Formula = "If(Discount > 0.1, Quantity * UnitPrice * 0.9, Quantity * UnitPrice)"
            });
            Rows.Add(new PowerFxRow
            {
                Name = "Subscription",
                Quantity = 12,
                UnitPrice = 19.99,
                Discount = 0,
                Tax = 0,
                Formula = "Round(Quantity * UnitPrice, 2)"
            });
            Rows.Add(new PowerFxRow
            {
                Name = "Bad Formula",
                Quantity = 1,
                UnitPrice = 10,
                Discount = 0,
                Tax = 0,
                Formula = "Quantity * MissingField"
            });

            foreach (var row in Rows)
            {
                row.PropertyChanged += OnRowPropertyChanged;
                EvaluateRow(row);
            }

            ConditionalFormatting = new ConditionalFormattingModel();
            ConditionalFormatting.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "formula-error",
                    @operator: ConditionalFormattingOperator.Equals,
                    columnId: nameof(PowerFxRow.Formula),
                    propertyPath: nameof(PowerFxRow.HasError),
                    value: true,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxFormulaErrorCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "result-positive",
                    @operator: ConditionalFormattingOperator.GreaterThan,
                    columnId: nameof(PowerFxRow.ResultText),
                    propertyPath: nameof(PowerFxRow.ResultNumber),
                    value: 0d,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxResultPositiveCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "result-negative",
                    @operator: ConditionalFormattingOperator.LessThan,
                    columnId: nameof(PowerFxRow.ResultText),
                    propertyPath: nameof(PowerFxRow.ResultNumber),
                    value: 0d,
                    valueSource: ConditionalFormattingValueSource.Item,
                    themeKey: "PowerFxResultNegativeCellTheme")
            });
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PowerFxRow row)
            {
                return;
            }

            if (e.PropertyName is nameof(PowerFxRow.ResultText)
                or nameof(PowerFxRow.ResultNumber)
                or nameof(PowerFxRow.ErrorMessage)
                or nameof(PowerFxRow.HasError))
            {
                return;
            }

            EvaluateRow(row);
        }

        private void EvaluateRow(PowerFxRow row)
        {
            if (string.IsNullOrWhiteSpace(row.Formula))
            {
                row.ResultText = string.Empty;
                row.ResultNumber = null;
                row.ErrorMessage = null;
                row.HasError = false;
                row.SetError(nameof(PowerFxRow.Formula), null);
                return;
            }

            try
            {
                var record = FormulaValue.NewRecordFromFields(new[]
                {
                    new NamedValue(nameof(PowerFxRow.Name), FormulaValue.New(row.Name)),
                    new NamedValue(nameof(PowerFxRow.Quantity), FormulaValue.New(row.Quantity)),
                    new NamedValue(nameof(PowerFxRow.UnitPrice), FormulaValue.New(row.UnitPrice)),
                    new NamedValue(nameof(PowerFxRow.Discount), FormulaValue.New(row.Discount)),
                    new NamedValue(nameof(PowerFxRow.Tax), FormulaValue.New(row.Tax))
                });

                var result = _engine.Eval(row.Formula, record, _parserOptions);
                ApplyResult(row, result);
            }
            catch (Exception ex)
            {
                ApplyError(row, ex.Message);
            }
        }

        private static void ApplyResult(PowerFxRow row, FormulaValue result)
        {
            if (result is ErrorValue error)
            {
                var message = error.Errors.FirstOrDefault()?.Message ?? "Invalid formula.";
                ApplyError(row, message);
                return;
            }

            row.ErrorMessage = null;
            row.HasError = false;
            row.SetError(nameof(PowerFxRow.Formula), null);

            row.ResultText = FormatFormulaValue(result, out var number);
            row.ResultNumber = number;
        }

        private static void ApplyError(PowerFxRow row, string message)
        {
            row.ResultText = "#ERROR";
            row.ResultNumber = null;
            row.ErrorMessage = message;
            row.HasError = true;
            row.SetError(nameof(PowerFxRow.Formula), message);
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
