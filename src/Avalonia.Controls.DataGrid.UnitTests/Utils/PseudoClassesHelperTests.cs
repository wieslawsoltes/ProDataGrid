// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Utils;

public class PseudoClassesHelperTests
{
    [AvaloniaFact]
    public void Set_Adds_And_Removes_PseudoClass()
    {
        var control = new Border();
        var classes = (IPseudoClasses)control.Classes;

        PseudoClassesHelper.Set(classes, ":foo", true);
        Assert.True(classes.Contains(":foo"));

        PseudoClassesHelper.Set(classes, ":foo", false);
        Assert.False(classes.Contains(":foo"));
    }

    [AvaloniaFact]
    public void Set_Throws_On_Null_Classes()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PseudoClassesHelper.Set(null!, ":foo", true));
    }
}
