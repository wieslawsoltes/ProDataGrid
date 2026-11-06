using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Collections;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

[CollectionDefinition("DataGrid reflection configuration", DisableParallelization = true)]
public sealed class DataGridReflectionCollection;

[Collection("DataGrid reflection configuration")]
public sealed class DataGridReflectionTests
{
    private const string SwitchName = "ProDataGrid.Reflection.IsEnabled";

    [Fact]
    public void Disabled_reflection_does_not_query_item_descriptors()
    {
        using var settings = new ReflectionSetting(false);
        var source = new DescriptorProbeList();

        Assert.Null(DataGridItemPropertyDescriptor.CreateDescriptors(source, typeof(Row)));
        Assert.Equal(0, source.DescriptorQueries);
    }

    [AvaloniaFact]
    public void Disabled_reflection_accepts_explicit_columns_without_discovering_metadata()
    {
        using var settings = new ReflectionSetting(false);
        var source = new DescriptorProbeList { new Row { Name = "Beta" } };
        var grid = new DataGrid { AutoGenerateColumns = false, ItemsSource = source };

        Assert.Same(source[0], grid.DataConnection.GetDataItem(0));
        Assert.Null(grid.DataConnection.DataProperties);
        Assert.Equal(0, source.DescriptorQueries);
    }

    [Fact]
    public void Disabled_reflection_rejects_path_sorting_instead_of_losing_the_sort()
    {
        using var settings = new ReflectionSetting(false);

        Assert.Throws<NotSupportedException>(() => DataGridSortDescription.FromPath(nameof(Row.Name)));
    }

    [Fact]
    public void Disabled_reflection_preserves_accessor_sorting_and_updates()
    {
        using var settings = new ReflectionSetting(false);
        var beta = new Row { Name = "Beta" };
        var alpha = new Row { Name = "Alpha" };
        var view = new DataGridCollectionView(new[] { beta, alpha });
        var accessor = new DataGridColumnValueAccessor<Row, string>(row => row.Name, (row, value) => row.Name = value);

        view.SortDescriptions.Add(DataGridSortDescription.FromAccessor(accessor));
        Assert.Same(alpha, view[0]);
        Assert.Same(beta, view[1]);
        accessor.SetValue(beta, "Aardvark");
        view.Refresh();
        Assert.Same(beta, view[0]);
        Assert.Equal("Aardvark", beta.Name);
    }

