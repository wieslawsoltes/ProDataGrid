using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels;

public class CustomTextColumnViewModel : ObservableObject
{
    private readonly Random _random = new();
    private static readonly string[] Statuses = { "Draft", "Review", "Ready", "Shared" };

    public CustomTextColumnViewModel()
    {
        Notes = new ObservableCollection<NoteEntry>
        {
            new("Release prep", "Collect final release notes, trim copy, and double-check the feature toggle list.", "Review"),
            new("Perf deep-dive", "Summarize GC metrics from last week's profiling session and capture the before/after numbers.", "Draft"),
            new("Docs sweep", "Refresh the quickstart to mention the new filtering pipeline and link to API samples.", "Ready"),
            new("Design feedback", "Capture the open questions from the design review and assign follow-ups.", "Shared")
        };

        AddNoteCommand = new RelayCommand(_ => AddNote());
        ShuffleStatusesCommand = new RelayCommand(_ => ShuffleStatuses());
    }

    public ObservableCollection<NoteEntry> Notes { get; }

    public RelayCommand AddNoteCommand { get; }

    public RelayCommand ShuffleStatusesCommand { get; }

    private void AddNote()
    {
        Notes.Add(new NoteEntry(
            $"New entry {Notes.Count + 1}",
            "Describe what changed and why it matters.",
            "Draft"));
    }

    private void ShuffleStatuses()
    {
        foreach (var note in Notes)
        {
            var index = _random.Next(Statuses.Length);
            note.Status = Statuses[index];
        }
    }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public class NoteEntry : ObservableObject
{
    private string _title;
    private string _body;
    private string _status;

    public NoteEntry(string title, string body, string status)
    {
        _title = title;
        _body = body;
        _status = status;
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Body
    {
        get => _body;
        set => SetProperty(ref _body, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
