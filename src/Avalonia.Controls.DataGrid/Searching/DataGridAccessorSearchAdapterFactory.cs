// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Controls;

namespace Avalonia.Controls.DataGridSearching
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridAccessorSearchAdapterFactory : IDataGridSearchAdapterFactory
    {
        public DataGridSearchAdapter Create(DataGrid grid, ISearchModel model)
        {
            return new DataGridAccessorSearchAdapter(model, () => grid.ColumnsItemsInternal, grid?.FastPathOptions);
        }
    }
}
