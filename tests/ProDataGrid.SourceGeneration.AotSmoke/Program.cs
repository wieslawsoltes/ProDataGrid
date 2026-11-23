// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Data.Core;
using Avalonia.Controls;
using Avalonia.Collections;
using ProDataGrid.SourceGeneration.AotSmoke;
using ProDataGrid.SourceGeneration.AotSmoke.Generated;
using ProDataGrid.SourceGeneration.AotSmoke.Views;
using ProDataGrid.Charting;
using ProCharts;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

RxAppBuilder.CreateReactiveUIBuilder()
    .WithAvalonia()
    .BuildApp();

using var viewModel = new AotTradeViewModel();
DataGridGeneratedCollectionMutationService<AotTradeRow> mutations =
    AotTradeRowSchema.CreateConfiguredCollectionMutationService(maximumItemsPerMutation: 8);
DataGridGeneratedNewRowService<AotTradeRow> newRows =
    AotTradeRowSchema.CreateConfiguredNewRowService();
using DataGridGeneratedEditController<AotTradeRow, int> edits = AotTradeRowSchema.CreateEditController();
using DataGridGeneratedValidationProjection<AotTradeRow, int> validation =
    AotTradeRowSchema.CreateValidationProjection(edits);
DataGridGeneratedEditResult editResult = validation.TrySetText(
    viewModel.Items[0],
    "symbol",
    "AOTX".AsSpan(),
    CultureInfo.InvariantCulture);
DataGridGeneratedFillModel<AotTradeRow, int> formulaFill =
    AotTradeRowSchema.CreateConfiguredFormulaFillModel(edits);
AotTradeRow newRow = await newRows.CreateAsync();
await mutations.AddAsync(0, new[] { newRow });
using DataGridGeneratedChartRangeProjection chartProjection = DataGridGeneratedChartAdapter.CreateRangeProjection(
    viewModel.Items,
    AotTradeRowSchema.AnalyticsFields,
    viewModel.ColumnDefinitions,
    new DataGridCellRange(0, 1, 1, 3),
    maximumRows: 8);
using var chartKeyMap = new DataGridGeneratedListChartKeyMap<AotTradeRow, int>(
    viewModel.Items,
    AotTradeRowSchema.Instance);
var chartSelection = AotTradeRowSchema.CreateSelectionController();
chartSelection.ResetSource(viewModel.Items);
using var chartSynchronization = new DataGridGeneratedChartSelectionSynchronizer<AotTradeRow, int>(
    chartKeyMap,
    chartSelection,
    chartProjection.Model.Interaction);
chartSelection.SelectOnlyKey(2);
using DataGridGeneratedLongFormChartDataSource longFormSource =
    DataGridGeneratedChartAdapter.CreateLongFormSource(
        viewModel.Items,
        AotTradeRowSchema.AnalyticsFields,
        maximumItems: 8,
        maximumSeries: 8);
using var longFormModel = new ChartModel { DataSource = longFormSource };
using var outline = AotTradeRowSchema.CreateOutlineReportModel(viewModel.Items);
using DataGridGeneratedCollectionViewController<AotTradeRow, int> collectionView =
    AotTradeRowSchema.CreateCollectionViewController(viewModel.Items);
collectionView.SelectionController.SelectKey(2);
collectionView.View.MoveToNextPage();
collectionView.View.MoveToFirstPage();
var dropHandler = new AotTradeDropHandler();
using DataGridGeneratedDragDropController<int> dragDrop = AotTradeRowSchema.CreateDragDropController(dropHandler);
bool dropApplied = await dragDrop.DropAsync(
    new[] { 1 },
    2,
    DataGridGeneratedDropPosition.Before);

if (!AotGeneratedRegistry.TryGetSchema(typeof(AotTradeRow), out IDataGridGeneratedSchemaManifestProvider schema) ||
    schema.Manifest.SchemaId != AotTradeRowSchema.SchemaId ||
    viewModel.ColumnDefinitions.Count != 5 ||
    !viewModel.FastPathOptions.StrictMode ||
    !editResult.IsApplied ||
    validation.HasErrors ||
    viewModel.Items[0].Symbol != "AOTX" ||
    newRow.Id != 42 ||
    chartProjection.Model.Snapshot.Series.Count != 1 ||
    collectionView.View.PageSize != 1 ||
    collectionView.View.CurrentItem is not AotTradeRow currentTrade ||
    currentTrade.Id != 1 ||
    collectionView.SelectionController.SelectedItemKeys.Count != 1 ||
    chartProjection.Model.Interaction.CrosshairCategoryIndex != 1 ||
    longFormModel.Snapshot.Categories.Count != 2 ||
    longFormModel.Snapshot.Series.Count != 2 ||
    outline.GroupFields.Count != 1 ||
    outline.ValueFields.Count != 1 ||
    outline.GroupFields[0].ShowSubtotals ||
    outline.ValueFields[0].NullLabel != "AOT empty" ||
    outline.Layout.ShowGrandTotal ||
    outline.Rows.Count == 0 ||
    !dropApplied ||
    dropHandler.ApplyCount != 1)
{
    return 1;
}

