using System;
using System.Collections.ObjectModel;
using DataGridSample.Models;

namespace DataGridSample.ViewModels
{
    public static class RowDetailsSampleDataProvider
    {
        public static ObservableCollection<LocalSampleItem> CreateSampleItems(int count = 100)
        {
            var items = new ObservableCollection<LocalSampleItem>();

            var descriptions = new[]
            {
                "Primary service",
                "Replica service",
                "Analytics worker",
                "Telemetry ingestion",
                "Streaming broker",
                "Cache layer",
                "Batch processor",
                "Edge proxy"
            };

            var priorities = new[] { "High", "Medium", "Low" };
            var owners = new[] { "Infrastructure", "Data", "Insights", "Observability", "Platform" };
            var statuses = new[] { "Running", "Syncing", "Idle", "Active", "Paused" };
            var regions = new[] { "us-east", "us-west", "eu-central", "ap-south", "sa-east" };

            for (int i = 1; i <= count; i++)
            {
                var baseIndex = i - 1;
                var details = new[]
                {
                    ("Priority", priorities[baseIndex % priorities.Length]),
                    ("Owner", owners[baseIndex % owners.Length]),
                    ("Status", statuses[baseIndex % statuses.Length]),
                    ("Region", regions[baseIndex % regions.Length]),
                    ("Last Seen", DateTime.UtcNow.AddMinutes(-baseIndex * 3).ToString("HH:mm:ss")),
                    ("Version", $"v{1 + (baseIndex % 4)}.{baseIndex % 10}")
                };

                var item = new LocalSampleItem
                {
                    Id = i,
                    Name = $"Node {i:D3}",
                    Info = descriptions[baseIndex % descriptions.Length]
                };

                foreach (var detail in details)
                {
                    item.Details.Add(new LocalSampleDetail
                    {
                        Field = detail.Item1,
                        Value = detail.Item2
                    });
                }

                items.Add(item);
            }

            return items;
        }
    }
}
