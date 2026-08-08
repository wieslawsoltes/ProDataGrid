using System;
using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace ProDiagnostics.Viewer.ViewModels;

[GenerateDataGridColumns(
    ProviderName = "ActivityEventGridSchema",
    SchemaId = "prodiagnostics/viewer/activity/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true,
    Streaming = true,
    PerformanceProfile = DataGridGeneratedPerformanceProfile.HighFrequencyStreaming)]
public sealed class ActivityEventViewModel : ObservableObject
{
    private string _displayName;

    public ActivityEventViewModel(string name, string sourceName, DateTimeOffset startTime, TimeSpan duration, string tagsSummary)
    {
        Name = name;
        _displayName = name;
        SourceName = sourceName;
        StartTime = startTime;
        Duration = duration;
        TagsSummary = tagsSummary;
    }

    public string Name { get; }
    [DataGridColumn(DataGridColumnKind.Text, Header = "Source", ColumnKey = "source", Order = 3, Width = "*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string SourceName { get; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Started", ColumnKey = "started", Order = 0, Width = "140", FormatString = "{}{0:HH:mm:ss.fff}", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public DateTimeOffset StartTime { get; }

    public TimeSpan Duration { get; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Duration (ms)", ColumnKey = "duration", Order = 2, Width = "120", FormatString = "0.###", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public double DurationMilliseconds => Duration.TotalMilliseconds;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Tags", ColumnKey = "tags", Order = 4, Width = "2*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string TagsSummary { get; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Activity", ColumnKey = "activity", Order = 1, Width = "2*", IsReadOnly = true, CanUserSort = true, CanUserHide = true)]
    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public void ApplyAlias(string? alias)
        => DisplayName = string.IsNullOrWhiteSpace(alias) ? Name : alias;
}
