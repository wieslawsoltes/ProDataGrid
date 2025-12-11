// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Data;
using System;
using System.Collections.Generic;

namespace Avalonia.Controls
{
    /// <summary>
    /// Validation handling
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    partial class DataGrid
    {

        //TODO Validation UI
        private void ResetValidationStatus()
        {
            // Clear the invalid status of the Cell, Row and DataGrid
            if (EditingRow != null)
            {
                EditingRow.IsValid = true;
                if (EditingRow.Index != -1)
                {
                    foreach (DataGridCell cell in EditingRow.Cells)
                    {
                        if (!cell.IsValid)
                        {
                            cell.IsValid = true;
                            cell.UpdatePseudoClasses();
                        }
                    }
                    EditingRow.ApplyState();
                }
            }
            IsValid = true;

            _validationSubscription?.Dispose();
            _validationSubscription = null;
        }

        private List<Exception> _bindingValidationErrors;

        private IDisposable _validationSubscription;

        private bool _isValid = true;


        public bool IsValid
        {
            get { return _isValid; }
            internal set
            {
                SetAndRaise(IsValidProperty, ref _isValid, value);
                PseudoClassesHelper.Set(PseudoClasses, ":invalid", !value);
            }
        }

    }
}
