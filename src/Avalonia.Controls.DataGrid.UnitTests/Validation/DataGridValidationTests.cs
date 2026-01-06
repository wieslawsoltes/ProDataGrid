// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Core.Plugins;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridValidationTests
{
    static DataGridValidationTests()
    {
        EnsureValidationPlugins();
    }

    [AvaloniaFact]
    public void Invalid_edit_marks_grid_row_and_cell()
    {
        var (grid, root, item, column) = CreateTextValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var row = FindRow(grid, item);
            var textBox = Assert.IsType<TextBox>(cell.Content);

            textBox.Text = string.Empty;
            UpdateEditingElementSource(textBox);
            Assert.False(grid.CommitEdit());
            grid.UpdateLayout();

            Assert.False(grid.IsValid);
            Assert.False(cell.IsValid);
            Assert.False(row.IsValid);
            Assert.True(((IPseudoClasses)cell.Classes).Contains(":invalid"));
            Assert.True(((IPseudoClasses)row.Classes).Contains(":invalid"));
            Assert.True(DataValidationErrors.GetHasErrors(cell));

            textBox.Text = "Valid";
            Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true));
            grid.UpdateLayout();

            Assert.True(grid.IsValid);
            Assert.True(cell.IsValid);
            Assert.True(row.IsValid);
            Assert.False(((IPseudoClasses)cell.Classes).Contains(":invalid"));
            Assert.False(((IPseudoClasses)row.Classes).Contains(":invalid"));
            Assert.False(DataValidationErrors.GetHasErrors(textBox));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_edit_clears_validation_errors_on_cell()
    {
        var (grid, root, item, column) = CreateTextValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var textBox = Assert.IsType<TextBox>(cell.Content);

            textBox.Text = string.Empty;
            UpdateEditingElementSource(textBox);
            Assert.False(grid.CommitEdit());
            grid.UpdateLayout();

            Assert.True(DataValidationErrors.GetHasErrors(cell));
            Assert.False(DataValidationErrors.GetHasErrors(textBox));

            Assert.True(grid.CancelEdit(DataGridEditingUnit.Cell));
            grid.UpdateLayout();

            Assert.False(DataValidationErrors.GetHasErrors(cell));
            Assert.False(DataValidationErrors.GetHasErrors(textBox));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Warning_edit_allows_commit_and_marks_cell()
    {
        var (grid, root, item, column) = CreateWarningValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var row = FindRow(grid, item);
            var textBox = Assert.IsType<TextBox>(cell.Content);

            textBox.Text = "X";
            UpdateEditingElementSource(textBox);
            Assert.True(grid.CommitEdit());
            grid.UpdateLayout();

            Assert.True(grid.IsValid);
            Assert.True(cell.IsValid);
            Assert.True(row.IsValid);
            Assert.Equal(DataGridValidationSeverity.Warning, cell.ValidationSeverity);
            Assert.True(((IPseudoClasses)cell.Classes).Contains(":warning"));
            Assert.Equal(DataGridValidationSeverity.None, row.ValidationSeverity);
            Assert.False(((IPseudoClasses)row.Classes).Contains(":warning"));
            Assert.True(DataValidationErrors.GetHasErrors(cell));
            Assert.False(DataValidationErrors.GetHasErrors(textBox));

            var validation = cell.GetVisualDescendants().OfType<DataValidationErrors>().First();
            Assert.True(grid.TryFindResource("DataGridCellDataValidationWarningsTheme", out var warningTheme));
            Assert.Same(warningTheme, validation.Theme);

            var errors = DataValidationErrors.GetErrors(cell)?.Cast<object>().ToList();
            Assert.NotNull(errors);
            Assert.Contains(errors!, error => ErrorContainsMessage(error, "Code should be at least 3"));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Info_edit_allows_commit_and_marks_cell()
    {
        var (grid, root, item, column) = CreateInfoValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var row = FindRow(grid, item);
            var textBox = Assert.IsType<TextBox>(cell.Content);

            textBox.Text = "info";
            UpdateEditingElementSource(textBox);
            Assert.True(grid.CommitEdit());
            grid.UpdateLayout();

            Assert.True(grid.IsValid);
            Assert.True(cell.IsValid);
            Assert.True(row.IsValid);
            Assert.Equal(DataGridValidationSeverity.Info, cell.ValidationSeverity);
            Assert.True(((IPseudoClasses)cell.Classes).Contains(":info"));
            Assert.Equal(DataGridValidationSeverity.None, row.ValidationSeverity);
            Assert.False(((IPseudoClasses)row.Classes).Contains(":info"));
            Assert.True(DataValidationErrors.GetHasErrors(cell));
            Assert.False(DataValidationErrors.GetHasErrors(textBox));

            var validation = cell.GetVisualDescendants().OfType<DataValidationErrors>().First();
            Assert.True(grid.TryFindResource("DataGridCellDataValidationInfoTheme", out var infoTheme));
            Assert.Same(infoTheme, validation.Theme);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Warning_validation_persists_after_selection_change()
    {
        var (grid, root, item, column) = CreateWarningValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var textBox = Assert.IsType<TextBox>(cell.Content);

            textBox.Text = "X";
            UpdateEditingElementSource(textBox);
            Assert.True(grid.CommitEdit());
            grid.UpdateLayout();

            var descriptionColumn = grid.ColumnsInternal
                .OfType<DataGridTextColumn>()
                .First(c => c.Header?.ToString() == "Description");

            Assert.True(grid.UpdateSelectionAndCurrency(descriptionColumn.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            var refreshedCell = FindCell(grid, item, column.Index);
            var row = FindRow(grid, item);
            Assert.Equal(DataGridValidationSeverity.Warning, refreshedCell.ValidationSeverity);
            Assert.True(((IPseudoClasses)refreshedCell.Classes).Contains(":warning"));
            Assert.True(DataValidationErrors.GetHasErrors(refreshedCell));
            Assert.Equal(DataGridValidationSeverity.None, row.ValidationSeverity);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_edit_restores_warning_state_for_exception_validation()
    {
        var (grid, root, item, column) = CreateWarningValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var textBox = Assert.IsType<TextBox>(cell.Content);

            textBox.Text = "X";
            UpdateEditingElementSource(textBox);
            Assert.True(grid.CommitEdit());
            grid.UpdateLayout();

            Assert.Equal(DataGridValidationSeverity.Warning, cell.ValidationSeverity);

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var editingCell = FindCell(grid, item, column.Index);
            var editingTextBox = Assert.IsType<TextBox>(editingCell.Content);

            editingTextBox.Text = "Valid";
            UpdateEditingElementSource(editingTextBox);

            Assert.True(grid.CancelEdit(DataGridEditingUnit.Cell));
            grid.UpdateLayout();

            var restoredCell = FindCell(grid, item, column.Index);
            Assert.Equal(DataGridValidationSeverity.Warning, restoredCell.ValidationSeverity);
            Assert.True(DataValidationErrors.GetHasErrors(restoredCell));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Warning_edit_does_not_clear_existing_row_error()
    {
        var (grid, root, item, warningColumn) = CreateWarningValidationGrid();

        try
        {
            var row = FindRow(grid, item);
            var descriptionColumn = grid.ColumnsInternal
                .OfType<DataGridTextColumn>()
                .First(c => c.Header?.ToString() == "Description");
            var errorCell = FindCell(grid, item, descriptionColumn.Index);

            errorCell.IsValid = false;
            errorCell.ValidationSeverity = DataGridValidationSeverity.Error;
            errorCell.UpdatePseudoClasses();
            DataValidationErrors.SetError(errorCell, new InvalidOperationException("Existing row error."));

            row.IsValid = false;
            row.ValidationSeverity = DataGridValidationSeverity.Error;
            row.ApplyState();

            Assert.False(errorCell.IsValid);
            Assert.False(row.IsValid);

            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(warningColumn.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var warningCell = FindCell(grid, item, warningColumn.Index);
            var textBox = Assert.IsType<TextBox>(warningCell.Content);

            textBox.Text = "X";
            UpdateEditingElementSource(textBox);
            Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true));
            grid.UpdateLayout();

            Assert.False(row.IsValid);
            Assert.Equal(DataGridValidationSeverity.Error, row.ValidationSeverity);
            Assert.Equal(DataGridValidationSeverity.Warning, warningCell.ValidationSeverity);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void INotifyDataErrorInfo_preserves_DataValidationException_severity()
    {
        var (grid, root, item, column) = CreateExceptionNotifyValidationGrid();

        try
        {
            var cell = FindCell(grid, item, column.Index);

            Assert.Equal(DataGridValidationSeverity.Warning, cell.ValidationSeverity);
            Assert.True(DataValidationErrors.GetHasErrors(cell));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void INotifyDataErrorInfo_edit_clears_cell_errors_while_editing()
    {
        var (grid, root, item, column) = CreateNotifyValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            Assert.Equal(DataGridValidationSeverity.Warning, cell.ValidationSeverity);
            Assert.True(DataValidationErrors.GetHasErrors(cell));

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var editingCell = FindCell(grid, item, column.Index);
            var textBox = Assert.IsType<TextBox>(editingCell.Content);

            Assert.False(DataValidationErrors.GetHasErrors(editingCell));
            Assert.True(DataValidationErrors.GetHasErrors(textBox));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void INotifyDataErrorInfo_validation_restores_on_row_recycle()
    {
        var (grid, root, item, column) = CreateNotifyValidationGrid();

        try
        {
            var cell = FindCell(grid, item, column.Index);
            var row = FindRow(grid, item);

            Assert.Equal(DataGridValidationSeverity.Warning, cell.ValidationSeverity);
            Assert.True(((IPseudoClasses)cell.Classes).Contains(":warning"));
            Assert.True(DataValidationErrors.GetHasErrors(cell));
            Assert.Equal(DataGridValidationSeverity.None, row.ValidationSeverity);

            grid.NotifyRowRecycling(row);
            Assert.Equal(DataGridValidationSeverity.None, row.ValidationSeverity);
            Assert.False(DataValidationErrors.GetHasErrors(cell));
            grid.NotifyRowPrepared(row, item);
            grid.UpdateLayout();

            var restoredCell = FindCell(grid, item, column.Index);
            var restoredRow = FindRow(grid, item);

            Assert.Equal(DataGridValidationSeverity.Warning, restoredCell.ValidationSeverity);
            Assert.True(((IPseudoClasses)restoredCell.Classes).Contains(":warning"));
            Assert.True(DataValidationErrors.GetHasErrors(restoredCell));
            Assert.Equal(DataGridValidationSeverity.None, restoredRow.ValidationSeverity);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Grid_invalid_when_offscreen_item_has_error()
    {
        var (grid, root, errorItem, items) = CreateOffscreenErrorValidationGrid();

        try
        {
            var realizedRows = grid.GetVisualDescendants().OfType<DataGridRow>().ToList();

            Assert.True(realizedRows.Count < items.Count);
            Assert.DoesNotContain(realizedRows, row => ReferenceEquals(row.DataContext, errorItem));
            Assert.False(grid.IsValid);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_row_edit_restores_template_column_validation_for_indei()
    {
        var (grid, root, item, boundColumn, templateColumn) = CreateNotifyTemplateValidationGrid();

        try
        {
            var templateCell = FindCell(grid, item, templateColumn.Index);
            templateCell.IsValid = false;
            templateCell.ValidationSeverity = DataGridValidationSeverity.Error;
            templateCell.UpdatePseudoClasses();
            DataValidationErrors.SetError(templateCell, new InvalidOperationException("Template validation error."));

            Assert.True(DataValidationErrors.GetHasErrors(templateCell));

            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(boundColumn.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            Assert.True(grid.CancelEdit(DataGridEditingUnit.Row));
            grid.UpdateLayout();

            var restoredCell = FindCell(grid, item, templateColumn.Index);
            Assert.False(restoredCell.IsValid);
            Assert.Equal(DataGridValidationSeverity.Error, restoredCell.ValidationSeverity);
            Assert.True(DataValidationErrors.GetHasErrors(restoredCell));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void DataValidationErrors_theme_uses_grid_corner_radius()
    {
        var (grid, root, item, column) = CreateTextValidationGrid(DataGridTheme.Fluent);

        try
        {
            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, item, column.Index);
            var textBox = Assert.IsType<TextBox>(cell.Content);
            var validation = textBox.GetVisualDescendants().OfType<DataValidationErrors>().First();

            Assert.True(grid.TryFindResource("DataGridCellDataValidationErrorsTheme", out var themeResource));
            Assert.Same(themeResource, validation.Theme);

            Assert.True(grid.TryFindResource("DataGridCellCornerRadius", out var cornerResource));
            Assert.Equal((CornerRadius)cornerResource!, validation.CornerRadius);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Editable_columns_report_validation_errors()
    {
        var (grid, root, item) = CreateValidationGrid();

        try
        {
            var slot = grid.SlotFromRowIndex(0);

            foreach (var column in grid.ColumnsInternal)
            {
                if (!ColumnValidationCases.TryGetValue(column.Header?.ToString() ?? string.Empty, out var testCase))
                {
                    continue;
                }

                Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
                grid.UpdateLayout();

                Assert.True(grid.BeginEdit());
                grid.UpdateLayout();

                var cell = FindCell(grid, item, column.Index);
                var editingElement = Assert.IsAssignableFrom<Control>(cell.Content);

                testCase.SetValue(editingElement, false);
                UpdateEditingElementSource(editingElement);

                Assert.False(grid.CommitEdit());
                grid.UpdateLayout();

                Assert.False(cell.IsValid);
                Assert.True(DataValidationErrors.GetHasErrors(cell));
                Assert.False(DataValidationErrors.GetHasErrors(editingElement));

                testCase.SetValue(editingElement, true);
                UpdateEditingElementSource(editingElement);

                Assert.True(grid.CommitEdit(), $"Column '{column.Header}' should commit after a valid edit.");
                grid.UpdateLayout();

                Assert.True(cell.IsValid);
                Assert.False(DataValidationErrors.GetHasErrors(cell));
                Assert.False(DataValidationErrors.GetHasErrors(editingElement));
            }
        }
        finally
        {
            root.Close();
        }
    }

    private static readonly IReadOnlyList<string> Categories = new[] { "Hardware", "Software", "Services" };
    private static readonly IReadOnlyList<string> Statuses = new[] { "Draft", "Active", "Closed" };

    private static readonly Dictionary<string, ColumnValidationCase> ColumnValidationCases = new()
    {
        ["Name"] = new ColumnValidationCase((control, valid) =>
        {
            var textBox = (TextBox)control;
            textBox.Text = valid ? "Gamma" : string.Empty;
        }),
        ["Category"] = new ColumnValidationCase((control, valid) =>
        {
            var autoComplete = (AutoCompleteBox)control;
            autoComplete.Text = valid ? Categories[0] : "Invalid";
        }),
        ["Status"] = new ColumnValidationCase((control, valid) =>
        {
            var comboBox = (ComboBox)control;
            comboBox.Text = valid ? Statuses[1] : "Bogus";
        }),
        ["Phone"] = new ColumnValidationCase((control, valid) =>
        {
            var masked = (MaskedTextBox)control;
            masked.Text = valid ? "(555) 010-1000" : string.Empty;
        }),
        ["Price"] = new ColumnValidationCase((control, valid) =>
        {
            var numeric = (NumericUpDown)control;
            numeric.Value = valid ? 25m : 12m;
        }),
        ["Due"] = new ColumnValidationCase((control, valid) =>
        {
            var picker = (CalendarDatePicker)control;
            picker.SelectedDate = valid ? NextWeekday(DateTime.Today.AddDays(1)) : DateTime.Today.AddDays(-1);
        }),
        ["Start"] = new ColumnValidationCase((control, valid) =>
        {
            var picker = (TimePicker)control;
            picker.SelectedTime = valid ? new TimeSpan(10, 0, 0) : new TimeSpan(7, 0, 0);
        }),
        ["Rating"] = new ColumnValidationCase((control, valid) =>
        {
            var slider = (Slider)control;
            slider.Value = valid ? 3.0 : 0.5;
        }),
        ["Active"] = new ColumnValidationCase((control, valid) =>
        {
            var toggle = (ToggleSwitch)control;
            toggle.IsChecked = valid;
        }),
        ["Pinned"] = new ColumnValidationCase((control, valid) =>
        {
            var toggle = (ToggleButton)control;
            toggle.IsChecked = valid;
        }),
        ["Approved"] = new ColumnValidationCase((control, valid) =>
        {
            var checkBox = (CheckBox)control;
            checkBox.IsChecked = valid;
        }),
        ["Website"] = new ColumnValidationCase((control, valid) =>
        {
            var textBox = (TextBox)control;
            textBox.Text = valid ? "https://example.com" : "not-a-url";
        })
    };

    private static (DataGrid grid, Window root, ValidationItem item, DataGridTextColumn column) CreateTextValidationGrid(DataGridTheme theme = DataGridTheme.Simple)
    {
        var item = new ValidationItem(Categories, Statuses)
        {
            Name = "Alpha",
            Category = Categories[0],
            Status = Statuses[0],
            Phone = "(555) 010-1000",
            Price = 25m,
            DueDate = NextWeekday(DateTime.Today.AddDays(1)),
            StartTime = new TimeSpan(10, 0, 0),
            Rating = 3.0,
            IsActive = true,
            IsPinned = true,
            IsApproved = true,
            Website = "https://example.com"
        };

        var items = new ObservableCollection<ValidationItem> { item };

        var root = new Window
        {
            Width = 800,
            Height = 400
        };

        root.SetThemeStyles(theme);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = TwoWayBinding(nameof(ValidationItem.Name))
        };

        grid.ColumnsInternal.Add(nameColumn);
        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, nameColumn);
    }

    private static (DataGrid grid, Window root, WarningValidationItem item, DataGridTextColumn column) CreateWarningValidationGrid()
    {
        var item = new WarningValidationItem
        {
            Code = "Alpha",
            Description = "Primary"
        };

        var items = new ObservableCollection<WarningValidationItem> { item };

        var root = new Window
        {
            Width = 600,
            Height = 300
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var codeColumn = new DataGridTextColumn
        {
            Header = "Code",
            Binding = TwoWayBinding(nameof(WarningValidationItem.Code))
        };

        grid.ColumnsInternal.Add(codeColumn);
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Description",
            Binding = TwoWayBinding(nameof(WarningValidationItem.Description))
        });
        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, codeColumn);
    }

    private static (DataGrid grid, Window root, InfoValidationItem item, DataGridTextColumn column) CreateInfoValidationGrid()
    {
        var item = new InfoValidationItem
        {
            Status = "OK"
        };

        var items = new ObservableCollection<InfoValidationItem> { item };

        var root = new Window
        {
            Width = 600,
            Height = 300
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var statusColumn = new DataGridTextColumn
        {
            Header = "Status",
            Binding = TwoWayBinding(nameof(InfoValidationItem.Status))
        };

        grid.ColumnsInternal.Add(statusColumn);
        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, statusColumn);
    }

    private static (DataGrid grid, Window root, NotifyValidationItem item, DataGridTextColumn column) CreateNotifyValidationGrid()
    {
        var item = new NotifyValidationItem
        {
            Code = "X"
        };

        var items = new ObservableCollection<NotifyValidationItem> { item };

        var root = new Window
        {
            Width = 600,
            Height = 300
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var codeColumn = new DataGridTextColumn
        {
            Header = "Code",
            Binding = TwoWayBinding(nameof(NotifyValidationItem.Code))
        };

        grid.ColumnsInternal.Add(codeColumn);
        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, codeColumn);
    }

    private static (DataGrid grid, Window root, MixedValidationItem errorItem, IReadOnlyList<MixedValidationItem> items) CreateOffscreenErrorValidationGrid()
    {
        var items = new ObservableCollection<MixedValidationItem>();

        for (var i = 0; i < 30; i++)
        {
            var item = new MixedValidationItem();
            if (i < 29)
            {
                item.ErrorValue = "Ok";
            }
            items.Add(item);
        }

        var errorItem = items[^1];

        var root = new Window
        {
            Width = 600,
            Height = 140
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            Height = 80
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Error",
            Binding = TwoWayBinding(nameof(MixedValidationItem.ErrorValue))
        });

        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, errorItem, items);
    }

    private static (DataGrid grid, Window root, EditableNotifyValidationItem item, DataGridTextColumn boundColumn, DataGridTemplateColumn templateColumn) CreateNotifyTemplateValidationGrid()
    {
        var item = new EditableNotifyValidationItem
        {
            Code = "Valid"
        };

        var items = new ObservableCollection<EditableNotifyValidationItem> { item };

        var root = new Window
        {
            Width = 600,
            Height = 200
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var boundColumn = new DataGridTextColumn
        {
            Header = "Code",
            Binding = TwoWayBinding(nameof(EditableNotifyValidationItem.Code))
        };

        var templateColumn = new DataGridTemplateColumn
        {
            Header = "Template",
            CellTemplate = new FuncDataTemplate<EditableNotifyValidationItem>((_, _) => new TextBlock { Text = "Template" })
        };

        grid.ColumnsInternal.Add(boundColumn);
        grid.ColumnsInternal.Add(templateColumn);

        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, boundColumn, templateColumn);
    }

    private static (DataGrid grid, Window root, MixedValidationItem item, DataGridTextColumn errorColumn, DataGridTextColumn warningColumn) CreateMixedValidationGrid()
    {
        var item = new MixedValidationItem
        {
            WarningValue = "Okay"
        };

        var items = new ObservableCollection<MixedValidationItem> { item };

        var root = new Window
        {
            Width = 600,
            Height = 300
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var errorColumn = new DataGridTextColumn
        {
            Header = "Error",
            Binding = TwoWayBinding(nameof(MixedValidationItem.ErrorValue))
        };

        var warningColumn = new DataGridTextColumn
        {
            Header = "Warning",
            Binding = TwoWayBinding(nameof(MixedValidationItem.WarningValue))
        };

        grid.ColumnsInternal.Add(errorColumn);
        grid.ColumnsInternal.Add(warningColumn);
        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, errorColumn, warningColumn);
    }

    private static (DataGrid grid, Window root, ExceptionNotifyValidationItem item, DataGridTextColumn column) CreateExceptionNotifyValidationGrid()
    {
        var item = new ExceptionNotifyValidationItem
        {
            Code = "X"
        };

        var items = new ObservableCollection<ExceptionNotifyValidationItem> { item };

        var root = new Window
        {
            Width = 600,
            Height = 300
        };

        root.SetThemeStyles(DataGridTheme.Simple);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        var codeColumn = new DataGridTextColumn
        {
            Header = "Code",
            Binding = TwoWayBinding(nameof(ExceptionNotifyValidationItem.Code))
        };

        grid.ColumnsInternal.Add(codeColumn);
        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item, codeColumn);
    }

    private static (DataGrid grid, Window root, ValidationItem item) CreateValidationGrid()
    {
        var item = new ValidationItem(Categories, Statuses)
        {
            Name = "Alpha",
            Category = Categories[0],
            Status = Statuses[1],
            Phone = "(555) 010-1000",
            Price = 25m,
            DueDate = NextWeekday(DateTime.Today.AddDays(1)),
            StartTime = new TimeSpan(10, 0, 0),
            Rating = 3.0,
            IsActive = true,
            IsPinned = true,
            IsApproved = true,
            Website = "https://example.com"
        };

        var items = new ObservableCollection<ValidationItem> { item };

        var root = new Window
        {
            Width = 1200,
            Height = 400
        };

        root.SetThemeStyles(DataGridTheme.Fluent);

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = TwoWayBinding(nameof(ValidationItem.Name))
        });

        grid.ColumnsInternal.Add(new DataGridAutoCompleteColumn
        {
            Header = "Category",
            Binding = TwoWayBinding(nameof(ValidationItem.Category)),
            ItemsSource = Categories,
            FilterMode = AutoCompleteFilterMode.Contains,
            MinimumPrefixLength = 1
        });

        grid.ColumnsInternal.Add(new DataGridComboBoxColumn
        {
            Header = "Status",
            ItemsSource = Statuses,
            IsEditable = true,
            TextBinding = TwoWayBinding(nameof(ValidationItem.Status))
        });

        grid.ColumnsInternal.Add(new DataGridMaskedTextColumn
        {
            Header = "Phone",
            Binding = TwoWayBinding(nameof(ValidationItem.Phone)),
            Mask = "(000) 000-0000"
        });

        grid.ColumnsInternal.Add(new DataGridNumericColumn
        {
            Header = "Price",
            Binding = TwoWayBinding(nameof(ValidationItem.Price)),
            Minimum = 0,
            Maximum = 500,
            Increment = 1
        });

        grid.ColumnsInternal.Add(new DataGridDatePickerColumn
        {
            Header = "Due",
            Binding = TwoWayBinding(nameof(ValidationItem.DueDate)),
            SelectedDateFormat = CalendarDatePickerFormat.Short
        });

        grid.ColumnsInternal.Add(new DataGridTimePickerColumn
        {
            Header = "Start",
            Binding = TwoWayBinding(nameof(ValidationItem.StartTime)),
            ClockIdentifier = "24HourClock"
        });

        grid.ColumnsInternal.Add(new DataGridSliderColumn
        {
            Header = "Rating",
            Binding = TwoWayBinding(nameof(ValidationItem.Rating)),
            Minimum = 0,
            Maximum = 5,
            TickFrequency = 0.5,
            IsSnapToTickEnabled = true
        });

        grid.ColumnsInternal.Add(new DataGridToggleSwitchColumn
        {
            Header = "Active",
            Binding = TwoWayBinding(nameof(ValidationItem.IsActive))
        });

        grid.ColumnsInternal.Add(new DataGridToggleButtonColumn
        {
            Header = "Pinned",
            Binding = TwoWayBinding(nameof(ValidationItem.IsPinned))
        });

        grid.ColumnsInternal.Add(new DataGridCheckBoxColumn
        {
            Header = "Approved",
            Binding = TwoWayBinding(nameof(ValidationItem.IsApproved))
        });

        grid.ColumnsInternal.Add(new DataGridHyperlinkColumn
        {
            Header = "Website",
            Binding = TwoWayBinding(nameof(ValidationItem.Website)),
            ContentBinding = TwoWayBinding(nameof(ValidationItem.Website))
        });

        root.Content = grid;
        root.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        return (grid, root, item);
    }

    private static DataGridCell FindCell(DataGrid grid, object item, int columnIndex)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(cell => cell.OwningColumn?.Index == columnIndex && ReferenceEquals(cell.DataContext, item));
    }

    private static DataGridRow FindRow(DataGrid grid, object item)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(row => ReferenceEquals(row.DataContext, item));
    }

    private static Binding TwoWayBinding(string path)
    {
        return new Binding(path)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
    }

    private static void UpdateEditingElementSource(Control editingElement)
    {
        switch (editingElement)
        {
            case MaskedTextBox masked:
                BindingOperations.GetBindingExpressionBase(masked, MaskedTextBox.TextProperty)?.UpdateSource();
                break;
            case TextBox textBox:
                BindingOperations.GetBindingExpressionBase(textBox, TextBox.TextProperty)?.UpdateSource();
                break;
            case AutoCompleteBox autoComplete:
                BindingOperations.GetBindingExpressionBase(autoComplete, AutoCompleteBox.TextProperty)?.UpdateSource();
                break;
            case ComboBox comboBox:
                BindingOperations.GetBindingExpressionBase(comboBox, ComboBox.TextProperty)?.UpdateSource();
                BindingOperations.GetBindingExpressionBase(comboBox, SelectingItemsControl.SelectedItemProperty)?.UpdateSource();
                BindingOperations.GetBindingExpressionBase(comboBox, SelectingItemsControl.SelectedValueProperty)?.UpdateSource();
                break;
            case NumericUpDown numeric:
                BindingOperations.GetBindingExpressionBase(numeric, NumericUpDown.ValueProperty)?.UpdateSource();
                break;
            case CalendarDatePicker datePicker:
                BindingOperations.GetBindingExpressionBase(datePicker, CalendarDatePicker.SelectedDateProperty)?.UpdateSource();
                break;
            case TimePicker timePicker:
                BindingOperations.GetBindingExpressionBase(timePicker, TimePicker.SelectedTimeProperty)?.UpdateSource();
                break;
            case Slider slider:
                BindingOperations.GetBindingExpressionBase(slider, Slider.ValueProperty)?.UpdateSource();
                break;
            case ToggleSwitch toggleSwitch:
                BindingOperations.GetBindingExpressionBase(toggleSwitch, ToggleSwitch.IsCheckedProperty)?.UpdateSource();
                break;
            case ToggleButton toggleButton:
                BindingOperations.GetBindingExpressionBase(toggleButton, ToggleButton.IsCheckedProperty)?.UpdateSource();
                break;
        }
    }

    private static void EnsureValidationPlugins()
    {
        var validators = BindingPlugins.DataValidators;

        if (!validators.Any(plugin => plugin is ExceptionValidationPlugin))
        {
            validators.Add(new ExceptionValidationPlugin());
        }

        if (!validators.Any(plugin => plugin is IndeiValidationPlugin))
        {
            validators.Add(new IndeiValidationPlugin());
        }
    }

    private static bool ErrorContainsMessage(object? error, string message)
    {
        if (error is null)
        {
            return false;
        }

        if (error is DataGridValidationResult result)
        {
            return result.Message.Contains(message, StringComparison.Ordinal);
        }

        if (error is DataValidationException dataValidationException)
        {
            return ErrorContainsMessage(dataValidationException.ErrorData, message);
        }

        if (error is AggregateException aggregateException)
        {
            foreach (var inner in aggregateException.InnerExceptions)
            {
                if (ErrorContainsMessage(inner, message))
                {
                    return true;
                }
            }
        }

        if (error is Exception exception)
        {
            return exception.Message.Contains(message, StringComparison.Ordinal);
        }

        if (error is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (ErrorContainsMessage(item, message))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static DateTime NextWeekday(DateTime date)
    {
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private sealed record ColumnValidationCase(Action<Control, bool> SetValue);

    private sealed class ValidationItem : INotifyPropertyChanged
    {
        private readonly HashSet<string> _categories;
        private readonly HashSet<string> _statuses;
        private string _name = string.Empty;
        private string? _category;
        private string? _status;
        private string? _phone;
        private decimal _price;
        private DateTime? _dueDate;
        private TimeSpan? _startTime;
        private double _rating;
        private bool _isActive;
        private bool _isPinned;
        private bool _isApproved;
        private string? _website;

        public ValidationItem(IEnumerable<string> categories, IEnumerable<string> statuses)
        {
            _categories = new HashSet<string>(categories);
            _statuses = new HashSet<string>(statuses);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set
            {
                RequireText(value, "Name is required.");
                SetProperty(ref _name, value);
            }
        }

        public string? Category
        {
            get => _category;
            set
            {
                RequireChoice(value, _categories, "Category must match the suggestions list.");
                SetProperty(ref _category, value);
            }
        }

        public string? Status
        {
            get => _status;
            set
            {
                RequireChoice(value, _statuses, "Status must be one of the defined options.");
                SetProperty(ref _status, value);
            }
        }

        public string? Phone
        {
            get => _phone;
            set
            {
                RequireText(value, "Phone is required.");
                if (value!.Count(char.IsDigit) != 10)
                {
                    throw new DataValidationException("Phone must include 10 digits.");
                }

                SetProperty(ref _phone, value);
            }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                if (value < 10m || value % 5m != 0)
                {
                    throw new DataValidationException("Price must be at least 10 and in increments of 5.");
                }

                SetProperty(ref _price, value);
            }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                RequireValue(value, "Due date is required.");
                var date = value!.Value.Date;
                if (date < DateTime.Today)
                {
                    throw new DataValidationException("Due date must be today or later.");
                }

                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    throw new DataValidationException("Due date cannot fall on a weekend.");
                }

                SetProperty(ref _dueDate, value);
            }
        }

        public TimeSpan? StartTime
        {
            get => _startTime;
            set
            {
                RequireValue(value, "Start time is required.");
                var time = value!.Value;
                if (time < new TimeSpan(9, 0, 0) || time > new TimeSpan(17, 0, 0))
                {
                    throw new DataValidationException("Start time must be between 09:00 and 17:00.");
                }

                SetProperty(ref _startTime, value);
            }
        }

        public double Rating
        {
            get => _rating;
            set
            {
                if (value < 1.0 || value > 4.5)
                {
                    throw new DataValidationException("Rating must be between 1.0 and 4.5.");
                }

                SetProperty(ref _rating, value);
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (!value)
                {
                    throw new DataValidationException("Active must stay enabled.");
                }

                SetProperty(ref _isActive, value);
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (!value)
                {
                    throw new DataValidationException("Pinned must stay enabled.");
                }

                SetProperty(ref _isPinned, value);
            }
        }

        public bool IsApproved
        {
            get => _isApproved;
            set
            {
                if (!value)
                {
                    throw new DataValidationException("Approval is required.");
                }

                SetProperty(ref _isApproved, value);
            }
        }

        public string? Website
        {
            get => _website;
            set
            {
                RequireText(value, "Website is required.");
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new DataValidationException("Website must be a valid http/https URL.");
                }

                SetProperty(ref _website, value);
            }
        }

        private static void RequireValue<T>(T? value, string message) where T : struct
        {
            if (!value.HasValue)
            {
                throw new DataValidationException(message);
            }
        }

        private static void RequireText(string? value, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DataValidationException(message);
            }
        }

        private static void RequireChoice(string? value, HashSet<string> allowed, string message)
        {
            if (string.IsNullOrWhiteSpace(value) || !allowed.Contains(value))
            {
                throw new DataValidationException(message);
            }
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class WarningValidationItem : INotifyPropertyChanged
    {
        private string _code = string.Empty;
        private string? _description;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Code
        {
            get => _code;
            set
            {
                if (!SetProperty(ref _code, value))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
                {
                    throw new DataValidationException(new DataGridValidationResult("Code should be at least 3 characters.", DataGridValidationSeverity.Warning));
                }
            }
        }

        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class InfoValidationItem : INotifyPropertyChanged
    {
        private string _status = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Status
        {
            get => _status;
            set
            {
                if (!SetProperty(ref _status, value))
                {
                    return;
                }

                if (string.Equals(value, "info", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DataValidationException(new DataGridValidationResult("Status is informational.", DataGridValidationSeverity.Info));
                }
            }
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class NotifyValidationItem : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private string _code = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Code
        {
            get => _code;
            set
            {
                if (SetProperty(ref _code, value))
                {
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Code)));
                }
            }
        }

        public bool HasErrors => string.IsNullOrWhiteSpace(_code) || _code.Length < 3;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(Code))
            {
                if (string.IsNullOrWhiteSpace(_code) || _code.Length < 3)
                {
                    return new[]
                    {
                        new DataGridValidationResult("Code should be at least 3 characters.", DataGridValidationSeverity.Warning)
                    };
                }
            }

            return Array.Empty<object>();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class EditableNotifyValidationItem : INotifyPropertyChanged, INotifyDataErrorInfo, IEditableObject
    {
        private string _code = string.Empty;
        private string? _originalCode;
        private bool _isEditing;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Code
        {
            get => _code;
            set
            {
                if (SetProperty(ref _code, value))
                {
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Code)));
                }
            }
        }

        public bool HasErrors => string.IsNullOrWhiteSpace(_code) || _code.Length < 3;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(Code))
            {
                if (string.IsNullOrWhiteSpace(_code) || _code.Length < 3)
                {
                    return new[]
                    {
                        new DataGridValidationResult("Code should be at least 3 characters.", DataGridValidationSeverity.Warning)
                    };
                }
            }

            return Array.Empty<object>();
        }

        public void BeginEdit()
        {
            if (_isEditing)
            {
                return;
            }

            _originalCode = _code;
            _isEditing = true;
        }

        public void CancelEdit()
        {
            if (!_isEditing)
            {
                return;
            }

            Code = _originalCode ?? string.Empty;
            _originalCode = null;
            _isEditing = false;
        }

        public void EndEdit()
        {
            _originalCode = null;
            _isEditing = false;
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class MixedValidationItem : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<object>> _errors = new();
        private string _errorValue = string.Empty;
        private string _warningValue = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public MixedValidationItem()
        {
            SetError(nameof(ErrorValue),
                new DataGridValidationResult("Error value is required.", DataGridValidationSeverity.Error));
        }

        public string ErrorValue
        {
            get => _errorValue;
            set
            {
                if (!SetProperty(ref _errorValue, value))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    SetError(nameof(ErrorValue),
                        new DataGridValidationResult("Error value is required.", DataGridValidationSeverity.Error));
                }
                else
                {
                    SetError(nameof(ErrorValue), null);
                }
            }
        }

        public string WarningValue
        {
            get => _warningValue;
            set
            {
                if (!SetProperty(ref _warningValue, value))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
                {
                    throw new DataValidationException(
                        new DataGridValidationResult("Warning value should be at least 3 characters.", DataGridValidationSeverity.Warning));
                }
            }
        }

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (propertyName is { } && _errors.TryGetValue(propertyName, out var errorList))
            {
                return errorList;
            }

            return Array.Empty<object>();
        }

        private void SetError(string propertyName, DataGridValidationResult? error)
        {
            if (error == null)
            {
                if (_errors.Remove(propertyName))
                {
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                }
                return;
            }

            if (_errors.TryGetValue(propertyName, out var errorList))
            {
                errorList.Clear();
                errorList.Add(error);
            }
            else
            {
                _errors.Add(propertyName, new List<object> { error });
            }

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class ExceptionNotifyValidationItem : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private string _code = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string Code
        {
            get => _code;
            set
            {
                if (SetProperty(ref _code, value))
                {
                    ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Code)));
                }
            }
        }

        public bool HasErrors => string.IsNullOrWhiteSpace(_code) || _code.Length < 3;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(Code))
            {
                if (string.IsNullOrWhiteSpace(_code) || _code.Length < 3)
                {
                    return new[]
                    {
                        new DataValidationException(
                            new DataGridValidationResult("Code should be at least 3 characters.", DataGridValidationSeverity.Warning))
                    };
                }
            }

            return Array.Empty<object>();
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
