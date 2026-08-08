// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Avalonia.Controls
{
    /// <summary>Identifies a generated tabular export representation.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedExportFormat
    {
        /// <summary>Tab-separated text.</summary>
        Text,
        /// <summary>Comma-separated values.</summary>
        Csv,
        /// <summary>GitHub-compatible Markdown table.</summary>
        Markdown,
        /// <summary>HTML table fragment.</summary>
        Html,
        /// <summary>JSON array of objects.</summary>
        Json,
        /// <summary>Simple XML row document.</summary>
        Xml,
        /// <summary>Simple YAML row sequence.</summary>
        Yaml
    }

    /// <summary>Defines generated clipboard/import safety limits.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedTransferLimits
    {
        /// <summary>Initializes transfer limits.</summary>
        public DataGridGeneratedTransferLimits(int maximumCells = 100000, int maximumCharacters = 8 * 1024 * 1024)
        {
            if (maximumCells <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCells));
            }
            if (maximumCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            }
            MaximumCells = maximumCells;
            MaximumCharacters = maximumCharacters;
        }

        /// <summary>Gets the maximum cells in one operation.</summary>
        public int MaximumCells { get; }

        /// <summary>Gets the maximum input or output character count.</summary>
        public int MaximumCharacters { get; }

        /// <summary>Gets conservative defaults.</summary>
        public static DataGridGeneratedTransferLimits Default => new(100000, 8 * 1024 * 1024);
    }

    /// <summary>Describes one generated paste or fill failure.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedTransferError<TKey>
    {
        /// <summary>Initializes a transfer error.</summary>
        public DataGridGeneratedTransferError(TKey itemKey, string columnKey, int rowOffset, int columnOffset, DataGridGeneratedEditResult result)
        {
            ItemKey = itemKey;
            ColumnKey = columnKey;
            RowOffset = rowOffset;
            ColumnOffset = columnOffset;
            Result = result;
        }

        /// <summary>Gets the item key.</summary>
        public TKey ItemKey { get; }

        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }

        /// <summary>Gets the zero-based input row offset.</summary>
        public int RowOffset { get; }

        /// <summary>Gets the zero-based input column offset.</summary>
        public int ColumnOffset { get; }

        /// <summary>Gets the edit outcome.</summary>
        public DataGridGeneratedEditResult Result { get; }
    }

    /// <summary>Summarizes a generated paste or fill operation.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedTransferResult<TKey>
    {
        internal DataGridGeneratedTransferResult(int appliedCells, bool truncated, List<DataGridGeneratedTransferError<TKey>> errors)
        {
            AppliedCells = appliedCells;
            Truncated = truncated;
            Errors = errors.AsReadOnly();
        }

        /// <summary>Gets the number of applied cells.</summary>
        public int AppliedCells { get; }

        /// <summary>Gets whether safety limits truncated the operation.</summary>
        public bool Truncated { get; }

        /// <summary>Gets structured cell errors.</summary>
        public IReadOnlyList<DataGridGeneratedTransferError<TKey>> Errors { get; }

        /// <summary>Gets whether every visited cell succeeded.</summary>
        public bool IsSuccess => !Truncated && Errors.Count == 0;
    }

    /// <summary>Exports and imports generated fields without reflection.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedClipboardController<TItem, TKey>
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly DataGridGeneratedEditController<TItem, TKey> _edits;
        private readonly IReadOnlyDictionary<string, IDataGridGeneratedEditField<TItem>> _fields;

        /// <summary>Initializes a generated clipboard controller.</summary>
        public DataGridGeneratedClipboardController(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            DataGridGeneratedEditController<TItem, TKey> edits)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _edits = edits ?? throw new ArgumentNullException(nameof(edits));
            _fields = edits.Fields;
        }

        /// <summary>Exports rows and stable column keys in the requested representation.</summary>
        public string Export(
            IReadOnlyList<TItem> rows,
            IReadOnlyList<string> columnKeys,
            DataGridGeneratedExportFormat format = DataGridGeneratedExportFormat.Csv,
            bool includeHeaders = true,
            IFormatProvider formatProvider = null,
            DataGridGeneratedTransferLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ValidateColumns(columnKeys);
            DataGridGeneratedTransferLimits effectiveLimits = limits ?? DataGridGeneratedTransferLimits.Default;
            long cellCount = (long)rows.Count * columnKeys.Count;
            if (cellCount > effectiveLimits.MaximumCells)
            {
                throw new InvalidOperationException("The generated export exceeds the configured cell limit.");
            }

            var builder = new StringBuilder(Math.Min(effectiveLimits.MaximumCharacters, Math.Max(64, (int)Math.Min(int.MaxValue, cellCount * 12))));
            IFormatProvider provider = formatProvider ?? CultureInfo.CurrentCulture;
            switch (format)
            {
                case DataGridGeneratedExportFormat.Text:
                    AppendDelimited(builder, rows, columnKeys, '\t', includeHeaders, provider);
                    break;
                case DataGridGeneratedExportFormat.Csv:
                    AppendDelimited(builder, rows, columnKeys, ',', includeHeaders, provider);
                    break;
                case DataGridGeneratedExportFormat.Markdown:
                    AppendMarkdown(builder, rows, columnKeys, includeHeaders, provider);
                    break;
                case DataGridGeneratedExportFormat.Html:
                    AppendHtml(builder, rows, columnKeys, includeHeaders, provider);
                    break;
                case DataGridGeneratedExportFormat.Json:
                    AppendJson(builder, rows, columnKeys, provider);
                    break;
                case DataGridGeneratedExportFormat.Xml:
                    AppendXml(builder, rows, columnKeys, provider);
                    break;
                case DataGridGeneratedExportFormat.Yaml:
                    AppendYaml(builder, rows, columnKeys, provider);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
            if (builder.Length > effectiveLimits.MaximumCharacters)
            {
                throw new InvalidOperationException("The generated export exceeds the configured character limit.");
            }
            return builder.ToString();
        }

        /// <summary>Parses delimited text and applies it as one undoable edit batch.</summary>
        public DataGridGeneratedTransferResult<TKey> PasteDelimited(
            IReadOnlyList<TItem> targetRows,
            IReadOnlyList<string> columnKeys,
            ReadOnlySpan<char> text,
            char delimiter = '\t',
            IFormatProvider formatProvider = null,
            DataGridGeneratedTransferLimits? limits = null)
        {
            ArgumentNullException.ThrowIfNull(targetRows);
            ValidateColumns(columnKeys);
            DataGridGeneratedTransferLimits effectiveLimits = limits ?? DataGridGeneratedTransferLimits.Default;
            if (text.Length > effectiveLimits.MaximumCharacters)
            {
                throw new InvalidOperationException("The generated paste exceeds the configured character limit.");
            }

            List<string[]> values = ParseDelimited(text, delimiter, effectiveLimits.MaximumCells, out bool truncated);
            var errors = new List<DataGridGeneratedTransferError<TKey>>();
            int applied = 0;
            _edits.BeginBatch();
            try
            {
                int rowCount = Math.Min(values.Count, targetRows.Count);
                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    TItem item = targetRows[rowIndex];
                    string[] row = values[rowIndex];
                    int columnCount = Math.Min(row.Length, columnKeys.Count);
                    for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    {
                        string columnKey = columnKeys[columnIndex];
                        DataGridGeneratedEditResult result = _edits.TrySetText(item, columnKey, row[columnIndex].AsSpan(), formatProvider);
                        if (result.IsApplied)
                        {
                            applied++;
                        }
                        else
                        {
                            errors.Add(new DataGridGeneratedTransferError<TKey>(
                                _keyAccessor.GetKey(item), columnKey, rowIndex, columnIndex, result));
                        }
                    }
                }
                _edits.CommitBatch();
            }
            catch
            {
                _edits.RollbackBatch();
                throw;
            }
            return new DataGridGeneratedTransferResult<TKey>(applied, truncated, errors);
        }

        private void ValidateColumns(IReadOnlyList<string> columnKeys)
        {
            ArgumentNullException.ThrowIfNull(columnKeys);
            for (int index = 0; index < columnKeys.Count; index++)
            {
                if (!_fields.ContainsKey(columnKeys[index]))
                {
                    throw new KeyNotFoundException("Generated edit field '" + columnKeys[index] + "' was not found.");
                }
            }
        }

        private void AppendDelimited(StringBuilder builder, IReadOnlyList<TItem> rows, IReadOnlyList<string> keys, char delimiter, bool headers, IFormatProvider provider)
        {
            if (headers)
            {
                AppendDelimitedRow(builder, keys, delimiter);
            }
            var values = new string[keys.Count];
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    values[columnIndex] = _fields[keys[columnIndex]].FormatValue(rows[rowIndex], provider);
                }
                AppendDelimitedRow(builder, values, delimiter);
            }
        }

        private static void AppendDelimitedRow(StringBuilder builder, IReadOnlyList<string> values, char delimiter)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(delimiter);
                }
                AppendQuoted(builder, values[index] ?? string.Empty, delimiter);
            }
            builder.AppendLine();
        }

        private void AppendMarkdown(StringBuilder builder, IReadOnlyList<TItem> rows, IReadOnlyList<string> keys, bool headers, IFormatProvider provider)
        {
            if (headers)
            {
                AppendMarkdownRow(builder, keys);
                builder.Append('|');
                for (int index = 0; index < keys.Count; index++)
                {
                    builder.Append(" --- |");
                }
                builder.AppendLine();
            }
            var values = new string[keys.Count];
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    values[columnIndex] = _fields[keys[columnIndex]].FormatValue(rows[rowIndex], provider);
                }
                AppendMarkdownRow(builder, values);
            }
        }

        private static void AppendMarkdownRow(StringBuilder builder, IReadOnlyList<string> values)
        {
            builder.Append('|');
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append(' ').Append((values[index] ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal)).Append(" |");
            }
            builder.AppendLine();
        }

        private void AppendHtml(StringBuilder builder, IReadOnlyList<TItem> rows, IReadOnlyList<string> keys, bool headers, IFormatProvider provider)
        {
            builder.Append("<table>");
            if (headers)
            {
                builder.Append("<thead><tr>");
                for (int index = 0; index < keys.Count; index++)
                {
                    builder.Append("<th>"); AppendXmlEscaped(builder, keys[index]); builder.Append("</th>");
                }
                builder.Append("</tr></thead>");
            }
            builder.Append("<tbody>");
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                builder.Append("<tr>");
                for (int columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    builder.Append("<td>");
                    AppendXmlEscaped(builder, _fields[keys[columnIndex]].FormatValue(rows[rowIndex], provider));
                    builder.Append("</td>");
                }
                builder.Append("</tr>");
            }
            builder.Append("</tbody></table>");
        }

        private void AppendJson(StringBuilder builder, IReadOnlyList<TItem> rows, IReadOnlyList<string> keys, IFormatProvider provider)
        {
            builder.Append('[');
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (rowIndex != 0) builder.Append(',');
                builder.Append('{');
                for (int columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    if (columnIndex != 0) builder.Append(',');
                    AppendJsonString(builder, keys[columnIndex]);
                    builder.Append(':');
                    AppendJsonString(builder, _fields[keys[columnIndex]].FormatValue(rows[rowIndex], provider));
                }
                builder.Append('}');
            }
            builder.Append(']');
        }

        private void AppendXml(StringBuilder builder, IReadOnlyList<TItem> rows, IReadOnlyList<string> keys, IFormatProvider provider)
        {
            builder.Append("<rows>");
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                builder.Append("<row>");
                for (int columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    builder.Append("<cell key=\""); AppendXmlEscaped(builder, keys[columnIndex]); builder.Append("\">");
                    AppendXmlEscaped(builder, _fields[keys[columnIndex]].FormatValue(rows[rowIndex], provider));
                    builder.Append("</cell>");
                }
                builder.Append("</row>");
            }
            builder.Append("</rows>");
        }

        private void AppendYaml(StringBuilder builder, IReadOnlyList<TItem> rows, IReadOnlyList<string> keys, IFormatProvider provider)
        {
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    builder.Append(columnIndex == 0 ? "- " : "  ");
                    AppendYamlQuoted(builder, keys[columnIndex]);
                    builder.Append(": ");
                    AppendYamlQuoted(builder, _fields[keys[columnIndex]].FormatValue(rows[rowIndex], provider));
                    builder.AppendLine();
                }
            }
        }

        private static List<string[]> ParseDelimited(ReadOnlySpan<char> text, char delimiter, int maximumCells, out bool truncated)
        {
            var rows = new List<string[]>();
            if (text.IsEmpty)
            {
                truncated = false;
                return rows;
            }
            var row = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            int cells = 0;
            truncated = false;
            for (int index = 0; index <= text.Length; index++)
            {
                if (index == text.Length && row.Count == 0 && value.Length == 0 &&
                    (text[index - 1] == '\r' || text[index - 1] == '\n'))
                {
                    break;
                }
                char current = index < text.Length ? text[index] : '\n';
                if (quoted)
                {
                    if (current == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            value.Append('"');
                            index++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        value.Append(current);
                    }
                    continue;
                }
                if (current == '"' && value.Length == 0)
                {
                    quoted = true;
                }
                else if (current == delimiter || current == '\r' || current == '\n')
                {
                    row.Add(value.ToString());
                    value.Clear();
                    cells++;
                    if (cells >= maximumCells)
                    {
                        truncated = index + 1 < text.Length;
                        rows.Add(row.ToArray());
                        return rows;
                    }
                    if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }
                    if (current != delimiter)
                    {
                        rows.Add(row.ToArray());
                        row.Clear();
                    }
                }
                else
                {
                    value.Append(current);
                }
            }
            return rows;
        }

        private static void AppendQuoted(StringBuilder builder, string value, char delimiter)
        {
            bool quote = value.IndexOfAny(new[] { delimiter, '"', '\r', '\n' }) >= 0;
            if (!quote) { builder.Append(value); return; }
            builder.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private static void AppendXmlEscaped(StringBuilder builder, string value) =>
            builder.Append((value ?? string.Empty).Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal));

        private static void AppendYamlQuoted(StringBuilder builder, string value) =>
            builder.Append('"').Append((value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)).Append('"');
    }

    /// <summary>Applies reflection-free copy, series, or custom fill values as one undo unit.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedFillController<TItem, TKey>
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly DataGridGeneratedEditController<TItem, TKey> _edits;

        /// <summary>Initializes a generated fill controller.</summary>
        public DataGridGeneratedFillController(IDataGridItemKey<TItem, TKey> keyAccessor, DataGridGeneratedEditController<TItem, TKey> edits)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _edits = edits ?? throw new ArgumentNullException(nameof(edits));
        }

        /// <summary>Copies the first row's value into the remaining rows.</summary>
        public DataGridGeneratedTransferResult<TKey> CopyDown(IReadOnlyList<TItem> rows, string columnKey, int maximumCells = 100000)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Count == 0)
            {
                return new DataGridGeneratedTransferResult<TKey>(0, false, new List<DataGridGeneratedTransferError<TKey>>());
            }
            if (!_edits.Fields.TryGetValue(columnKey, out IDataGridGeneratedEditField<TItem> field))
            {
                throw new KeyNotFoundException("Generated edit field '" + columnKey + "' was not found.");
            }
            object seed = field.GetValue(rows[0]);
            return Fill(rows, columnKey, 1, static (_, state) => state, seed, maximumCells);
        }

        /// <summary>Fills a typed series produced from a zero-based destination offset.</summary>
        public DataGridGeneratedTransferResult<TKey> Fill<TValue>(
            IReadOnlyList<TItem> rows,
            string columnKey,
            int startIndex,
            Func<int, TValue> valueFactory,
            int maximumCells = 100000)
        {
            ArgumentNullException.ThrowIfNull(valueFactory);
            return Fill(rows, columnKey, startIndex, static (index, factory) => factory(index), valueFactory, maximumCells);
        }

        private DataGridGeneratedTransferResult<TKey> Fill<TState>(
            IReadOnlyList<TItem> rows,
            string columnKey,
            int startIndex,
            Func<int, TState, object> valueFactory,
            TState state,
            int maximumCells)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (startIndex < 0 || startIndex > rows.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (maximumCells <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCells));
            var errors = new List<DataGridGeneratedTransferError<TKey>>();
            int applied = 0;
            int count = Math.Min(rows.Count - startIndex, maximumCells);
            _edits.BeginBatch();
            try
            {
                for (int offset = 0; offset < count; offset++)
                {
                    TItem item = rows[startIndex + offset];
                    DataGridGeneratedEditResult result = _edits.TrySetValue(item, columnKey, valueFactory(offset, state));
                    if (result.IsApplied) applied++;
                    else errors.Add(new DataGridGeneratedTransferError<TKey>(_keyAccessor.GetKey(item), columnKey, offset, 0, result));
                }
                _edits.CommitBatch();
            }
            catch
            {
                _edits.RollbackBatch();
                throw;
            }
            return new DataGridGeneratedTransferResult<TKey>(applied, rows.Count - startIndex > maximumCells, errors);
        }
    }
}
