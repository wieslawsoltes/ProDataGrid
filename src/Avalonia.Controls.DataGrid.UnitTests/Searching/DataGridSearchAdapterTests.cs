// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Searching;

public class DataGridSearchAdapterTests
{
    [Fact]
    public void Contains_Finds_Matches_Across_All_Columns()
    {
        var items = new[]
        {
            new Person("Alice", "North"),
            new Person("Bob", "Alpha"),
            new Person("Cara", "Gamma")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        var regionColumn = new DataGridTextColumn { Header = "Region", SortMemberPath = "Region", Binding = new Binding("Region") };
        grid.Columns.Add(nameColumn);
        grid.Columns.Add(regionColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "al",
            scope: SearchScope.AllColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, nameColumn) && r.RowIndex == 0);
        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, regionColumn) && r.RowIndex == 1);
    }

    [Fact]
    public void ExplicitColumns_Limits_Search_To_Selected_Columns()
    {
        var items = new[]
        {
            new Person("Alice", "North"),
            new Person("Bob", "Alpha")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        var regionColumn = new DataGridTextColumn { Header = "Region", SortMemberPath = "Region", Binding = new Binding("Region") };
        grid.Columns.Add(nameColumn);
        grid.Columns.Add(regionColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "al",
            scope: SearchScope.ExplicitColumns,
            columnIds: new object[] { "Name" },
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, nameColumn));
        Assert.DoesNotContain(model.Results, r => ReferenceEquals(r.ColumnId, regionColumn));
    }

    [Fact]
    public void SearchMemberPath_Overrides_Binding_Path()
    {
        var items = new[]
        {
            new PersonWithAlias("Alice", "Specter")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            SortMemberPath = "Name",
            Binding = new Binding("Name")
        };
        DataGridColumnSearch.SetSearchMemberPath(nameColumn, "Alias");
        grid.Columns.Add(nameColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Specter",
            scope: SearchScope.AllColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, nameColumn));
    }

    [Fact]
    public void TextProvider_Takes_Precedence_Over_Binding()
    {
        var items = new[]
        {
            new Person("Alice", "North")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            SortMemberPath = "Name",
            Binding = new Binding("Name")
        };
        DataGridColumnSearch.SetTextProvider(nameColumn, _ => "Magic");
        grid.Columns.Add(nameColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Magic",
            scope: SearchScope.AllColumns,
            comparison: StringComparison.Ordinal));

        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, nameColumn));
    }

    [Fact]
    public void VisibleColumns_Scope_Excludes_Hidden_Columns()
    {
        var items = new[]
        {
            new Person("Alice", "Hidden")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        var regionColumn = new DataGridTextColumn { Header = "Region", SortMemberPath = "Region", Binding = new Binding("Region"), IsVisible = false };
        grid.Columns.Add(nameColumn);
        grid.Columns.Add(regionColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Hidden",
            scope: SearchScope.VisibleColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(model.Results, r => ReferenceEquals(r.ColumnId, regionColumn));
        Assert.DoesNotContain(model.Results, r => ReferenceEquals(r.ColumnId, nameColumn));
    }

    [Fact]
    public void Wildcard_Mode_Finds_Matches()
    {
        var items = new[]
        {
            new Person("Alpha", "North")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        grid.Columns.Add(nameColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Al*",
            matchMode: SearchMatchMode.Wildcard,
            scope: SearchScope.AllColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, nameColumn));
    }

    [Fact]
    public void Regex_Invalid_Pattern_Does_Not_Throw()
    {
        var items = new[]
        {
            new Person("Alpha", "North")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        grid.Columns.Add(nameColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "[",
            matchMode: SearchMatchMode.Regex,
            scope: SearchScope.AllColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Empty(model.Results);
    }

    [Fact]
    public void TermMode_All_Requires_All_Terms()
    {
        var items = new[]
        {
            new Person("Alpha Beta", "North"),
            new Person("Alpha Gamma", "South")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        grid.Columns.Add(nameColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Alpha Beta",
            termMode: SearchTermCombineMode.All,
            scope: SearchScope.AllColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Contains(model.Results, r => r.RowIndex == 0);
        Assert.DoesNotContain(model.Results, r => r.RowIndex == 1);
    }

    [Fact]
    public void Converter_With_StringFormat_Uses_Formatted_Text()
    {
        var items = new[]
        {
            new PersonWithScore(5)
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var converter = new MultiplyConverter();
        var scoreColumn = new DataGridTextColumn
        {
            Header = "Score",
            SortMemberPath = "Score",
            Binding = new Binding("Score")
            {
                Converter = converter,
                StringFormat = "Score: {0}"
            }
        };
        grid.Columns.Add(scoreColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Score: 10",
            scope: SearchScope.AllColumns,
            comparison: StringComparison.Ordinal));

        Assert.Contains(model.Results, r => ReferenceEquals(r.ColumnId, scoreColumn));
    }

    [Fact]
    public void Item_Property_Changes_Recompute_Results()
    {
        var items = new[]
        {
            new NotifyPerson("Alpha")
        };

        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var nameColumn = new DataGridTextColumn { Header = "Name", SortMemberPath = "Name", Binding = new Binding("Name") };
        grid.Columns.Add(nameColumn);

        var adapter = new DataGridSearchAdapter(model, () => grid.ColumnDefinitions);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "Alpha",
            scope: SearchScope.AllColumns,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Single(model.Results);

        items[0].Name = "Beta";

        Assert.Empty(model.Results);
    }

    private sealed class Person
    {
        public Person(string name, string region)
        {
            Name = name;
            Region = region;
        }

        public string Name { get; }
        public string Region { get; }
    }

    private sealed class PersonWithAlias
    {
        public PersonWithAlias(string name, string alias)
        {
            Name = name;
            Alias = alias;
        }

        public string Name { get; }
        public string Alias { get; }
    }

    private sealed class PersonWithScore
    {
        public PersonWithScore(int score)
        {
            Score = score;
        }

        public int Score { get; }
    }

    private sealed class MultiplyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is int number ? number * 2 : 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NotifyPerson : INotifyPropertyChanged
    {
        private string _name;

        public NotifyPerson(string name)
        {
            _name = name;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
