using System;

namespace DataGridSample.Models;

public sealed class OptimizedCellSampleRow
{
    public OptimizedCellSampleRow(
        string id,
        string name,
        string category,
        string owner,
        string region,
        string state,
        string detail,
        bool isActive,
        DateTime date,
        TimeSpan time,
        string phone,
        double sliderValue)
    {
        Id = id;
        Name = name;
        Category = category;
        Owner = owner;
        Region = region;
        State = state;
        Detail = detail;
        IsActive = isActive;
        Date = date;
        Time = time;
        Phone = phone;
        SliderValue = sliderValue;
    }

    public string Id { get; set; }

    public string Name { get; set; }

    public string Category { get; set; }

    public string Owner { get; set; }

    public string Region { get; set; }

    public string State { get; set; }

    public string Detail { get; set; }

    public bool IsActive { get; set; }

    public DateTime Date { get; set; }

    public TimeSpan Time { get; set; }

    public string Phone { get; set; }

    public double SliderValue { get; set; }
}
