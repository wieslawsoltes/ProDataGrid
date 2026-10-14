// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.ComponentModel;
using Avalonia.Media;

namespace ProCharts.Controls;

/// <summary>Bindable native legend entry. Visibility can be changed when the model uses SeriesVisibilityChartDataSource.</summary>
public sealed class ChartLegendItem : INotifyPropertyChanged
{
    private bool _isVisible;
    private readonly Action<bool>? _setVisibility;
    public ChartLegendItem(int index, string name, IBrush swatch, bool visible, Action<bool>? setVisibility)
    { Index = index; Name = name; Swatch = swatch; _isVisible = visible; _setVisibility = setVisibility; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public int Index { get; }
    public string Name { get; }
    public IBrush Swatch { get; }
    public bool CanToggle => _setVisibility != null;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _setVisibility?.Invoke(value);
            UpdateVisibility(value);
        }
    }
    internal void UpdateVisibility(bool value)
    {
        if (_isVisible == value) return;
        _isVisible = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
    }
}
