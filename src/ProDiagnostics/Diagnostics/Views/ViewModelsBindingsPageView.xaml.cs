using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Views;

partial class ViewModelsBindingsPageView : UserControl
{
    private readonly DataGrid _viewModelsGrid;
    private readonly DataGrid _bindingsGrid;
    private DataGridRow? _hovered;
    private System.IDisposable? _adorner;
    private Visual? _adornedVisual;
    private MainViewModel? _mainView;

    public ViewModelsBindingsPageView()
    {
        InitializeComponent();
        _viewModelsGrid = this.GetControl<DataGrid>("viewModelsGrid");
        _bindingsGrid = this.GetControl<DataGrid>("bindingsGrid");
    }

    protected void UpdateAdorner(object? sender, PointerEventArgs e)
    {
        if (e.Source is not StyledElement source)
        {
            return;
        }

        var row = source.FindLogicalAncestorOfType<DataGridRow>();
        if (row == _hovered)
        {
            return;
        }

        _adorner?.Dispose();
        _adorner = null;

        if (row is null || (row.OwningGrid != _viewModelsGrid && row.OwningGrid != _bindingsGrid))
        {
            _hovered = null;
            _adornedVisual = null;
            return;
        }

        _hovered = row;
        if (DataContext is not ViewModelsBindingsPageViewModel pageViewModel)
        {
            return;
        }

        if (pageViewModel.MainView is { HighlightElements: false })
        {
            return;
        }

        var visual = ResolveVisual(row.DataContext);
        if (visual is null)
        {
            _adornedVisual = null;
            return;
        }

        _adornedVisual = visual;

        _adorner = pageViewModel.MainView is { } mainView
            ? Controls.ControlHighlightAdorner.Add(visual, mainView.OverlayDisplayOptions)
            : Controls.ControlHighlightAdorner.Add(visual, visualizeMarginPadding: false);
    }

    private void RemoveAdorner(object? sender, PointerEventArgs e)
    {
        _adorner?.Dispose();
        _adorner = null;
        _hovered = null;
        _adornedVisual = null;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != DataContextProperty)
        {
            return;
        }

        if (change.GetOldValue<object?>() is ViewModelsBindingsPageViewModel oldViewModel && oldViewModel.MainView is { } oldMainView)
        {
            oldMainView.PropertyChanged -= OnMainViewPropertyChanged;
        }

        if (change.GetNewValue<object?>() is ViewModelsBindingsPageViewModel newViewModel && newViewModel.MainView is { } newMainView)
        {
            _mainView = newMainView;
            newMainView.PropertyChanged += OnMainViewPropertyChanged;
            RefreshAdornerFromCurrentVisual();
        }
        else
        {
            _mainView = null;
            _adornedVisual = null;
            _adorner?.Dispose();
            _adorner = null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_mainView is not null)
        {
            _mainView.PropertyChanged -= OnMainViewPropertyChanged;
        }

        _adorner?.Dispose();
        _adorner = null;
        _adornedVisual = null;
        _mainView = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnMainViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.HighlightElements)
            or nameof(MainViewModel.ShouldVisualizeMarginPadding)
            or nameof(MainViewModel.ShowOverlayInfo)
            or nameof(MainViewModel.ShowOverlayRulers)
            or nameof(MainViewModel.ShowOverlayExtensionLines))
        {
            RefreshAdornerFromCurrentVisual();
        }
    }

    private void RefreshAdornerFromCurrentVisual()
    {
        _adorner?.Dispose();
        _adorner = null;

        if (_adornedVisual is null || _mainView is not { HighlightElements: true })
        {
            return;
        }

        _adorner = Controls.ControlHighlightAdorner.Add(_adornedVisual, _mainView.OverlayDisplayOptions);
    }

    private static Visual? ResolveVisual(object? dataContext)
    {
        return dataContext switch
        {
            ViewModelContextEntryViewModel viewModelEntry => viewModelEntry.SourceObject as Visual,
            BindingDiagnosticEntryViewModel bindingEntry => bindingEntry.SourceObject as Visual,
            _ => null
        };
    }
}
