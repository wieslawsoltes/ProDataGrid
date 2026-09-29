// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    public sealed partial class ExcelFunctionRegistry : IFormulaFunctionRegistry, IFormulaFunctionRegistryVersion
    {
        private readonly Dictionary<string, IFormulaFunction> _functions;

        public ExcelFunctionRegistry()
        {
            _functions = new Dictionary<string, IFormulaFunction>(StringComparer.OrdinalIgnoreCase);
            RegisterDefaults();
            RegisterExtendedDefaults();
            RegisterTextExtensions();
        }

        public bool TryGetFunction(string name, out IFormulaFunction function)
        {
            if (_functions.TryGetValue(name, out var found))
            {
                function = found;
                return true;
            }

            function = null!;
            return false;
        }

        public IEnumerable<IFormulaFunction> GetAll()
        {
            return _functions.Values;
        }

        public void Register(IFormulaFunction function)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            _functions[function.Name] = function;
            unchecked { Version++; }
        }

        private void RegisterDefaults()
        {
            Register(new AbsFunction());
            Register(new SumFunction());
            Register(new SumIfFunction());
            Register(new SumIfsFunction());
            Register(new AverageFunction());
            Register(new AverageIfFunction());
            Register(new AverageIfsFunction());
            Register(new CountFunction());
            Register(new CountaFunction());
            Register(new CountBlankFunction());
            Register(new CountIfFunction());
            Register(new CountIfsFunction());
            Register(new MinFunction());
            Register(new MaxFunction());
            Register(new MedianFunction());
            Register(new ModeSingleFunction());
            Register(new StdevSFunction());
            Register(new StdevPFunction());
            Register(new VarSFunction());
            Register(new VarPFunction());
            Register(new LargeFunction());
            Register(new SmallFunction());
            Register(new PercentileIncFunction());
            Register(new QuartileIncFunction());
            Register(new RankEqFunction());
            Register(new RankAvgFunction());
            Register(new IfFunction());
            Register(new IfErrorFunction());
            Register(new IfNaFunction());
            Register(new NotFunction());
            Register(new AndFunction());
            Register(new OrFunction());
            Register(new IntFunction());
            Register(new RoundFunction());
            Register(new RoundUpFunction());
            Register(new RoundDownFunction());
            Register(new ModFunction());
            Register(new PiFunction());
            Register(new PowerFunction());
            Register(new SqrtFunction());
            Register(new LnFunction());
            Register(new LogFunction());
            Register(new Log10Function());
            Register(new ExpFunction());
            Register(new SignFunction());
            Register(new IsBlankFunction());
            Register(new IsNumberFunction());
            Register(new IsTextFunction());
            Register(new IsLogicalFunction());
            Register(new IsErrorFunction());
            Register(new IsErrFunction());
            Register(new IsNaFunction());
            Register(new IsNonTextFunction());
            Register(new IsEvenFunction());
            Register(new IsOddFunction());
            Register(new TypeFunction());
            Register(new ErrorTypeFunction());
            Register(new NaFunction());
            Register(new NFunction());
            Register(new TFunction());
            Register(new ValueFunction());
            Register(new LenFunction());
            Register(new LeftFunction());
            Register(new RightFunction());
            Register(new MidFunction());
            Register(new ConcatFunction("CONCAT"));
            Register(new ConcatFunction("CONCATENATE"));
            Register(new TextJoinFunction());
            Register(new TextSplitFunction());
            Register(new LowerFunction());
            Register(new UpperFunction());
            Register(new TrimFunction());
            Register(new DateFunction());
            Register(new TimeFunction());
            Register(new DateValueFunction());
            Register(new TimeValueFunction());
            Register(new YearFunction());
            Register(new MonthFunction());
            Register(new DayFunction());
            Register(new HourFunction());
            Register(new MinuteFunction());
            Register(new SecondFunction());
            Register(new TodayFunction());
            Register(new NowFunction());
            Register(new EoMonthFunction());
            Register(new WorkdayFunction());
            Register(new NetworkDaysFunction());
            Register(new RandFunction());
            Register(new RandBetweenFunction());
            Register(new PvFunction());
            Register(new FvFunction());
            Register(new PmtFunction());
            Register(new NperFunction());
            Register(new RateFunction());
            Register(new NpvFunction());
            Register(new IrrFunction());
            Register(new IndexFunction());
            Register(new MatchFunction());
            Register(new VLookupFunction());
            Register(new HLookupFunction());
            Register(new XMatchFunction());
            Register(new XLookupFunction());
            Register(new OffsetFunction());
            Register(new IndirectFunction());
            Register(new SequenceFunction());
            Register(new FilterFunction());
            Register(new SortFunction());
            Register(new UniqueFunction());
        }
    }

    internal enum ExcelRoundMode
    {
        Nearest,
        Up,
        Down
    }

    internal abstract class ExcelFunctionBase : IFormulaFunction
    {
        protected ExcelFunctionBase(string name, FormulaFunctionInfo info)
        {
            Name = name;
            Info = info;
        }

        public string Name { get; }

        public FormulaFunctionInfo Info { get; }

        public abstract FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args);
    }

    internal sealed class SumFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public SumFunction()
            : base("SUM", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var accumulator = AggregateAccumulator.Create(context.EvaluationContext.Workbook.Settings);
            foreach (var arg in args)
            {
                if (!accumulator.Add(arg))
                {
                    return FormulaValue.FromError(accumulator.Error);
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, accumulator.Sum);
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            var accumulator = AggregateAccumulator.Create(context.EvaluationContext.Workbook.Settings);
            foreach (var entry in ExcelFunctionUtilities.EnumerateArgumentValuesWithOrigin(arguments, context, evaluator, resolver))
            {
                if (entry.IsFromReference)
                {
                    if (!accumulator.AddRangeValue(entry.Value))
                    {
                        return FormulaValue.FromError(accumulator.Error);
                    }
                    continue;
                }

                if (!accumulator.Add(entry.Value))
                {
                    return FormulaValue.FromError(accumulator.Error);
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, accumulator.Sum);
        }
    }

    internal sealed class AbsFunction : ExcelFunctionBase
    {
        public AbsFunction()
            : base("ABS", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return ExcelFunctionUtilities.CreateNumber(context, Math.Abs(number));
            });
        }
    }

    internal sealed class AverageFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public AverageFunction()
            : base("AVERAGE", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var accumulator = AggregateAccumulator.Create(context.EvaluationContext.Workbook.Settings);
            foreach (var arg in args)
            {
                if (!accumulator.Add(arg))
                {
                    return FormulaValue.FromError(accumulator.Error);
                }
            }

            if (accumulator.Count == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            }

            return ExcelFunctionUtilities.CreateNumber(context, accumulator.Sum / accumulator.Count);
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            var accumulator = AggregateAccumulator.Create(context.EvaluationContext.Workbook.Settings);
            foreach (var entry in ExcelFunctionUtilities.EnumerateArgumentValuesWithOrigin(arguments, context, evaluator, resolver))
            {
                if (entry.IsFromReference)
                {
                    if (!accumulator.AddRangeValue(entry.Value))
                    {
                        return FormulaValue.FromError(accumulator.Error);
                    }
                    continue;
                }

                if (!accumulator.Add(entry.Value))
                {
                    return FormulaValue.FromError(accumulator.Error);
                }
            }

            if (accumulator.Count == 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            }

            return ExcelFunctionUtilities.CreateNumber(context, accumulator.Sum / accumulator.Count);
        }
    }

    internal sealed class CountFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public CountFunction()
            : base("COUNT", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var count = 0;
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaValueKind.Array)
                {
                    foreach (var element in arg.AsArray().Flatten())
                    {
                        if (element.Kind == FormulaValueKind.Number)
                        {
                            count++;
                        }
                    }
                    continue;
                }

                if (arg.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                if (ExcelFunctionUtilities.TryCoerceToNumber(context, arg, out _, out _))
                {
                    count++;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            var count = 0;
            foreach (var entry in ExcelFunctionUtilities.EnumerateArgumentValuesWithOrigin(arguments, context, evaluator, resolver))
            {
                var value = entry.Value;
                if (value.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                if (entry.IsFromReference)
                {
                    if (value.Kind == FormulaValueKind.Number)
                    {
                        count++;
                    }
                    continue;
                }

                if (ExcelFunctionUtilities.TryCoerceToNumber(context, value, out _, out _))
                {
                    count++;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }
    }

    internal sealed class CountaFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public CountaFunction()
            : base("COUNTA", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var count = 0;
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaValueKind.Array)
                {
                    foreach (var element in arg.AsArray().Flatten())
                    {
                        if (element.Kind != FormulaValueKind.Blank)
                        {
                            count++;
                        }
                    }
                    continue;
                }

                if (arg.Kind != FormulaValueKind.Blank)
                {
                    count++;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            var count = 0;
            foreach (var value in ExcelFunctionUtilities.EnumerateArgumentValues(arguments, context, evaluator, resolver))
            {
                if (value.Kind != FormulaValueKind.Blank)
                {
                    count++;
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }
    }

    internal sealed class CountIfFunction : ExcelFunctionBase
    {
        public CountIfFunction()
            : base("COUNTIF", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var criteriaValue = args[1];
            if (criteriaValue.Kind == FormulaValueKind.Array)
            {
                var criteriaArray = criteriaValue.AsArray();
                if (criteriaArray.RowCount != 1 || criteriaArray.ColumnCount != 1)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }

                criteriaValue = criteriaArray[0, 0];
            }

            if (!ExcelCriteriaUtilities.TryCreateCriteria(criteriaValue, out var criteria, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var count = 0;
            var range = args[0];
            if (range.Kind == FormulaValueKind.Array)
            {
                foreach (var value in range.AsArray().Flatten())
                {
                    if (!ExcelCriteriaUtilities.TryMatch(value, criteria, out var match, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (match)
                    {
                        count++;
                    }
                }

                return ExcelFunctionUtilities.CreateNumber(context, count);
            }

            if (!ExcelCriteriaUtilities.TryMatch(range, criteria, out var singleMatch, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (singleMatch)
            {
                count++;
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }
    }

    internal sealed class CountIfsFunction : ExcelFunctionBase
    {
        public CountIfsFunction()
            : base("COUNTIFS", new FormulaFunctionInfo(2, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count % 2 != 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var pairCount = args.Count / 2;
            var ranges = new FormulaValue[pairCount];
            var criteria = new ExcelCriteria[pairCount];
            var criteriaErrors = new FormulaError[pairCount];
            for (var i = 0; i < pairCount; i++)
            {
                ranges[i] = args[i * 2];
                var criteriaValue = args[i * 2 + 1];
                if (criteriaValue.Kind == FormulaValueKind.Array)
                {
                    var criteriaArray = criteriaValue.AsArray();
                    if (criteriaArray.RowCount != 1 || criteriaArray.ColumnCount != 1)
                    {
                        return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                    }

                    criteriaValue = criteriaArray[0, 0];
                }

                if (!ExcelCriteriaUtilities.TryCreateCriteria(criteriaValue, out criteria[i], out criteriaErrors[i]))
                {
                    return FormulaValue.FromError(criteriaErrors[i]);
                }
            }

            var hasArray = false;
            var hasScalar = false;
            var rows = 1;
            var columns = 1;
            var arrays = new FormulaArray?[pairCount];
            for (var i = 0; i < pairCount; i++)
            {
                if (ranges[i].Kind == FormulaValueKind.Array)
                {
                    var array = ranges[i].AsArray();
                    arrays[i] = array;
                    if (!hasArray)
                    {
                        hasArray = true;
                        rows = array.RowCount;
                        columns = array.ColumnCount;
                    }
                    else if (rows != array.RowCount || columns != array.ColumnCount)
                    {
                        return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                    }
                }
                else
                {
                    hasScalar = true;
                }
            }

            if (hasArray && hasScalar && (rows != 1 || columns != 1))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!hasArray)
            {
                for (var i = 0; i < pairCount; i++)
                {
                    if (!ExcelCriteriaUtilities.TryMatch(ranges[i], criteria[i], out var match, out var error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (!match)
                    {
                        return ExcelFunctionUtilities.CreateNumber(context, 0);
                    }
                }

                return ExcelFunctionUtilities.CreateNumber(context, 1);
            }

            var count = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var matchesAll = true;
                    for (var i = 0; i < pairCount; i++)
                    {
                        if (arrays[i] != null && arrays[i]!.HasMask && !arrays[i]!.IsPresent(row, column))
                        {
                            matchesAll = false;
                            break;
                        }

                        var value = arrays[i] != null ? arrays[i]![row, column] : ranges[i];
                        if (!ExcelCriteriaUtilities.TryMatch(value, criteria[i], out var match, out var error))
                        {
                            return FormulaValue.FromError(error);
                        }

                        if (!match)
                        {
                            matchesAll = false;
                            break;
                        }
                    }

                    if (matchesAll)
                    {
                        count++;
                    }
                }
            }

            return ExcelFunctionUtilities.CreateNumber(context, count);
        }
    }

    internal sealed class IntFunction : ExcelFunctionBase
    {
        public IntFunction()
            : base("INT", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return ExcelFunctionUtilities.CreateNumber(context, Math.Floor(number));
            });
        }
    }

    internal sealed class RoundFunction : ExcelFunctionBase
    {
        public RoundFunction()
            : base("ROUND", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyBinary(args[0], args[1], (value, digitsValue) =>
                ExcelFunctionUtilities.RoundValue(context, value, digitsValue, ExcelRoundMode.Nearest));
        }
    }

    internal sealed class RoundUpFunction : ExcelFunctionBase
    {
        public RoundUpFunction()
            : base("ROUNDUP", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyBinary(args[0], args[1], (value, digitsValue) =>
                ExcelFunctionUtilities.RoundValue(context, value, digitsValue, ExcelRoundMode.Up));
        }
    }

    internal sealed class RoundDownFunction : ExcelFunctionBase
    {
        public RoundDownFunction()
            : base("ROUNDDOWN", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyBinary(args[0], args[1], (value, digitsValue) =>
                ExcelFunctionUtilities.RoundValue(context, value, digitsValue, ExcelRoundMode.Down));
        }
    }

    internal sealed class ModFunction : ExcelFunctionBase
    {
        public ModFunction()
            : base("MOD", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyBinary(args[0], args[1], (value, divisorValue) =>
                EvaluateMod(context, value, divisorValue));
        }

        private static FormulaValue EvaluateMod(
            FormulaFunctionContext context,
            FormulaValue value,
            FormulaValue divisorValue)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                return value;
            }

            if (divisorValue.Kind == FormulaValueKind.Error)
            {
                return divisorValue;
            }

            if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelFunctionUtilities.TryCoerceToNumber(context, divisorValue, out var divisor, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (Math.Abs(divisor) <= double.Epsilon)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            }

            var result = number - divisor * Math.Floor(number / divisor);
            return ExcelFunctionUtilities.CreateNumber(context, result);
        }
    }

    internal sealed class MinFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public MinFunction()
            : base("MIN", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            double? min = null;
            foreach (var value in ExcelFunctionUtilities.Flatten(args))
            {
                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                if (value.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                min = min.HasValue ? Math.Min(min.Value, number) : number;
            }

            if (!min.HasValue)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            return ExcelFunctionUtilities.CreateNumber(context, min.Value);
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            double? min = null;
            foreach (var value in ExcelFunctionUtilities.EnumerateArgumentValues(arguments, context, evaluator, resolver))
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (value.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                min = min.HasValue ? Math.Min(min.Value, number) : number;
            }

            if (!min.HasValue)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            return ExcelFunctionUtilities.CreateNumber(context, min.Value);
        }
    }

    internal sealed class MaxFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public MaxFunction()
            : base("MAX", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            double? max = null;
            foreach (var value in ExcelFunctionUtilities.Flatten(args))
            {
                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                if (value.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                max = max.HasValue ? Math.Max(max.Value, number) : number;
            }

            if (!max.HasValue)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            return ExcelFunctionUtilities.CreateNumber(context, max.Value);
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            double? max = null;
            foreach (var value in ExcelFunctionUtilities.EnumerateArgumentValues(arguments, context, evaluator, resolver))
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (value.Kind == FormulaValueKind.Blank)
                {
                    continue;
                }

                if (!ExcelFunctionUtilities.TryCoerceToNumber(context, value, out var number, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                max = max.HasValue ? Math.Max(max.Value, number) : number;
            }

            if (!max.HasValue)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            return ExcelFunctionUtilities.CreateNumber(context, max.Value);
        }
    }

    internal sealed class IfFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public IfFunction()
            : base("IF", new FormulaFunctionInfo(2, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count < 2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (!ExcelFunctionUtilities.TryCoerceToBoolean(
                    args[0],
                    context.EvaluationContext.Address,
                    out var condition,
                    out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (condition)
            {
                return args[1];
            }

            if (args.Count >= 3)
            {
                return args[2];
            }

            return FormulaValue.Blank;
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            if (arguments.Count < 2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var conditionValue = evaluator.Evaluate(arguments[0], context.EvaluationContext, resolver);
            if (conditionValue.Kind == FormulaValueKind.Error)
            {
                return conditionValue;
            }

            if (!ExcelFunctionUtilities.TryCoerceToBoolean(
                    conditionValue,
                    context.EvaluationContext.Address,
                    out var condition,
                    out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (condition)
            {
                return evaluator.Evaluate(arguments[1], context.EvaluationContext, resolver);
            }

            if (arguments.Count >= 3)
            {
                return evaluator.Evaluate(arguments[2], context.EvaluationContext, resolver);
            }

            return FormulaValue.Blank;
        }
    }

    internal sealed class IfErrorFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public IfErrorFunction()
            : base("IFERROR", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyBinary(args[0], args[1], (value, fallback) =>
            {
                return value.Kind == FormulaValueKind.Error ? fallback : value;
            });
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            if (arguments.Count < 2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var value = evaluator.Evaluate(arguments[0], context.EvaluationContext, resolver);
            if (value.Kind == FormulaValueKind.Error)
            {
                return evaluator.Evaluate(arguments[1], context.EvaluationContext, resolver);
            }

            if (value.Kind != FormulaValueKind.Array)
            {
                return value;
            }

            var array = value.AsArray();
            if (!ExcelFunctionUtilities.ContainsError(array))
            {
                return value;
            }

            var fallback = evaluator.Evaluate(arguments[1], context.EvaluationContext, resolver);
            return ExcelFunctionUtilities.ApplyBinary(value, fallback, (item, replacement) =>
                item.Kind == FormulaValueKind.Error ? replacement : item);
        }
    }

    internal sealed class IfNaFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        public IfNaFunction()
            : base("IFNA", new FormulaFunctionInfo(2, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyBinary(args[0], args[1], (value, fallback) =>
            {
                if (value.Kind == FormulaValueKind.Error && value.AsError().Type == FormulaErrorType.NA)
                {
                    return fallback;
                }

                return value;
            });
        }

        public FormulaValue InvokeLazy(
            FormulaFunctionContext context,
            IReadOnlyList<FormulaExpression> arguments,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            if (arguments.Count < 2)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var value = evaluator.Evaluate(arguments[0], context.EvaluationContext, resolver);
            if (value.Kind == FormulaValueKind.Error)
            {
                if (value.AsError().Type == FormulaErrorType.NA)
                {
                    return evaluator.Evaluate(arguments[1], context.EvaluationContext, resolver);
                }

                return value;
            }

            if (value.Kind != FormulaValueKind.Array)
            {
                return value;
            }

            var array = value.AsArray();
            if (!ExcelFunctionUtilities.ContainsError(array, FormulaErrorType.NA))
            {
                return value;
            }

            var fallback = evaluator.Evaluate(arguments[1], context.EvaluationContext, resolver);
            return ExcelFunctionUtilities.ApplyBinary(value, fallback, (item, replacement) =>
                item.Kind == FormulaValueKind.Error && item.AsError().Type == FormulaErrorType.NA
                    ? replacement
                    : item);
        }
    }

    internal sealed class NotFunction : ExcelFunctionBase
    {
        public NotFunction()
            : base("NOT", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToBoolean(value, context.EvaluationContext.Address, out var result, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return FormulaValue.FromBoolean(!result);
            });
        }
    }

    internal sealed class AndFunction : ExcelFunctionBase
    {
        public AndFunction()
            : base("AND", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var hasValue = false;
            var result = true;
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaValueKind.Array)
                {
                    foreach (var element in arg.AsArray().Flatten())
                    {
                        if (element.Kind == FormulaValueKind.Error)
                        {
                            return element;
                        }

                        if (element.Kind == FormulaValueKind.Text || element.Kind == FormulaValueKind.Blank)
                        {
                            continue;
                        }

                        if (!ExcelFunctionUtilities.TryCoerceToBoolean(
                                element,
                                context.EvaluationContext.Address,
                                out var logical,
                                out var error))
                        {
                            return FormulaValue.FromError(error);
                        }

                        hasValue = true;
                        if (!logical)
                        {
                            result = false;
                        }
                    }
                    continue;
                }

                if (arg.Kind == FormulaValueKind.Error)
                {
                    return arg;
                }

                if (!ExcelFunctionUtilities.TryCoerceToBoolean(
                        arg,
                        context.EvaluationContext.Address,
                        out var scalar,
                        out var scalarError))
                {
                    return FormulaValue.FromError(scalarError);
                }

                hasValue = true;
                if (!scalar)
                {
                    result = false;
                }
            }

            if (!hasValue)
            {
                return FormulaValue.FromBoolean(true);
            }

            return FormulaValue.FromBoolean(result);
        }
    }

    internal sealed class OrFunction : ExcelFunctionBase
    {
        public OrFunction()
            : base("OR", new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var hasValue = false;
            var result = false;
            foreach (var arg in args)
            {
                if (arg.Kind == FormulaValueKind.Array)
                {
                    foreach (var element in arg.AsArray().Flatten())
                    {
                        if (element.Kind == FormulaValueKind.Error)
                        {
                            return element;
                        }

                        if (element.Kind == FormulaValueKind.Text || element.Kind == FormulaValueKind.Blank)
                        {
                            continue;
                        }

                        if (!ExcelFunctionUtilities.TryCoerceToBoolean(
                                element,
                                context.EvaluationContext.Address,
                                out var logical,
                                out var error))
                        {
                            return FormulaValue.FromError(error);
                        }

                        hasValue = true;
                        if (logical)
                        {
                            result = true;
                        }
                    }
                    continue;
                }

                if (arg.Kind == FormulaValueKind.Error)
                {
                    return arg;
                }

                if (!ExcelFunctionUtilities.TryCoerceToBoolean(
                        arg,
                        context.EvaluationContext.Address,
                        out var scalar,
                        out var scalarError))
                {
                    return FormulaValue.FromError(scalarError);
                }

                hasValue = true;
                if (scalar)
                {
                    result = true;
                }
            }

            if (!hasValue)
            {
                return FormulaValue.FromBoolean(false);
            }

            return FormulaValue.FromBoolean(result);
        }
    }

    internal sealed class IsBlankFunction : ExcelFunctionBase
    {
        public IsBlankFunction()
            : base("ISBLANK", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
                FormulaValue.FromBoolean(value.Kind == FormulaValueKind.Blank));
        }
    }

    internal sealed class IsNumberFunction : ExcelFunctionBase
    {
        public IsNumberFunction()
            : base("ISNUMBER", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
                FormulaValue.FromBoolean(value.Kind == FormulaValueKind.Number));
        }
    }

    internal sealed class IsTextFunction : ExcelFunctionBase
    {
        public IsTextFunction()
            : base("ISTEXT", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
                FormulaValue.FromBoolean(value.Kind == FormulaValueKind.Text));
        }
    }

    internal sealed class IsLogicalFunction : ExcelFunctionBase
    {
        public IsLogicalFunction()
            : base("ISLOGICAL", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
                FormulaValue.FromBoolean(value.Kind == FormulaValueKind.Boolean));
        }
    }

    internal sealed class IsErrorFunction : ExcelFunctionBase
    {
        public IsErrorFunction()
            : base("ISERROR", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
                FormulaValue.FromBoolean(value.Kind == FormulaValueKind.Error));
        }
    }

    internal sealed class IsNaFunction : ExcelFunctionBase
    {
        public IsNaFunction()
            : base("ISNA", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
                FormulaValue.FromBoolean(value.Kind == FormulaValueKind.Error && value.AsError().Type == FormulaErrorType.NA));
        }
    }

    internal sealed class LenFunction : ExcelFunctionBase
    {
        public LenFunction()
            : base("LEN", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return ExcelFunctionUtilities.CreateNumber(context, ExcelTextIndexing.Length(text, context.EvaluationContext.Workbook.Settings));
            });
        }
    }

    internal sealed class LeftFunction : ExcelFunctionBase
    {
        public LeftFunction()
            : base("LEFT", new FormulaFunctionInfo(1, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var countValue = args.Count > 1 ? args[1] : ExcelFunctionUtilities.CreateNumber(context, 1);
            return ExcelFunctionUtilities.ApplyBinary(args[0], countValue, (value, count) =>
                ApplyLeft(context, value, count));
        }

        private static FormulaValue ApplyLeft(
            FormulaFunctionContext context,
            FormulaValue value,
            FormulaValue countValue)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                return value;
            }

            if (countValue.Kind == FormulaValueKind.Error)
            {
                return countValue;
            }

            if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, countValue, out var count, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (count < 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (count == 0)
            {
                return FormulaValue.FromText(string.Empty);
            }

            if (count >= text.Length)
            {
                return FormulaValue.FromText(text);
            }

            return FormulaValue.FromText(ExcelTextIndexing.Prefix(text, count, context.EvaluationContext.Workbook.Settings));
        }
    }

    internal sealed class RightFunction : ExcelFunctionBase
    {
        public RightFunction()
            : base("RIGHT", new FormulaFunctionInfo(1, 2))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var countValue = args.Count > 1 ? args[1] : ExcelFunctionUtilities.CreateNumber(context, 1);
            return ExcelFunctionUtilities.ApplyBinary(args[0], countValue, (value, count) =>
                ApplyRight(context, value, count));
        }

        private static FormulaValue ApplyRight(
            FormulaFunctionContext context,
            FormulaValue value,
            FormulaValue countValue)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                return value;
            }

            if (countValue.Kind == FormulaValueKind.Error)
            {
                return countValue;
            }

            if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, countValue, out var count, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (count < 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (count == 0)
            {
                return FormulaValue.FromText(string.Empty);
            }

            if (count >= text.Length)
            {
                return FormulaValue.FromText(text);
            }

            return FormulaValue.FromText(ExcelTextIndexing.Suffix(text, count, context.EvaluationContext.Workbook.Settings));
        }
    }

    internal sealed class MidFunction : ExcelFunctionBase
    {
        public MidFunction()
            : base("MID", new FormulaFunctionInfo(3, 3))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyTernary(args[0], args[1], args[2], (value, start, length) =>
                ApplyMid(context, value, start, length));
        }

        private static FormulaValue ApplyMid(
            FormulaFunctionContext context,
            FormulaValue value,
            FormulaValue startValue,
            FormulaValue lengthValue)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                return value;
            }

            if (startValue.Kind == FormulaValueKind.Error)
            {
                return startValue;
            }

            if (lengthValue.Kind == FormulaValueKind.Error)
            {
                return lengthValue;
            }

            if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, startValue, out var start, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (!ExcelFunctionUtilities.TryCoerceToInteger(context, lengthValue, out var length, out error))
            {
                return FormulaValue.FromError(error);
            }

            if (start < 1 || length < 0)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            if (length == 0 || start > text.Length)
            {
                return FormulaValue.FromText(string.Empty);
            }

            return FormulaValue.FromText(ExcelTextIndexing.Middle(text, start, length, context.EvaluationContext.Workbook.Settings));
        }
    }

    internal sealed class ConcatFunction : ExcelFunctionBase
    {
        public ConcatFunction(string name)
            : base(name, new FormulaFunctionInfo(1, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var builder = new StringBuilder();
            foreach (var value in args)
            {
                foreach (var element in ExcelFunctionUtilities.FlattenValues(value))
                {
                    if (element.Kind == FormulaValueKind.Error)
                    {
                        return element;
                    }

                    if (!ExcelFunctionUtilities.TryCoerceToText(element, out var text, out var error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    builder.Append(text);
                }
            }

            return FormulaValue.FromText(builder.ToString());
        }
    }

    internal sealed class LowerFunction : ExcelFunctionBase
    {
        public LowerFunction()
            : base("LOWER", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return FormulaValue.FromText(text.ToLowerInvariant());
            });
        }
    }

    internal sealed class UpperFunction : ExcelFunctionBase
    {
        public UpperFunction()
            : base("UPPER", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return FormulaValue.FromText(text.ToUpperInvariant());
            });
        }
    }

    internal sealed class TrimFunction : ExcelFunctionBase
    {
        public TrimFunction()
            : base("TRIM", new FormulaFunctionInfo(1, 1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            return ExcelFunctionUtilities.ApplyUnary(args[0], (value) =>
            {
                if (value.Kind == FormulaValueKind.Error)
                {
                    return value;
                }

                if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out var error))
                {
                    return FormulaValue.FromError(error);
                }

                return FormulaValue.FromText(ExcelFunctionUtilities.TrimText(text));
            });
        }
    }

    internal enum ExcelCriteriaOperator
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    internal enum ExcelCriteriaKind
    {
        Number,
        Boolean,
        Text,
        Empty
    }

    internal readonly struct ExcelCriteria
    {
        public ExcelCriteria(
            ExcelCriteriaOperator op,
            ExcelCriteriaKind kind,
            double number,
            bool boolean,
            string text,
            bool hasWildcard)
        {
            Operator = op;
            Kind = kind;
            Number = number;
            Boolean = boolean;
            Text = text;
            HasWildcard = hasWildcard;
        }

        public ExcelCriteriaOperator Operator { get; }

        public ExcelCriteriaKind Kind { get; }

        public double Number { get; }

        public bool Boolean { get; }

        public string Text { get; }

        public bool HasWildcard { get; }
    }

    internal static class ExcelCriteriaUtilities
    {
        public static bool TryCreateCriteria(
            FormulaValue criteriaValue,
            out ExcelCriteria criteria,
            out FormulaError error)
        {
            error = default;
            criteria = default;

            switch (criteriaValue.Kind)
            {
                case FormulaValueKind.Error:
                    error = criteriaValue.AsError();
                    return false;
                case FormulaValueKind.Number:
                    criteria = new ExcelCriteria(ExcelCriteriaOperator.Equal, ExcelCriteriaKind.Number,
                        criteriaValue.AsNumber(), false, string.Empty, false);
                    return true;
                case FormulaValueKind.Boolean:
                    criteria = new ExcelCriteria(ExcelCriteriaOperator.Equal, ExcelCriteriaKind.Boolean,
                        0, criteriaValue.AsBoolean(), string.Empty, false);
                    return true;
                case FormulaValueKind.Blank:
                    criteria = new ExcelCriteria(ExcelCriteriaOperator.Equal, ExcelCriteriaKind.Empty,
                        0, false, string.Empty, false);
                    return true;
                case FormulaValueKind.Text:
                    return TryCreateCriteriaFromText(criteriaValue.AsText(), out criteria, out error);
                default:
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
            }
        }

        public static bool TryMatch(
            FormulaValue value,
            ExcelCriteria criteria,
            out bool matches,
            out FormulaError error)
        {
            matches = false;
            error = default;

            if (value.Kind == FormulaValueKind.Error)
            {
                error = value.AsError();
                return false;
            }

            switch (criteria.Kind)
            {
                case ExcelCriteriaKind.Number:
                    if (!TryConvertToNumber(value, out var number))
                    {
                        matches = false;
                        return true;
                    }
                    matches = Compare(number, criteria.Number, criteria.Operator);
                    return true;
                case ExcelCriteriaKind.Boolean:
                    if (!TryConvertToBoolean(value, out var boolean))
                    {
                        matches = false;
                        return true;
                    }
                    if (criteria.Operator == ExcelCriteriaOperator.Equal)
                    {
                        matches = boolean == criteria.Boolean;
                        return true;
                    }
                    if (criteria.Operator == ExcelCriteriaOperator.NotEqual)
                    {
                        matches = boolean != criteria.Boolean;
                        return true;
                    }
                    matches = Compare(boolean ? 1 : 0, criteria.Boolean ? 1 : 0, criteria.Operator);
                    return true;
                case ExcelCriteriaKind.Empty:
                    matches = IsEmptyMatch(value, criteria.Operator);
                    return true;
                case ExcelCriteriaKind.Text:
                    if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out error))
                    {
                        return false;
                    }
                    matches = CompareText(text, criteria);
                    return true;
                default:
                    error = new FormulaError(FormulaErrorType.Value);
                    return false;
            }
        }

        private static bool TryCreateCriteriaFromText(
            string text,
            out ExcelCriteria criteria,
            out FormulaError error)
        {
            error = default;
            criteria = default;

            var criteriaText = text ?? string.Empty;
            ParseCriteriaText(criteriaText, out var op, out var operand);

            operand = operand.Trim();
            if (operand.Length == 0)
            {
                criteria = new ExcelCriteria(op, ExcelCriteriaKind.Empty, 0, false, string.Empty, false);
                return true;
            }

            if (double.TryParse(operand, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                criteria = new ExcelCriteria(op, ExcelCriteriaKind.Number, number, false, string.Empty, false);
                return true;
            }

            if (string.Equals(operand, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                criteria = new ExcelCriteria(op, ExcelCriteriaKind.Boolean, 0, true, string.Empty, false);
                return true;
            }

            if (string.Equals(operand, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                criteria = new ExcelCriteria(op, ExcelCriteriaKind.Boolean, 0, false, string.Empty, false);
                return true;
            }

            var hasWildcard = ContainsWildcard(operand);
            criteria = new ExcelCriteria(op, ExcelCriteriaKind.Text, 0, false, operand, hasWildcard);
            return true;
        }

        private static void ParseCriteriaText(
            string text,
            out ExcelCriteriaOperator op,
            out string operand)
        {
            op = ExcelCriteriaOperator.Equal;
            operand = text;

            if (text.StartsWith("<>", StringComparison.Ordinal))
            {
                op = ExcelCriteriaOperator.NotEqual;
                operand = text.Substring(2);
                return;
            }

            if (text.StartsWith(">=", StringComparison.Ordinal))
            {
                op = ExcelCriteriaOperator.GreaterOrEqual;
                operand = text.Substring(2);
                return;
            }

            if (text.StartsWith("<=", StringComparison.Ordinal))
            {
                op = ExcelCriteriaOperator.LessOrEqual;
                operand = text.Substring(2);
                return;
            }

            if (text.StartsWith(">", StringComparison.Ordinal))
            {
                op = ExcelCriteriaOperator.Greater;
                operand = text.Substring(1);
                return;
            }

            if (text.StartsWith("<", StringComparison.Ordinal))
            {
                op = ExcelCriteriaOperator.Less;
                operand = text.Substring(1);
                return;
            }

            if (text.StartsWith("=", StringComparison.Ordinal))
            {
                op = ExcelCriteriaOperator.Equal;
                operand = text.Substring(1);
            }
        }

        private static bool TryConvertToNumber(FormulaValue value, out double number)
        {
            number = 0;
            switch (value.Kind)
            {
                case FormulaValueKind.Number:
                    number = value.AsNumber();
                    return true;
                case FormulaValueKind.Boolean:
                    number = value.AsBoolean() ? 1 : 0;
                    return true;
                case FormulaValueKind.Blank:
                    number = 0;
                    return true;
                case FormulaValueKind.Text:
                    return double.TryParse(value.AsText(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out number);
                default:
                    return false;
            }
        }

        private static bool TryConvertToBoolean(FormulaValue value, out bool result)
        {
            result = false;
            switch (value.Kind)
            {
                case FormulaValueKind.Boolean:
                    result = value.AsBoolean();
                    return true;
                case FormulaValueKind.Number:
                    result = Math.Abs(value.AsNumber()) > double.Epsilon;
                    return true;
                case FormulaValueKind.Blank:
                    result = false;
                    return true;
                case FormulaValueKind.Text:
                    if (string.Equals(value.AsText(), "TRUE", StringComparison.OrdinalIgnoreCase))
                    {
                        result = true;
                        return true;
                    }
                    if (string.Equals(value.AsText(), "FALSE", StringComparison.OrdinalIgnoreCase))
                    {
                        result = false;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private static bool Compare(double left, double right, ExcelCriteriaOperator op)
        {
            return op switch
            {
                ExcelCriteriaOperator.Equal => left == right,
                ExcelCriteriaOperator.NotEqual => left != right,
                ExcelCriteriaOperator.Greater => left > right,
                ExcelCriteriaOperator.GreaterOrEqual => left >= right,
                ExcelCriteriaOperator.Less => left < right,
                ExcelCriteriaOperator.LessOrEqual => left <= right,
                _ => false
            };
        }

        private static bool CompareText(string text, ExcelCriteria criteria)
        {
            if (criteria.HasWildcard &&
                (criteria.Operator == ExcelCriteriaOperator.Equal || criteria.Operator == ExcelCriteriaOperator.NotEqual))
            {
                var matched = MatchesWildcard(text, criteria.Text);
                return criteria.Operator == ExcelCriteriaOperator.Equal ? matched : !matched;
            }

            var criteriaText = UnescapeCriteriaText(criteria.Text);
            var comparison = string.Compare(text, criteriaText, StringComparison.OrdinalIgnoreCase);
            return criteria.Operator switch
            {
                ExcelCriteriaOperator.Equal => comparison == 0,
                ExcelCriteriaOperator.NotEqual => comparison != 0,
                ExcelCriteriaOperator.Greater => comparison > 0,
                ExcelCriteriaOperator.GreaterOrEqual => comparison >= 0,
                ExcelCriteriaOperator.Less => comparison < 0,
                ExcelCriteriaOperator.LessOrEqual => comparison <= 0,
                _ => false
            };
        }

        private static bool IsEmptyMatch(FormulaValue value, ExcelCriteriaOperator op)
        {
            var isEmpty = value.Kind == FormulaValueKind.Blank;
            if (value.Kind == FormulaValueKind.Text && value.AsText().Length == 0)
            {
                isEmpty = true;
            }

            return op switch
            {
                ExcelCriteriaOperator.Equal => isEmpty,
                ExcelCriteriaOperator.NotEqual => !isEmpty,
                _ => CompareTextValue(value, string.Empty, op)
            };
        }

        private static bool CompareTextValue(FormulaValue value, string target, ExcelCriteriaOperator op)
        {
            if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out _))
            {
                return false;
            }

            var comparison = string.Compare(text, target, StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                ExcelCriteriaOperator.Greater => comparison > 0,
                ExcelCriteriaOperator.GreaterOrEqual => comparison >= 0,
                ExcelCriteriaOperator.Less => comparison < 0,
                ExcelCriteriaOperator.LessOrEqual => comparison <= 0,
                _ => false
            };
        }

        internal static bool MatchesWildcard(string text, string pattern)
        {
            var textIndex = 0;
            var patternIndex = 0;
            var starIndex = -1;
            var matchIndex = 0;

            while (textIndex < text.Length)
            {
                if (TryReadPatternToken(pattern, patternIndex, out var token, out var isStar, out var isQuestion, out var advance))
                {
                    if (isStar)
                    {
                        starIndex = patternIndex;
                        matchIndex = textIndex;
                        patternIndex += advance;
                        continue;
                    }

                    if (isQuestion || CharEquals(token, text[textIndex]))
                    {
                        patternIndex += advance;
                        textIndex++;
                        continue;
                    }
                }

                if (starIndex >= 0)
                {
                    patternIndex = starIndex + 1;
                    matchIndex++;
                    textIndex = matchIndex;
                    continue;
                }

                return false;
            }

            while (TryReadPatternToken(pattern, patternIndex, out _, out var isStar, out _, out var advance))
            {
                if (!isStar)
                {
                    return false;
                }

                patternIndex += advance;
            }

            return patternIndex >= pattern.Length;
        }

        private static bool CharEquals(char left, char right)
        {
            return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
        }

        internal static bool ContainsWildcard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                if (ch == '~')
                {
                    if (index + 1 < text.Length)
                    {
                        var next = text[index + 1];
                        if (next == '*' || next == '?' || next == '~')
                        {
                            index++;
                        }
                    }
                    continue;
                }

                if (ch == '*' || ch == '?')
                {
                    return true;
                }
            }

            return false;
        }

        private static string UnescapeCriteriaText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('~') < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                if (ch == '~' && index + 1 < text.Length)
                {
                    var next = text[index + 1];
                    if (next == '*' || next == '?' || next == '~')
                    {
                        builder.Append(next);
                        index++;
                        continue;
                    }
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static bool TryReadPatternToken(
            string pattern,
            int index,
            out char token,
            out bool isStar,
            out bool isQuestion,
            out int advance)
        {
            token = default;
            isStar = false;
            isQuestion = false;
            advance = 0;

            if (index >= pattern.Length)
            {
                return false;
            }

            var ch = pattern[index];
            if (ch == '~')
            {
                if (index + 1 < pattern.Length)
                {
                    var next = pattern[index + 1];
                    if (next == '*' || next == '?' || next == '~')
                    {
                        token = next;
                        advance = 2;
                        return true;
                    }
                }

                token = '~';
                advance = 1;
                return true;
            }

            if (ch == '*')
            {
                isStar = true;
                advance = 1;
                return true;
            }

            if (ch == '?')
            {
                isQuestion = true;
                advance = 1;
                return true;
            }

            token = ch;
            advance = 1;
            return true;
        }
    }

    internal static class ExcelFunctionUtilities
    {
        public static FormulaValue ApplyImplicitIntersection(FormulaValue value, FormulaCellAddress address)
        {
            return FormulaCoercion.ApplyImplicitIntersection(value, address);
        }

        public static FormulaValue ApplyUnary(FormulaValue value, Func<FormulaValue, FormulaValue> apply)
        {
            if (value.Kind != FormulaValueKind.Array)
            {
                return apply(value);
            }

            var array = value.AsArray();
            var result = new FormulaArray(array.RowCount, array.ColumnCount, array.Origin, array.HasMask);
            for (var row = 0; row < array.RowCount; row++)
            {
                for (var column = 0; column < array.ColumnCount; column++)
                {
                    if (array.HasMask && !array.IsPresent(row, column))
                    {
                        result.SetValue(row, column, FormulaValue.Blank, false);
                        continue;
                    }
                    result[row, column] = apply(array[row, column]);
                }
            }

            return FormulaValue.FromArray(result);
        }

        public static FormulaValue ApplyBinary(
            FormulaValue left,
            FormulaValue right,
            Func<FormulaValue, FormulaValue, FormulaValue> apply)
        {
            if (left.Kind != FormulaValueKind.Array && right.Kind != FormulaValueKind.Array)
            {
                return apply(left, right);
            }

            var leftArray = left.Kind == FormulaValueKind.Array ? left.AsArray() : null;
            var rightArray = right.Kind == FormulaValueKind.Array ? right.AsArray() : null;

            if (leftArray != null && rightArray != null)
            {
                if (leftArray.RowCount != rightArray.RowCount || leftArray.ColumnCount != rightArray.ColumnCount)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }

                var useMask = leftArray.HasMask || rightArray.HasMask;
                var result = new FormulaArray(leftArray.RowCount, leftArray.ColumnCount, leftArray.Origin ?? rightArray.Origin, useMask);
                for (var row = 0; row < leftArray.RowCount; row++)
                {
                    for (var column = 0; column < leftArray.ColumnCount; column++)
                    {
                        var leftPresent = !leftArray.HasMask || leftArray.IsPresent(row, column);
                        var rightPresent = !rightArray.HasMask || rightArray.IsPresent(row, column);
                        if (!leftPresent || !rightPresent)
                        {
                            result.SetValue(row, column, FormulaValue.Blank, false);
                            continue;
                        }
                        result[row, column] = apply(leftArray[row, column], rightArray[row, column]);
                    }
                }

                return FormulaValue.FromArray(result);
            }

            var array = leftArray ?? rightArray!;
            var scalar = leftArray == null ? left : right;
            var mapped = new FormulaArray(array.RowCount, array.ColumnCount, array.Origin, array.HasMask);
            for (var row = 0; row < array.RowCount; row++)
            {
                for (var column = 0; column < array.ColumnCount; column++)
                {
                    if (array.HasMask && !array.IsPresent(row, column))
                    {
                        mapped.SetValue(row, column, FormulaValue.Blank, false);
                        continue;
                    }
                    mapped[row, column] = apply(leftArray == null ? scalar : array[row, column],
                        leftArray == null ? array[row, column] : scalar);
                }
            }

            return FormulaValue.FromArray(mapped);
        }

        public static FormulaValue ApplyTernary(
            FormulaValue first,
            FormulaValue second,
            FormulaValue third,
            Func<FormulaValue, FormulaValue, FormulaValue, FormulaValue> apply)
        {
            var firstArray = first.Kind == FormulaValueKind.Array ? first.AsArray() : null;
            var secondArray = second.Kind == FormulaValueKind.Array ? second.AsArray() : null;
            var thirdArray = third.Kind == FormulaValueKind.Array ? third.AsArray() : null;

            if (firstArray == null && secondArray == null && thirdArray == null)
            {
                return apply(first, second, third);
            }

            var rows = firstArray?.RowCount ?? secondArray?.RowCount ?? thirdArray!.RowCount;
            var columns = firstArray?.ColumnCount ?? secondArray?.ColumnCount ?? thirdArray!.ColumnCount;

            if ((firstArray != null && (firstArray.RowCount != rows || firstArray.ColumnCount != columns)) ||
                (secondArray != null && (secondArray.RowCount != rows || secondArray.ColumnCount != columns)) ||
                (thirdArray != null && (thirdArray.RowCount != rows || thirdArray.ColumnCount != columns)))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            }

            var origin = firstArray?.Origin ?? secondArray?.Origin ?? thirdArray?.Origin;
            var useMask = (firstArray?.HasMask ?? false) ||
                          (secondArray?.HasMask ?? false) ||
                          (thirdArray?.HasMask ?? false);
            var result = new FormulaArray(rows, columns, origin, useMask);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var firstPresent = firstArray == null || !firstArray.HasMask || firstArray.IsPresent(row, column);
                    var secondPresent = secondArray == null || !secondArray.HasMask || secondArray.IsPresent(row, column);
                    var thirdPresent = thirdArray == null || !thirdArray.HasMask || thirdArray.IsPresent(row, column);
                    if (!firstPresent || !secondPresent || !thirdPresent)
                    {
                        result.SetValue(row, column, FormulaValue.Blank, false);
                        continue;
                    }
                    result[row, column] = apply(
                        firstArray != null ? firstArray[row, column] : first,
                        secondArray != null ? secondArray[row, column] : second,
                        thirdArray != null ? thirdArray[row, column] : third);
                }
            }

            return FormulaValue.FromArray(result);
        }

        public static IEnumerable<FormulaValue> Flatten(IReadOnlyList<FormulaValue> args)
        {
            foreach (var value in args)
            {
                if (value.Kind == FormulaValueKind.Array)
                {
                    foreach (var inner in value.AsArray().Flatten())
                    {
                        yield return inner;
                    }
                }
                else
                {
                    yield return value;
                }
            }
        }

        public static IEnumerable<FormulaValue> FlattenValues(FormulaValue value)
        {
            if (value.Kind == FormulaValueKind.Array)
            {
                foreach (var inner in value.AsArray().Flatten())
                {
                    yield return inner;
                }
                yield break;
            }

            yield return value;
        }

        public static IEnumerable<FormulaValue> EnumerateArgumentValues(
            IReadOnlyList<FormulaExpression> arguments,
            FormulaFunctionContext context,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            foreach (var argument in arguments)
            {
                foreach (var value in EnumerateArgumentValue(argument, context, evaluator, resolver))
                {
                    yield return value;
                }
            }
        }

        public readonly struct ArgumentValue
        {
            public ArgumentValue(FormulaValue value, bool isFromReference)
            {
                Value = value;
                IsFromReference = isFromReference;
            }

            public FormulaValue Value { get; }

            public bool IsFromReference { get; }
        }

        public static IEnumerable<ArgumentValue> EnumerateArgumentValuesWithOrigin(
            IReadOnlyList<FormulaExpression> arguments,
            FormulaFunctionContext context,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            foreach (var argument in arguments)
            {
                if (argument is FormulaReferenceExpression referenceExpression &&
                    resolver is IFormulaRangeValueResolver rangeResolver)
                {
                    foreach (var value in rangeResolver.EnumerateReferenceValues(
                                 context.EvaluationContext,
                                 referenceExpression.Reference))
                    {
                        yield return new ArgumentValue(value, true);
                    }
                    continue;
                }

                var valueResult = evaluator.Evaluate(argument, context.EvaluationContext, resolver);
                if (valueResult.Kind == FormulaValueKind.Array)
                {
                    foreach (var inner in valueResult.AsArray().Flatten())
                    {
                        yield return new ArgumentValue(inner, true);
                    }
                    continue;
                }

                yield return new ArgumentValue(valueResult, false);
            }
        }

        private static IEnumerable<FormulaValue> EnumerateArgumentValue(
            FormulaExpression argument,
            FormulaFunctionContext context,
            FormulaEvaluator evaluator,
            IFormulaValueResolver resolver)
        {
            if (argument is FormulaReferenceExpression referenceExpression &&
                resolver is IFormulaRangeValueResolver rangeResolver)
            {
                foreach (var value in rangeResolver.EnumerateReferenceValues(
                             context.EvaluationContext,
                             referenceExpression.Reference))
                {
                    yield return value;
                }
                yield break;
            }

            var valueResult = evaluator.Evaluate(argument, context.EvaluationContext, resolver);
            if (valueResult.Kind == FormulaValueKind.Array)
            {
                foreach (var inner in valueResult.AsArray().Flatten())
                {
                    yield return inner;
                }
                yield break;
            }

            yield return valueResult;
        }

        public static bool TryCoerceToNumber(FormulaValue value, out double number, out FormulaError error)
        {
            return FormulaCoercion.TryCoerceToNumber(value, out number, out error);
        }

        public static bool TryCoerceToNumber(
            FormulaCalculationSettings settings,
            FormulaValue value,
            out double number,
            out FormulaError error)
        {
            return FormulaCoercion.TryCoerceToNumber(value, settings, out number, out error);
        }

        public static bool TryCoerceToNumber(
            FormulaFunctionContext context,
            FormulaValue value,
            out double number,
            out FormulaError error)
        {
            return FormulaCoercion.TryCoerceToNumber(value, context.EvaluationContext.Workbook.Settings, out number, out error);
        }

        public static bool TryCoerceToText(FormulaValue value, out string text, out FormulaError error)
        {
            return FormulaCoercion.TryCoerceToText(value, out text, out error);
        }

        public static FormulaValue RoundValue(FormulaValue value, FormulaValue digitsValue, ExcelRoundMode mode)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                return value;
            }

            if (digitsValue.Kind == FormulaValueKind.Error)
            {
                return digitsValue;
            }

            if (!TryCoerceToNumber(value, out var number, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!TryCoerceToInteger(digitsValue, out var digits, out error))
            {
                return FormulaValue.FromError(error);
            }

            var rounded = RoundNumber(number, digits, mode);
            return FormulaValue.FromNumber(rounded);
        }

        public static FormulaValue RoundValue(
            FormulaFunctionContext context,
            FormulaValue value,
            FormulaValue digitsValue,
            ExcelRoundMode mode)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                return value;
            }

            if (digitsValue.Kind == FormulaValueKind.Error)
            {
                return digitsValue;
            }

            if (!TryCoerceToNumber(context, value, out var number, out var error))
            {
                return FormulaValue.FromError(error);
            }

            if (!TryCoerceToInteger(context, digitsValue, out var digits, out error))
            {
                return FormulaValue.FromError(error);
            }

            var rounded = RoundNumber(number, digits, mode);
            return CreateNumber(context, rounded);
        }

        public static bool TryCoerceToInteger(FormulaValue value, out int result, out FormulaError error)
        {
            if (!TryCoerceToNumber(value, out var number, out error))
            {
                result = 0;
                return false;
            }

            number = AdjustIntegerPrecision(number);

            if (number >= int.MaxValue)
            {
                result = int.MaxValue;
            }
            else if (number <= int.MinValue)
            {
                result = int.MinValue;
            }
            else
            {
                result = (int)Math.Truncate(number);
            }
            return true;
        }

        public static bool TryCoerceToInteger(
            FormulaCalculationSettings settings,
            FormulaValue value,
            out int result,
            out FormulaError error)
        {
            if (!TryCoerceToNumber(settings, value, out var number, out error))
            {
                result = 0;
                return false;
            }

            number = AdjustIntegerPrecision(number);

            if (number >= int.MaxValue)
            {
                result = int.MaxValue;
            }
            else if (number <= int.MinValue)
            {
                result = int.MinValue;
            }
            else
            {
                result = (int)Math.Truncate(number);
            }
            return true;
        }

        public static bool TryCoerceToInteger(
            FormulaFunctionContext context,
            FormulaValue value,
            out int result,
            out FormulaError error)
        {
            if (!TryCoerceToNumber(context, value, out var number, out error))
            {
                result = 0;
                return false;
            }

            number = AdjustIntegerPrecision(number);

            if (number >= int.MaxValue)
            {
                result = int.MaxValue;
            }
            else if (number <= int.MinValue)
            {
                result = int.MinValue;
            }
            else
            {
                result = (int)Math.Truncate(number);
            }
            return true;
        }

        private static double AdjustIntegerPrecision(double number)
        {
            const double epsilon = 1e-9;
            var truncated = Math.Truncate(number);
            if (Math.Abs(number - truncated) <= epsilon)
            {
                return truncated;
            }

            var direction = number >= 0 ? 1d : -1d;
            var next = truncated + direction;
            if (Math.Abs(number - next) <= epsilon)
            {
                return next;
            }

            return truncated;
        }

        public static FormulaValue CreateNumber(FormulaCalculationSettings settings, double value)
        {
            if (settings.ApplyNumberPrecision)
            {
                value = FormulaNumberUtilities.ApplyPrecision(value, settings.NumberPrecisionDigits);
            }

            return FormulaValue.FromNumber(value);
        }

        public static FormulaValue CreateNumber(FormulaFunctionContext context, double value)
        {
            return CreateNumber(context.EvaluationContext.Workbook.Settings, value);
        }

        public static bool TryCoerceToBoolean(
            FormulaValue value,
            FormulaCellAddress address,
            out bool result,
            out FormulaError error)
        {
            if (value.Kind == FormulaValueKind.Array)
            {
                value = ApplyImplicitIntersection(value, address);
            }

            return FormulaCoercion.TryCoerceToBoolean(value, out result, out error);
        }

        public static bool ContainsError(FormulaArray array, FormulaErrorType? errorType = null)
        {
            for (var row = 0; row < array.RowCount; row++)
            {
                for (var column = 0; column < array.ColumnCount; column++)
                {
                    if (array.HasMask && !array.IsPresent(row, column))
                    {
                        continue;
                    }
                    var value = array[row, column];
                    if (value.Kind != FormulaValueKind.Error)
                    {
                        continue;
                    }

                    if (errorType == null || value.AsError().Type == errorType)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string TrimText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var start = 0;
            var end = text.Length - 1;
            while (start <= end && text[start] == ' ')
            {
                start++;
            }

            while (end >= start && text[end] == ' ')
            {
                end--;
            }

            if (start > end)
            {
                return string.Empty;
            }

            var trimmed = text.Substring(start, end - start + 1);
            var builder = new StringBuilder(trimmed.Length);
            var inSpace = false;
            foreach (var ch in trimmed)
            {
                if (ch == ' ')
                {
                    if (inSpace)
                    {
                        continue;
                    }

                    inSpace = true;
                    builder.Append(ch);
                    continue;
                }

                inSpace = false;
                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static double RoundNumber(double number, int digits, ExcelRoundMode mode)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                return number;
            }

            if (digits > 308)
            {
                return number;
            }

            if (digits < -308)
            {
                return 0;
            }

            if (TryRoundDecimal(number, digits, mode, out var decimalRounded))
            {
                return decimalRounded;
            }

            if (digits >= 0)
            {
                var factor = Math.Pow(10, digits);
                return mode switch
                {
                    ExcelRoundMode.Nearest => Math.Round(number * factor, 0, MidpointRounding.AwayFromZero) / factor,
                    ExcelRoundMode.Up => Math.Sign(number) * Math.Ceiling(Math.Abs(number) * factor) / factor,
                    _ => Math.Sign(number) * Math.Floor(Math.Abs(number) * factor) / factor
                };
            }

            var scale = Math.Pow(10, -digits);
            return mode switch
            {
                ExcelRoundMode.Nearest => Math.Round(number / scale, 0, MidpointRounding.AwayFromZero) * scale,
                ExcelRoundMode.Up => Math.Sign(number) * Math.Ceiling(Math.Abs(number) / scale) * scale,
                _ => Math.Sign(number) * Math.Floor(Math.Abs(number) / scale) * scale
            };
        }

        private static bool TryRoundDecimal(double number, int digits, ExcelRoundMode mode, out double rounded)
        {
            rounded = number;
            if (digits < -28 || digits > 28)
            {
                return false;
            }

            if (number > (double)decimal.MaxValue || number < (double)decimal.MinValue)
            {
                return false;
            }

            decimal dec = (decimal)number;
            if (digits >= 0)
            {
                var factor = (decimal)Math.Pow(10, digits);
                decimal result = mode switch
                {
                    ExcelRoundMode.Nearest => decimal.Round(dec, digits, MidpointRounding.AwayFromZero),
                    ExcelRoundMode.Up => Math.Sign(dec) * decimal.Ceiling(Math.Abs(dec) * factor) / factor,
                    _ => Math.Sign(dec) * decimal.Floor(Math.Abs(dec) * factor) / factor
                };

                rounded = (double)result;
                return true;
            }

            var scale = (decimal)Math.Pow(10, -digits);
            decimal scaled = dec / scale;
            decimal scaledRounded = mode switch
            {
                ExcelRoundMode.Nearest => decimal.Round(scaled, 0, MidpointRounding.AwayFromZero),
                ExcelRoundMode.Up => Math.Sign(dec) * decimal.Ceiling(Math.Abs(scaled)),
                _ => Math.Sign(dec) * decimal.Floor(Math.Abs(scaled))
            };

            rounded = (double)(scaledRounded * scale);
            return true;
        }
    }

    internal struct AggregateAccumulator
    {
        private readonly FormulaCalculationSettings? _settings;
        public double Sum;
        public int Count;
        public FormulaError Error;

        public AggregateAccumulator(FormulaCalculationSettings? settings)
        {
            _settings = settings;
            Sum = 0;
            Count = 0;
            Error = default;
        }

        public static AggregateAccumulator Create()
        {
            return new AggregateAccumulator(null);
        }

        public static AggregateAccumulator Create(FormulaCalculationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return new AggregateAccumulator(settings);
        }

        public bool Add(FormulaValue value)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                Error = value.AsError();
                return false;
            }

            if (value.Kind == FormulaValueKind.Array)
            {
                foreach (var inner in value.AsArray().Flatten())
                {
                    if (!AddFromArray(inner))
                    {
                        return false;
                    }
                }

                return true;
            }

            return AddDirect(value);
        }

        public bool AddRangeValue(FormulaValue value)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                Error = value.AsError();
                return false;
            }

            if (value.Kind == FormulaValueKind.Number)
            {
                Sum = ApplyPrecision(Sum + value.AsNumber());
                Count++;
            }

            return true;
        }

        private bool AddFromArray(FormulaValue value)
        {
            if (value.Kind == FormulaValueKind.Error)
            {
                Error = value.AsError();
                return false;
            }

            if (value.Kind != FormulaValueKind.Number)
            {
                return true;
            }

            Sum = ApplyPrecision(Sum + value.AsNumber());
            Count++;
            return true;
        }

        private bool AddDirect(FormulaValue value)
        {
            if (!TryCoerceToNumber(value, out var number, out var error))
            {
                Error = error;
                return false;
            }

            if (value.Kind != FormulaValueKind.Blank)
            {
                Sum = ApplyPrecision(Sum + number);
                Count++;
            }

            return true;
        }

        private bool TryCoerceToNumber(FormulaValue value, out double number, out FormulaError error)
        {
            if (_settings != null)
            {
                return ExcelFunctionUtilities.TryCoerceToNumber(_settings, value, out number, out error);
            }

            return ExcelFunctionUtilities.TryCoerceToNumber(value, out number, out error);
        }

        private double ApplyPrecision(double value)
        {
            if (_settings != null && _settings.ApplyNumberPrecision)
            {
                return FormulaNumberUtilities.ApplyPrecision(value, _settings.NumberPrecisionDigits);
            }

            return value;
        }
    }
}
