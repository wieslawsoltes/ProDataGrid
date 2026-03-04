using System;
using System.ComponentModel;
using Avalonia.Data;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class RemotePropertyViewModel : PropertyViewModel
{
    private readonly string _name;
    private readonly string _group;
    private readonly string _displayType;
    private readonly Type _propertyType;
    private readonly Type _assignedType;
    private readonly Type? _declaringType;
    private readonly string _priority;
    private readonly bool? _isAttached;
    private readonly bool _isReadOnly;
    private readonly Action<RemotePropertyViewModel, object?>? _setValueCallback;
    private object? _value;

    public RemotePropertyViewModel(
        string name,
        string group,
        string displayType,
        string? declaringTypeName,
        string priority,
        bool? isAttached,
        bool isReadOnly,
        string? valueText,
        Action<RemotePropertyViewModel, object?>? setValueCallback = null)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "(unknown)" : name;
        _group = string.IsNullOrWhiteSpace(group) ? "Properties" : group;
        _displayType = string.IsNullOrWhiteSpace(displayType) ? "Object" : displayType;
        _declaringType = TryResolveType(declaringTypeName);
        _propertyType = typeof(object);
        _assignedType = typeof(object);
        _priority = priority ?? string.Empty;
        _isAttached = isAttached;
        _isReadOnly = isReadOnly;
        _setValueCallback = setValueCallback;
        _value = valueText;
    }

    public override object Key => _name;

    public override string Name => _name;

    public override string Group => IsPinned ? "Pinned" : _group;

    public override Type AssignedType => _assignedType;

    public override Type? DeclaringType => _declaringType;

    public override object? Value
    {
        get => _value;
        set
        {
            if (IsReadonly)
            {
                return;
            }

            if (ReferenceEquals(value, BindingOperations.DoNothing))
            {
                return;
            }

            if (RaiseAndSetIfChanged(ref _value, value))
            {
                _setValueCallback?.Invoke(this, value);
            }
        }
    }

    public override string Priority => _priority;

    public override bool? IsAttached => _isAttached;

    public override void Update()
    {
        // Remote snapshot values are immutable for read-only phase.
    }

    public override Type PropertyType => _propertyType;

    public override bool IsReadonly => _isReadOnly;

    public override string DisplayType => _displayType;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsPinned))
        {
            RaisePropertyChanged(nameof(Group));
        }
    }

    private static Type? TryResolveType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        return global::System.Type.GetType(typeName, throwOnError: false);
    }
}
