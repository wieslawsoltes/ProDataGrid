// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls.DataGridTests;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Themes;

public class DataGridThemeResourceCoverageTests
{
    private sealed record ControlThemeExpectation(object Key, Type TargetType);

    private sealed record ResourceExpectation(object Key, Type ExpectedType);

    public static IEnumerable<object[]> AllThemes()
    {
        yield return new object[] { DataGridTheme.Simple };
        yield return new object[] { DataGridTheme.SimpleV2 };
        yield return new object[] { DataGridTheme.Fluent };
        yield return new object[] { DataGridTheme.FluentV2 };
    }

    public static IEnumerable<object[]> SimpleThemes()
    {
        yield return new object[] { DataGridTheme.Simple };
        yield return new object[] { DataGridTheme.SimpleV2 };
    }

    public static IEnumerable<object[]> FluentThemes()
    {
        yield return new object[] { DataGridTheme.Fluent };
        yield return new object[] { DataGridTheme.FluentV2 };
    }

    [AvaloniaTheory]
    [MemberData(nameof(AllThemes))]
    public void Generic_control_themes_are_registered(DataGridTheme theme)
    {
        using var scope = new ThemeScope(theme, ThemeVariant.Default);

        foreach (var expectation in GenericControlThemeExpectations)
        {
            AssertControlTheme(scope.Application, ThemeVariant.Default, expectation);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(AllThemes))]
    public void Generic_templates_and_geometries_are_registered(DataGridTheme theme)
    {
        using var scope = new ThemeScope(theme, ThemeVariant.Default);

        foreach (var expectation in GenericTemplateExpectations.Concat(GenericGeometryExpectations))
        {
            AssertResource(scope.Application, ThemeVariant.Default, expectation);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(SimpleThemes))]
    public void Simple_theme_resources_are_registered(DataGridTheme theme)
    {
        using var scope = new ThemeScope(theme, ThemeVariant.Default);

        foreach (var expectation in SimpleResourceExpectations())
        {
            AssertResource(scope.Application, ThemeVariant.Default, expectation);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(FluentThemes))]
    public void Fluent_theme_default_resources_are_registered(DataGridTheme theme)
    {
        using var scope = new ThemeScope(theme, ThemeVariant.Default);

        foreach (var expectation in FluentDefaultResourceExpectations())
        {
            AssertResource(scope.Application, ThemeVariant.Default, expectation);
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(FluentThemes))]
    public void Fluent_theme_dark_resources_are_registered(DataGridTheme theme)
    {
        using var scope = new ThemeScope(theme, ThemeVariant.Dark);

        foreach (var expectation in FluentDarkResourceExpectations())
        {
            AssertResource(scope.Application, ThemeVariant.Dark, expectation);
        }
    }

    private sealed class ThemeScope : IDisposable
    {
        private readonly List<IStyle> _addedStyles = new();
        private readonly List<object> _addedResourceKeys = new();

        public ThemeScope(DataGridTheme theme, ThemeVariant variant)
        {
            Application = Application.Current ?? throw new InvalidOperationException("Application.Current is null.");

            foreach (var style in ThemeHelper.GetThemeStyles(theme))
            {
                Application.Styles.Add(style);
                _addedStyles.Add(style);
            }

            var existingKeys = new HashSet<object>(Application.Resources.Keys.Cast<object>());

            SeedBaseThemeResources(Application.Resources, theme, variant);

            foreach (var key in Application.Resources.Keys.Cast<object>())
            {
                if (!existingKeys.Contains(key))
                {
                    _addedResourceKeys.Add(key);
                }
            }
        }

        public Application Application { get; }

        public void Dispose()
        {
            foreach (var style in _addedStyles)
            {
                Application.Styles.Remove(style);
            }

            foreach (var key in _addedResourceKeys)
            {
                Application.Resources.Remove(key);
            }
        }
    }

