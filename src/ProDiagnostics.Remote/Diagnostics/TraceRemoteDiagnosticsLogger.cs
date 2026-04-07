using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Writes structured backend diagnostics entries to <see cref="Trace"/>.
/// </summary>
public sealed class TraceRemoteDiagnosticsLogger : IRemoteDiagnosticsLogger
{
    public void Log(in RemoteDiagnosticsLogEntry entry)
    {
        var builder = new StringBuilder(256);
        builder.Append(entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" [");
        builder.Append(entry.Level);
        builder.Append("] ");
        builder.Append(entry.Category);
        builder.Append(" ");
        builder.Append(entry.EventName);
        builder.Append(" transport=");
        builder.Append(string.IsNullOrWhiteSpace(entry.TransportName) ? "unknown" : entry.TransportName);

        if (entry.ConnectionId != Guid.Empty)
        {
            builder.Append(" connection=");
            builder.Append(entry.ConnectionId);
        }

        if (entry.SessionId != Guid.Empty)
        {
            builder.Append(" session=");
            builder.Append(entry.SessionId);
        }

        if (!string.IsNullOrWhiteSpace(entry.RemoteEndpoint))
        {
            builder.Append(" endpoint=");
            builder.Append(entry.RemoteEndpoint);
        }

        if (entry.MessageKind is { } kind)
        {
            builder.Append(" kind=");
            builder.Append(kind);
        }

        if (entry.Bytes > 0)
        {
            builder.Append(" bytes=");
            builder.Append(entry.Bytes);
        }

        if (!string.IsNullOrWhiteSpace(entry.Details))
        {
            builder.Append(" details=\"");
            builder.Append(entry.Details);
            builder.Append('"');
        }

        if (!string.IsNullOrWhiteSpace(entry.ExceptionType))
        {
            builder.Append(" exception=");
            builder.Append(entry.ExceptionType);
        }

        Trace.WriteLine(builder.ToString());
    }
}
