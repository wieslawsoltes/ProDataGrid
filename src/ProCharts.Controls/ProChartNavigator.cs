// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace ProCharts.Controls;

/// <summary>A lookless category-window navigator. The native Slider supplies keyboard, pointer and touch interaction.</summary>
public class ProChartNavigator : TemplatedControl
{
    private ChartModel? _model;
    private double _position;
    private double _maximum;
    private bool _canNavigate;
    private string _summary = "All data";
    private bool _attached;
    private bool _updating;
    public static readonly DirectProperty<ProChartNavigator, ChartModel?> ModelProperty =
        AvaloniaProperty.RegisterDirect<ProChartNavigator, ChartModel?>(nameof(Model), x => x.Model, (x, v) => x.Model = v);
    public ChartModel? Model { get => _model; set => SetAndRaise(ModelProperty, ref _model, value); }
    public static readonly DirectProperty<ProChartNavigator, double> PositionProperty =
        AvaloniaProperty.RegisterDirect<ProChartNavigator, double>(nameof(Position), x => x.Position, (x, v) => x.Position = v);
    public double Position
    {
        get => _position;
        set
        {
            if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            value = Math.Clamp(value, 0, Maximum);
            if (SetAndRaise(PositionProperty, ref _position, value) && !_updating && Model != null &&
                Model.TryGetVisibleWindow(out _, out _, out int count))
                Model.SetVisibleWindow((int)Math.Round(value), count);
        }
    }
    public static readonly DirectProperty<ProChartNavigator, double> MaximumProperty =
        AvaloniaProperty.RegisterDirect<ProChartNavigator, double>(nameof(Maximum), x => x.Maximum);
    public double Maximum { get => _maximum; private set => SetAndRaise(MaximumProperty, ref _maximum, value); }
    public static readonly DirectProperty<ProChartNavigator, bool> CanNavigateProperty =
        AvaloniaProperty.RegisterDirect<ProChartNavigator, bool>(nameof(CanNavigate), x => x.CanNavigate);
    public bool CanNavigate { get => _canNavigate; private set => SetAndRaise(CanNavigateProperty, ref _canNavigate, value); }
    public static readonly DirectProperty<ProChartNavigator, string> SummaryProperty =
        AvaloniaProperty.RegisterDirect<ProChartNavigator, string>(nameof(Summary), x => x.Summary);
    public string Summary { get => _summary; private set => SetAndRaise(SummaryProperty, ref _summary, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ModelProperty) return;
        if (_attached && change.OldValue is ChartModel oldModel) oldModel.PropertyChanged -= OnModelChanged;
        if (_attached && Model != null) Model.PropertyChanged += OnModelChanged;
        Update();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); _attached = true;
        if (Model != null) Model.PropertyChanged += OnModelChanged;
        Update();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        if (Model != null) Model.PropertyChanged -= OnModelChanged;
        base.OnDetachedFromVisualTree(e);
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName is nameof(ChartModel.Request) or nameof(ChartModel.Snapshot) or nameof(ChartModel.DataSource)) Update(); }
    private void Update()
    {
        _updating = true;
        try
        {
            if (Model != null && Model.TryGetVisibleWindow(out int total, out int start, out int count))
            {
                Maximum = Math.Max(0, total - count);
                Position = start;
                CanNavigate = Maximum > 0;
                Summary = $"{start + 1:N0}–{start + count:N0} of {total:N0}";
            }
            else { Maximum = Position = 0; CanNavigate = false; Summary = "No data"; }
        }
        finally { _updating = false; }
    }
}
