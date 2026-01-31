// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Utilities;
using System.Collections.Specialized;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    abstract partial class DataGridColumn
    {
        private DataGridSummaryDescriptionCollection _summaries;
        private ControlTheme _summaryCellTheme;

        /// <summary>
        /// Backing field for SummaryCellTheme property.
        /// </summary>
        public static readonly DirectProperty<DataGridColumn, ControlTheme> SummaryCellThemeProperty =
            AvaloniaProperty.RegisterDirect<DataGridColumn, ControlTheme>(
                nameof(SummaryCellTheme),
                o => o.SummaryCellTheme,
                (o, v) => o.SummaryCellTheme = v);

        /// <summary>
        /// Identifies the <see cref="SummaryCellHorizontalContentAlignment"/> property.
        /// </summary>
        public static readonly StyledProperty<HorizontalAlignment?> SummaryCellHorizontalContentAlignmentProperty =
            AvaloniaProperty.Register<DataGridColumn, HorizontalAlignment?>(
                nameof(SummaryCellHorizontalContentAlignment));

        /// <summary>
        /// Identifies the <see cref="SummaryCellVerticalContentAlignment"/> property.
        /// </summary>
        public static readonly StyledProperty<VerticalAlignment?> SummaryCellVerticalContentAlignmentProperty =
            AvaloniaProperty.Register<DataGridColumn, VerticalAlignment?>(
                nameof(SummaryCellVerticalContentAlignment));

        /// <summary>
        /// Gets the collection of summary descriptions for this column.
        /// </summary>
        public DataGridSummaryDescriptionCollection Summaries
        {
            get
            {
                if (_summaries == null)
                {
                    _summaries = new DataGridSummaryDescriptionCollection { OwningColumn = this };
                    WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridColumn>(
                        _summaries,
                        nameof(INotifyCollectionChanged.CollectionChanged),
                        OnSummariesCollectionChanged);
                }
                return _summaries;
            }
        }

        /// <summary>
        /// Gets or sets the theme for summary cells in this column.
        /// </summary>
        public ControlTheme SummaryCellTheme
        {
            get => _summaryCellTheme;
            set => SetAndRaise(SummaryCellThemeProperty, ref _summaryCellTheme, value);
        }

        /// <summary>
        /// Gets or sets the horizontal alignment for summary cell content in this column.
        /// </summary>
        public HorizontalAlignment? SummaryCellHorizontalContentAlignment
        {
            get => GetValue(SummaryCellHorizontalContentAlignmentProperty);
            set => SetValue(SummaryCellHorizontalContentAlignmentProperty, value);
        }

        /// <summary>
        /// Gets or sets the vertical alignment for summary cell content in this column.
        /// </summary>
        public VerticalAlignment? SummaryCellVerticalContentAlignment
        {
            get => GetValue(SummaryCellVerticalContentAlignmentProperty);
            set => SetValue(SummaryCellVerticalContentAlignmentProperty, value);
        }

        /// <summary>
        /// Gets whether this column has any summary descriptions.
        /// </summary>
        internal bool HasSummaries => _summaries != null && _summaries.Count > 0;

        private void OnSummariesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Notify the owning grid that summaries have changed
            OwningGrid?.OnColumnSummariesChanged(this);
        }
    }
}
