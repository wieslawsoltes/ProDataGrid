// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridClipboard;
using Avalonia.Controls.DataGridFilling;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedTransferTests
{
    [Fact]
    public void Export_uses_stable_keys_typed_formatters_and_supported_representations()
    {
        Row[] rows = [new(1, "A, B", 12.5m), new(2, "C", 3m)];
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        var clipboard = new DataGridGeneratedClipboardController<Row, int>(new RowKey(), edits);

        string csv = clipboard.Export(rows, ["name", "amount"], DataGridGeneratedExportFormat.Csv, formatProvider: CultureInfo.InvariantCulture);
        string json = clipboard.Export(rows, ["name"], DataGridGeneratedExportFormat.Json);
        string html = clipboard.Export(rows, ["name"], DataGridGeneratedExportFormat.Html);

        Assert.Equal("name,amount\n\"A, B\",12.50\nC,3.00\n", csv.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("[{\"name\":\"A, B\"},{\"name\":\"C\"}]", json);
        Assert.Contains("<td>A, B</td>", html);
    }

    [Fact]
    public void Paste_is_quoted_typed_structured_and_one_undo_batch()
    {
        Row[] rows = [new(1, "old", 1m), new(2, "old", 2m)];
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        var clipboard = new DataGridGeneratedClipboardController<Row, int>(new RowKey(), edits);

        DataGridGeneratedTransferResult<int> result = clipboard.PasteDelimited(
            rows,
            ["name", "amount"],
            "\"new, one\",10.5\nsecond,invalid".AsSpan(),
            ',',
            CultureInfo.InvariantCulture);

        Assert.Equal(3, result.AppliedCells);
        Assert.Single(result.Errors);
        Assert.Equal(DataGridGeneratedEditStatus.ParseFailed, result.Errors[0].Result.Status);
        Assert.Equal(("new, one", 10.5m), (rows[0].Name, rows[0].Amount));
        Assert.Equal(("second", 2m), (rows[1].Name, rows[1].Amount));
        Assert.True(edits.Undo());
        Assert.Equal(("old", 1m), (rows[0].Name, rows[0].Amount));
        Assert.Equal(("old", 2m), (rows[1].Name, rows[1].Amount));
    }

    [Fact]
    public void Fill_supports_copy_custom_series_limits_and_undo()
    {
        Row[] rows = [new(1, "A", 1m), new(2, "B", 2m), new(3, "C", 3m)];
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        var fill = new DataGridGeneratedFillController<Row, int>(new RowKey(), edits);

        DataGridGeneratedTransferResult<int> copied = fill.CopyDown(rows, "name");
        DataGridGeneratedTransferResult<int> series = fill.Fill(rows, "amount", 0, index => 10m + index, maximumCells: 2);

        Assert.Equal(2, copied.AppliedCells);
        Assert.Equal(("A", "A", "A"), (rows[0].Name, rows[1].Name, rows[2].Name));
        Assert.True(series.Truncated);
        Assert.Equal((10m, 11m, 3m), (rows[0].Amount, rows[1].Amount, rows[2].Amount));
        Assert.True(edits.Undo());
        Assert.Equal((1m, 2m, 3m), (rows[0].Amount, rows[1].Amount, rows[2].Amount));
    }

    [Fact]
    public void Export_enforces_cell_and_character_limits()
    {
        Row[] rows = [new(1, "A", 1m)];
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        var clipboard = new DataGridGeneratedClipboardController<Row, int>(new RowKey(), edits);

        Assert.Throws<InvalidOperationException>(() => clipboard.Export(
            rows, ["name", "amount"], limits: new DataGridGeneratedTransferLimits(1, 100)));
        Assert.Throws<InvalidOperationException>(() => clipboard.Export(
            rows, ["name"], limits: new DataGridGeneratedTransferLimits(10, 1)));
    }

    [Fact]
    public void Paste_ignores_a_terminal_line_break()
    {
        Row[] rows = [new(1, "old", 1m), new(2, "unchanged", 2m)];
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        var clipboard = new DataGridGeneratedClipboardController<Row, int>(new RowKey(), edits);

        DataGridGeneratedTransferResult<int> result = clipboard.PasteDelimited(
            rows,
            ["name"],
            "new\r\n".AsSpan());

        Assert.Equal(1, result.AppliedCells);
        Assert.Equal("new", rows[0].Name);
        Assert.Equal("unchanged", rows[1].Name);
    }

    [AvaloniaFact]
    public void Generated_clipboard_import_model_uses_column_keys_and_records_one_undo_batch()
    {
        var rows = new ObservableCollection<Row>
        {
            new(1, "old", 1m),
            new(2, "old", 2m)
        };
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        DataGridGeneratedTransferResult<int>? reported = null;
        var model = new DataGridGeneratedClipboardImportModel<Row, int>(
            new RowKey(),
            edits,
            result => reported = result,
            CultureInfo.InvariantCulture);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            ItemsSource = rows,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        var nameColumn = new DataGridTextColumn { Header = "Name", ColumnKey = "name" };
        var amountColumn = new DataGridNumericColumn { Header = "Amount", ColumnKey = "amount" };
        grid.ColumnsInternal.Add(nameColumn);
        grid.ColumnsInternal.Add(amountColumn);
        var window = new Window { Content = grid };
        window.Show();
        grid.UpdateLayout();

        try
        {
            var context = new DataGridClipboardImportContext(
                grid,
                "first\t10.5\nsecond\t20.25",
                [new DataGridCellInfo(rows[0], nameColumn, 0, 0)]);

            Assert.True(model.Paste(context));
            Assert.NotNull(reported);
            Assert.Equal(4, reported.AppliedCells);
            Assert.Equal(("first", 10.5m), (rows[0].Name, rows[0].Amount));
            Assert.Equal(("second", 20.25m), (rows[1].Name, rows[1].Amount));
            Assert.True(edits.Undo());
            Assert.Equal(("old", 1m), (rows[0].Name, rows[0].Amount));
            Assert.Equal(("old", 2m), (rows[1].Name, rows[1].Amount));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_fill_model_extrapolates_numeric_series_and_records_one_undo_batch()
    {
        var rows = new ObservableCollection<Row>
        {
            new(1, "A", 1m),
            new(2, "B", 2m),
            new(3, "C", 0m),
            new(4, "D", 0m)
        };
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        DataGridGeneratedTransferResult<int>? reported = null;
        var model = new DataGridGeneratedFillModel<Row, int>(
            new RowKey(),
            edits,
            result => reported = result);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            ItemsSource = rows,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        grid.ColumnsInternal.Add(new DataGridNumericColumn { Header = "Amount", ColumnKey = "amount" });
        var window = new Window { Content = grid };
        window.Show();
        grid.UpdateLayout();

        try
        {
            model.ApplyFill(new DataGridFillContext(
                grid,
                new DataGridCellRange(0, 1, 0, 0),
                new DataGridCellRange(0, 3, 0, 0)));

            Assert.NotNull(reported);
            Assert.Equal(2, reported.AppliedCells);
            Assert.Equal((1m, 2m, 3m, 4m), (rows[0].Amount, rows[1].Amount, rows[2].Amount, rows[3].Amount));
            Assert.True(edits.Undo());
            Assert.Equal((1m, 2m, 0m, 0m), (rows[0].Amount, rows[1].Amount, rows[2].Amount, rows[3].Amount));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_clipboard_import_model_applies_one_value_to_selection_with_a_hard_limit()
    {
        var rows = new ObservableCollection<Row>
        {
            new(1, "first", 1m),
            new(2, "second", 2m)
        };
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        DataGridGeneratedTransferResult<int>? reported = null;
        var model = new DataGridGeneratedClipboardImportModel<Row, int>(
            new RowKey(),
            edits,
            result => reported = result,
            CultureInfo.InvariantCulture,
            new DataGridGeneratedTransferLimits(maximumCells: 1, maximumCharacters: 100));
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            ItemsSource = rows,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        var nameColumn = new DataGridTextColumn { Header = "Name", ColumnKey = "name" };
        grid.ColumnsInternal.Add(nameColumn);
        var window = new Window { Content = grid };
        window.Show();
        grid.UpdateLayout();

        try
        {
            var context = new DataGridClipboardImportContext(
                grid,
                "shared",
                [
                    new DataGridCellInfo(rows[0], nameColumn, 0, 0),
                    new DataGridCellInfo(rows[1], nameColumn, 1, 0)
                ]);

            Assert.True(model.Paste(context));
            Assert.NotNull(reported);
            Assert.Equal(1, reported.AppliedCells);
            Assert.True(reported.Truncated);
            Assert.Equal(("shared", "second"), (rows[0].Name, rows[1].Name));
            Assert.True(edits.Undo());
            Assert.Equal(("first", "second"), (rows[0].Name, rows[1].Name));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_fill_model_supports_bounded_cyclic_copy_without_series()
    {
        var rows = new ObservableCollection<Row>
        {
            new(1, "A", 1m),
            new(2, "B", 2m),
            new(3, "C", 3m),
            new(4, "D", 4m)
        };
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        DataGridGeneratedTransferResult<int>? reported = null;
        var model = new DataGridGeneratedFillModel<Row, int>(
            new RowKey(),
            edits,
            result => reported = result,
            maximumCells: 2,
            useSeries: false);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            ItemsSource = rows,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", ColumnKey = "name" });
        var window = new Window { Content = grid };
        window.Show();
        grid.UpdateLayout();

        try
        {
            model.ApplyFill(new DataGridFillContext(
                grid,
                new DataGridCellRange(0, 0, 0, 0),
                new DataGridCellRange(0, 3, 0, 0)));

            Assert.NotNull(reported);
            Assert.Equal(2, reported.AppliedCells);
            Assert.True(reported.Truncated);
            Assert.Equal(new[] { "A", "A", "A", "D" }, rows.Select(static row => row.Name));
            Assert.True(edits.Undo());
            Assert.Equal(new[] { "A", "B", "C", "D" }, rows.Select(static row => row.Name));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Generated_transfer_adapter_constructors_validate_required_dependencies_and_limits()
    {
        using DataGridGeneratedEditController<Row, int> edits = CreateEdits();
        var key = new RowKey();

        Assert.Throws<ArgumentNullException>(() => new DataGridGeneratedClipboardImportModel<Row, int>(null!, edits));
        Assert.Throws<ArgumentNullException>(() => new DataGridGeneratedClipboardImportModel<Row, int>(key, null!));
        Assert.Throws<ArgumentNullException>(() => new DataGridGeneratedFillModel<Row, int>(null!, edits));
        Assert.Throws<ArgumentNullException>(() => new DataGridGeneratedFillModel<Row, int>(key, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DataGridGeneratedFillModel<Row, int>(key, edits, maximumCells: 0));
    }

    private static DataGridGeneratedEditController<Row, int> CreateEdits() =>
        new(
            new RowKey(),
            new IDataGridGeneratedEditField<Row>[]
            {
                new DataGridGeneratedEditField<Row, string>(
                    "name",
                    static row => row.Name,
                    static (row, value) => row.Name = value,
                    static (ReadOnlySpan<char> text, IFormatProvider _, out string value) => { value = text.ToString(); return true; },
                    static (value, _) => value),
                new DataGridGeneratedEditField<Row, decimal>(
                    "amount",
                    static row => row.Amount,
                    static (row, value) => row.Amount = value,
                    static (ReadOnlySpan<char> text, IFormatProvider provider, out decimal value) => decimal.TryParse(text, provider, out value),
                    static (value, provider) => value.ToString("0.00", provider))
            });

    private sealed class Row
    {
        public Row(int id, string name, decimal amount) { Id = id; Name = name; Amount = amount; }
        public int Id { get; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class RowKey : IDataGridItemKey<Row, int>
    {
        public int GetKey(Row item) => item.Id;
    }
}
