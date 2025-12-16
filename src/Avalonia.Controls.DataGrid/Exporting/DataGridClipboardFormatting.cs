// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Utilities;

namespace Avalonia.Controls
{
    internal static class DataGridClipboardFormatting
    {
        internal static string BuildDelimitedText(IReadOnlyList<DataGridRowClipboardEventArgs> rows, char delimiter, bool quoteAlways)
        {
            var builder = StringBuilderCache.Acquire();
            foreach (var row in rows)
            {
                AppendDelimitedRow(row, delimiter, quoteAlways, builder);
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static void AppendDelimitedRow(DataGridRowClipboardEventArgs args, char delimiter, bool quoteAlways, StringBuilder builder)
        {
            var cells = args.ClipboardRowContent;
            var lastCellIndex = cells.Count - 1;

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var content = cell.Content?.ToString() ?? string.Empty;

                if (quoteAlways)
                {
                    content = content.Replace("\"", "\"\"");
                    builder.Append('"').Append(content).Append('"');
                }
                else
                {
                    builder.Append(EscapeForDelimited(content, delimiter));
                }

                builder.Append(i == lastCellIndex ? "\r\n" : delimiter);
            }

            if (cells.Count == 0)
            {
                builder.Append("\r\n");
            }
        }

        private static string EscapeForDelimited(string content, char delimiter)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            var needsQuotes = content.IndexOfAny(new[] { '"', '\r', '\n', delimiter }) >= 0;
            var escaped = needsQuotes ? content.Replace("\"", "\"\"") : content;
            return needsQuotes ? $"\"{escaped}\"" : escaped;
        }

        internal static string BuildHtml(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            var table = BuildHtmlTable(rows);

            if (table.Length == 0)
            {
                return string.Empty;
            }

            return BuildHtmlDocument(table);
        }

        internal static string BuildJson(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            var hasHeader = rows[0].IsColumnHeadersRow;
            var headers = hasHeader ? rows[0].ClipboardRowContent : null;
            var startIndex = hasHeader ? 1 : 0;

            var builder = StringBuilderCache.Acquire();
            builder.Append('[');

            for (var i = startIndex; i < rows.Count; i++)
            {
                var row = rows[i];
                var cells = row.ClipboardRowContent;

                if (headers is not null && headers.Count > 0)
                {
                    builder.Append('{');
                    for (var c = 0; c < headers.Count; c++)
                    {
                        var header = headers[c].Content?.ToString() ?? $"Column{c + 1}";
                        var value = c < cells.Count ? cells[c].Content?.ToString() ?? string.Empty : string.Empty;
                        builder.Append('"').Append(EscapeJson(header)).Append("\":\"").Append(EscapeJson(value)).Append('"');
                        if (c < headers.Count - 1)
                        {
                            builder.Append(',');
                        }
                    }

                    builder.Append('}');
                }
                else
                {
                    builder.Append('[');
                    for (var c = 0; c < cells.Count; c++)
                    {
                        var value = cells[c].Content?.ToString() ?? string.Empty;
                        builder.Append('"').Append(EscapeJson(value)).Append('"');
                        if (c < cells.Count - 1)
                        {
                            builder.Append(',');
                        }
                    }

                    builder.Append(']');
                }

                if (i < rows.Count - 1)
                {
                    builder.Append(',');
                }
            }

            builder.Append(']');
            return StringBuilderCache.GetStringAndRelease(builder);
        }

        internal static bool TryBuildHtmlPayloads(
            IReadOnlyList<DataGridRowClipboardEventArgs> rows,
            out string html,
            out string cfHtml)
        {
            var table = BuildHtmlTable(rows);

            if (table.Length == 0)
            {
                html = string.Empty;
                cfHtml = string.Empty;
                return false;
            }

            html = BuildHtmlDocument(table);
            cfHtml = BuildCfHtmlDocument(table);
            return true;
        }

        private static void AppendHtmlRow(DataGridRowClipboardEventArgs args, StringBuilder builder)
        {
            var cells = args.ClipboardRowContent;
            if (cells.Count == 0)
            {
                builder.Append("<tr><td>&nbsp;</td></tr>");
                return;
            }

            builder.Append("<tr>");
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var tag = args.IsColumnHeadersRow ? "th" : "td";
                builder.Append('<').Append(tag).Append('>');

                if (cell.Content is null)
                {
                    builder.Append("&nbsp;");
                }
                else
                {
                    AppendHtmlEncoded(cell.Content.ToString() ?? string.Empty, builder);
                }

                builder.Append("</").Append(tag).Append('>');
            }

            builder.Append("</tr>");
        }

        private static string BuildHtmlTable(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            var builder = StringBuilderCache.Acquire();
            builder.Append("<table>");

            foreach (var row in rows)
            {
                AppendHtmlRow(row, builder);
            }

            builder.Append("</table>");
            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static void AppendHtmlEncoded(string text, StringBuilder builder)
        {
            var prevCh = '\0';
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                switch (ch)
                {
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '"':
                        builder.Append("&quot;");
                        break;
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case ' ':
                        builder.Append(prevCh == ' ' ? "&nbsp;" : " ");
                        break;
                    case '\r':
                        break;
                    case '\n':
                        builder.Append("<br>");
                        break;
                    default:
                        if (ch >= 160 && ch < 256)
                        {
                            builder.Append("&#").Append(((int)ch).ToString(CultureInfo.InvariantCulture)).Append(';');
                        }
                        else
                        {
                            builder.Append(ch);
                        }

                        break;
                }

                prevCh = ch;
            }
        }

