// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Controls.DataGridConditionalFormatting;
using ProDataGrid.SourceGeneration;
using ReactiveUI;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedConditionalFormattingRowSchema",
    SchemaId = "sample/generated-conditional-formatting-row/v1",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true)]
public sealed class GeneratedConditionalFormattingRow : ReactiveObject
{
    private int _id;
    private string _region = string.Empty;
    private double _score;
    private double _change;
    private double _target;
    private string _status = string.Empty;

    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", ColumnKey = "id", Width = "72", IsReadOnly = true)]
    public int Id
    {
        get => _id;
        init => _id = value;
    }

    [DataGridColumn(Header = "Region", ColumnKey = "region", Width = "1.5*", IsReadOnly = true)]
    public string Region
    {
        get => _region;
        init => _region = value;
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Score", ColumnKey = "score", Width = "*", FormatString = "N1")]
    [DataGridConditionalFormat(DataGridCondition.GreaterThanOrEqual, RuleId = "score-high", Operand = "90", CellThemeKey = "GeneratedScoreHighCellTheme", Priority = 0)]
    [DataGridConditionalFormat(DataGridCondition.LessThan, RuleId = "score-low", Operand = "60", CellThemeKey = "GeneratedScoreLowCellTheme", Priority = 0)]
    [DataGridConditionalFormat(DataGridCondition.Custom, RuleId = "score-below-target", PredicateMethod = nameof(IsBelowTarget), CellThemeKey = "GeneratedScoreBelowTargetCellTheme", Priority = 1)]
    public double Score
    {
        get => _score;
        set => this.RaiseAndSetIfChanged(ref _score, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Change", ColumnKey = "change", Width = "*", FormatString = "+0.0;-0.0;0.0")]
    [DataGridConditionalFormat(DataGridCondition.LessThan, RuleId = "change-negative", Operand = "0", CellThemeKey = "GeneratedChangeNegativeCellTheme")]
    public double Change
    {
        get => _change;
        set => this.RaiseAndSetIfChanged(ref _change, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Target", ColumnKey = "target", Width = "*", FormatString = "N1")]
    [DataGridConditionalFormat(DataGridCondition.GreaterThan, RuleId = "target-stretch", Operand = "85", CellThemeKey = "GeneratedTargetStretchCellTheme")]
    public double Target
    {
        get => _target;
        set => this.RaiseAndSetIfChanged(ref _target, value);
    }

    [DataGridColumn(Header = "Status", ColumnKey = "status", Width = "1.4*")]
    [DataGridConditionalFormat(DataGridCondition.Equals, RuleId = "row-overdue", Operand = "Overdue", CellThemeKey = "GeneratedRowAlertTheme", Priority = 0, Target = ConditionalFormattingTarget.Row)]
    [DataGridConditionalFormat(DataGridCondition.Equals, RuleId = "row-risk", Operand = "At Risk", CellThemeKey = "GeneratedRowWarningTheme", Priority = 1, Target = ConditionalFormattingTarget.Row)]
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public static bool IsBelowTarget(GeneratedConditionalFormattingRow item, double score) =>
        score < item.Target;
}
