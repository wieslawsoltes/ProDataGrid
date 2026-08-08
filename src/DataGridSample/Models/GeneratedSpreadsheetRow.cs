// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using ProDataGrid.SourceGeneration;
using ReactiveUI;

namespace DataGridSample.Models;

[GenerateDataGridIndexedColumns(
    Name = "Cells",
    GetterMethod = nameof(GetCell),
    SetterMethod = nameof(SetCell),
    NotificationNameMethod = nameof(GetCellPropertyName))]
public sealed class GeneratedSpreadsheetRow : ReactiveObject
{
    private readonly object?[] _cells;

    public GeneratedSpreadsheetRow(int rowNumber, int capacity)
    {
        if (rowNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber));
        }
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        RowNumber = rowNumber;
        _cells = new object?[capacity];
    }

    public int RowNumber { get; }

    public int Capacity => _cells.Length;

    public object? GetCell(int index)
    {
        ValidateIndex(index);
        return _cells[index];
    }

    public void SetCell(int index, object? value)
    {
        ValidateIndex(index);
        if (Equals(_cells[index], value))
        {
            return;
        }

        _cells[index] = value;
        this.RaisePropertyChanged(GetCellPropertyName(index));
    }

    public static string GetCellPropertyName(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        int dividend = index + 1;
        Span<char> buffer = stackalloc char[8];
        int position = buffer.Length;
        while (dividend > 0)
        {
            dividend--;
            buffer[--position] = (char)('A' + dividend % 26);
            dividend /= 26;
        }

        return new string(buffer[position..]);
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_cells.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
