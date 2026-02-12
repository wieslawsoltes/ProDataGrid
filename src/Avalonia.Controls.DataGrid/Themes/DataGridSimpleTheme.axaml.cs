// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Avalonia.Controls.Themes;

/// <summary>
/// Simple DataGrid theme resource host with density switching support.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
partial class DataGridSimpleTheme : Styles, IResourceNode
{
    readonly ResourceDictionary _compactStyles;
    DataGridDensityStyle _densityStyle;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataGridSimpleTheme"/> class.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider used by XAML loader.</param>
    public DataGridSimpleTheme(IServiceProvider? serviceProvider = null)
    {
        AvaloniaXamlLoader.Load(serviceProvider, this);
        _compactStyles = (ResourceDictionary)GetAndRemove("CompactStyles");

        object GetAndRemove(string key)
        {
            var value = Resources[key] ?? throw new KeyNotFoundException($"Key '{key}' was not found in resources.");
            Resources.Remove(key);
            return value;
        }
    }

    /// <summary>
    /// Identifies the <see cref="DensityStyle"/> property.
    /// </summary>
    public static readonly DirectProperty<DataGridSimpleTheme, DataGridDensityStyle> DensityStyleProperty =
        AvaloniaProperty.RegisterDirect<DataGridSimpleTheme, DataGridDensityStyle>(
            nameof(DensityStyle),
            o => o.DensityStyle,
            (o, v) => o.DensityStyle = v);

    /// <summary>
    /// Gets or sets the density style used by this theme.
    /// </summary>
    public DataGridDensityStyle DensityStyle
    {
        get => _densityStyle;
        set => SetAndRaise(DensityStyleProperty, ref _densityStyle, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DensityStyleProperty)
        {
            Owner?.NotifyHostedResourcesChanged(new ResourcesChangedEventArgs());
        }
    }

    bool IResourceNode.TryGetResource(object key, ThemeVariant? theme, out object? value)
    {
        if (_densityStyle == DataGridDensityStyle.Compact && _compactStyles.TryGetResource(key, theme, out value))
        {
            return true;
        }

        return base.TryGetResource(key, theme, out value);
    }
}
