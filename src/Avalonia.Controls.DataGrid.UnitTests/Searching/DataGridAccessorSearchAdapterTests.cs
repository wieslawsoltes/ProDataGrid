using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Searching;

public class DataGridAccessorSearchAdapterTests
{
    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void AccessorAdapter_Normalizes_Whitespace_And_Diacritics()
    {
        var items = new[]
        {
            new Person("Creme Brulee"),
            new Person("Crème   Brûlée")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, string>(p => p.Name));

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "creme brulee",
            comparison: StringComparison.OrdinalIgnoreCase,
            normalizeWhitespace: true,
            ignoreDiacritics: true));

        Assert.Equal(2, model.Results.Count);
    }

    [AvaloniaFact]
    public void AccessorAdapter_WholeWord_Wildcard_Uses_Word_Boundaries()
    {
        var items = new[]
        {
            new Person("alpha"),
            new Person("alphabet"),
            new Person("beta alpha")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, string>(p => p.Name));

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "alpha",
            matchMode: SearchMatchMode.Wildcard,
            wholeWord: true,
            comparison: StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, model.Results.Count);
        Assert.DoesNotContain(model.Results, r => ReferenceEquals(r.Item, items[1]));
    }

    [AvaloniaFact]
    public void AccessorAdapter_Wildcard_Default_Comparison_Is_CaseInsensitive()
    {
        var items = new[]
        {
            new Person("Alpha"),
            new Person("bravo")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, string>(p => p.Name));

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor(
            "ALPHA",
            matchMode: SearchMatchMode.Wildcard));

        var result = Assert.Single(model.Results);
        Assert.Same(items[0], result.Item);
    }

    [AvaloniaFact]
    public void AccessorAdapter_Wildcard_Default_Comparison_Is_CultureInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");

            var items = new[]
            {
                new Person("Istanbul")
            };
            var view = new DataGridCollectionView(items);
            var model = new SearchModel();

            var column = new DataGridTextColumn();
            DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, string>(p => p.Name));

            var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column });
            adapter.AttachView(view);

            model.SetOrUpdate(new SearchDescriptor(
                "i*",
                matchMode: SearchMatchMode.Wildcard));

            var result = Assert.Single(model.Results);
            Assert.Same(items[0], result.Item);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [AvaloniaFact]
    public void AccessorAdapter_HighPerformanceSearch_Updates_Incrementally_For_Add_Remove()
    {
        var items = new ObservableCollection<Person>
        {
            new("Alpha"),
            new("Beta")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, string>(p => p.Name));

        var options = new DataGridFastPathOptions
        {
            EnableHighPerformanceSearching = true,
            HighPerformanceSearchTrackItemChanges = false
        };
        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column }, options);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));
        Assert.Single(model.Results);

        items.Insert(0, new Person("Gamma"));
        items.Add(new Person("Beta"));

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, model.Results.Count);
        Assert.Equal(new[] { 2, 3 }, model.Results.Select(r => r.RowIndex).ToArray());

        items.RemoveAt(0);

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new[] { 1, 2 }, model.Results.Select(r => r.RowIndex).ToArray());
    }

    [AvaloniaFact]
    public void AccessorAdapter_HighPerformanceSearch_Tracks_Item_Changes_When_Enabled()
    {
        var items = new ObservableCollection<MutablePerson>
        {
            new("Alpha")
        };
        var view = new DataGridCollectionView(items);
        var model = new SearchModel();

        var column = new DataGridTextColumn();
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<MutablePerson, string>(p => p.Name));

        var options = new DataGridFastPathOptions
        {
            EnableHighPerformanceSearching = true,
            HighPerformanceSearchTrackItemChanges = true
        };

        var adapter = new DataGridAccessorSearchAdapter(model, () => new[] { column }, options);
        adapter.AttachView(view);

        model.SetOrUpdate(new SearchDescriptor("Beta", comparison: StringComparison.OrdinalIgnoreCase));
        Assert.Empty(model.Results);

        items[0].Name = "Beta";

        Dispatcher.UIThread.RunJobs();

        var result = Assert.Single(model.Results);
        Assert.Same(items[0], result.Item);
    }

    private sealed class Person
    {
        public Person(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private sealed class MutablePerson : INotifyPropertyChanged
    {
        private string _name;

        public MutablePerson(string name)
        {
            _name = name;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (string.Equals(_name, value, StringComparison.Ordinal))
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
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