    [Fact]
    public void Enabled_reflection_preserves_property_path_sorting()
    {
        using var settings = new ReflectionSetting(true);
        var beta = new Row { Name = "Beta" };
        var alpha = new Row { Name = "Alpha" };
        var view = new DataGridCollectionView(new[] { beta, alpha });

        view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Row.Name)));
        Assert.Same(alpha, view[0]);
        Assert.Same(beta, view[1]);
    }

    [AvaloniaFact]
    public void Disabled_reflection_preserves_compiled_binding_edit_commit()
    {
        using var settings = new ReflectionSetting(false);
        var item = new Row { Name = "Before" };
        var textBox = new TextBox { DataContext = item };
        var root = new Window { Content = textBox };
        var property = new ClrPropertyInfo(nameof(Row.Name), value => ((Row)value).Name,
            (value, name) => ((Row)value).Name = (string)name!, typeof(string));
        var binding = new CompiledBindingExtension
        {
            Path = new CompiledBindingPathBuilder()
                .Property(property, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
                .Build(),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        };

        root.Show();
        textBox.Bind(TextBox.TextProperty, binding);
        Dispatcher.UIThread.RunJobs();
        using var editBinding = new CellEditBinding(textBox, TextBox.TextProperty, binding);
        try
        {
            Assert.Equal("Before", textBox.Text);
            textBox.Text = "After";
            Assert.True(editBinding.CommitEdit());
            Assert.Equal("After", item.Name);
            Assert.True(editBinding.IsValid);
        }
        finally
        {
            root.Close();
        }
    }

    [Fact]
    public void Disabled_reflection_preserves_standard_value_conversion_and_nullable_empty_input()
    {
        using var settings = new ReflectionSetting(false);

        Assert.Equal(12.5m, DataGridValueConverter.Instance.ConvertBack("12.5", typeof(decimal?), null, CultureInfo.InvariantCulture));
        Assert.Equal("12.5", DataGridValueConverter.Instance.Convert(12.5m, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Null(DataGridValueConverter.Instance.ConvertBack("", typeof(decimal?), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void String_conversion_preserves_null_and_the_original_reference(bool reflectionEnabled)
    {
        using var settings = new ReflectionSetting(reflectionEnabled);
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        foreach (string? value in new string?[] { null, string.Empty, new string('x', 32), "I\u0130" })
        {
            Assert.Same(value, DataGridValueConverter.Instance.Convert(value, typeof(string), new object(), culture));
        }
    }

    [Fact]
    public void Disabled_reflection_requires_an_explicit_converter_for_custom_target_types()
    {
        using var settings = new ReflectionSetting(false);

        Assert.Throws<NotSupportedException>(() => DataGridValueConverter.Instance.ConvertBack(
            "https://example.test", typeof(Uri), null, CultureInfo.InvariantCulture));
    }
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Typed_columns_preserve_real_grid_writable_capability_and_edit_commit(bool reflectionEnabled)
    {
        using var settings = new ReflectionSetting(reflectionEnabled);
        var item = new Row { Name = "Before" };
        var grid = CreateTypedGrid(item, writable: true);
        var window = CreateWindow(grid);
        try
        {
            Assert.False(grid.Columns[0].IsReadOnly);
            window.Show();
            grid.ApplyTemplate();
            grid.UpdateLayout();
            grid.UpdateSelectionAndCurrency(0, grid.SlotFromRowIndex(0), DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
            grid.UpdateLayout();
            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();
            var cell = grid.GetVisualDescendants().OfType<DataGridCell>()
                .First(value => value.OwningColumn?.Header?.ToString() == grid.Columns[0].Header?.ToString() && value.OwningRow?.Index == 0);
            var editor = Assert.IsType<TextBox>(cell.Content);
            editor.Text = "After";
            Assert.True(grid.CommitEdit());
            Assert.Equal("After", item.Name);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_reflection_preserves_real_grid_getter_only_column_readonly_capability()
    {
        using var settings = new ReflectionSetting(false);
        var grid = CreateTypedGrid(new Row { Name = "Read only" }, writable: false);

        Assert.True(grid.Columns[0].IsReadOnly);
    }

    [AvaloniaFact]
    public void Disabled_reflection_preserves_explicit_grid_and_column_readonly_overrides()
    {
        using var settings = new ReflectionSetting(false);
        var grid = CreateTypedGrid(new Row { Name = "Read only" }, writable: true);
        grid.IsReadOnly = true;
        Assert.True(grid.Columns[0].IsReadOnly);
        grid.IsReadOnly = false;
        grid.Columns[0].IsReadOnly = true;
        Assert.True(grid.Columns[0].IsReadOnly);
    }

    [AvaloniaFact]
    public void Disabled_reflection_rejects_slider_reflection_display_binding()
    {
        using var settings = new ReflectionSetting(false);
        var column = new SliderProbe { Binding = new Binding(nameof(Row.Number)), ShowValueText = true };

        Assert.Throws<NotSupportedException>(() => column.Render(new Row()));
    }

    [AvaloniaFact]
    public void Disabled_reflection_rejects_time_picker_reflection_display_binding()
    {
        using var settings = new ReflectionSetting(false);
        var column = new TimePickerProbe { Binding = new Binding(nameof(Row.Time)) };

        Assert.Throws<NotSupportedException>(() => column.Render(new Row()));
    }

    [AvaloniaFact]
    public void Disabled_reflection_keeps_readonly_sources_readonly()
    {
        using var settings = new ReflectionSetting(false);
        var item = new Row { Name = "Read only" };
        var grid = CreateTypedGrid(item, writable: true);
        grid.ItemsSource = new ReadOnlyCollection<Row>(new[] { item });

        Assert.True(grid.Columns[0].IsReadOnly);
    }

    [AvaloniaFact]
    public void Disabled_reflection_preserves_formula_display_and_version_invalidation()
    {
        using var settings = new ReflectionSetting(false);
        var definition = new DataGridFormulaColumnDefinition { Header = "Formula", Formula = "1+2" };
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = new ObservableCollection<Row> { new() },
            ColumnDefinitionsSource = new[] { definition }
        };
        var window = CreateWindow(grid);
        try
        {
            window.Show();
            grid.ApplyTemplate();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var cell = grid.GetVisualDescendants().OfType<DataGridCell>()
                .First(value => value.OwningColumn?.Header?.ToString() == grid.Columns[0].Header?.ToString() && value.OwningRow?.Index == 0);
            var display = Assert.IsAssignableFrom<TextBlock>(cell.Content);
            Assert.Equal("3", display.Text);

            definition.Formula = "5+6";
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var updatedCell = grid.GetVisualDescendants().OfType<DataGridCell>()
                .First(value => value.OwningColumn?.Header?.ToString() == "Formula" && value.OwningRow?.Index == 0);
            Assert.Equal("11", Assert.IsAssignableFrom<TextBlock>(updatedCell.Content).Text);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(DataGrid grid)
    {
        var window = new Window { Width = 600, Height = 300 };
        // Theme resources must be installed before the control joins the styling tree.
        window.SetThemeStyles();
        window.Content = grid;
        return window;
    }

    private static DataGrid CreateTypedGrid(Row row, bool writable)
    {
        Action<Row, string>? setter = writable ? (item, value) => item.Name = value : null;
        var property = new ClrPropertyInfo(nameof(Row.Name), value => ((Row)value).Name,
            writable ? (value, name) => ((Row)value).Name = (string)name! : null, typeof(string));
        return new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell,
            ItemsSource = new ObservableCollection<Row> { row },
            ColumnDefinitionsSource = new[]
            {
                new DataGridTextColumnDefinition
                {
                    Header = "Name",
                    Binding = DataGridBindingDefinition.Create<Row, string>(property, item => item.Name, setter)
                }
            }
        };
    }

    private sealed class SliderProbe : DataGridSliderColumn
    {
        public Control Render(object row) => GenerateElement(new DataGridCell(), row);
    }

    private sealed class TimePickerProbe : DataGridTimePickerColumn
    {
        public Control Render(object row) => GenerateElement(new DataGridCell(), row);
    }
    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;
        public double Number { get; set; }
        public TimeSpan? Time { get; set; }
    }

    private sealed class DescriptorProbeList : List<Row>, ITypedList
    {
        public int DescriptorQueries { get; private set; }

        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[]? listAccessors)
        {
            DescriptorQueries++;
            throw new InvalidOperationException("The typed path must not discover item descriptors.");
        }

        public string GetListName(PropertyDescriptor[]? listAccessors) => nameof(DescriptorProbeList);
    }

    private sealed class ReflectionSetting : IDisposable
    {
        private readonly bool _previous;

        public ReflectionSetting(bool enabled)
        {
            _previous = !AppContext.TryGetSwitch(SwitchName, out bool previous) || previous;
            AppContext.SetSwitch(SwitchName, enabled);
        }

        public void Dispose() => AppContext.SetSwitch(SwitchName, _previous);
    }
}