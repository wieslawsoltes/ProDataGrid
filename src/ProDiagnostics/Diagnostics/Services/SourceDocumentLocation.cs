using System;

namespace Avalonia.Diagnostics.Services
{
    internal sealed class SourceDocumentLocation
    {
        public SourceDocumentLocation(string filePath, int line, string methodName)
        {
            FilePath = filePath ?? string.Empty;
            Line = line;
            MethodName = methodName ?? string.Empty;
        }

        public string FilePath { get; }

        public int Line { get; }

        public string MethodName { get; }

        public string DisplayText => FilePath.Length == 0
            ? string.Empty
            : FilePath + ":" + Line + " (" + MethodName + ")";
    }
}
