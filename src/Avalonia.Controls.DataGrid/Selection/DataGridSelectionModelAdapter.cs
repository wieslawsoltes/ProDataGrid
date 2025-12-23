// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
// This layer wraps Avalonia's ISelectionModel and will be wired into DataGrid in later steps.

#nullable disable

using System;
using Avalonia.Controls.Selection;

namespace Avalonia.Controls.DataGridSelection
{
    /// <summary>
    /// Factory hook for creating selection models used by the DataGrid.
    /// Implementations can provide custom selection behavior.
    /// </summary>
    public interface IDataGridSelectionModelFactory
    {
        ISelectionModel Create();
    }

    /// <summary>
    /// Default adapter around ISelectionModel that DataGrid will use to manage selection.
    /// </summary>
    public class DataGridSelectionModelAdapter : IDisposable
    {
        public DataGridSelectionModelAdapter(ISelectionModel model)
            : this(model, null, null)
        {
        }

        public DataGridSelectionModelAdapter(
            ISelectionModel model,
            Func<object?, object?>? itemSelector,
            Func<object?, int>? indexResolver)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            SelectedItemsView = itemSelector == null && indexResolver == null
                ? new SelectedItemsView(Model)
                : new SelectedItemsView(Model, itemSelector, indexResolver);
        }

        public ISelectionModel Model { get; }

        public SelectedItemsView SelectedItemsView { get; }

        public bool IsSelected(int index) => Model.IsSelected(index);

        public void Select(int index)
        {
            Model.Select(index);
        }

        public void Deselect(int index)
        {
            Model.Deselect(index);
        }

        public void SelectRange(int start, int end)
        {
            Model.SelectRange(start, end);
        }

        public void DeselectRange(int start, int end)
        {
            Model.DeselectRange(start, end);
        }

        public void Clear()
        {
            Model.Clear();
        }

        public void Dispose()
        {
            SelectedItemsView.Dispose();
        }
    }
}
