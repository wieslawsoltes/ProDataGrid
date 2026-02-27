using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using DataGridSample.CustomDrawing;
using DataGridSample.Models;
using Xunit;

namespace DataGridSample.Tests;

public sealed class SkiaTextCellDrawOperationFactoryItemCacheTests
{
    [AvaloniaFact]
    public void TryMeasure_UsesItemContractCache_WhenEnabled()
    {
        var item = new TrackingItemCache();
        var factory = new SkiaTextCellDrawOperationFactory
        {
            UseItemCacheContract = true,
            ItemCacheSlot = 3
        };

        var firstContext = CreateMeasureContext(item, "cached text");
        var secondContext = CreateMeasureContext(item, "cached text");

        Assert.True(factory.TryMeasure(firstContext, out Size firstSize));
        Assert.True(factory.TryMeasure(secondContext, out Size secondSize));

        Assert.Equal(firstSize, secondSize);
        Assert.Equal(1, item.SetCount);
        Assert.True(item.TryGetCount >= 2);
    }

    [AvaloniaFact]
    public void TryMeasure_DoesNotUseItemContractCache_WhenDisabled()
    {
        var item = new TrackingItemCache();
        var factory = new SkiaTextCellDrawOperationFactory
        {
            UseItemCacheContract = false,
            ItemCacheSlot = 3
        };

        var firstContext = CreateMeasureContext(item, "cached text");
        var secondContext = CreateMeasureContext(item, "cached text");

        Assert.True(factory.TryMeasure(firstContext, out _));
        Assert.True(factory.TryMeasure(secondContext, out _));
        Assert.Equal(0, item.SetCount);
        Assert.Equal(0, item.TryGetCount);
    }

    [Fact]
    public void VariableHeightItem_ClearsContractCache_WhenTrackedPropertiesChange()
    {
        var item = new VariableHeightItem
        {
            Id = 1,
            Title = "Item 1",
            Description = "Line 1",
            LineCount = 1,
            ExpectedHeight = 36
        };
        object marker = new object();

        item.SetCellDrawCacheEntry(cacheSlot: 1, cacheKey: 123, marker);
        Assert.True(item.TryGetCellDrawCacheEntry(cacheSlot: 1, cacheKey: 123, out object cachedBeforeChange));
        Assert.Same(marker, cachedBeforeChange);

        item.Title = "Item 1";
        Assert.True(item.TryGetCellDrawCacheEntry(cacheSlot: 1, cacheKey: 123, out object cachedSameValue));
        Assert.Same(marker, cachedSameValue);

        item.Title = "Item 1 updated";
        Assert.False(item.TryGetCellDrawCacheEntry(cacheSlot: 1, cacheKey: 123, out _));
    }

    private static DataGridCellDrawOperationMeasureContext CreateMeasureContext(object item, string text)
    {
        return new DataGridCellDrawOperationMeasureContext(
            cell: new DataGridCell(),
            column: new DataGridTextColumn(),
            item: item,
            value: text,
            text: text,
            availableSize: new Size(500, double.PositiveInfinity),
            foreground: Brushes.Black,
            typeface: new Typeface("Inter"),
            fontSize: 14,
            textAlignment: TextAlignment.Left,
            textTrimming: TextTrimming.None,
            flowDirection: FlowDirection.LeftToRight,
            isCurrent: false,
            isSelected: false);
    }

    private sealed class TrackingItemCache : IDataGridCellDrawOperationItemCache
    {
        private readonly Dictionary<(int Slot, int Key), object> _cache = new();

        public int TryGetCount { get; private set; }
        public int SetCount { get; private set; }

        public bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value)
        {
            TryGetCount++;
            return _cache.TryGetValue((cacheSlot, cacheKey), out value!);
        }

        public void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value)
        {
            SetCount++;
            _cache[(cacheSlot, cacheKey)] = value;
        }
    }
}
