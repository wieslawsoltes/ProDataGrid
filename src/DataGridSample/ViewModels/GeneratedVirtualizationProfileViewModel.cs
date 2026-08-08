// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Models;
using DataGridSample.Pages;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedVirtualizationRow), ProviderName = "GeneratedVirtualizationRowSchema")]
[GenerateDataGridView(
    typeof(GeneratedVirtualizationRow),
    ViewName = "GeneratedVirtualizationProfilePage",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.GridOnly,
    Title = "Generated virtualization and input profile",
    AutomationId = "generated-virtualization-profile-grid",
    PerformanceProfile = DataGridGeneratedPerformanceProfile.VariableHeightEstimated,
    InputMapType = typeof(GeneratedVirtualizationInputMap),
    InputCommandPropertyName = nameof(InputCommand),
    NavigationInteractionPropertyName = nameof(Navigation),
    DiagnosticsSinkType = typeof(GeneratedVirtualizationMetricsSink))]
public sealed partial class GeneratedVirtualizationProfileViewModel : ReactiveObject
{
    private readonly ObservableCollection<GeneratedVirtualizationRow> _items;

    [Reactive]
    private string _lastAction = "Use Ctrl/Cmd+F, Ctrl/Cmd+D/R/Z, or J/K navigation.";

    public GeneratedVirtualizationProfileViewModel()
    {
        _items =
        [
            new GeneratedVirtualizationRow { Id = 1, Workload = "Streaming", Description = "High-frequency keyed updates with bounded ingestion and stable row identity.", UpdatesPerSecond = 12_500 },
            new GeneratedVirtualizationRow { Id = 2, Workload = "Variable height", Description = "Estimated logical scrolling avoids measuring the full data set while preserving responsive navigation.", UpdatesPerSecond = 2_400 },
            new GeneratedVirtualizationRow { Id = 3, Workload = "Recycling", Description = "Renderer counters are delivered to an activation-scoped typed diagnostics sink.", UpdatesPerSecond = 8_100 }
        ];
        InputCommand = ReactiveCommand.Create<DataGridGeneratedInputEvent<GeneratedVirtualizationRow>>(HandleInput);
        Navigation = new Interaction<
            DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>,
            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow>>();
    }

    public ObservableCollection<GeneratedVirtualizationRow> Items => _items;

    public ReactiveCommand<DataGridGeneratedInputEvent<GeneratedVirtualizationRow>, RxVoid> InputCommand { get; }

    public Interaction<
        DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>,
        DataGridGeneratedNavigationResult<GeneratedVirtualizationRow>> Navigation { get; }

    public DataGridGeneratedInputEvent<GeneratedVirtualizationRow>? LastInput { get; private set; }

    private void HandleInput(DataGridGeneratedInputEvent<GeneratedVirtualizationRow> input)
    {
        LastInput = input;
        string row = input.Item is null ? "no current row" : input.Item.Workload;
        LastAction = $"{input.Action}: {row} at [{input.RowIndex}, {input.ColumnIndex}]";
    }
}