if (!AotGeneratedRegistry.TryCreateView(viewModel, out Control? registeredView) ||
    registeredView is not AotRegisteredView)
{
    return 2;
}

Control avaloniaView = new AotGeneratedGridView(viewModel);
Control reactiveView = new AotGeneratedReactiveGridView(viewModel);
if (AotGeneratedGridView.GeneratedViewThemeKey != "AotGeneratedViewTheme" ||
    AotGeneratedGridView.GeneratedDataGridThemeKey != "AotGeneratedDataGridTheme" ||
    !avaloniaView.Classes.Contains("generated-aot-view"))
{
    return 3;
}

if (DataGridReflectionOptions.IsEnabled)
{
    return 4;
}

var beta = new AotTradeRow { Id = 8, Symbol = "Beta" };
var alpha = new AotTradeRow { Id = 3, Symbol = "Alpha" };
var typedView = new DataGridCollectionView(new List<AotTradeRow> { beta, alpha });
var symbolAccessor = new DataGridColumnValueAccessor<AotTradeRow, string>(
    row => row.Symbol, (row, symbol) => row.Symbol = symbol);
typedView.SortDescriptions.Add(DataGridSortDescription.FromAccessor(symbolAccessor));
if (!ReferenceEquals(typedView[0], alpha))
{
    return 5;
}

symbolAccessor.SetValue(beta, "Aardvark");
typedView.Refresh();
var typedGrid = new DataGrid
{
    AutoGenerateColumns = false,
    FastPathOptions = viewModel.FastPathOptions,
    ColumnDefinitionsSource = viewModel.ColumnDefinitions,
    ItemsSource = typedView,
    SelectedItem = beta
};
if (!ReferenceEquals(typedView[0], beta) ||
    beta.Symbol != "Aardvark" ||
    !ReferenceEquals(typedGrid.SelectedItem, beta) ||
    typedGrid.SelectedItems.Count != 1)
{
    return 6;
}

if (typedGrid.Columns[1].IsReadOnly || !typedGrid.Columns[0].IsReadOnly)
{
    return 7;
}

var volumeProperty = new ClrPropertyInfo(nameof(AotTradeRow.Volume), row => ((AotTradeRow)row).Volume,
    (row, value) => ((AotTradeRow)row).Volume = (double)value!, typeof(double));
var timeProperty = new ClrPropertyInfo(nameof(AotTradeRow.Time), row => ((AotTradeRow)row).Time,
    (row, value) => ((AotTradeRow)row).Time = (TimeSpan?)value, typeof(TimeSpan?));
var additionalGrid = new DataGrid
{
    AutoGenerateColumns = false,
    ColumnDefinitionsSource = new DataGridColumnDefinition[]
    {
        new DataGridSliderColumnDefinition
        {
            Header = "Volume",
            ShowValueText = true,
            Binding = DataGridBindingDefinition.Create<AotTradeRow, double>(volumeProperty,
                row => row.Volume, (row, value) => row.Volume = value)
        },
        new DataGridTimePickerColumnDefinition
        {
            Header = "Time",
            Binding = DataGridBindingDefinition.Create<AotTradeRow, TimeSpan?>(timeProperty,
                row => row.Time, (row, value) => row.Time = value)
        },
        new DataGridFormulaColumnDefinition { Header = "Formula", Formula = "1+2" }
    },
    ItemsSource = new List<AotTradeRow> { beta, alpha }
};
GC.KeepAlive(additionalGrid);
Console.WriteLine("NativeAOT smoke passed: reflection disabled; typed sorting, setters, selection, writable/readonly capabilities, Slider/TimePicker/formula columns, generated editing and views.");
GC.KeepAlive(typedGrid);
GC.KeepAlive(registeredView);
GC.KeepAlive(avaloniaView);
GC.KeepAlive(reactiveView);
GC.KeepAlive(formulaFill);
return 0;
