using System;

namespace ProDiagnostics.Viewer.ViewModels;

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
    public string SourceName { get; }
    public DateTimeOffset StartTime { get; }
    public TimeSpan Duration { get; }
    public string TagsSummary { get; }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public void ApplyAlias(string? alias)
        => DisplayName = string.IsNullOrWhiteSpace(alias) ? Name : alias;
}
