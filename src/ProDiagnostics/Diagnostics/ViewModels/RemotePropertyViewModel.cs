using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class RemotePropertyViewModel : PropertyViewModel
{
    private readonly string _name;
    private readonly string _group;
    private readonly string _displayType;
    private readonly Type _propertyType;
    private readonly Type _assignedType;
    private readonly Type? _declaringType;
    private readonly string? _declaringTypeName;
    private readonly string _priority;
    private readonly bool? _isAttached;
    private readonly bool _isReadOnly;
    private readonly string _propertyKind;
    private readonly string _editorKindToken;
    private readonly IReadOnlyList<string> _enumOptions;
    private readonly bool _canClearValue;
    private readonly bool _canSetNull;
    private readonly Action<RemotePropertyViewModel, object?>? _setValueCallback;
    private object? _value;

    public RemotePropertyViewModel(
        string name,
        string group,
        string displayType,
        string? assignedTypeName,
        string? propertyTypeName,
        string? declaringTypeName,
        string priority,
        bool? isAttached,
        bool isReadOnly,
        string? valueText,
        string propertyKind = "unknown",
        string editorKind = "text",
        IReadOnlyList<string>? enumOptions = null,
        bool canClearValue = false,
        bool canSetNull = true,
        Action<RemotePropertyViewModel, object?>? setValueCallback = null)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "(unknown)" : name;
        _group = string.IsNullOrWhiteSpace(group) ? "Properties" : group;
        _displayType = string.IsNullOrWhiteSpace(displayType) ? "Object" : displayType;
        _declaringTypeName = declaringTypeName;
        _declaringType = TryResolveType(declaringTypeName);
        _editorKindToken = NormalizeEditorKind(editorKind);
        _propertyType = ResolvePropertyType(propertyTypeName, _editorKindToken);
        _assignedType = ResolveAssignedType(assignedTypeName, _propertyType, _editorKindToken);
        _priority = priority ?? string.Empty;
        _isAttached = isAttached;
        _isReadOnly = isReadOnly;
        _propertyKind = NormalizePropertyKind(propertyKind);
        _enumOptions = enumOptions?
            .Where(static option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        _canClearValue = canClearValue;
        _canSetNull = canSetNull;
        _setValueCallback = setValueCallback;
        _value = ParseInitialValue(valueText, _propertyType, _assignedType, _editorKindToken);
    }

    public override object Key => _name;

    public override string Name => _name;

    public override string Group => IsPinned ? "Pinned" : _group;

    public override Type AssignedType => _assignedType;

    public override Type? DeclaringType => _declaringType;

    public string? DeclaringTypeName => _declaringTypeName;

    public string PropertyKind => _propertyKind;

    public string EditorKindToken => _editorKindToken;

    public IReadOnlyList<string> EnumOptions => _enumOptions;

    public bool CanClearValue => _canClearValue;

    public bool CanSetNull => _canSetNull;

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

        var resolved = global::System.Type.GetType(typeName, throwOnError: false);
        if (resolved is not null)
        {
            return resolved;
        }

        var fullName = typeName;
        var separatorIndex = typeName.IndexOf(',');
        if (separatorIndex > 0)
        {
            fullName = typeName.Substring(0, separatorIndex).Trim();
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            try
            {
                resolved = assembly.GetType(typeName, throwOnError: false)
                    ?? assembly.GetType(fullName, throwOnError: false);
            }
            catch
            {
                resolved = null;
            }

            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static Type ResolvePropertyType(string? propertyTypeName, string editorKindToken)
    {
        return TryResolveType(propertyTypeName) ?? ResolveEditorBindingType(editorKindToken);
    }

    private static Type ResolveAssignedType(string? assignedTypeName, Type propertyType, string editorKindToken)
    {
        return TryResolveType(assignedTypeName) ?? ResolveEditorBindingType(editorKindToken) ?? propertyType;
    }

    private static Type ResolveEditorBindingType(string editorKindToken)
    {
        return editorKindToken switch
        {
            "boolean" => typeof(bool),
            "numeric" => typeof(double),
            "color" => typeof(Color),
            "brush" => typeof(IBrush),
            "image" => typeof(IImage),
            "geometry" => typeof(Geometry),
            "enum" => typeof(string),
            _ => typeof(string),
        };
    }

    private static object? ParseInitialValue(string? valueText, Type propertyType, Type assignedType, string editorKindToken)
    {
        if (valueText is null)
        {
            return null;
        }

        if (propertyType == typeof(string) || assignedType == typeof(string))
        {
            return valueText;
        }

        if (editorKindToken == "enum" && propertyType == typeof(string))
        {
            return valueText;
        }

        if (TryParseValue(valueText, assignedType, out var assignedValue))
        {
            return assignedValue;
        }

        if (TryParseValue(valueText, propertyType, out var propertyValue))
        {
            return propertyValue;
        }

        return valueText;
    }

    private static bool TryParseValue(string valueText, Type targetType, out object? value)
    {
        try
        {
            value = targetType == typeof(string)
                ? valueText
                : Services.PropertyValueEditorStringConversion.FromString(valueText, targetType);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static string NormalizeEditorKind(string editorKind)
    {
        return string.IsNullOrWhiteSpace(editorKind)
            ? "text"
            : editorKind.Trim().ToLowerInvariant();
    }

    private static string NormalizePropertyKind(string propertyKind)
    {
        return string.IsNullOrWhiteSpace(propertyKind)
            ? "unknown"
            : propertyKind.Trim().ToLowerInvariant();
    }
}
