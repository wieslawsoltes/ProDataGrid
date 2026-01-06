using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridConditionalFormatting;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ExcelLikeEditingViewModel : ObservableObject
    {
        public ObservableCollection<SpreadsheetRow> Rows { get; } = new();

        public IConditionalFormattingModel ConditionalFormatting { get; }

        public ExcelLikeEditingViewModel()
        {
            Rows.Add(new SpreadsheetRow { Account = "North", Q1 = 1200, Q2 = 1350, Q3 = 1425, Q4 = 1550, Notes = "Steady growth" });
            Rows.Add(new SpreadsheetRow { Account = "South", Q1 = 980, Q2 = 1100, Q3 = 900, Q4 = 1020, Notes = "Seasonal dip" });
            Rows.Add(new SpreadsheetRow { Account = "East", Q1 = 1500, Q2 = 1620, Q3 = 1700, Q4 = 1890, Notes = "New contracts" });
            Rows.Add(new SpreadsheetRow { Account = "West", Q1 = 875, Q2 = 920, Q3 = 1010, Q4 = 980, Notes = "Recovery" });
            Rows.Add(new SpreadsheetRow { Account = "Central", Q1 = 1320, Q2 = 1280, Q3 = 1360, Q4 = 1400, Notes = "Stable" });

            ConditionalFormatting = new ConditionalFormattingModel();
            ConditionalFormatting.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "delta-positive",
                    @operator: ConditionalFormattingOperator.GreaterThan,
                    columnId: nameof(SpreadsheetRow.Delta),
                    value: 0d,
                    themeKey: "ExcelDeltaPositiveCellTheme"),
                new ConditionalFormattingDescriptor(
                    ruleId: "delta-negative",
                    @operator: ConditionalFormattingOperator.LessThan,
                    columnId: nameof(SpreadsheetRow.Delta),
                    value: 0d,
                    themeKey: "ExcelDeltaNegativeCellTheme")
            });
        }
    }
}
