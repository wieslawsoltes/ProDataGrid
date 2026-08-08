using System;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedEmployeeDataGridSchema",
    Strict = true)]
public sealed class GeneratedEmployee
{
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", Order = 0, ColumnKey = "employee-id", Width = "80", IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Name", Order = 1, ColumnKey = "employee-name", Width = "2*")]
    public string Name { get; set; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Team", Order = 2, ColumnKey = "employee-team", Width = "*")]
    public string Team { get; set; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Score", Order = 3, ColumnKey = "employee-score", Minimum = 0, Maximum = 100)]
    public decimal Score { get; set; }

    [DataGridColumn(DataGridColumnKind.CheckBox, Header = "Active", Order = 4, ColumnKey = "employee-active")]
    public bool IsActive { get; set; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Joined", Order = 5, ColumnKey = "employee-joined", FormatString = "yyyy-MM-dd")]
    public DateTimeOffset Joined { get; set; }
}
