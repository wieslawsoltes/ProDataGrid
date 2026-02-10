using System;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Filtering;

public class DataGridAccessorFilteringAdapterTests
{
    [AvaloniaFact]
    public void AccessorAdapter_Uses_ValueAccessor()
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

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 2 }, scores);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Uses_FilterValueAccessor()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnFilter.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Score));

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 2 }, scores);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Reuses_Predicate_For_Equivalent_Descriptors()
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

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2));

        var first = view.Filter;

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2));

        var second = view.Filter;

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Skips_PropertyPath_When_No_Accessor()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn { SortMemberPath = "Score" };

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: "Score",
            @operator: FilteringOperator.Equals,
            propertyPath: "Score",
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 1, 2 }, scores);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Throws_When_Missing_Accessor_And_Option_Set()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn { SortMemberPath = "Score" };
        var options = new DataGridFastPathOptions { ThrowOnMissingAccessor = true };

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column }, options);
        adapter.AttachView(view);

        Assert.Throws<InvalidOperationException>(() =>
            model.SetOrUpdate(new FilteringDescriptor(
                columnId: "Score",
                @operator: FilteringOperator.Equals,
                propertyPath: "Score",
                value: 2)));
    }

    [AvaloniaFact]
    public void AccessorAdapter_Raises_Diagnostics_When_Missing_Accessor()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn { SortMemberPath = "Score" };
        var options = new DataGridFastPathOptions();
        DataGridFastPathMissingAccessorEventArgs captured = null;
        options.MissingAccessor += (_, args) => captured = args;

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column }, options);
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2));

        Assert.NotNull(captured);
        Assert.Equal(DataGridFastPathFeature.Filtering, captured.Feature);
        Assert.Same(column, captured.Column);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Uses_FilterAccessor_When_Available()
    {
        var items = new[]
        {
            new Person("A", 1),
            new Person("B", 2)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        var accessor = new FilterAccessor();
        DataGridColumnMetadata.SetValueAccessor(column, accessor);

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new FilteringDescriptor(
            columnId: column,
            @operator: FilteringOperator.Equals,
            value: 2));

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 2 }, scores);
        Assert.True(accessor.MatchCalls > 0);
        Assert.Equal(0, accessor.ValueCalls);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Between_With_Int32_Bounds_And_Int64_Value_Does_Not_Throw()
    {
        var items = new[]
        {
            new LongPerson("A", 1L),
            new LongPerson("B", 5L),
            new LongPerson("C", 9L),
            new LongPerson("D", 12L)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<LongPerson, long>(p => p.Score));

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        var exception = Record.Exception(() =>
            model.SetOrUpdate(new FilteringDescriptor(
                columnId: column,
                @operator: FilteringOperator.Between,
                values: new object[] { 5, 10 })));

        Assert.Null(exception);

        var scores = view.Cast<LongPerson>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 5L, 9L }, scores);
    }

    [AvaloniaFact]
    public void AccessorAdapter_GreaterThan_With_Int32_Value_And_Int64_Source_Does_Not_Throw()
    {
        var items = new[]
        {
            new LongPerson("A", 1L),
            new LongPerson("B", 5L),
            new LongPerson("C", 9L),
            new LongPerson("D", 12L)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<LongPerson, long>(p => p.Score));

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        var exception = Record.Exception(() =>
            model.SetOrUpdate(new FilteringDescriptor(
                columnId: column,
                @operator: FilteringOperator.GreaterThan,
                value: 5)));

        Assert.Null(exception);

        var scores = view.Cast<LongPerson>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 9L, 12L }, scores);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Between_With_Int64_Bounds_And_Int32_Value_Does_Not_Throw()
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

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Score));

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        var exception = Record.Exception(() =>
            model.SetOrUpdate(new FilteringDescriptor(
                columnId: column,
                @operator: FilteringOperator.Between,
                values: new object[] { 5L, 10L })));

        Assert.Null(exception);

        var scores = view.Cast<Person>().Select(p => p.Score).ToArray();
        Assert.Equal(new[] { 5, 9 }, scores);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Between_With_NonConvertible_Bounds_Does_Not_Throw_And_Matches_Nothing()
    {
        var items = new[]
        {
            new LongPerson("A", 1L),
            new LongPerson("B", 5L),
            new LongPerson("C", 9L)
        };
        var view = new DataGridCollectionView(items);
        var model = new FilteringModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<LongPerson, long>(p => p.Score));

        var adapter = new DataGridAccessorFilteringAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        var exception = Record.Exception(() =>
            model.SetOrUpdate(new FilteringDescriptor(
                columnId: column,
                @operator: FilteringOperator.Between,
                values: new object[] { "x", "y" })));

        Assert.Null(exception);
        Assert.Empty(view.Cast<LongPerson>());
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

    private sealed class FilterAccessor : IDataGridColumnValueAccessor, IDataGridColumnFilterAccessor
    {
        public int MatchCalls { get; private set; }

        public int ValueCalls { get; private set; }

        public Type ItemType => typeof(Person);

        public Type ValueType => typeof(int);

        public bool CanWrite => false;

        public object GetValue(object item)
        {
            ValueCalls++;
            return item is Person person ? person.Score : null;
        }

        public void SetValue(object item, object value)
        {
            throw new InvalidOperationException();
        }

        public bool TryMatch(object item, FilteringDescriptor descriptor, out bool match)
        {
            MatchCalls++;
            match = item is Person person && descriptor.Value is int expected && person.Score == expected;
            return true;
        }
    }

    private sealed class LongPerson
    {
        public LongPerson(string name, long score)
        {
            Name = name;
            Score = score;
        }

        public string Name { get; }

        public long Score { get; }
    }
}
