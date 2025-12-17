using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels;

public class ColumnThemesViewModel : ObservableObject
{
    private readonly Random _random = new();
    private static readonly string[] Statuses = { "New", "Active", "Blocked", "Done" };
    private static readonly string[] Links = { "https://example.com", "https://avaloniaui.net", "https://github.com" };

    public ColumnThemesViewModel()
    {
        Rows = new ObservableCollection<ThemedRow>
        {
            new(true, "Active", "https://example.com", "Docs", "Custom column styles reuse grid cell themes."),
            new(false, "Blocked", "https://github.com", "Issues", "Click the hyperlink column to navigate."),
            new(true, "Done", "https://avaloniaui.net", "Avalonia", "Toggle the checkbox to mark completion."),
            new(false, "New", "https://example.com", "Overview", "Choose a status from the combo box.")
        };

        ShuffleStatusesCommand = new RelayCommand(_ => ShuffleStatuses());
        ShuffleLinksCommand = new RelayCommand(_ => ShuffleLinks());
    }

    public ObservableCollection<ThemedRow> Rows { get; }

    public IReadOnlyList<string> StatusOptions => Statuses;

    public RelayCommand ShuffleStatusesCommand { get; }

    public RelayCommand ShuffleLinksCommand { get; }

    public void ShuffleStatuses()
    {
        foreach (var row in Rows)
        {
            var index = _random.Next(Statuses.Length);
            row.Status = Statuses[index];
        }
    }

    public void ShuffleLinks()
    {
        foreach (var row in Rows)
        {
            var index = _random.Next(Links.Length);
            row.Target = Links[index];
        }
    }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public class ThemedRow : ObservableObject
{
    private bool _done;
    private string _status;
    private string _target;
    private string _label;
    private string _notes;

    public ThemedRow(bool done, string status, string target, string label, string notes)
    {
        _done = done;
        _status = status;
        _target = target;
        _label = label;
        _notes = notes;
    }

    public bool Done
    {
        get => _done;
        set => SetProperty(ref _done, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }
}
