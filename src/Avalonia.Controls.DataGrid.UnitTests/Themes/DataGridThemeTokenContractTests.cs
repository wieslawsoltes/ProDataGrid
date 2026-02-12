// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls.Themes;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Themes;

public class DataGridThemeTokenContractTests
{
    private static readonly string[] AddedTokenKeys =
    [
        "DataGridFilterEditorSpacing",
        "DataGridFilterEditorActionSpacing",
        "DataGridFilterTextEditorWidth",
        "DataGridFilterNumberEditorWidth",
        "DataGridFilterNumberInputWidth",
        "DataGridFilterDateEditorWidth",
        "DataGridFilterDateEditorFieldSpacing",
        "DataGridFilterEnumEditorWidth",
        "DataGridPivotHeaderSegmentsSpacing",
        "DataGridColumnBandHeaderSegmentsSpacing",
        "DataGridFilterButtonSize",
        "DataGridFilterGlyphSize",
        "DataGridFilterGlyphStrokeThickness",
        "DataGridValidationInlineIndicatorMargin",
        "DataGridValidationInlineIndicatorSize",
        "DataGridValidationInlineIndicatorStrokeThickness",
        "DataGridCellValidationStrokeThickness",
        "DataGridCellFocusPrimaryStrokeThickness",
        "DataGridCellFocusSecondaryMargin",
        "DataGridCellFocusSecondaryStrokeThickness",
        "DataGridCellGridLineWidth",
        "DataGridColumnHeaderVerticalSeparatorWidth",
        "DataGridColumnHeaderDragGripIconStrokeThickness",
        "DataGridColumnHeaderDragIndicatorOpacity",
        "DataGridTopLeftColumnHeaderBorderThickness",
        "DataGridTopLeftColumnHeaderSeparatorStrokeThickness",
        "DataGridTopLeftColumnHeaderSeparatorHeight",
        "DataGridRowHeaderBorderThickness",
        "DataGridRowHeaderHorizontalSeparatorHeight",
        "DataGridRowHeaderHorizontalSeparatorMargin",
        "DataGridRowHeaderDragGripIconStrokeThickness",
        "DataGridHierarchicalPresenterIndent",
        "DataGridHierarchicalDisabledGlyphOpacity",
        "DataGridRowBottomGridLineHeight",
        "DataGridRowGroupBottomGridLineHeight",
        "DataGridRowInvalidOpacity",
        "DataGridRowWarningOpacity",
        "DataGridRowInfoOpacity",
        "DataGridRowSearchMatchOpacity",
        "DataGridRowSearchCurrentOpacity",
        "DataGridRowDraggingOpacity",
        "DataGridRowDragOverInsideOpacity",
        "DataGridRowGroupCurrencyStrokeThickness",
        "DataGridRowGroupFocusPrimaryStrokeThickness",
        "DataGridRowGroupFocusSecondaryMargin",
        "DataGridRowGroupFocusSecondaryStrokeThickness",
        "DataGridSummaryRowBorderThickness",
        "DataGridSummaryCellBorderThickness",
        "DataGridRowGroupFooterBorderThickness",
        "DataGridFillHandleBorderThickness",
        "DataGridFillHandleCornerRadius",
        "DataGridSelectionOverlayInitialSize",
        "DataGridColumnHeadersSeparatorHeight",
        "DataGridColumnChooserItemsSpacing",
        "DataGridColumnChooserFlyoutPadding",
        "DataGridColumnChooserFlyoutMaxWidth",
        "DataGridColumnChooserFlyoutMaxHeight"
    ];

    [AvaloniaFact]
    public void Fluent_theme_should_expose_added_design_tokens()
    {
        AssertTokenContract(new DataGridFluentTheme());
    }

    [AvaloniaFact]
    public void Simple_theme_should_expose_added_design_tokens()
    {
        AssertTokenContract(new DataGridSimpleTheme());
    }

    private static void AssertTokenContract(IResourceNode resources)
    {
        foreach (var key in AddedTokenKeys)
        {
            Assert.True(resources.TryGetResource(key, ThemeVariant.Default, out var value), $"Missing design token '{key}'.");
            Assert.NotNull(value);
        }
    }
}
