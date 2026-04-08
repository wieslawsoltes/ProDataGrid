using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridFilling;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using ProDataGrid.ExcelSample.Helpers;
using ProDataGrid.ExcelSample.Models;
using ReactiveUI;

namespace ProDataGrid.ExcelSample.ViewModels;

public sealed class WorkbookViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();
    private readonly IScheduler _uiScheduler;
    private readonly IScheduler _backgroundScheduler;
    private readonly Random _random = new(2048);
    private SheetViewModel _selectedSheet;
    private RibbonTabViewModel _selectedRibbonTab;
    private string? _searchText;
    private string? _filterText;
    private string? _nameBoxText = "A1";
    private string? _formulaText = string.Empty;
    private string? _formulaError;
    private string _statusLeft = "Ready";
    private string _statusRight = "100%";
    private bool _isLiveUpdates;
    private bool _isChartPanelVisible;
    private bool _isFormulaBarVisible = true;
    private readonly ChartPanelViewModel _chartPanel;
    private readonly SpreadsheetClipboardState _clipboardState;

    public WorkbookViewModel()
        : this(ReactiveUI.RxSchedulers.MainThreadScheduler, ReactiveUI.RxSchedulers.TaskpoolScheduler, startLiveUpdates: false)
    {
    }

    public WorkbookViewModel(IScheduler uiScheduler, IScheduler backgroundScheduler, bool startLiveUpdates = true)
    {
        _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));
        _backgroundScheduler = backgroundScheduler ?? throw new ArgumentNullException(nameof(backgroundScheduler));
        _isLiveUpdates = startLiveUpdates;

        Sheets = new ObservableCollection<SheetViewModel>
        {
            new SheetViewModel("Sheet1"),
            new SheetViewModel("Sheet2", rowCount: 120),
            new SheetViewModel("Sheet3", rowCount: 80)
        };
        _selectedSheet = Sheets[0];

        SortingModel = new SortingModel
        {
            OwnsViewSorts = true,
            MultiSort = true
        };
        FilteringModel = new FilteringModel
        {
            OwnsViewFilter = true
        };
        SearchModel = new SearchModel
        {
            HighlightMode = SearchHighlightMode.TextAndCell,
            HighlightCurrent = true
        };

        FastPathOptions = new DataGridFastPathOptions
        {
            UseAccessorsOnly = true,
            ThrowOnMissingAccessor = true
        };

        QuickCommands = new ObservableCollection<RibbonCommandViewModel>
        {
            CreateCommand("Undo"),
            CreateCommand("Redo"),
            CreateCommand("Save")
        };

        RibbonTabs = BuildRibbonTabs();
        _selectedRibbonTab = RibbonTabs[0];

        SelectionState = new SpreadsheetSelectionState();
        FillModel = new SpreadsheetFillModel();
        ConditionalFormattingModel = BuildConditionalFormattingModel();
        _clipboardState = new SpreadsheetClipboardState();
        _chartPanel = new ChartPanelViewModel(SelectionState, _uiScheduler);
        _chartPanel.AutoApplySelection = false;
        _chartPanel.IsEnabled = false;
        _chartPanel.SetSheet(_selectedSheet);
        CommitFormulaCommand = ReactiveCommand.Create(CommitFormula);
        CancelFormulaCommand = ReactiveCommand.Create(CancelFormula);
        CommitNameCommand = ReactiveCommand.Create(CommitName);
        ReorderSheetCommand = ReactiveCommand.Create<SheetTabReorderRequest>(ApplySheetReorder);

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.SearchText)
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => ApplySearch()));

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.FilterText, vm => vm.SelectedSheet)
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => ApplyFilter()));

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.IsLiveUpdates)
                .Select(enabled => enabled
                    ? Observable.Interval(TimeSpan.FromMilliseconds(450), _backgroundScheduler).ObserveOn(_uiScheduler)
                    : Observable.Empty<long>())
                .Switch()
                .Subscribe(_ => ApplyLiveUpdates()));

        _subscriptions.Add(
            SelectionState.Changed
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => UpdateSelectionDisplay()));

        _subscriptions.Add(
            _clipboardState.WhenAnyValue(state => state.CopiedRange, state => state.ClipboardRowCount, state => state.ClipboardColumnCount)
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => UpdateSelectionDisplay()));

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.SelectedSheet)
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => UpdateSelectionDisplay()));

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.SelectedSheet)
                .ObserveOn(_uiScheduler)
                .Subscribe(sheet => _chartPanel.SetSheet(sheet)));
    }

    public ObservableCollection<SheetViewModel> Sheets { get; }

    public SheetViewModel SelectedSheet
    {
        get => _selectedSheet;
        set => this.RaiseAndSetIfChanged(ref _selectedSheet, value);
    }

    public ObservableCollection<RibbonTabViewModel> RibbonTabs { get; }

    public RibbonTabViewModel SelectedRibbonTab
    {
        get => _selectedRibbonTab;
        set => this.RaiseAndSetIfChanged(ref _selectedRibbonTab, value);
    }

    public ObservableCollection<RibbonCommandViewModel> QuickCommands { get; }

    public SortingModel SortingModel { get; }

    public FilteringModel FilteringModel { get; }

    public SearchModel SearchModel { get; }

    public DataGridFastPathOptions FastPathOptions { get; }

    public SpreadsheetSelectionState SelectionState { get; }

    public SpreadsheetClipboardState ClipboardState => _clipboardState;

    public IDataGridFillModel FillModel { get; }

    public IConditionalFormattingModel ConditionalFormattingModel { get; }

    public ChartPanelViewModel ChartPanel => _chartPanel;

    public ReactiveCommand<Unit, Unit> CommitFormulaCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelFormulaCommand { get; }

    public ReactiveCommand<Unit, Unit> CommitNameCommand { get; }

    public ReactiveCommand<SheetTabReorderRequest, Unit> ReorderSheetCommand { get; }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public string? FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public string? NameBoxText
    {
        get => _nameBoxText;
        set => this.RaiseAndSetIfChanged(ref _nameBoxText, value);
    }

    public string? FormulaText
    {
        get => _formulaText;
        set => this.RaiseAndSetIfChanged(ref _formulaText, value);
    }

    public bool IsChartPanelVisible
    {
        get => _isChartPanelVisible;
        set
        {
            if (_isChartPanelVisible == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isChartPanelVisible, value);
            _chartPanel.IsEnabled = value;
        }
    }

    public bool IsFormulaBarVisible
    {
        get => _isFormulaBarVisible;
        set => this.RaiseAndSetIfChanged(ref _isFormulaBarVisible, value);
    }

    public string StatusLeft
    {
        get => _statusLeft;
        private set => this.RaiseAndSetIfChanged(ref _statusLeft, value);
    }

    public string StatusRight
    {
        get => _statusRight;
        private set => this.RaiseAndSetIfChanged(ref _statusRight, value);
    }

    public string? FormulaError
    {
        get => _formulaError;
        private set => this.RaiseAndSetIfChanged(ref _formulaError, value);
    }

    public bool IsLiveUpdates
    {
        get => _isLiveUpdates;
        set => this.RaiseAndSetIfChanged(ref _isLiveUpdates, value);
    }

    public void Dispose()
    {
        _chartPanel.Dispose();
        _subscriptions.Dispose();
    }

    private void UpdateSelectionDisplay()
    {
        var current = SelectionState.CurrentCell;
        var range = SelectionState.SelectedRange;
        if (range.HasValue)
        {
            NameBoxText = range.Value.ToA1Range();
        }
        else if (current.HasValue)
        {
            NameBoxText = current.Value.ToA1();
        }
        else
        {
            NameBoxText = string.Empty;
        }

        UpdateSelectionStatus(range, current);
        UpdateStatusLeft();
        UpdateFormulaText(current);
    }

    private void ApplySheetReorder(SheetTabReorderRequest request)
    {
        var count = Sheets.Count;
        if (count == 0)
        {
            return;
        }

        var fromIndex = request.FromIndex;
        var toIndex = request.ToIndex;

        if (fromIndex < 0 || fromIndex >= count)
        {
            return;
        }

        if (toIndex < 0)
        {
            toIndex = 0;
        }
        else if (toIndex > count)
        {
            toIndex = count;
        }

        if (fromIndex == toIndex || fromIndex + 1 == toIndex)
        {
            return;
        }

        var sheet = Sheets[fromIndex];
        Sheets.RemoveAt(fromIndex);

        if (fromIndex < toIndex)
        {
            toIndex--;
        }

        if (toIndex < 0)
        {
            toIndex = 0;
        }
        else if (toIndex > Sheets.Count)
        {
            toIndex = Sheets.Count;
        }

        Sheets.Insert(toIndex, sheet);
        SelectedSheet = sheet;
    }

    private void UpdateSelectionStatus(SpreadsheetCellRange? range, SpreadsheetCellReference? current)
    {
        var selectionRange = range ?? (current.HasValue
            ? new SpreadsheetCellRange(current.Value, current.Value)
            : (SpreadsheetCellRange?)null);

        if (range.HasValue)
        {
            var cells = range.Value.RowCount * range.Value.ColumnCount;
            var selectionText = range.Value.IsSingleCell
                ? range.Value.ToA1Range()
                : $"{range.Value.RowCount} x {range.Value.ColumnCount} ({cells} cells)";
            StatusRight = ComposeStatusRight(selectionText, selectionRange);
            return;
        }

        var currentText = current.HasValue ? current.Value.ToA1() : string.Empty;
        StatusRight = ComposeStatusRight(currentText, selectionRange);
    }

    private void UpdateFormulaText(SpreadsheetCellReference? current)
    {
        if (!current.HasValue)
        {
            FormulaText = string.Empty;
            return;
        }

        if (!SelectedSheet.TryGetCell(current.Value, out var cell))
        {
            FormulaText = string.Empty;
            return;
        }

        FormulaText = cell;
    }

    private void SetFormulaError(string? error)
    {
        FormulaError = string.IsNullOrWhiteSpace(error) ? null : error;
        UpdateStatusLeft();
    }

    private void UpdateStatusLeft()
    {
        if (!string.IsNullOrWhiteSpace(FormulaError))
        {
            StatusLeft = FormulaError!;
            return;
        }

        if (_clipboardState.HasClipboard)
        {
            if (_clipboardState.CopiedRange.HasValue)
            {
                StatusLeft = $"Clipboard: {_clipboardState.CopiedRange.Value.ToA1Range()}";
                return;
            }

            StatusLeft = $"Clipboard: {_clipboardState.ClipboardRowCount} x {_clipboardState.ClipboardColumnCount}";
            return;
        }

        StatusLeft = "Ready";
    }

    private string ComposeStatusRight(string selectionText, SpreadsheetCellRange? selectionRange)
    {
        var preview = BuildPastePreview(selectionRange);
        if (string.IsNullOrWhiteSpace(preview))
        {
            return selectionText;
        }

        if (string.IsNullOrWhiteSpace(selectionText))
        {
            return preview;
        }

        return $"{selectionText} • {preview}";
    }

    private string? BuildPastePreview(SpreadsheetCellRange? selectionRange)
    {
        if (!selectionRange.HasValue || !_clipboardState.HasClipboard)
        {
            return null;
        }

        var rows = _clipboardState.ClipboardRowCount;
        var columns = _clipboardState.ClipboardColumnCount;
        if (rows <= 0 || columns <= 0)
        {
            return null;
        }

        var targetRange = selectionRange.Value;
        if (targetRange.RowCount >= rows && targetRange.ColumnCount >= columns &&
            targetRange.RowCount % rows == 0 && targetRange.ColumnCount % columns == 0 &&
            (targetRange.RowCount != rows || targetRange.ColumnCount != columns))
        {
            return $"Paste preview: {targetRange.ToA1Range()} ({rows}x{columns} repeat)";
        }

        var start = targetRange.Start;
        var end = new SpreadsheetCellReference(start.RowIndex + rows - 1, start.ColumnIndex + columns - 1);
        var previewRange = new SpreadsheetCellRange(start, end);
        return $"Paste preview: {previewRange.ToA1Range()}";
    }

    private void ApplySearch()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            SearchModel.Clear();
            return;
        }

        SearchModel.Apply(new[]
        {
            new SearchDescriptor(_searchText)
        });
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(_filterText))
        {
            FilteringModel.Clear();
            return;
        }

        var column = SelectedSheet.ColumnDefinitions[0];
        FilteringModel.Apply(new[]
        {
            new FilteringDescriptor(column, FilteringOperator.Contains, value: _filterText, stringComparison: StringComparison.OrdinalIgnoreCase)
        });
    }

    private void ApplyLiveUpdates()
    {
        if (!IsLiveUpdates)
        {
            return;
        }

        var rows = SelectedSheet.Rows;
        if (rows.Count == 0)
        {
            return;
        }

        for (var i = 0; i < 4; i++)
        {
            var row = rows[_random.Next(rows.Count)];
            row.SetCell(1, _random.Next(1, 50));
            row.SetCell(2, Math.Round(_random.NextDouble() * 250 + 10, 2));
            row.SetCell(3, Math.Round(_random.NextDouble() * 0.25, 2));
            row.SetCell(8, Math.Round(_random.NextDouble() * 100, 2));
            row.SetCell(10, Math.Round(_random.NextDouble() * 2 - 1, 2));
        }
    }

    private void CommitFormula()
    {
        var current = SelectionState.CurrentCell;
        if (!current.HasValue)
        {
            return;
        }

        var input = FormulaText ?? string.Empty;
        if (!SelectedSheet.TryApplyFormula(current.Value, input, out var error))
        {
            SetFormulaError(error);
            return;
        }

        SetFormulaError(null);
        UpdateFormulaText(current);
    }

    private void CancelFormula()
    {
        var current = SelectionState.CurrentCell;
        if (!current.HasValue)
        {
            FormulaText = string.Empty;
            SetFormulaError(null);
            return;
        }

        SetFormulaError(null);
        UpdateFormulaText(current);
    }

    private void CommitName()
    {
        var name = NameBoxText?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (TrySelectRange(name))
        {
            SetFormulaError(null);
            return;
        }

        if (SelectedSheet.FormulaModel.TryGetNamedRange(name, out var namedFormula) &&
            TrySelectNamedRange(namedFormula))
        {
            SetFormulaError(null);
            return;
        }

        var range = SelectionState.SelectedRange ?? (SelectionState.CurrentCell.HasValue
            ? new SpreadsheetCellRange(SelectionState.CurrentCell.Value, SelectionState.CurrentCell.Value)
            : (SpreadsheetCellRange?)null);

        if (!range.HasValue)
        {
            SetFormulaError("Select a range to name.");
            return;
        }

        var rangeText = range.Value.ToA1Range();
        if (!SelectedSheet.FormulaModel.TrySetNamedRange(name, rangeText, out var error))
        {
            SetFormulaError(error);
            return;
        }

        SetFormulaError(null);
        UpdateSelectionDisplay();
    }

    private bool TrySelectRange(string text)
    {
        if (!SpreadsheetAddressParser.TryParseRange(text, out var range))
        {
            return false;
        }

        SelectionState.SelectedRange = range;
        SelectionState.CurrentCell = range.Start;
        return true;
    }

    private bool TrySelectNamedRange(string? formulaText)
    {
        if (string.IsNullOrWhiteSpace(formulaText))
        {
            return false;
        }

        var trimmed = formulaText.Trim();
        if (trimmed.StartsWith("=", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.IndexOf('!') >= 0)
        {
            return false;
        }

        return TrySelectRange(trimmed);
    }

    private ObservableCollection<RibbonTabViewModel> BuildRibbonTabs()
    {
        return new ObservableCollection<RibbonTabViewModel>
        {
            new(
                "Home",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Clipboard",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Paste"),
                            CreateCommand("Cut"),
                            CreateCommand("Copy")
                        }),
                    new RibbonGroupViewModel(
                        "Font",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Bold"),
                            CreateCommand("Italic"),
                            CreateCommand("Underline")
                        }),
                    new RibbonGroupViewModel(
                        "Alignment",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Left"),
                            CreateCommand("Center"),
                            CreateCommand("Right")
                        })
                }),
            new(
                "Insert",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Tables",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Table"),
                            CreateCommand("PivotTable")
                        }),
                    new RibbonGroupViewModel(
                        "Charts",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Column"),
                            CreateCommand("Line"),
                            CreateCommand("Bar")
                        })
                }),
            new(
                "Page Layout",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Themes",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Themes"),
                            CreateCommand("Colors"),
                            CreateCommand("Fonts")
                        })
                }),
            new(
                "Formulas",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Function Library",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("AutoSum"),
                            CreateCommand("Financial"),
                            CreateCommand("Logical")
                        }),
                    new RibbonGroupViewModel(
                        "Defined Names",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Name Manager"),
                            CreateCommand("Define Name")
                        })
                }),
            new(
                "Data",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Sort & Filter",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Sort A-Z"),
                            CreateCommand("Sort Z-A"),
                            CreateCommand("Filter")
                        }),
                    new RibbonGroupViewModel(
                        "Analysis",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Pivot"),
                            CreateCommand("Chart")
                        })
                }),
            new(
                "Review",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Proofing",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Spelling"),
                            CreateCommand("Translate")
                        }),
                    new RibbonGroupViewModel(
                        "Protect",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Protect Sheet")
                        })
                }),
            new(
                "View",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Show",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Gridlines"),
                            CreateCommand("Headings"),
                            CreateToggleCommand("Formula Bar", value => IsFormulaBarVisible = value),
                            CreateToggleCommand("Chart Pane", value => IsChartPanelVisible = value)
                        }),
                    new RibbonGroupViewModel(
                        "Window",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("New Window"),
                            CreateCommand("Arrange")
                        })
                }),
            new(
                "Help",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Help",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Help"),
                            CreateCommand("Training"),
                            CreateCommand("Feedback")
                        })
                }),
            new(
                "Draw",
                new[]
                {
                    new RibbonGroupViewModel(
                        "Tools",
                        new IRibbonCommandViewModel[]
                        {
                            CreateCommand("Pen"),
                            CreateCommand("Highlighter"),
                            CreateCommand("Eraser")
                        })
                })
        };
    }

    private static RibbonCommandViewModel CreateCommand(string label, string? glyph = null)
    {
        var command = ReactiveCommand.Create(() => { });
        if (string.IsNullOrWhiteSpace(glyph) && !string.IsNullOrWhiteSpace(label))
        {
            glyph = label.Trim()[0].ToString();
        }

        return new RibbonCommandViewModel(label, command, glyph);
    }

    private RibbonToggleCommandViewModel CreateToggleCommand(string label, Action<bool> onToggled, string? glyph = null)
    {
        if (string.IsNullOrWhiteSpace(glyph) && !string.IsNullOrWhiteSpace(label))
        {
            glyph = label.Trim()[0].ToString();
        }

        return new RibbonToggleCommandViewModel(label, IsChartPanelVisible, onToggled, glyph);
    }

    private static IConditionalFormattingModel BuildConditionalFormattingModel()
    {
        var model = new ConditionalFormattingModel();
        model.Apply(new[]
        {
            new ConditionalFormattingDescriptor(
                ruleId: "delta-positive",
                @operator: ConditionalFormattingOperator.GreaterThan,
                columnId: "K",
                value: 0d,
                themeKey: "ExcelDeltaPositiveCellTheme"),
            new ConditionalFormattingDescriptor(
                ruleId: "delta-negative",
                @operator: ConditionalFormattingOperator.LessThan,
                columnId: "K",
                value: 0d,
                themeKey: "ExcelDeltaNegativeCellTheme")
        });

        return model;
    }
}
