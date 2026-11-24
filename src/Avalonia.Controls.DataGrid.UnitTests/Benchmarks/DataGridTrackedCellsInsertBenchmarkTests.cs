using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Benchmarks;

public class DataGridTrackedCellsInsertBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public DataGridTrackedCellsInsertBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsertBenchmark_Reports_Latency_Of_Inserts_Above_Selected_Cells(bool trackSelectedCellsByItem)
    {
        const int rowCount = 2_000;
        const int columnCount = 8;
        const int inserts = 20;
        const double defaultMaxAverageMs = 1000;

        var maxAverageMs = ResolveBudget("DATAGRID_TRACKED_CELLS_INSERT_MAX_AVERAGE_MS", defaultMaxAverageMs);
        var items = new ObservableCollection<string>(Enumerable.Range(0, rowCount).Select(i => $"Item {i}"));
        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.Cell,
            TrackSelectedCellsByItem = trackSelectedCellsByItem,
            Height = 400,
        };
        for (int i = 0; i < columnCount; i++)
        {
            grid.Columns.Add(new DataGridTextColumn { Header = $"C{i}", Binding = new Binding(".") });
        }
        var window = new Window { Width = 900, Height = 450, Content = grid };
        window.SetThemeStyles();

        try
        {
            window.Show();
            grid.UpdateLayout();
            grid.SelectAllCells();
            grid.UpdateLayout();
            items.Insert(0, "warm-up");
            grid.UpdateLayout();
            int selectedCells = grid.SelectedCells.Count;
            int changes = 0;
            grid.SelectedCellsChanged += (_, _) => changes++;

            var samples = new double[inserts];
            for (int i = 0; i < inserts; i++)
            {
                var clock = Stopwatch.StartNew();
                items.Insert(0, $"New {i}");
                grid.UpdateLayout();
                clock.Stop();
                samples[i] = clock.Elapsed.TotalMilliseconds;
            }

            var average = samples.Average();
            _output.WriteLine(FormattableString.Invariant(
                $"Tracked cells insert benchmark: track={trackSelectedCellsByItem}, rows={rowCount}, selected cells={selectedCells}, inserts above={inserts}; avg={average:F2} ms, max={samples.Max():F2} ms, SelectedCellsChanged={changes}; budget avg={maxAverageMs:F0} ms"));

            Assert.Equal(selectedCells, grid.SelectedCells.Count);
            Assert.Equal(trackSelectedCellsByItem ? 0 : inserts, changes);
            Assert.InRange(average, 0, maxAverageMs);
        }
        finally
        {
            window.Close();
        }
    }

    private static double ResolveBudget(string variableName, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }
}
