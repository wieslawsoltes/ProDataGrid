using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using DataGridSample.Behaviors;
using DataGridSample.CustomDrawing;
using DataGridSample.Helpers;
using DataGridSample.Models;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class OptimizedFlatCellPathsViewModel : ReactiveObject
{
    private const int PreviewRowCount = 1_000;
    private const int MaximumRowCount = 1_000_000;

    private static readonly string[] s_categories =
        { "Compute", "Storage", "Network", "Identity", "Analytics", "Messaging" };
    private static readonly string[] s_owners =
        { "Atlas", "Beacon", "Cirrus", "Delta", "Ember", "Flux", "Gaia", "Helix" };
    private static readonly string[] s_regions =
        { "eu-central", "eu-west", "us-east", "us-west", "ap-south", "ap-northeast" };
    private static readonly string[] s_states =
        { "Healthy", "Busy", "Pending", "Throttled", "Recovering" };

    private readonly IReadOnlyDictionary<string, IList<DataGridColumnDefinition>> _columnsByPath;
    private readonly IReadOnlyDictionary<string, IList<DataGridColumnDefinition>> _virtualColumnsByMode;
    private IReadOnlyList<OptimizedCellSampleRow> _items;
    private readonly DataGridColumnDefinitionList _activeColumns;
    private readonly DataGridColumnDefinitionList _activeVirtualColumns;
    private OptimizedCellPathOption _selectedPath;
    private VirtualSurfaceModeOption _selectedVirtualMode;
    private OptimizedCellSampleRow? _selectedItem;
    private int _targetRowCount = 250_000;
    private string _summary;
    private string _managedMemorySummary;

    public OptimizedFlatCellPathsViewModel()
    {
        Paths = CreatePaths();
        VirtualModes = CreateVirtualModes();
        var customDrawingFactory = new SkiaTextCellDrawOperationFactory
        {
            MetricsCacheCapacity = 8_192,
        };
        _columnsByPath = new Dictionary<string, IList<DataGridColumnDefinition>>(StringComparer.Ordinal)
        {
            ["standard"] = CreateColumns(FlatColumnPath.Standard, customDrawingFactory),
            ["optimized-theme"] = CreateColumns(FlatColumnPath.Standard, customDrawingFactory),
            ["direct-accessor"] = CreateColumns(FlatColumnPath.DirectAccessor, customDrawingFactory),
            ["direct-cell"] = CreateColumns(FlatColumnPath.DirectCell, customDrawingFactory),
            ["built-in-drawn"] = CreateColumns(FlatColumnPath.BuiltInDrawn, customDrawingFactory),
            ["custom-drawn"] = CreateColumns(FlatColumnPath.CustomDrawn, customDrawingFactory),
        };

        _virtualColumnsByMode = CreateVirtualColumns();

        _selectedPath = Paths[0];
        _activeColumns = new DataGridColumnDefinitionList(_columnsByPath[_selectedPath.Key]);
        _selectedVirtualMode = VirtualModes[0];
        _activeVirtualColumns = new DataGridColumnDefinitionList(_virtualColumnsByMode[_selectedVirtualMode.Key]);
        _items = CreateRows(PreviewRowCount);
        SelectionModel = new SelectionModel<OptimizedCellSampleRow> { SingleSelect = true };
        Operations = new VirtualSurfaceDataOperationsViewModel(
            nameof(OptimizedCellSampleRow.Owner),
            nameof(OptimizedCellSampleRow.Owner));
        _summary = $"Preview: {_items.Count:n0} rows. Load the representative workload for manual profiling.";
        _managedMemorySummary = CreateManagedMemorySummary();

        LoadRepresentativeWorkloadCommand = ReactiveCommand.CreateFromTask(LoadRepresentativeWorkloadAsync);
        JumpToFirstCommand = ReactiveCommand.Create(JumpToFirst);
        JumpToMiddleCommand = ReactiveCommand.Create(JumpToMiddle);
        JumpToLastCommand = ReactiveCommand.Create(JumpToLast);
        RefreshManagedMemoryCommand = ReactiveCommand.Create(RefreshManagedMemory);
    }

    public IReadOnlyList<OptimizedCellPathOption> Paths { get; }

    public IReadOnlyList<VirtualSurfaceModeOption> VirtualModes { get; }

    public IReadOnlyList<OptimizedCellSampleRow> Items
    {
        get => _items;
        private set => this.RaiseAndSetIfChanged(ref _items, value);
    }

    public IList<DataGridColumnDefinition> ActiveColumns => _activeColumns;

    public IList<DataGridColumnDefinition> ActiveVirtualColumns => _activeVirtualColumns;

    public SelectionModel<OptimizedCellSampleRow> SelectionModel { get; }

    public VirtualSurfaceDataOperationsViewModel Operations { get; }

    public OptimizedCellPathOption SelectedPath
    {
        get => _selectedPath;
        set
        {
            if (value is null || ReferenceEquals(_selectedPath, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedPath, value);
            ReplaceColumns(_activeColumns, _columnsByPath[value.Key]);
            this.RaisePropertyChanged(nameof(UseOptimizedTheme));
        }
    }

    public VirtualSurfaceModeOption SelectedVirtualMode
    {
        get => _selectedVirtualMode;
        set
        {
            if (value is null || ReferenceEquals(_selectedVirtualMode, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedVirtualMode, value);
            ReplaceColumns(_activeVirtualColumns, _virtualColumnsByMode[value.Key]);
        }
    }

    public bool UseOptimizedTheme => SelectedPath.UsesOptimizedTheme;

    public OptimizedCellSampleRow? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public int TargetRowCount
    {
        get => _targetRowCount;
        set => this.RaiseAndSetIfChanged(ref _targetRowCount, Math.Clamp(value, PreviewRowCount, MaximumRowCount));
    }

    public string Summary
    {
        get => _summary;
        private set => this.RaiseAndSetIfChanged(ref _summary, value);
    }

    public string ManagedMemorySummary
    {
        get => _managedMemorySummary;
        private set => this.RaiseAndSetIfChanged(ref _managedMemorySummary, value);
    }

    public ReactiveCommand<RxVoid, RxVoid> LoadRepresentativeWorkloadCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> JumpToFirstCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> JumpToMiddleCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> JumpToLastCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RefreshManagedMemoryCommand { get; }

    private static void ReplaceColumns(
        DataGridColumnDefinitionList target,
        IEnumerable<DataGridColumnDefinition> columns)
    {
        using (target.SuspendNotifications())
        {
            target.Clear();
            target.AddRange(columns);
        }
    }

    public async Task LoadRepresentativeWorkloadAsync()
    {
        int count = TargetRowCount;
        Summary = $"Generating {count:n0} immutable rows off the UI thread...";
        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<OptimizedCellSampleRow> rows = await Task.Run(() => CreateRows(count));
        stopwatch.Stop();

        Items = rows;
        SelectionModel.Clear();
        SelectedItem = null;
        Summary = $"Loaded {rows.Count:n0} rows in {stopwatch.Elapsed.TotalSeconds:n2}s. Scroll or use the equal-distance jumps.";
        RefreshManagedMemory();
    }

    private static IReadOnlyList<OptimizedCellPathOption> CreatePaths() =>
        new[]
        {
            new OptimizedCellPathOption(
                "standard",
                "Standard retained",
                "DataGridCell + normal retained TextBlock/content template",
                "DisplayMode=Retained; generic row, cell, and header themes",
                "Compatibility baseline for arbitrary templates, converters, validation, and dynamic resources.",
                false),
            new OptimizedCellPathOption(
                "optimized-theme",
                "Optimized retained theme",
                "DataGridCell + normal retained TextBlock",
                "DataGridOptimizedFeatureUnfrozenRowTheme + DataGridOptimizedCellTheme + optimized feature header",
                "Keeps Avalonia layout and standard bindings while removing redundant chrome and presenters.",
                true),
            new OptimizedCellPathOption(
                "direct-accessor",
                "Retained direct accessor",
                "DataGridCell + retained TextBlock",
                "UseDirectTextContent=True; typed accessors; immutable-value tracking disabled",
                "Keeps the normal retained content path but avoids one display binding expression per realized text cell.",
                true),
            new OptimizedCellPathOption(
                "direct-cell",
                "Direct retained text cell",
                "DataGridDirectTextCell",
                "UseDirectTextCell=True; typed accessors; immutable-value tracking disabled",
                "Coalesces the retained cell and text presentation while preserving selection, automation, and editing.",
                true),
            new OptimizedCellPathOption(
                "built-in-drawn",
                "Built-in drawn text",
                "DataGridCustomDrawingCell",
                "DisplayMode=Drawn; typed accessors; retained editor on edit",
                "Uses the built-in drawn display renderer for the smallest realized display tree.",
                true),
            new OptimizedCellPathOption(
                "custom-drawn",
                "Custom Skia draw operation",
                "DataGridCustomDrawingCell",
                "DataGridCustomDrawingColumn + direct accessor + cached Skia draw-operation metrics",
                "Shows the extensible custom-rendering path with shared bounded text measurement caching.",
                true),
        };

    private static IReadOnlyList<VirtualSurfaceModeOption> CreateVirtualModes() =>
        new[]
        {
            new VirtualSurfaceModeOption(
                "virtual",
                "Text baseline — fastest",
                "The benchmark endpoint: typed text accessors recorded by one rowless surface.",
                true),
            new VirtualSurfaceModeOption("virtual-checkbox", "Checkbox", "Centered two-state checkbox surface renderer.", true),
            new VirtualSurfaceModeOption("virtual-date", "Date", "Custom yyyy-MM-dd date surface renderer.", true),
            new VirtualSurfaceModeOption("virtual-time", "Time", "24-hour time surface renderer with seconds.", true),
            new VirtualSurfaceModeOption("virtual-masked", "Masked text", "Raw typed text on the surface; mask behavior remains in the editor.", true),
            new VirtualSurfaceModeOption("virtual-autocomplete", "Autocomplete text", "Typed display text on the surface; suggestions remain in the editor.", true),
            new VirtualSurfaceModeOption("virtual-slider-text", "Slider value text", "Centered formatted slider value text on the surface.", true),
            new VirtualSurfaceModeOption("virtual-combobox-text", "Editable ComboBox text", "Formatted text and dropdown glyph on the surface; interaction remains in the editor.", true),
            new VirtualSurfaceModeOption(
                "virtual-all",
                "All supported renderers",
                "Shows text, checkbox, date, time, masked, autocomplete, slider-text, and ComboBox-text columns together.",
                true),
        };

    private static IReadOnlyDictionary<string, IList<DataGridColumnDefinition>> CreateVirtualColumns() =>
        new Dictionary<string, IList<DataGridColumnDefinition>>(StringComparer.Ordinal)
        {
            ["virtual"] = CreateVirtualTextColumns(),
            ["virtual-all"] = CreateAllVirtualColumns(),
            ["virtual-checkbox"] = CreateVirtualVariantColumns(CreateVirtualCheckBoxColumn()),
            ["virtual-date"] = CreateVirtualVariantColumns(CreateVirtualDateColumn()),
            ["virtual-time"] = CreateVirtualVariantColumns(CreateVirtualTimeColumn()),
            ["virtual-masked"] = CreateVirtualVariantColumns(CreateVirtualMaskedColumn()),
            ["virtual-autocomplete"] = CreateVirtualVariantColumns(CreateVirtualAutoCompleteColumn()),
            ["virtual-slider-text"] = CreateVirtualVariantColumns(CreateVirtualSliderColumn()),
            ["virtual-combobox-text"] = CreateVirtualVariantColumns(CreateVirtualComboBoxColumn()),
        };

    private static IList<DataGridColumnDefinition> CreateVirtualTextColumns() =>
        new DataGridColumnDefinition[]
        {
            CreateVirtualTextColumn(
                "ID", nameof(OptimizedCellSampleRow.Id), static row => row.Id, static (row, value) => row.Id = value, 90),
            CreateVirtualTextColumn(
                "Name", nameof(OptimizedCellSampleRow.Name), static row => row.Name, static (row, value) => row.Name = value, 240),
            CreateVirtualTextColumn(
                "Owner", nameof(OptimizedCellSampleRow.Owner), static row => row.Owner, static (row, value) => row.Owner = value, 130),
            CreateVirtualTextColumn(
                "Detail", nameof(OptimizedCellSampleRow.Detail), static row => row.Detail, static (row, value) => row.Detail = value, 300),
        };

    private static IList<DataGridColumnDefinition> CreateVirtualVariantColumns(DataGridColumnDefinition variant) =>
        new DataGridColumnDefinition[]
        {
            CreateVirtualTextColumn(
                "ID", nameof(OptimizedCellSampleRow.Id), static row => row.Id, static (row, value) => row.Id = value, 90),
            CreateVirtualTextColumn(
                "Name", nameof(OptimizedCellSampleRow.Name), static row => row.Name, static (row, value) => row.Name = value, 240),
            CreateVirtualTextColumn(
                "Owner", nameof(OptimizedCellSampleRow.Owner), static row => row.Owner, static (row, value) => row.Owner = value, 130),
            variant,
        };

    private static IList<DataGridColumnDefinition> CreateAllVirtualColumns() =>
        new DataGridColumnDefinition[]
        {
            CreateVirtualTextColumn(
                "ID", nameof(OptimizedCellSampleRow.Id), static row => row.Id, static (row, value) => row.Id = value, 90),
            CreateVirtualTextColumn(
                "Name", nameof(OptimizedCellSampleRow.Name), static row => row.Name, static (row, value) => row.Name = value, 220),
            CreateVirtualCheckBoxColumn(),
            CreateVirtualDateColumn(),
            CreateVirtualTimeColumn(),
            CreateVirtualMaskedColumn(),
            CreateVirtualAutoCompleteColumn(),
            CreateVirtualSliderColumn(),
            CreateVirtualComboBoxColumn(),
        };

    private static DataGridTextColumnDefinition CreateVirtualTextColumn(
        string header,
        string propertyName,
        Func<OptimizedCellSampleRow, string> getter,
        Action<OptimizedCellSampleRow, string> setter,
        double width) =>
        new()
        {
            Header = header,
            Binding = ColumnDefinitionBindingFactory.CreateBinding(propertyName, getter, setter),
            ColumnKey = propertyName,
            SortMemberPath = propertyName,
            Width = new DataGridLength(width),
            IsReadOnly = false,
            TrackDirectTextValueChanges = false,
        };

    private static DataGridCheckBoxColumnDefinition CreateVirtualCheckBoxColumn() =>
        new()
        {
            Header = "Active",
            Binding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, bool>(
                nameof(OptimizedCellSampleRow.IsActive),
                static row => row.IsActive,
                static (row, value) => row.IsActive = value),
            ColumnKey = nameof(OptimizedCellSampleRow.IsActive),
            SortMemberPath = nameof(OptimizedCellSampleRow.IsActive),
            Width = new DataGridLength(110),
            IsReadOnly = false,
        };

    private static DataGridDatePickerColumnDefinition CreateVirtualDateColumn() =>
        new()
        {
            Header = "Date",
            Binding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, DateTime>(
                nameof(OptimizedCellSampleRow.Date),
                static row => row.Date,
                static (row, value) => row.Date = value),
            ColumnKey = nameof(OptimizedCellSampleRow.Date),
            SortMemberPath = nameof(OptimizedCellSampleRow.Date),
            Width = new DataGridLength(150),
            IsReadOnly = false,
            SelectedDateFormat = CalendarDatePickerFormat.Custom,
            CustomDateFormatString = "yyyy-MM-dd",
        };

    private static DataGridTimePickerColumnDefinition CreateVirtualTimeColumn() =>
        new()
        {
            Header = "Time",
            Binding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, TimeSpan>(
                nameof(OptimizedCellSampleRow.Time),
                static row => row.Time,
                static (row, value) => row.Time = value),
            ColumnKey = nameof(OptimizedCellSampleRow.Time),
            SortMemberPath = nameof(OptimizedCellSampleRow.Time),
            Width = new DataGridLength(135),
            IsReadOnly = false,
            ClockIdentifier = "24HourClock",
            UseSeconds = true,
        };

    private static DataGridMaskedTextColumnDefinition CreateVirtualMaskedColumn() =>
        new()
        {
            Header = "Phone",
            Binding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, string>(
                nameof(OptimizedCellSampleRow.Phone),
                static row => row.Phone,
                static (row, value) => row.Phone = value),
            ColumnKey = nameof(OptimizedCellSampleRow.Phone),
            SortMemberPath = nameof(OptimizedCellSampleRow.Phone),
            Width = new DataGridLength(170),
            IsReadOnly = false,
            Mask = "(000) 000-0000",
        };

    private static DataGridAutoCompleteColumnDefinition CreateVirtualAutoCompleteColumn() =>
        new()
        {
            Header = "Autocomplete",
            Binding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, string>(
                nameof(OptimizedCellSampleRow.Category),
                static row => row.Category,
                static (row, value) => row.Category = value),
            ColumnKey = nameof(OptimizedCellSampleRow.Category),
            SortMemberPath = nameof(OptimizedCellSampleRow.Category),
            Width = new DataGridLength(170),
            IsReadOnly = false,
            ItemsSource = s_categories,
        };

    private static DataGridSliderColumnDefinition CreateVirtualSliderColumn() =>
        new()
        {
            Header = "Slider text",
            Binding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, double>(
                nameof(OptimizedCellSampleRow.SliderValue),
                static row => row.SliderValue,
                static (row, value) => row.SliderValue = value),
            ColumnKey = nameof(OptimizedCellSampleRow.SliderValue),
            SortMemberPath = nameof(OptimizedCellSampleRow.SliderValue),
            Width = new DataGridLength(145),
            IsReadOnly = false,
            Minimum = 0,
            Maximum = 100,
            ShowValueText = true,
            ValueTextFormat = "{0:0.0}",
        };

    private static DataGridComboBoxColumnDefinition CreateVirtualComboBoxColumn() =>
        new()
        {
            Header = "ComboBox text",
            TextBinding = ColumnDefinitionBindingFactory.CreateBinding<OptimizedCellSampleRow, string>(
                nameof(OptimizedCellSampleRow.Category),
                static row => row.Category,
                static (row, value) => row.Category = value),
            ColumnKey = nameof(OptimizedCellSampleRow.Category),
            SortMemberPath = nameof(OptimizedCellSampleRow.Category),
            Width = new DataGridLength(180),
            IsReadOnly = false,
            IsEditable = true,
            ItemsSource = s_categories,
        };

    private static IList<DataGridColumnDefinition> CreateColumns(
        FlatColumnPath path,
        SkiaTextCellDrawOperationFactory customDrawingFactory) =>
        new[]
        {
            CreateColumn("ID", nameof(OptimizedCellSampleRow.Id), static row => row.Id, 0.7, path, customDrawingFactory),
            CreateColumn("Name", nameof(OptimizedCellSampleRow.Name), static row => row.Name, 1.4, path, customDrawingFactory),
            CreateColumn("Category", nameof(OptimizedCellSampleRow.Category), static row => row.Category, 1.0, path, customDrawingFactory),
            CreateColumn("Owner", nameof(OptimizedCellSampleRow.Owner), static row => row.Owner, 1.0, path, customDrawingFactory),
            CreateColumn("Region", nameof(OptimizedCellSampleRow.Region), static row => row.Region, 1.0, path, customDrawingFactory),
            CreateColumn("State", nameof(OptimizedCellSampleRow.State), static row => row.State, 1.0, path, customDrawingFactory),
            CreateColumn("Detail", nameof(OptimizedCellSampleRow.Detail), static row => row.Detail, 2.4, path, customDrawingFactory),
        };

    private static DataGridColumnDefinition CreateColumn(
        string header,
        string propertyName,
        Func<OptimizedCellSampleRow, string> getter,
        double width,
        FlatColumnPath path,
        SkiaTextCellDrawOperationFactory customDrawingFactory)
    {
        DataGridBindingDefinition binding = ColumnDefinitionBindingFactory.CreateBinding(propertyName, getter);
        if (path == FlatColumnPath.CustomDrawn)
        {
            return new DataGridCustomDrawingColumnDefinition
            {
                Header = header,
                Binding = binding,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star),
                IsReadOnly = true,
                UseDirectValueAccessor = true,
                TrackDirectValueChanges = false,
                DrawingMode = DataGridCustomDrawingMode.DrawOperation,
                RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
                DrawOperationFactory = customDrawingFactory,
                DrawOperationLayoutFastPath = true,
                TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
                SharedTextLayoutCacheCapacity = 4_096,
            };
        }

        return new DataGridTextColumnDefinition
        {
            Header = header,
            Binding = binding,
            Width = new DataGridLength(width, DataGridLengthUnitType.Star),
            IsReadOnly = true,
            DisplayMode = path == FlatColumnPath.BuiltInDrawn
                ? DataGridColumnDisplayMode.Drawn
                : DataGridColumnDisplayMode.Retained,
            UseDirectTextContent = path == FlatColumnPath.DirectAccessor,
            UseDirectTextCell = path == FlatColumnPath.DirectCell,
            TrackDirectTextValueChanges = false,
        };
    }

    private static IReadOnlyList<OptimizedCellSampleRow> CreateRows(int count)
    {
        var rows = new OptimizedCellSampleRow[count];
        for (int index = 0; index < rows.Length; index++)
        {
            int id = index + 1;
            string category = s_categories[index % s_categories.Length];
            string owner = s_owners[(index / s_categories.Length) % s_owners.Length];
            string region = s_regions[(index / 11) % s_regions.Length];
            string state = s_states[(index / 17) % s_states.Length];
            rows[index] = new OptimizedCellSampleRow(
                id.ToString("D7"),
                $"Work item {id:D7}",
                category,
                owner,
                region,
                state,
                $"{category} workload shard {id % 4_096:D4} owned by {owner}",
                isActive: id % 2 == 0,
                date: new DateTime(2020, 1, 1).AddDays(id % 3_650),
                time: TimeSpan.FromSeconds(id % 86_400),
                phone: $"(555) {id % 1_000:D3}-{id % 10_000:D4}",
                sliderValue: (id % 1_000) / 10d);
        }

        return rows;
    }

    private void JumpToFirst() => SelectedItem = Items.Count == 0 ? null : Items[0];

    private void JumpToMiddle() => SelectedItem = Items.Count == 0 ? null : Items[Items.Count / 2];

    private void JumpToLast() => SelectedItem = Items.Count == 0 ? null : Items[^1];

    private void RefreshManagedMemory() => ManagedMemorySummary = CreateManagedMemorySummary();

    private static string CreateManagedMemorySummary() =>
        $"Process managed heap snapshot: {GC.GetTotalMemory(false) / (1024d * 1024d):n1} MiB";

    private enum FlatColumnPath
    {
        Standard,
        DirectAccessor,
        DirectCell,
        BuiltInDrawn,
        CustomDrawn,
    }
}

public sealed class OptimizedFlatCellPathsViewModelFactory : IDataContextFactory
{
    public object Create() => new OptimizedFlatCellPathsViewModel();
}
