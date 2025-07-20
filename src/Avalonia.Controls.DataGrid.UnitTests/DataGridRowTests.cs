using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowTests
{
    [AvaloniaFact]
    public void IsSelected_Binding_Works_For_Initial_Rows()
    {
        var items = Enumerable.Range(0, 100).Select(x => new Model($"Item {x}")).ToList();
        items[2].IsSelected = true;
        
        var target = CreateTarget(items, [IsSelectedBinding()]);
        var rows = GetRows(target);

        Assert.Equal(0, GetFirstRealizedRowIndex(target));
        Assert.Equal(4, GetLastRealizedRowIndex(target));
        Assert.All(rows, x => Assert.Equal(x.Index == 2, x.IsSelected));
    }

    [AvaloniaFact]
    public void IsSelected_Binding_Works_For_Rows_Scrolled_Into_View()
    {
        var items = Enumerable.Range(0, 100).Select(x => new Model($"Item {x}")).ToList();
        items[10].IsSelected = true;

        var target = CreateTarget(items, [IsSelectedBinding()]);
        var rows = GetRows(target);

        Assert.Equal(0, GetFirstRealizedRowIndex(target));
        Assert.Equal(4, GetLastRealizedRowIndex(target));

        target.ScrollIntoView(items[10], target.Columns[0]);
        target.UpdateLayout();

        Assert.Equal(6, GetFirstRealizedRowIndex(target));
        Assert.Equal(10, GetLastRealizedRowIndex(target));

        Assert.All(rows, x => Assert.Equal(x.Index == 10, x.IsSelected));
    }

    [AvaloniaFact]
    public void Can_Toggle_IsSelected_Via_Binding()
    {
        var items = Enumerable.Range(0, 100).Select(x => new Model($"Item {x}")).ToList();
        items[2].IsSelected = true;

        var target = CreateTarget(items, [IsSelectedBinding()]);
        var rows = GetRows(target);

        Assert.Equal(0, GetFirstRealizedRowIndex(target));
        Assert.Equal(4, GetLastRealizedRowIndex(target));
        Assert.All(rows, x => Assert.Equal(x.Index == 2, x.IsSelected));

        items[2].IsSelected = false;

        Assert.All(rows, x => Assert.False(x.IsSelected));
    }

    [AvaloniaFact]
    public void Can_Toggle_IsSelected_Via_DataGrid()
    {
        var items = Enumerable.Range(0, 100).Select(x => new Model($"Item {x}")).ToList();
        items[2].IsSelected = true;

        var target = CreateTarget(items, [IsSelectedBinding()]);
        var rows = GetRows(target);

        Assert.Equal(0, GetFirstRealizedRowIndex(target));
        Assert.Equal(4, GetLastRealizedRowIndex(target));
        Assert.All(rows, x => Assert.Equal(x.Index == 2, x.IsSelected));

        target.SelectedItems.Remove(items[2]);

        Assert.All(rows, x => Assert.False(x.IsSelected));
        Assert.False(items[2].IsSelected);
    }

    [AvaloniaFact]
    public void Variable_Row_Heights_Virtualize_Correctly()
    {
        var items = Enumerable.Range(0, 100)
            .Select(x => new Model($"Item {x}", x % 2 == 0 ? 20 : 40)).ToList();

        var style = new Style(x => x.OfType<DataGridRow>())
        {
            Setters = { new Setter(DataGridRow.HeightProperty, new Binding("Height")) }
        };

        var target = CreateTarget(items, [style]);

        Assert.Equal(0, GetFirstRealizedRowIndex(target));

        target.ScrollIntoView(items[80], target.Columns[0]);
        target.UpdateLayout();

        Assert.Equal(80, GetLastRealizedRowIndex(target));
    }

    [AvaloniaFact]
    public void DataGridRow_Bounds_Match_DataGrid_When_Header_Present()
    {
        var items = Enumerable.Range(0, 100).Select(x => new Model($"Item {x}")).ToList();
        DataGrid target = CreateTarget(items, [WithHeader()]);
       
        // target.HeadersVisibility = DataGridHeadersVisibility.All;
        var rows = GetRows(target);

        Assert.All(rows, x => Assert.Equal(target.Bounds.Width, x.Bounds.Width));
    }

    private static DataGrid CreateTarget(
        IList items,
        IEnumerable<Style>? styles = null)
    {
        var root = new Window
        {
            Width = 200,
            Height = 100,
            Styles =
            {
                new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var target = new DataGrid 
        { 
            Columns =
            {
                new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") }
            },
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.All,
        };

        if (styles is not null)
        {
            foreach (var style in styles)
                target.Styles.Add(style);
        }
       
        root.Content = target;
        root.Show();
        return target;
    }

    private static int GetFirstRealizedRowIndex(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants().OfType<DataGridRow>().Select(x => x.Index).Min();
    }

    private static int GetLastRealizedRowIndex(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants().OfType<DataGridRow>().Select(x => x.Index).Max();
    }

    private static IReadOnlyList<DataGridRow> GetRows(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants().OfType<DataGridRow>().ToList();
    }

    private static Style IsSelectedBinding()
    {
        return new Style(x => x.OfType<DataGridRow>())
        {
            Setters = { new Setter(DataGridRow.IsSelectedProperty, new Binding("IsSelected", BindingMode.TwoWay)) }
        };
    }

    private static Style WithHeader()
    {
        return new Style(x => x.OfType<DataGridRow>())
        {
            Setters = { new Setter(DataGridRow.HeaderProperty, new Binding("Name", BindingMode.OneWay))} 
        };
    }

    private class Model : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private bool _isSelected;
        private string _name;
        private double _height;

        public Model(string name, double height = 30)
        {
            _name = name;
            _height = height;
        }

        public bool IsSelected 
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public double Height
        {
            get => _height;
            set => SetField(ref _height, value);
        }
    }
}
