// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedEditableOrderSchema",
    SchemaId = "sample/generated-editable-order/v1",
    Strict = true)]
public sealed class GeneratedEditableOrder
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Order", ColumnKey = "order-id", Width = "82", IsReadOnly = true)]
    public int OrderId { get; init; }

    [Required]
    [StringLength(16, MinimumLength = 2)]
    [DataGridColumn(
        Header = "Product",
        ColumnKey = "product",
        Width = "*",
        ParserMethod = nameof(ParseProduct),
        FormatterMethod = nameof(FormatProduct))]
    public string Product { get; set; } = string.Empty;

    [Range(1, 500)]
    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Qty", ColumnKey = "quantity", Width = "88")]
    public int Quantity { get; set; }

    [DataGridColumn(
        DataGridColumnKind.Numeric,
        Header = "Unit price",
        ColumnKey = "unit-price",
        Width = "130",
        FormatString = "C2",
        ParserMethod = nameof(ParseUnitPrice),
        FormatterMethod = nameof(FormatUnitPrice),
        ValidatorMethod = nameof(ValidateUnitPrice),
        AsyncValidatorMethod = nameof(ValidateUnitPriceAsync),
        CoerceMethod = nameof(CoerceUnitPrice),
        CanEditMethod = nameof(CanEditUnitPrice))]
    public decimal UnitPrice { get; set; }

    [DataGridColumn(
        DataGridColumnKind.Numeric,
        Header = "Discount",
        ColumnKey = "discount",
        Width = "112",
        FormatString = "P0",
        ValidatorMethod = nameof(ValidateDiscount),
        CoerceMethod = nameof(CoerceDiscount))]
    public decimal Discount { get; set; }

    [DataGridColumn(DataGridColumnKind.DatePicker, Header = "Due", ColumnKey = "due", Width = "166")]
    public DateTimeOffset Due { get; set; }

    [DataGridColumn(DataGridColumnKind.CheckBox, Header = "Locked", ColumnKey = "locked", Width = "86")]
    public bool Locked { get; set; }

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Total", ColumnKey = "total", Width = "142", FormatString = "C2", IsReadOnly = true)]
    public decimal Total => Quantity * UnitPrice * (1m - Discount);

    public static bool ParseProduct(ReadOnlySpan<char> text, IFormatProvider formatProvider, out string value)
    {
        value = text.Trim().ToString().ToUpperInvariant();
        return value.Length != 0;
    }

    public static string FormatProduct(string value, IFormatProvider formatProvider) => value;

    public static bool ParseUnitPrice(ReadOnlySpan<char> text, IFormatProvider formatProvider, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, formatProvider, out value);

    public static string FormatUnitPrice(decimal value, IFormatProvider formatProvider) =>
        value.ToString("0.00", formatProvider);

    public static string? ValidateUnitPrice(GeneratedEditableOrder item, decimal value) =>
        value <= 0m ? "Unit price must be greater than zero." : null;

    public static async ValueTask<string?> ValidateUnitPriceAsync(
        GeneratedEditableOrder item,
        decimal value,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return value > 5_000m ? "Unit price exceeds the approval threshold." : null;
    }

    public static decimal CoerceUnitPrice(GeneratedEditableOrder item, decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static bool CanEditUnitPrice(GeneratedEditableOrder item) => !item.Locked;

    public static string? ValidateDiscount(GeneratedEditableOrder item, decimal value) =>
        value is < 0m or > 0.5m ? "Discount must be between 0% and 50%." : null;

    public static decimal CoerceDiscount(GeneratedEditableOrder item, decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
