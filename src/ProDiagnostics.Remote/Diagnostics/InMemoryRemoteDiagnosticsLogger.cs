using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// In-memory bounded logger for remote attach diagnostics entries.
/// </summary>
public sealed class InMemoryRemoteDiagnosticsLogger : IRemoteDiagnosticsLogger
{
    private readonly object _sync = new();
    private readonly RemoteDiagnosticsLogEntry[] _entries;
    private int _nextIndex;
    private int _count;

    public InMemoryRemoteDiagnosticsLogger(int maxEntries = 1024)
    {
        var capacity = maxEntries <= 0 ? 1024 : maxEntries;
        _entries = new RemoteDiagnosticsLogEntry[capacity];
    }

    public int Capacity => _entries.Length;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _count;
            }
        }
    }

    public void Log(in RemoteDiagnosticsLogEntry entry)
    {
        lock (_sync)
        {
            _entries[_nextIndex] = entry;
            _nextIndex++;
            if (_nextIndex == _entries.Length)
            {
                _nextIndex = 0;
            }

            if (_count < _entries.Length)
            {
                _count++;
            }
        }
    }

    public IReadOnlyList<RemoteDiagnosticsLogEntry> GetSnapshot()
    {
        lock (_sync)
        {
            if (_count == 0)
            {
                return Array.Empty<RemoteDiagnosticsLogEntry>();
            }

            var result = new RemoteDiagnosticsLogEntry[_count];
            var start = _count == _entries.Length ? _nextIndex : 0;
            for (var i = 0; i < _count; i++)
            {
                var index = (start + i) % _entries.Length;
                result[i] = _entries[index];
            }

            return result;
        }
    }
}
