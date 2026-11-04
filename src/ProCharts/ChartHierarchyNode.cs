// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>An immutable weighted hierarchy node. Branch totals are derived from their children.</summary>
    public sealed class ChartHierarchyNode
    {
        /// <summary>Creates a leaf with a finite, nonnegative weight.</summary>
        public ChartHierarchyNode(string id, string? label, double value)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Hierarchy weights must be finite and nonnegative.");
            Id = id;
            Label = label;
            TotalValue = value;
            Children = Array.Empty<ChartHierarchyNode>();
        }

        /// <summary>Creates a branch, copying its child collection and deriving its total and height.</summary>
        /// <remarks>Trees are limited to 64 edges in depth. Aggregate weights must remain finite.</remarks>
        public ChartHierarchyNode(string id, string? label, IReadOnlyList<ChartHierarchyNode> children)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            ArgumentNullException.ThrowIfNull(children);
            ChartHierarchyNode[] owned = new ChartHierarchyNode[children.Count];
            double total = 0;
            double compensation = 0;
            int height = 0;
            for (int i = 0; i < owned.Length; i++)
            {
                ChartHierarchyNode child = children[i] ?? throw new ArgumentException("Children cannot contain null.", nameof(children));
                owned[i] = child;
                height = Math.Max(height, child.Height + 1);
                double adjusted = child.TotalValue - compensation;
                double next = total + adjusted;
                compensation = (next - total) - adjusted;
                total = next;
                if (!double.IsFinite(total))
                    throw new ArgumentOutOfRangeException(nameof(children), "Aggregate hierarchy weight exceeds the finite numeric range.");
            }
            if (height > 64)
                throw new ArgumentOutOfRangeException(nameof(children), "Hierarchy depth cannot exceed 64 edges.");
            Id = id;
            Label = label;
            TotalValue = total;
            Height = height;
            Children = Array.AsReadOnly(owned);
        }

        /// <summary>Gets the stable identifier. Identifiers must be unique within a hierarchy snapshot.</summary>
        public string Id { get; }

        /// <summary>Gets the display label.</summary>
        public string? Label { get; }

        /// <summary>Gets the leaf weight or the sum of descendant leaf weights.</summary>
        public double TotalValue { get; }

        /// <summary>Gets the owned, read-only child collection.</summary>
        public IReadOnlyList<ChartHierarchyNode> Children { get; }

        /// <summary>Gets the maximum number of edges from this node to a leaf.</summary>
        public int Height { get; }

        /// <summary>Gets whether this node has no children.</summary>
        public bool IsLeaf => Children.Count == 0;
    }
}