    private static void SeedBaseThemeResources(IResourceDictionary resources, DataGridTheme theme, ThemeVariant variant)
    {
        switch (theme)
        {
            case DataGridTheme.Simple:
            case DataGridTheme.SimpleV2:
                AddBaseThemeResources(resources, new SimpleTheme(), variant, includeHighlightBrush: true, includeTooltipErrors: false);
                break;
            case DataGridTheme.Fluent:
            case DataGridTheme.FluentV2:
                AddBaseThemeResources(resources, new FluentTheme(), variant, includeHighlightBrush: false, includeTooltipErrors: true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
        }
    }

    private static void AddBaseThemeResources(IResourceDictionary resources, IStyle baseTheme, ThemeVariant variant, bool includeHighlightBrush, bool includeTooltipErrors)
    {
        AddResourceIfAvailable(resources, baseTheme, typeof(TextBox), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(ComboBox), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(AutoCompleteBox), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(MaskedTextBox), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(NumericUpDown), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(CalendarDatePicker), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(TimePicker), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(Slider), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(ToggleSwitch), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(ToggleButton), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(CheckBox), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(ProgressBar), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(Button), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(HyperlinkButton), variant);
        AddResourceIfAvailable(resources, baseTheme, typeof(DataValidationErrors), variant);

        EnsureControlTheme(resources, typeof(TextBox));
        EnsureControlTheme(resources, typeof(ComboBox));
        EnsureControlTheme(resources, typeof(AutoCompleteBox));
        EnsureControlTheme(resources, typeof(MaskedTextBox));
        EnsureControlTheme(resources, typeof(NumericUpDown));
        EnsureControlTheme(resources, typeof(CalendarDatePicker));
        EnsureControlTheme(resources, typeof(TimePicker));
        EnsureControlTheme(resources, typeof(Slider));
        EnsureControlTheme(resources, typeof(ToggleSwitch));
        EnsureControlTheme(resources, typeof(ToggleButton));
        EnsureControlTheme(resources, typeof(CheckBox));
        EnsureControlTheme(resources, typeof(ProgressBar));
        EnsureControlTheme(resources, typeof(Button));
        EnsureControlTheme(resources, typeof(HyperlinkButton));
        EnsureControlTheme(resources, typeof(DataValidationErrors));

        if (includeHighlightBrush)
        {
            AddResourceIfAvailable(resources, baseTheme, "HighlightBrush", variant);
            EnsureBrushResource(resources, "HighlightBrush");
        }

        if (includeTooltipErrors)
        {
            AddResourceIfAvailable(resources, baseTheme, "TooltipDataValidationErrors", variant);
            EnsureStyleResource(resources, "TooltipDataValidationErrors");
        }
    }

    private static void AddResourceIfAvailable(IResourceDictionary resources, IStyle baseTheme, object key, ThemeVariant variant)
    {
        if (resources.ContainsKey(key))
        {
            return;
        }

        if (baseTheme.TryGetResource(key, variant, out var value) ||
            baseTheme.TryGetResource(key, ThemeVariant.Default, out value))
        {
            resources.Add(key, value);
        }
    }

    private static void EnsureControlTheme(IResourceDictionary resources, Type targetType)
    {
        if (!resources.ContainsKey(targetType))
        {
            resources.Add(targetType, new ControlTheme(targetType));
        }
    }

    private static void EnsureBrushResource(IResourceDictionary resources, string key)
    {
        if (!resources.ContainsKey(key))
        {
            resources.Add(key, new SolidColorBrush(Colors.Transparent));
        }
    }

    private static void EnsureStyleResource(IResourceDictionary resources, string key)
    {
        if (!resources.ContainsKey(key))
        {
            resources.Add(key, new ControlTheme(typeof(DataValidationErrors)));
        }
    }

    private static void AssertControlTheme(IResourceHost resources, ThemeVariant variant, ControlThemeExpectation expectation)
    {
        Assert.True(resources.TryFindResource(expectation.Key, variant, out var value),
            $"Missing ControlTheme resource '{expectation.Key}'.");

        var theme = Assert.IsType<ControlTheme>(value);
        Assert.Equal(expectation.TargetType, theme.TargetType);
    }

    private static void AssertResource(IResourceHost resources, ThemeVariant variant, ResourceExpectation expectation)
    {
        Assert.True(resources.TryFindResource(expectation.Key, variant, out var value),
            $"Missing resource '{expectation.Key}'.");

        Assert.IsAssignableFrom(expectation.ExpectedType, value);
    }

    private static IEnumerable<ResourceExpectation> SimpleResourceExpectations()
    {
        foreach (var key in SimpleBrushKeys)
        {
            yield return new ResourceExpectation(key, typeof(IBrush));
        }

        foreach (var key in SimpleDoubleKeys)
        {
            yield return new ResourceExpectation(key, typeof(double));
        }

        foreach (var key in SimpleGridLengthKeys)
        {
            yield return new ResourceExpectation(key, typeof(GridLength));
        }

        foreach (var key in SimpleThicknessKeys)
        {
            yield return new ResourceExpectation(key, typeof(Thickness));
        }

        foreach (var key in SimpleCornerRadiusKeys)
        {
            yield return new ResourceExpectation(key, typeof(CornerRadius));
        }
    }

    private static IEnumerable<ResourceExpectation> FluentDefaultResourceExpectations()
    {
        foreach (var expectation in FluentCommonResourceExpectations())
        {
            yield return expectation;
        }

        foreach (var key in FluentThemeBrushKeys)
        {
            yield return new ResourceExpectation(key, typeof(IBrush));
        }
    }

    private static IEnumerable<ResourceExpectation> FluentDarkResourceExpectations()
    {
        foreach (var expectation in FluentCommonResourceExpectations())
        {
            yield return expectation;
        }

        foreach (var key in FluentThemeBrushKeys.Concat(FluentDarkOnlyBrushKeys))
        {
            yield return new ResourceExpectation(key, typeof(IBrush));
        }
    }

    private static IEnumerable<ResourceExpectation> FluentCommonResourceExpectations()
    {
        foreach (var key in FluentBrushKeys)
        {
            yield return new ResourceExpectation(key, typeof(IBrush));
        }

        foreach (var key in FluentDoubleKeys)
        {
            yield return new ResourceExpectation(key, typeof(double));
        }

        foreach (var key in FluentGridLengthKeys)
        {
            yield return new ResourceExpectation(key, typeof(GridLength));
        }

        foreach (var key in FluentThicknessKeys)
        {
            yield return new ResourceExpectation(key, typeof(Thickness));
        }

        foreach (var key in FluentCornerRadiusKeys)
        {
            yield return new ResourceExpectation(key, typeof(CornerRadius));
        }

        yield return new ResourceExpectation("DataGridGridLinesVisibility", typeof(DataGridGridLinesVisibility));
        yield return new ResourceExpectation("DataGridCellDataValidationErrorsTheme", typeof(IStyle));
        yield return new ResourceExpectation("DataGridCellDataValidationWarningsTheme", typeof(IStyle));
        yield return new ResourceExpectation("DataGridCellDataValidationInfoTheme", typeof(IStyle));
    }

    private static readonly ControlThemeExpectation[] GenericControlThemeExpectations =
    {
        new("DataGridFilterFlyoutPresenterTheme", typeof(FlyoutPresenter)),
        new("DataGridFilterButtonTheme", typeof(Button)),
        new("DataGridFilterButtonFilteredTheme", typeof(Button)),
        new("DataGridCellDataValidationErrorsTheme", typeof(DataValidationErrors)),
        new("DataGridCellDataValidationWarningsTheme", typeof(DataValidationErrors)),
        new("DataGridCellDataValidationInfoTheme", typeof(DataValidationErrors)),
        new("DataGridCellTextBlockTheme", typeof(TextBlock)),
        new("DataGridCellTextBoxTheme", typeof(TextBox)),
        new("DataGridCellAutoCompleteTextBlockTheme", typeof(TextBlock)),
        new("DataGridCellAutoCompleteBoxTheme", typeof(AutoCompleteBox)),
        new("DataGridCellComboBoxTheme", typeof(ComboBox)),
        new("DataGridCellComboBoxDisplayTheme", typeof(ComboBox)),
        new("DataGridCellMaskedTextBlockTheme", typeof(TextBlock)),
        new("DataGridCellMaskedTextBoxTheme", typeof(MaskedTextBox)),
        new("DataGridCellNumericTextBlockTheme", typeof(TextBlock)),
        new("DataGridCellNumericUpDownTheme", typeof(NumericUpDown)),
        new("DataGridCellDateTextBlockTheme", typeof(TextBlock)),
        new("DataGridCellDatePickerTheme", typeof(CalendarDatePicker)),
        new("DataGridCellTimeTextBlockTheme", typeof(TextBlock)),
        new("DataGridCellTimePickerTheme", typeof(TimePicker)),
        new("DataGridCellSliderTheme", typeof(Slider)),
        new("DataGridCellSliderDisplayTheme", typeof(Slider)),
        new("DataGridCellToggleSwitchTheme", typeof(ToggleSwitch)),
        new("DataGridCellToggleButtonTheme", typeof(ToggleButton)),
        new("DataGridCellCheckBoxTheme", typeof(CheckBox)),
        new("DataGridCellProgressBarTheme", typeof(ProgressBar)),
        new("DataGridCellButtonTheme", typeof(Button)),
        new("DataGridCellHyperlinkButtonTheme", typeof(HyperlinkButton)),
        new("DataGridHierarchicalExpanderTheme", typeof(ToggleButton)),
        new(typeof(DataGridHierarchicalPresenter), typeof(DataGridHierarchicalPresenter)),
        new(typeof(DataGridCell), typeof(DataGridCell)),
        new(typeof(DataGridColumnHeader), typeof(DataGridColumnHeader)),
        new("DataGridTopLeftColumnHeader", typeof(DataGridColumnHeader)),
        new(typeof(DataGridRowHeader), typeof(DataGridRowHeader)),
        new(typeof(DataGridRow), typeof(DataGridRow)),
        new("DataGridRowGroupExpanderButtonTheme", typeof(ToggleButton)),
        new(typeof(DataGridRowGroupHeader), typeof(DataGridRowGroupHeader)),
        new(typeof(DataGridSummaryRow), typeof(DataGridSummaryRow)),
        new(typeof(DataGridSummaryCell), typeof(DataGridSummaryCell)),
        new(typeof(DataGridColumnChooser), typeof(DataGridColumnChooser)),
        new(typeof(DataGridRowGroupFooter), typeof(DataGridRowGroupFooter)),
        new(typeof(DataGrid), typeof(DataGrid))
    };

    private static readonly ResourceExpectation[] GenericTemplateExpectations =
    {
        new("DataGridFilterTextEditorTemplate", typeof(IDataTemplate)),
        new("DataGridFilterNumberEditorTemplate", typeof(IDataTemplate)),
        new("DataGridFilterDateEditorTemplate", typeof(IDataTemplate)),
        new("DataGridFilterEnumEditorTemplate", typeof(IDataTemplate)),
        new("DataGridColumnChooserItemTemplate", typeof(IDataTemplate)),
        new("DataGridHierarchicalCellTemplate", typeof(IDataTemplate))
    };

    private static readonly ResourceExpectation[] GenericGeometryExpectations =
    {
        new("DataGridSortIconDescendingPath", typeof(StreamGeometry)),
        new("DataGridSortIconAscendingPath", typeof(StreamGeometry)),
        new("DataGridFilterIconPath", typeof(StreamGeometry)),
        new("DataGridFilterIconFilledPath", typeof(StreamGeometry)),
        new("DataGridRowGroupHeaderIconClosedPath", typeof(StreamGeometry)),
        new("DataGridRowGroupHeaderIconOpenedPath", typeof(StreamGeometry)),
        new("DataGridDragGripIconPath", typeof(StreamGeometry))
    };

    private static readonly string[] SimpleBrushKeys =
    {
        "DataGridColumnHeaderForegroundBrush",
        "DataGridColumnHeaderBackgroundBrush",
        "DataGridColumnHeaderHoveredBackgroundBrush",
        "DataGridColumnHeaderPressedBackgroundBrush",
        "DataGridColumnHeaderDraggedBackgroundBrush",
        "DataGridRowGroupHeaderBackgroundBrush",
        "DataGridRowGroupHeaderPressedBackgroundBrush",
        "DataGridRowGroupHeaderForegroundBrush",
        "DataGridRowGroupHeaderHoveredBackgroundBrush",
        "DataGridRowHoveredBackgroundBrush",
        "DataGridCellHoveredBackgroundBrush",
        "DataGridRowInvalidBrush",
        "DataGridRowWarningBrush",
        "DataGridRowInfoBrush",
        "DataGridRowSelectedBackgroundBrush",
        "DataGridRowSelectedHoveredBackgroundBrush",
        "DataGridRowSelectedUnfocusedBackgroundBrush",
        "DataGridRowSelectedHoveredUnfocusedBackgroundBrush",
        "DataGridCellSelectedBackgroundBrush",
        "DataGridCellSelectedHoveredBackgroundBrush",
        "DataGridCellSelectedUnfocusedBackgroundBrush",
        "DataGridCellSelectedHoveredUnfocusedBackgroundBrush",
        "DataGridCellFocusVisualPrimaryBrush",
        "DataGridCellFocusVisualSecondaryBrush",
        "DataGridCellInvalidBrush",
        "DataGridCellWarningBrush",
        "DataGridCellInfoBrush",
        "DataGridGridLinesBrush",
        "DataGridDetailsPresenterBackgroundBrush",
        "DataGridRowBackgroundBrush",
        "DataGridCellBackgroundBrush",
        "DataGridCurrencyVisualPrimaryBrush",
        "DataGridFillerColumnGridLinesBrush",
        "DataGridBackgroundBrush",
        "DataGridBorderBrush",
        "DataGridDropLocationIndicatorBrush",
        "DataGridFilterFlyoutBackgroundBrush",
        "DataGridSearchMatchBrush",
        "DataGridSearchCurrentBrush",
        "DataGridSearchMatchForegroundBrush",
        "DataGridRowDropIndicatorBrush"
    };

    private static readonly string[] SimpleDoubleKeys =
    {
        "DataGridRowSelectedBackgroundOpacity",
        "DataGridRowSelectedHoveredBackgroundOpacity",
        "DataGridRowSelectedUnfocusedBackgroundOpacity",
        "DataGridRowSelectedHoveredUnfocusedBackgroundOpacity",
        "DataGridSortIconMinWidth",
        "DataGridCellFontSize",
        "DataGridCellMinHeight",
        "DataGridColumnHeaderFontSize",
        "DataGridColumnHeaderMinHeight",
        "DataGridRowGroupHeaderFontSize",
        "DataGridRowGroupHeaderMinHeight",
        "DataGridColumnHeaderIconSize",
        "DataGridColumnHeaderIconSpacing",
        "DataGridRowHeaderDragGripSize",
        "DataGridRowGroupHeaderExpanderSize",
        "DataGridHierarchicalExpanderSize",
        "DataGridHierarchicalExpanderGlyphSize",
        "DataGridDropLocationIndicatorWidth"
    };

    private static readonly string[] SimpleGridLengthKeys =
    {
        "DataGridRowHeaderIndicatorWidth"
    };

    private static readonly string[] SimpleThicknessKeys =
    {
        "DataGridCellTextBlockMargin",
        "DataGridCellComboBoxPadding",
        "DataGridColumnHeaderPadding",
        "DataGridColumnHeaderSortIconMargin",
        "DataGridRowGroupHeaderExpanderMargin",
        "DataGridRowGroupHeaderContentMargin",
        "DataGridRowGroupHeaderTextMargin",
        "DataGridHierarchicalPresenterContentMargin",
        "DataGridFilterFlyoutPadding",
        "DataGridBorderThickness"
    };

    private static readonly string[] SimpleCornerRadiusKeys =
    {
        "DataGridFilterFlyoutCornerRadius",
        "DataGridCellCornerRadius"
    };

    private static readonly string[] FluentThemeBrushKeys =
    {
        "DataGridColumnHeaderForegroundBrush",
        "DataGridColumnHeaderBackgroundBrush",
        "DataGridColumnHeaderHoveredBackgroundBrush",
        "DataGridColumnHeaderPressedBackgroundBrush",
        "DataGridColumnHeaderDraggedBackgroundBrush",
        "DataGridRowGroupHeaderBackgroundBrush",
        "DataGridRowGroupHeaderPressedBackgroundBrush",
        "DataGridRowGroupHeaderForegroundBrush",
        "DataGridRowGroupHeaderHoveredBackgroundBrush",
        "DataGridRowHoveredBackgroundBrush",
        "DataGridCellHoveredBackgroundBrush",
        "DataGridRowInvalidBrush",
        "DataGridRowWarningBrush",
        "DataGridRowInfoBrush",
        "DataGridRowDropIndicatorBrush",
        "DataGridRowSelectedBackgroundBrush",
        "DataGridRowSelectedHoveredBackgroundBrush",
        "DataGridRowSelectedUnfocusedBackgroundBrush",
        "DataGridRowSelectedHoveredUnfocusedBackgroundBrush",
        "DataGridCellSelectedBackgroundBrush",
        "DataGridCellSelectedHoveredBackgroundBrush",
        "DataGridCellSelectedUnfocusedBackgroundBrush",
        "DataGridCellSelectedHoveredUnfocusedBackgroundBrush",
        "DataGridCellFocusVisualPrimaryBrush",
        "DataGridCellFocusVisualSecondaryBrush",
        "DataGridCellInvalidBrush",
        "DataGridCellWarningBrush",
        "DataGridCellInfoBrush",
        "DataGridGridLinesBrush",
        "DataGridDetailsPresenterBackgroundBrush",
        "DataGridSummaryRowBackgroundBrush",
        "DataGridSummaryRowForegroundBrush",
        "DataGridTotalSummaryRowBackgroundBrush",
        "DataGridGroupSummaryRowBackgroundBrush",
        "DataGridFilterFlyoutBackgroundBrush",
        "DataGridSearchMatchBrush",
        "DataGridSearchCurrentBrush",
        "DataGridSearchMatchForegroundBrush"
    };

    private static readonly string[] FluentDarkOnlyBrushKeys = Array.Empty<string>();

    private static readonly string[] FluentBrushKeys =
    {
        "DataGridTransparentBrush",
        "DataGridRowBackgroundBrush",
        "DataGridCellBackgroundBrush",
        "DataGridCurrencyVisualPrimaryBrush",
        "DataGridFillerColumnGridLinesBrush",
        "DataGridBackgroundBrush",
        "DataGridBorderBrush",
        "DataGridDropLocationIndicatorBackground",
        "DataGridDropLocationIndicatorBrush"
    };

    private static readonly string[] FluentDoubleKeys =
    {
        "ListAccentLowOpacity",
        "ListAccentMediumOpacity",
        "DataGridSortIconMinWidth",
        "DataGridCellFontSize",
        "DataGridCellMinHeight",
        "DataGridColumnHeaderFontSize",
        "DataGridColumnHeaderMinHeight",
        "DataGridRowGroupHeaderFontSize",
        "DataGridRowGroupHeaderMinHeight",
        "DataGridSummaryRowMinHeight",
        "DataGridColumnHeaderIconSize",
        "DataGridColumnHeaderIconSpacing",
        "DataGridRowHeaderDragGripSize",
        "DataGridRowGroupHeaderExpanderSize",
        "DataGridHierarchicalExpanderSize",
        "DataGridHierarchicalExpanderGlyphSize",
        "DataGridDropLocationIndicatorWidth",
        "DataGridRowSelectedBackgroundOpacity",
        "DataGridRowSelectedHoveredBackgroundOpacity",
        "DataGridRowSelectedUnfocusedBackgroundOpacity",
        "DataGridRowSelectedHoveredUnfocusedBackgroundOpacity"
    };

    private static readonly string[] FluentGridLengthKeys =
    {
        "DataGridRowHeaderIndicatorWidth"
    };

    private static readonly string[] FluentThicknessKeys =
    {
        "DataGridCellTextBlockMargin",
        "DataGridCellComboBoxPadding",
        "DataGridColumnHeaderPadding",
        "DataGridColumnHeaderSortIconMargin",
        "DataGridRowGroupHeaderExpanderMargin",
        "DataGridRowGroupHeaderContentMargin",
        "DataGridRowGroupHeaderTextMargin",
        "DataGridHierarchicalPresenterContentMargin",
        "DataGridFilterFlyoutPadding",
        "DataGridBorderThickness"
    };

    private static readonly string[] FluentCornerRadiusKeys =
    {
        "DataGridFilterFlyoutCornerRadius",
        "DataGridCellCornerRadius"
    };
}

public class DataGridThemeTemplateCoverageTests
{
    public static IEnumerable<object[]> V2Themes()
    {
        yield return new object[] { DataGridTheme.SimpleV2 };
        yield return new object[] { DataGridTheme.FluentV2 };
    }

