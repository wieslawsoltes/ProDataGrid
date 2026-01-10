// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

namespace Avalonia.Controls.DataGridFiltering
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridAccessorFilteringAdapterFactory : IDataGridFilteringAdapterFactory
    {
        public DataGridFilteringAdapter Create(DataGrid grid, IFilteringModel model)
        {
            return new DataGridAccessorFilteringAdapter(model, () => grid.ColumnsItemsInternal, grid?.FastPathOptions);
        }
    }
}