        private static string BuildHtmlDocument(string fragment)
        {
            var builder = StringBuilderCache.Acquire();
            builder.Append("<html><body>");
            builder.Append(fragment);
            builder.Append("</body></html>");
            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static string BuildCfHtmlDocument(string fragment)
        {
            const string htmlPrefix = "<html><body><!--StartFragment-->";
            const string htmlSuffix = "<!--EndFragment--></body></html>";

            var bodyBuilder = StringBuilderCache.Acquire();
            bodyBuilder.Append(htmlPrefix);
            bodyBuilder.Append(fragment);
            bodyBuilder.Append(htmlSuffix);

            var body = StringBuilderCache.GetStringAndRelease(bodyBuilder);
            const string placeholder = "00000000";

            var headerTemplate =
                $"Version:1.0\r\n" +
                $"StartHTML:{placeholder}\r\n" +
                $"EndHTML:{placeholder}\r\n" +
                $"StartFragment:{placeholder}\r\n" +
                $"EndFragment:{placeholder}\r\n";

            var startHtml = Encoding.UTF8.GetByteCount(headerTemplate);
            var startFragment = startHtml + Encoding.UTF8.GetByteCount(htmlPrefix);
            var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            var endHtml = startHtml + Encoding.UTF8.GetByteCount(body);

            var header = string.Format(
                CultureInfo.InvariantCulture,
                "Version:1.0\r\nStartHTML:{0:D8}\r\nEndHTML:{1:D8}\r\nStartFragment:{2:D8}\r\nEndFragment:{3:D8}\r\n",
                startHtml,
                endHtml,
                startFragment,
                endFragment);

            return header + body;
        }

        internal static string BuildMarkdown(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            var builder = StringBuilderCache.Acquire();
            var startIndex = 0;

            if (rows[0].IsColumnHeadersRow)
            {
                AppendMarkdownRow(rows[0], builder);
                AppendMarkdownSeparator(rows[0].ClipboardRowContent.Count, builder);
                startIndex = 1;
            }

            for (var i = startIndex; i < rows.Count; i++)
            {
                AppendMarkdownRow(rows[i], builder);
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static void AppendMarkdownRow(DataGridRowClipboardEventArgs row, StringBuilder builder)
        {
            var cells = row.ClipboardRowContent;
            builder.Append('|');

            if (cells.Count == 0)
            {
                builder.AppendLine(" |");
                return;
            }

            foreach (var cell in cells)
            {
                var text = cell.Content?.ToString() ?? string.Empty;
                builder.Append(text.Replace("|", "\\|")).Append('|');
            }

            builder.AppendLine();
        }

        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = StringBuilderCache.Acquire();

            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static void AppendMarkdownSeparator(int cellCount, StringBuilder builder)
        {
            if (cellCount <= 0)
            {
                return;
            }

            builder.Append('|');
            for (var i = 0; i < cellCount; i++)
            {
                builder.Append("---|");
            }
            builder.AppendLine();
        }

        internal static string BuildXml(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            var builder = StringBuilderCache.Acquire();
            builder.Append("<rows>");

            foreach (var row in rows)
            {
                AppendXmlRow(row, builder);
            }

            builder.Append("</rows>");
            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static void AppendXmlRow(DataGridRowClipboardEventArgs row, StringBuilder builder)
        {
            var tag = row.IsColumnHeadersRow ? "header" : "row";
            builder.Append('<').Append(tag).Append('>');
            foreach (var cell in row.ClipboardRowContent)
            {
                builder.Append("<cell>");
                AppendXmlEncoded(cell.Content?.ToString() ?? string.Empty, builder);
                builder.Append("</cell>");
            }
            builder.Append("</").Append(tag).Append('>');
        }

        private static void AppendXmlEncoded(string text, StringBuilder builder)
        {
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '"':
                        builder.Append("&quot;");
                        break;
                    case '\'':
                        builder.Append("&apos;");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }
        }

        internal static string BuildYaml(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            var builder = StringBuilderCache.Acquire();
            builder.AppendLine("rows:");

            foreach (var row in rows)
            {
                builder.AppendLine("- cells:");
                foreach (var cell in row.ClipboardRowContent)
                {
                    var value = cell.Content?.ToString() ?? string.Empty;
                    builder.Append("  - ").AppendLine(EscapeYaml(value));
                }
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static string EscapeYaml(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            if (value.IndexOfAny(new[] { ':', '-', '#', '{', '}', '[', ']', ',', '&', '*', '!', '|', '>', '\'', '"', '%', '@', '`' }) >= 0 ||
                value.IndexOf('\n') >= 0)
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            return value;
        }
    }
}
