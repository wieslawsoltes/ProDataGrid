using System.Collections.Generic;
using DataGridSample.Mvvm;

namespace DataGridSample.Models
{
    public class PowerFxSheetRow : ObservableObject
    {
        public PowerFxSheetRow(int rowIndex)
        {
            RowIndex = rowIndex;
            A = new PowerFxSheetCell();
            B = new PowerFxSheetCell();
            C = new PowerFxSheetCell();
            D = new PowerFxSheetCell();
            E = new PowerFxSheetCell();
            F = new PowerFxSheetCell();
        }

        public int RowIndex { get; }

        public PowerFxSheetCell A { get; }

        public PowerFxSheetCell B { get; }

        public PowerFxSheetCell C { get; }

        public PowerFxSheetCell D { get; }

        public PowerFxSheetCell E { get; }

        public PowerFxSheetCell F { get; }

        public PowerFxSheetCell? GetCell(string? columnKey)
        {
            return columnKey switch
            {
                "A" => A,
                "B" => B,
                "C" => C,
                "D" => D,
                "E" => E,
                "F" => F,
                _ => null
            };
        }

        public IEnumerable<(string Key, PowerFxSheetCell Cell)> EnumerateCells()
        {
            yield return ("A", A);
            yield return ("B", B);
            yield return ("C", C);
            yield return ("D", D);
            yield return ("E", E);
            yield return ("F", F);
        }
    }
}
