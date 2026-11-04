using System;
using System.Collections.Generic;

namespace DataGridSample.Models;

public sealed class OptimizedHierarchyCellSampleNode
{
    public OptimizedHierarchyCellSampleNode(
        int id,
        string name,
        string kind,
        string owner,
        string region,
        string state,
        string detail,
        IReadOnlyList<OptimizedHierarchyCellSampleNode> children,
        bool isExpanded)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Owner = owner;
        Region = region;
        State = state;
        Detail = detail;
        Children = children;
        IsExpanded = isExpanded;
        Date = new DateTime(2020, 1, 1).AddDays(id % 3_650);
        Time = TimeSpan.FromSeconds(id % 86_400);
        Phone = $"(555) {id % 1_000:D3}-{id % 10_000:D4}";
        Category = $"Category-{id % 997:D3}";
        SliderValue = (id % 1_000) / 10d;
        IsActive = id % 2 == 0;
    }

    public int Id { get; }

    public string Name { get; set; }

    public string Kind { get; set; }

    public string Owner { get; set; }

    public string Region { get; set; }

    public string State { get; set; }

    public string Detail { get; set; }

    public bool HasChildren => Children.Count != 0;

    public bool IsActive { get; set; }

    public DateTime Date { get; set; }

    public TimeSpan Time { get; set; }

    public string Phone { get; set; }

    public string Category { get; set; }

    public double SliderValue { get; set; }

    public IReadOnlyList<OptimizedHierarchyCellSampleNode> Children { get; }

    public bool IsExpanded { get; set; }
}
