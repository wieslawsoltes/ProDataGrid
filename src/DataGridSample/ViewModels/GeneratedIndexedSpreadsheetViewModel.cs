// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFormulas;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridView(
    typeof(GeneratedSpreadsheetRow),
    ViewName = "GeneratedIndexedSpreadsheetGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Spreadsheet,
    Title = "Generated runtime indexed spreadsheet",
    AutomationId = "generated-indexed-spreadsheet-grid",
    FormulaModelPropertyName = nameof(FormulaModel),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
    EditTriggers = DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.TextInput | DataGridEditTriggers.F2)]
public sealed partial class GeneratedIndexedSpreadsheetViewModel : ReactiveObject, IDisposable
{
    public const int MinimumColumnCount = 7;
    public const int MaximumColumnCount = 12;
    private const int InitialColumnCount = 10;
    private const int RowCount = 24;
    private readonly Random _random = new(7041);
    private bool _disposed;

    [Reactive]
    private DataGridColumnDefinitionList _columnDefinitions = null!;

    [Reactive]
    private int _visibleColumnCount;

    [Reactive]
    private string _status = "Generated indexed accessors and formulas are ready.";

    [Reactive]
    private string _formulaPreview = "Select Recalculate to inspect E1, G1, H1, and J1.";

    public GeneratedIndexedSpreadsheetViewModel()
    {
        Items = new ObservableCollection<GeneratedSpreadsheetRow>();
        FastPathOptions = new DataGridFastPathOptions { StrictMode = true };
        FormulaModel = new DataGridFormulaModel();
        FormulaModel.Invalidated += OnFormulaInvalidated;

        for (int rowIndex = 0; rowIndex < RowCount; rowIndex++)
        {
            var row = new GeneratedSpreadsheetRow(rowIndex + 1, MaximumColumnCount);
            SeedRow(row, rowIndex);
            Items.Add(row);
        }

        _visibleColumnCount = InitialColumnCount;
        _columnDefinitions = CreateColumns(_visibleColumnCount);

        AddColumnCommand = ReactiveCommand.Create(AddColumn);
        RemoveColumnCommand = ReactiveCommand.Create(RemoveColumn);
        RandomizeInputsCommand = ReactiveCommand.Create(RandomizeInputs);
        ApplyCellFormulaCommand = ReactiveCommand.Create(ApplyCellFormula);
        RecalculateCommand = ReactiveCommand.Create(Recalculate);
    }

    public ObservableCollection<GeneratedSpreadsheetRow> Items { get; }

    public DataGridFastPathOptions FastPathOptions { get; }

    public DataGridFormulaModel FormulaModel { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddColumnCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RemoveColumnCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RandomizeInputsCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyCellFormulaCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RecalculateCommand { get; }

    public bool IsDisposed => _disposed;

    public object? EvaluateFormula(int rowIndex, int columnIndex)
    {
        if ((uint)rowIndex >= (uint)Items.Count ||
            (uint)columnIndex >= (uint)ColumnDefinitions.Count ||
            ColumnDefinitions[columnIndex] is not DataGridFormulaColumnDefinition formula)
        {
            return null;
        }

        return FormulaModel.Evaluate(Items[rowIndex], formula);
    }

    private void AddColumn()
    {
        if (VisibleColumnCount >= MaximumColumnCount)
        {
            Status = $"The generated family is bounded at {MaximumColumnCount} runtime slots.";
            return;
        }

        VisibleColumnCount++;
        ColumnDefinitions = CreateColumns(VisibleColumnCount);
        FormulaModel.Invalidate();
        Status = $"Materialized {VisibleColumnCount} columns from one generated indexed family.";
    }

    private void RemoveColumn()
    {
        if (VisibleColumnCount <= MinimumColumnCount)
        {
            Status = $"The first {MinimumColumnCount} business and formula columns are retained.";
            return;
        }

        VisibleColumnCount--;
        ColumnDefinitions = CreateColumns(VisibleColumnCount);
        FormulaModel.Invalidate();
        Status = $"Reduced the runtime family to {VisibleColumnCount} visible columns.";
    }

    private void RandomizeInputs()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            GeneratedSpreadsheetRow row = Items[index];
            row.SetCell(1, (double)_random.Next(1, 60));
            row.SetCell(2, Math.Round(15d + _random.NextDouble() * 210d, 2));
            row.SetCell(3, Math.Round(_random.NextDouble() * 0.25d, 2));
            row.SetCell(8, Math.Round(_random.NextDouble() * 500d, 2));
            row.SetCell(10, Math.Round(_random.NextDouble() * 250d, 2));
        }

