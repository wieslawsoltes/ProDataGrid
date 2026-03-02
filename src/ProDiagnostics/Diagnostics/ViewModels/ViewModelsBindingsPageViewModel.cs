using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.LogicalTree;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class ViewModelsBindingsPageViewModel : ViewModelBase
{
    private readonly AvaloniaList<BindingDiagnosticEntryViewModel> _bindingEntries = new();
    private readonly AvaloniaList<ViewModelContextEntryViewModel> _viewModelEntries = new();
    private readonly DataGridCollectionView _bindingEntriesView;
    private readonly DataGridCollectionView _viewModelEntriesView;
    private readonly Func<AvaloniaObject?>? _selectedObjectAccessor;
    private AvaloniaObject? _inspectedObject;
    private string _inspectedElement = "(none)";
    private string _inspectedElementType = string.Empty;
    private bool _showOnlyBindingErrors;

    public ViewModelsBindingsPageViewModel()
        : this(mainView: null, selectedObjectAccessor: null)
    {
    }

    internal ViewModelsBindingsPageViewModel(Func<AvaloniaObject?>? selectedObjectAccessor)
        : this(mainView: null, selectedObjectAccessor)
    {
    }

    internal ViewModelsBindingsPageViewModel(MainViewModel? mainView, Func<AvaloniaObject?>? selectedObjectAccessor)
    {
        MainView = mainView;
        _selectedObjectAccessor = selectedObjectAccessor;

        BindingsFilter = new FilterViewModel();
        BindingsFilter.RefreshFilter += (_, _) => RefreshBindings();

        ViewModelsFilter = new FilterViewModel();
        ViewModelsFilter.RefreshFilter += (_, _) => RefreshViewModels();

        _bindingEntriesView = new DataGridCollectionView(_bindingEntries)
        {
            Filter = FilterBindingEntry
        };
        _bindingEntriesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(BindingDiagnosticEntryViewModel.HasError),
            ListSortDirection.Descending));
        _bindingEntriesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(BindingDiagnosticEntryViewModel.PropertyName),
            ListSortDirection.Ascending));

        _viewModelEntriesView = new DataGridCollectionView(_viewModelEntries)
        {
            Filter = FilterViewModelEntry
        };
        _viewModelEntriesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(ViewModelContextEntryViewModel.Level),
            ListSortDirection.Ascending));
    }

    public MainViewModel? MainView { get; }

    public FilterViewModel BindingsFilter { get; }

    public FilterViewModel ViewModelsFilter { get; }

    public DataGridCollectionView BindingEntriesView => _bindingEntriesView;

    public DataGridCollectionView ViewModelEntriesView => _viewModelEntriesView;

    public string InspectedElement
    {
        get => _inspectedElement;
        private set => RaiseAndSetIfChanged(ref _inspectedElement, value);
    }

    public string InspectedElementType
    {
        get => _inspectedElementType;
        private set => RaiseAndSetIfChanged(ref _inspectedElementType, value);
    }

    public bool ShowOnlyBindingErrors
    {
        get => _showOnlyBindingErrors;
        set
        {
            if (RaiseAndSetIfChanged(ref _showOnlyBindingErrors, value))
            {
                RefreshBindings();
            }
        }
    }

    public int BindingCount => _bindingEntries.Count;

    public int VisibleBindingCount => _bindingEntriesView.Count;

    public int ViewModelCount => _viewModelEntries.Count;

    public int VisibleViewModelCount => _viewModelEntriesView.Count;

    public void InspectSelection()
    {
        InspectControl(_selectedObjectAccessor?.Invoke());
    }

    public void Refresh()
    {
        InspectControl(_inspectedObject);
    }

    public void Clear()
    {
        _inspectedObject = null;
        _bindingEntries.Clear();
        _viewModelEntries.Clear();
        InspectedElement = "(none)";
        InspectedElementType = string.Empty;
        RefreshBindings();
        RefreshViewModels();
    }

    internal void InspectControl(AvaloniaObject? target)
    {
        _inspectedObject = target;
        _bindingEntries.Clear();
        _viewModelEntries.Clear();

        if (target is null)
        {
            InspectedElement = "(none)";
            InspectedElementType = string.Empty;
            RefreshBindings();
            RefreshViewModels();
            return;
        }

        InspectedElement = DescribeElement(target);
        InspectedElementType = target.GetType().FullName ?? target.GetType().Name;

        CaptureBindingEntries(target);
        CaptureViewModelEntries(target);

        RefreshBindings();
        RefreshViewModels();
    }

    private void CaptureBindingEntries(AvaloniaObject target)
    {
        foreach (var property in EnumerateProperties(target))
        {
            BindingExpressionBase? expression;
            try
            {
                expression = BindingOperations.GetBindingExpressionBase(target, property);
            }
            catch
            {
                continue;
            }

            if (expression is null)
            {
                continue;
            }

            AvaloniaPropertyValue diagnostic;
            try
            {
                diagnostic = target.GetDiagnostic(property);
            }
            catch
            {
                continue;
            }

            var valuePreview = FormatValuePreview(diagnostic.Value);
            var valueType = FormatValueType(diagnostic.Value);
            var bindingDescription = DescribeBindingExpression(expression);
            var hasError = TryHasBindingError(expression, diagnostic.Value, out var status);
            var entry = new BindingDiagnosticEntryViewModel(
                property.Name,
                property.OwnerType.Name,
                diagnostic.Priority.ToString(),
                bindingDescription,
                diagnostic.Diagnostic ?? string.Empty,
                valueType,
                valuePreview,
                hasError,
                status,
                target);
            _bindingEntries.Add(entry);
        }
    }

    private void CaptureViewModelEntries(AvaloniaObject target)
    {
        if (target is not StyledElement styledElement)
        {
            return;
        }

        var level = 0;
        StyledElement? current = styledElement;
        while (current is not null)
        {
            AvaloniaPropertyValue diagnostic;
            try
            {
                diagnostic = current.GetDiagnostic(StyledElement.DataContextProperty);
            }
            catch
            {
                break;
            }

            _viewModelEntries.Add(new ViewModelContextEntryViewModel(
                level,
                DescribeElement(current),
                diagnostic.Priority.ToString(),
                FormatValueType(diagnostic.Value),
                FormatValuePreview(diagnostic.Value),
                isCurrent: level == 0,
                sourceObject: current));

            current = (current as ILogical)?.LogicalParent as StyledElement;
            level++;
        }
    }

    private void RefreshBindings()
    {
        _bindingEntriesView.Refresh();
        RaisePropertyChanged(nameof(BindingCount));
        RaisePropertyChanged(nameof(VisibleBindingCount));
    }

    private void RefreshViewModels()
    {
        _viewModelEntriesView.Refresh();
        RaisePropertyChanged(nameof(ViewModelCount));
        RaisePropertyChanged(nameof(VisibleViewModelCount));
    }

    private bool FilterBindingEntry(object item)
    {
        if (item is not BindingDiagnosticEntryViewModel entry)
        {
            return true;
        }

        if (ShowOnlyBindingErrors && !entry.HasError)
        {
            return false;
        }

        if (BindingsFilter.Filter(entry.PropertyName))
        {
            return true;
        }

        if (BindingsFilter.Filter(entry.OwnerType))
        {
            return true;
        }

        if (BindingsFilter.Filter(entry.BindingDescription))
        {
            return true;
        }

        if (BindingsFilter.Filter(entry.Diagnostic))
        {
            return true;
        }

        if (BindingsFilter.Filter(entry.ValuePreview))
        {
            return true;
        }

        return BindingsFilter.Filter(entry.Status);
    }

    private bool FilterViewModelEntry(object item)
    {
        if (item is not ViewModelContextEntryViewModel entry)
        {
            return true;
        }

        if (ViewModelsFilter.Filter(entry.Element))
        {
            return true;
        }

        if (ViewModelsFilter.Filter(entry.Priority))
        {
            return true;
        }

        if (ViewModelsFilter.Filter(entry.ViewModelType))
        {
            return true;
        }

        return ViewModelsFilter.Filter(entry.ValuePreview);
    }

    private static IEnumerable<AvaloniaProperty> EnumerateProperties(AvaloniaObject target)
    {
        var seen = new HashSet<AvaloniaProperty>();
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(target))
        {
            if (seen.Add(property))
            {
                yield return property;
            }
        }

        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(target.GetType()))
        {
            if (seen.Add(property))
            {
                yield return property;
            }
        }
    }

    private static string DescribeElement(AvaloniaObject target)
    {
        var typeName = target.GetType().Name;
        if (target is StyledElement { Name: { Length: > 0 } name })
        {
            return typeName + "#" + name;
        }

        return typeName;
    }

    private static string DescribeBindingExpression(BindingExpressionBase expression)
    {
        if (expression is UntypedBindingExpressionBase untyped &&
            !string.IsNullOrWhiteSpace(untyped.Description))
        {
            return untyped.Description;
        }

        return expression.ToString() ?? string.Empty;
    }

    private static bool TryHasBindingError(BindingExpressionBase expression, object? value, out string status)
    {
        if (expression is UntypedBindingExpressionBase untyped &&
            untyped.ErrorType is BindingErrorType.Error or BindingErrorType.DataValidationError)
        {
            status = untyped.ErrorType.ToString();
            return true;
        }

        if (value is BindingNotification notification &&
            notification.ErrorType is BindingErrorType.Error or BindingErrorType.DataValidationError)
        {
            status = notification.Error?.Message ?? notification.ErrorType.ToString();
            return true;
        }

        status = "OK";
        return false;
    }

    private static string FormatValueType(object? value)
    {
        var actualValue = UnwrapValue(value);
        if (actualValue is null || ReferenceEquals(actualValue, AvaloniaProperty.UnsetValue))
        {
            return "(none)";
        }

        return actualValue.GetType().FullName ?? actualValue.GetType().Name;
    }

    private static string FormatValuePreview(object? value)
    {
        var actualValue = UnwrapValue(value);
        if (ReferenceEquals(actualValue, AvaloniaProperty.UnsetValue))
        {
            return "(unset)";
        }

        if (actualValue is null)
        {
            return "null";
        }

        var text = actualValue.ToString() ?? string.Empty;
        if (text.Length <= 140)
        {
            return text;
        }

        return text.Substring(0, 140) + "...";
    }

    private static object? UnwrapValue(object? value)
    {
        if (value is BindingNotification notification)
        {
            if (notification.HasValue)
            {
                return notification.Value;
            }

            return AvaloniaProperty.UnsetValue;
        }

        return value;
    }
}
