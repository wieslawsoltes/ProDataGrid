// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Utilities;
using System.Globalization;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// A cell that displays a summary value.
    /// </summary>
    [PseudoClasses(":sum", ":average", ":count", ":min", ":max", ":custom", ":none")]
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridSummaryCell : ContentControl
    {
        private DataGridColumn _column;
        private DataGrid _owningGrid;
        private DataGridSummaryRow _owningRow;
        private DataGridSummaryDescription _description;

        /// <summary>
        /// Identifies the <see cref="Value"/> property.
        /// </summary>
        public static readonly StyledProperty<object> ValueProperty =
            AvaloniaProperty.Register<DataGridSummaryCell, object>(nameof(Value));

        /// <summary>
        /// Identifies the <see cref="Description"/> property.
        /// </summary>
        public static readonly StyledProperty<DataGridSummaryDescription> DescriptionProperty =
            AvaloniaProperty.Register<DataGridSummaryCell, DataGridSummaryDescription>(nameof(Description));

        /// <summary>
        /// Identifies the <see cref="DisplayText"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGridSummaryCell, string> DisplayTextProperty =
            AvaloniaProperty.RegisterDirect<DataGridSummaryCell, string>(
                nameof(DisplayText),
                o => o.DisplayText);

        private string _displayText;

        static DataGridSummaryCell()
        {
            ValueProperty.Changed.AddClassHandler<DataGridSummaryCell>((x, e) => x.OnValueChanged());
            DescriptionProperty.Changed.AddClassHandler<DataGridSummaryCell>((x, e) => x.OnDescriptionChanged());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridSummaryCell"/> class.
        /// </summary>
        public DataGridSummaryCell()
        {
        }

        /// <summary>
        /// Gets or sets the owning column.
        /// </summary>
        public DataGridColumn Column
        {
            get => _column;
            internal set
            {
                if (_column != null)
                {
                    WeakEventHandlerManager.Unsubscribe<AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                        _column,
                        nameof(AvaloniaObject.PropertyChanged),
                        OnColumnPropertyChanged);
                }

                _column = value;

                if (_column != null)
                {
                    WeakEventHandlerManager.Subscribe<AvaloniaObject, AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                        _column,
                        nameof(AvaloniaObject.PropertyChanged),
                        OnColumnPropertyChanged);
                }

                ApplySummaryCellTheme();
                ApplySummaryCellAlignment();
            }
        }

        /// <summary>
        /// Gets or sets the owning summary row.
        /// </summary>
        internal DataGridSummaryRow OwningRow
        {
            get => _owningRow;
            set
            {
                if (ReferenceEquals(_owningRow, value))
                {
                    return;
                }

                _owningRow = value;
                UpdateOwningGrid(_owningRow?.OwningGrid);
            }
        }

        /// <summary>
        /// Gets or sets the calculated summary value.
        /// </summary>
        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Gets or sets the summary description.
        /// </summary>
        public DataGridSummaryDescription Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>
        /// Gets the formatted display text.
        /// </summary>
        public string DisplayText
        {
            get => _displayText;
            private set => SetAndRaise(DisplayTextProperty, ref _displayText, value);
        }

        private void OnValueChanged()
        {
            UpdateDisplayText();
        }

        private void OnDescriptionChanged()
        {
            if (_description != null)
            {
                WeakEventHandlerManager.Unsubscribe<AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                    _description,
                    nameof(AvaloniaObject.PropertyChanged),
                    OnDescriptionPropertyChanged);
            }

            _description = Description;

            if (_description != null)
            {
                WeakEventHandlerManager.Subscribe<AvaloniaObject, AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                    _description,
                    nameof(AvaloniaObject.PropertyChanged),
                    OnDescriptionPropertyChanged);
            }

            UpdatePseudoClasses();
            UpdateDisplayText();
            UpdateContentTemplate();
        }

        private void OnDescriptionPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            UpdatePseudoClasses();
            UpdateDisplayText();
            UpdateContentTemplate();
        }

        private void OnColumnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataGridColumn.SummaryCellThemeProperty)
            {
                ApplySummaryCellTheme();
            }
            else if (e.Property == DataGridColumn.SummaryCellHorizontalContentAlignmentProperty
                || e.Property == DataGridColumn.SummaryCellVerticalContentAlignmentProperty)
            {
                ApplySummaryCellAlignment();
            }
        }

        private void OnGridPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataGrid.SummaryCellThemeProperty)
            {
                ApplySummaryCellTheme();
            }
            else if (e.Property == DataGrid.SummaryCellHorizontalContentAlignmentProperty
                || e.Property == DataGrid.SummaryCellVerticalContentAlignmentProperty)
            {
                ApplySummaryCellAlignment();
            }
        }

        private void UpdatePseudoClasses()
        {
            var aggregateType = Description?.AggregateType ?? DataGridAggregateType.None;

            PseudoClasses.Set(":none", aggregateType == DataGridAggregateType.None);
            PseudoClasses.Set(":sum", aggregateType == DataGridAggregateType.Sum);
            PseudoClasses.Set(":average", aggregateType == DataGridAggregateType.Average);
            PseudoClasses.Set(":count", aggregateType == DataGridAggregateType.Count || aggregateType == DataGridAggregateType.CountDistinct);
            PseudoClasses.Set(":min", aggregateType == DataGridAggregateType.Min);
            PseudoClasses.Set(":max", aggregateType == DataGridAggregateType.Max);
            PseudoClasses.Set(":custom", aggregateType == DataGridAggregateType.Custom);
        }

        private void UpdateDisplayText()
        {
            if (Description != null)
            {
                var culture = OwningRow?.OwningGrid?.CollectionView?.Culture ?? CultureInfo.CurrentCulture;
                DisplayText = Description.FormatValue(Value, culture);
            }
            else
            {
                DisplayText = Value?.ToString() ?? string.Empty;
            }

            Content = ContentTemplate == null ? DisplayText : Value;
        }

        private void UpdateContentTemplate()
        {
            if (Description?.ContentTemplate != null)
            {
                ContentTemplate = Description.ContentTemplate;
                Content = Value;
            }
            else
            {
                ContentTemplate = null;
                Content = DisplayText;
            }
        }

        private void ApplySummaryCellTheme()
        {
            if (Column?.SummaryCellTheme != null)
            {
                Theme = Column.SummaryCellTheme;
            }
            else if (_owningGrid?.SummaryCellTheme != null)
            {
                Theme = _owningGrid.SummaryCellTheme;
            }
            else
            {
                ClearValue(ThemeProperty);
            }
        }

        private void ApplySummaryCellAlignment()
        {
            var column = Column;
            var columnHorizontal = column?.SummaryCellHorizontalContentAlignment;
            var columnHorizontalIsSet = column?.IsSet(DataGridColumn.SummaryCellHorizontalContentAlignmentProperty) == true;
            var gridHorizontal = _owningGrid?.SummaryCellHorizontalContentAlignment;

            if (columnHorizontalIsSet && columnHorizontal.HasValue)
            {
                SetValue(HorizontalContentAlignmentProperty, columnHorizontal.Value);
            }
            else if (gridHorizontal.HasValue)
            {
                SetValue(HorizontalContentAlignmentProperty, gridHorizontal.Value);
            }
            else if (columnHorizontal.HasValue)
            {
                SetValue(HorizontalContentAlignmentProperty, columnHorizontal.Value);
            }
            else
            {
                ClearValue(HorizontalContentAlignmentProperty);
            }

            var columnVertical = column?.SummaryCellVerticalContentAlignment;
            var columnVerticalIsSet = column?.IsSet(DataGridColumn.SummaryCellVerticalContentAlignmentProperty) == true;
            var gridVertical = _owningGrid?.SummaryCellVerticalContentAlignment;

            if (columnVerticalIsSet && columnVertical.HasValue)
            {
                SetValue(VerticalContentAlignmentProperty, columnVertical.Value);
            }
            else if (gridVertical.HasValue)
            {
                SetValue(VerticalContentAlignmentProperty, gridVertical.Value);
            }
            else if (columnVertical.HasValue)
            {
                SetValue(VerticalContentAlignmentProperty, columnVertical.Value);
            }
            else
            {
                ClearValue(VerticalContentAlignmentProperty);
            }
        }

        internal void UpdateOwningGrid(DataGrid grid)
        {
            if (ReferenceEquals(_owningGrid, grid))
            {
                return;
            }

            if (_owningGrid != null)
            {
                WeakEventHandlerManager.Unsubscribe<AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                    _owningGrid,
                    nameof(AvaloniaObject.PropertyChanged),
                    OnGridPropertyChanged);
            }

            _owningGrid = grid;

            if (_owningGrid != null)
            {
                WeakEventHandlerManager.Subscribe<AvaloniaObject, AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                    _owningGrid,
                    nameof(AvaloniaObject.PropertyChanged),
                    OnGridPropertyChanged);
            }

            ApplySummaryCellTheme();
            ApplySummaryCellAlignment();
        }

        internal void UpdateAppearance()
        {
            ApplySummaryCellTheme();
            ApplySummaryCellAlignment();
        }

        /// <summary>
        /// Detaches event handlers before the cell is removed.
        /// </summary>
        internal void Detach()
        {
            if (_column != null)
            {
                WeakEventHandlerManager.Unsubscribe<AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                    _column,
                    nameof(AvaloniaObject.PropertyChanged),
                    OnColumnPropertyChanged);
            }

            if (_description != null)
            {
                WeakEventHandlerManager.Unsubscribe<AvaloniaPropertyChangedEventArgs, DataGridSummaryCell>(
                    _description,
                    nameof(AvaloniaObject.PropertyChanged),
                    OnDescriptionPropertyChanged);
            }

            UpdateOwningGrid(null);
            _owningRow = null;
        }

        /// <summary>
        /// Recalculates the summary value.
        /// </summary>
        internal void Recalculate()
        {
            if (Column == null || OwningRow?.OwningGrid?.SummaryService == null)
            {
                Value = null;
                Description = null;
                return;
            }

            var scope = OwningRow.Scope;
            var summaryService = OwningRow.OwningGrid.SummaryService;

            // Prefer exact scope matches over Both to avoid shadowing.
            var description = Column.Summaries.FirstOrDefault(d => d.Scope == scope)
                ?? Column.Summaries.FirstOrDefault(d => d.Scope == DataGridSummaryScope.Both);

            if (description == null)
            {
                Value = null;
                Description = null;
                return;
            }

            Description = description;

            // Get the calculated value
            if (scope == DataGridSummaryScope.Total || scope == DataGridSummaryScope.Both)
            {
                Value = summaryService.GetTotalSummaryValue(Column, description);
            }
            else if (scope == DataGridSummaryScope.Group && OwningRow.Group != null)
            {
                Value = summaryService.GetGroupSummaryValue(Column, description, OwningRow.Group);
            }
            else
            {
                Value = null;
            }
        }
    }
}
