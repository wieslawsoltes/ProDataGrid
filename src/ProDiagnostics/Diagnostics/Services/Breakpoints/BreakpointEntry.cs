using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Interactivity;

namespace Avalonia.Diagnostics.Services;

internal sealed class BreakpointEntry : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private int _hitCount;
    private int _triggerAfterHits = 1;
    private bool _suspendExecution;
    private bool _logMessage;
    private bool _removeOnceHit;
    private DateTimeOffset? _lastHitAt;
    private string _lastHitDetails = string.Empty;

    public BreakpointEntry(
        BreakpointKind kind,
        string name,
        RoutedEvent? routedEvent,
        AvaloniaProperty? property,
        AvaloniaObject? target,
        string targetDescription)
    {
        Kind = kind;
        Name = name;
        RoutedEvent = routedEvent;
        Property = property;
        TargetDescription = targetDescription;
        Target = target != null ? new WeakReference<AvaloniaObject>(target) : null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public BreakpointKind Kind { get; }

    public string Name { get; }

    public RoutedEvent? RoutedEvent { get; }

    public AvaloniaProperty? Property { get; }

    public WeakReference<AvaloniaObject>? Target { get; }

    public string TargetDescription { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public int HitCount
    {
        get => _hitCount;
        set => SetProperty(ref _hitCount, value);
    }

    public int TriggerAfterHits
    {
        get => _triggerAfterHits;
        set => SetProperty(ref _triggerAfterHits, value > 0 ? value : 1);
    }

    public bool SuspendExecution
    {
        get => _suspendExecution;
        set => SetProperty(ref _suspendExecution, value);
    }

    public bool LogMessage
    {
        get => _logMessage;
        set => SetProperty(ref _logMessage, value);
    }

    public bool RemoveOnceHit
    {
        get => _removeOnceHit;
        set => SetProperty(ref _removeOnceHit, value);
    }

    public DateTimeOffset? LastHitAt
    {
        get => _lastHitAt;
        set => SetProperty(ref _lastHitAt, value);
    }

    public string LastHitDetails
    {
        get => _lastHitDetails;
        set => SetProperty(ref _lastHitDetails, value);
    }

    public bool MatchesTarget(AvaloniaObject? sender)
    {
        if (Target == null)
        {
            return true;
        }

        return Target.TryGetTarget(out var target) && ReferenceEquals(sender, target);
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
