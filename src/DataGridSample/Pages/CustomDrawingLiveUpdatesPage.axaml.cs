using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using DataGridSample.CustomDrawing;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages;

public partial class CustomDrawingLiveUpdatesPage : UserControl
{
    private CustomDrawingLiveUpdatesViewModel? _viewModel;
    private SkiaAnimatedTextCellDrawOperationFactory? _factory;
    private bool _isAttached;

    public CustomDrawingLiveUpdatesPage()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        DataContextChanged += OnDataContextChanged;
        HookViewModel(DataContext as CustomDrawingLiveUpdatesViewModel);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        if (this.TryFindResource("LiveSkiaFactory", out var resource) &&
            resource is SkiaAnimatedTextCellDrawOperationFactory factory)
        {
            _factory = factory;
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isAttached = true;
        DataContext ??= new CustomDrawingLiveUpdatesViewModel();
        HookViewModel(DataContext as CustomDrawingLiveUpdatesViewModel);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        _viewModel?.OnDetached();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookViewModel(DataContext as CustomDrawingLiveUpdatesViewModel);
    }

    private void HookViewModel(CustomDrawingLiveUpdatesViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        _viewModel?.OnDetached();
        _viewModel = viewModel;

        if (_viewModel is null)
        {
            return;
        }

        _viewModel.AttachFactory(_factory);

        if (_isAttached)
        {
            _viewModel.OnAttached();
        }
    }
}
