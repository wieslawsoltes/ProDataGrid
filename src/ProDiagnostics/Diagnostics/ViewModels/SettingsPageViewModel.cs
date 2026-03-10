using System;
using System.ComponentModel;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class SettingsPageViewModel : ViewModelBase, IDisposable
{
    private readonly MainViewModel _mainViewModel;

    public SettingsPageViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
    }

    public bool VisualizeMarginPaddingOverlay
    {
        get => _mainViewModel.ShouldVisualizeMarginPadding;
        set
        {
            if (_mainViewModel.ShouldVisualizeMarginPadding != value)
            {
                _mainViewModel.ShouldVisualizeMarginPadding = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool ShowOverlayInfo
    {
        get => _mainViewModel.ShowOverlayInfo;
        set
        {
            if (_mainViewModel.ShowOverlayInfo != value)
            {
                _mainViewModel.ShowOverlayInfo = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool ShowOverlayRulers
    {
        get => _mainViewModel.ShowOverlayRulers;
        set
        {
            if (_mainViewModel.ShowOverlayRulers != value)
            {
                _mainViewModel.ShowOverlayRulers = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool ShowOverlayExtensionLines
    {
        get => _mainViewModel.ShowOverlayExtensionLines;
        set
        {
            if (_mainViewModel.ShowOverlayExtensionLines != value)
            {
                _mainViewModel.ShowOverlayExtensionLines = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool HighlightElements
    {
        get => _mainViewModel.HighlightElements;
        set
        {
            if (_mainViewModel.HighlightElements != value)
            {
                _mainViewModel.HighlightElements = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool LiveHoverOverlay
    {
        get => _mainViewModel.LiveHoverOverlay;
        set
        {
            if (_mainViewModel.LiveHoverOverlay != value)
            {
                _mainViewModel.LiveHoverOverlay = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool TrackFocusedControl
    {
        get => _mainViewModel.TrackFocusedControl;
        set
        {
            if (_mainViewModel.TrackFocusedControl != value)
            {
                _mainViewModel.TrackFocusedControl = value;
                RaisePropertyChanged();
            }
        }
    }

    public bool ShowImplementedInterfaces
    {
        get => _mainViewModel.ShowImplementedInterfaces;
        set => _mainViewModel.SetShowImplementedInterfaces(value);
    }

    public void ResetOverlayDefaults()
    {
        _mainViewModel.ApplyOverlayDefaults();
        RaiseAllProperties();
    }

    public void Dispose()
    {
        _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ShouldVisualizeMarginPadding):
                RaisePropertyChanged(nameof(VisualizeMarginPaddingOverlay));
                break;
            case nameof(MainViewModel.ShowOverlayInfo):
                RaisePropertyChanged(nameof(ShowOverlayInfo));
                break;
            case nameof(MainViewModel.ShowOverlayRulers):
                RaisePropertyChanged(nameof(ShowOverlayRulers));
                break;
            case nameof(MainViewModel.ShowOverlayExtensionLines):
                RaisePropertyChanged(nameof(ShowOverlayExtensionLines));
                break;
            case nameof(MainViewModel.HighlightElements):
                RaisePropertyChanged(nameof(HighlightElements));
                break;
            case nameof(MainViewModel.LiveHoverOverlay):
                RaisePropertyChanged(nameof(LiveHoverOverlay));
                break;
            case nameof(MainViewModel.TrackFocusedControl):
                RaisePropertyChanged(nameof(TrackFocusedControl));
                break;
            case nameof(MainViewModel.ShowImplementedInterfaces):
                RaisePropertyChanged(nameof(ShowImplementedInterfaces));
                break;
        }
    }

    private void RaiseAllProperties()
    {
        RaisePropertyChanged(nameof(VisualizeMarginPaddingOverlay));
        RaisePropertyChanged(nameof(ShowOverlayInfo));
        RaisePropertyChanged(nameof(ShowOverlayRulers));
        RaisePropertyChanged(nameof(ShowOverlayExtensionLines));
        RaisePropertyChanged(nameof(HighlightElements));
        RaisePropertyChanged(nameof(LiveHoverOverlay));
        RaisePropertyChanged(nameof(TrackFocusedControl));
        RaisePropertyChanged(nameof(ShowImplementedInterfaces));
    }
}
