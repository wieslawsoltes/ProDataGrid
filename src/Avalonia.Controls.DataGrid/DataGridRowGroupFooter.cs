// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls
{
    /// <summary>
    /// A footer row displayed after group items, showing group summaries.
    /// </summary>
    [TemplatePart(DATAGRID_GROUPFOOTER_elementSummaryRow, typeof(DataGridSummaryRow))]
    [PseudoClasses(":current")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridRowGroupFooter : TemplatedControl
    {
        private const string DATAGRID_GROUPFOOTER_elementSummaryRow = "PART_SummaryRow";

        private DataGridSummaryRow _summaryRow;
        private DataGrid _owningGrid;

        /// <summary>
        /// Identifies the <see cref="Group"/> property.
        /// </summary>
        public static readonly StyledProperty<DataGridCollectionViewGroup> GroupProperty =
            AvaloniaProperty.Register<DataGridRowGroupFooter, DataGridCollectionViewGroup>(nameof(Group));

        /// <summary>
        /// Identifies the <see cref="Level"/> property.
        /// </summary>
        public static readonly StyledProperty<int> LevelProperty =
            AvaloniaProperty.Register<DataGridRowGroupFooter, int>(nameof(Level));

        /// <summary>
        /// Identifies the <see cref="SublevelIndent"/> property.
        /// </summary>
        public static readonly StyledProperty<double> SublevelIndentProperty =
            AvaloniaProperty.Register<DataGridRowGroupFooter, double>(
                nameof(SublevelIndent),
                defaultValue: DataGrid.DATAGRID_defaultRowGroupSublevelIndent);

        static DataGridRowGroupFooter()
        {
            GroupProperty.Changed.AddClassHandler<DataGridRowGroupFooter>((x, e) => x.OnGroupChanged(e));
            LevelProperty.Changed.AddClassHandler<DataGridRowGroupFooter>((x, e) => x.OnLevelChanged(e));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridRowGroupFooter"/> class.
        /// </summary>
        public DataGridRowGroupFooter()
        {
        }

        internal void SetRecycledBounds(Rect bounds)
        {
            Bounds = bounds;
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
                if (_summaryRow != null)
                {
                    _summaryRow.OwningGrid = value;
                    ApplySummaryRowTheme();
                    UpdateSummaryRowOffset();
                    UpdateSummaryRowState();
                }
            }
        }

        /// <summary>
        /// Gets or sets the associated group.
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
        /// Gets or sets the sublevel indent amount.
        /// </summary>
        public double SublevelIndent
        {
            get => GetValue(SublevelIndentProperty);
            set => SetValue(SublevelIndentProperty, value);
        }

        /// <summary>
        /// Gets the row group info associated with this footer.
        /// </summary>
        internal DataGridRowGroupInfo RowGroupInfo { get; set; }

        /// <summary>
        /// Gets the summary row.
        /// </summary>
        internal DataGridSummaryRow SummaryRow => _summaryRow;

        /// <summary>
        /// Gets or sets whether this footer is recycled.
        /// </summary>
        internal bool IsRecycled { get; set; }

        private bool ShouldShowSummaryRow =>
            OwningGrid != null &&
            OwningGrid.ShowGroupSummary &&
            (OwningGrid.GroupSummaryPosition == DataGridGroupSummaryPosition.Footer ||
             OwningGrid.GroupSummaryPosition == DataGridGroupSummaryPosition.Both);

        /// <inheritdoc/>
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _summaryRow = e.NameScope.Find<DataGridSummaryRow>(DATAGRID_GROUPFOOTER_elementSummaryRow);

            if (_summaryRow != null)
            {
                _summaryRow.OwningGrid = OwningGrid;
                _summaryRow.Scope = DataGridSummaryScope.Group;
                _summaryRow.Group = Group;
                _summaryRow.Level = Level;
                ApplySummaryRowTheme();
                UpdateSummaryRowOffset();
                UpdateSummaryRowState();
            }

            UpdateIndent();
        }

        private void OnGroupChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_summaryRow != null)
            {
                _summaryRow.Group = Group;
                if (_summaryRow.IsVisible)
                {
                    _summaryRow.Recalculate();
                }
            }
        }

        private void OnLevelChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_summaryRow != null)
            {
                _summaryRow.Level = Level;
            }
            UpdateIndent();
        }

        private void UpdateIndent()
        {
            // Apply indentation based on level
            double indent = Level * SublevelIndent;
            Margin = new Thickness(indent, 0, 0, 0);
        }

        /// <summary>
        /// Recalculates the summary values.
        /// </summary>
        public void Recalculate()
        {
            _summaryRow?.Recalculate();
        }

        /// <summary>
        /// Updates the layout when columns change.
        /// </summary>
        internal void UpdateCellLayout()
        {
            _summaryRow?.UpdateCellLayout();
        }

        /// <summary>
        /// Ensures cells are created when the footer becomes visible.
        /// </summary>
        internal void EnsureCells()
        {
            _summaryRow?.EnsureCells();
        }

        /// <summary>
        /// Applies the owning grid's summary row theme.
        /// </summary>
        internal void ApplySummaryRowTheme()
        {
            if (_summaryRow == null)
            {
                return;
            }

            if (OwningGrid?.SummaryRowTheme != null)
            {
                _summaryRow.Theme = OwningGrid.SummaryRowTheme;
            }
            else
            {
                _summaryRow.ClearValue(ThemeProperty);
            }
        }

        /// <summary>
        /// Updates summary row visibility and ensures cells are available.
        /// </summary>
        internal void UpdateSummaryRowState()
        {
            if (_summaryRow == null)
            {
                return;
            }

            _summaryRow.IsVisible = ShouldShowSummaryRow;
            if (_summaryRow.IsVisible)
            {
                if (_summaryRow.CellsPresenter != null && OwningGrid != null && _summaryRow.Cells.Count != OwningGrid.ColumnsItemsInternal.Count)
                {
                    _summaryRow.EnsureCells();
                }

                _summaryRow.Recalculate();
            }
        }

        /// <summary>
        /// Updates whether the summary row should apply horizontal offset internally.
        /// </summary>
        internal void UpdateSummaryRowOffset()
        {
            if (_summaryRow == null)
            {
                return;
            }

            _summaryRow.ApplyHorizontalOffset = OwningGrid?.AreRowGroupHeadersFrozen == true;
        }
    }
}
