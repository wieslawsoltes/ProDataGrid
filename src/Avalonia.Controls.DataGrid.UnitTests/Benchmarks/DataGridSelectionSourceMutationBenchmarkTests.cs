using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Benchmarks;

public class DataGridSelectionSourceMutationBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public DataGridSelectionSourceMutationBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public void SourceMutationBenchmark_Reports_Insert_Latency_With_Many_Selected_Rows()
    {
        const int rowCount = 20_000;
        const int selectedEvery = 10;
        const int inserts = 20;
        const double defaultMaxAverageMs = 1000;

        var maxAverageMs = ResolveBudget("DATAGRID_SELECTION_SOURCE_MUTATION_MAX_AVERAGE_MS", defaultMaxAverageMs);
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
            for (int i = 0; i < rowCount; i += selectedEvery)
            {
                grid.SelectedItems.Add(items[i]);
            }
            grid.UpdateLayout();
            items.Add("warm-up");
            grid.UpdateLayout();

            var samples = new double[inserts];
            long allocated = 0;
            for (int i = 0; i < inserts; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                items.Add($"New {i}");
                grid.UpdateLayout();
                clock.Stop();
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                samples[i] = clock.Elapsed.TotalMilliseconds;
            }

            var average = samples.Average();
            _output.WriteLine(FormattableString.Invariant(
                $"Selection source mutation benchmark: rows={rowCount}, selected={rowCount / selectedEvery}, inserts={inserts}; avg={average:F2} ms, max={samples.Max():F2} ms, allocated avg={allocated / 1024.0 / inserts:F0} KB; budget avg={maxAverageMs:F0} ms"));

            Assert.Equal(rowCount / selectedEvery, grid.SelectedItems.Count);
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
