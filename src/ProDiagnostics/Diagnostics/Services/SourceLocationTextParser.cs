using System;
using System.IO;

namespace Avalonia.Diagnostics.Services
{
    internal static class SourceLocationTextParser
    {
        public static bool TryParse(string? text, out SourceDocumentLocation location)
        {
            location = default!;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();

            // Strip optional prefixes like "XAML: " / "C#: ".
            var labelSeparator = trimmed.IndexOf(": ", StringComparison.Ordinal);
            if (labelSeparator > 0)
            {
                var prefix = trimmed.Substring(0, labelSeparator);
                if (prefix.IndexOf(Path.DirectorySeparatorChar) < 0 &&
                    prefix.IndexOf(Path.AltDirectorySeparatorChar) < 0)
                {
                    trimmed = trimmed.Substring(labelSeparator + 2);
                }
            }

            var methodName = string.Empty;
            var methodStart = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
            if (methodStart > 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                methodName = trimmed.Substring(methodStart + 2, trimmed.Length - methodStart - 3);
                trimmed = trimmed.Substring(0, methodStart).TrimEnd();
            }

            if (!TrySplitLineAndColumn(trimmed, out var filePath, out var line, out var column))
            {
                return false;
            }

            filePath = filePath.Trim().Trim('"');
            if (filePath.Length == 0)
            {
                return false;
            }

            if (line <= 0)
            {
                line = 1;
            }

            if (column < 0)
            {
                column = 0;
            }

            location = new SourceDocumentLocation(filePath, line, methodName, column);
            return true;
        }

        public static bool IsSameDocument(string pathA, string pathB)
        {
            if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB))
            {
                return false;
            }

            return string.Equals(NormalizePath(pathA), NormalizePath(pathB), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static bool TrySplitLineAndColumn(string text, out string filePath, out int line, out int column)
        {
            filePath = text;
            line = 0;
            column = 0;

            var end = text.Length - 1;
            while (end >= 0 && char.IsWhiteSpace(text[end]))
            {
                end--;
            }

            if (end < 0)
            {
                return false;
            }

            var cursor = end;
            while (cursor >= 0 && char.IsDigit(text[cursor]))
            {
                cursor--;
            }

            if (cursor < 0 || cursor == end || text[cursor] != ':')
            {
                return true;
            }

            if (!int.TryParse(text.Substring(cursor + 1, end - cursor), out var trailing))
            {
                return true;
            }

            var beforeTrailing = cursor - 1;
            while (beforeTrailing >= 0 && char.IsWhiteSpace(text[beforeTrailing]))
            {
                beforeTrailing--;
            }

            var secondCursor = beforeTrailing;
            while (secondCursor >= 0 && char.IsDigit(text[secondCursor]))
            {
                secondCursor--;
            }

            if (secondCursor >= 0 && secondCursor < beforeTrailing && text[secondCursor] == ':')
            {
                if (int.TryParse(text.Substring(secondCursor + 1, beforeTrailing - secondCursor), out var parsedLine))
                {
                    line = parsedLine;
                    column = trailing;
                    filePath = text.Substring(0, secondCursor);
                    return true;
                }
            }

            line = trailing;
            filePath = text.Substring(0, cursor);
            return true;
        }
    }
}
