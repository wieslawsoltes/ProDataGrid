// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridAotSmoke;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

// Exercises a DataGrid declared in XAML with compiled bindings, the way a trimmed or Native AOT
// application uses it without generated schemas. Each failing scenario returns its own exit code.
AppBuilder.Configure<SmokeApp>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
    .SetupWithoutStarting();

var engineering = new Department("Engineering");
var sales = new Department("Sales");

List<Person> CreatePeople() =>
[
    new("Carol", 41, sales),
    new("Alice", 29, engineering),
    new("Bob", 35, sales),
    new("Dave", 23, engineering),
];

(PeopleGridView View, DataGridCollectionView Collection, List<Person> People) Show()
{
    var people = CreatePeople();
    var collection = new DataGridCollectionView(people);
    var view = new PeopleGridView();
    view.PeopleGrid.ItemsSource = collection;
    view.PeopleGrid.Columns.OfType<DataGridComboBoxColumn>().Single().ItemsSource = new[] { engineering, sales };

    var window = new Window { Width = 800, Height = 600, Content = view };
    window.Show();
    Pump(window);
    return (view, collection, people);
}

void Pump(TopLevel root)
{
    Dispatcher.UIThread.RunJobs();
    root.UpdateLayout();
    Dispatcher.UIThread.RunJobs();
}

string[] ColumnTexts(DataGrid grid, int columnIndex) => grid.GetVisualDescendants()
    .OfType<DataGridRow>()
    .Where(row => row.DataContext is Person)
    .OrderBy(row => row.Index)
    .Select(row => row.GetVisualDescendants().OfType<DataGridCell>().ElementAtOrDefault(columnIndex)?.Content)
    .Select(content => content is TextBlock textBlock ? textBlock.Text ?? string.Empty : string.Empty)
    .ToArray();

int Fail(int code, string scenario, string detail)
{
    Console.Error.WriteLine($"{scenario} failed: {detail}");
    return code;
}

// Cells show the values of compiled bindings.
{
    var (view, _, _) = Show();
    var names = ColumnTexts(view.PeopleGrid, 0);
    if (!names.SequenceEqual(new[] { "Carol", "Alice", "Bob", "Dave" }))
    {
        return Fail(1, "Rendering", string.Join(",", names));
    }
}

// A header sort orders the rows by the compiled binding of the column.
{
    var (view, collection, _) = Show();
    view.PeopleGrid.Columns[1].Sort(ListSortDirection.Descending);
    Pump(TopLevel.GetTopLevel(view)!);
    var ages = collection.Cast<Person>().Select(person => person.Age).ToArray();
    if (!ages.SequenceEqual(new[] { 41, 35, 29, 23 }))
    {
        return Fail(2, "Header sorting", string.Join(",", ages));
    }
}

// A column filter reads the values of the compiled binding.
{
    var (view, collection, _) = Show();
    var ageColumn = view.PeopleGrid.Columns[1];
    view.PeopleGrid.FilteringModel.SetOrUpdate(new FilteringDescriptor(
        columnId: ageColumn,
        @operator: FilteringOperator.GreaterThan,
        propertyPath: nameof(Person.Age),
        value: 30));
    var names = collection.Cast<Person>().Select(person => person.Name).ToArray();
    if (!names.SequenceEqual(new[] { "Carol", "Bob" }))
    {
        return Fail(3, "Filtering", string.Join(",", names));
    }
}

// A search reads the values of the compiled binding.
{
    var (view, _, people) = Show();
    view.PeopleGrid.SearchModel.SetOrUpdate(new SearchDescriptor("bob", comparison: StringComparison.OrdinalIgnoreCase));
    var results = view.PeopleGrid.SearchModel.Results;
    if (results.Count != 1 || !ReferenceEquals(results[0].Item, people[2]))
    {
        return Fail(4, "Searching", $"{results.Count} results");
    }
}

// An edited cell commits through the compiled binding.
{
    var (view, _, people) = Show();
    var grid = view.PeopleGrid;
    grid.SelectedIndex = 0;
    grid.CurrentColumn = grid.Columns[0];
    Pump(TopLevel.GetTopLevel(view)!);
    var began = grid.BeginEdit();
    Pump(TopLevel.GetTopLevel(view)!);
    var editor = grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
    if (!began || editor == null)
    {
        return Fail(5, "Editing", $"began={began}, editor={editor != null}");
    }

    editor.Text = "Zed";
    var committed = grid.CommitEdit();
    if (!committed || people[0].Name != "Zed")
    {
        return Fail(5, "Editing", $"committed={committed}, name={people[0].Name}");
    }
}

// New rows come from the collection view factory.
{
    var collection = new DataGridCollectionView(CreatePeople())
    {
        NewItemFactory = () => new Person("New", 0, engineering)
    };
    var added = collection.CanAddNew ? collection.AddNew() : null;
    collection.CommitNew();
    if (added is not Person || collection.Count != 5)
    {
        return Fail(6, "Adding rows", $"canAddNew={collection.CanAddNew}, count={collection.Count}");
    }
}

// Groups come from a typed group description.
{
    var (view, collection, _) = Show();
    collection.GroupDescriptions.Add(
        new DataGridGeneratedGroupDescription<Person, string>("department", person => person.Department.Name));
    Pump(TopLevel.GetTopLevel(view)!);
    var groups = collection.Groups?.Cast<DataGridCollectionViewGroup>().Select(group => $"{group.Key}:{group.ItemCount}").ToArray() ?? [];
    var headers = view.PeopleGrid.GetVisualDescendants().OfType<DataGridRowGroupHeader>().Count();
    if (!groups.SequenceEqual(new[] { "Sales:2", "Engineering:2" }) || headers != 2)
    {
        return Fail(7, "Grouping", $"{string.Join(",", groups)}, headers={headers}");
    }
}

// A combo box column shows its items through a compiled display member binding.
{
    var (view, _, _) = Show();
    var comboBox = view.PeopleGrid.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
    var shown = comboBox?.GetVisualDescendants().OfType<TextBlock>().Any(textBlock => textBlock.Text == "Sales") == true;
    if (comboBox?.DisplayMemberBinding == null || !shown)
    {
        return Fail(8, "Combo box display member", $"binding={comboBox?.DisplayMemberBinding != null}, shown={shown}");
    }
}

Console.WriteLine("DataGrid NativeAOT smoke scenarios passed.");
return 0;
