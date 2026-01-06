using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridConditionalFormatting;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ConditionalFormattingSampleViewModel : ObservableObject
    {
        public ObservableCollection<ConditionalFormattingSampleRow> Rows { get; } = new();

        public IConditionalFormattingModel ConditionalFormatting { get; }

        public ConditionalFormattingSampleViewModel()
        {
            Rows.Add(new ConditionalFormattingSampleRow { Region = "North", Score = 92, Change = 4.5, Target = 90, Status = "On Track" });
            Rows.Add(new ConditionalFormattingSampleRow { Region = "South", Score = 58, Change = -6.2, Target = 75, Status = "At Risk" });
            Rows.Add(new ConditionalFormattingSampleRow { Region = "East", Score = 81, Change = 1.2, Target = 80, Status = "On Track" });
            Rows.Add(new ConditionalFormattingSampleRow { Region = "West", Score = 73, Change = -2.3, Target = 78, Status = "At Risk" });
            Rows.Add(new ConditionalFormattingSampleRow { Region = "Central", Score = 49, Change = -9.8, Target = 70, Status = "Overdue" });

            ConditionalFormatting = new ConditionalFormattingModel();
            ConditionalFormatting.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "score-high",
                    @operator: ConditionalFormattingOperator.GreaterThanOrEqual,
                    columnId: nameof(ConditionalFormattingSampleRow.Score),
                    value: 90d,
                    themeKey: "ConditionalScoreHighCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "score-low",
                    @operator: ConditionalFormattingOperator.LessThan,
                    columnId: nameof(ConditionalFormattingSampleRow.Score),
                    value: 60d,
                    themeKey: "ConditionalScoreLowCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "change-negative",
                    @operator: ConditionalFormattingOperator.LessThan,
                    columnId: nameof(ConditionalFormattingSampleRow.Change),
                    value: 0d,
                    themeKey: "ConditionalDeltaNegativeCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "row-overdue",
                    @operator: ConditionalFormattingOperator.Equals,
                    propertyPath: nameof(ConditionalFormattingSampleRow.Status),
                    value: "Overdue",
                    target: ConditionalFormattingTarget.Row,
                    valueSource: ConditionalFormattingValueSource.Item,
                    priority: 0,
                    themeKey: "ConditionalRowAlertTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "row-risk",
                    @operator: ConditionalFormattingOperator.Equals,
                    propertyPath: nameof(ConditionalFormattingSampleRow.Status),
                    value: "At Risk",
                    target: ConditionalFormattingTarget.Row,
                    valueSource: ConditionalFormattingValueSource.Item,
                    priority: 1,
                    themeKey: "ConditionalRowWarningTheme")
            });
        }
    }
}
