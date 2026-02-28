using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels;

public sealed class CustomDrawingEditingViewModel : ObservableObject
{
    public CustomDrawingEditingViewModel()
    {
        Rows = new ObservableCollection<CustomDrawingEditingRow>();
        AddRowCommand = new RelayCommand(_ => AddRow());
        ResetRowsCommand = new RelayCommand(_ => ResetRows());
        ResetRows();
    }

    public ObservableCollection<CustomDrawingEditingRow> Rows { get; }

    public RelayCommand AddRowCommand { get; }

    public RelayCommand ResetRowsCommand { get; }

    private void AddRow()
    {
        int nextId = Rows.Count + 1;
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = nextId,
            Title = $"Task {nextId}",
            Notes = "New editable row created from command.",
            Category = "Draft"
        });
    }

    private void ResetRows()
    {
        Rows.Clear();
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 1,
            Title = "Release validation",
            Notes = "Verify custom-drawing text editing and commit behavior.",
            Category = "QA"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 2,
            Title = "Performance notes",
            Notes = "Track scroll smoothness while editing frequently updated cells.",
            Category = "Perf"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 3,
            Title = "Docs update",
            Notes = "Capture usage guidance for editable custom drawing columns.",
            Category = "Docs"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 4,
            Title = "Regression sweep",
            Notes = "Switch tabs and re-select cells to validate consistent foreground updates.",
            Category = "Stability"
        });
    }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class CustomDrawingEditingRow : ObservableObject, IDataGridCellDrawOperationItemCache
{
    private SlotEntry[]? _entries;
    private int _id;
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private string _category = string.Empty;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value)
    {
        if (_entries is not null && cacheSlot >= 0 && cacheSlot < _entries.Length)
        {
            SlotEntry entry = _entries[cacheSlot];
            if (entry.HasValue && entry.CacheKey == cacheKey && entry.Value is not null)
            {
                value = entry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value)
    {
        if (cacheSlot < 0)
        {
            return;
        }

        EnsureCapacity(cacheSlot + 1)[cacheSlot] = new SlotEntry
        {
            HasValue = true,
            CacheKey = cacheKey,
            Value = value
        };
    }

    private SlotEntry[] EnsureCapacity(int minLength)
    {
        if (_entries is null)
        {
            _entries = new SlotEntry[minLength];
            return _entries;
        }

        if (_entries.Length >= minLength)
        {
            return _entries;
        }

        int newLength = _entries.Length;
        while (newLength < minLength)
        {
            newLength *= 2;
        }

        var expanded = new SlotEntry[newLength];
        _entries.CopyTo(expanded, 0);
        _entries = expanded;
        return _entries;
    }

    private struct SlotEntry
    {
        public bool HasValue;
        public int CacheKey;
        public object? Value;
    }
}
