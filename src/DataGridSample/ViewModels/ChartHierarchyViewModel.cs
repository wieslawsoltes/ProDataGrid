using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DataGridSample.Mvvm;
using ProCharts;
using ProCharts.Skia;

namespace DataGridSample.ViewModels;

public sealed class ChartHierarchyViewModel : INotifyPropertyChanged, IDisposable
{
    private bool _disposed;
    private ChartHierarchyNode? _selectedBranch;
    public ChartHierarchyViewModel()
    {
        Source = new HierarchyChartDataSource(new ChartHierarchyNode("portfolio", "Portfolio", new[]
        {
            new ChartHierarchyNode("platform", "Platform", new[]
            {
                new ChartHierarchyNode("compute", "Compute", new[]
                {
                    new ChartHierarchyNode("cpu", "CPU", 23), new ChartHierarchyNode("gpu", "GPU", 34)
                }),
                new ChartHierarchyNode("storage", "Storage", 21), new ChartHierarchyNode("network", "Network", 15)
            }),
            new ChartHierarchyNode("products", "Products", new[]
            {
                new ChartHierarchyNode("analytics", "Analytics", 27), new ChartHierarchyNode("automation", "Automation", 19)
            }),
            new ChartHierarchyNode("support", "Support", 16)
        }));
        Chart = new ChartModel { DataSource = Source };
        Chart.CategoryAxis.IsVisible = false;
        Chart.ValueAxis.IsVisible = false;
        Chart.Legend.IsVisible = true;
        UpCommand = new RelayCommand(_ => Source.TryDrillUp(), _ => !_disposed && Source.CanDrillUp);
        ResetCommand = new RelayCommand(_ => Source.ResetNavigation(), _ => !_disposed && Source.CanDrillUp);
        EnterCommand = new RelayCommand(_ => Enter(), _ => !_disposed && SelectedBranch != null);
        Source.DataInvalidated += OnChanged;
        UpdateState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public HierarchyChartDataSource Source { get; }
    public ChartModel Chart { get; }
    public SkiaChartStyle Style { get; } = new()
    {
        ShowCategoryLabels = true, ShowDataLabels = true, LabelSize = 14,
        Advanced = new SkiaAdvancedChartStyle { TreemapGap = 4, TreemapHeaderHeight = 30 }
    };
    public IReadOnlyList<ChartSeriesKind> Kinds { get; } = new[] { ChartSeriesKind.Treemap, ChartSeriesKind.Sunburst };
    public ChartSeriesKind Kind
    {
        get => Source.Kind;
        set { if (!_disposed) Source.Kind = value; }
    }
    public string Breadcrumb { get; private set; } = string.Empty;
    public IReadOnlyList<ChartHierarchyNode> Branches { get; private set; } = Array.Empty<ChartHierarchyNode>();
    public ChartHierarchyNode? SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            if (ReferenceEquals(_selectedBranch, value)) return;
            _selectedBranch = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBranch)));
            EnterCommand.RaiseCanExecuteChanged();
        }
    }
    public RelayCommand UpCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand EnterCommand { get; }

    private void Enter()
    {
        if (!_disposed && SelectedBranch is { } branch) Source.TryDrillDown(branch.Id);
    }
    private void OnChanged(object? sender, EventArgs e) => UpdateState();
    private void UpdateState()
    {
        Breadcrumb = string.Join(" / ", Source.GetPath().Select(node => node.Label ?? node.Id));
        Branches = Source.CurrentRoot.Children.Where(node => !node.IsLeaf && node.TotalValue > 0).ToArray();
        SelectedBranch = Branches.FirstOrDefault();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Breadcrumb)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Branches)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Kind)));
        UpCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Source.DataInvalidated -= OnChanged;
        Chart.Dispose();
        UpCommand.RaiseCanExecuteChanged(); ResetCommand.RaiseCanExecuteChanged(); EnterCommand.RaiseCanExecuteChanged();
    }
}
