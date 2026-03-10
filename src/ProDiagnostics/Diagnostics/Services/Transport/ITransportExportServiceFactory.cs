using System.Collections.Generic;

namespace Avalonia.Diagnostics.Services;

internal interface ITransportExportServiceFactory
{
    IReadOnlyList<string> SupportedProtocols { get; }

    bool TryCreate(string protocol, out ITransportExportService service);
}