        FormulaModel.Invalidate();
        Status = "Updated typed numeric slots; dependent formulas were invalidated once.";
    }

    private void ApplyCellFormula()
    {
        if (ColumnDefinitions.Count <= 7 || ColumnDefinitions[7] is not DataGridFormulaColumnDefinition formula)
        {
            Status = "The H cell-formula column is not currently visible.";
            return;
        }

        if (FormulaModel.TrySetCellFormula(
            Items[0],
            formula,
            "=125+25",
            out string? error))
        {
            Status = "Applied a row-local formula override to H1 through the bound formula model.";
            Recalculate();
        }
        else
        {
            Status = error ?? "The H1 formula could not be applied.";
        }
    }

    private void Recalculate()
    {
        FormulaModel.Recalculate();
        UpdateFormulaPreview();
        Status = $"Recalculated {Items.Count} rows across {VisibleColumnCount} runtime columns.";
    }

    private void UpdateFormulaPreview(string? prefix = null)
    {
        FormulaPreview =
            (string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + "  ") +
            $"E1={FormatValue(EvaluateFormula(0, 4))}  " +
            $"G1={FormatValue(EvaluateFormula(0, 6))}  " +
            $"H1={FormatValue(EvaluateFormula(0, 7))}  " +
            $"J1={FormatValue(EvaluateFormula(0, 9))}";
    }

    private void OnFormulaInvalidated(object? sender, DataGridFormulaInvalidatedEventArgs args)
    {
        UpdateFormulaPreview($"v{FormulaModel.FormulaVersion}; refresh={args.RequiresRefresh}");
    }

    private static DataGridColumnDefinitionList CreateColumns(int count)
    {
        var columns = new DataGridColumnDefinitionList();
        for (int index = 0; index < count; index++)
        {
            columns.Add(CreateColumn(index));
        }

        return columns;
    }

    private static DataGridColumnDefinition CreateColumn(int index)
    {
        string name = GeneratedSpreadsheetRow.GetCellPropertyName(index);
        DataGridLength width = index == 0 ? new DataGridLength(150d) : new DataGridLength(105d);

        if (index is 4 or 6 or 7 or 9 or 11)
        {
            string formula = index switch
            {
                4 => "=([@B]*[@C])*(1-[@D])",
                6 => "=[@E]/5",
                7 => "=[@E]",
                9 => "=[@E]+[@I]",
                11 => "=[@J]+[@K]",
                _ => string.Empty
            };
            var formulaOptions = new DataGridGeneratedIndexedColumnOptions<double>
            {
                Header = name,
                ColumnKey = name,
                PropertyName = name,
                Kind = DataGridGeneratedIndexedColumnKind.Formula,
                Formula = formula,
                FormulaName = name,
                AllowCellFormulas = index == 7,
                IsReadOnly = index != 7,
                Width = width
            };
            return GeneratedSpreadsheetRowCells.CreateColumn<double>(index, in formulaOptions);
        }

        if (index is 1 or 2 or 3 or 8 or 10)
        {
            var numericOptions = new DataGridGeneratedIndexedColumnOptions<double>
            {
                Header = name,
                ColumnKey = name,
                PropertyName = name,
                Kind = DataGridGeneratedIndexedColumnKind.Numeric,
                FormatString = index == 3 ? "P0" : "N2",
                Width = width
            };
            return GeneratedSpreadsheetRowCells.CreateColumn<double>(index, in numericOptions);
        }

        var textOptions = new DataGridGeneratedIndexedColumnOptions<string?>
        {
            Header = name,
            ColumnKey = name,
            PropertyName = name,
            Kind = DataGridGeneratedIndexedColumnKind.Text,
            Width = width
        };
        return GeneratedSpreadsheetRowCells.CreateColumn<string?>(index, in textOptions);
    }

    private static void SeedRow(GeneratedSpreadsheetRow row, int index)
    {
        string[] regions = ["North", "South", "East", "West"];
        row.SetCell(0, $"SKU-{index + 1:000}");
        row.SetCell(1, 4d + index % 9);
        row.SetCell(2, 22.5d + index * 3.75d);
        row.SetCell(3, (index % 4) * 0.05d);
        row.SetCell(5, regions[index % regions.Length]);
        row.SetCell(8, 40d + index * 2d);
        row.SetCell(10, 15d + index);
    }

    private static string FormatValue(object? value) => value switch
    {
        double number => number.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
        null => "—",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "—"
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        FormulaModel.Invalidated -= OnFormulaInvalidated;
        FormulaModel.Dispose();
        _disposed = true;
    }
}
