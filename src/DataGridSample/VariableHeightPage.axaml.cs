using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.Models;

namespace DataGridSample
{
    public partial class VariableHeightPage : UserControl
    {
        private DataGrid? _dataGrid;
        private NumericUpDown? _itemCountUpDown;
        private NumericUpDown? _seedUpDown;
        private NumericUpDown? _scrollToIndexUpDown;
        private TextBlock? _itemCountText;
        private TextBlock? _scrollInfoText;
        private TextBlock? _visibleRangeText;
        private ObservableCollection<VariableHeightItem> _items;

        public VariableHeightPage()
        {
            _items = new ObservableCollection<VariableHeightItem>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _dataGrid = this.FindControl<DataGrid>("VariableHeightDataGrid");
            _itemCountUpDown = this.FindControl<NumericUpDown>("ItemCountUpDown");
            _seedUpDown = this.FindControl<NumericUpDown>("SeedUpDown");
            _scrollToIndexUpDown = this.FindControl<NumericUpDown>("ScrollToIndexUpDown");
            _itemCountText = this.FindControl<TextBlock>("ItemCountText");
            _scrollInfoText = this.FindControl<TextBlock>("ScrollInfoText");
            _visibleRangeText = this.FindControl<TextBlock>("VisibleRangeText");

            var regenerateButton = this.FindControl<Button>("RegenerateButton");
            var scrollToButton = this.FindControl<Button>("ScrollToButton");

            if (regenerateButton != null)
                regenerateButton.Click += OnRegenerateClick;

            if (scrollToButton != null)
                scrollToButton.Click += OnScrollToClick;

            if (_dataGrid != null)
            {
                _dataGrid.ItemsSource = _items;
                
                // Subscribe to scroll events for status updates
                _dataGrid.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "VerticalOffset" || e.Property.Name == "ViewportHeight")
                    {
                        UpdateScrollInfo();
                    }
                };

                // Also try to hook into scroll changes via the ScrollViewer
                _dataGrid.TemplateApplied += OnDataGridTemplateApplied;
            }

            // Generate initial items
            Dispatcher.UIThread.InvokeAsync(() => GenerateItems(), DispatcherPriority.Loaded);

            DataContext = this;
        }

        private void OnDataGridTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            // Try to find the internal ScrollViewer for more detailed scroll tracking
            if (_dataGrid != null)
            {
                var scrollViewer = _dataGrid.FindDescendantOfType<ScrollViewer>();
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged += (s, args) => UpdateScrollInfo();
                }
            }
        }

        private void OnRegenerateClick(object? sender, RoutedEventArgs e)
        {
            GenerateItems();
        }

        private void OnScrollToClick(object? sender, RoutedEventArgs e)
        {
            if (_dataGrid != null && _scrollToIndexUpDown?.Value != null)
            {
                int index = (int)_scrollToIndexUpDown.Value;
                if (index >= 0 && index < _items.Count)
                {
                    _dataGrid.ScrollIntoView(_items[index], null);
                    _dataGrid.SelectedIndex = index;
                }
            }
        }

        private void GenerateItems()
        {
            int count = (int)(_itemCountUpDown?.Value ?? 500);
            int seed = (int)(_seedUpDown?.Value ?? 42);

            _items.Clear();
            
            var newItems = VariableHeightItem.GenerateItems(count, seed);
            foreach (var item in newItems)
            {
                _items.Add(item);
            }

            if (_scrollToIndexUpDown != null)
            {
                _scrollToIndexUpDown.Maximum = count - 1;
            }

            UpdateItemCountText();
            UpdateScrollInfo();
        }

        private void UpdateItemCountText()
        {
            if (_itemCountText != null)
            {
                _itemCountText.Text = $"Items: {_items.Count}";
            }
        }

        private void UpdateScrollInfo()
        {
            if (_dataGrid == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var scrollViewer = _dataGrid.FindDescendantOfType<ScrollViewer>();
                    if (scrollViewer != null && _scrollInfoText != null)
                    {
                        _scrollInfoText.Text = $"Scroll: {scrollViewer.Offset.Y:F1} / {scrollViewer.Extent.Height:F1}";
                    }

                    // Try to determine visible range
                    if (_visibleRangeText != null)
                    {
                        int firstVisible = -1;
                        int lastVisible = -1;

                        // Find visible rows by checking the DataGridRowsPresenter
                        var rowsPresenter = _dataGrid.FindDescendantOfType<DataGridRowsPresenter>();
                        if (rowsPresenter != null)
                        {
                            foreach (var child in rowsPresenter.Children)
                            {
                                if (child is DataGridRow row && row.IsVisible)
                                {
                                    int index = row.Index;
                                    if (index >= 0)
                                    {
                                        if (firstVisible < 0 || index < firstVisible)
                                            firstVisible = index;
                                        if (index > lastVisible)
                                            lastVisible = index;
                                    }
                                }
                            }
                        }

                        if (firstVisible >= 0 && lastVisible >= 0)
                        {
                            _visibleRangeText.Text = $"Visible: {firstVisible} - {lastVisible} ({lastVisible - firstVisible + 1} rows)";
                        }
                        else
                        {
                            _visibleRangeText.Text = "Visible Range: N/A";
                        }
                    }
                }
                catch
                {
                    // Ignore errors during scroll info updates
                }
            });
        }
    }
}
