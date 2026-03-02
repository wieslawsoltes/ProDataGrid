using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class CodePageViewModel : ViewModelBase
{
    private readonly ISourceLocationService _sourceLocationService;
    private readonly Action<SourceDocumentLocation>? _sourceSelectionChanged;
    private AvaloniaObject? _inspectedObject;
    private string _inspectedElement = "(none)";
    private string _inspectedElementType = string.Empty;
    private string _status = "Select an element to inspect source.";
    private int _selectedDocumentTab;
    private string _xamlText = string.Empty;
    private string _xamlFilePath = string.Empty;
    private string _xamlLocationText = string.Empty;
    private int _xamlCaretIndex;
    private int _xamlSelectionStart;
    private int _xamlSelectionEnd;
    private int[] _xamlLineStarts = new[] { 0 };
    private string _codeText = string.Empty;
    private string _codeFilePath = string.Empty;
    private string _codeLocationText = string.Empty;
    private int _codeCaretIndex;
    private int _codeSelectionStart;
    private int _codeSelectionEnd;
    private int[] _codeLineStarts = new[] { 0 };
    private bool _isApplyingCaretSelection;
    private string? _lastPublishedPath;
    private int _lastPublishedLine;
    private int _lastPublishedColumn;

    public CodePageViewModel(
        Func<AvaloniaObject?>? selectedObjectAccessor = null,
        Action<SourceDocumentLocation>? sourceSelectionChanged = null,
        ISourceLocationService? sourceLocationService = null)
    {
        _sourceSelectionChanged = sourceSelectionChanged;
        _sourceLocationService = sourceLocationService ?? new PortablePdbSourceLocationService();
        OpenSourceLocationCommand = SourceLocationOpenCommand.Instance;
        _inspectedObject = selectedObjectAccessor?.Invoke();
    }

    public ICommand OpenSourceLocationCommand { get; }

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

    public string Status
    {
        get => _status;
        private set => RaiseAndSetIfChanged(ref _status, value);
    }

    public bool HasAnyDocument => HasXamlDocument || HasCodeDocument;

    public bool HasXamlDocument => !string.IsNullOrWhiteSpace(XamlText);

    public bool HasCodeDocument => !string.IsNullOrWhiteSpace(CodeText);

    public int SelectedDocumentTab
    {
        get => _selectedDocumentTab;
        set => RaiseAndSetIfChanged(ref _selectedDocumentTab, value);
    }

    public string XamlText
    {
        get => _xamlText;
        private set
        {
            if (RaiseAndSetIfChanged(ref _xamlText, value))
            {
                RaisePropertyChanged(nameof(HasXamlDocument));
                RaisePropertyChanged(nameof(HasAnyDocument));
            }
        }
    }

    public string XamlFilePath
    {
        get => _xamlFilePath;
        private set => RaiseAndSetIfChanged(ref _xamlFilePath, value);
    }

    public string XamlLocationText
    {
        get => _xamlLocationText;
        private set => RaiseAndSetIfChanged(ref _xamlLocationText, value);
    }

    public int XamlCaretIndex
    {
        get => _xamlCaretIndex;
        set
        {
            if (RaiseAndSetIfChanged(ref _xamlCaretIndex, value) && !_isApplyingCaretSelection)
            {
                PublishCaretLocation(DocumentKind.Xaml, value);
            }
        }
    }

    public int XamlSelectionStart
    {
        get => _xamlSelectionStart;
        set => RaiseAndSetIfChanged(ref _xamlSelectionStart, value);
    }

    public int XamlSelectionEnd
    {
        get => _xamlSelectionEnd;
        set => RaiseAndSetIfChanged(ref _xamlSelectionEnd, value);
    }

    public string CodeText
    {
        get => _codeText;
        private set
        {
            if (RaiseAndSetIfChanged(ref _codeText, value))
            {
                RaisePropertyChanged(nameof(HasCodeDocument));
                RaisePropertyChanged(nameof(HasAnyDocument));
            }
        }
    }

    public string CodeFilePath
    {
        get => _codeFilePath;
        private set => RaiseAndSetIfChanged(ref _codeFilePath, value);
    }

    public string CodeLocationText
    {
        get => _codeLocationText;
        private set => RaiseAndSetIfChanged(ref _codeLocationText, value);
    }

    public int CodeCaretIndex
    {
        get => _codeCaretIndex;
        set
        {
            if (RaiseAndSetIfChanged(ref _codeCaretIndex, value) && !_isApplyingCaretSelection)
            {
                PublishCaretLocation(DocumentKind.Code, value);
            }
        }
    }

    public int CodeSelectionStart
    {
        get => _codeSelectionStart;
        set => RaiseAndSetIfChanged(ref _codeSelectionStart, value);
    }

    public int CodeSelectionEnd
    {
        get => _codeSelectionEnd;
        set => RaiseAndSetIfChanged(ref _codeSelectionEnd, value);
    }

    public void InspectControl(AvaloniaObject? target, string? preferredSourceText = null)
    {
        _inspectedObject = target;
        if (target is null)
        {
            InspectedElement = "(none)";
            InspectedElementType = string.Empty;
            ClearDocuments("No inspected element.");
            return;
        }

        InspectedElement = DescribeElement(target);
        InspectedElementType = target.GetType().FullName ?? target.GetType().Name;

        var sourceInfo = _sourceLocationService.ResolveObject(target);
        ApplyDocument(DocumentKind.Xaml, sourceInfo.XamlLocation);
        ApplyDocument(DocumentKind.Code, sourceInfo.CodeLocation);

        var preferredLocation = SourceLocationTextParser.TryParse(preferredSourceText, out var parsedPreferred)
            ? parsedPreferred
            : null;

        var selectedLocation = ResolvePreferredLocation(sourceInfo, preferredLocation);
        if (selectedLocation is null)
        {
            Status = sourceInfo.Status;
            return;
        }

        SelectDocumentByLocation(selectedLocation);
        Status = sourceInfo.HasAnyLocation
            ? "Source synchronized with selection."
            : sourceInfo.Status;
    }

    public void Refresh()
    {
        InspectControl(_inspectedObject);
    }

    private void ApplyDocument(DocumentKind kind, SourceDocumentLocation? location)
    {
        if (location is null || string.IsNullOrWhiteSpace(location.FilePath))
        {
            SetDocumentState(kind, string.Empty, string.Empty, string.Empty, new[] { 0 });
            return;
        }

        var filePath = location.FilePath;
        if (!File.Exists(filePath))
        {
            var missingText = "Unable to load source file: " + filePath;
            SetDocumentState(kind, filePath, location.DisplayText, missingText, new[] { 0 });
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(filePath);
        }
        catch (Exception e)
        {
            text = "Unable to load source file: " + filePath + Environment.NewLine + e.Message;
        }

        SetDocumentState(kind, filePath, location.DisplayText, text, BuildLineStarts(text));
    }

    private void SetDocumentState(DocumentKind kind, string path, string locationText, string text, int[] lineStarts)
    {
        if (lineStarts.Length == 0)
        {
            lineStarts = new[] { 0 };
        }

        if (kind == DocumentKind.Xaml)
        {
            XamlFilePath = path;
            XamlLocationText = locationText;
            XamlText = text;
            _xamlLineStarts = lineStarts;
            XamlCaretIndex = 0;
            XamlSelectionStart = 0;
            XamlSelectionEnd = 0;
            return;
        }

        CodeFilePath = path;
        CodeLocationText = locationText;
        CodeText = text;
        _codeLineStarts = lineStarts;
        CodeCaretIndex = 0;
        CodeSelectionStart = 0;
        CodeSelectionEnd = 0;
    }

    private void SelectDocumentByLocation(SourceDocumentLocation location)
    {
        var targetKind = ResolveDocumentKind(location);
        var lineStarts = targetKind == DocumentKind.Xaml ? _xamlLineStarts : _codeLineStarts;
        var text = targetKind == DocumentKind.Xaml ? XamlText : CodeText;
        var caretIndex = GetCaretIndexForLocation(location, lineStarts, text);
        var (selectionStart, selectionEnd) = GetLineBounds(lineStarts, text, location.Line);

        _isApplyingCaretSelection = true;
        try
        {
            SelectedDocumentTab = targetKind == DocumentKind.Xaml ? 0 : 1;
            if (targetKind == DocumentKind.Xaml)
            {
                XamlCaretIndex = caretIndex;
                XamlSelectionStart = selectionStart;
                XamlSelectionEnd = selectionEnd;
            }
            else
            {
                CodeCaretIndex = caretIndex;
                CodeSelectionStart = selectionStart;
                CodeSelectionEnd = selectionEnd;
            }
        }
        finally
        {
            _isApplyingCaretSelection = false;
        }
    }

    private void PublishCaretLocation(DocumentKind kind, int caretIndex)
    {
        var path = kind == DocumentKind.Xaml ? XamlFilePath : CodeFilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var lineStarts = kind == DocumentKind.Xaml ? _xamlLineStarts : _codeLineStarts;
        if (lineStarts.Length == 0)
        {
            return;
        }

        var line = ResolveLineNumber(lineStarts, caretIndex);
        if (line <= 0)
        {
            return;
        }

        var lineStart = lineStarts[line - 1];
        var column = Math.Max(1, caretIndex - lineStart + 1);
        if (string.Equals(_lastPublishedPath, path, StringComparison.OrdinalIgnoreCase) &&
            _lastPublishedLine == line &&
            _lastPublishedColumn == column)
        {
            return;
        }

        _lastPublishedPath = path;
        _lastPublishedLine = line;
        _lastPublishedColumn = column;
        _sourceSelectionChanged?.Invoke(new SourceDocumentLocation(path, line, "Caret", column));
    }

    private SourceDocumentLocation? ResolvePreferredLocation(
        SourceLocationInfo sourceInfo,
        SourceDocumentLocation? preferredLocation)
    {
        if (preferredLocation is not null)
        {
            if (sourceInfo.XamlLocation is not null &&
                SourceLocationTextParser.IsSameDocument(preferredLocation.FilePath, sourceInfo.XamlLocation.FilePath))
            {
                return new SourceDocumentLocation(
                    sourceInfo.XamlLocation.FilePath,
                    preferredLocation.Line,
                    sourceInfo.XamlLocation.MethodName,
                    preferredLocation.Column);
            }

            if (sourceInfo.CodeLocation is not null &&
                SourceLocationTextParser.IsSameDocument(preferredLocation.FilePath, sourceInfo.CodeLocation.FilePath))
            {
                return new SourceDocumentLocation(
                    sourceInfo.CodeLocation.FilePath,
                    preferredLocation.Line,
                    sourceInfo.CodeLocation.MethodName,
                    preferredLocation.Column);
            }
        }

        if (sourceInfo.XamlLocation is not null)
        {
            return sourceInfo.XamlLocation;
        }

        return sourceInfo.CodeLocation;
    }

    private DocumentKind ResolveDocumentKind(SourceDocumentLocation location)
    {
        if (HasXamlDocument &&
            SourceLocationTextParser.IsSameDocument(location.FilePath, XamlFilePath))
        {
            return DocumentKind.Xaml;
        }

        if (HasCodeDocument &&
            SourceLocationTextParser.IsSameDocument(location.FilePath, CodeFilePath))
        {
            return DocumentKind.Code;
        }

        var extension = Path.GetExtension(location.FilePath);
        if (string.Equals(extension, ".xaml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".axaml", StringComparison.OrdinalIgnoreCase))
        {
            return HasXamlDocument ? DocumentKind.Xaml : DocumentKind.Code;
        }

        return HasCodeDocument ? DocumentKind.Code : DocumentKind.Xaml;
    }

    private void ClearDocuments(string status)
    {
        SetDocumentState(DocumentKind.Xaml, string.Empty, string.Empty, string.Empty, new[] { 0 });
        SetDocumentState(DocumentKind.Code, string.Empty, string.Empty, string.Empty, new[] { 0 });
        Status = status;
    }

    private static int ResolveLineNumber(IReadOnlyList<int> lineStarts, int caretIndex)
    {
        if (lineStarts.Count == 0)
        {
            return 0;
        }

        var index = 0;
        for (var i = 1; i < lineStarts.Count; i++)
        {
            if (lineStarts[i] > caretIndex)
            {
                break;
            }

            index = i;
        }

        return index + 1;
    }

    private static int GetCaretIndexForLocation(SourceDocumentLocation location, IReadOnlyList<int> lineStarts, string text)
    {
        var (lineStart, lineEnd) = GetLineBounds(lineStarts, text, location.Line);
        var caret = lineStart;
        if (location.Column > 0)
        {
            caret = Math.Min(lineEnd, lineStart + Math.Max(0, location.Column - 1));
        }

        return Math.Clamp(caret, 0, text.Length);
    }

    private static (int Start, int End) GetLineBounds(IReadOnlyList<int> lineStarts, string text, int line)
    {
        if (lineStarts.Count == 0)
        {
            return (0, 0);
        }

        var lineIndex = Math.Clamp(line - 1, 0, lineStarts.Count - 1);
        var start = lineStarts[lineIndex];
        var end = lineIndex + 1 < lineStarts.Count ? lineStarts[lineIndex + 1] : text.Length;
        if (end > start && text[end - 1] == '\n')
        {
            end--;
        }

        if (end > start && text[end - 1] == '\r')
        {
            end--;
        }

        return (start, Math.Max(start, end));
    }

    private static int[] BuildLineStarts(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new[] { 0 };
        }

        var starts = new List<int>(Math.Max(16, text.Length / 40)) { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && i + 1 < text.Length)
            {
                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }

    private static string DescribeElement(AvaloniaObject target)
    {
        if (target is INamed named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return named.Name + " (" + target.GetType().Name + ")";
        }

        return target.GetType().Name;
    }

    private enum DocumentKind
    {
        Xaml,
        Code
    }
}
