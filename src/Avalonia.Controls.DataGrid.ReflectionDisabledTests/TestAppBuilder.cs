// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia;
using Avalonia.Headless;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(Avalonia.Controls.DataGridReflectionDisabledTests.ReflectionDisabledTestAppBuilder))]

namespace Avalonia.Controls.DataGridReflectionDisabledTests;

internal sealed class ReflectionDisabledTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<ReflectionDisabledTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

internal sealed class ReflectionDisabledTestApp : Application
{
}
