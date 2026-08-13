// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridNavigation;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        /// <summary>
        /// Creates a framework-neutral route context for the current cell.
        /// </summary>
        /// <param name="origin">The route activation source.</param>
        /// <returns>
        /// A context containing the current item, stable column key, and position, or an empty
        /// context when no data cell is current.
        /// </returns>
        public DataGridRouteContext GetCurrentRouteContext(DataGridRouteNavigationOrigin origin)
        {
            DataGridCellInfo cell = CurrentCell;
            if (!cell.IsValid)
            {
                return new DataGridRouteContext(
                    null,
                    null,
                    null,
                    DataGridNavigationPosition.Unset,
                    origin,
                    hasItem: false);
            }

            object columnKey = cell.Column.ColumnKey;
            if (columnKey == null && !string.IsNullOrWhiteSpace(cell.Column.SortMemberPath))
            {
                columnKey = cell.Column.SortMemberPath;
            }

            return new DataGridRouteContext(
                cell.Item,
                null,
                columnKey,
                new DataGridNavigationPosition(cell.RowIndex, cell.Column.DisplayIndex),
                origin);
        }

        /// <summary>
        /// Resolves and executes an application-route operation for the current cell.
        /// </summary>
        /// <param name="kind">The route or history operation.</param>
        /// <param name="origin">The route activation source.</param>
        /// <param name="cancellationToken">Cancels navigation or a route guard.</param>
        /// <returns>A typed non-throwing route result.</returns>
        public ValueTask<DataGridRouteNavigationResult> NavigateRouteAsync(
            DataGridRouteNavigationKind kind,
            DataGridRouteNavigationOrigin origin = DataGridRouteNavigationOrigin.Programmatic,
            CancellationToken cancellationToken = default)
        {
            IDataGridRouteNavigationModel model = RouteNavigationModel;
            if (model == null)
            {
                return ValueTask.FromResult(DataGridRouteNavigationResult.FromStatus(
                    DataGridRouteNavigationStatus.NotSupported));
            }

            DataGridRouteContext context = kind is DataGridRouteNavigationKind.Back or
                DataGridRouteNavigationKind.Forward
                    ? DataGridRouteContext.Empty
                    : GetCurrentRouteContext(origin);
            return model.NavigateAsync(kind, context, cancellationToken);
        }
    }
}
