// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelScientificOperation
    {
        Sin, Cos, Tan, Cot, Sec, Csc, Asin, Acos, Atan, Acot,
        Sinh, Cosh, Tanh, Coth, Sech, Csch, Asinh, Acosh, Atanh, Acoth,
        Degrees, Radians, SqrtPi
    }

    internal sealed class ScientificFunction : ExcelScalarFunctionBase
    {
        private readonly ExcelScientificOperation _operation;
        public ScientificFunction(string name, ExcelScientificOperation operation) : base(name, 1, 1)
        {
            _operation = operation;
        }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelNumericUtilities.TryFinite(context, args[0], out var x, out var error))
                return FormulaValue.FromError(error);
            // The six reciprocal trig/hyperbolic functions specify this input bound.
            if ((_operation is ExcelScientificOperation.Cot or ExcelScientificOperation.Sec or ExcelScientificOperation.Csc
                or ExcelScientificOperation.Coth or ExcelScientificOperation.Sech or ExcelScientificOperation.Csch)
                && Math.Abs(x) >= 134217728d)
                return ExcelNumericUtilities.NumError();
            double result;
            switch (_operation)
            {
                case ExcelScientificOperation.Sin: result = Math.Sin(x); break;
                case ExcelScientificOperation.Cos: result = Math.Cos(x); break;
                case ExcelScientificOperation.Tan: result = Math.Tan(x); break;
                case ExcelScientificOperation.Cot:
                    if (x == 0) return ExcelNumericUtilities.DivisionError();
                    result = 1 / Math.Tan(x); break;
                case ExcelScientificOperation.Sec: result = 1 / Math.Cos(x); break;
                case ExcelScientificOperation.Csc:
                    if (x == 0) return ExcelNumericUtilities.DivisionError();
                    result = 1 / Math.Sin(x); break;
                case ExcelScientificOperation.Asin:
                    if (Math.Abs(x) > 1) return ExcelNumericUtilities.NumError();
                    result = Math.Asin(x); break;
                case ExcelScientificOperation.Acos:
                    if (Math.Abs(x) > 1) return ExcelNumericUtilities.NumError();
                    result = Math.Acos(x); break;
                case ExcelScientificOperation.Atan: result = Math.Atan(x); break;
                case ExcelScientificOperation.Acot: result = Math.Atan2(1, x); break;
                case ExcelScientificOperation.Sinh: result = Math.Sinh(x); break;
                case ExcelScientificOperation.Cosh: result = Math.Cosh(x); break;
                case ExcelScientificOperation.Tanh: result = Math.Tanh(x); break;
                case ExcelScientificOperation.Coth:
                    if (x == 0) return ExcelNumericUtilities.DivisionError();
                    result = 1 / Math.Tanh(x); break;
                case ExcelScientificOperation.Sech:
                    var decay = Math.Exp(-Math.Abs(x));
                    // Avoid overflowing cosh before taking its reciprocal.
                    result = 2 * decay / (1 + decay * decay); break;
                case ExcelScientificOperation.Csch:
                    if (x == 0) return ExcelNumericUtilities.DivisionError();
                    if (Math.Abs(x) < 20) result = 1 / Math.Sinh(x);
                    else
                    {
                        var tail = Math.Exp(-Math.Abs(x));
                        result = Math.CopySign(2 * tail / (1 - tail * tail), x);
                    }
                    break;
                case ExcelScientificOperation.Asinh: result = Math.Asinh(x); break;
                case ExcelScientificOperation.Acosh:
                    if (x < 1) return ExcelNumericUtilities.NumError();
                    result = Math.Acosh(x); break;
                case ExcelScientificOperation.Atanh:
                    if (Math.Abs(x) >= 1) return ExcelNumericUtilities.NumError();
                    result = Math.Atanh(x); break;
                case ExcelScientificOperation.Acoth:
                    if (Math.Abs(x) <= 1) return ExcelNumericUtilities.NumError();
                    result = Math.Atanh(1 / x); break;
                case ExcelScientificOperation.Degrees: result = x * (180 / Math.PI); break;
                case ExcelScientificOperation.Radians: result = x * (Math.PI / 180); break;
                case ExcelScientificOperation.SqrtPi:
                    if (x < 0) return ExcelNumericUtilities.NumError();
                    result = Math.Sqrt(x) * Math.Sqrt(Math.PI); break;
                default: return ExcelNumericUtilities.NumError();
            }
            return ExcelNumericUtilities.Number(context, result);
        }
    }

    internal sealed class Atan2Function : ExcelScalarFunctionBase
    {
        public Atan2Function() : base("ATAN2", 2, 2) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelNumericUtilities.TryFinite(context, args[0], out var x, out var error) ||
                !ExcelNumericUtilities.TryFinite(context, args[1], out var y, out error)) return FormulaValue.FromError(error);
            if (x == 0 && y == 0) return ExcelNumericUtilities.DivisionError();
            // Excel accepts x,y; the runtime accepts y,x. Its negative axis is +pi, including -0 y.
            return ExcelNumericUtilities.Number(context, y == 0 && x < 0 ? Math.PI : Math.Atan2(y, x));
        }
    }

    internal static class ExcelNumericUtilities
    {
        public static bool TryFinite(FormulaFunctionContext context, FormulaValue value, out double number, out FormulaError error)
        {
            if (!FormulaCoercion.TryCoerceToNumber(value, context.EvaluationContext.Workbook.Settings, out number, out error)) return false;
            if (double.IsFinite(number)) return true;
            error = new FormulaError(FormulaErrorType.Num);
            return false;
        }
        public static FormulaValue Number(FormulaFunctionContext context, double number)
            => double.IsFinite(number) ? ExcelFunctionUtilities.CreateNumber(context, number) : NumError();
        public static FormulaValue NumError() => FormulaValue.FromError(new FormulaError(FormulaErrorType.Num));
        public static FormulaValue DivisionError() => FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
    }

    public sealed partial class ExcelFunctionRegistry
    {
        partial void RegisterScientificDefaults()
        {
            Register(new ScientificFunction("SIN", ExcelScientificOperation.Sin));
            Register(new ScientificFunction("COS", ExcelScientificOperation.Cos));
            Register(new ScientificFunction("TAN", ExcelScientificOperation.Tan));
            Register(new ScientificFunction("COT", ExcelScientificOperation.Cot));
            Register(new ScientificFunction("SEC", ExcelScientificOperation.Sec));
            Register(new ScientificFunction("CSC", ExcelScientificOperation.Csc));
            Register(new ScientificFunction("ASIN", ExcelScientificOperation.Asin));
            Register(new ScientificFunction("ACOS", ExcelScientificOperation.Acos));
            Register(new ScientificFunction("ATAN", ExcelScientificOperation.Atan));
            Register(new ScientificFunction("ACOT", ExcelScientificOperation.Acot));
            Register(new ScientificFunction("SINH", ExcelScientificOperation.Sinh));
            Register(new ScientificFunction("COSH", ExcelScientificOperation.Cosh));
            Register(new ScientificFunction("TANH", ExcelScientificOperation.Tanh));
            Register(new ScientificFunction("COTH", ExcelScientificOperation.Coth));
            Register(new ScientificFunction("SECH", ExcelScientificOperation.Sech));
            Register(new ScientificFunction("CSCH", ExcelScientificOperation.Csch));
            Register(new ScientificFunction("ASINH", ExcelScientificOperation.Asinh));
            Register(new ScientificFunction("ACOSH", ExcelScientificOperation.Acosh));
            Register(new ScientificFunction("ATANH", ExcelScientificOperation.Atanh));
            Register(new ScientificFunction("ACOTH", ExcelScientificOperation.Acoth));
            Register(new ScientificFunction("DEGREES", ExcelScientificOperation.Degrees));
            Register(new ScientificFunction("RADIANS", ExcelScientificOperation.Radians));
            Register(new ScientificFunction("SQRTPI", ExcelScientificOperation.SqrtPi));
            Register(new Atan2Function());
            Register(new FactorialFunction(doubleFactorial: false));
            Register(new FactorialFunction(doubleFactorial: true));
            Register(new CombinatorialFunction("COMBIN", ExcelCombinatorialOperation.Combination));
            Register(new CombinatorialFunction("PERMUT", ExcelCombinatorialOperation.Permutation));
            Register(new CombinatorialFunction("PERMUTATIONA", ExcelCombinatorialOperation.RepeatedPermutation));
            Register(new QuotientFunction());
            Register(new ParityRoundFunction(odd: false));
            Register(new ParityRoundFunction(odd: true));
        }
    }
}
