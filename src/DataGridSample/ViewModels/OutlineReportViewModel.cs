using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls.DataGridPivoting;
using Avalonia.Controls.DataGridReporting;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public sealed class OutlineReportViewModel : ObservableObject
    {
        private readonly IList<SalesRecord> _filteredSource;
        private bool _showFilteredData;
        private bool _showSubtotals;
        private bool _showGrandTotal;
        private bool _showDetailRows;
        private bool _autoExpandGroups;

        public OutlineReportViewModel()
        {
            Source = new ObservableCollection<SalesRecord>(SalesRecordSampleData.CreateSalesRecords(500));
            _filteredSource = Source;

            Report = BuildReport(Source);

            _showSubtotals = Report.Layout.ShowSubtotals;
            _showGrandTotal = Report.Layout.ShowGrandTotal;
            _showDetailRows = Report.Layout.ShowDetailRows;
            _autoExpandGroups = Report.Layout.AutoExpandGroups;

            ExpandAllCommand = new RelayCommand(_ => Report.HierarchicalModel.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Report.HierarchicalModel.CollapseAll());
        }

        public ObservableCollection<SalesRecord> Source { get; }

        public OutlineReportModel Report { get; }

        public IEnumerable<SalesRecord> DataRows => _showFilteredData ? _filteredSource : Source;

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public bool ShowSubtotals
        {
            get => _showSubtotals;
            set
            {
                if (SetProperty(ref _showSubtotals, value))
                {
                    Report.Layout.ShowSubtotals = value;
                }
            }
        }

        public bool ShowGrandTotal
        {
            get => _showGrandTotal;
            set
            {
                if (SetProperty(ref _showGrandTotal, value))
                {
                    Report.Layout.ShowGrandTotal = value;
                }
            }
        }

        public bool ShowDetailRows
        {
            get => _showDetailRows;
            set
            {
                if (SetProperty(ref _showDetailRows, value))
                {
                    Report.Layout.ShowDetailRows = value;
                }
            }
        }

        public bool AutoExpandGroups
        {
            get => _autoExpandGroups;
            set
            {
                if (SetProperty(ref _autoExpandGroups, value))
                {
                    Report.Layout.AutoExpandGroups = value;
                }
            }
        }

        public bool ShowFilteredData
        {
            get => _showFilteredData;
            set
            {
                if (SetProperty(ref _showFilteredData, value))
                {
                    OnPropertyChanged(nameof(DataRows));
                }
            }
        }

        private static OutlineReportModel BuildReport(IEnumerable<SalesRecord> source)
        {
            var report = new OutlineReportModel
            {
                ItemsSource = source,
                Culture = CultureInfo.CurrentCulture
            };

            using (report.DeferRefresh())
            {
                report.GroupFields.Add(new OutlineGroupField
                {
                    Header = "Region",
                    ValueSelector = item => ((SalesRecord)item!).Region
                });

                report.GroupFields.Add(new OutlineGroupField
                {
                    Header = "Category",
                    ValueSelector = item => ((SalesRecord)item!).Category
                });

                report.ValueFields.Add(new OutlineValueField
                {
                    Header = "Sales",
                    ValueSelector = item => ((SalesRecord)item!).Sales,
                    AggregateType = PivotAggregateType.Sum,
                    StringFormat = "C0"
                });

                report.ValueFields.Add(new OutlineValueField
                {
                    Header = "Profit",
                    ValueSelector = item => ((SalesRecord)item!).Profit,
                    AggregateType = PivotAggregateType.Sum,
                    StringFormat = "C0"
                });

                report.ValueFields.Add(new OutlineValueField
                {
                    Header = "Units",
                    ValueSelector = item => ((SalesRecord)item!).Quantity,
                    AggregateType = PivotAggregateType.Sum,
                    StringFormat = "N0"
                });

                report.Layout.RowHeaderLabel = "Group";
                report.Layout.ShowSubtotals = true;
                report.Layout.ShowGrandTotal = true;
                report.Layout.ShowDetailRows = true;
                report.Layout.AutoExpandGroups = true;
                report.Layout.DetailLabelSelector = item => ((SalesRecord)item!).Product;
            }

            return report;
        }
    }
}
