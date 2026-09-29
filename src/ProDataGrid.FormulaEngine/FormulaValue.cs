// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    public enum FormulaValueKind
    {
        Blank,
        Number,
        Text,
        Boolean,
        Error,
        Array,
        Reference,
        Lambda
    }

    public sealed class FormulaArray
    {
        private readonly FormulaValue[,] _values;
        private readonly bool[,]? _present;

        public FormulaArray(int rows, int columns, FormulaCellAddress? origin = null, bool sparse = false)
        {
            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows));
            }
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            _values = new FormulaValue[rows, columns];
            _present = sparse ? new bool[rows, columns] : null;
            Origin = origin;
        }

        public FormulaCellAddress? Origin { get; }

        public int RowCount => _values.GetLength(0);

        public int ColumnCount => _values.GetLength(1);

        public bool HasMask => _present != null;

        public FormulaValue this[int row, int column]
        {
            get => _values[row, column];
            set
            {
                _values[row, column] = value;
                if (_present != null)
                {
                    _present[row, column] = true;
                }
            }
        }

        public bool IsPresent(int row, int column)
        {
            return _present == null || _present[row, column];
        }

        public void SetValue(int row, int column, FormulaValue value, bool present)
        {
            _values[row, column] = value;
            if (_present != null)
            {
                _present[row, column] = present;
            }
        }

        public IEnumerable<FormulaValue> Flatten()
        {
            for (var row = 0; row < RowCount; row++)
            {
                for (var column = 0; column < ColumnCount; column++)
                {
                    if (_present == null || _present[row, column])
                    {
                        yield return _values[row, column];
                    }
                }
            }
        }
    }

    /// <summary>
    /// An immutable tagged value used by formula evaluation and array storage.
    /// </summary>
    /// <remarks>
    /// Numeric, Boolean and error metadata share the numeric slot. Text, array and
    /// reference payloads share one managed-reference slot. Reference descriptors are
    /// boxed on construction; other factories do not allocate a payload wrapper.
    /// The private physical layout is not a serialization or interop contract.
    /// </remarks>
    public readonly partial struct FormulaValue : IEquatable<FormulaValue>
    {
        private readonly double _number;
        private readonly object? _payload;

        private FormulaValue(FormulaValueKind kind, double number = 0, object? payload = null)
        {
            Kind = kind;
            _number = number;
            _payload = payload;
        }

        public FormulaValueKind Kind { get; }

        public static FormulaValue Blank => default;

        public static FormulaValue FromNumber(double value) => new(FormulaValueKind.Number, number: value);

        public static FormulaValue FromText(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return new FormulaValue(FormulaValueKind.Text, payload: value);
        }

        public static FormulaValue FromBoolean(bool value) => new(FormulaValueKind.Boolean, number: value ? 1 : 0);

        public static FormulaValue FromError(FormulaError error)
            => new(FormulaValueKind.Error, number: (int)error.Type, payload: error.Message);

        public static FormulaValue FromArray(FormulaArray array)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            return new FormulaValue(FormulaValueKind.Array, payload: array);
        }

        public static FormulaValue FromReference(FormulaReference reference)
            => new(FormulaValueKind.Reference, payload: reference);

        public double AsNumber()
        {
            if (Kind != FormulaValueKind.Number)
            {
                throw new InvalidOperationException($"Cannot access {Kind} as number.");
            }
            return _number;
        }

        public bool AsBoolean()
        {
            if (Kind != FormulaValueKind.Boolean)
            {
                throw new InvalidOperationException($"Cannot access {Kind} as boolean.");
            }
            return _number != 0;
        }

        public string AsText()
        {
            if (Kind != FormulaValueKind.Text)
            {
                throw new InvalidOperationException($"Cannot access {Kind} as text.");
            }
            return (string?)_payload ?? string.Empty;
        }

        public FormulaError AsError()
        {
            if (Kind != FormulaValueKind.Error)
            {
                throw new InvalidOperationException($"Cannot access {Kind} as error.");
            }
            return new FormulaError((FormulaErrorType)(int)_number, (string?)_payload);
        }

        public FormulaArray AsArray()
        {
            if (Kind != FormulaValueKind.Array)
            {
                throw new InvalidOperationException($"Cannot access {Kind} as array.");
            }
            return (FormulaArray)_payload!;
        }

        public FormulaReference AsReference()
        {
            if (Kind != FormulaValueKind.Reference)
            {
                throw new InvalidOperationException($"Cannot access {Kind} as reference.");
            }
            return (FormulaReference)_payload!;
        }

        public bool Equals(FormulaValue other)
        {
            if (Kind != other.Kind)
            {
                return false;
            }
            return Kind switch
            {
                FormulaValueKind.Blank => true,
                FormulaValueKind.Number => _number.Equals(other._number),
                FormulaValueKind.Text => string.Equals((string?)_payload, (string?)other._payload, StringComparison.Ordinal),
                FormulaValueKind.Boolean => _number.Equals(other._number),
                FormulaValueKind.Error => AsError().Equals(other.AsError()),
                FormulaValueKind.Array => ReferenceEquals(_payload, other._payload),
                FormulaValueKind.Lambda => ReferenceEquals(_payload, other._payload),
                FormulaValueKind.Reference => AsReference().Equals(other.AsReference()),
                _ => false
            };
        }

        public override bool Equals(object? obj) => obj is FormulaValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Kind.GetHashCode();
                switch (Kind)
                {
                    case FormulaValueKind.Number:
                        hash = (hash * 31) + _number.GetHashCode();
                        break;
                    case FormulaValueKind.Text:
                        hash = (hash * 31) + (_payload?.GetHashCode() ?? 0);
                        break;
                    case FormulaValueKind.Boolean:
                        hash = (hash * 31) + AsBoolean().GetHashCode();
                        break;
                    case FormulaValueKind.Error:
                        hash = (hash * 31) + AsError().GetHashCode();
                        break;
                    case FormulaValueKind.Lambda:
                    case FormulaValueKind.Array:
                        hash = (hash * 31) + (_payload?.GetHashCode() ?? 0);
                        break;
                    case FormulaValueKind.Reference:
                        hash = (hash * 31) + AsReference().GetHashCode();
                        break;
                }
                return hash;
            }
        }

        public override string ToString()
        {
            return Kind switch
            {
                FormulaValueKind.Blank => string.Empty,
                FormulaValueKind.Number => _number.ToString(),
                FormulaValueKind.Text => (string?)_payload ?? string.Empty,
                FormulaValueKind.Boolean => AsBoolean() ? "TRUE" : "FALSE",
                FormulaValueKind.Error => AsError().ToString(),
                FormulaValueKind.Array => $"Array({AsArray().RowCount}x{AsArray().ColumnCount})",
                FormulaValueKind.Reference => AsReference().ToString(),
                FormulaValueKind.Lambda => "#CALC!",
                _ => string.Empty
            };
        }

        public static bool operator ==(FormulaValue left, FormulaValue right) => left.Equals(right);

        public static bool operator !=(FormulaValue left, FormulaValue right) => !left.Equals(right);
    }
}
