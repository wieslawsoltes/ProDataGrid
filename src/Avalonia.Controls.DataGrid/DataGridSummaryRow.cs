// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using System.Collections.Generic;

namespace Avalonia.Controls
{
    /// <summary>
    /// A row that displays summary values for columns.
    /// </summary>
    [TemplatePart(DATAGRID_SUMMARYROW_elementCellsPresenter, typeof(DataGridSummaryCellsPresenter))]
    [PseudoClasses(":total", ":group")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridSummaryRow : TemplatedControl
    {
        private const string DATAGRID_SUMMARYROW_elementCellsPresenter = "PART_CellsPresenter";

        private DataGridSummaryCellsPresenter _cellsPresenter;
        private DataGrid _owningGrid;
        private List<DataGridSummaryCell> _cells;
        private bool _applyHorizontalOffset = true;

        /// <summary>
        /// Identifies the <see cref="Scope"/> property.
        /// </summary>
        public static readonly StyledProperty<DataGridSummaryScope> ScopeProperty =
            AvaloniaProperty.Register<DataGridSummaryRow, DataGridSummaryScope>(
                nameof(Scope),
                defaultValue: DataGridSummaryScope.Total);

        /// <summary>
        /// Identifies the <see cref="Group"/> property.
        /// </summary>
        public static readonly StyledProperty<DataGridCollectionViewGroup> GroupProperty =
            AvaloniaProperty.Register<DataGridSummaryRow, DataGridCollectionViewGroup>(nameof(Group));

        /// <summary>
        /// Identifies the <see cref="Level"/> property.
        /// </summary>
        public static readonly StyledProperty<int> LevelProperty =
            AvaloniaProperty.Register<DataGridSummaryRow, int>(nameof(Level));

        static DataGridSummaryRow()
        {
            ScopeProperty.Changed.AddClassHandler<DataGridSummaryRow>((x, e) => x.OnScopeChanged(e));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridSummaryRow"/> class.
        /// </summary>
        public DataGridSummaryRow()
        {
            _cells = new List<DataGridSummaryCell>();
        }

        /// <summary>
        /// Gets the owning DataGrid.
        /// </summary>
        public DataGrid OwningGrid
        {
            get => _owningGrid;
            internal set
            {
                _owningGrid = value;
                if (_cellsPresenter != null)
                {
                    _cellsPresenter.OwningGrid = value;
                    _cellsPresenter.OwnerRow = value == null ? null : this;
                }
            }
        }

        /// <summary>
        /// Gets or sets the summary scope (Total or Group).
        /// </summary>
        public DataGridSummaryScope Scope
        {
            get => GetValue(ScopeProperty);
            set => SetValue(ScopeProperty, value);
        }

        /// <summary>
        /// Gets or sets the associated group (for group summaries).
        /// </summary>
        public DataGridCollectionViewGroup Group
        {
            get => GetValue(GroupProperty);
            set => SetValue(GroupProperty, value);
        }

        /// <summary>
        /// Gets or sets the level (for nested groups).
        /// </summary>
        public int Level
        {
            get => GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        /// <summary>
        /// Gets the collection of summary cells.
        /// </summary>
        public IReadOnlyList<DataGridSummaryCell> Cells => _cells;

        /// <summary>
        /// Gets the cells presenter.
        /// </summary>
        internal DataGridSummaryCellsPresenter CellsPresenter => _cellsPresenter;

        /// <summary>
        /// Gets or sets whether the row should apply the horizontal offset internally.
        /// </summary>
        internal bool ApplyHorizontalOffset
        {
            get => _applyHorizontalOffset;
            set
            {
                if (_applyHorizontalOffset != value)
                {
                    _applyHorizontalOffset = value;
                    UpdateCellLayout();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _cellsPresenter = e.NameScope.Find<DataGridSummaryCellsPresenter>(DATAGRID_SUMMARYROW_elementCellsPresenter);

            if (_cellsPresenter != null)
            {
                _cellsPresenter.OwningGrid = OwningGrid;
                _cellsPresenter.OwnerRow = this;
            }

            UpdatePseudoClasses();
            EnsureCells();
        }

        private void OnScopeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            UpdatePseudoClasses();
        }

        private void UpdatePseudoClasses()
        {
            PseudoClasses.Set(":total", Scope == DataGridSummaryScope.Total);
            PseudoClasses.Set(":group", Scope == DataGridSummaryScope.Group);
        }

        /// <summary>
        /// Ensures cells are created for all columns.
        /// </summary>
        internal void EnsureCells()
        {
            if (OwningGrid == null || _cellsPresenter == null)
            {
                return;
            }

            _cellsPresenter.OwningGrid = OwningGrid;
            _cellsPresenter.OwnerRow = this;

            // Clear existing cells
            foreach (var cell in _cells)
            {
                cell.Detach();
            }
            _cells.Clear();
            _cellsPresenter.Children.Clear();

            // Create cells for each internal column so indices match ColumnsItemsInternal.
            foreach (var column in OwningGrid.ColumnsItemsInternal)
            {
                var cell = new DataGridSummaryCell
                {
                    Column = column,
                    OwningRow = this
                };

                _cells.Add(cell);
                _cellsPresenter.Children.Add(cell);
            }

            Recalculate();
        }

        /// <summary>
        /// Recalculates all summary values.
        /// </summary>
        public void Recalculate()
        {
            if (OwningGrid?.SummaryService == null)
            {
                return;
            }

            foreach (var cell in _cells)
            {
                cell.Recalculate();
            }
        }

        /// <summary>
        /// Updates the layout of cells to match column widths.
        /// </summary>
        internal void UpdateCellLayout()
        {
            _cellsPresenter?.InvalidateMeasure();
            _cellsPresenter?.InvalidateArrange();
        }

        /// <summary>
        /// Called when a column is added.
        /// </summary>
        internal void OnColumnAdded(DataGridColumn column, int index)
        {
            if (_cellsPresenter == null) return;

            var cell = new DataGridSummaryCell
            {
                Column = column,
                OwningRow = this
            };

            _cells.Insert(index, cell);
            _cellsPresenter.Children.Insert(index, cell);
            cell.Recalculate();
        }

        /// <summary>
        /// Called when a column is removed.
        /// </summary>
        internal void OnColumnRemoved(DataGridColumn column)
        {
            if (_cellsPresenter == null) return;

            var cell = _cells.Find(c => c.Column == column);
            if (cell != null)
            {
                cell.Detach();
                _cells.Remove(cell);
                _cellsPresenter.Children.Remove(cell);
            }
        }

        /// <summary>
        /// Called when column order changes.
        /// </summary>
        internal void OnColumnsReordered()
        {
            EnsureCells();
        }

        internal void DetachFromGrid()
        {
            if (_cellsPresenter != null)
            {
                _cellsPresenter.OwningGrid = null;
                _cellsPresenter.OwnerRow = null;
                _cellsPresenter.Children.Clear();
            }

            foreach (var cell in _cells)
            {
                cell.Detach();
                cell.Column = null;
                cell.Description = null;
                cell.Value = null;
            }

            _cells.Clear();
            _owningGrid = null;
            Group = null;
            Level = 0;
        }
    }
}
