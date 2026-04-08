// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class BindingCloneHelperTests
{
    [Fact]
    public void BindingAccessors_Handle_AvaloniaBinding()
    {
        var converter = new PassThroughConverter();
        var binding = new Binding(nameof(TestItem.Name))
        {
            Mode = BindingMode.TwoWay,
            Converter = converter,
            ConverterCulture = CultureInfo.InvariantCulture,
            ConverterParameter = "name",
            StringFormat = ">{0}<"
        };

        Assert.Equal(nameof(TestItem.Name), BindingCloneHelper.GetPath(binding));
        Assert.Equal(BindingMode.TwoWay, BindingCloneHelper.GetMode(binding));
        Assert.Same(converter, BindingCloneHelper.GetConverter(binding));
        Assert.Equal(CultureInfo.InvariantCulture, BindingCloneHelper.GetConverterCulture(binding));
        Assert.Equal("name", BindingCloneHelper.GetConverterParameter(binding));
        Assert.Equal(">{0}<", BindingCloneHelper.GetStringFormat(binding));

        Assert.True(BindingCloneHelper.TrySetMode(binding, BindingMode.OneWay));
        Assert.Equal(BindingMode.OneWay, binding.Mode);
        Assert.True(BindingCloneHelper.TrySetConverter(binding, null));
        Assert.Null(binding.Converter);
    }

    [Fact]
    public void BindingAccessors_Handle_CompiledBindingExtension()
    {
        var converter = new PassThroughConverter();
        var binding = Assert.IsType<CompiledBindingExtension>(DataGridBindingDefinition.Create<TestItem, string>(item => item.Name).CreateBinding());
        binding.Mode = BindingMode.TwoWay;
        binding.Converter = converter;
        binding.ConverterCulture = CultureInfo.InvariantCulture;
        binding.ConverterParameter = "compiled";
        binding.StringFormat = "[{0}]";

        Assert.Equal(nameof(TestItem.Name), BindingCloneHelper.GetPath(binding));
        Assert.Equal(BindingMode.TwoWay, BindingCloneHelper.GetMode(binding));
        Assert.Same(converter, BindingCloneHelper.GetConverter(binding));
        Assert.Equal(CultureInfo.InvariantCulture, BindingCloneHelper.GetConverterCulture(binding));
        Assert.Equal("compiled", BindingCloneHelper.GetConverterParameter(binding));
        Assert.Equal("[{0}]", BindingCloneHelper.GetStringFormat(binding));

        Assert.True(BindingCloneHelper.TrySetMode(binding, BindingMode.OneWay));
        Assert.Equal(BindingMode.OneWay, binding.Mode);
        Assert.True(BindingCloneHelper.TrySetConverter(binding, null));
        Assert.Null(binding.Converter);
    }

    [Fact]
    public void SupportsDirectDataContextMemberWrite_Rejects_Unsafe_Bindings()
    {
        var safeBinding = new Binding(nameof(TestItem.Enabled))
        {
            Mode = BindingMode.TwoWay
        };

        var converterBinding = new Binding(nameof(TestItem.Enabled))
        {
            Mode = BindingMode.TwoWay,
            Converter = new PassThroughConverter()
        };

        var sourceBinding = new Binding(nameof(TestItem.Enabled))
        {
            Mode = BindingMode.TwoWay,
            Source = new TestItem()
        };

        var elementNameBinding = new Binding(nameof(TestItem.Enabled))
        {
            Mode = BindingMode.TwoWay,
            ElementName = "ToggleHost"
        };

        var compiledBinding = Assert.IsType<CompiledBindingExtension>(DataGridBindingDefinition.Create<TestItem, bool>(item => item.Enabled).CreateBinding());
        compiledBinding.Mode = BindingMode.TwoWay;

        var compiledSourceBinding = Assert.IsType<CompiledBindingExtension>(DataGridBindingDefinition.Create<TestItem, bool>(item => item.Enabled).CreateBinding());
        compiledSourceBinding.Mode = BindingMode.TwoWay;
        compiledSourceBinding.Source = new TestItem();

        Assert.True(BindingCloneHelper.SupportsDirectDataContextMemberWrite(safeBinding));
        Assert.False(BindingCloneHelper.SupportsDirectDataContextMemberWrite(converterBinding));
        Assert.False(BindingCloneHelper.SupportsDirectDataContextMemberWrite(sourceBinding));
        Assert.False(BindingCloneHelper.SupportsDirectDataContextMemberWrite(elementNameBinding));
        Assert.True(BindingCloneHelper.SupportsDirectDataContextMemberWrite(compiledBinding));
        Assert.False(BindingCloneHelper.SupportsDirectDataContextMemberWrite(compiledSourceBinding));
    }

    private sealed class PassThroughConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }

    private sealed class TestItem
    {
        public string Name { get; set; } = string.Empty;

        public bool Enabled { get; set; }
    }
}
