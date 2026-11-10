// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    // Private open-addressed counts: storage follows distinct values, not sample length.
    // Count == 0 marks an empty slot. Ordinals survive growth and make ties deterministic.
    internal ref struct ExcelModeCounter
    {
        private Span<ModeEntry> _slots;
        private ModeEntry[]? _rented;
        private int _distinct;
        private int _peak;
        private int _modeCount;
        private int _firstOrdinal;
        private double _firstValue;

        public ExcelModeCounter(Span<ModeEntry> initial)
        {
            _slots = initial;
            _slots.Clear();
            _rented = null;
            _distinct = _peak = _modeCount = _firstOrdinal = 0;
            _firstValue = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(double number)
        {
            var slot = FindSlot(_slots, number);
            if (_slots[slot].Count == 0)
            {
                // Check for an existing key before growing: repeated samples need no growth.
                if (_distinct >= _slots.Length / 2)
                {
                    if (!Grow()) return false;
                    slot = FindSlot(_slots, number);
                }
                _slots[slot] = new ModeEntry(number, _distinct++);
            }
            ref var entry = ref _slots[slot];
            entry.Count++;
            if (entry.Count > _peak)
            {
                _peak = entry.Count;
                _modeCount = 1;
                _firstOrdinal = entry.Ordinal;
                _firstValue = entry.Value;
            }
            else if (entry.Count == _peak)
            {
                _modeCount++;
                if (entry.Ordinal < _firstOrdinal)
                {
                    _firstOrdinal = entry.Ordinal;
                    _firstValue = entry.Value;
                }
            }
            return true;
        }

        public FormulaValue Result(FormulaFunctionContext context, bool multiple)
        {
            if (_peak < 2) return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            if (!multiple) return ExcelFunctionUtilities.CreateNumber(context, _firstValue);
            if (!ExcelArrayShapeUtilities.TryCreate(context, _modeCount, 1, out var result, out var error))
                return FormulaValue.FromError(error);
            // No further counting follows publication. Compact matching slots in place,
            // then sort only the winning entries by first appearance, not by numeric value.
            var written = 0;
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i].Count == _peak) _slots[written++] = _slots[i];
            var modes = _slots.Slice(0, written);
            modes.Sort();
            for (var i = 0; i < modes.Length; i++)
                result[i, 0] = ExcelFunctionUtilities.CreateNumber(context, modes[i].Value);
            return FormulaValue.FromArray(result);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool Grow()
        {
            if (_slots.Length > Array.MaxLength / 2) return false;
            var capacity = _slots.Length * 2;
            var rented = ArrayPool<ModeEntry>.Shared.Rent(capacity);
            var next = rented.AsSpan(0, capacity);
            next.Clear();
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i].Count != 0) next[FindSlot(next, _slots[i].Value)] = _slots[i];
            if (_rented != null) ArrayPool<ModeEntry>.Shared.Return(_rented, clearArray: true);
            _rented = rented;
            _slots = next;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindSlot(Span<ModeEntry> slots, double number)
        {
            // Canonical hash for signed zero. NaN/infinity are rejected before insertion.
            var bits = number == 0 ? 0UL : (ulong)BitConverter.DoubleToInt64Bits(number);
            unchecked
            {
                bits = (bits ^ (bits >> 30)) * 0xbf58476d1ce4e5b9UL;
                bits = (bits ^ (bits >> 27)) * 0x94d049bb133111ebUL;
                bits ^= bits >> 31;
            }
            var mask = slots.Length - 1;
            var index = (int)(bits & (uint)mask);
            while (slots[index].Count != 0 && slots[index].Value != number)
                index = (index + 1) & mask;
            return index;
        }

        public void Dispose()
        {
            if (_rented != null) ArrayPool<ModeEntry>.Shared.Return(_rented, clearArray: true);
            _rented = null;
            _slots = default;
        }

        internal struct ModeEntry : IComparable<ModeEntry>
        {
            public readonly double Value;
            public readonly int Ordinal;
            public int Count;
            public ModeEntry(double value, int ordinal) { Value = value; Ordinal = ordinal; Count = 0; }
            public int CompareTo(ModeEntry other) => Ordinal.CompareTo(other.Ordinal);
        }
    }
}
