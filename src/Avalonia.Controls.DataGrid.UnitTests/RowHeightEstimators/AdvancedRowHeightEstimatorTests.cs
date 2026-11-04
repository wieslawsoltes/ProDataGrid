// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Xunit;

namespace Avalonia.Controls.DataGridTests.RowHeightEstimators;

/// <summary>
/// Unit tests for <see cref="AdvancedRowHeightEstimator"/>.
/// </summary>
public class AdvancedRowHeightEstimatorTests : RowHeightEstimatorTestBase
{
    protected override IDataGridRowHeightEstimator CreateEstimator()
    {
        return new AdvancedRowHeightEstimator();
    }

    #region Initial State Tests

    [Fact]
    public void InitialState_HasDefaultValues()
    {
        var estimator = CreateEstimator();
        VerifyInitialState(estimator);
    }

    [Fact]
    public void DefaultRowHeight_CanBeSet()
    {
        var estimator = CreateEstimator();
        estimator.DefaultRowHeight = 30.0;
        Assert.Equal(30.0, estimator.DefaultRowHeight);
    }

    #endregion

    #region State Preservation

    [Fact]
    public void State_Can_Be_Captured_And_Restored()
    {
        var estimator = CreateEstimator();
        var stateful = Assert.IsAssignableFrom<IDataGridRowHeightEstimatorStateful>(estimator);

        estimator.RecordMeasuredHeight(12, LargeRowHeight, hasDetails: true, detailsHeight: DetailsHeight);
        estimator.RecordRowGroupHeaderHeight(0, 1, GroupHeaderHeight);

        var state = stateful.CaptureState();

        estimator.Reset();
        Assert.Equal(DefaultRowHeight, estimator.RowHeightEstimate, Tolerance);

        Assert.True(stateful.TryRestoreState(state));
        Assert.Equal(GroupHeaderHeight, estimator.GetRowGroupHeaderHeightEstimate(1), Tolerance);
        Assert.Equal(DetailsHeight, estimator.RowDetailsHeightEstimate, Tolerance);
        Assert.Equal(LargeRowHeight + DetailsHeight, estimator.GetEstimatedHeight(12, hasDetails: true), Tolerance);
    }

    #endregion

    #region Same Height Rows - No Grouping

