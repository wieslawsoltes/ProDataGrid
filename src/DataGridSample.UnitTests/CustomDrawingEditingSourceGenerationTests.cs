using Avalonia.Controls;
using DataGridSample.CustomDrawing;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class CustomDrawingEditingSourceGenerationTests
{
    [Fact]
    public void ViewModel_uses_generated_custom_drawing_definitions()
    {
        var viewModel = new CustomDrawingEditingViewModel();

        Assert.Equal(4, viewModel.ColumnDefinitions.Count);
        for (int index = 0; index < viewModel.ColumnDefinitions.Count; index++)
        {
            var definition = Assert.IsType<DataGridCustomDrawingColumnDefinition>(viewModel.ColumnDefinitions[index]);
            Assert.IsType<SkiaTextCellDrawOperationFactory>(definition.DrawOperationFactory);
            Assert.Equal(DataGridCustomDrawingMode.DrawOperation, definition.DrawingMode);
            Assert.Equal(DataGridCustomDrawingRenderBackend.CompositionCustomVisual, definition.RenderBackend);
            Assert.Equal(DataGridCustomDrawingTextLayoutCacheMode.Shared, definition.TextLayoutCacheMode);
            Assert.Equal(1024, definition.SharedTextLayoutCacheCapacity);
            Assert.True(definition.DrawOperationLayoutFastPath);
        }
    }

    [Fact]
    public void Row_uses_generated_bounded_slot_cache()
    {
        Assert.Equal(0, CustomDrawingEditingRow.IdCellDrawCacheSlot);
        Assert.Equal(1, CustomDrawingEditingRow.TitleCellDrawCacheSlot);
        Assert.Equal(2, CustomDrawingEditingRow.NotesCellDrawCacheSlot);
        Assert.Equal(3, CustomDrawingEditingRow.CategoryCellDrawCacheSlot);

        var row = new CustomDrawingEditingRow();
        IDataGridCellDrawOperationItemCache cache = row;
        cache.SetCellDrawCacheEntry(CustomDrawingEditingRow.TitleCellDrawCacheSlot, 17, "metrics");

        Assert.True(cache.TryGetCellDrawCacheEntry(
            CustomDrawingEditingRow.TitleCellDrawCacheSlot,
            17,
            out object cached));
        Assert.Equal("metrics", cached);

        row.ClearGeneratedCellDrawCache(CustomDrawingEditingRow.TitleCellDrawCacheSlot);
        Assert.False(cache.TryGetCellDrawCacheEntry(
            CustomDrawingEditingRow.TitleCellDrawCacheSlot,
            17,
            out _));

        cache.SetCellDrawCacheEntry(4, 18, "outside-bound");
        Assert.False(cache.TryGetCellDrawCacheEntry(4, 18, out _));
    }
}
