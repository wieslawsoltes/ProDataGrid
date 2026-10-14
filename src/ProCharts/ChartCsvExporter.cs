// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ProCharts;

/// <summary>Invariant, long-form CSV export of the displayed snapshot, including OHLC and bubble channels.</summary>
public static class ChartCsvExporter
{
    /// <summary>Exports a snapshot. Text formula prefixes are neutralized by default; numeric negatives remain numeric.</summary>
    public static string Export(ChartDataSnapshot snapshot, bool protectSpreadsheetFormulas = true)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        Write(snapshot, writer, protectSpreadsheetFormulas);
        return writer.ToString();
    }
    /// <summary>Streams CSV to a caller-owned writer. Gaps/non-finite values become empty numeric fields.</summary>
    public static void Write(ChartDataSnapshot snapshot, TextWriter writer,
        bool protectSpreadsheetFormulas = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write("Series,Point,Category,X,Value,Open,High,Low,Size\r\n");
        for (int s = 0; s < snapshot.Series.Count; s++)
        {
            ChartSeriesSnapshot series = snapshot.Series[s];
            for (int i = 0; i < series.Values.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Text(writer, series.Name, protectSpreadsheetFormulas); writer.Write(',');
                writer.Write(i.ToString(CultureInfo.InvariantCulture)); writer.Write(',');
                Text(writer, i < snapshot.Categories.Count ? snapshot.Categories[i] : null, protectSpreadsheetFormulas);
                writer.Write(',');
                Number(writer, series.XValues != null && i < series.XValues.Count ? series.XValues[i] : null);
                writer.Write(','); Number(writer, series.Values[i]);
                writer.Write(','); Number(writer, At(series.OpenValues, i));
                writer.Write(','); Number(writer, At(series.HighValues, i));
                writer.Write(','); Number(writer, At(series.LowValues, i));
                writer.Write(','); Number(writer, At(series.SizeValues, i));
                writer.Write("\r\n");
            }
        }
    }
    private static double? At(System.Collections.Generic.IReadOnlyList<double?>? channel, int index)
        => channel != null && index < channel.Count ? channel[index] : null;
    private static void Number(TextWriter writer, double? value)
    {
        if (value is double finite && double.IsFinite(finite))
            writer.Write(finite.ToString("R", CultureInfo.InvariantCulture));
    }
    private static void Text(TextWriter writer, string? value, bool protect)
    {
        if (string.IsNullOrEmpty(value)) return;
        ReadOnlySpan<char> trimmed = value.AsSpan().TrimStart();
        bool formula = protect && (value[0] is '\t' or '\r' or '\n' ||
            (!trimmed.IsEmpty && trimmed[0] is '=' or '+' or '-' or '@'));
        writer.Write('"');
        if (formula) writer.Write('\'');
        foreach (char c in value)
        {
            if (c == '"') writer.Write('"');
            writer.Write(c);
        }
        writer.Write('"');
    }
}
