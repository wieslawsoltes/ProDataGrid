using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Benchmarks;

public class DataGridSelectionInsertBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public DataGridSelectionInsertBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public void InsertBenchmark_Reports_Latency_Of_Inserts_Above_A_Large_Row_Selection()
    {
        const int rowCount = 5_000;
        const int inserts = 20;
        const double defaultMaxAverageMs = 1000;

        var maxAverageMs = ResolveBudget("DATAGRID_SELECTION_INSERT_MAX_AVERAGE_MS", defaultMaxAverageMs);
        var items = new ObservableCollection<string>(Enumerable.Range(0, rowCount).Select(i => $"Item {i}"));
        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            Height = 400,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(".") });
        var window = new Window { Width = 400, Height = 450, Content = grid };
        window.SetThemeStyles();

        try
        {
            window.Show();
            grid.UpdateLayout();
            grid.SelectAll();
            grid.UpdateLayout();
            items.Insert(0, "warm-up");
            grid.UpdateLayout();
            int selected = grid.SelectedItems.Count;

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
                $"Selection insert benchmark: rows={rowCount}, selected={selected}, inserts above the selection={inserts}; avg={average:F2} ms, max={samples.Max():F2} ms; budget avg={maxAverageMs:F0} ms"));

            Assert.Equal(selected, grid.SelectedItems.Count);
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
