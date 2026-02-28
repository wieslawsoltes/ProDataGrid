// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Optional per-item cache contract for draw-operation factories.
    /// </summary>
    interface IDataGridCellDrawOperationItemCache
    {
        /// <summary>
        /// Tries to get a previously cached value for the specified slot and key.
        /// </summary>
        /// <param name="cacheSlot">Factory-defined cache slot.</param>
        /// <param name="cacheKey">Factory-defined cache key.</param>
        /// <param name="value">Cached value when found.</param>
        /// <returns><c>true</c> when a cached value was found; otherwise <c>false</c>.</returns>
        bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value);

        /// <summary>
        /// Stores a cached value for the specified slot and key.
        /// </summary>
        /// <param name="cacheSlot">Factory-defined cache slot.</param>
        /// <param name="cacheKey">Factory-defined cache key.</param>
        /// <param name="value">Cached value.</param>
        void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value);
    }
}
