using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class BreakpointsPageViewModel : ViewModelBase, IDisposable
{
    private readonly BreakpointService _breakpointService;
    private readonly AvaloniaList<BreakpointEntry> _displayEntries = new();
    private readonly DataGridCollectionView _breakpointsView;
    private BreakpointEntry? _selectedBreakpoint;
    private IRemoteReadOnlyDiagnosticsDomainService? _remoteReadOnly;
    private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
    private bool _isApplyingRemoteBreakpointState;
    private bool _isRemoteRefreshScheduled;

    public BreakpointsPageViewModel(BreakpointService breakpointService)
    {
        _breakpointService = breakpointService ?? throw new ArgumentNullException(nameof(breakpointService));

        BreakpointsFilter = new FilterViewModel();
        BreakpointsFilter.RefreshFilter += (_, _) => Refresh();

        _breakpointService.Entries.CollectionChanged += OnBreakpointsCollectionChanged;
        _breakpointsView = new DataGridCollectionView(_displayEntries)
        {
            Filter = FilterBreakpoint
        };

        ReplaceDisplayEntries(_breakpointService.Entries);
    }

    public FilterViewModel BreakpointsFilter { get; }

    public DataGridCollectionView BreakpointsView => _breakpointsView;

    public int BreakpointCount => _displayEntries.Count;

    public int VisibleBreakpointCount => _breakpointsView.Count;

    public BreakpointEntry? SelectedBreakpoint
    {
        get => _selectedBreakpoint;
        set => RaiseAndSetIfChanged(ref _selectedBreakpoint, value);
    }

    internal void SetRemoteReadOnlySource(IRemoteReadOnlyDiagnosticsDomainService? readOnly, bool refreshNow = true)
    {
        _remoteReadOnly = readOnly;
        if (readOnly is null)
        {
            ReplaceDisplayEntries(_breakpointService.Entries);
            Refresh();
            return;
        }

        if (refreshNow)
        {
            QueueRemoteRefresh();
        }
    }

    public void RemoveSelected()
    {
        if (SelectedBreakpoint is not { } selected)
        {
            return;
        }

        if (_remoteReadOnly is not null)
        {
            if (_remoteMutation is null)
            {
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.RemoveBreakpointAsync(
                    new RemoteRemoveBreakpointRequest
                    {
                        BreakpointId = selected.Id,
                    }),
                fallback: QueueRemoteRefresh,
                onSuccess: QueueRemoteRefresh);
            return;
        }

        if (_remoteMutation is null)
        {
            _breakpointService.Remove(selected);
            return;
        }

        _ = InvokeRemoteMutationAsync(
            mutation => mutation.RemoveBreakpointAsync(
                new RemoteRemoveBreakpointRequest
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
        if (_remoteReadOnly is not null)
        {
            if (_remoteMutation is null)
            {
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.ClearBreakpointsAsync(),
                fallback: QueueRemoteRefresh,
                onSuccess: QueueRemoteRefresh);
            SelectedBreakpoint = null;
            return;
        }

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
        if (_remoteReadOnly is not null)
        {
            if (_remoteMutation is null)
            {
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.SetBreakpointsEnabledAsync(new RemoteSetBreakpointsEnabledRequest { IsEnabled = true }),
                fallback: QueueRemoteRefresh,
                onSuccess: QueueRemoteRefresh);
            return;
        }

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
        if (_remoteReadOnly is not null)
        {
            if (_remoteMutation is null)
            {
                return;
            }

            _ = InvokeRemoteMutationAsync(
                mutation => mutation.SetBreakpointsEnabledAsync(new RemoteSetBreakpointsEnabledRequest { IsEnabled = false }),
                fallback: QueueRemoteRefresh,
                onSuccess: QueueRemoteRefresh);
            return;
        }

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
        UnsubscribeDisplayEntries(_displayEntries);
    }

    public void Refresh()
    {
        if (_remoteReadOnly is not null)
        {
            QueueRemoteRefresh();
            return;
        }

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
        if (_remoteReadOnly is not null)
        {
            return;
        }

        ReplaceDisplayEntries(_breakpointService.Entries);
        Refresh();
    }

    private void OnBreakpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingRemoteBreakpointState || sender is not BreakpointEntry entry)
        {
            return;
        }

        if (_remoteReadOnly is not null)
        {
            if (e.PropertyName == nameof(BreakpointEntry.IsEnabled) && _remoteMutation is not null)
            {
                _ = InvokeRemoteMutationAsync(
                    mutation => mutation.ToggleBreakpointAsync(
                        new RemoteToggleBreakpointRequest
                        {
                            BreakpointId = entry.Id,
                            IsEnabled = entry.IsEnabled,
                        }),
                    fallback: QueueRemoteRefresh,
                    onSuccess: null);
                return;
            }

            QueueRemoteRefresh();
            return;
        }

        if (_remoteMutation is null || e.PropertyName != nameof(BreakpointEntry.IsEnabled))
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
        Action fallback,
        Action? onSuccess = null)
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
            onSuccess?.Invoke();
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
        foreach (var entry in _displayEntries)
        {
            entry.IsEnabled = true;
        }
    }

    private void SetAllEnabledLocalFalse()
    {
        foreach (var entry in _displayEntries)
        {
            entry.IsEnabled = false;
        }
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

    private void QueueRemoteRefresh()
    {
        if (_remoteReadOnly is null || _isRemoteRefreshScheduled)
        {
            return;
        }

        _isRemoteRefreshScheduled = true;
        _ = RefreshRemoteSnapshotAsync();
    }

    private async Task RefreshRemoteSnapshotAsync()
    {
        var readOnly = _remoteReadOnly;
        if (readOnly is null)
        {
            _isRemoteRefreshScheduled = false;
            return;
        }

        try
        {
            var snapshot = await readOnly.GetBreakpointsSnapshotAsync(
                new RemoteBreakpointsSnapshotRequest
                {
                    Scope = "combined",
                }).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_remoteReadOnly, readOnly))
                {
                    return;
                }

                ApplyRemoteSnapshot(snapshot);
            });
        }
        catch
        {
            // Preserve the last applied remote snapshot when refresh fails.
        }
        finally
        {
            _isRemoteRefreshScheduled = false;
        }
    }

    private void ApplyRemoteSnapshot(RemoteBreakpointsSnapshot snapshot)
    {
        var selectedId = SelectedBreakpoint?.Id;
        var items = new List<BreakpointEntry>(snapshot.Breakpoints.Count);
        for (var i = 0; i < snapshot.Breakpoints.Count; i++)
        {
            var remoteBreakpoint = snapshot.Breakpoints[i];
            var entry = new BreakpointEntry(
                ParseBreakpointKind(remoteBreakpoint.Kind),
                remoteBreakpoint.Name,
                routedEvent: null,
                property: null,
                target: null,
                targetDescription: remoteBreakpoint.TargetDescription,
                id: remoteBreakpoint.Id)
            {
                IsEnabled = remoteBreakpoint.IsEnabled,
                HitCount = remoteBreakpoint.HitCount,
                TriggerAfterHits = remoteBreakpoint.TriggerAfterHits,
                SuspendExecution = remoteBreakpoint.SuspendExecution,
                LogMessage = remoteBreakpoint.LogMessage,
                RemoveOnceHit = remoteBreakpoint.RemoveOnceHit,
                LastHitAt = remoteBreakpoint.LastHitAt,
                LastHitDetails = remoteBreakpoint.LastHitDetails,
            };
            items.Add(entry);
        }

        ReplaceDisplayEntries(items);
        SelectedBreakpoint = selectedId is null
            ? null
            : _displayEntries.FirstOrDefault(entry => string.Equals(entry.Id, selectedId, StringComparison.Ordinal));
        RefreshViewOnly();
    }

    private void ReplaceDisplayEntries(IReadOnlyList<BreakpointEntry> entries)
    {
        UnsubscribeDisplayEntries(_displayEntries);
        _displayEntries.Clear();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            _displayEntries.Add(entry);
            entry.PropertyChanged += OnBreakpointPropertyChanged;
        }
    }

    private void UnsubscribeDisplayEntries(IEnumerable<BreakpointEntry> entries)
    {
        foreach (var entry in entries)
        {
            entry.PropertyChanged -= OnBreakpointPropertyChanged;
        }
    }

    private void RefreshViewOnly()
    {
        _breakpointsView.Refresh();
        RaisePropertyChanged(nameof(BreakpointCount));
        RaisePropertyChanged(nameof(VisibleBreakpointCount));
    }

    private static BreakpointKind ParseBreakpointKind(string? kind)
    {
        return Enum.TryParse(kind, ignoreCase: true, out BreakpointKind parsed)
            ? parsed
            : BreakpointKind.Property;
    }
}
