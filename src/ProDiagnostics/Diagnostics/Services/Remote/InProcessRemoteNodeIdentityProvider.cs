using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessRemoteNodeIdentityProvider
{
    private sealed class NodeIdentity
    {
        public NodeIdentity(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    private readonly ConditionalWeakTable<AvaloniaObject, NodeIdentity> _identities = new();
    private long _nextId;

    public string GetNodeId(AvaloniaObject node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var identity = _identities.GetValue(
            node,
            CreateIdentity);
        return identity.Id;
    }

    private NodeIdentity CreateIdentity(AvaloniaObject _)
    {
        return new NodeIdentity("node-" + Interlocked.Increment(ref _nextId));
    }
}
