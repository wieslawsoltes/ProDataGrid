using System;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridScrollHeightIndexTests
{
    [Theory]
    [InlineData(true, 1_000_000, 0, true)]
    [InlineData(false, 99_999, 0, true)]
    [InlineData(false, 100_000, 1_024, false)]
    [InlineData(false, 100_000, 1_025, true)]
    public void IndexBuildDecisionAvoidsLargeDirtyIndexesForModerateScrolls(
        bool hasCurrentIndex,
        int slotCount,
        double estimatedRows,
        bool expected)
    {
        bool actual = Avalonia.Controls.DataGrid.ShouldBuildScrollHeightIndex(
            hasCurrentIndex,
            slotCount,
            estimatedRows);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PrefixAndOffsetQueriesRespectVariableHeights()
    {
        var index = CreateIndex(new[] { 10d, 20d, 30d, 40d });

        Assert.Equal(100, index.TotalHeight);
        Assert.Equal(0, index.GetOffsetToSlot(0));
        Assert.Equal(10, index.GetOffsetToSlot(1));
        Assert.Equal(30, index.GetOffsetToSlot(2));
        Assert.Equal(0, index.FindSlotAtOffset(0));
        Assert.Equal(0, index.FindSlotAtOffset(9.99));
        Assert.Equal(1, index.FindSlotAtOffset(10));
        Assert.Equal(2, index.FindSlotAtOffset(30));
        Assert.Equal(3, index.FindSlotAtOffset(99.99));
        Assert.Equal(3, index.FindSlotAtOffset(100));
    }

    [Fact]
    public void CollapsedRangesHaveZeroHeightAndRemainIndexed()
    {
        var index = CreateIndex(new[] { 10d, 20d, 30d, 40d, 50d });

        index.SetVisibleRange(1, 3, false);

        Assert.Equal(60, index.TotalHeight);
        Assert.Equal(4, index.FindFirstVisibleAtOrAfter(1));
        Assert.Equal(4, index.FindFirstVisibleAtOrAfter(1 + 3));
        Assert.Equal(0, index.FindLastVisibleAtOrBefore(3));
        Assert.Equal(4, index.FindSlotAtOffset(10));
        Assert.Equal(10, index.GetOffsetToSlot(4));

        index.SetVisibleRange(1, 3, true);

        Assert.Equal(150, index.TotalHeight);
        Assert.Equal(1, index.FindSlotAtOffset(10));
        Assert.Equal(3, index.FindLastVisibleAtOrBefore(3));
    }

    [Fact]
    public void PointUpdatesPreserveRangeVisibilityState()
    {
        var index = CreateIndex(new[] { 10d, 20d, 30d });
        index.SetVisible(1, false);
        index.SetHeight(1, 80);

        Assert.Equal(40, index.TotalHeight);
        Assert.Equal(2, index.FindSlotAtOffset(10));

        index.SetVisible(1, true);

        Assert.Equal(120, index.TotalHeight);
        Assert.Equal(1, index.FindSlotAtOffset(10));
    }

    [Fact]
    public void ZeroHeightVisibleSlotsRemainAddressableAtBoundaries()
    {
        var index = CreateIndex(new[] { 0d, 20d, 0d, 30d });

        Assert.Equal(0, index.FindSlotAtOffset(0));
        Assert.Equal(1, index.FindSlotAtOffset(1));
        Assert.Equal(2, index.FindFirstVisibleAtOrAfter(2));
        Assert.Equal(2, index.FindLastVisibleAtOrBefore(2));
    }

    [Fact]
    public void RebuildReusesBuffersWhenCapacityIsUnchanged()
    {
        const int count = 100_000;
        // Reallocating the five backing buffers for this count costs several
        // megabytes. Keep the guard far below that while allowing small,
        // runtime-specific bookkeeping allocations observed in test hosts.
        const long maxRuntimeBookkeepingBytes = 16 * 1024;
        var index = new DataGridScrollHeightIndex();
        Func<int, double> getHeight = static slot => 20 + slot % 3;
        Func<int, bool> isVisible = static _ => true;

        index.Rebuild(count, getHeight, isVisible);
        index.Rebuild(count, getHeight, isVisible);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        index.Rebuild(count, getHeight, isVisible);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.InRange(allocated, 0, maxRuntimeBookkeepingBytes);
        Assert.Equal(count, index.Count);
        Assert.True(index.TotalHeight > 0);
    }

    private static DataGridScrollHeightIndex CreateIndex(double[] heights)
    {
        var index = new DataGridScrollHeightIndex();
        index.Rebuild(heights.Length, slot => heights[slot], _ => true);
        return index;
    }
}
