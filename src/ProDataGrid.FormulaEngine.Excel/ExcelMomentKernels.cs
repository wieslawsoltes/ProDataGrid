// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Numerics;

namespace ProDataGrid.FormulaEngine.Excel
{
    // These kernels receive finite, privately owned values. Normalized intermediate
    // magnitudes are bounded, allowing SIMD compensation without per-lane error objects.
    internal static class ExcelMomentKernels
    {
        public static double MaximumMagnitude(ReadOnlySpan<double> values)
        {
            var i = 0;
            var maximum = 0d;
            if (Vector.IsHardwareAccelerated && values.Length >= Vector<double>.Count * 2)
            {
                var lanes = Vector<double>.Zero;
                for (; i <= values.Length - Vector<double>.Count; i += Vector<double>.Count)
                    lanes = Vector.Max(lanes, Vector.Abs(new Vector<double>(values.Slice(i))));
                for (var lane = 0; lane < Vector<double>.Count; lane++) maximum = Math.Max(maximum, lanes[lane]);
            }
            for (; i < values.Length; i++) maximum = Math.Max(maximum, Math.Abs(values[i]));
            return maximum;
        }

        public static double Normalize(Span<double> values, int exponent, double origin, bool squares)
        {
            var factor = Math.ScaleB(1d, -exponent);
            var i = 0;
            var sum = new ExcelCompensatedSum();
            if (double.IsFinite(factor))
            {
                if (Vector.IsHardwareAccelerated && values.Length >= Vector<double>.Count * 2)
                {
                    var scale = new Vector<double>(factor);
                    var offset = new Vector<double>(squares ? 0 : origin);
                    var lanes = new LaneSum();
                    for (; i <= values.Length - Vector<double>.Count; i += Vector<double>.Count)
                    {
                        var value = new Vector<double>(values.Slice(i)) * scale - offset;
                        value.CopyTo(values.Slice(i));
                        lanes.Add(squares ? value * value : value);
                    }
                    lanes.Merge(ref sum);
                }
                for (; i < values.Length; i++)
                {
                    var value = values[i] * factor - (squares ? 0 : origin);
                    values[i] = value;
                    sum.Add(squares ? value * value : value);
                }
            }
            else
            {
                // An inverse scale for subnormal inputs need not itself be representable.
                for (; i < values.Length; i++)
                {
                    var value = Math.ScaleB(values[i], -exponent) - (squares ? 0 : origin);
                    values[i] = value;
                    sum.Add(squares ? value * value : value);
                }
            }
            return sum.Total;
        }

        public static double SubtractAndSum(Span<double> values, double offset)
        {
            var i = 0;
            var sum = new ExcelCompensatedSum();
            if (Vector.IsHardwareAccelerated && values.Length >= Vector<double>.Count * 2)
            {
                var delta = new Vector<double>(offset);
                var lanes = new LaneSum();
                for (; i <= values.Length - Vector<double>.Count; i += Vector<double>.Count)
                {
                    var value = new Vector<double>(values.Slice(i)) - delta;
                    value.CopyTo(values.Slice(i));
                    lanes.Add(value);
                }
                lanes.Merge(ref sum);
            }
            for (; i < values.Length; i++) { values[i] -= offset; sum.Add(values[i]); }
            return sum.Total;
        }

        public static double RecenterAndSquares(Span<double> values, double correction)
        {
            var i = 0;
            var sum = new ExcelCompensatedSum();
            if (Vector.IsHardwareAccelerated && values.Length >= Vector<double>.Count * 2)
            {
                var offset = new Vector<double>(correction);
                var lanes = new LaneSum();
                for (; i <= values.Length - Vector<double>.Count; i += Vector<double>.Count)
                {
                    var value = new Vector<double>(values.Slice(i)) - offset;
                    value.CopyTo(values.Slice(i));
                    lanes.Add(value * value);
                }
                lanes.Merge(ref sum);
            }
            for (; i < values.Length; i++) { values[i] -= correction; sum.Add(values[i] * values[i]); }
            return sum.Total;
        }

        private struct LaneSum
        {
            private Vector<double> _sum;
            private Vector<double> _correction;
            public void Add(Vector<double> value)
            {
                var next = _sum + value;
                _correction += Vector.ConditionalSelect(Vector.GreaterThanOrEqual(Vector.Abs(_sum), Vector.Abs(value)),
                    (_sum - next) + value, (value - next) + _sum);
                _sum = next;
            }
            public void Merge(ref ExcelCompensatedSum result)
            {
                for (var lane = 0; lane < Vector<double>.Count; lane++)
                {
                    result.Add(_sum[lane]);
                    result.Add(_correction[lane]);
                }
            }
        }
    }
}
