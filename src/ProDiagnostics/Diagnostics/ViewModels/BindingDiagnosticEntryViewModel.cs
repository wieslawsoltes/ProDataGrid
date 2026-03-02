using Avalonia;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class BindingDiagnosticEntryViewModel
{
    public BindingDiagnosticEntryViewModel(
        string propertyName,
        string ownerType,
        string priority,
        string bindingDescription,
        string diagnostic,
        string valueType,
        string valuePreview,
        bool hasError,
        string status,
        AvaloniaObject? sourceObject)
    {
        PropertyName = propertyName;
        OwnerType = ownerType;
        Priority = priority;
        BindingDescription = bindingDescription;
        Diagnostic = diagnostic;
        ValueType = valueType;
        ValuePreview = valuePreview;
        HasError = hasError;
        Status = status;
        SourceObject = sourceObject;
    }

    public string PropertyName { get; }

    public string OwnerType { get; }

    public string Priority { get; }

    public string BindingDescription { get; }

    public string Diagnostic { get; }

    public string ValueType { get; }

    public string ValuePreview { get; }

    public bool HasError { get; }

    public string Status { get; }

    public AvaloniaObject? SourceObject { get; }
}
