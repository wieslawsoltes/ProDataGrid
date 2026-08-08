// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridConditionalFormatting;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedConditionalFormattingRow), ProviderName = "GeneratedConditionalFormattingRowSchema")]
[GenerateDataGridView(
    typeof(GeneratedConditionalFormattingRow),
    ViewName = "GeneratedConditionalFormattingGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated conditional formatting",
    AutomationId = "generated-conditional-formatting-grid",
    ConditionalFormattingModelPropertyName = nameof(ConditionalFormatting),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.Cell)]
public sealed partial class GeneratedConditionalFormattingViewModel : ReactiveObject
{
    private readonly Random _random = new(3107);

    [Reactive]
    private bool _rulesEnabled = true;

    [Reactive]
    private string _status = "Seven typed rules are active: five cell rules and two row rules.";

    [Reactive]
    private int _highScoreCount;

    [Reactive]
    private int _belowTargetCount;

    [Reactive]
    private int _atRiskCount;

    public GeneratedConditionalFormattingViewModel()
    {
        Items = [];
        ConditionalFormatting = GeneratedConditionalFormattingRowSchema.CreateConditionalFormattingModel();
        RestoreBaselineRows();

        RandomizeCommand = ReactiveCommand.Create(Randomize);
        ToggleRulesCommand = ReactiveCommand.Create(ToggleRules);
        RestoreCommand = ReactiveCommand.Create(Restore);
    }

    public ObservableCollection<GeneratedConditionalFormattingRow> Items { get; }

    public IConditionalFormattingModel ConditionalFormatting { get; }

    public ReactiveCommand<RxVoid, RxVoid> RandomizeCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ToggleRulesCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RestoreCommand { get; }

    private void Randomize()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            GeneratedConditionalFormattingRow row = Items[index];
            row.Target = 68d + _random.Next(0, 24);
            row.Score = 42d + _random.NextDouble() * 58d;
            row.Change = -12d + _random.NextDouble() * 22d;
            row.Status = ResolveStatus(row.Score, row.Target, row.Change);
        }

        PublishCounts("Randomized reactive values; generated predicates were reused without descriptor rebuilding.");
    }

    private void ToggleRules()
    {
        if (RulesEnabled)
        {
            ConditionalFormatting.Clear();
            RulesEnabled = false;
            Status = "Formatting disabled by clearing the bound model; generated metadata remains reusable.";
            return;
        }

        IConditionalFormattingModel generated = GeneratedConditionalFormattingRowSchema.CreateConditionalFormattingModel();
        ConditionalFormatting.Apply(generated.Descriptors);
        RulesEnabled = true;
        PublishCounts("Restored all generated rules through the typed model factory.");
    }

    private void Restore()
    {
        RestoreBaselineRows();
        if (!RulesEnabled)
        {
            IConditionalFormattingModel generated = GeneratedConditionalFormattingRowSchema.CreateConditionalFormattingModel();
            ConditionalFormatting.Apply(generated.Descriptors);
            RulesEnabled = true;
        }
        PublishCounts("Restored the deterministic baseline and generated formatting model.");
    }

    private void RestoreBaselineRows()
    {
        Items.Clear();
        string[] regions = ["North", "South", "East", "West"];
        for (int index = 0; index < 16; index++)
        {
            double target = 70d + index % 5 * 5d;
            double score = 48d + index * 3.4d;
            double change = -8d + index * 1.15d;
            Items.Add(new GeneratedConditionalFormattingRow
            {
                Id = 7001 + index,
                Region = regions[index % regions.Length],
                Score = score,
                Change = change,
                Target = target,
                Status = ResolveStatus(score, target, change)
            });
        }
        PublishCounts("Loaded deterministic rows for generated cell and row formatting.");
    }

    private void PublishCounts(string message)
    {
        int high = 0;
        int belowTarget = 0;
        int risk = 0;
        for (int index = 0; index < Items.Count; index++)
        {
            GeneratedConditionalFormattingRow row = Items[index];
            if (row.Score >= 90d) high++;
            if (GeneratedConditionalFormattingRow.IsBelowTarget(row, row.Score)) belowTarget++;
            if (row.Status is "At Risk" or "Overdue") risk++;
        }

        HighScoreCount = high;
        BelowTargetCount = belowTarget;
        AtRiskCount = risk;
        Status = message;
    }

    private static string ResolveStatus(double score, double target, double change)
    {
        if (score < 58d || change < -7d) return "Overdue";
        if (score < target || change < 0d) return "At Risk";
        return "On Track";
    }
}
