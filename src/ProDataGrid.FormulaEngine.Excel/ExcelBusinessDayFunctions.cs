// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal class BusinessDayFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        private readonly bool _networkDays;
        private readonly bool _international;

        public BusinessDayFunction(bool networkDays, bool international)
            : base((networkDays ? "NETWORKDAYS" : "WORKDAY") + (international ? ".INTL" : ""),
                new FormulaFunctionInfo(2, international ? 4 : 3))
        {
            _networkDays = networkDays;
            _international = international;
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) => Apply(context, args, 0);

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < 2 || args.Count > Info.MaxArgs) return ExcelTextUtilities.ValueError();
            var values = new FormulaValue[args.Count];
            uint omitted = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (context.EvaluationContext.IsArgumentOmitted(args[i])) omitted |= 1u << i;
                values[i] = evaluator.Evaluate(args[i], context.EvaluationContext, resolver);
            }
            return Apply(context, values, omitted);
        }

        private FormulaValue Apply(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args, uint omitted)
        {
            if (args.Count < 2 || args.Count > Info.MaxArgs) return ExcelTextUtilities.ValueError();
            var controls = Math.Min(args.Count, _international ? 3 : 2);
            var rows = 1;
            var columns = 1;
            var arrayResult = false;
            for (var i = 0; i < controls; i++)
            {
                if (args[i].Kind != FormulaValueKind.Array) continue;
                arrayResult = true;
                var array = args[i].AsArray();
                rows = Math.Max(rows, array.RowCount);
                columns = Math.Max(columns, array.ColumnCount);
            }
            for (var i = 0; i < controls; i++)
            {
                if (args[i].Kind != FormulaValueKind.Array) continue;
                var array = args[i].AsArray();
                if ((array.RowCount != 1 && array.RowCount != rows) || (array.ColumnCount != 1 && array.ColumnCount != columns))
                    return ExcelTextUtilities.ValueError();
            }
            var settings = context.EvaluationContext.Workbook.Settings;
            if (rows > 1048576 || columns > 16384 || (long)rows * columns > settings.MaximumArrayCellCount)
                return ExcelNumericUtilities.NumError();
            var holidayArgument = _international ? 3 : 2;
            var source = new ExcelArrayOperand(holidayArgument < args.Count ? args[holidayArgument] : FormulaValue.Blank);
            if (source.IsUnresolvedReference) return ExcelTextUtilities.ValueError();
            if (source.Length > settings.MaximumArrayCellCount) return ExcelNumericUtilities.NumError();
            var holidays = new ExcelHolidayIndex((int)source.Length, stackalloc int[128]);
            try
            {
                if (!holidays.TryLoad(source, settings, out var error)) return FormulaValue.FromError(error);
                if (!arrayResult) return EvaluateScalar(context, new ExcelScalarArguments(args, omitted), holidays);
                if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out error)) return FormulaValue.FromError(error);
                for (var row = 0; row < rows; row++) for (var col = 0; col < columns; col++)
                    result[row, col] = EvaluateScalar(context, new ExcelScalarArguments(args, omitted, row, col), holidays);
                return FormulaValue.FromArray(result);
            }
            finally { holidays.Dispose(); }
        }

        private FormulaValue EvaluateScalar(FormulaFunctionContext context, in ExcelScalarArguments args, in ExcelHolidayIndex holidays)
        {
            var settings = context.EvaluationContext.Workbook.Settings;
            if (!ExcelBusinessCalendarMath.TryDate(args[0], settings, out var start, out var error)) return FormulaValue.FromError(error);
            int endOrDays;
            if (_networkDays)
            {
                if (!ExcelBusinessCalendarMath.TryDate(args[1], settings, out endOrDays, out error)) return FormulaValue.FromError(error);
            }
            else if (!ExcelBusinessCalendarMath.TryOffset(args[1], settings, out endOrDays, out error)) return FormulaValue.FromError(error);
            var weekend = 96;
            if (_international && !args.IsOmitted(2) && !ExcelBusinessCalendarMath.TryWeekend(args[2], settings, out weekend, out error))
                return FormulaValue.FromError(error);
            if (_networkDays)
            {
                var reverse = endOrDays < start;
                var count = ExcelBusinessCalendarMath.Count(Math.Min(start, endOrDays), Math.Max(start, endOrDays), weekend, settings.DateSystem, holidays);
                return ExcelFunctionUtilities.CreateNumber(settings, reverse ? -count : count);
            }
            if (weekend == 127 || !ExcelBusinessCalendarMath.TryOffsetDate(start, endOrDays, weekend, settings.DateSystem, holidays, out var date))
                return ExcelNumericUtilities.NumError();
            return ExcelFunctionUtilities.CreateNumber(settings, date);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterBusinessCalendarFunctions()
        {
            Register(new BusinessDayFunction(networkDays: false, international: true));
            Register(new BusinessDayFunction(networkDays: true, international: true));
        }
    }
}