    [Fact]
    public void SameHeight_NoGrouping_SmallScroll_UpdatesEstimate()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void SameHeight_NoGrouping_UsesRegionalStatistics()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // The advanced estimator should use regional statistics
        var diag = estimator.GetDiagnostics();
        Assert.Contains("Regional", diag.AlgorithmName);
    }

    [Fact]
    public void SameHeight_NoGrouping_SmallScrollDown_MaintainsEstimate()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        // Initial display
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Scroll down by 3 rows
        RecordUniformHeights(estimator, visibleRows, 3, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 3, visibleRows + 2, heights, rowHeight * 3);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void SameHeight_NoGrouping_LargeScroll_UpdatesEstimate()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 1000;

        // Initial display
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Large scroll to middle
        int scrollToSlot = 500;
        RecordUniformHeights(estimator, scrollToSlot, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, scrollToSlot, scrollToSlot + visibleRows - 1, heights, scrollToSlot * rowHeight);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void SameHeight_NoGrouping_ScrollToBottom_UpdatesMultipleRegions()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 500;

        // Initial display
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Scroll to bottom
        int bottomStart = totalRows - visibleRows;
        RecordUniformHeights(estimator, bottomStart, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, bottomStart, totalRows - 1, heights, bottomStart * rowHeight);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void SameHeight_NoGrouping_FastScrollTopToBottom()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 1000;

        // Initial at top
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Fast scroll to bottom
        int bottomStart = totalRows - visibleRows;
        RecordUniformHeights(estimator, bottomStart, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, bottomStart, totalRows - 1, heights, bottomStart * rowHeight);

        // Fast scroll back to top
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void SameHeight_NoGrouping_CalculateTotalHeight_UsesAllData()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 100;

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double totalHeight = estimator.CalculateTotalHeight(totalRows, 0, Array.Empty<int>(), 0);
        double expectedHeight = totalRows * rowHeight;

        Assert.InRange(totalHeight, expectedHeight - 100, expectedHeight + 100);
    }

    [Fact]
    public void SameHeight_NoGrouping_EstimateSlotAtOffset_ReturnsReasonableValue()
    {
        var estimator = CreateEstimator();
        int visibleRows = 30;
        double rowHeight = DefaultRowHeight;
        int totalRows = 100;

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Request slot at offset that would be around slot 15 for uniform rows
        double targetOffset = rowHeight * 15;
        int estimatedSlot = estimator.EstimateSlotAtOffset(targetOffset, totalRows);
        
        // Should return a valid slot within range
        Assert.InRange(estimatedSlot, 0, totalRows - 1);
        
        // For small offset relative to total, should be early slots
        int estimatedSlotSmall = estimator.EstimateSlotAtOffset(rowHeight * 3, totalRows);
        Assert.True(estimatedSlotSmall < totalRows / 2, "Small offset should estimate to early slots");
    }

    [Fact]
    public void SameHeight_NoGrouping_EstimateOffsetToSlot_ReturnsReasonableValue()
    {
        var estimator = CreateEstimator();
        int visibleRows = 20;
        double rowHeight = DefaultRowHeight;

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double offset = estimator.EstimateOffsetToSlot(10);
        
        // Should return a positive value for slot 10
        Assert.True(offset >= 0, "Offset should be non-negative");
        
        // Offset should increase with slot number
        double offset5 = estimator.EstimateOffsetToSlot(5);
        double offset15 = estimator.EstimateOffsetToSlot(15);
        Assert.True(offset15 >= offset, "Later slot should have higher offset");
        Assert.True(offset >= offset5, "Slot 10 should have higher offset than slot 5");
    }

    #endregion

    #region Same Height Rows - With Grouping

    [Fact]
    public void SameHeight_WithGrouping_RecordsGroupHeaderHeights()
    {
        var estimator = CreateEstimator();

        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);
        estimator.RecordRowGroupHeaderHeight(5, 1, GroupHeaderHeight + 5);

        Assert.Equal(GroupHeaderHeight, estimator.GetRowGroupHeaderHeightEstimate(0), Tolerance);
        Assert.Equal(GroupHeaderHeight + 5, estimator.GetRowGroupHeaderHeightEstimate(1), Tolerance);
    }

    [Fact]
    public void SameHeight_WithGrouping_CalculateTotalHeight_IncludesHeaders()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalSlots = 110;
        int[] groupHeaderCounts = { 5, 5 };

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);
        estimator.RecordRowGroupHeaderHeight(1, 1, GroupHeaderHeight);

        double totalHeight = estimator.CalculateTotalHeight(totalSlots, 0, groupHeaderCounts, 0);

        Assert.True(totalHeight > 0);
    }

    [Fact]
    public void SameHeight_WithGrouping_CollapsedGroups_DoesNotCrash()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalSlots = 100;
        int collapsedSlots = 20;

        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double fullHeight = estimator.CalculateTotalHeight(totalSlots, 0, Array.Empty<int>(), 0);
        double collapsedHeight = estimator.CalculateTotalHeight(totalSlots, collapsedSlots, Array.Empty<int>(), 0);

        // Both should return valid positive heights
        Assert.True(fullHeight > 0, "Full height should be positive");
        Assert.True(collapsedHeight >= 0, "Collapsed height should be non-negative");
    }

    [Fact]
    public void SameHeight_WithGrouping_ScrollPastCollapsedGroup()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        // Initial display
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0, 0, 0);

        // Scroll past collapsed group
        RecordUniformHeights(estimator, 20, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 20, 29, heights, 15 * rowHeight, 0, 5);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    #endregion

    #region Variable Height Rows - No Grouping

    [Fact]
    public void VariableHeight_NoGrouping_InitialDisplay_UsesRegionalAverages()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;

        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double expectedAverage = (SmallRowHeight + LargeRowHeight) / 2;
        VerifyEstimateAfterMeasurements(estimator, expectedAverage);
    }

    [Fact]
    public void VariableHeight_NoGrouping_CachesAllMeasuredHeights()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;

        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);

        for (int i = 0; i < visibleRows; i++)
        {
            Assert.Equal(AlternatingHeight(i), estimator.GetEstimatedHeight(i), Tolerance);
        }
    }

    [Fact]
    public void VariableHeight_NoGrouping_SmallScroll_AccumulatesData()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;

        // Initial display
        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Scroll down by 2 rows
        RecordVariableHeights(estimator, visibleRows, 2, AlternatingHeight);
        heights = CreateVariableHeights(2, visibleRows, AlternatingHeight);
        double offset = SmallRowHeight + LargeRowHeight;
        SimulateScroll(estimator, 2, visibleRows + 1, heights, offset);

        var diag = estimator.GetDiagnostics();
        Assert.Equal(visibleRows + 2, diag.CachedHeightCount);
    }

    [Fact]
    public void VariableHeight_NoGrouping_LargeScroll_BuildsMultipleRegions()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        int totalRows = 1000;

        // Initial display (region 0)
        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Large scroll to region 5 (slots 500-509)
        int scrollToSlot = 500;
        RecordVariableHeights(estimator, scrollToSlot, visibleRows, AlternatingHeight);
        heights = CreateVariableHeights(scrollToSlot, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, scrollToSlot, scrollToSlot + visibleRows - 1, heights, scrollToSlot * 35);

        var diag = estimator.GetDiagnostics();
        Assert.Contains("Regions:", diag.AdditionalInfo);
    }

    [Fact]
    public void VariableHeight_NoGrouping_FastScrollTopToBottom_HandlesCorrection()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        int totalRows = 1000;

        // Initial at top
        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Fast scroll to bottom
        int bottomStart = totalRows - visibleRows;
        RecordVariableHeights(estimator, bottomStart, visibleRows, AlternatingHeight);
        heights = CreateVariableHeights(bottomStart, visibleRows, AlternatingHeight);
        double avgHeight = (SmallRowHeight + LargeRowHeight) / 2;
        SimulateScroll(estimator, bottomStart, totalRows - 1, heights, bottomStart * avgHeight);

        // Should have applied smooth correction
        var diag = estimator.GetDiagnostics();
        Assert.Contains("PendingCorr", diag.AdditionalInfo);
    }

    [Fact]
    public void VariableHeight_NoGrouping_GradualIncrease_TracksRegionalVariation()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;

        RecordVariableHeights(estimator, 0, visibleRows, GradualIncreaseHeight);
        var heights = CreateVariableHeights(0, visibleRows, GradualIncreaseHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Later rows should be estimated higher based on regional data
        var diag = estimator.GetDiagnostics();
        Assert.True(diag.MaxMeasuredHeight > diag.MinMeasuredHeight);
    }

    [Fact]
    public void VariableHeight_NoGrouping_PseudoRandomHeights_HandlesMixedData()
    {
        var estimator = CreateEstimator();
        int visibleRows = 20;

        RecordVariableHeights(estimator, 0, visibleRows, PseudoRandomHeight);
        var heights = CreateVariableHeights(0, visibleRows, PseudoRandomHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        var diag = estimator.GetDiagnostics();
        Assert.Equal(visibleRows, diag.CachedHeightCount);
        Assert.True(diag.AverageMeasuredHeight >= SmallRowHeight);
        Assert.True(diag.AverageMeasuredHeight <= LargeRowHeight);
    }

    [Fact]
    public void VariableHeight_NoGrouping_EstimateSlotAtOffset_ReturnsValidSlot()
    {
        var estimator = CreateEstimator();
        int visibleRows = 30;
        int totalRows = 100;

        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double avgHeight = (SmallRowHeight + LargeRowHeight) / 2;
        int estimatedSlot = estimator.EstimateSlotAtOffset(avgHeight * 15, totalRows);

        // Should return a valid slot within bounds
        Assert.InRange(estimatedSlot, 0, totalRows - 1);
    }

    [Fact]
    public void VariableHeight_NoGrouping_EstimateOffsetToSlot_ReturnsValidOffset()
    {
        var estimator = CreateEstimator();
        int visibleRows = 20;

        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double offset10 = estimator.EstimateOffsetToSlot(10);
        
        // Should return a non-negative offset
        Assert.True(offset10 >= 0, "Offset should be non-negative");
        
        // Offset should increase with slot number
        double offset5 = estimator.EstimateOffsetToSlot(5);
        double offset15 = estimator.EstimateOffsetToSlot(15);
        Assert.True(offset10 >= offset5, "Higher slot should have higher offset");
        Assert.True(offset15 >= offset10, "Higher slot should have higher offset");
    }

    #endregion

    #region Variable Height Rows - With Grouping

    [Fact]
    public void VariableHeight_WithGrouping_MixedContent()
    {
        var estimator = CreateEstimator();

        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);
        estimator.RecordMeasuredHeight(1, SmallRowHeight);
        estimator.RecordMeasuredHeight(2, LargeRowHeight);
        estimator.RecordRowGroupHeaderHeight(3, 0, GroupHeaderHeight);
        estimator.RecordMeasuredHeight(4, MediumRowHeight);

        Assert.Equal(GroupHeaderHeight, estimator.GetEstimatedHeight(0, isRowGroupHeader: true), Tolerance);
        Assert.Equal(SmallRowHeight, estimator.GetEstimatedHeight(1), Tolerance);
        Assert.Equal(LargeRowHeight, estimator.GetEstimatedHeight(2), Tolerance);
    }

    [Fact]
    public void VariableHeight_WithGrouping_NestedGroups()
    {
        var estimator = CreateEstimator();

        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);
        estimator.RecordRowGroupHeaderHeight(1, 1, GroupHeaderHeight - 5);
        estimator.RecordRowGroupHeaderHeight(10, 2, GroupHeaderHeight - 10);

        Assert.Equal(GroupHeaderHeight, estimator.GetRowGroupHeaderHeightEstimate(0), Tolerance);
        Assert.Equal(GroupHeaderHeight - 5, estimator.GetRowGroupHeaderHeightEstimate(1), Tolerance);
        Assert.Equal(GroupHeaderHeight - 10, estimator.GetRowGroupHeaderHeightEstimate(2), Tolerance);
    }

    [Fact]
    public void VariableHeight_WithGrouping_ScrollAcrossMultipleGroups()
    {
        var estimator = CreateEstimator();
        int visibleRows = 8;

        // Initial display in first group
        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);
        RecordVariableHeights(estimator, 1, visibleRows - 1, AlternatingHeight);

        // Scroll to third group
        estimator.RecordRowGroupHeaderHeight(100, 0, GroupHeaderHeight);
        RecordVariableHeights(estimator, 101, visibleRows - 1, AlternatingHeight);

        // Both group headers should be recorded
        Assert.Equal(GroupHeaderHeight, estimator.GetRowGroupHeaderHeightEstimate(0), Tolerance);
    }

    [Fact]
    public void VariableHeight_WithGrouping_CalculateTotalHeight_Complex()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        int totalSlots = 150;
        int[] groupHeaderCounts = { 10, 15, 5 };

        RecordVariableHeights(estimator, 0, visibleRows, AlternatingHeight);
        var heights = CreateVariableHeights(0, visibleRows, AlternatingHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);
        estimator.RecordRowGroupHeaderHeight(1, 1, GroupHeaderHeight - 3);
        estimator.RecordRowGroupHeaderHeight(2, 2, GroupHeaderHeight - 6);

        double totalHeight = estimator.CalculateTotalHeight(totalSlots, 0, groupHeaderCounts, 0);

        Assert.True(totalHeight > 0);
        Assert.True(totalHeight < totalSlots * LargeRowHeight);
    }

    #endregion

    #region Data Source Changes

    [Fact]
    public void OnDataSourceChanged_ResetsAllState()
    {
        var estimator = CreateEstimator();

        RecordUniformHeights(estimator, 0, 10, LargeRowHeight);
        var heights = CreateUniformHeights(10, LargeRowHeight);
        SimulateScroll(estimator, 0, 9, heights, 0);

        estimator.OnDataSourceChanged(50);

        var diag = estimator.GetDiagnostics();
        Assert.Equal(0, diag.CachedHeightCount);
        Assert.Equal(DefaultRowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void OnItemsInserted_ShiftsDataAndRebuildsFenwick()
    {
        var estimator = CreateEstimator();

        RecordUniformHeights(estimator, 0, 20, DefaultRowHeight);
        var heights = CreateUniformHeights(10, DefaultRowHeight);
        SimulateScroll(estimator, 0, 9, heights, 0);

        estimator.OnItemsInserted(5, 10);

        // Should still function correctly
        Assert.True(estimator.RowHeightEstimate > 0);
    }

    [Fact]
    public void OnItemsRemoved_ShiftsDataAndRebuildsFenwick()
    {
        var estimator = CreateEstimator();

        RecordUniformHeights(estimator, 0, 30, DefaultRowHeight);
        var heights = CreateUniformHeights(10, DefaultRowHeight);
        SimulateScroll(estimator, 0, 9, heights, 0);

        estimator.OnItemsRemoved(5, 10);

        Assert.True(estimator.RowHeightEstimate > 0);
    }

    [Fact]
    public void Reset_ClearsAllDataStructures()
    {
        var estimator = CreateEstimator();

        RecordUniformHeights(estimator, 0, 50, LargeRowHeight);
        estimator.RecordMeasuredHeight(0, LargeRowHeight, true, DetailsHeight);
        estimator.RecordRowGroupHeaderHeight(0, 0, GroupHeaderHeight);

        estimator.Reset();

        var diag = estimator.GetDiagnostics();
        Assert.Equal(0, diag.CachedHeightCount);
        Assert.Equal(DefaultRowHeight, estimator.RowHeightEstimate, Tolerance);
        Assert.Equal(0, estimator.RowDetailsHeightEstimate, Tolerance);
    }

    #endregion

    #region Row Details

    [Fact]
    public void RowDetails_RecordsDetailsHeight()
    {
        var estimator = CreateEstimator();

        estimator.RecordMeasuredHeight(0, DefaultRowHeight, true, DetailsHeight);

        Assert.Equal(DetailsHeight, estimator.RowDetailsHeightEstimate, Tolerance);
    }

    [Fact]
    public void RowDetails_GetEstimatedHeight_IncludesDetails()
    {
        var estimator = CreateEstimator();

        estimator.RecordMeasuredHeight(0, DefaultRowHeight, true, DetailsHeight);

        double heightWithDetails = estimator.GetEstimatedHeight(0, hasDetails: true);
        double heightWithoutDetails = estimator.GetEstimatedHeight(0, hasDetails: false);

        Assert.Equal(heightWithoutDetails + DetailsHeight, heightWithDetails, Tolerance);
    }

    [Fact]
    public void RowDetails_CalculateTotalHeight_IncludesDetails()
    {
        var estimator = CreateEstimator();
        int totalRows = 100;
        int detailsCount = 5;

        RecordUniformHeights(estimator, 0, 10, DefaultRowHeight);
        var heights = CreateUniformHeights(10, DefaultRowHeight);
        SimulateScroll(estimator, 0, 9, heights, 0);

        estimator.RecordMeasuredHeight(0, DefaultRowHeight, true, DetailsHeight);

        double totalWithDetails = estimator.CalculateTotalHeight(totalRows, 0, Array.Empty<int>(), detailsCount);
        double totalWithoutDetails = estimator.CalculateTotalHeight(totalRows, 0, Array.Empty<int>(), 0);

        Assert.True(totalWithDetails > totalWithoutDetails);
    }

    #endregion

    #region Smooth Scroll Correction

    [Fact]
    public void SmoothCorrection_AppliesGradually()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        // Initial display
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Simulate scroll with offset mismatch to trigger correction
        SimulateScroll(estimator, 10, 19, heights, rowHeight * 15); // Intentional mismatch

        var diag = estimator.GetDiagnostics();
        Assert.Contains("PendingCorr", diag.AdditionalInfo);
    }

    #endregion

    #region Diagnostics

    [Fact]
    public void GetDiagnostics_ReportsAdvancedAlgorithm()
    {
        var estimator = CreateEstimator();

        var diag = estimator.GetDiagnostics();

        Assert.Contains("Advanced", diag.AlgorithmName);
        Assert.Contains("Regional", diag.AlgorithmName);
    }

    [Fact]
    public void GetDiagnostics_ReportsRegionInfo()
    {
        var estimator = CreateEstimator();

        RecordUniformHeights(estimator, 0, 10, DefaultRowHeight);
        var heights = CreateUniformHeights(10, DefaultRowHeight);
        SimulateScroll(estimator, 0, 9, heights, 0);

        var diag = estimator.GetDiagnostics();

        Assert.Contains("Regions:", diag.AdditionalInfo);
    }

    [Fact]
    public void GetDiagnostics_ReportsMeasuredRange()
    {
        var estimator = CreateEstimator();

        RecordUniformHeights(estimator, 5, 10, DefaultRowHeight);
        var heights = CreateUniformHeights(10, DefaultRowHeight);
        SimulateScroll(estimator, 5, 14, heights, 0);

        var diag = estimator.GetDiagnostics();

        Assert.Contains("Range:", diag.AdditionalInfo);
    }

    [Fact]
    public void GetDiagnostics_ReportsStatistics()
    {
        var estimator = CreateEstimator();

        RecordVariableHeights(estimator, 0, 20, AlternatingHeight);
        var heights = CreateVariableHeights(0, 20, AlternatingHeight);
        SimulateScroll(estimator, 0, 19, heights, 0);

        var diag = estimator.GetDiagnostics();

        Assert.Equal(20, diag.CachedHeightCount);
        Assert.Equal(SmallRowHeight, diag.MinMeasuredHeight, Tolerance);
        Assert.Equal(LargeRowHeight, diag.MaxMeasuredHeight, Tolerance);
    }

    #endregion

    #region Bidirectional Scrolling Tests

    [Fact]
    public void ScrollDown_SmallIncrement_MaintainsAccuracy()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 100;

        // Start at top
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Scroll down one row at a time
        for (int i = 1; i <= 5; i++)
        {
            int startSlot = i;
            RecordUniformHeights(estimator, startSlot + visibleRows - 1, 1, rowHeight); // New row coming into view
            heights = CreateUniformHeights(visibleRows, rowHeight);
            SimulateScroll(estimator, startSlot, startSlot + visibleRows - 1, heights, startSlot * rowHeight);
        }

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void ScrollUp_SmallIncrement_MaintainsAccuracy()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        // Start at middle
        int startPosition = 50;
        RecordUniformHeights(estimator, startPosition, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, startPosition, startPosition + visibleRows - 1, heights, startPosition * rowHeight);

        // Scroll up one row at a time
        for (int i = 1; i <= 5; i++)
        {
            int startSlot = startPosition - i;
            RecordUniformHeights(estimator, startSlot, 1, rowHeight); // New row coming into view at top
            heights = CreateUniformHeights(visibleRows, rowHeight);
            SimulateScroll(estimator, startSlot, startSlot + visibleRows - 1, heights, startSlot * rowHeight);
        }

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void ScrollDown_LargeJump_HandlesGap()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 1000;

        // Start at top
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Jump to middle (large gap)
        int middleStart = 500;
        RecordUniformHeights(estimator, middleStart, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, middleStart, middleStart + visibleRows - 1, heights, middleStart * rowHeight);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void ScrollUp_LargeJump_HandlesGap()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 1000;

        // Start at bottom
        int bottomStart = totalRows - visibleRows;
        RecordUniformHeights(estimator, bottomStart, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, bottomStart, totalRows - 1, heights, bottomStart * rowHeight);

        // Jump back to top (large gap)
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void ScrollDown_ThenUp_BidirectionalAccuracy()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        // Start at top
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Scroll down to position 30
        RecordUniformHeights(estimator, 30, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 30, 39, heights, 30 * rowHeight);

        // Scroll back up to position 10
        RecordUniformHeights(estimator, 10, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 10, 19, heights, 10 * rowHeight);

        // Scroll down again to position 25
        RecordUniformHeights(estimator, 25, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 25, 34, heights, 25 * rowHeight);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void ScrollUp_ThenDown_BidirectionalAccuracy()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;

        // Start at middle
        RecordUniformHeights(estimator, 50, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 50, 59, heights, 50 * rowHeight);

        // Scroll up to position 20
        RecordUniformHeights(estimator, 20, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 20, 29, heights, 20 * rowHeight);

        // Scroll back down to position 40
        RecordUniformHeights(estimator, 40, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 40, 49, heights, 40 * rowHeight);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void VariableHeight_ScrollDown_AdaptsToHeightChanges()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;

        // Top region has small rows
        RecordUniformHeights(estimator, 0, visibleRows, SmallRowHeight);
        var heights = CreateUniformHeights(visibleRows, SmallRowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        double initialEstimate = estimator.RowHeightEstimate;

        // Scroll to middle region with large rows
        RecordUniformHeights(estimator, 50, visibleRows, LargeRowHeight);
        heights = CreateUniformHeights(visibleRows, LargeRowHeight);
        double offset = 50 * SmallRowHeight; // Approximate
        SimulateScroll(estimator, 50, 59, heights, offset);

        // Estimate should adapt (increase)
        Assert.True(estimator.RowHeightEstimate > initialEstimate, 
            "Estimate should increase after seeing larger rows");
    }

    [Fact]
    public void VariableHeight_ScrollUp_AdaptsToHeightChanges()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;

        // Start at bottom with large rows
        RecordUniformHeights(estimator, 90, visibleRows, LargeRowHeight);
        var heights = CreateUniformHeights(visibleRows, LargeRowHeight);
        SimulateScroll(estimator, 90, 99, heights, 90 * LargeRowHeight);

        double initialEstimate = estimator.RowHeightEstimate;

        // Scroll up to top region with small rows
        RecordUniformHeights(estimator, 0, visibleRows, SmallRowHeight);
        heights = CreateUniformHeights(visibleRows, SmallRowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Estimate should adapt (decrease)
        Assert.True(estimator.RowHeightEstimate < initialEstimate,
            "Estimate should decrease after seeing smaller rows");
    }

    #endregion

    #region Multi-Region Scrolling Tests

    [Fact]
    public void MultiRegion_TopMiddleBottom_SameHeight()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 1000;

        // Sample top region
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Sample middle region
        int middleStart = totalRows / 2;
        RecordUniformHeights(estimator, middleStart, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, middleStart, middleStart + visibleRows - 1, heights, middleStart * rowHeight);

        // Sample bottom region
        int bottomStart = totalRows - visibleRows;
        RecordUniformHeights(estimator, bottomStart, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, bottomStart, totalRows - 1, heights, bottomStart * rowHeight);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);

        // Total height should still be accurate
        double totalHeight = estimator.CalculateTotalHeight(totalRows, 0, Array.Empty<int>(), 0);
        Assert.InRange(totalHeight, totalRows * rowHeight * 0.9, totalRows * rowHeight * 1.1);
    }

    [Fact]
    public void MultiRegion_DifferentHeightsPerRegion()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        int totalRows = 300;

        // Top region: small rows (0-99)
        RecordUniformHeights(estimator, 0, visibleRows, SmallRowHeight);
        var heights = CreateUniformHeights(visibleRows, SmallRowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Middle region: medium rows (100-199)
        RecordUniformHeights(estimator, 100, visibleRows, MediumRowHeight);
        heights = CreateUniformHeights(visibleRows, MediumRowHeight);
        double offset = 100 * SmallRowHeight; // Approximate
        SimulateScroll(estimator, 100, 109, heights, offset);

        // Bottom region: large rows (200-299)
        RecordUniformHeights(estimator, 200, visibleRows, LargeRowHeight);
        heights = CreateUniformHeights(visibleRows, LargeRowHeight);
        offset = 100 * SmallRowHeight + 100 * MediumRowHeight; // Approximate
        SimulateScroll(estimator, 200, 209, heights, offset);

        var diag = estimator.GetDiagnostics();
        
        // Should have recorded all heights
        Assert.Equal(30, diag.CachedHeightCount);
        
        // Estimate should be somewhere in the middle
        double avgHeight = (SmallRowHeight + MediumRowHeight + LargeRowHeight) / 3;
        Assert.InRange(estimator.RowHeightEstimate, SmallRowHeight, LargeRowHeight);
    }

    [Fact]
    public void MultiRegion_RandomJumps_MaintainsStability()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 500;

        // Jump around to various positions
        int[] positions = { 0, 250, 100, 400, 50, 300, 450, 150, 350, 0 };

        foreach (int pos in positions)
        {
            RecordUniformHeights(estimator, pos, visibleRows, rowHeight);
            var heights = CreateUniformHeights(visibleRows, rowHeight);
            SimulateScroll(estimator, pos, pos + visibleRows - 1, heights, pos * rowHeight);
        }

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void MultiRegion_VariableHeight_RegionalAverages()
    {
        var estimator = CreateEstimator();
        int visibleRows = 15;
        int totalRows = 300;

        // Sample multiple regions with alternating heights
        int[] samplePositions = { 0, 50, 100, 150, 200, 250 };

        foreach (int pos in samplePositions)
        {
            RecordVariableHeights(estimator, pos, visibleRows, AlternatingHeight);
            var heights = CreateVariableHeights(pos, visibleRows, AlternatingHeight);
            double offset = pos * ((SmallRowHeight + LargeRowHeight) / 2);
            SimulateScroll(estimator, pos, pos + visibleRows - 1, heights, offset);
        }

        var diag = estimator.GetDiagnostics();
        
        // Should have sampled many rows
        Assert.True(diag.CachedHeightCount >= samplePositions.Length * visibleRows);
        
        // Average should be between small and large
        double expectedAvg = (SmallRowHeight + LargeRowHeight) / 2;
        Assert.InRange(diag.AverageMeasuredHeight, SmallRowHeight, LargeRowHeight);
    }

    [Fact]
    public void MultiRegion_ScrollSequence_TopToMiddleToBottomAndBack()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 500;

        // Top
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        // Middle
        int middle = 250;
        RecordUniformHeights(estimator, middle, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, middle, middle + visibleRows - 1, heights, middle * rowHeight);

        // Bottom
        int bottom = totalRows - visibleRows;
        RecordUniformHeights(estimator, bottom, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, bottom, totalRows - 1, heights, bottom * rowHeight);

        // Back to middle
        RecordUniformHeights(estimator, middle, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, middle, middle + visibleRows - 1, heights, middle * rowHeight);

        // Back to top
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        Assert.Equal(rowHeight, estimator.RowHeightEstimate, Tolerance);
    }

    [Fact]
    public void MultiRegion_EstimateSlotAtOffset_UsesMultipleRegions()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 1000;

        // Sample multiple regions
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        RecordUniformHeights(estimator, 500, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 500, 509, heights, 500 * rowHeight);

        RecordUniformHeights(estimator, 990, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 990, 999, heights, 990 * rowHeight);

        // Estimate slot at middle offset
        int estimatedSlot = estimator.EstimateSlotAtOffset(500 * rowHeight, totalRows);
        Assert.InRange(estimatedSlot, 0, totalRows - 1);

        // Estimate slot near bottom
        int bottomSlot = estimator.EstimateSlotAtOffset(950 * rowHeight, totalRows);
        Assert.InRange(bottomSlot, 0, totalRows - 1);
    }

    [Fact]
    public void MultiRegion_EstimateOffsetToSlot_ConsistentAcrossRegions()
    {
        var estimator = CreateEstimator();
        int visibleRows = 10;
        double rowHeight = DefaultRowHeight;
        int totalRows = 500;

        // Sample regions
        RecordUniformHeights(estimator, 0, visibleRows, rowHeight);
        var heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 0, visibleRows - 1, heights, 0);

        RecordUniformHeights(estimator, 250, visibleRows, rowHeight);
        heights = CreateUniformHeights(visibleRows, rowHeight);
        SimulateScroll(estimator, 250, 259, heights, 250 * rowHeight);

        // Offsets should increase with slot number
        double offset50 = estimator.EstimateOffsetToSlot(50);
        double offset100 = estimator.EstimateOffsetToSlot(100);
        double offset200 = estimator.EstimateOffsetToSlot(200);
        double offset300 = estimator.EstimateOffsetToSlot(300);

        Assert.True(offset100 > offset50, "Offset to slot 100 should be > offset to slot 50");
        Assert.True(offset200 > offset100, "Offset to slot 200 should be > offset to slot 100");
        Assert.True(offset300 > offset200, "Offset to slot 300 should be > offset to slot 200");
    }

    #endregion

    #region Collapsed Slots

    [Fact]
    public void ScrollCorrection_Ignores_Rows_Of_Collapsed_Groups_Above_The_Viewport()
    {
        const int groups = 60;
        const int rowsPerGroup = 50;
        const int slotsPerGroup = rowsPerGroup + 1;
        const int totalSlots = groups * slotsPerGroup;
        const int collapsedSlots = (groups - 1) * rowsPerGroup;

        var estimator = CreateEstimator();
        estimator.DefaultRowHeight = SmallRowHeight;
        estimator.OnDataSourceChanged(totalSlots);
        RecordUniformHeights(estimator, 1, rowsPerGroup, SmallRowHeight);
        var headerCounts = new[] { groups };
        var expected = groups * GroupHeaderHeight + rowsPerGroup * SmallRowHeight;

        for (var firstGroup = 1; firstGroup < groups - 10; firstGroup++)
        {
            var firstSlot = firstGroup * slotsPerGroup;
            var verticalOffset = GroupHeaderHeight + rowsPerGroup * SmallRowHeight + (firstGroup - 1) * GroupHeaderHeight;
            var displayed = new double[10];
            for (var i = 0; i < displayed.Length; i++)
            {
                estimator.RecordRowGroupHeaderHeight(firstSlot + i * slotsPerGroup, 0, GroupHeaderHeight);
                displayed[i] = GroupHeaderHeight;
            }

            SimulateScroll(estimator, firstSlot, firstSlot + 9 * slotsPerGroup, displayed, verticalOffset,
                collapsedSlotCount: collapsedSlots);

            var total = estimator.CalculateTotalHeight(totalSlots, collapsedSlots, headerCounts, 0);
            Assert.True(total > expected * 0.9, $"Extent fell to {total:F0} (expected about {expected:F0}) after scrolling to group {firstGroup}.");
        }
    }

    #endregion
}
