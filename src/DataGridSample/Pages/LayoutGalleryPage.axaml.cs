// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DataGridSample.Pages;

public partial class LayoutGalleryPage : UserControl
{
    public LayoutGalleryPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
