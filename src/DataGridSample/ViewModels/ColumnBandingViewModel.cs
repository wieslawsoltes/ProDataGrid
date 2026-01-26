using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridBanding;
using DataGridSample.Helpers;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public sealed class ColumnBandingViewModel : ObservableObject
    {
        public ColumnBandingViewModel()
        {
            Source = new ObservableCollection<SalesRecord>(SalesRecordSampleData.CreateSalesRecords(300));
            Bands = BuildBands();
        }

        public ObservableCollection<SalesRecord> Source { get; }

        public ColumnBandModel Bands { get; }

        private static ColumnBandModel BuildBands()
        {
            var model = new ColumnBandModel();

            var orderDateBinding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, DateTime>(
                nameof(SalesRecord.OrderDate),
                record => record.OrderDate);
            orderDateBinding.StringFormat = "d";
            var orderDateColumn = new DataGridTextColumnDefinition
            {
                Header = "Order Date",
                Binding = orderDateBinding,
                IsReadOnly = true
            };

            var regionColumn = new DataGridTextColumnDefinition
            {
                Header = "Region",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, string>(
                    nameof(SalesRecord.Region),
                    record => record.Region),
                IsReadOnly = true
            };

            var segmentColumn = new DataGridTextColumnDefinition
            {
                Header = "Segment",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, string>(
                    nameof(SalesRecord.Segment),
                    record => record.Segment),
                IsReadOnly = true
            };

            var categoryColumn = new DataGridTextColumnDefinition
            {
                Header = "Category",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, string>(
                    nameof(SalesRecord.Category),
                    record => record.Category),
                IsReadOnly = true
            };

            var productColumn = new DataGridTextColumnDefinition
            {
                Header = "Product",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, string>(
                    nameof(SalesRecord.Product),
                    record => record.Product),
                IsReadOnly = true
            };

            var salesColumn = new DataGridNumericColumnDefinition
            {
                Header = "Sales",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, double>(
                    nameof(SalesRecord.Sales),
                    record => record.Sales),
                FormatString = "C0",
                IsReadOnly = true
            };

            var profitColumn = new DataGridNumericColumnDefinition
            {
                Header = "Profit",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, double>(
                    nameof(SalesRecord.Profit),
                    record => record.Profit),
                FormatString = "C0",
                IsReadOnly = true
            };

            var unitsColumn = new DataGridNumericColumnDefinition
            {
                Header = "Units",
                Binding = ColumnDefinitionBindingFactory.CreateBinding<SalesRecord, int>(
                    nameof(SalesRecord.Quantity),
                    record => record.Quantity),
                FormatString = "N0",
                IsReadOnly = true
            };

            using (model.DeferRefresh())
            {
                model.Bands.Add(new ColumnBand
                {
                    Header = "Order",
                    Children =
                    {
                        new ColumnBand { Header = "Date", ColumnDefinition = orderDateColumn },
                        new ColumnBand { Header = "Region", ColumnDefinition = regionColumn },
                        new ColumnBand { Header = "Segment", ColumnDefinition = segmentColumn }
                    }
                });

                model.Bands.Add(new ColumnBand
                {
                    Header = "Merchandise",
                    Children =
                    {
                        new ColumnBand { Header = "Category", ColumnDefinition = categoryColumn },
                        new ColumnBand { Header = "Product", ColumnDefinition = productColumn }
                    }
                });

                model.Bands.Add(new ColumnBand
                {
                    Header = "Financials",
                    Children =
                    {
                        new ColumnBand
                        {
                            Header = "Revenue",
                            Children =
                            {
                                new ColumnBand { Header = "Sales", ColumnDefinition = salesColumn },
                                new ColumnBand { Header = "Profit", ColumnDefinition = profitColumn }
                            }
                        },
                        new ColumnBand
                        {
                            Header = "Volume",
                            Children =
                            {
                                new ColumnBand { Header = "Units", ColumnDefinition = unitsColumn }
                            }
                        }
                    }
                });
            }

            return model;
        }
    }
}
