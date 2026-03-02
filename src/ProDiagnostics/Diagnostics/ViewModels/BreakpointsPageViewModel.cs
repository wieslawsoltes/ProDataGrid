using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class BreakpointsPageViewModel : ViewModelBase, IDisposable
{
    private readonly BreakpointService _breakpointService;
    private readonly DataGridCollectionView _breakpointsView;
    private BreakpointEntry? _selectedBreakpoint;

    public BreakpointsPageViewModel(BreakpointService breakpointService)
    {
        _breakpointService = breakpointService ?? throw new ArgumentNullException(nameof(breakpointService));

        BreakpointsFilter = new FilterViewModel();
        BreakpointsFilter.RefreshFilter += (_, _) => Refresh();

        _breakpointService.Entries.CollectionChanged += OnBreakpointsCollectionChanged;
        _breakpointsView = new DataGridCollectionView(_breakpointService.Entries)
        {
            Filter = FilterBreakpoint
        };
    }

    public FilterViewModel BreakpointsFilter { get; }

    public DataGridCollectionView BreakpointsView => _breakpointsView;

    public int BreakpointCount => _breakpointService.Entries.Count;

    public int VisibleBreakpointCount => _breakpointsView.Count;

    public BreakpointEntry? SelectedBreakpoint
    {
        get => _selectedBreakpoint;
        set => RaiseAndSetIfChanged(ref _selectedBreakpoint, value);
    }

    public void RemoveSelected()
    {
        _breakpointService.Remove(SelectedBreakpoint);
    }

    public bool RemoveSelectedRecord()
    {
        if (SelectedBreakpoint is null)
        {
            return false;
        }

        RemoveSelected();
        SelectedBreakpoint = null;
        Refresh();
        return true;
    }

    public void ClearAll()
    {
        _breakpointService.Clear();
        SelectedBreakpoint = null;
    }

    public void EnableAll()
    {
        foreach (var entry in _breakpointService.Entries)
        {
            entry.IsEnabled = true;
        }
    }

    public void DisableAll()
    {
        foreach (var entry in _breakpointService.Entries)
        {
            entry.IsEnabled = false;
        }
    }

    public void Dispose()
    {
        _breakpointService.Entries.CollectionChanged -= OnBreakpointsCollectionChanged;
    }

    private bool FilterBreakpoint(object item)
    {
        if (item is not BreakpointEntry entry)
        {
            return true;
        }

        return BreakpointsFilter.Filter(entry.Name) ||
               BreakpointsFilter.Filter(entry.TargetDescription) ||
               BreakpointsFilter.Filter(entry.LastHitDetails) ||
               BreakpointsFilter.Filter(entry.Kind.ToString());
    }

    private void OnBreakpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Refresh();
    }

    public void Refresh()
    {
        _breakpointsView.Refresh();
        RaisePropertyChanged(nameof(BreakpointCount));
        RaisePropertyChanged(nameof(VisibleBreakpointCount));
    }

    public bool SelectNextMatch()
    {
        return NavigateSelection(forward: true);
    }

    public bool SelectPreviousMatch()
    {
        return NavigateSelection(forward: false);
    }

    public bool ClearSelectionOrFilter()
    {
        if (SelectedBreakpoint is not null)
        {
            SelectedBreakpoint = null;
            return true;
        }

        if (!string.IsNullOrEmpty(BreakpointsFilter.FilterString))
        {
            BreakpointsFilter.FilterString = string.Empty;
            return true;
        }

        return false;
    }

    private bool NavigateSelection(bool forward)
    {
        var visible = _breakpointsView.Cast<BreakpointEntry>().ToArray();
        if (visible.Length == 0)
        {
            SelectedBreakpoint = null;
            return false;
        }

        var currentIndex = Array.IndexOf(visible, SelectedBreakpoint);
        int nextIndex;
        if (currentIndex < 0)
        {
            nextIndex = forward ? 0 : visible.Length - 1;
        }
        else if (forward)
        {
            nextIndex = (currentIndex + 1) % visible.Length;
        }
        else
        {
            nextIndex = currentIndex == 0 ? visible.Length - 1 : currentIndex - 1;
        }

        SelectedBreakpoint = visible[nextIndex];
        return true;
    }
}
