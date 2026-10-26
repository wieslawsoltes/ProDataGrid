// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridReflectionDisabledTests;

/// <summary>
/// Verifies the grid's behavior when <see cref="DataGridReflectionFeature"/> is off, as it is in trimmed
/// and Native AOT applications. The switch comes from this project's runtime configuration.
/// </summary>
public class ReflectionDisabledTests
{
    [Fact]
    public void Feature_Switch_Is_Disabled_By_Runtime_Configuration()
    {
        Assert.True(AppContext.TryGetSwitch(DataGridReflectionFeature.SwitchName, out var isEnabled));
        Assert.False(isEnabled);
        Assert.False(DataGridReflectionFeature.IsEnabled);
    }

    [AvaloniaFact]
    public void DataGrid_Does_Not_Register_TypeDescriptor_Binding_Plugin()
    {
        _ = new DataGrid();

        // BindingPlugins is internal to Avalonia, so read the accessor list through reflection.
        var accessors = (System.Collections.IEnumerable)typeof(AvaloniaObject).Assembly
            .GetType("Avalonia.Data.Core.Plugins.BindingPlugins")!
            .GetProperty("PropertyAccessors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(null)!;

        Assert.DoesNotContain(
            accessors.Cast<object>(),
            plugin => plugin.GetType().DeclaringType?.Name == "DataGridTypeDescriptorPlugin");
    }

    [AvaloniaFact]
    public void AutoGenerateColumns_Does_Not_Generate_Columns()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = true,
            ItemsSource = new List<Person> { new("Alice", 30) }
        };

        typeof(DataGrid)
            .GetField("_measured", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grid, true);
        typeof(DataGrid)
            .GetMethod("AutoGenerateColumnsPrivate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(grid, null);

        Assert.Empty(grid.Columns);
    }

    [Fact]
    public void CollectionView_Without_Factory_Cannot_Add_New()
    {
        var view = new DataGridCollectionView(new List<Person>());

        Assert.False(view.CanAddNew);
    }

    [Fact]
    public void CollectionView_Uses_NewItemFactory_To_Add_New()
    {
        var items = new List<Person>();
        var view = new DataGridCollectionView(items)
        {
            NewItemFactory = () => new Person("New", 1)
        };

        Assert.True(view.CanAddNew);
        var added = Assert.IsType<Person>(view.AddNew());
        view.CommitNew();

        Assert.Equal("New", added.Name);
        Assert.Same(added, Assert.Single(items));
    }

    [Fact]
    public void ReflectionPropertyPath_Resolves_Nothing()
    {
        var person = new Person("Alice", 30);

        Assert.Null(ReflectionPropertyPath.GetValue(person, nameof(Person.Name)));
        Assert.Null(ReflectionPropertyPath.GetPropertyType(typeof(Person), nameof(Person.Name)));
        Assert.Null(ReflectionPropertyPath.GetDisplayName(typeof(Person), nameof(Person.Name)));
        Assert.False(ReflectionPropertyPath.TrySetValue(person, nameof(Person.Name), "Bob"));
        Assert.Equal("Alice", person.Name);
    }

    [Fact]
    public void ValueConverter_Converts_Primitives_Without_TypeConverters()
    {
        var converter = DataGridValueConverter.Instance;

        Assert.Equal(12, converter.Convert("12", typeof(int), null, CultureInfo.InvariantCulture));
        Assert.Equal("12.5", converter.Convert(12.5m, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(DayOfWeek.Friday, converter.Convert("friday", typeof(DayOfWeek), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.ConvertBack(string.Empty, typeof(int?), null, CultureInfo.InvariantCulture));
        Assert.IsType<BindingNotification>(converter.Convert("abc", typeof(int), null, CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public void Sorting_By_Path_Without_Accessor_Adds_No_Sort_Description()
    {
        var column = new DataGridTextColumn { SortMemberPath = nameof(Person.Name) };
        var view = new DataGridCollectionView(new List<Person> { new("B", 2), new("A", 1) });
        var model = new SortingModel();
        var adapter = new DataGridSortingAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.Toggle(new SortingDescriptor(column, ListSortDirection.Ascending, column.GetSortPropertyName(), culture: view.Culture));

        Assert.Empty(view.SortDescriptions);
    }

    [Fact]
    public void Hierarchical_Sibling_Comparer_Ignores_Path_Without_Comparer()
    {
        var comparer = HierarchicalSiblingComparerBuilder.Build(
            new[] { new SortingDescriptor("Name", ListSortDirection.Ascending, nameof(Person.Name)) },
            defaultComparer: null);

        Assert.NotNull(comparer);
        Assert.Equal(0, comparer!.Compare(new Person("B", 2), new Person("A", 1)));
    }

    [Fact]
    public void Hierarchical_Sibling_Comparer_Uses_Descriptor_Comparer()
    {
        var byAge = Comparer<object>.Create((x, y) => ((Person)x!).Age.CompareTo(((Person)y!).Age));
        var comparer = HierarchicalSiblingComparerBuilder.Build(
            new[] { new SortingDescriptor("Age", ListSortDirection.Ascending, nameof(Person.Age), byAge) },
            defaultComparer: null);

        Assert.True(comparer!.Compare(new Person("B", 2), new Person("A", 1)) > 0);
    }

    [Fact]
    public void Hierarchical_ChildrenPropertyPath_Reports_Load_Error_With_Guidance()
    {
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenPropertyPath = nameof(Node.Children)
        });
        model.SetRoot(new Node());

        model.Expand(model.Root!);

        var error = Assert.IsType<InvalidOperationException>(model.Root!.LoadError);
        Assert.Contains("ProDataGridIsReflectionEnabledByDefault", error.Message);
    }

    [Fact]
    public void Hierarchical_ChildrenSelector_Works()
    {
        var root = new Node();
        root.Children.Add(new Node());
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Node)item).Children
        });
        model.SetRoot(root);

        model.Expand(model.Root!);

        Assert.Equal(2, model.Count);
    }

    public sealed class Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; set; }

        public int Age { get; set; }
    }

    public sealed class Node
    {
        public ObservableCollection<Node> Children { get; } = new();
    }
}
