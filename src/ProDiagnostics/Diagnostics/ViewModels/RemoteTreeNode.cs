using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Diagnostics.Remote;
using Avalonia.Styling;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class RemoteTreeNode : TreeNode
{
    private readonly RemoteTreeNodeCollection _children;

    public RemoteTreeNode(
        RemoteTreeNodeSnapshot snapshot,
        AvaloniaObject visual,
        TreeNode? parent)
        : base(visual, parent, customTypeName: snapshot.Type)
    {
        Snapshot = snapshot;
        _children = new RemoteTreeNodeCollection(this);
    }

    public RemoteTreeNodeSnapshot Snapshot { get; }

    public override TreeNodeCollection Children => _children;

    public void AddChild(RemoteTreeNode node)
    {
        _children.Add(node);
    }

    public static AvaloniaObject CreateSnapshotVisual(RemoteTreeNodeSnapshot snapshot)
    {
        var visual = new RemoteTreeVisual
        {
            Name = snapshot.ElementName ?? string.Empty
        };

        foreach (var className in ParseStyleClasses(snapshot.Classes))
        {
            visual.Classes.Add(className);
        }

        return visual;
    }

    private static IEnumerable<string> ParseStyleClasses(string classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
        {
            yield break;
        }

        var text = classes.Trim();
        if (text.StartsWith("(", StringComparison.Ordinal) &&
            text.EndsWith(")", StringComparison.Ordinal) &&
            text.Length > 2)
        {
            text = text[1..^1];
        }

        var split = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < split.Length; i++)
        {
            var className = split[i];
            if (className.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            yield return className;
        }
    }

    private sealed class RemoteTreeNodeCollection : TreeNodeCollection
    {
        private readonly List<TreeNode> _nodes = new();

        public RemoteTreeNodeCollection(TreeNode owner)
            : base(owner)
        {
        }

        public void Add(TreeNode node)
        {
            _nodes.Add(node);
        }

        protected override void Initialize(AvaloniaList<TreeNode> nodes)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                nodes.Add(_nodes[i]);
            }
        }
    }

    private sealed class RemoteTreeVisual : StyledElement
    {
    }
}
