// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls.Themes;
using Avalonia.Themes.Fluent;

namespace Avalonia.Controls.DataGridAotSmoke;

public sealed class SmokeApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new DataGridFluentTheme());
    }
}
