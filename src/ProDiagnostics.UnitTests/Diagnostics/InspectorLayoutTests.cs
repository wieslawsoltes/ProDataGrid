extern alias Diagnostics;

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Themes.Simple;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using InspectorGrid = Diagnostics::Avalonia.Controls.DataGrid;
using InspectorRow = Diagnostics::Avalonia.Controls.DataGridRow;

namespace Avalonia.Diagnostics.UnitTests.Diagnostics;

public class InspectorLayoutTests
{
    [AvaloniaTheory]
    [InlineData(600)]
    [InlineData(721)]
    [InlineData(855)]
    [InlineData(1200)]
    public void Selecting_Controls_In_A_Narrow_Inspector_Keeps_Rows_Bounded_And_Settles_Layout(int width)
    {
        var button = new Button { Content = "Play" };
        var toggle = new ToggleButton { Content = "Loop" };
        var playback = new StackPanel { Children = { button, toggle } };
        var carousel = new Carousel { ItemsSource = new[] { new Grid { Children = { playback } }, new Grid() } };
        var menu = new Menu { ItemsSource = new[] { new MenuItem { Header = "File" } } };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { menu, carousel } };
        Grid.SetRow(carousel, 1);
        var owner = new Window { Content = root, Width = 1024, Height = 737 };
        owner.Styles.Add(new SimpleTheme());
        using var model = new MainViewModel(owner);
        var window = new MainWindow { DataContext = model, Width = width, Height = 737 };
        var priorLogger = Logger.Sink;
        var logger = new LayoutCycleLogger();
        Logger.Sink = logger;
        try
        {
            owner.Show();
            owner.UpdateLayout();
            window.Show();
            foreach (var control in new Control[] { owner, button, toggle, carousel, menu, button })
            {
                model.SelectControl(control);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var properties = Assert.Single(window.GetVisualDescendants().OfType<ControlPropertiesView>());
                var grid = properties.FindControl<InspectorGrid>("DataGrid")!;
                var rows = grid.GetVisualDescendants().OfType<InspectorRow>().ToArray();
                Assert.InRange(rows.Length, 1, 64);
                Assert.Contains(rows, row => row.IsVisible && row.DesiredSize.Height > 0);
                Assert.Equal(0, logger.Cycles);
            }
        }
        finally
        {
            window.Close();
            owner.Close();
            Logger.Sink = priorLogger;
        }
    }

    private sealed class LayoutCycleLogger : ILogSink
    {
        public int Cycles { get; private set; }
        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Layout;
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            if (messageTemplate.Contains("Layout cycle", StringComparison.Ordinal))
                Cycles++;
        }
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
            => Log(level, area, source, messageTemplate);
    }
}
