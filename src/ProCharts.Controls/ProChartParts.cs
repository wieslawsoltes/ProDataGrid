// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace ProCharts.Controls;

/// <summary>A fully templated legend composed from ordinary, keyboard-accessible Avalonia controls.</summary>
public class ProChartLegend : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<ChartLegendItem>?> ItemsProperty =
        AvaloniaProperty.Register<ProChartLegend, IReadOnlyList<ChartLegendItem>?>(nameof(Items));
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<ProChartLegend, IDataTemplate?>(nameof(ItemTemplate));
    public IReadOnlyList<ChartLegendItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
}

/// <summary>A lookless chart toolbar. Replace its ControlTheme to change or reorder every command.</summary>
public class ProChartToolbar : TemplatedControl
{
    public static readonly StyledProperty<ProChart?> TargetProperty =
        AvaloniaProperty.Register<ProChartToolbar, ProChart?>(nameof(Target));
    public ProChart? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
}

/// <summary>A lookless, strongly typed data tooltip. Its content may be completely replaced with an IDataTemplate.</summary>
public class ProChartToolTip : TemplatedControl
{
    public static readonly StyledProperty<ChartPointInfo?> PointProperty =
        AvaloniaProperty.Register<ProChartToolTip, ChartPointInfo?>(nameof(Point));
    public static readonly StyledProperty<IDataTemplate?> PointTemplateProperty =
        AvaloniaProperty.Register<ProChartToolTip, IDataTemplate?>(nameof(PointTemplate));
    public ChartPointInfo? Point { get => GetValue(PointProperty); set => SetValue(PointProperty, value); }
    public IDataTemplate? PointTemplate { get => GetValue(PointTemplateProperty); set => SetValue(PointTemplateProperty, value); }
}
