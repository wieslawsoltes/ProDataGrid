// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Numerics;

namespace ProDataGrid.FormulaEngine.Excel
{
    // The two axes keep independent power-of-two scales and split means. In particular,
    // correlation and predictions do not require a representable covariance or slope.
    internal readonly struct ExcelBivariateModel
    {
        private readonly Axis _x;
        private readonly Axis _y;
        public double XX { get; }
        public double YY { get; }
        public double XY { get; }
        private double ScaledSlope => XY / XX;

        public ExcelBivariateModel(Span<double> x, Span<double> y)
        {
            _x = new Axis(x);
            _y = new Axis(y);
            XX = _x.Squares;
            YY = _y.Squares;
            XY = Dot(x, y);
        }

        public double Covariance(int divisor) => Math.ScaleB(XY / divisor, _x.Exponent + _y.Exponent);
        public double Slope => Math.ScaleB(ScaledSlope, _y.Exponent - _x.Exponent);
        // These are independently normalized centered sums, not physical variances.
        // Their product stays in range for the bounded input count and represented spreads.
        public double Correlation => Math.Clamp(XY / Math.Sqrt(XX * YY), -1d, 1d);

        public double Predict(double x)
        {
            var slope = ScaledSlope;
            if (slope == 0) return Math.ScaleB(_y.Origin + _y.Offset + _y.Correction, _y.Exponent);
            var scaled = Math.ScaleB(x, -_x.Exponent);
            var product = slope * scaled;
            var normalized = PredictNormalized(scaled, slope);
            var lostTargetBits = Math.ScaleB(scaled, _x.Exponent) != x;
            var tinyProduct = double.IsSubnormal(product) || product == 0 && x != 0;
            if (double.IsFinite(normalized) && !lostTargetBits && !tinyProduct)
                return Math.ScaleB(normalized, _y.Exponent);

            // Extrapolation can overflow or underflow x's normalized scale even when
            // the prediction is finite. Combine a scaled product and intercept before
            // restoring their common exponent, without constructing the actual b or b*x.
            var intercept = PredictNormalized(0, slope);
            if (x == 0) return Math.ScaleB(intercept, _y.Exponent);
            var eb = Math.ILogB(Math.Abs(slope));
            var ev = Math.ILogB(Math.Abs(x));
            product = Math.ScaleB(slope, -eb) * Math.ScaleB(x, -ev);
            var productExponent = eb + ev + _y.Exponent - _x.Exponent;
            if (intercept == 0) return Math.ScaleB(product, productExponent);
            var ea = Math.ILogB(Math.Abs(intercept));
            var interceptExponent = ea + _y.Exponent;
            var common = Math.Max(productExponent, interceptExponent);
            return Math.ScaleB(Math.ScaleB(product, productExponent - common) +
                Math.ScaleB(Math.ScaleB(intercept, -ea), interceptExponent - common), common);
        }

        private double PredictNormalized(double x, double slope)
        {
            // Expanding the split mean with compensated products avoids losing the low
            // part of x-origin before an FMA. This matters even for an identity fit when
            // the extrapolation is 2^53 times the training scale.
            var sum = new ExcelCompensatedSum();
            sum.Add(_y.Origin);
            sum.Add(_y.Offset);
            sum.Add(_y.Correction);
            return AddProduct(ref sum, slope, x) && AddProduct(ref sum, -slope, _x.Origin) &&
                AddProduct(ref sum, -slope, _x.Offset) && AddProduct(ref sum, -slope, _x.Correction)
                ? sum.Total : double.NaN;
        }

        private static bool AddProduct(ref ExcelCompensatedSum sum, double left, double right)
        {
            if (left == 0 || right == 0) return true;
            var product = left * right;
            return double.IsFinite(product) && sum.Add(product) && sum.Add(Math.FusedMultiplyAdd(left, right, -product));
        }

        public double StandardError(ReadOnlySpan<double> x, Span<double> y)
        {
            // Sum actual centered residuals, not YY-XY*XY/XX: the latter loses the
            // entire residual for an almost-exact fit. y is private scratch storage.
            var slope = ScaledSlope;
            for (var i = 0; i < x.Length; i++) y[i] = Math.FusedMultiplyAdd(-slope, x[i], y[i]);
            var maximum = ExcelMomentKernels.MaximumMagnitude(y);
            if (maximum == 0) return 0;
            var exponent = Math.ILogB(maximum);
            var squares = ExcelMomentKernels.Normalize(y, exponent, 0, squares: true);
            return Math.ScaleB(Math.Sqrt(squares / (x.Length - 2)), _y.Exponent + exponent);
        }

        private readonly struct Axis
        {
            public int Exponent { get; }
            public double Origin { get; }
            public double Offset { get; }
            public double Correction { get; }
            public double Squares { get; }
            public Axis(Span<double> values)
            {
                var maximum = ExcelMomentKernels.MaximumMagnitude(values);
                Exponent = maximum == 0 ? 0 : Math.ILogB(maximum);
                Origin = Math.ScaleB(values[0], -Exponent);
                Offset = ExcelMomentKernels.Normalize(values, Exponent, Origin, squares: false) / values.Length;
                Correction = ExcelMomentKernels.SubtractAndSum(values, Offset) / values.Length;
                Squares = ExcelMomentKernels.RecenterAndSquares(values, Correction);
            }
        }

        private static double Dot(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
        {
            var sum = new ExcelCompensatedSum();
            var i = 0;
            if (Vector.IsHardwareAccelerated && x.Length >= 2 * Vector<double>.Count)
            {
                var lanes = Vector<double>.Zero;
                var errors = Vector<double>.Zero;
                for (; i <= x.Length - Vector<double>.Count; i += Vector<double>.Count)
                {
                    var product = new Vector<double>(x.Slice(i)) * new Vector<double>(y.Slice(i));
                    var next = lanes + product;
                    errors += Vector.ConditionalSelect(Vector.GreaterThanOrEqual(Vector.Abs(lanes), Vector.Abs(product)),
                        (lanes - next) + product, (product - next) + lanes);
                    lanes = next;
                }
                for (var lane = 0; lane < Vector<double>.Count; lane++) { sum.Add(lanes[lane]); sum.Add(errors[lane]); }
            }
            for (; i < x.Length; i++) sum.Add(x[i] * y[i]);
            return sum.Total;
        }
    }
}
