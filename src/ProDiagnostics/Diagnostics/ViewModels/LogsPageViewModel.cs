using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Logging;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class LogsPageViewModel : ViewModelBase, IDisposable
{
    private readonly AvaloniaList<LogEntryViewModel> _entries = new();
    private readonly DataGridCollectionView _entriesView;
    private IDevToolsLogCollector _logCollector;
    private IDisposable _subscription;
    private int _maxEntries = 1000;
    private bool _showVerbose;
    private bool _showDebug;
    private bool _showInformation = true;
    private bool _showWarning = true;
    private bool _showError = true;
    private bool _showFatal = true;
    private LogEntryViewModel? _selectedEntry;
    private string _collectorName;
    private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
    private bool _isApplyingRemoteSettings;

    public LogsPageViewModel()
        : this(InProcessDevToolsLogCollector.Instance)
    {
    }

    internal LogsPageViewModel(IDevToolsLogCollector logCollector)
    {
        LogsFilter = new FilterViewModel();
        LogsFilter.RefreshFilter += (_, _) => RefreshEntries();

        _entriesView = new DataGridCollectionView(_entries)
        {
            Filter = FilterEntry
        };
        _entriesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(LogEntryViewModel.Timestamp),
            ListSortDirection.Descending));

        _logCollector = logCollector ?? throw new ArgumentNullException(nameof(logCollector));
        _collectorName = _logCollector.CollectorName;
        _subscription = _logCollector.Subscribe(OnLogCaptured);
    }

    public DataGridCollectionView EntriesView => _entriesView;

    public FilterViewModel LogsFilter { get; }

    public int EntryCount => _entries.Count;

    public int VisibleEntryCount => _entriesView.Count;

    public string CollectorName
    {
        get => _collectorName;
        private set
        {
            if (RaiseAndSetIfChanged(ref _collectorName, value))
            {
                RaisePropertyChanged(nameof(CollectorSummary));
            }
        }
    }

    public string CollectorSummary => "Collector: " + CollectorName;

    public LogEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set => RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    public int MaxEntries
    {
        get => _maxEntries;
        set
        {
            var clamped = value > 0 ? value : 1;
            if (RaiseAndSetIfChanged(ref _maxEntries, clamped))
            {
                TrimToMaxEntries();
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public bool ShowVerbose
    {
        get => _showVerbose;
        set
        {
            if (RaiseAndSetIfChanged(ref _showVerbose, value))
            {
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public bool ShowDebug
    {
        get => _showDebug;
        set
        {
            if (RaiseAndSetIfChanged(ref _showDebug, value))
            {
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public bool ShowInformation
    {
        get => _showInformation;
        set
        {
            if (RaiseAndSetIfChanged(ref _showInformation, value))
            {
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public bool ShowWarning
    {
        get => _showWarning;
        set
        {
            if (RaiseAndSetIfChanged(ref _showWarning, value))
            {
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public bool ShowError
    {
        get => _showError;
        set
        {
            if (RaiseAndSetIfChanged(ref _showError, value))
            {
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public bool ShowFatal
    {
        get => _showFatal;
        set
        {
            if (RaiseAndSetIfChanged(ref _showFatal, value))
            {
                RefreshEntries();
                QueueRemoteLogSettingsUpdate();
            }
        }
    }

    public void Clear()
    {
        if (_remoteMutation is null)
        {
            ClearLocalEntries();
            return;
        }

        _ = InvokeRemoteMutationAsync(
            mutation => mutation.ClearLogsAsync(),
            onSuccess: ClearLocalEntries,
            onFailure: ClearLocalEntries);
    }

    internal void SetRemoteMutationSource(IRemoteMutationDiagnosticsDomainService? mutation)
    {
        _remoteMutation = mutation;
    }

    internal void ClearEntriesLocal()
    {
        ClearLocalEntries();
    }

    internal int ApplyLogSettingsFromRemote(RemoteSetLogLevelsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var changed = 0;
        var wasApplying = _isApplyingRemoteSettings;
        _isApplyingRemoteSettings = true;
        try
        {
            if (request.ShowVerbose is { } showVerbose && ShowVerbose != showVerbose)
            {
                ShowVerbose = showVerbose;
                changed++;
            }

            if (request.ShowDebug is { } showDebug && ShowDebug != showDebug)
            {
                ShowDebug = showDebug;
                changed++;
            }

            if (request.ShowInformation is { } showInformation && ShowInformation != showInformation)
            {
                ShowInformation = showInformation;
                changed++;
            }

            if (request.ShowWarning is { } showWarning && ShowWarning != showWarning)
            {
                ShowWarning = showWarning;
                changed++;
            }

            if (request.ShowError is { } showError && ShowError != showError)
            {
                ShowError = showError;
                changed++;
            }

            if (request.ShowFatal is { } showFatal && ShowFatal != showFatal)
            {
                ShowFatal = showFatal;
                changed++;
            }

            if (request.MaxEntries is { } maxEntries && MaxEntries != maxEntries)
            {
                MaxEntries = maxEntries;
                changed++;
            }
        }
        finally
        {
            _isApplyingRemoteSettings = wasApplying;
        }

        return changed;
    }

    public bool RemoveSelectedRecord()
    {
        if (SelectedEntry is not { } selected)
        {
            return false;
        }

        var removed = _entries.Remove(selected);
        if (removed)
        {
            SelectedEntry = null;
            RefreshEntries();
        }

        return removed;
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
        if (SelectedEntry is not null)
        {
            SelectedEntry = null;
            return true;
        }

        if (!string.IsNullOrEmpty(LogsFilter.FilterString))
        {
            LogsFilter.FilterString = string.Empty;
            return true;
        }

        return false;
    }

    public void Refresh()
    {
        RefreshEntries();
    }

    public void SetOptions(DevToolsOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var collector = options.LogCollector ?? InProcessDevToolsLogCollector.Instance;
        if (ReferenceEquals(_logCollector, collector))
        {
            return;
        }

        _subscription.Dispose();
        _logCollector = collector;
        CollectorName = _logCollector.CollectorName;
        _subscription = _logCollector.Subscribe(OnLogCaptured);
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }

    private void OnLogCaptured(DevToolsLogEvent capturedEvent)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AddEntry(capturedEvent);
        }
        else
        {
            Dispatcher.UIThread.Post(() => AddEntry(capturedEvent));
        }
    }

    private void AddEntry(DevToolsLogEvent capturedEvent)
    {
        _entries.Add(new LogEntryViewModel(
            capturedEvent.Timestamp,
            capturedEvent.Level,
            capturedEvent.Area,
            capturedEvent.Source,
            capturedEvent.Message));

        TrimToMaxEntries();
        RefreshEntries();
    }

    private void ClearLocalEntries()
    {
        _entries.Clear();
        SelectedEntry = null;
        Refresh();
    }

    private void RefreshEntries()
    {
        // Re-assigning the predicate ensures the underlying collection view re-evaluates
        // filter state when level toggles are changed repeatedly.
        _entriesView.Filter = FilterEntry;
        _entriesView.Refresh();
        RaisePropertyChanged(nameof(EntryCount));
        RaisePropertyChanged(nameof(VisibleEntryCount));
    }

    private void TrimToMaxEntries()
    {
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(0);
        }
    }

    private bool FilterEntry(object obj)
    {
        if (obj is not LogEntryViewModel entry)
        {
            return true;
        }

        if (!IsLevelVisible(entry.Level))
        {
            return false;
        }

        if (LogsFilter.Filter(entry.Message))
        {
            return true;
        }

        if (LogsFilter.Filter(entry.Area))
        {
            return true;
        }

        if (LogsFilter.Filter(entry.Source))
        {
            return true;
        }

        return LogsFilter.Filter(entry.Level.ToString());
    }

    private bool IsLevelVisible(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => ShowVerbose,
            LogEventLevel.Debug => ShowDebug,
            LogEventLevel.Information => ShowInformation,
            LogEventLevel.Warning => ShowWarning,
            LogEventLevel.Error => ShowError,
            LogEventLevel.Fatal => ShowFatal,
            _ => true
        };
    }

    private bool NavigateSelection(bool forward)
    {
        var visible = _entriesView.Cast<LogEntryViewModel>().ToArray();
        if (visible.Length == 0)
        {
            SelectedEntry = null;
            return false;
        }

        var currentIndex = Array.IndexOf(visible, SelectedEntry);
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

        SelectedEntry = visible[nextIndex];
        return true;
    }

    private void QueueRemoteLogSettingsUpdate()
    {
        if (_remoteMutation is null || _isApplyingRemoteSettings)
        {
            return;
        }

        _ = ApplyRemoteLogSettingsAsync();
    }

    private async Task ApplyRemoteLogSettingsAsync()
    {
        var mutation = _remoteMutation;
        if (mutation is null)
        {
            return;
        }

        var request = new RemoteSetLogLevelsRequest
        {
            ShowVerbose = ShowVerbose,
            ShowDebug = ShowDebug,
            ShowInformation = ShowInformation,
            ShowWarning = ShowWarning,
            ShowError = ShowError,
            ShowFatal = ShowFatal,
            MaxEntries = MaxEntries,
        };

        try
        {
            await mutation.SetLogLevelsAsync(request).ConfigureAwait(false);
        }
        catch
        {
            // Keep local log filtering responsive when remote settings update fails.
        }
    }

    private async Task InvokeRemoteMutationAsync(
        Func<IRemoteMutationDiagnosticsDomainService, ValueTask<RemoteMutationResult>> action,
        Action? onSuccess = null,
        Action? onFailure = null)
    {
        var mutation = _remoteMutation;
        if (mutation is null)
        {
            onFailure?.Invoke();
            return;
        }

        try
        {
            _isApplyingRemoteSettings = true;
            await action(mutation).ConfigureAwait(false);
            onSuccess?.Invoke();
        }
        catch
        {
            onFailure?.Invoke();
        }
        finally
        {
            _isApplyingRemoteSettings = false;
        }
    }
}
