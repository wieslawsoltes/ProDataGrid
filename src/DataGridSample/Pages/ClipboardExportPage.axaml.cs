// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DataGridSample.Pages;

public partial class ClipboardExportPage : UserControl
{
    public ClipboardExportPage()
    {
        InitializeComponent();

        ItemsGrid.ItemsSource = BuildItems();
        ItemsGrid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
        ItemsGrid.SelectionMode = DataGridSelectionMode.Extended;
        ItemsGrid.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;

        UpdateExportSettings(null, null);
    }

    private void CopyTextFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Text);

    private void CopyCsvFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Csv);

    private void CopyHtmlFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Html);

    private void CopyMarkdownFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Markdown);

    private void CopyXmlFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Xml);

    private void CopyYamlFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Yaml);

    private void CopyJsonFormat(object? sender, RoutedEventArgs e) =>
        CopyFormat(DataGridClipboardExportFormat.Json);

    private void CopyFormat(DataGridClipboardExportFormat format)
    {
        ItemsGrid.CopySelectionToClipboard(format);
    }

    private void UpdateExportSettings(object? sender, RoutedEventArgs? e)
    {
        var format = sender switch
        {
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, TextFormatRadioButton) => DataGridClipboardExportFormat.Text,
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, CsvFormatRadioButton) => DataGridClipboardExportFormat.Csv,
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, HtmlFormatRadioButton) => DataGridClipboardExportFormat.Html,
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, MarkdownFormatRadioButton) => DataGridClipboardExportFormat.Markdown,
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, XmlFormatRadioButton) => DataGridClipboardExportFormat.Xml,
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, YamlFormatRadioButton) => DataGridClipboardExportFormat.Yaml,
            RadioButton { IsChecked: true } radio when ReferenceEquals(radio, JsonFormatRadioButton) => DataGridClipboardExportFormat.Json,
            _ => SelectActiveFormat()
        };

        ItemsGrid.ClipboardExportFormat = format;
    }

    private DataGridClipboardExportFormat SelectActiveFormat()
    {
        if (TextFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Text;
        }

        if (CsvFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Csv;
        }

        if (HtmlFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Html;
        }

        if (MarkdownFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Markdown;
        }

        if (XmlFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Xml;
        }

        if (YamlFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Yaml;
        }

        if (JsonFormatRadioButton.IsChecked == true)
        {
            return DataGridClipboardExportFormat.Json;
        }

        TextFormatRadioButton.IsChecked = true;
        return DataGridClipboardExportFormat.Text;
    }

    private static IReadOnlyList<ClipboardSampleItem> BuildItems() => new List<ClipboardSampleItem>
    {
        new("Kumquat", "Citrus", 12.50m, new DateTime(2025, 2, 3)),
        new("Morel", "Foraged", 42.00m, new DateTime(2025, 1, 18)),
        new("Radicchio", "Greens", 8.25m, new DateTime(2025, 2, 9)),
        new("Habanero", "Spice", 5.75m, new DateTime(2025, 1, 27)),
        new("Black garlic", "Condiment", 14.90m, new DateTime(2025, 2, 4)),
        new("Quinoa", "Grain", 6.10m, new DateTime(2025, 2, 8)),
        new("Elderflower", "Floral", 18.60m, new DateTime(2025, 1, 31)),
    };

    private sealed record ClipboardSampleItem(string Name, string Category, decimal Price, DateTime LastOrder);
}
