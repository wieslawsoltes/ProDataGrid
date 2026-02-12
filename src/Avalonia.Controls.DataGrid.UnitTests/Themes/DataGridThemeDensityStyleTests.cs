// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls.Themes;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Themes;

public class DataGridThemeDensityStyleTests
{
    private readonly record struct DensityExpectation(
        double CellMinHeight,
        double ColumnHeaderMinHeight,
        double SummaryRowMinHeight,
        double FilterButtonSize,
        double HierarchicalIndent,
        Thickness ColumnHeaderPadding);

    [AvaloniaFact]
    public void Fluent_density_style_should_switch_runtime_resources()
    {
        var theme = new DataGridFluentTheme();

        AssertDensitySwitch(
            resources: theme,
            setDensity: density => theme.DensityStyle = density,
            normal: new DensityExpectation(
                CellMinHeight: 32d,
                ColumnHeaderMinHeight: 32d,
                SummaryRowMinHeight: 32d,
                FilterButtonSize: 22d,
                HierarchicalIndent: 16d,
                ColumnHeaderPadding: new Thickness(12, 0, 0, 0)),
            compact: new DensityExpectation(
                CellMinHeight: 26d,
                ColumnHeaderMinHeight: 26d,
                SummaryRowMinHeight: 24d,
                FilterButtonSize: 18d,
                HierarchicalIndent: 12d,
                ColumnHeaderPadding: new Thickness(8, 0, 0, 0)));
    }

    [AvaloniaFact]
    public void Simple_density_style_should_switch_runtime_resources()
    {
        var theme = new DataGridSimpleTheme();

        AssertDensitySwitch(
            resources: theme,
            setDensity: density => theme.DensityStyle = density,
            normal: new DensityExpectation(
                CellMinHeight: 24d,
                ColumnHeaderMinHeight: 24d,
                SummaryRowMinHeight: 24d,
                FilterButtonSize: 22d,
                HierarchicalIndent: 16d,
                ColumnHeaderPadding: new Thickness(4)),
            compact: new DensityExpectation(
                CellMinHeight: 20d,
                ColumnHeaderMinHeight: 20d,
                SummaryRowMinHeight: 20d,
                FilterButtonSize: 18d,
                HierarchicalIndent: 12d,
                ColumnHeaderPadding: new Thickness(3)));
    }

    private static void AssertDensitySwitch(
        IResourceNode resources,
        Action<DataGridDensityStyle> setDensity,
        DensityExpectation normal,
        DensityExpectation compact)
    {
        setDensity(DataGridDensityStyle.Normal);
        AssertDensityExpectation(resources, normal);

        setDensity(DataGridDensityStyle.Compact);
        AssertDensityExpectation(resources, compact);
    }

    private static void AssertDensityExpectation(IResourceNode resources, DensityExpectation expected)
    {
        Assert.True(resources.TryGetResource("DataGridCellMinHeight", ThemeVariant.Default, out var cellMinHeightValue));
        Assert.Equal(expected.CellMinHeight, Assert.IsType<double>(cellMinHeightValue));

        Assert.True(resources.TryGetResource("DataGridColumnHeaderMinHeight", ThemeVariant.Default, out var headerMinHeightValue));
        Assert.Equal(expected.ColumnHeaderMinHeight, Assert.IsType<double>(headerMinHeightValue));

        Assert.True(resources.TryGetResource("DataGridSummaryRowMinHeight", ThemeVariant.Default, out var summaryMinHeightValue));
        Assert.Equal(expected.SummaryRowMinHeight, Assert.IsType<double>(summaryMinHeightValue));

        Assert.True(resources.TryGetResource("DataGridFilterButtonSize", ThemeVariant.Default, out var filterButtonSizeValue));
        Assert.Equal(expected.FilterButtonSize, Assert.IsType<double>(filterButtonSizeValue));

        Assert.True(resources.TryGetResource("DataGridHierarchicalPresenterIndent", ThemeVariant.Default, out var hierarchicalIndentValue));
        Assert.Equal(expected.HierarchicalIndent, Assert.IsType<double>(hierarchicalIndentValue));

        Assert.True(resources.TryGetResource("DataGridColumnHeaderPadding", ThemeVariant.Default, out var columnHeaderPaddingValue));
        Assert.Equal(expected.ColumnHeaderPadding, Assert.IsType<Thickness>(columnHeaderPaddingValue));
    }
}
