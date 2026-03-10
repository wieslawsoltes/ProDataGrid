using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Raised when remote request/response roundtrip fails on transport or protocol level.
/// </summary>
public sealed class RemoteDiagnosticsRequestException : Exception
{
    public RemoteDiagnosticsRequestException(
        string method,
        long requestId,
        string errorCode,
        string errorMessage)
        : base(BuildMessage(method, requestId, errorCode, errorMessage))
    {
        Method = method;
        RequestId = requestId;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public string Method { get; }

    public long RequestId { get; }

    public string ErrorCode { get; }

    public string ErrorMessage { get; }

    private static string BuildMessage(string method, long requestId, string errorCode, string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) || !string.IsNullOrWhiteSpace(errorMessage))
        {
            return "Remote request failed (" + method + " #" + requestId.ToString() + "): " +
                   errorCode + ": " + errorMessage;
        }

        return "Remote request failed (" + method + " #" + requestId.ToString() + ").";
    }
}