    public static IEnumerable<object[]> V1Themes()
    {
        yield return new object[] { DataGridTheme.Simple };
        yield return new object[] { DataGridTheme.Fluent };
    }

    [AvaloniaTheory]
    [MemberData(nameof(V2Themes))]
    public void DataGrid_v2_template_includes_scroll_viewer(DataGridTheme theme)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300
        };

        window.SetThemeStyles(theme);

        var grid = CreateSampleGrid();
        window.Content = grid;
        window.Show();

        try
        {
            grid.ApplyTemplate();
            grid.UpdateLayout();

            var hasScrollViewer = grid
                .GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Any(viewer => viewer.Name == "PART_ScrollViewer");

            Assert.True(hasScrollViewer);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(V1Themes))]
    public void DataGrid_v1_template_excludes_scroll_viewer(DataGridTheme theme)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300
        };

        window.SetThemeStyles(theme);

        var grid = CreateSampleGrid();
        window.Content = grid;
        window.Show();

        try
        {
            grid.ApplyTemplate();
            grid.UpdateLayout();

            var hasScrollViewer = grid
                .GetVisualDescendants()
                .OfType<ScrollViewer>()
                .Any(viewer => viewer.Name == "PART_ScrollViewer");

            Assert.False(hasScrollViewer);
        }
        finally
        {
            window.Close();
        }
    }

    private static DataGrid CreateSampleGrid()
    {
        return new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = new[] { new SampleItem("A") },
            Columns = new ObservableCollection<DataGridColumn>
            {
                new DataGridTextColumn
                {
                    Header = "Name",
                    Binding = new Binding(nameof(SampleItem.Name))
                }
            }
        };
    }

    private sealed class SampleItem
    {
        public SampleItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
