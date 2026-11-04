// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    // All recurrences operate in coordinates relative to the first price of a finite run.
    // A growing range rescales a fixed number of scalars; it never scans or retains history.
    // On opposite-sign subtraction overflow, switch to magnitude-scaled coordinates at origin zero.
    internal struct StreamingIndicatorScale
    {
        private bool _initialized;
        private bool _hasVariation;
        private double _origin;
        private double _scale;

        public bool Include(double value, out double factor, out double shift)
        {
            factor = 1;
            shift = 0;
            if (!_initialized)
            {
                _initialized = true;
                _origin = value;
                _scale = 1;
                return false;
            }
            double distance = Math.Abs(value - _origin);
            if (!double.IsFinite(distance))
            {
                double scale = Math.Max(Math.Abs(value), Math.Abs(_origin));
                factor = _scale / scale;
                shift = _origin / scale;
                _origin = 0;
                _scale = scale;
                _hasVariation = true;
                return true;
            }
            if (distance == 0 || (_hasVariation && distance <= _scale)) return false;
            // Until the first variation, every retained normalized scalar is zero. A zero factor
            // avoids 1/subnormal overflow while preserving those exact states.
            factor = _hasVariation ? _scale / distance : 0;
            _scale = distance;
            _hasVariation = true;
            return true;
        }

        public double Encode(double value) => (value - _origin) / _scale;
        public double? Decode(double value) => Finite(Math.FusedMultiplyAdd(value, _scale, _origin));
        public double? DecodeDifference(double value) => Finite(value * _scale);
        private static double? Finite(double value) => double.IsFinite(value) ? value : null;
    }

    internal struct StreamingIndicatorAverage
    {
        public int Count { get; private set; }
        public double Mean { get; private set; }

        public bool Add(double value, int period, double alpha)
        {
            if (Count == 0 || period == 1)
            {
                Count = 1;
                Mean = value;
            }
            else if (Count < period)
            {
                Count++;
                Mean += (value - Mean) / Count;
            }
            else Mean += alpha * (value - Mean);
            return Count == period;
        }

        public void Transform(double factor, double shift = 0)
        {
            if (Count != 0) Mean = Math.FusedMultiplyAdd(Mean, factor, shift);
        }
    }
}
