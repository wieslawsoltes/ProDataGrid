// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Filtering;

public class DataGridFilteringAdapterTests
{
    [Fact]
    public void Contains_IgnoreCase_Filters_Items()
    {
        var items = new[]
        {
            new Person("Alice", 8),
            new Person("Bob", 4),
            new Person("ALAN", 6)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Name",
            @operator: FilteringOperator.Contains,
            propertyPath: "Name",
            value: "al",
            stringComparison: StringComparison.OrdinalIgnoreCase));

        var names = view.Cast<Person>().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "Alice", "ALAN" }, names);
    }

    [Fact]
    public void Between_Filters_Range()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 5),
            new Person("C", 9),
            new Person("D", 12)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Score",
            @operator: FilteringOperator.Between,
            propertyPath: "Score",
            values: new object[] { 5, 10 }));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();

        Assert.Equal(new[] { 5, 9 }, scores);
    }

    [Fact]
    public void In_Filters_Matching_Items()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2),
            new Person("C", 3)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Score",
            @operator: FilteringOperator.In,
            propertyPath: "Score",
            values: new object[] { 2, 3 }));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();

        Assert.Equal(new[] { 2, 3 }, scores);
    }

    [Fact]
    public void Adapter_Reuses_Predicate_For_Equivalent_Descriptors()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Score",
            @operator: FilteringOperator.Equals,
            propertyPath: "Score",
            value: 2));

        var first = view.Filter;

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Score",
            @operator: FilteringOperator.Equals,
            propertyPath: "Score",
            value: 2));

        var second = view.Filter;

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void Custom_Predicate_Is_Used()
    {
        var items = new[]
        {
            new Person("Alpha", 1),
            new Person("Beta", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Custom",
            @operator: FilteringOperator.Custom,
            predicate: o => ((Person)o).Name.StartsWith("B", StringComparison.Ordinal)));

        var names = view.Cast<Person>().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "Beta" }, names);
    }

    [AvaloniaFact]
    public void Column_PredicateFactory_Takes_Precedence()
    {
        var items = new[]
        {
            new Person("alpha", 1),
            new Person("Beta", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        DataGridColumnFilter.SetPredicateFactory(column, descriptor =>
            o => ((Person)o).Name.Equals("alpha", StringComparison.Ordinal));

        var adapter = new DataGridFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            propertyPath: "Name",
            value: "Beta"));

        var names = view.Cast<Person>().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "alpha" }, names);
    }

    [AvaloniaFact]
    public void Column_ValueAccessor_Is_Used_When_Filtering()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Score));

        var adapter = new DataGridFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            propertyPath: "Missing",
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();

        Assert.Equal(new[] { 2 }, scores);
    }

    [AvaloniaFact]
    public void Column_FilterValueAccessor_Is_Used_When_Filtering()
    {
        var items = new[]
        {
            new Person("Alpha", 1),
            new Person("Beta", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnFilter.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Score));

        var adapter = new DataGridFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            propertyPath: "Name",
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();

        Assert.Equal(new[] { 2 }, scores);
    }

    [AvaloniaFact]
    public void Custom_Predicate_Is_Used_When_ValueAccessor_Is_Available()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Score));

        var adapter = new DataGridFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Custom,
            predicate: o => ((Person)o).Name == "B"));

        var names = view.Cast<Person>().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "B" }, names);
    }

    [AvaloniaFact]
    public void Column_Definition_Id_Uses_ValueAccessor()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var definition = new DataGridTextColumnDefinition
        {
            Header = "Score",
            Binding = DataGridBindingDefinition.Create<Person, int>(p => p.Score)
        };

        var grid = new DataGrid
        {
            ColumnDefinitionsSource = new[] { definition }
        };

        var adapter = new DataGridFilteringAdapter(model, () => grid.Columns);
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: definition,
            @operator: FilteringOperator.Equals,
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();

        Assert.Equal(new[] { 2 }, scores);
    }

    [Fact]
    public void OwnsViewFilter_False_Does_Not_Overwrite_External_Filter()
    {
        var items = new[]
        {
            new Person("alpha", 1),
            new Person("Beta", 2)
        };
        var view = new DataGridCollectionView(items);
        Func<object, bool> external = o => ((Person)o).Name.StartsWith("a", StringComparison.OrdinalIgnoreCase);
        view.Filter = external;

        var model = new FilteringModel { OwnsViewFilter = false };
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Name",
            @operator: FilteringOperator.Equals,
            propertyPath: "Name",
            value: "Beta"));

        Assert.Same(external, view.Filter);
        var names = view.Cast<Person>().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "alpha" }, names);
    }

    [Fact]
    public void Observer_Mode_Reconciles_External_Filter_Into_Descriptor()
    {
        var items = new[]
        {
            new Person("alpha", 1),
            new Person("Beta", 2)
        };

        var view = new DataGridCollectionView(items);
        var external = new Func<object, bool>(o => ((Person)o).Name.StartsWith("a", StringComparison.OrdinalIgnoreCase));
        view.Filter = external;

        var model = new FilteringModel { OwnsViewFilter = false };
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        Assert.Single(model.Descriptors);
        var descriptor = model.Descriptors[0];
        Assert.Equal(FilteringOperator.Custom, descriptor.Operator);
        Assert.Same(external, descriptor.Predicate);
    }

    [Fact]
    public void Observer_Mode_Clears_Descriptors_When_External_Filter_Clears()
    {
        var items = new[]
        {
            new Person("alpha", 1),
            new Person("Beta", 2)
        };

        var view = new DataGridCollectionView(items);
        view.Filter = o => ((Person)o).Name.StartsWith("a", StringComparison.OrdinalIgnoreCase);

        var model = new FilteringModel { OwnsViewFilter = false };
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        Assert.NotEmpty(model.Descriptors);

        view.Filter = null;

        Assert.Empty(model.Descriptors);
    }

    [Fact]
    public void Lifecycle_Callbacks_Are_Invoked()
    {
        var items = new[] { new Person("alpha", 1) };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var before = 0;
        var after = 0;
        var adapter = new DataGridFilteringAdapter(
            model,
            () => Array.Empty<DataGridColumn>(),
            () => before++,
            () => after++);

        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Name",
            @operator: FilteringOperator.Equals,
            propertyPath: "Name",
            value: "alpha"));

        Assert.True(before >= 1);
        Assert.True(after >= 1);
        Assert.True(before >= after);
    }

    [Fact]
    public void Culture_Compare_Uses_Descriptor_Culture()
    {
        var items = new[]
        {
            new CultureItem(new CultureString("i")),
            new CultureItem(new CultureString("a"))
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => Array.Empty<DataGridColumn>());
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Value",
            @operator: FilteringOperator.GreaterThan,
            propertyPath: "Value",
            value: new CultureString("I"),
            culture: CultureInfo.GetCultureInfo("tr-TR")));

        var values = view.Cast<CultureItem>().Select(x => x.Value.Raw).ToArray();

        Assert.Equal(new[] { "i" }, values);
    }

    [AvaloniaFact]
    public void FilteringModel_With_OwnsViewFilter_False_Does_Not_Clear_View_Filter()
    {
        var items = new ObservableCollection<Person>
        {
            new Person("Alpha", 1),
            new Person("Bravo", 5)
        };

        var view = new DataGridCollectionView(items);
        Func<object, bool> externalFilter = item => ((Person)item).Score >= 0;
        view.Filter = externalFilter;

        var root = new Window
        {
            Width = 250,
            Height = 150
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            FilteringModel = new FilteringModel { OwnsViewFilter = false },
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Person.Name))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        try
        {
            Assert.False(grid.FilteringModel.OwnsViewFilter);
            Assert.Same(externalFilter, view.Filter);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Selection_Preserved_When_Filtering_Model_Changes()
    {
        var items = new ObservableCollection<Person>
        {
            new Person("Alpha", 1),
            new Person("Bravo", 5),
            new Person("Charlie", 9)
        };

        var filteringModel = new FilteringModel();
        var grid = CreateGrid(items, filteringModel);
        grid.UpdateLayout();

        var selected = items[1];
        grid.SelectedItem = selected;
        grid.UpdateLayout();

        Assert.Same(selected, grid.SelectedItem);

        filteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: "Score",
            @operator: FilteringOperator.GreaterThanOrEqual,
            propertyPath: nameof(Person.Score),
            value: 5));

        grid.UpdateLayout();

        var view = Assert.IsType<DataGridCollectionView>(grid.ItemsSource);
        Assert.Equal(new[] { selected, items[2] }, view.Cast<Person>().ToArray());
        Assert.Same(selected, grid.SelectedItem);
        Assert.Contains(selected, grid.SelectedItems.Cast<object>());
        Assert.Equal(0, grid.SelectedIndex);
        Assert.Equal(0, grid.Selection.SelectedIndex);
    }

    private static DataGrid CreateGrid(IEnumerable<Person> items, IFilteringModel filteringModel)
    {
        var root = new Window
        {
            Width = 250,
            Height = 150,
        };

        root.SetThemeStyles();

        var view = new DataGridCollectionView(items);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false
        };

        grid.FilteringModel = filteringModel;
        grid.ItemsSource = view;

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Person.Name)),
            SortMemberPath = nameof(Person.Name)
        });

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Score",
            Binding = new Binding(nameof(Person.Score)),
            SortMemberPath = nameof(Person.Score)
        });

        root.Content = grid;
        root.Show();

        return grid;
    }

    private sealed class Person
    {
        public Person(string name, int score)
        {
            Name = name;
            Score = score;
        }

        public string Name { get; }

        public int Score { get; }
    }

    private sealed class CultureItem
    {
        public CultureItem(CultureString value)
        {
            Value = value;
        }

        public CultureString Value { get; }
    }

    private sealed class CultureString : IFormattable
    {
        public CultureString(string raw)
        {
            Raw = raw;
        }

        public string Raw { get; }

        public override string ToString() => Raw;

        public string ToString(string? format, IFormatProvider? provider)
        {
            var culture = provider as CultureInfo ?? CultureInfo.InvariantCulture;
            return culture.TextInfo.ToUpper(Raw);
        }
    }
}
