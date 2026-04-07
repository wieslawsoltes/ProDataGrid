using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Services;

internal sealed class TransportExportServiceFactory : ITransportExportServiceFactory
{
    private static readonly IReadOnlyList<string> s_supportedProtocols = new[] { UdpTransportExportService.UdpProtocolName };

    public IReadOnlyList<string> SupportedProtocols => s_supportedProtocols;

    public bool TryCreate(string protocol, out ITransportExportService service)
    {
        var normalized = NormalizeProtocol(protocol);
        if (string.Equals(normalized, UdpTransportExportService.UdpProtocolName, StringComparison.Ordinal))
        {
            service = new UdpTransportExportService();
            return true;
        }

        service = null!;
        return false;
    }

    private static string NormalizeProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return UdpTransportExportService.UdpProtocolName;
        }

        return protocol.Trim().ToLowerInvariant();
    }
}
