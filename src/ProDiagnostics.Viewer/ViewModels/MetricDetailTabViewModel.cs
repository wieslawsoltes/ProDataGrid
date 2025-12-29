using System;

namespace ProDiagnostics.Viewer.ViewModels;

public sealed class MetricDetailTabViewModel : ObservableObject
{
    private readonly Action<MetricDetailTabViewModel> _closeAction;

    public MetricDetailTabViewModel(MetricSeriesViewModel series, Action<MetricDetailTabViewModel> closeAction)
    {
        Series = series;
        _closeAction = closeAction;
        CloseCommand = new RelayCommand(() => _closeAction(this));
    }

    public MetricSeriesViewModel Series { get; }

    public RelayCommand CloseCommand { get; }
}
