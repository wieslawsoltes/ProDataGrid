// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using ProDataGrid.SourceGeneration;
using ReactiveUI;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedEventCommandSchema",
    SchemaId = "sample/generated-event-command/v1",
    Strict = true)]
public sealed class GeneratedEventCommandRow : ReactiveObject
{
    private string _symbol = string.Empty;
    private string _desk = string.Empty;
    private decimal _price;
    private string _lastEvent = "None";

    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "ID", Order = 0, ColumnKey = "event-id", IsReadOnly = true, Width = "70")]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Symbol", Order = 1, ColumnKey = "event-symbol", Width = "*")]
    public string Symbol
    {
        get => _symbol;
        set => this.RaiseAndSetIfChanged(ref _symbol, value);
    }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Desk", Order = 2, ColumnKey = "event-desk", Width = "*")]
    public string Desk
    {
        get => _desk;
        set => this.RaiseAndSetIfChanged(ref _desk, value);
    }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Price", Order = 3, ColumnKey = "event-price", FormatString = "N2")]
    public decimal Price
    {
        get => _price;
        set => this.RaiseAndSetIfChanged(ref _price, value);
    }

    [DataGridColumn(DataGridColumnKind.Text, Header = "Last generated event", Order = 4, ColumnKey = "event-last", IsReadOnly = true, Width = "2*")]
    public string LastEvent
    {
        get => _lastEvent;
        set => this.RaiseAndSetIfChanged(ref _lastEvent, value);
    }
}
