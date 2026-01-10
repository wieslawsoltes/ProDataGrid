using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Interactivity;
using DataGridSample.Models;
using DataGridExtensions;
using Avalonia.Markup.Xaml.Templates;

namespace DataGridSample.Pages
{
    public partial class LegacyFilterTestPage : UserControl
    {
        private readonly ObservableCollection<LegacyFilterItem> _items = new();
        private readonly ObservableCollection<string> _filterSummaries = new();
        private readonly FilteringModel _filteringModel = new();

        public ObservableCollection<string> FilterSummaries => _filterSummaries;

        public FilteringModel FilteringModel => _filteringModel;

        public LegacyFilterTestPage()
        {
            InitializeComponent();
            DataContext = this;

            PopulateSampleData();
            LegacyGrid.ItemsSource = _items;
            LegacyGrid.FilteringModel = _filteringModel;

            _filteringModel.FilteringChanged += FilteringModelOnFilteringChanged;
            InitializeFilterControls();
            UpdateFilterSummaries();
        }

        private void PopulateSampleData()
        {
            AddSample("Telemetry alignment", "Telemetry", 0x1A2B3C4D);
            AddSample("Startup sequence", "Diagnostics", 0x12345678);
            AddSample("Hardware fallback", "Platform", 0x0000ABCD);
            AddSample("User preferences", "Settings", 0x00FF00FF);
            AddSample("Performance capture", "Telemetry", 0xDEADBEEF);
            AddSample("Memory tracker", "Diagnostics", 0x0BADF00D);
            AddSample("Hex sampler", "Utilities", 0x00C0FFEE);
            AddSample("Legacy channel", "Compatibility", 0xCAFEBABE);
            AddSample("Network handshake", "Platform", 0x7F000001);
            AddSample("Debug pipeline", "Diagnostics", 0xFEEDFACE);
        }

        private void AddSample(string description, string category, uint token)
        {
            _items.Add(new LegacyFilterItem
            {
                Description = description,
                Category = category,
                Token = token
            });
        }

        private void InitializeFilterControls()
        {
            if (Resources["DefaultFilter"] is ControlTemplate defaultFilter)
            {
                ApplyTemplateToColumn("Description", defaultFilter);
                ApplyTemplateToColumn("Category", defaultFilter);
            }

            if (Resources["HexFilter"] is ControlTemplate hexFilter)
            {
                ApplyTemplateToColumn("Token", hexFilter);
            }
        }

        private void ApplyTemplateToColumn(string header, ControlTemplate template)
        {
            var column = LegacyGrid.Columns.FirstOrDefault(col =>
                string.Equals(col.Header?.ToString(), header, StringComparison.Ordinal));
            if (column != null)
            {
                // Attach the legacy filter template so the legacy bridge sees FilterValue/ContentFilter.
                column.SetTemplate(template);
            }
        }

        private void FilteringModelOnFilteringChanged(object? sender, FilteringChangedEventArgs e)
        {
            UpdateFilterSummaries();
        }

        private void UpdateFilterSummaries()
        {
            _filterSummaries.Clear();
            var descriptors = _filteringModel.Descriptors;
            if (descriptors?.Count > 0)
            {
                foreach (var descriptor in descriptors)
                {
                    _filterSummaries.Add(FormatDescriptor(descriptor));
                }
            }
            else
            {
                _filterSummaries.Add("No legacy filters active.");
            }
        }

        private static string FormatDescriptor(FilteringDescriptor descriptor)
        {
            var column = descriptor.ColumnId as DataGridColumn;
            var header = column?.Header?.ToString() ?? descriptor.PropertyPath ?? "Column";
            var filterText = column?.FilterValue ?? descriptor.Value?.ToString() ?? "<custom>";
            return $"{header} ({descriptor.Operator}): {filterText}";
        }

        private void OnClearFiltersClicked(object? sender, RoutedEventArgs e)
        {
            DataGridFilter.GetFilter(LegacyGrid).Clear();
        }
    }
}
