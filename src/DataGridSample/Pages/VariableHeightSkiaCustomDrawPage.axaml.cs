using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.ViewModels;

namespace DataGridSample
{
    public partial class VariableHeightSkiaCustomDrawPage : UserControl
    {
        private DataGrid? _dataGrid;
        private ScrollViewer? _scrollViewer;
        private VariableHeightViewModel? _viewModel;
        private VariableHeightViewModel? _initializedViewModel;

        public VariableHeightSkiaCustomDrawPage()
        {
            InitializeComponent();
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _dataGrid = this.FindControl<DataGrid>("VariableHeightSkiaDataGrid");
            Button? scrollToButton = this.FindControl<Button>("ScrollToButton");

            if (scrollToButton != null)
            {
                scrollToButton.Click += OnScrollToClick;
            }

            if (_dataGrid != null)
            {
                _dataGrid.PropertyChanged += OnDataGridPropertyChanged;
                _dataGrid.TemplateApplied += OnDataGridTemplateApplied;
            }

            DataContextChanged += OnDataContextChanged;
            HookViewModel(DataContext as VariableHeightViewModel);
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            DataContext ??= new VariableHeightViewModel();
            EnsureItemsInitialized();
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            HookViewModel(DataContext as VariableHeightViewModel);
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollViewerScrollChanged;
                _scrollViewer = null;
            }
        }

        private void HookViewModel(VariableHeightViewModel? viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.ItemsRegenerated -= OnItemsRegenerated;
            }

            _viewModel = viewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.ItemsRegenerated += OnItemsRegenerated;
                ApplyEstimatorFromSelection(_viewModel.SelectedEstimator);
                EnsureItemsInitialized();
            }
        }

        private void EnsureItemsInitialized()
        {
            if (_viewModel == null || ReferenceEquals(_initializedViewModel, _viewModel))
            {
                return;
            }

            VariableHeightViewModel viewModel = _viewModel;
            _initializedViewModel = viewModel;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (viewModel.Items.Count == 0)
                {
                    viewModel.GenerateItems();
                }
            }, DispatcherPriority.Loaded);
        }

        private void OnDataGridTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollViewerScrollChanged;
                _scrollViewer = null;
            }

            if (_dataGrid != null)
            {
                _scrollViewer = _dataGrid.FindDescendantOfType<ScrollViewer>();
                if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
                }
            }
        }

        private void OnScrollViewerScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateScrollInfo();
        }

        private void OnScrollToClick(object? sender, RoutedEventArgs e)
        {
            if (_dataGrid != null && _viewModel != null)
            {
                int index = _viewModel.ScrollToIndex;
                if (index >= 0 && index < _viewModel.Items.Count)
                {
                    _dataGrid.ScrollIntoView(_viewModel.Items[index], null);
                    _dataGrid.SelectedIndex = index;
                }
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VariableHeightViewModel.SelectedEstimator))
            {
                ApplyEstimatorFromSelection(_viewModel?.SelectedEstimator);
            }
        }

        private void ApplyEstimatorFromSelection(string? name)
        {
            if (_dataGrid == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _dataGrid.RowHeightEstimator = name switch
            {
                "Caching" => new CachingRowHeightEstimator(),
                "Default" => new DefaultRowHeightEstimator(),
                _ => new AdvancedRowHeightEstimator(),
            };
        }

        private void OnDataGridPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "VerticalOffset" || e.Property.Name == "ViewportHeight")
            {
                UpdateScrollInfo();
            }
        }

        private void OnItemsRegenerated()
        {
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.ScrollToIndex = Math.Clamp(_viewModel.ScrollToIndex, 0, Math.Max(_viewModel.Items.Count - 1, 0));
            UpdateScrollInfo();
        }

        private void UpdateScrollInfo()
        {
            if (_dataGrid == null || _viewModel == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    ScrollViewer? scrollViewer = _dataGrid.FindDescendantOfType<ScrollViewer>();
                    if (scrollViewer != null)
                    {
                        _viewModel.ScrollInfoText = $"Scroll: {scrollViewer.Offset.Y:F1} / {scrollViewer.Extent.Height:F1}";
                    }

                    int firstVisible = -1;
                    int lastVisible = -1;

                    DataGridRowsPresenter? rowsPresenter = _dataGrid.FindDescendantOfType<DataGridRowsPresenter>();
                    if (rowsPresenter != null)
                    {
                        foreach (Control child in rowsPresenter.Children)
                        {
                            if (child is DataGridRow row && row.IsVisible)
                            {
                                int index = row.Index;
                                if (index >= 0)
                                {
                                    if (firstVisible < 0 || index < firstVisible)
                                    {
                                        firstVisible = index;
                                    }

                                    if (index > lastVisible)
                                    {
                                        lastVisible = index;
                                    }
                                }
                            }
                        }
                    }

                    if (firstVisible >= 0 && lastVisible >= 0)
                    {
                        _viewModel.VisibleRangeText = $"Visible: {firstVisible} - {lastVisible} ({lastVisible - firstVisible + 1} rows)";
                    }
                    else
                    {
                        _viewModel.VisibleRangeText = "Visible Range: N/A";
                    }
                }
                catch
                {
                    // Ignore errors during scroll info updates.
                }
            });
        }
    }
}
