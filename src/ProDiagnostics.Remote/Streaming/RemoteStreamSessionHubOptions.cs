namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Controls queueing and dispatch behavior for attach stream fan-out.
/// </summary>
public readonly record struct RemoteStreamSessionHubOptions(
    int MaxQueueLengthPerSession,
    int MaxDispatchBatchSize)
{
    public static RemoteStreamSessionHubOptions Default => new(
        MaxQueueLengthPerSession: 2048,
        MaxDispatchBatchSize: 256);

    public static RemoteStreamSessionHubOptions Normalize(in RemoteStreamSessionHubOptions options)
    {
        var queueLength = options.MaxQueueLengthPerSession <= 0
            ? Default.MaxQueueLengthPerSession
            : options.MaxQueueLengthPerSession;
        var batchSize = options.MaxDispatchBatchSize <= 0
            ? Default.MaxDispatchBatchSize
            : options.MaxDispatchBatchSize;

        return new RemoteStreamSessionHubOptions(queueLength, batchSize);
    }
}
