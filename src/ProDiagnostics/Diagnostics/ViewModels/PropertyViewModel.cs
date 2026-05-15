using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels;

internal abstract class PropertyViewModel : ViewModelBase
{
    private bool _isPinned;

    public abstract object Key { get; }
    public abstract string Name { get; }
    public abstract string Group { get; }
    public abstract Type AssignedType { get; }
    public abstract Type? DeclaringType { get; }
    public abstract object? Value { get; set; }
    public abstract string Priority { get; }
    public abstract bool? IsAttached { get; }
    public abstract void Update();
    public abstract Type PropertyType { get; }

    public string Type => PropertyType == AssignedType ?
        PropertyType.GetTypeName() :
        $"{PropertyType.GetTypeName()} {{{AssignedType.GetTypeName()}}}";

    public abstract bool IsReadonly { get; }

    public bool IsPinned { get => _isPinned; set => RaiseAndSetIfChanged(ref _isPinned, value); }

    public string FullName => $"{GetType().Name.Replace("PropertyViewModel","")}:{DeclaringType?.FullName}.{Name}";

    internal AvaloniaObject? InspectedObject { get; set; }

    internal IDevToolsPropertyEditHandler? PropertyEditHandler { get; set; }

    protected abstract object Target { get; }

    protected abstract string XamlPropertyName { get; }

    protected abstract bool IsAvaloniaProperty { get; }

    internal virtual bool SupportsDynamicResourceReferences => false;

    internal virtual bool TrySetResourceReference(ResourceReferenceCandidate candidate, out string? error)
    {
        error = "This property cannot be set from a resource reference.";
        return false;
    }

    protected void NotifyPropertyEdited(
        object? oldValue,
        object? newValue,
        DevToolsResourceReferenceKind resourceReferenceKind = DevToolsResourceReferenceKind.None,
        object? resourceKey = null,
        string? resourceKeyText = null)
    {
        if (resourceReferenceKind == DevToolsResourceReferenceKind.None &&
            Equals(oldValue, newValue))
        {
            return;
        }

        if (InspectedObject is not { } inspectedObject ||
            PropertyEditHandler is not { } propertyEditHandler)
        {
            return;
        }

        try
        {
            propertyEditHandler.OnPropertyEdited(new DevToolsPropertyEdit(
                inspectedObject,
                Target,
                Name,
                XamlPropertyName,
                PropertyType,
                DeclaringType,
                oldValue,
                newValue,
                ConvertValueToText(oldValue),
                resourceReferenceKind == DevToolsResourceReferenceKind.None || resourceKeyText is null
                    ? ConvertValueToText(newValue)
                    : ResourceReferenceTextFormatter.Format(resourceReferenceKind, resourceKeyText),
                IsAttached == true,
                IsAvaloniaProperty,
                resourceReferenceKind,
                resourceKey,
                resourceKeyText));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static string? ConvertValueToText(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return PropertyValueEditorStringConversion.ToString(value);
        }
        catch
        {
            return value.ToString();
        }
    }
}
