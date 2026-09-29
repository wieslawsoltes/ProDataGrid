// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed partial class ExcelRadixFunctionTests
    {
        public static IEnumerable<object[]> RoundingCases()
        {
            // Explicit IEEE bits at the exact-integer boundary complement the broad
            // independent oracle; no expectations use production conversion helpers.
            yield return new object[] { "9007199254740991", 10, 4845873199050653695L };
            yield return new object[] { "9007199254740992", 10, 4845873199050653696L };
            yield return new object[] { "9007199254740993", 10, 4845873199050653696L };
            yield return new object[] { "9007199254740995", 10, 4845873199050653698L };
            yield return new object[] { "20000000000001", 16, 4845873199050653696L };
            yield return new object[] { "20000000000003", 16, 4845873199050653698L };

            // Exact integer construction and the runtime's independent decimal parser
            // replace a verbose static fixture table. Include overflowing magnitudes;
            // the test requires #NUM! there, not a leaked Infinity or an exception.
            var random = new Random(620813);
            var lengths = new[] { 1, 9, 16, 17, 31, 53, 64, 97, 127, 191, 254, 255 };
            for (var radix = 2; radix <= 36; radix++)
                foreach (var length in lengths)
                {
                    var chars = new char[length];
                    var integer = BigInteger.Zero;
                    for (var i = 0; i < chars.Length; i++)
                    {
                        var digit = random.Next(i == 0 ? 1 : 0, radix);
                        chars[i] = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[digit];
                        integer = integer * radix + digit;
                    }
                    yield return Case(new string(chars), radix, integer);
                }

            // Even/odd significands one below/on/above halfway. Spacing the
            // significands apart avoids duplicate cases at the smallest exponent.
            foreach (var exponent in new[] { 53, 54, 63, 64, 127, 255, 511, 1023 })
                foreach (var parity in new[] { 2, 5 })
                    for (var delta = -1; delta <= 1; delta++)
                    {
                        var shift = exponent - 52;
                        var integer = (((BigInteger.One << 52) + parity) << shift) +
                            (BigInteger.One << (shift - 1)) + delta;
                        yield return Case(BigIntegerFormat(integer, 36), 36, integer);
                    }
        }

        private static object[] Case(string digits, int radix, BigInteger integer)
        {
            var rounded = double.Parse(integer.ToString(CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture);
            return new object[] { digits, radix, BitConverter.DoubleToInt64Bits(rounded) };
        }
    }
}
