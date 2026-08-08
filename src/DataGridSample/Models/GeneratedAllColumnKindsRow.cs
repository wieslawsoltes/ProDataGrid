using System;
using System.Collections.Generic;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedAllColumnKindsSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class GeneratedAllColumnKindsRow
{
    public static IReadOnlyList<string> Choices { get; } = new[] { "Alpha", "Beta", "Gamma" };

    [DataGridColumn(DataGridColumnKind.Text, Order = 0, Width = "140")]
    public string Text { get; set; } = "Text";

    [DataGridColumn(DataGridColumnKind.CheckBox, Order = 1)]
    public bool CheckBox { get; set; } = true;

    [DataGridColumn(DataGridColumnKind.Hyperlink, Order = 2, Width = "180")]
    public string Hyperlink { get; set; } = "https://avaloniaui.net";

    [DataGridColumn(DataGridColumnKind.Image, Order = 3)]
    public object? Image { get; set; }

    [DataGridColumn(DataGridColumnKind.Numeric, Order = 4, Minimum = 0, Maximum = 100)]
    public decimal Numeric { get; set; } = 42;

    [DataGridColumn(DataGridColumnKind.ProgressBar, Order = 5, Minimum = 0, Maximum = 100)]
    public double ProgressBar { get; set; } = 72;

    [DataGridColumn(DataGridColumnKind.Slider, Order = 6, Minimum = 0, Maximum = 100, Increment = 1)]
    public double Slider { get; set; } = 35;

    [DataGridColumn(DataGridColumnKind.DatePicker, Order = 7, FormatString = "yyyy-MM-dd")]
    public DateTime DatePicker { get; set; } = new(2026, 8, 7);

    [DataGridColumn(DataGridColumnKind.TimePicker, Order = 8, FormatString = "HH:mm")]
    public TimeSpan TimePicker { get; set; } = new(14, 30, 0);

    [DataGridColumn(DataGridColumnKind.MaskedText, Order = 9, Mask = "000-000")]
    public string MaskedText { get; set; } = "123456";

    [DataGridColumn(DataGridColumnKind.AutoComplete, Order = 10, ItemsSourceMember = nameof(Choices))]
    public string AutoComplete { get; set; } = "Alpha";

    [DataGridColumn(DataGridColumnKind.ToggleButton, Order = 11, Content = "Toggle")]
    public bool ToggleButton { get; set; } = true;

    [DataGridColumn(DataGridColumnKind.ToggleSwitch, Order = 12, Content = "On")]
    public bool ToggleSwitch { get; set; } = true;

    [DataGridColumn(DataGridColumnKind.Hierarchical, Order = 13)]
    public string Hierarchical { get; set; } = "Root / Child";

    [DataGridColumn(DataGridColumnKind.CustomDrawing, Order = 14)]
    public string CustomDrawing { get; set; } = "Custom";

    [DataGridColumn(DataGridColumnKind.ComboBoxSelectedItem, Order = 15, ItemsSourceMember = nameof(Choices))]
    public string ComboBoxSelectedItem { get; set; } = "Alpha";

    [DataGridColumn(DataGridColumnKind.ComboBoxSelectedValue, Order = 16, ItemsSourceMember = nameof(Choices))]
    public string ComboBoxSelectedValue { get; set; } = "Beta";

    [DataGridColumn(DataGridColumnKind.ComboBoxText, Order = 17, ItemsSourceMember = nameof(Choices), IsEditable = true)]
    public string ComboBoxText { get; set; } = "Gamma";

    [DataGridColumn(DataGridColumnKind.Template, Order = 18, TemplateKey = "GeneratedCellTemplate")]
    public string Template { get; set; } = "Template";

    [DataGridColumn(DataGridColumnKind.Button, Order = 19, Content = "Run")]
    public string Button { get; set; } = "Run";

    [DataGridColumn(DataGridColumnKind.Formula, Order = 20, Formula = "=1+1", FormulaName = "Generated")]
    public double Formula { get; set; } = 2;
}
