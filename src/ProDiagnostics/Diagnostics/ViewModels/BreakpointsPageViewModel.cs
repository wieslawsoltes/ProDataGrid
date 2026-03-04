using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class BreakpointsPageViewModel : ViewModelBase, IDisposable
{
    private readonly BreakpointService _breakpointService;
    private readonly DataGridCollectionView _breakpointsView;
    private BreakpointEntry? _selectedBreakpoint;
    private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
    private bool _isApplyingRemoteBreakpointState;

    public BreakpointsPageViewModel(BreakpointService breakpointService)
    {
        _breakpointService = breakpointService ?? throw new ArgumentNullException(nameof(breakpointService));

        BreakpointsFilter = new FilterViewModel();
        BreakpointsFilter.RefreshFilter += (_, _) => Refresh();

        _breakpointService.Entries.CollectionChanged += OnBreakpointsCollectionChanged;
        for (var i = 0; i < _breakpointService.Entries.Count; i++)
        {
            _breakpointService.Entries[i].PropertyChanged += OnBreakpointPropertyChanged;
        }
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
        if (SelectedBreakpoint is not { } selected)
        {
            return;
        }

        if (_remoteMutation is null)
        {
            _breakpointService.Remove(selected);
            return;
        }

        _ = InvokeRemoteMutationAsync(
            mutation => mutation.RemoveBreakpointAsync(new RemoteRemoveBreakpointRequest
            {
                BreakpointId = selected.Id,
            }),
            fallback: () => _breakpointService.Remove(selected));
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
        if (_remoteMutation is null)
        {
            _breakpointService.Clear();
            SelectedBreakpoint = null;
            return;
        }

        _ = InvokeRemoteMutationAsync(
            mutation => mutation.ClearBreakpointsAsync(),
            fallback: () => _breakpointService.Clear());
        SelectedBreakpoint = null;
    }

    public void EnableAll()
    {
        if (_remoteMutation is not null)
        {
            _ = InvokeRemoteMutationAsync(
                mutation => mutation.SetBreakpointsEnabledAsync(new RemoteSetBreakpointsEnabledRequest { IsEnabled = true }),
                fallback: SetAllEnabledLocalTrue);
            return;
        }

        SetAllEnabledLocalTrue();
    }

    public void DisableAll()
    {
        if (_remoteMutation is not null)
        {
            _ = InvokeRemoteMutationAsync(
                mutation => mutation.SetBreakpointsEnabledAsync(new RemoteSetBreakpointsEnabledRequest { IsEnabled = false }),
                fallback: SetAllEnabledLocalFalse);
            return;
        }

        SetAllEnabledLocalFalse();
    }

    internal void SetRemoteMutationSource(IRemoteMutationDiagnosticsDomainService? mutation)
    {
        _remoteMutation = mutation;
    }

    internal void ApplyRemoteStateMutation(Action action)
    {
        if (action is null)
        {
            return;
        }

        var wasApplying = _isApplyingRemoteBreakpointState;
        _isApplyingRemoteBreakpointState = true;
        try
        {
            action();
        }
        finally
        {
            _isApplyingRemoteBreakpointState = wasApplying;
        }
    }

    public void Dispose()
    {
        _breakpointService.Entries.CollectionChanged -= OnBreakpointsCollectionChanged;
        for (var i = 0; i < _breakpointService.Entries.Count; i++)
        {
            _breakpointService.Entries[i].PropertyChanged -= OnBreakpointPropertyChanged;
        }
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
        if (e.OldItems is not null)
        {
            for (var i = 0; i < e.OldItems.Count; i++)
            {
                if (e.OldItems[i] is BreakpointEntry oldEntry)
                {
                    oldEntry.PropertyChanged -= OnBreakpointPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            for (var i = 0; i < e.NewItems.Count; i++)
            {
                if (e.NewItems[i] is BreakpointEntry newEntry)
                {
                    newEntry.PropertyChanged += OnBreakpointPropertyChanged;
                }
            }
        }

        Refresh();
    }

    private void OnBreakpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_remoteMutation is null ||
            _isApplyingRemoteBreakpointState ||
            sender is not BreakpointEntry entry ||
            e.PropertyName != nameof(BreakpointEntry.IsEnabled))
        {
            return;
        }

        _ = InvokeRemoteMutationAsync(
            mutation => mutation.ToggleBreakpointAsync(
                new RemoteToggleBreakpointRequest
                {
                    BreakpointId = entry.Id,
                    IsEnabled = entry.IsEnabled,
                }),
            fallback: () => { });
    }

    private async Task InvokeRemoteMutationAsync(
        Func<IRemoteMutationDiagnosticsDomainService, ValueTask<RemoteMutationResult>> action,
        Action fallback)
    {
        var mutation = _remoteMutation;
        if (mutation is null)
        {
            fallback();
            return;
        }

        try
        {
            await action(mutation).ConfigureAwait(false);
        }
        catch
        {
            _isApplyingRemoteBreakpointState = true;
            try
            {
                fallback();
            }
            finally
            {
                _isApplyingRemoteBreakpointState = false;
            }
        }
    }

    private void SetAllEnabledLocalTrue()
    {
        foreach (var entry in _breakpointService.Entries)
        {
            entry.IsEnabled = true;
        }
    }

    private void SetAllEnabledLocalFalse()
    {
        foreach (var entry in _breakpointService.Entries)
        {
            entry.IsEnabled = false;
        }
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
