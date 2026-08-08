// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;

namespace Avalonia.Controls
{
    /// <summary>Identifies explicit generated DataGrid performance presets.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedPerformanceProfile
    {
        /// <summary>Use balanced runtime defaults.</summary>
        Balanced,
        /// <summary>Optimize for fixed-height rows.</summary>
        UniformRows,
        /// <summary>Estimate highly variable row heights.</summary>
        VariableHeightEstimated,
        /// <summary>Cache measured variable row heights.</summary>
        VariableHeightMeasured,
        /// <summary>Optimize keyboard-heavy spreadsheet navigation.</summary>
        Spreadsheet,
        /// <summary>Optimize hierarchical flattening and scrolling.</summary>
        Tree,
        /// <summary>Optimize high-frequency bounded streaming updates.</summary>
        HighFrequencyStreaming
    }

    /// <summary>Contains explicit settings selected by a generated performance profile.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedPerformanceOptions
    {
        /// <summary>Gets the named profile.</summary>
        public DataGridGeneratedPerformanceProfile Profile { get; init; }
        /// <summary>Gets whether logical scrolling is enabled.</summary>
        public bool UseLogicalScrollable { get; init; }
        /// <summary>Gets an optional fixed row height.</summary>
        public double? RowHeight { get; init; }
        /// <summary>Gets the row-height estimator factory.</summary>
        public Func<IDataGridRowHeightEstimator> RowHeightEstimatorFactory { get; init; }
        /// <summary>Gets whether generated search indexes track item changes.</summary>
        public bool SearchTracksItemChanges { get; init; } = true;

        /// <summary>Applies the explicit profile to a DataGrid UI boundary.</summary>
        public void Apply(DataGrid dataGrid)
        {
            ArgumentNullException.ThrowIfNull(dataGrid);
            dataGrid.UseLogicalScrollable = UseLogicalScrollable;
            if (RowHeight.HasValue) dataGrid.RowHeight = RowHeight.Value;
            if (RowHeightEstimatorFactory != null) dataGrid.RowHeightEstimator = RowHeightEstimatorFactory();
        }

        /// <summary>Creates settings for a named profile.</summary>
        public static DataGridGeneratedPerformanceOptions Create(DataGridGeneratedPerformanceProfile profile) =>
            profile switch
            {
                DataGridGeneratedPerformanceProfile.UniformRows => new()
                {
                    Profile = profile,
                    UseLogicalScrollable = true,
                    RowHeight = 28d,
                    RowHeightEstimatorFactory = static () => new DefaultRowHeightEstimator()
                },
                DataGridGeneratedPerformanceProfile.VariableHeightEstimated => new()
                {
                    Profile = profile,
                    UseLogicalScrollable = true,
                    RowHeightEstimatorFactory = static () => new AdvancedRowHeightEstimator()
                },
                DataGridGeneratedPerformanceProfile.VariableHeightMeasured => new()
                {
                    Profile = profile,
                    UseLogicalScrollable = true,
                    RowHeightEstimatorFactory = static () => new CachingRowHeightEstimator()
                },
                DataGridGeneratedPerformanceProfile.Spreadsheet => new()
                {
                    Profile = profile,
                    UseLogicalScrollable = true,
                    RowHeight = 24d,
                    RowHeightEstimatorFactory = static () => new DefaultRowHeightEstimator()
                },
                DataGridGeneratedPerformanceProfile.Tree => new()
                {
                    Profile = profile,
                    UseLogicalScrollable = true,
                    RowHeightEstimatorFactory = static () => new AdvancedRowHeightEstimator()
                },
                DataGridGeneratedPerformanceProfile.HighFrequencyStreaming => new()
                {
                    Profile = profile,
                    UseLogicalScrollable = true,
                    RowHeight = 26d,
                    SearchTracksItemChanges = false,
                    RowHeightEstimatorFactory = static () => new DefaultRowHeightEstimator()
                },
                _ => new()
                {
                    Profile = DataGridGeneratedPerformanceProfile.Balanced,
                    UseLogicalScrollable = true,
                    RowHeightEstimatorFactory = static () => new AdvancedRowHeightEstimator()
                }
            };
    }
}
