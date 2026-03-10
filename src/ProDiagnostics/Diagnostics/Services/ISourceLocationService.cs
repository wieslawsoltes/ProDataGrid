using System;
using System.Reflection;

namespace Avalonia.Diagnostics.Services
{
    internal interface ISourceLocationService
    {
        SourceLocationInfo Resolve(Type? type);

        SourceDocumentLocation? ResolveDocument(Assembly? assembly, string? documentHint, string? lineHint = null);

        SourceLocationInfo ResolveObject(object? source, string? documentHint = null, string? lineHint = null);
    }
}
