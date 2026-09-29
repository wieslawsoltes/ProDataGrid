// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Numerics;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelRadixUtilities
    {
        private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static int Digit(char character)
        {
            if (character >= '0' && character <= '9') return character - '0';
            if (character >= 'A' && character <= 'Z') return character - 'A' + 10;
            if (character >= 'a' && character <= 'z') return character - 'a' + 10;
            return -1;
        }

        public static int SignedBits(int radix) => radix == 2 ? 10 : radix == 8 ? 30 : 40;

        public static bool TrySigned(FormulaValue value, int radix, out long result, out FormulaError error)
        {
            result = 0;
            if (!FormulaCoercion.TryCoerceToText(value, out var text, out error)) return false;
            if (text.Length > 10)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            ulong raw = 0;
            foreach (var character in text)
            {
                var digit = Digit(character);
                if (digit < 0 || digit >= radix)
                {
                    error = new FormulaError(FormulaErrorType.Num);
                    return false;
                }
                raw = raw * (uint)radix + (uint)digit;
            }
            var bits = SignedBits(radix);
            result = (raw & (1UL << (bits - 1))) == 0 ? (long)raw : (long)raw - (1L << bits);
            return true;
        }

        public static int DigitCount(ulong value, int radix)
        {
            var count = 1;
            while (value >= (uint)radix) { value /= (uint)radix; count++; }
            return count;
        }

        // Every caller bounds the width at 255 before entering this method.
        // The only managed allocation is the final owned output string.
        public static string Format(ulong value, int radix, int width)
        {
            Span<char> buffer = stackalloc char[255];
            var output = buffer.Slice(0, width);
            output.Fill('0');
            var index = width;
            do
            {
                output[--index] = Digits[(int)(value % (uint)radix)];
                value /= (uint)radix;
            } while (value != 0);
            return new string(output);
        }

        public static bool TryUnsignedDouble(FormulaValue value, int radix, out double result, out FormulaError error)
        {
            result = 0;
            if (!FormulaCoercion.TryCoerceToText(value, out var text, out error)) return false;
            if (text.Length > 255)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }

            // Keep common inputs entirely in UInt64. Promote to bounded stack limbs
            // only when that exact accumulator would overflow. 255 base-36 digits
            // fit in 42 UInt32 limbs; the input length is checked first. Computing
            // the cutoff once avoids integer division at every parsed character.
            var radixValue = (uint)radix;
            var cutoff = ulong.MaxValue / radixValue;
            var finalDigit = ulong.MaxValue % radixValue;
            ulong small = 0;
            var cursor = 0;
            for (; cursor < text.Length; cursor++)
            {
                var digit = Digit(text[cursor]);
                if (digit < 0 || digit >= radix)
                {
                    error = new FormulaError(FormulaErrorType.Num);
                    return false;
                }
                if (small > cutoff || (small == cutoff && (uint)digit > finalDigit)) break;
                small = small * radixValue + (uint)digit;
            }
            if (cursor == text.Length)
            {
                result = RoundUnsigned(small);
                return true;
            }

            Span<uint> limbs = stackalloc uint[42];
            limbs.Clear();
            limbs[0] = (uint)small;
            limbs[1] = (uint)(small >> 32);
            var used = limbs[1] == 0 ? 1 : 2;
            for (; cursor < text.Length; cursor++)
            {
                var digit = Digit(text[cursor]);
                if (digit < 0 || digit >= radix)
                {
                    error = new FormulaError(FormulaErrorType.Num);
                    return false;
                }
                ulong carry = (uint)digit;
                for (var i = 0; i < used; i++)
                {
                    var product = (ulong)limbs[i] * radixValue + carry;
                    limbs[i] = (uint)product;
                    carry = product >> 32;
                }
                if (carry != 0) limbs[used++] = (uint)carry;
            }

            var bits = (used - 1) * 32 + 32 - BitOperations.LeadingZeroCount(limbs[used - 1]);
            var discarded = bits - 53;
            ulong significand = 0;
            for (var bit = bits - 1; bit >= discarded; bit--)
                significand = (significand << 1) | ((limbs[bit / 32] >> (bit % 32)) & 1u);

            var halfwayBit = discarded - 1;
            var halfwaySet = (limbs[halfwayBit / 32] & (1u << (halfwayBit % 32))) != 0;
            if (halfwaySet)
            {
                var sticky = false;
                for (var i = 0; i < halfwayBit / 32; i++) sticky |= limbs[i] != 0;
                var remainderBits = halfwayBit % 32;
                if (remainderBits != 0)
                    sticky |= (limbs[halfwayBit / 32] & ((1u << remainderBits) - 1)) != 0;
                if (sticky || (significand & 1) != 0) significand++;
            }
            // Perform one round-to-nearest, ties-to-even conversion. Repeated double
            // multiply/add would accumulate avoidable rounding at each input digit.
            result = Math.ScaleB((double)significand, discarded);
            if (!double.IsFinite(result))
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            return true;
        }

        private static double RoundUnsigned(ulong value)
        {
            if (value <= 9007199254740991UL) return value;
            // Round the integer before conversion. An implicit high-bit UInt64 to
            // double cast can have platform-dependent double-rounding behavior.
            // The remaining significand is always exactly representable as double.
            var discarded = 64 - BitOperations.LeadingZeroCount(value) - 53;
            var significand = value >> discarded;
            var remainder = value & ((1UL << discarded) - 1);
            var halfway = 1UL << (discarded - 1);
            if (remainder > halfway || (remainder == halfway && (significand & 1) != 0)) significand++;
            return Math.ScaleB((double)significand, discarded);
        }
    }
}
