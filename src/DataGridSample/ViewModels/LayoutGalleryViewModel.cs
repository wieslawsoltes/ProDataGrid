// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia.Controls.DataGridLayouts;
using DataGridSample.Layouts;
using DataGridSample.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

public sealed partial class LayoutGalleryViewModel : ReactiveObject
{
    private DataGridLayoutChoice _selectedLayout;

    [Reactive]
    private IDataGridLayoutModel _layoutModel = null!;

    [Reactive]
    private string _description = string.Empty;

    [Reactive]
    private int _switchCount;

    public LayoutGalleryViewModel()
    {
        Layouts =
        [
            new("Virtualizing stack", "Variable-height vertical list using the existing indexed row-height estimator.", new DataGridStackLayoutModel { Spacing = 2 }),
            new("Horizontal stack", "Virtualized horizontal flow; width becomes the estimated major axis.", new DataGridStackLayoutModel { Orientation = DataGridLayoutOrientation.Horizontal, Spacing = 8 }),
            new("Non-virtualizing stack", "Reference layout that realizes every item. Keep data sets intentionally small.", new DataGridNonVirtualizingStackLayoutModel { Spacing = 2 }),
            new("Uniform grid", "Equal 260 × 74 cells with O(1) line and extent calculations.", new DataGridUniformGridLayoutModel { MinItemWidth = 260, MinItemHeight = 74, MinColumnSpacing = 8, MinRowSpacing = 8 }),
            new("Vertical uniform grid", "Equal cells fill downward and virtualize columns horizontally.", new DataGridUniformGridLayoutModel { Orientation = DataGridLayoutOrientation.Vertical, MinItemWidth = 260, MinItemHeight = 74, MinColumnSpacing = 8, MinRowSpacing = 8, MaximumRowsOrColumns = 4 }),
            new("Variable wrap", "Rows keep their measured size and wrap into cached, bounded line records.", new DataGridWrapLayoutModel { HorizontalSpacing = 8, VerticalSpacing = 8, MaximumCachedLines = 96 }),
            new("Vertical variable wrap", "Variable-size items fill downward before wrapping into columns.", new DataGridWrapLayoutModel { Orientation = DataGridLayoutOrientation.Vertical, HorizontalSpacing = 8, VerticalSpacing = 8, MaximumCachedLines = 96 }),
            new("Custom indented stack", "Application-defined virtualizing layout with its own spatial navigation resolver.", new IndentedStackLayoutModel { Indent = 42, Spacing = 5 })
        ];
        Rows = CreateRows(500);
        _selectedLayout = Layouts[0];
        LayoutModel = _selectedLayout.Model;
        Description = _selectedLayout.Description;
        NextLayoutCommand = ReactiveCommand.Create(SelectNextLayout);
    }

    public IReadOnlyList<DataGridLayoutChoice> Layouts { get; }

    public IReadOnlyList<LayoutGalleryRow> Rows { get; }

    public ReactiveCommand<RxVoid, RxVoid> NextLayoutCommand { get; }

    public DataGridLayoutChoice SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (value == null || ReferenceEquals(_selectedLayout, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedLayout, value);
            LayoutModel = value.Model;
            Description = value.Description;
            SwitchCount++;
        }
    }

    private void SelectNextLayout()
    {
        int index = 0;
        for (int candidate = 0; candidate < Layouts.Count; candidate++)
        {
            if (ReferenceEquals(Layouts[candidate], SelectedLayout))
            {
                index = candidate;
                break;
            }
        }
        SelectedLayout = Layouts[(index + 1) % Layouts.Count];
    }

    private static IReadOnlyList<LayoutGalleryRow> CreateRows(int count)
    {
        var rows = new List<LayoutGalleryRow>(count);
        for (int index = 0; index < count; index++)
        {
            string detail = (index % 5) switch
            {
                0 => "Short item.",
                1 => "A medium description that demonstrates measured row content.",
                2 => "Longer content: the same recycled DataGrid row containers are arranged by every model without rebuilding the item source or selection state.",
                3 => "Navigation remains semantic while the active layout resolves spatial targets.",
                _ => "Runtime switching retains one bounded algorithm session per model instance."
            };
            rows.Add(new LayoutGalleryRow
            {
                Id = index + 1,
                Category = $"Group {index % 12 + 1:00}",
                Title = $"Layout item {index + 1:n0}",
                Notes = detail
            });
        }
        return rows;
    }
}

public sealed class DataGridLayoutChoice
{
    public DataGridLayoutChoice(string name, string description, IDataGridLayoutModel model)
    {
        Name = name;
        Description = description;
        Model = model;
    }

    public string Name { get; }

    public string Description { get; }

    public IDataGridLayoutModel Model { get; }
}
