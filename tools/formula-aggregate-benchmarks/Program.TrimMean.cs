// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

internal static partial class Program
{
    private static void ValidateTrimMean(FormulaEvaluationContext context, FormulaEvaluator evaluator,
        IFormulaValueResolver resolver, ExcelFormulaParser parser)
    {
        var cases = new (string Formula, double Expected)[]
        {
            ("TRIMMEAN({1;2;3;4;100},0.4)",3),
            ("TRIMMEAN({1;2;3;4;100},0)",22),
            ("TRIMMEAN({1;2;3;4;100},1)",3),
            ("TRIMMEAN({1;2;3;100},0.5)",2.5),
            ("TRIMMEAN({1,2;3,100},0.5)",2.5),
            ("TRIMMEAN({1;\"99\";TRUE;3},0)",2),
            ("TRIMMEAN({1E308;1;-1E308},0)",1d/3),
            ("TRIMMEAN({1E308;1E308},0)",1E308),
            ("SUM(TRIMMEAN({1;2;3;4;100},{0;0.4;0.8}))",28),
            ("TRIMMEAN({4;5;6;7;2;3;4;5;1;2;3},0.2)",34d/9)
        };
        var errors = new (string Formula, FormulaErrorType Error)[]
        {
            ("TRIMMEAN({1;2},1)",FormulaErrorType.Num),
            ("TRIMMEAN({1;2},-0.1)",FormulaErrorType.Num),
            ("TRIMMEAN({1;2},1.1)",FormulaErrorType.Num),
            ("TRIMMEAN({1;2},NA())",FormulaErrorType.NA),
            ("TRIMMEAN({\"a\";TRUE},0)",FormulaErrorType.Num)
        };
        foreach(var compiled in new[]{true,false})
        {
            context.Workbook.Settings.EnableCompiledExpressions=compiled;
            context.Workbook.Settings.ApplyNumberPrecision=false;
            foreach(var item in cases)
            {
                var result=evaluator.Evaluate(parser.Parse(item.Formula,new FormulaParseOptions()),context,resolver);
                if(result.Kind!=FormulaValueKind.Number||result.AsNumber()!=item.Expected)
                    throw new InvalidOperationException("TrimMean smoke failed: "+item.Formula+" => "+result);
            }
            foreach(var item in errors)
            {
                var result=evaluator.Evaluate(parser.Parse(item.Formula,new FormulaParseOptions()),context,resolver);
                if(result.Kind!=FormulaValueKind.Error||result.AsError().Type!=item.Error)
                    throw new InvalidOperationException("TrimMean error smoke failed: "+item.Formula);
            }
        }
        context.Workbook.Settings.EnableCompiledExpressions=true;
        Console.WriteLine("TrimMean smoke assertions="+2*(cases.Length+errors.Length));
    }

    private static void MeasureTrimMean(StringBuilder output, FormulaEvaluationContext context, bool reverse)
    {
        const int count=20000;
        var input=new FormulaArray(count,1);
        var percentages=new FormulaArray(128,1);
        for(var i=0;i<count;i++)input[i,0]=FormulaValue.FromNumber((i*7919)%count);
        for(var i=0;i<percentages.RowCount;i++)percentages[i,0]=FormulaValue.FromNumber(((i*37)%128)/128d);
        var scoped=context.WithLocalValue("trimData",FormulaValue.FromArray(input))
            .WithLocalValue("trimPercents",FormulaValue.FromArray(percentages));
        var parser=new ExcelFormulaParser();var evaluator=new FormulaEvaluator();var resolver=new EmptyResolver();
        var direct=parser.Parse("TRIMMEAN(trimData,trimPercents)",new FormulaParseOptions());
        var mapped=parser.Parse("MAP(trimPercents,LAMBDA(p,TRIMMEAN(trimData,p)))",new FormulaParseOptions());
        var actual=evaluator.Evaluate(direct,scoped,resolver).AsArray();
        var reference=evaluator.Evaluate(mapped,scoped,resolver).AsArray();
        for(var i=0;i<percentages.RowCount;i++)
            if(actual[i,0]!=reference[i,0]||actual[i,0].AsNumber()!=9999.5)
                throw new InvalidOperationException("TrimMean full-result comparison failed.");
        var expected=TrimMeanChecksum(actual);
        for(var pass=0;pass<2;pass++)
        {
            var batch=reverse?pass==1:pass==0;
            var expression=batch?direct:mapped;
            Measure(output,batch?"trimmean_20000_128_batch":"trimmean_20000_128_map",2,
                ()=>TrimMeanChecksum(evaluator.Evaluate(expression,scoped,resolver).AsArray()),expected);
        }
        var function=Function((ExcelFunctionRegistry)context.FunctionRegistry,"TRIMMEAN");
        var call=new FormulaFunctionContext(context);
        var arguments=new[]{FormulaValue.FromArray(input),FormulaValue.FromNumber(0.2)};
        for(var pass=0;pass<2;pass++)
        {
            var selected=reverse?pass==1:pass==0;
            Measure(output,selected?"trimmean_20000_scalar_selection":"trimmean_20000_scalar_sort_reference",20,
                ()=>selected?function.Invoke(call,arguments).AsNumber():SortTrimMeanReference(input,0.2),9999.5);
        }
    }

    private static double TrimMeanChecksum(FormulaArray array)
    {
        double sum=0;for(var i=0;i<array.RowCount;i++)sum+=array[i,0].AsNumber()*(i+1d);return sum;
    }
    private static double SortTrimMeanReference(FormulaArray input,double fraction)
    {
        var data=new List<double>();
        foreach(var value in input.Flatten())if(value.Kind==FormulaValueKind.Number)data.Add(value.AsNumber());
        data.Sort();var tail=(int)(data.Count*fraction/2);double total=0;
        for(var i=tail;i<data.Count-tail;i++)total+=data[i];
        return total/(data.Count-2*tail);
    }
}
