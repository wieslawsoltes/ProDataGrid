using System;

namespace Avalonia.Diagnostics.Services
{
    internal sealed class SourceLocationInfo
    {
        public SourceLocationInfo(
            Type? targetType,
            SourceDocumentLocation? xamlLocation,
            SourceDocumentLocation? codeLocation,
            string status)
        {
            TargetType = targetType;
            XamlLocation = xamlLocation;
            CodeLocation = codeLocation;
            Status = status ?? string.Empty;
        }

        public static SourceLocationInfo Empty { get; } = new(
            targetType: null,
            xamlLocation: null,
            codeLocation: null,
            status: "Source symbols unavailable.");

        public Type? TargetType { get; }

        public SourceDocumentLocation? XamlLocation { get; }

        public SourceDocumentLocation? CodeLocation { get; }

        public string Status { get; }

        public bool HasAnyLocation => XamlLocation != null || CodeLocation != null;
    }
}
