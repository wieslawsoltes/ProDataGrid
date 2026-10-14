using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using ReactiveUI.Avalonia;
using Avalonia.Automation;
using Avalonia.Browser;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ProCharts.Controls;

namespace ProCharts.Sample.Browser;

[SupportedOSPlatform("browser")]
public static partial class Program
{
    private static bool _ready;
    public static async Task Main(string[] args)
    {
        await AppBuilder.Configure<App>().UseReactiveUI(static _ => { }).WithInterFont().StartBrowserAppAsync("out", new BrowserPlatformOptions
        { RenderingMode = new[] { BrowserRenderingMode.WebGL2, BrowserRenderingMode.Software2D } });
        App app = (App)Application.Current!;
        app.ExportService = new BrowserExportService();
        app.View!.FindControl<ProChart>("Chart")!.ExportService = app.ExportService;
        _ready = true;
    }
    [JSImport("save", "chart-host")]
    internal static partial void Save(string name, string mimeType, string base64);

    /// <summary>Read-only, source-generated diagnostics of the real Avalonia visual tree, for browser tests.</summary>
    [JSExport]
    public static string GetState()
    {
        BrowserState state = new() { Ready = _ready };
        if (Application.Current is App { Gallery: { } vm, View: { } view } app)
        {
            ProChart chart = view.FindControl<ProChart>("Chart")!;
            state.Title = chart.Title; state.Kind = vm.SelectedKind.ToString(); state.Theme = app.ActualThemeVariant.ToString();
            state.Compact = vm.IsCompact; state.HasData = chart.HasData; state.Empty = chart.IsEmpty;
            state.Loading = chart.IsLoading; state.Error = chart.HasError; state.Hover = chart.HoverPoint?.SeriesName;
            state.ExportError = chart.ExportError; state.Status = chart.StatusText; state.Series = vm.Model.Snapshot.Series.Count;
            state.Points = vm.Model.Snapshot.Series.Sum(s => s.Values.Count); state.RowsReceived = vm.ReceivedRows;
            state.Streaming = vm.IsStreaming; state.CanUndo = vm.Model.CanUndoWindow; state.CanRedo = vm.Model.CanRedoWindow;
            if (vm.Model.TryGetVisibleWindow(out int total, out int start, out int count))
            { state.Total = total; state.Start = start; state.Count = count; }
            state.Legend = chart.LegendItems.Select(x => x.IsVisible).ToArray();
            state.Elements = new List<ElementBounds>();
            foreach (Control control in view.GetVisualDescendants().OfType<Control>())
            {
                if (!control.IsEffectivelyVisible || control.Bounds.Width < 1 || control.Bounds.Height < 1) continue;
                string? id = AutomationProperties.GetAutomationId(control);
                if (control is ListBoxItem && control.DataContext is GalleryExample example) id = "case-" + example.Id;
                if (control is CheckBox && control.DataContext is ChartLegendItem legend) id = "legend-" + legend.Index;
                if (string.IsNullOrEmpty(id)) continue;
                Point? p = control.TranslatePoint(default, view);
                if (p.HasValue) state.Elements.Add(new ElementBounds(id, p.Value.X, p.Value.Y, control.Bounds.Width,
                    control.Bounds.Height, control.IsEffectivelyEnabled));
            }
        }
        return JsonSerializer.Serialize(state, BrowserJsonContext.Default.BrowserState);
    }
    /// <summary>Explicit gallery selection used for exhaustive renderer smoke tests, not a substitute for pointer tests.</summary>
    [JSExport]
    public static void SelectKind(string kind)
    {
        if (!Enum.TryParse(kind, false, out ChartSeriesKind value) || !Enum.IsDefined(value)) throw new ArgumentException("Unknown chart kind", nameof(kind));
        ((App)Application.Current!).Gallery!.SelectedKind = value;
    }
    [JSExport]
    public static string GetKinds() => string.Join(",", Enum.GetNames<ChartSeriesKind>());
    [JSExport]
    public static void SelectExample(string id)
    {
        ChartGalleryViewModel vm = ((App)Application.Current!).Gallery!;
        vm.SelectedExample = vm.Examples.Single(x => x.Id == id);
    }
}

[SupportedOSPlatform("browser")]
internal sealed class BrowserExportService : IChartExportService
{
    public Task SaveAsync(ChartExportPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Program.Save(payload.FileName, payload.MimeType, Convert.ToBase64String(payload.Data));
        return Task.CompletedTask;
    }
}
public sealed class BrowserState
{
    public bool Ready { get; set; }
    public string? Title { get; set; }
    public string? Kind { get; set; }
    public string? Theme { get; set; }
    public bool Compact { get; set; }
    public bool HasData { get; set; }
    public bool Empty { get; set; }
    public bool Loading { get; set; }
    public bool Error { get; set; }
    public string? Hover { get; set; }
    public string? ExportError { get; set; }
    public string? Status { get; set; }
    public int Series { get; set; }
    public int Points { get; set; }
    public int Total { get; set; }
    public int Start { get; set; }
    public int Count { get; set; }
    public long RowsReceived { get; set; }
    public bool Streaming { get; set; }
    public bool CanUndo { get; set; }
    public bool CanRedo { get; set; }
    public bool[] Legend { get; set; } = Array.Empty<bool>();
    public List<ElementBounds> Elements { get; set; } = new();
}
public sealed record ElementBounds(string Id, double X, double Y, double Width, double Height, bool Enabled);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserState))]
internal partial class BrowserJsonContext : JsonSerializerContext { }
