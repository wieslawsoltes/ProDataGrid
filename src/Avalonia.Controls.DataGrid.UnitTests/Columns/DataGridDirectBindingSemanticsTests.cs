// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public sealed class DataGridDirectBindingSemanticsTests
{
    [AvaloniaFact]
    public void CustomConverter_UsesAvaloniaBindingFallback_WithParameterAndCulture()
    {
        var item = new Item("alpha");
        var converter = new RecordingConverter();
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(Item.Name))
            {
                Converter = converter,
                ConverterParameter = "prefix",
                ConverterCulture = culture,
            },
            UseDirectTextCell = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<Item, string>(static value => value.Name));
        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;

        Assert.Null(column.GenerateDisplay(cell, item));
        var window = new Window { Width = 180, Height = 60, Content = cell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.False(cell.UsesValueAccessor);
            Assert.Equal("prefix:fr-FR:alpha", cell.Value);
            Assert.Equal(typeof(string), converter.LastTargetType);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FallbackValue_UsesAvaloniaBindingPath_InsteadOfTypedAccessor()
    {
        var item = new Item("typed accessor must not bypass fallback");
        var column = new TestTextColumn
        {
            Binding = new Binding("MissingProperty")
            {
                FallbackValue = "binding fallback",
            },
            UseDirectTextCell = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<Item, string>(static value => value.Name));
        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;

        Assert.Null(column.GenerateDisplay(cell, item));
        var window = new Window { Width = 180, Height = 60, Content = cell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(cell.UsesValueAccessor);
            Assert.Equal("binding fallback", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TargetNullValue_UsesAvaloniaBindingPath_InsteadOfTypedAccessor()
    {
        var item = new NullableItem(null);
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NullableItem.Name))
            {
                TargetNullValue = "null replacement",
            },
            UseDirectTextCell = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NullableItem, string?>(static value => value.Name));
        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;

        Assert.Null(column.GenerateDisplay(cell, item));
        var window = new Window { Width = 180, Height = 60, Content = cell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(cell.UsesValueAccessor);
            Assert.Equal("null replacement", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void OneTime_Delayed_AndNonLocalPriorityBindings_DoNotUseDirectSubscriptionPath()
    {
        var item = new Item("value");

        Assert.False(CreateColumn(BindingMode.OneTime).CanUseDirectValueAccessorFor(item));
        TestTextColumn delayed = CreateColumn(BindingMode.OneWay);
        ((Binding)delayed.Binding!).Delay = 25;
        Assert.False(delayed.CanUseDirectValueAccessorFor(item));

        TestTextColumn nonLocalPriority = CreateColumn(BindingMode.OneWay);
        ((Binding)nonLocalPriority.Binding!).Priority = BindingPriority.Style;
        Assert.False(nonLocalPriority.CanUseDirectValueAccessorFor(item));
    }

    [Fact]
    public void DirectStringFormat_PreservesNullFormatting()
    {
        var item = new NullableItem(null);
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NullableItem.Name))
            {
                StringFormat = "Value: {0}",
            },
            UseDirectTextCell = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NullableItem, string?>(static value => value.Name));
        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;

        Assert.True(cell.ConfigureValueAccessor(column, item));
        Assert.Equal("Value: ", cell.Value);
    }

    [AvaloniaFact]
    public void Nested_Path_Falls_Back_To_Binding_And_Observes_Leaf_Notifications()
    {
        var address = new Address("Warsaw");
        var item = new PersonWithAddress(address);
        var column = new TestTextColumn
        {
            Binding = new Binding("Address.City"),
            UseDirectTextCell = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<PersonWithAddress, string>(
                static person => person.Address.City));
        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;

        Assert.Null(column.GenerateDisplay(cell, item));
        var window = new Window { Width = 180, Height = 60, Content = cell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(cell.UsesValueAccessor);
            Assert.Equal("Warsaw", cell.Value);

            address.City = "Krakow";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Krakow", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    private static TestTextColumn CreateColumn(BindingMode mode)
    {
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(Item.Name)) { Mode = mode },
            UseDirectTextCell = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<Item, string>(static value => value.Name));
        return column;
    }

    private sealed class TestTextColumn : DataGridTextColumn
    {
        public Control? GenerateDisplay(DataGridCell cell, object item) =>
            GenerateElement(cell, item);
    }

    private sealed record Item(string Name);

    private sealed record NullableItem(string? Name);

    private sealed record PersonWithAddress(Address Address);

    private sealed class Address : INotifyPropertyChanged
    {
        private string _city;

        public Address(string city)
        {
            _city = city;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string City
        {
            get => _city;
            set
            {
                if (_city == value)
                {
                    return;
                }

                _city = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(City)));
            }
        }
    }

    private sealed class RecordingConverter : IValueConverter
    {
        public Type? LastTargetType { get; private set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            LastTargetType = targetType;
            return $"{parameter}:{culture.Name}:{value}";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
