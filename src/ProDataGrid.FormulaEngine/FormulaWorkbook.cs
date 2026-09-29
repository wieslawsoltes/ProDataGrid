// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProDataGrid.FormulaEngine
{
    public enum FormulaDateSystem
    {
        Windows1900,
        Mac1904
    }

    public enum FormulaCalculationMode
    {
        Automatic,
        Manual
    }

    public sealed partial class FormulaCalculationSettings
    {
        public FormulaReferenceMode ReferenceMode { get; set; } = FormulaReferenceMode.A1;

        public FormulaDateSystem DateSystem { get; set; } = FormulaDateSystem.Windows1900;

        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

        public FormulaCalculationMode CalculationMode { get; set; } = FormulaCalculationMode.Automatic;

        public bool UseExcelNumberParsing { get; set; } = true;

        public bool ApplyNumberPrecision { get; set; } = true;

        public int NumberPrecisionDigits { get; set; } = 15;

        public bool EnableDynamicArrays { get; set; } = true;

        public bool EnableIterativeCalculation { get; set; } = false;

        public bool EnableCompiledExpressions { get; set; } = true;

        public bool EnableParallelCalculation { get; set; } = false;

        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        public int IterativeMaxIterations { get; set; } = 100;

        public double IterativeTolerance { get; set; } = 0.0001d;

        public IFormulaCalculationObserver? CalculationObserver { get; set; }

        public FormulaParseOptions CreateParseOptions(FormulaReferenceMode? referenceMode = null)
        {
            var culture = Culture ?? CultureInfo.InvariantCulture;
            var listSeparator = culture.TextInfo.ListSeparator;
            var argumentSeparator = string.IsNullOrEmpty(listSeparator) ? ',' : listSeparator[0];
            var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
            var decimalChar = string.IsNullOrEmpty(decimalSeparator) ? '.' : decimalSeparator[0];

            return new FormulaParseOptions
            {
                ReferenceMode = referenceMode ?? ReferenceMode,
                ArgumentSeparator = argumentSeparator,
                DecimalSeparator = decimalChar,
                AllowLeadingEquals = true
            };
        }
    }

    public interface IFormulaCalculationModeProvider
    {
        FormulaCalculationMode CalculationMode { get; }
    }

    public interface IFormulaWorkbook
    {
        string Name { get; }

        IReadOnlyList<IFormulaWorksheet> Worksheets { get; }

        FormulaCalculationSettings Settings { get; }

        IFormulaWorksheet GetWorksheet(string name);
    }

    public interface IFormulaWorkbookResolver
    {
        bool TryGetWorkbook(string name, out IFormulaWorkbook workbook);
    }

    public interface IFormulaWorksheet
    {
        string Name { get; }

        IFormulaWorkbook Workbook { get; }

        IFormulaCell GetCell(int row, int column);

        bool TryGetCell(int row, int column, out IFormulaCell cell);
    }

    public interface IFormulaCell
    {
        FormulaCellAddress Address { get; }

        string? Formula { get; set; }

        FormulaExpression? Expression { get; set; }

        FormulaValue Value { get; set; }
    }

    public sealed partial class FormulaEvaluationContext
    {
        public FormulaEvaluationContext(
            IFormulaWorkbook workbook,
            IFormulaWorksheet worksheet,
            FormulaCellAddress address,
            IFormulaFunctionRegistry functionRegistry)
        {
            Workbook = workbook;
            Worksheet = worksheet;
            Address = address;
            FunctionRegistry = functionRegistry;
        }

        public IFormulaWorkbook Workbook { get; }

        public IFormulaWorksheet Worksheet { get; }

        public FormulaCellAddress Address { get; }

        public IFormulaFunctionRegistry FunctionRegistry { get; }
    }
}
