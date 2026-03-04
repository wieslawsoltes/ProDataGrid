using System;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Base exception for deterministic remote mutation error contracts.
/// </summary>
public abstract class RemoteMutationException : Exception
{
    protected RemoteMutationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

/// <summary>
/// Raised when command payload is invalid.
/// </summary>
public sealed class RemoteMutationValidationException : RemoteMutationException
{
    public RemoteMutationValidationException(string message)
        : base("validation_error", message)
    {
    }
}

/// <summary>
/// Raised when a requested target/entity cannot be resolved.
/// </summary>
public sealed class RemoteMutationNotFoundException : RemoteMutationException
{
    public RemoteMutationNotFoundException(string message)
        : base("not_found", message)
    {
    }
}

/// <summary>
/// Raised when a command depends on an unavailable backend capability.
/// </summary>
public sealed class RemoteMutationUnavailableException : RemoteMutationException
{
    public RemoteMutationUnavailableException(string message)
        : base("feature_unavailable", message)
    {
    }
}
