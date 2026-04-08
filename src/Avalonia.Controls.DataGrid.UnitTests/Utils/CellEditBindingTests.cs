// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class CellEditBindingTests
{
    static CellEditBindingTests()
    {
        Avalonia12TestCompat.EnsureDataValidator("ExceptionValidationPlugin");
        Avalonia12TestCompat.EnsureDataValidator("IndeiValidationPlugin");
    }

    [AvaloniaFact]
    public void CommitEdit_Clears_Stale_Parse_Errors_After_Successful_Update()
    {
        var item = new NumericItem { Count = 1 };
        var textBox = new TextBox { DataContext = item };
        var root = new Window { Content = textBox };
        var binding = new Binding(nameof(NumericItem.Count))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        };

        root.Show();
        textBox.Bind(TextBox.TextProperty, binding);
        Dispatcher.UIThread.RunJobs();

        var editBinding = new CellEditBinding(textBox, TextBox.TextProperty, binding);

        try
        {
            textBox.Text = "abc";
            Assert.False(editBinding.CommitEdit());
            Assert.False(editBinding.IsValid);

            textBox.Text = "12";
            Assert.True(editBinding.CommitEdit());
            Assert.True(editBinding.IsValid);
            Assert.Equal(12, item.Count);
            Assert.False(DataValidationErrors.GetHasErrors(textBox));
        }
        finally
        {
            editBinding.Dispose();
            root.Close();
        }
    }

    [AvaloniaFact]
    public void CommitEdit_Skips_Direct_Write_For_Converter_Backed_Toggle_Binding()
    {
        var item = new ToggleBindingItem { State = "no" };
        var toggle = new ToggleButton { DataContext = item };
        var root = new Window { Content = toggle };
        var binding = new Binding(nameof(ToggleBindingItem.State))
        {
            Mode = BindingMode.TwoWay,
            Converter = new ToggleStateConverter(),
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        };

        root.Show();
        toggle.Bind(ToggleButton.IsCheckedProperty, binding);
        Dispatcher.UIThread.RunJobs();

        var editBinding = new CellEditBinding(toggle, ToggleButton.IsCheckedProperty, binding);

        try
        {
            toggle.IsChecked = true;

            Assert.True(editBinding.CommitEdit());
            Assert.True(editBinding.IsValid);
            Assert.Equal("yes", item.State);
        }
        finally
        {
            editBinding.Dispose();
            root.Close();
        }
    }

    private sealed class NumericItem
    {
        public int Count { get; set; }
    }

    private sealed class ToggleBindingItem
    {
        public string State { get; set; } = "no";
    }

    private sealed class ToggleStateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return string.Equals(value as string, "yes", StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? "yes" : "no";
        }
    }
}
