// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using ProCharts.Skia;

namespace ProCharts.Controls;

public partial class ProChart
{
    public static readonly DirectProperty<ProChart, ChartModel?> ModelProperty =
        AvaloniaProperty.RegisterDirect<ProChart, ChartModel?>(nameof(Model), x => x.Model, (x, v) => x.Model = v);
    public ChartModel? Model { get => _model; set => SetAndRaise(ModelProperty, ref _model, value); }
    public static readonly DirectProperty<ProChart, IChartExportService?> ExportServiceProperty =
        AvaloniaProperty.RegisterDirect<ProChart, IChartExportService?>(nameof(ExportService), x => x.ExportService, (x, v) => x.ExportService = v);
    public IChartExportService? ExportService { get => _exportService; set => SetAndRaise(ExportServiceProperty, ref _exportService, value); }
    public static readonly DirectProperty<ProChart, ChartPointerTool> PointerToolProperty =
        AvaloniaProperty.RegisterDirect<ProChart, ChartPointerTool>(nameof(PointerTool), x => x.PointerTool, (x, v) => x.PointerTool = v);
    public ChartPointerTool PointerTool
    {
        get => _pointerTool;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (SetAndRaise(PointerToolProperty, ref _pointerTool, value) && Model != null)
                Model.Interaction.PointerTool = value;
        }
    }
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProChart, string>(nameof(Title), "Chart");
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<ProChart, string>(nameof(Subtitle), "");
    public string Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<ProChart, object?>(nameof(Header), null);
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ProChart, object?>(nameof(Footer), null);
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(HeaderTemplate), null);
    public IDataTemplate? HeaderTemplate { get => GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(FooterTemplate), null);
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> LegendItemTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(LegendItemTemplate), null);
    public IDataTemplate? LegendItemTemplate { get => GetValue(LegendItemTemplateProperty); set => SetValue(LegendItemTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> ToolTipTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(ToolTipTemplate), null);
    public IDataTemplate? ToolTipTemplate { get => GetValue(ToolTipTemplateProperty); set => SetValue(ToolTipTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> EmptyTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(EmptyTemplate), null);
    public IDataTemplate? EmptyTemplate { get => GetValue(EmptyTemplateProperty); set => SetValue(EmptyTemplateProperty, value); }
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<ProChart, object?>(nameof(EmptyContent), "No data to display");
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    public static readonly StyledProperty<ControlTheme?> LegendThemeProperty =
        AvaloniaProperty.Register<ProChart, ControlTheme?>(nameof(LegendTheme), null);
    public ControlTheme? LegendTheme { get => GetValue(LegendThemeProperty); set => SetValue(LegendThemeProperty, value); }
    public static readonly StyledProperty<ControlTheme?> ToolbarThemeProperty =
        AvaloniaProperty.Register<ProChart, ControlTheme?>(nameof(ToolbarTheme), null);
    public ControlTheme? ToolbarTheme { get => GetValue(ToolbarThemeProperty); set => SetValue(ToolbarThemeProperty, value); }
    public static readonly StyledProperty<ControlTheme?> NavigatorThemeProperty =
        AvaloniaProperty.Register<ProChart, ControlTheme?>(nameof(NavigatorTheme), null);
    public ControlTheme? NavigatorTheme { get => GetValue(NavigatorThemeProperty); set => SetValue(NavigatorThemeProperty, value); }
    public static readonly StyledProperty<SkiaChartStyle?> ChartStyleProperty =
        AvaloniaProperty.Register<ProChart, SkiaChartStyle?>(nameof(ChartStyle), null);
    public SkiaChartStyle? ChartStyle { get => GetValue(ChartStyleProperty); set => SetValue(ChartStyleProperty, value); }
    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowLegend), true);
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowToolbar), true);
    public bool ShowToolbar { get => GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }
    public static readonly StyledProperty<bool> ShowNavigatorProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowNavigator), true);
    public bool ShowNavigator { get => GetValue(ShowNavigatorProperty); set => SetValue(ShowNavigatorProperty, value); }
    public static readonly StyledProperty<bool> ShowCrosshairProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowCrosshair), true);
    public bool ShowCrosshair { get => GetValue(ShowCrosshairProperty); set => SetValue(ShowCrosshairProperty, value); }
    public static readonly StyledProperty<bool> ShowToolTipsProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowToolTips), true);
    public bool ShowToolTips { get => GetValue(ShowToolTipsProperty); set => SetValue(ShowToolTipsProperty, value); }
    public static readonly StyledProperty<bool> EnablePanZoomProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(EnablePanZoom), true);
    public bool EnablePanZoom { get => GetValue(EnablePanZoomProperty); set => SetValue(EnablePanZoomProperty, value); }
    public static readonly StyledProperty<bool> EnableKeyboardNavigationProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(EnableKeyboardNavigation), true);
    public bool EnableKeyboardNavigation { get => GetValue(EnableKeyboardNavigationProperty); set => SetValue(EnableKeyboardNavigationProperty, value); }
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(IsLoading), false);
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ProChart, string?>(nameof(ErrorMessage), null);
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public static readonly StyledProperty<double> ZoomStepProperty =
        AvaloniaProperty.Register<ProChart, double>(nameof(ZoomStep), 0.25, validate: value => double.IsFinite(value) && value > 0 && value <= 10);
    public double ZoomStep { get => GetValue(ZoomStepProperty); set => SetValue(ZoomStepProperty, value); }
    public static readonly StyledProperty<int> MinWindowCountProperty =
        AvaloniaProperty.Register<ProChart, int>(nameof(MinWindowCount), 2, validate: value => value >= 1);
    public int MinWindowCount { get => GetValue(MinWindowCountProperty); set => SetValue(MinWindowCountProperty, value); }
    private bool _hasData = false;
    public static readonly DirectProperty<ProChart, bool> HasDataProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(HasData), x => x.HasData);
    public bool HasData { get => _hasData; private set => SetAndRaise(HasDataProperty, ref _hasData, value); }
    private bool _isEmpty = true;
    public static readonly DirectProperty<ProChart, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsEmpty), x => x.IsEmpty);
    public bool IsEmpty { get => _isEmpty; private set => SetAndRaise(IsEmptyProperty, ref _isEmpty, value); }
    private bool _hasError = false;
    public static readonly DirectProperty<ProChart, bool> HasErrorProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(HasError), x => x.HasError);
    public bool HasError { get => _hasError; private set => SetAndRaise(HasErrorProperty, ref _hasError, value); }
    private bool _isLegendVisible = true;
    public static readonly DirectProperty<ProChart, bool> IsLegendVisibleProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsLegendVisible), x => x.IsLegendVisible);
    public bool IsLegendVisible { get => _isLegendVisible; private set => SetAndRaise(IsLegendVisibleProperty, ref _isLegendVisible, value); }
    private bool _isExporting = false;
    public static readonly DirectProperty<ProChart, bool> IsExportingProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsExporting), x => x.IsExporting);
    public bool IsExporting { get => _isExporting; private set => SetAndRaise(IsExportingProperty, ref _isExporting, value); }
    private string _statusText = "No data";
    public static readonly DirectProperty<ProChart, string> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<ProChart, string>(nameof(StatusText), x => x.StatusText);
    public string StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }
    private string? _exportError = null;
    public static readonly DirectProperty<ProChart, string?> ExportErrorProperty =
        AvaloniaProperty.RegisterDirect<ProChart, string?>(nameof(ExportError), x => x.ExportError);
    public string? ExportError { get => _exportError; private set => SetAndRaise(ExportErrorProperty, ref _exportError, value); }
    private ChartPointInfo? _hoverPoint = null;
    public static readonly DirectProperty<ProChart, ChartPointInfo?> HoverPointProperty =
        AvaloniaProperty.RegisterDirect<ProChart, ChartPointInfo?>(nameof(HoverPoint), x => x.HoverPoint);
    public ChartPointInfo? HoverPoint { get => _hoverPoint; private set => SetAndRaise(HoverPointProperty, ref _hoverPoint, value); }
    private bool _isToolTipVisible = false;
    public static readonly DirectProperty<ProChart, bool> IsToolTipVisibleProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsToolTipVisible), x => x.IsToolTipVisible);
    public bool IsToolTipVisible { get => _isToolTipVisible; private set => SetAndRaise(IsToolTipVisibleProperty, ref _isToolTipVisible, value); }
    private double _toolTipX = 0;
    public static readonly DirectProperty<ProChart, double> ToolTipXProperty =
        AvaloniaProperty.RegisterDirect<ProChart, double>(nameof(ToolTipX), x => x.ToolTipX);
    public double ToolTipX { get => _toolTipX; private set => SetAndRaise(ToolTipXProperty, ref _toolTipX, value); }
    private double _toolTipY = 0;
    public static readonly DirectProperty<ProChart, double> ToolTipYProperty =
        AvaloniaProperty.RegisterDirect<ProChart, double>(nameof(ToolTipY), x => x.ToolTipY);
    public double ToolTipY { get => _toolTipY; private set => SetAndRaise(ToolTipYProperty, ref _toolTipY, value); }

}
