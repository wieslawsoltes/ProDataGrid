using System;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Searching;

public class DataGridAccessorSearchAdapterTests
{
    [Fact]
    public void AccessorAdapter_Uses_ValueAccessor()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, string>(p => p.Name));

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));

        var result = Assert.Single(model.Results);
        Assert.Same(items[1], result.Item);
        Assert.Same(column, result.ColumnId);
    }

    [Fact]
    public void AccessorAdapter_Skips_PropertyPath_When_No_Accessor()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn { SortMemberPath = "Name" };

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Empty(model.Results);
    }

    [Fact]
    public void AccessorAdapter_Throws_When_Missing_Accessor_And_Option_Set()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var options = new DataGridFastPathOptions { ThrowOnMissingAccessor = true };

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column }, options);
        adapter.AttachView(view);

        Assert.Throws<InvalidOperationException>(() =>
            model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AccessorAdapter_Raises_Diagnostics_When_Missing_Accessor()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var options = new DataGridFastPathOptions();
        DataGridFastPathMissingAccessorEventArgs captured = null;
        options.MissingAccessor += (_, args) => captured = args;

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column }, options);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(captured);
        Assert.Equal(DataGridFastPathFeature.Searching, captured.Feature);
        Assert.Same(column, captured.Column);
    }

    [Fact]
    public void AccessorAdapter_Ignores_FillerColumns_In_StrictMode()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();
        var options = new DataGridFastPathOptions { ThrowOnMissingAccessor = true };

        var grid = new DataGrid();
        var filler = grid.ColumnsInternal.FillerColumn;

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { filler }, options);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Empty(model.Results);
    }

    [Fact]
    public void AccessorAdapter_Uses_Column_Order_For_Fallback_Index()
    {
        var items = new[]
        {
            new Person("Alpha")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var nonSearchable = new DataGridTextColumn();
        DataGridColumnSearch.SetIsSearchable(nonSearchable, false);
        nonSearchable.Index = -1;

        var searchable = new DataGridTextColumn();
        DataGridColumnSearch.SetIsSearchable(searchable, true);
        DataGridColumnMetadata.SetValueAccessor(searchable, new DataGridColumnValueAccessor<Person, string>(p => p.Name));
        searchable.Index = -1;

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { nonSearchable, searchable });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Alpha", comparison: StringComparison.OrdinalIgnoreCase));

        var result = Assert.Single(model.Results);
        Assert.Equal(1, result.ColumnIndex);
    }

    [Fact]
    public void AccessorAdapter_Uses_TextAccessor_When_Available()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        var accessor = new TextAccessor();
        DataGridColumnMetadata.SetValueAccessor(column, accessor);

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(model.Results);
        Assert.True(accessor.TextCalls > 0);
        Assert.Equal(0, accessor.ValueCalls);
    }

    private sealed class Person
    {
        public Person(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private sealed class TextAccessor : IDataGridColumnValueAccessor, IDataGridColumnTextAccessor
    {
        public int TextCalls { get; private set; }

        public int ValueCalls { get; private set; }

        public Type ItemType => typeof(Person);

        public Type ValueType => typeof(string);

        public bool CanWrite => false;

        public object GetValue(object item)
        {
            ValueCalls++;
            return item is Person person ? person.Name : null;
        }

        public void SetValue(object item, object value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetText(
            object item,
            Avalonia.Data.Converters.IValueConverter converter,
            object converterParameter,
            string stringFormat,
            System.Globalization.CultureInfo culture,
            IFormatProvider formatProvider,
            out string text)
        {
            TextCalls++;
            text = item is Person person ? person.Name : null;
            return item is Person;
        }
    }
}
