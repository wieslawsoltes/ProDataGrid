// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridReflectionDisabledTests;

/// <summary>
/// Verifies that data operations read the values of a column through its compiled binding
/// when <see cref="DataGridReflectionFeature"/> is off.
/// </summary>
public class CompiledBindingValueAccessorTests
{
    [AvaloniaFact]
    public void Accessor_Reads_And_Writes_The_Bound_Property()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) };
        var person = new Person("Alice", 30);

        var accessor = DataGridColumnMetadata.ResolveValueAccessor(column);

        Assert.NotNull(accessor);
        Assert.Equal(typeof(int), accessor.ValueType);
        Assert.True(accessor.CanWrite);
        Assert.Equal(30, accessor.GetValue(person));
        accessor.SetValue(person, 31);
        Assert.Equal(31, person.Age);
    }

    [AvaloniaFact]
    public void Accessor_Follows_A_Nested_Property_Path()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, string>(p => p.Address.City) };
        var person = new Person("Alice", 30) { Address = new Address("Oslo") };

        var accessor = DataGridColumnMetadata.ResolveValueAccessor(column);

        Assert.NotNull(accessor);
        Assert.Equal("Oslo", accessor.GetValue(person));
        accessor.SetValue(person, "Bergen");
        Assert.Equal("Bergen", person.Address.City);
        Assert.Null(accessor.GetValue(new Person("Bob", 40) { Address = null! }));
    }

    [AvaloniaFact]
    public void Accessor_Is_Reused_Until_The_Binding_Changes()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) };

        var first = DataGridColumnMetadata.ResolveValueAccessor(column);
        var second = DataGridColumnMetadata.ResolveValueAccessor(column);
        column.Binding = CompiledBinding.Create<Person, string>(p => p.Name);
        var third = DataGridColumnMetadata.ResolveValueAccessor(column);

        Assert.Same(first, second);
        Assert.NotSame(first, third);
        Assert.Equal("Alice", third.GetValue(new Person("Alice", 30)));
    }

    [AvaloniaFact]
    public void Explicit_Value_Accessor_Takes_Precedence()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) };
        var explicitAccessor = new DataGridColumnValueAccessor<Person, string>(p => p.Name);
        DataGridColumnMetadata.SetValueAccessor(column, explicitAccessor);

        Assert.Same(explicitAccessor, DataGridColumnMetadata.ResolveValueAccessor(column));
    }

    [AvaloniaFact]
    public void No_Accessor_When_The_Sort_Member_Path_Differs_From_The_Binding()
    {
        var column = new DataGridTextColumn
        {
            Binding = CompiledBinding.Create<Person, string>(p => p.Name),
            SortMemberPath = nameof(Person.Age)
        };

        Assert.Null(DataGridColumnMetadata.ResolveValueAccessor(column));
        Assert.NotNull(DataGridColumnMetadata.ResolveValueAccessor(column, nameof(Person.Name)));
    }

    [AvaloniaFact]
    public void No_Accessor_For_A_Binding_With_Its_Own_Source()
    {
        var column = new DataGridTextColumn
        {
            Binding = CompiledBinding.Create<Person, string>(p => p.Name, source: new Person("Source", 1))
        };

        Assert.Null(DataGridColumnMetadata.ResolveValueAccessor(column));
    }

    [AvaloniaFact]
    public void No_Accessor_For_A_Reflection_Binding()
    {
        var column = new DataGridTextColumn { Binding = new ReflectionBinding(nameof(Person.Name)) };

        Assert.Null(DataGridColumnMetadata.ResolveValueAccessor(column));
    }

    [AvaloniaFact]
    public void Sorting_Uses_The_Compiled_Binding()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) };
        var view = new DataGridCollectionView(CreatePeople());
        var model = new SortingModel();
        var adapter = new DataGridSortingAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.Toggle(new SortingDescriptor(column, ListSortDirection.Descending, column.GetSortPropertyName(), culture: view.Culture));

        Assert.Single(view.SortDescriptions);
        Assert.Equal(new[] { 41, 35, 29 }, view.Cast<Person>().Select(p => p.Age));
    }

    [AvaloniaFact]
    public void Filtering_Uses_The_Compiled_Binding()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) };
        var view = new DataGridCollectionView(CreatePeople());
        var model = new FilteringModel();
        var adapter = new DataGridFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.GreaterThan,
            propertyPath: column.GetSortPropertyName(),
            value: 30));

        Assert.Equal(new[] { "Carol", "Bob" }, view.Cast<Person>().Select(p => p.Name));
    }

    [AvaloniaFact]
    public void Searching_Uses_The_Compiled_Binding()
    {
        var people = CreatePeople();
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, string>(p => p.Name) };
        var view = new DataGridCollectionView(people);
        var model = new SearchModel();
        var adapter = new DataGridSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("bob", comparison: StringComparison.OrdinalIgnoreCase));

        var result = Assert.Single(model.Results);
        Assert.Same(people[2], result.Item);
    }

    [AvaloniaFact]
    public void Summary_Uses_The_Compiled_Binding()
    {
        var column = new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) };
        var description = new DataGridAggregateSummaryDescription { Aggregate = DataGridAggregateType.Sum };

        Assert.Equal(105m, description.Calculate(CreatePeople(), column));
    }

    [AvaloniaFact]
    public void Paste_Writes_Through_The_Compiled_Binding()
    {
        var people = CreatePeople();
        var grid = new DataGrid { ItemsSource = people };
        grid.Columns.Add(new DataGridTextColumn { Binding = CompiledBinding.Create<Person, int>(p => p.Age) });

        Assert.True(grid.TrySetCellValue(people[0], 0, "50"));
        Assert.Equal(50, people[0].Age);
    }

    [AvaloniaFact]
    public void ComboBox_Column_Sorts_Through_Its_Compiled_Selected_Value_Binding()
    {
        var column = new DataGridComboBoxColumn { SelectedValueBinding = CompiledBinding.Create<Person, int>(p => p.Age) };
        var view = new DataGridCollectionView(CreatePeople());
        var model = new SortingModel();
        var adapter = new DataGridSortingAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.Toggle(new SortingDescriptor(column, ListSortDirection.Ascending, column.GetSortPropertyName(), culture: view.Culture));

        Assert.Equal(new[] { 29, 35, 41 }, view.Cast<Person>().Select(p => p.Age));
    }

    private static List<Person> CreatePeople() =>
    [
        new("Carol", 41),
        new("Alice", 29),
        new("Bob", 35),
    ];

    public sealed class Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; set; }

        public int Age { get; set; }

        public Address Address { get; set; } = new(string.Empty);
    }

    public sealed class Address
    {
        public Address(string city)
        {
            City = city;
        }

        public string City { get; set; }
    }
}
