// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;

namespace Avalonia.Controls
{
    /// <summary>
    /// Describes a summary that is materialized independently for every column created from a
    /// <see cref="DataGridColumnDefinition"/>.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridSummaryDefinition
    {
        /// <summary>Initializes a built-in aggregate summary definition.</summary>
        /// <param name="aggregate">The aggregate to calculate.</param>
        /// <param name="scope">The total/group scope.</param>
        /// <param name="stringFormat">The optional result format.</param>
        /// <param name="title">The optional result prefix.</param>
        public DataGridSummaryDefinition(
            DataGridAggregateType aggregate,
            DataGridSummaryScope scope = DataGridSummaryScope.Total,
            string stringFormat = null,
            string title = null)
        {
            Aggregate = aggregate;
            Scope = scope;
            StringFormat = stringFormat;
            Title = title;
        }

        /// <summary>Gets the aggregate kind.</summary>
        public DataGridAggregateType Aggregate { get; }

        /// <summary>Gets the total/group scope.</summary>
        public DataGridSummaryScope Scope { get; }

        /// <summary>Gets the optional result format.</summary>
        public string StringFormat { get; }

        /// <summary>Gets the optional result prefix.</summary>
        public string Title { get; }

        /// <summary>
        /// Gets or sets an optional direct factory for a custom summary description. The factory is
        /// invoked once for every materialized column and does not use reflection.
        /// </summary>
        public Func<DataGridSummaryDescription> Factory { get; set; }

        internal DataGridSummaryDescription CreateDescription()
        {
            DataGridSummaryDescription description = Factory != null
                ? Factory() ?? throw new InvalidOperationException("The summary factory returned null.")
                : new DataGridAggregateSummaryDescription { Aggregate = Aggregate };
            description.Scope = Scope;
            description.StringFormat = StringFormat;
            description.Title = Title;
            return description;
        }
    }
}
