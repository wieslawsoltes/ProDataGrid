using System;

namespace Avalonia.Diagnostics.Services
{
    internal sealed class SourceDocumentLocation
    {
        public SourceDocumentLocation(string filePath, int line, string methodName, int column = 0)
        {
            FilePath = filePath ?? string.Empty;
            Line = line;
            MethodName = methodName ?? string.Empty;
            Column = column;
        }

        public string FilePath { get; }

        public int Line { get; }

        public int Column { get; }

        public string MethodName { get; }

        public string DisplayText => FilePath.Length == 0
            ? string.Empty
            : Column > 0
                ? FilePath + ":" + Line + ":" + Column + " (" + MethodName + ")"
                : FilePath + ":" + Line + " (" + MethodName + ")";
    }
}
