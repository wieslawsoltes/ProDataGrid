using System;
using DataGridSample.Collections;

namespace DataGridSample.Models
{
    public class HierarchicalStreamingItem
    {
        public HierarchicalStreamingItem(int id, string name, double price, DateTime updatedAt, bool isExpanded)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Price = price;
            UpdatedAt = updatedAt;
            PriceDisplay = price.ToString("F2");
            UpdatedAtDisplay = updatedAt.ToString("T");
            IsExpanded = isExpanded;
            Children = new ObservableRangeCollection<HierarchicalStreamingItem>();
        }

        public int Id { get; }

        public string Name { get; }

        public double Price { get; }

        public DateTime UpdatedAt { get; }

        public string PriceDisplay { get; }

        public string UpdatedAtDisplay { get; }

        public bool IsExpanded { get; set; }

        public ObservableRangeCollection<HierarchicalStreamingItem> Children { get; }
    }
}
