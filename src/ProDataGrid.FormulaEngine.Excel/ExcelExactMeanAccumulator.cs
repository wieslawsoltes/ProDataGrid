// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Numerics;

namespace ProDataGrid.FormulaEngine.Excel
{
    // Exact finite-binary64 sum in units of 2^-1074, followed by one rounded division.
    // 68 words cover the full exponent range plus an Int32-sized observation count.
    // Workspace is borrowed from the caller; no BigInteger, heap payload or shared state.
    internal ref struct ExcelExactMeanAccumulator
    {
        public const int WordCount = 68;
        private Span<uint> _positive;
        private Span<uint> _negative;
        public int Count { get; private set; }

        public ExcelExactMeanAccumulator(Span<uint> workspace)
        {
            _positive = workspace.Slice(0, WordCount);
            _negative = workspace.Slice(WordCount, WordCount);
            _positive.Clear();
            _negative.Clear();
            Count = 0;
        }

        // Caller has validated finite values and the maximum observation count.
        public void Add(double value)
        {
            Count++;
            var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            var mantissa = bits & 0x000fffffffffffffUL;
            var exponent = (int)((bits >> 52) & 0x7ff);
            var shift = 0;
            if (exponent != 0) { mantissa |= 1UL << 52; shift = exponent - 1; }
            if (mantissa == 0) return;
            var words = (bits >> 63) == 0 ? _positive : _negative;
            var index = shift >> 5;
            var offset = shift & 31;
            var low = mantissa << offset;
            var high = offset == 0 ? 0 : mantissa >> (64 - offset);
            var sum = (ulong)words[index] + (uint)low;
            words[index] = (uint)sum;
            sum = (sum >> 32) + words[index + 1] + (low >> 32);
            words[index + 1] = (uint)sum;
            sum = (sum >> 32) + words[index + 2] + high;
            words[index + 2] = (uint)sum;
            index += 3;
            while ((sum >>= 32) != 0)
            {
                sum += words[index];
                words[index++] = (uint)sum;
            }
        }

        public double Mean()
        {
            if (Count == 0) throw new InvalidOperationException("A mean requires observations.");
            var comparison = 0;
            for (var i = WordCount - 1; i >= 0; i--)
                if (_positive[i] != _negative[i]) { comparison = _positive[i] > _negative[i] ? 1 : -1; break; }
            if (comparison == 0) return 0;
            var larger = comparison > 0 ? _positive : _negative;
            var smaller = comparison > 0 ? _negative : _positive;
            Span<uint> quotient = stackalloc uint[WordCount];
            ulong borrow = 0;
            var top = 0;
            for (var i = 0; i < WordCount; i++)
            {
                var subtrahend = (ulong)smaller[i] + borrow;
                var minuend = (ulong)larger[i];
                quotient[i] = unchecked((uint)(minuend - subtrahend));
                borrow = minuend < subtrahend ? 1UL : 0UL;
                if (quotient[i] != 0) top = i;
            }
            // Long division retains the remainder even below the smallest subnormal unit.
            ulong remainder = 0;
            var divisor = (uint)Count;
            for (var i = top; i >= 0; i--)
            {
                var word = (remainder << 32) | quotient[i];
                quotient[i] = (uint)(word / divisor);
                remainder = word % divisor;
            }
            while (top > 0 && quotient[top] == 0) top--;
            var length = top * 32 + 32 - BitOperations.LeadingZeroCount(quotient[top]);
            var shift = Math.Max(0, length - 53);
            var index = shift >> 5;
            var offset = shift & 31;
            var significand = ((ulong)quotient[index] >> offset) | ((ulong)quotient[index + 1] << (32 - offset));
            if (offset != 0) significand |= (ulong)quotient[index + 2] << (64 - offset);
            significand &= (1UL << 53) - 1;
            bool roundUp;
            if (shift == 0)
            {
                var twiceRemainder = remainder * 2;
                roundUp = twiceRemainder > divisor || twiceRemainder == divisor && (significand & 1) != 0;
            }
            else
            {
                var guardPosition = shift - 1;
                var guardWord = guardPosition >> 5;
                var guardOffset = guardPosition & 31;
                var guard = (quotient[guardWord] & (1u << guardOffset)) != 0;
                var sticky = remainder != 0 || (quotient[guardWord] & ((1u << guardOffset) - 1)) != 0;
                for (var i = 0; !sticky && i < guardWord; i++) sticky = quotient[i] != 0;
                roundUp = guard && (sticky || (significand & 1) != 0);
            }
            if (roundUp) significand++;
            var result = Math.ScaleB((double)significand, shift - 1074);
            return comparison > 0 ? result : -result;
        }
    }
}
