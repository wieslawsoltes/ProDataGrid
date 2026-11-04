// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaValueStorageTests
    {
        [Fact]
        public void Value_Storage_Is_Compact_Without_Unsafe_Reference_Overlays()
        {
            Assert.InRange(Unsafe.SizeOf<FormulaValue>(), 1, 32);
            Assert.True(RuntimeHelpers.IsReferenceOrContainsReferences<FormulaValue>());
            Assert.Equal(FormulaValueKind.Blank, default(FormulaValue).Kind);
            Assert.Equal(FormulaValue.Blank, default(FormulaValue));
        }

        [Fact]
        public void Every_Value_Kind_RoundTrips_And_Preserves_Equality_Hashing_And_Access_Validation()
        {
            var reference = new FormulaReference(new FormulaReferenceAddress(FormulaReferenceMode.A1, 19, 27, true, false,
                new FormulaSheetReference("Book1", "A sheet")));
            var array = new FormulaArray(2, 3);
            var error = new FormulaError(FormulaErrorType.Ref, "preserve message");
            var values = new[] { FormulaValue.Blank, FormulaValue.FromNumber(-123.75), FormulaValue.FromText("text"),
                FormulaValue.FromBoolean(true), FormulaValue.FromError(error), FormulaValue.FromArray(array), FormulaValue.FromReference(reference) };
            var copies = new[] { default(FormulaValue), FormulaValue.FromNumber(-123.75), FormulaValue.FromText(new string("text".ToCharArray())),
                FormulaValue.FromBoolean(true), FormulaValue.FromError(error), FormulaValue.FromArray(array), FormulaValue.FromReference(reference) };
            var dictionary = new Dictionary<FormulaValue, int>();
            for (var i = 0; i < values.Length; i++)
            {
                dictionary.Add(values[i], i);
                Assert.Equal(values[i], copies[i]);
                Assert.Equal(values[i].GetHashCode(), copies[i].GetHashCode());
                Assert.Equal(i, dictionary[copies[i]]);
                Assert.True(values[i] == copies[i]);
                Assert.False(values[i] != copies[i]);
                Assert.True(values[i].Equals((object)copies[i]));
                Assert.False(values[i].Equals(null));
                Assert.False(values[i].Equals("unrelated"));
                Assert.Equal(values[i].ToString(), copies[i].ToString());
                for (var j = 0; j < values.Length; j++) if (i != j) Assert.NotEqual(values[i], values[j]);
                if (values[i].Kind != FormulaValueKind.Number) Assert.Throws<InvalidOperationException>(() => values[i].AsNumber());
                if (values[i].Kind != FormulaValueKind.Text) Assert.Throws<InvalidOperationException>(() => values[i].AsText());
                if (values[i].Kind != FormulaValueKind.Boolean) Assert.Throws<InvalidOperationException>(() => values[i].AsBoolean());
                if (values[i].Kind != FormulaValueKind.Error) Assert.Throws<InvalidOperationException>(() => values[i].AsError());
                if (values[i].Kind != FormulaValueKind.Array) Assert.Throws<InvalidOperationException>(() => values[i].AsArray());
                if (values[i].Kind != FormulaValueKind.Reference) Assert.Throws<InvalidOperationException>(() => values[i].AsReference());
            }
            Assert.Equal(-123.75, values[1].AsNumber());
            Assert.Equal("text", values[2].AsText());
            Assert.True(values[3].AsBoolean());
            Assert.Equal(error, values[4].AsError());
            Assert.Same(array, values[5].AsArray());
            Assert.Equal(reference, values[6].AsReference());
            Assert.NotEqual(values[5], FormulaValue.FromArray(new FormulaArray(2, 3)));
            Assert.Throws<ArgumentNullException>(() => FormulaValue.FromText(null!));
            Assert.Throws<ArgumentNullException>(() => FormulaValue.FromArray(null!));
        }

        [Fact]
        public void Error_Metadata_And_Default_Reference_Are_Lossless()
        {
            foreach (var type in Enum.GetValues<FormulaErrorType>())
            {
                foreach (var message in new string?[] { null, "", "diagnostic" })
                {
                    var error = new FormulaError(type, message);
                    var value = FormulaValue.FromError(error);
                    Assert.Equal(error, value.AsError());
                    Assert.Equal(message, value.AsError().Message);
                    Assert.Equal(error.Code, value.ToString());
                }
            }
            foreach (var invalid in new[] { int.MinValue, int.MaxValue, -1 })
            {
                var error = new FormulaError((FormulaErrorType)invalid, "unknown");
                Assert.Equal(error, FormulaValue.FromError(error).AsError());
            }
            Assert.NotEqual(FormulaValue.FromError(new FormulaError(FormulaErrorType.NA)), FormulaValue.FromError(new FormulaError(FormulaErrorType.NA, "")));
            Assert.Equal(default(FormulaReference), FormulaValue.FromReference(default).AsReference());
        }

        [Fact]
        public void Numeric_Bits_And_Legacy_NaN_And_Zero_Equality_Are_Preserved()
        {
            var random = new Random(90217);
            for (var i = 0; i < 10000; i++)
            {
                var bits = random.NextInt64();
                if ((i & 1) != 0) bits = ~bits;
                var number = BitConverter.Int64BitsToDouble(bits);
                var value = FormulaValue.FromNumber(number);
                Assert.Equal(bits, BitConverter.DoubleToInt64Bits(value.AsNumber()));
                Assert.Equal(LegacyHash(FormulaValueKind.Number, number.GetHashCode()), value.GetHashCode());
            }
            Assert.Equal(FormulaValue.FromNumber(0), FormulaValue.FromNumber(-0d));
            Assert.Equal(FormulaValue.FromNumber(double.NaN), FormulaValue.FromNumber(-double.NaN));
            Assert.NotEqual(FormulaValue.FromBoolean(false), FormulaValue.FromNumber(0));
            Assert.NotEqual(FormulaValue.FromBoolean(true), FormulaValue.FromNumber(1));
            foreach (var boolean in new[] { true, false })
                Assert.Equal(LegacyHash(FormulaValueKind.Boolean, boolean.GetHashCode()), FormulaValue.FromBoolean(boolean).GetHashCode());
        }

        [Fact]
        public void Values_Remain_Valid_After_Moving_Collection_And_Array_Copy()
        {
            var source = new FormulaArray(1000, 1);
            for (var i = 0; i < 1000; i++)
            {
                var reference = new FormulaReference(new FormulaReferenceAddress(FormulaReferenceMode.R1C1, i, -i, false, false,
                    new FormulaSheetReference(null, "Sheet " + i)));
                source[i, 0] = FormulaValue.FromReference(reference);
            }
            var copy = new FormulaValue[1000];
            for (var i = 0; i < copy.Length; i++) copy[i] = source[i, 0];
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            for (var i = 0; i < copy.Length; i++)
            {
                Assert.Equal(source[i, 0], copy[i]);
                Assert.Equal(source[i, 0].AsReference(), copy[i].AsReference());
            }
        }

        [Fact]
        public void Common_Factories_Do_Not_Allocate_Payload_Wrappers()
        {
            var array = new FormulaArray(1, 1);
            var error = new FormulaError(FormulaErrorType.NA, "diagnostic");
            const string text = "payload";
            for (var i = 0; i < 100; i++) ExerciseFactories(array, error, text);
            var start = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0;
            for (var i = 0; i < 1000; i++) checksum += ExerciseFactories(array, error, text);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.Equal(9000, checksum);
            Assert.True(allocated < 4096, $"Common factory allocation: {allocated} bytes");
        }

        private static int ExerciseFactories(FormulaArray array, FormulaError error, string text)
        {
            return (int)FormulaValue.FromNumber(1).AsNumber() +
                (FormulaValue.FromBoolean(true).AsBoolean() ? 1 : 0) +
                FormulaValue.FromText(text).AsText().Length +
                (FormulaValue.FromArray(array).AsArray() == array ? 0 : 1) +
                (FormulaValue.FromError(error).AsError() == error ? 0 : 1);
        }

        private static int LegacyHash(FormulaValueKind kind, int payloadHash)
        {
            unchecked { return ((17 * 31 + kind.GetHashCode()) * 31) + payloadHash; }
        }
    }
}
