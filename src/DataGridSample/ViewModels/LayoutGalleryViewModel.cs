// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia;
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
            new("Table list", "Classic variable-height DataGrid rows and cells, including column headers.", new DataGridStackLayoutModel { Spacing = 2 }),
            new("Horizontal card stack", "Virtualized horizontal item flow using the gallery card template instead of rows or cells.", new DataGridStackLayoutModel { Orientation = DataGridLayoutOrientation.Horizontal, Spacing = 10, PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(280, 92) }),
            new("Non-virtualizing cards", "Reference item layout that realizes every card. Keep production data sets intentionally small.", new DataGridNonVirtualizingStackLayoutModel { Spacing = 8, PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(280, 92) }),
            new("Uniform card grid", "Equal 260 × 96 item slots with O(1) line and extent calculations.", new DataGridUniformGridLayoutModel { PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(260, 96), MinItemWidth = 260, MinItemHeight = 96, MinColumnSpacing = 8, MinRowSpacing = 8 }),
            new("Vertical uniform cards", "Equal item slots fill downward and virtualize columns horizontally.", new DataGridUniformGridLayoutModel { Orientation = DataGridLayoutOrientation.Vertical, PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(260, 96), MinItemWidth = 260, MinItemHeight = 96, MinColumnSpacing = 8, MinRowSpacing = 8, MaximumRowsOrColumns = 4 }),
            new("Variable card wrap", "Measured card widths wrap into cached, bounded horizontal line records.", new DataGridWrapLayoutModel { PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(240, 92), HorizontalSpacing = 8, VerticalSpacing = 8, MaximumCachedLines = 96 }),
            new("Vertical card wrap", "Variable-size cards fill downward before wrapping into virtualized columns.", new DataGridWrapLayoutModel { Orientation = DataGridLayoutOrientation.Vertical, PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(240, 92), HorizontalSpacing = 8, VerticalSpacing = 8, MaximumCachedLines = 96 }),
            new("Custom indented cards", "Application-defined item layout and spatial navigation resolver using the same recyclable template containers.", new IndentedStackLayoutModel { Indent = 42, Spacing = 8, PresentationMode = DataGridLayoutPresentationMode.Items, ItemSizeEstimate = new Size(280, 92) })
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
                2 => "Longer content: recyclable item containers switch templates and geometry without rebuilding the item source or selection state.",
                3 => "Navigation remains semantic while the active layout resolves spatial targets.",
                _ => "Runtime switching retains one bounded algorithm session per model instance."
            };
            rows.Add(new LayoutGalleryRow
            {
                Id = index + 1,
                Category = $"Group {index % 12 + 1:00}",
                Title = $"Layout item {index + 1:n0}",
                Notes = detail,
                CardWidth = 200 + ((index % 3) * 24)
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

    public string Presentation =>
        Model is IDataGridLayoutPresentationModel { PresentationMode: DataGridLayoutPresentationMode.Items }
            ? "Item template"
            : "Rows and cells";
}
