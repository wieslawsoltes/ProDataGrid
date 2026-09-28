// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProCharts
{
    /// <summary>An immutable preorder index of a hierarchy, shared by rendering, navigation and selection.</summary>
    public sealed class ChartHierarchySnapshot
    {
        private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);

        /// <summary>Indexes an immutable tree and rejects duplicate identifiers.</summary>
        public ChartHierarchySnapshot(ChartHierarchyNode root) : this(root, null) { }

        private ChartHierarchySnapshot(ChartHierarchyNode root,
            Dictionary<ChartHierarchyNode, (int Series, int Point)>? mapping)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            List<ChartHierarchyNode> nodes = new();
            List<int> parents = new();
            List<int> depths = new();
            List<int> branches = new();
            List<string?> categories = new();
            List<int> sourceSeries = new();
            List<int> sourcePoints = new();
            Stack<(ChartHierarchyNode Node, int Parent, int Depth, int Branch)> pending = new();
            pending.Push((root, -1, 0, 0));
            while (pending.Count != 0)
            {
                var item = pending.Pop();
                int index = nodes.Count;
                if (!_indices.TryAdd(item.Node.Id, index))
                    throw new ArgumentException($"Duplicate hierarchy identifier '{item.Node.Id}'.", nameof(root));
                nodes.Add(item.Node);
                parents.Add(item.Parent);
                depths.Add(item.Depth);
                branches.Add(item.Branch);
                categories.Add(item.Node.Label ?? item.Node.Id);
                var origin = mapping != null && mapping.TryGetValue(item.Node, out var mapped) ? mapped : (0, index);
                sourceSeries.Add(origin.Item1);
                sourcePoints.Add(origin.Item2);
                for (int child = item.Node.Children.Count - 1; child >= 0; child--)
                    pending.Push((item.Node.Children[child], index, item.Depth + 1, index == 0 ? child : item.Branch));
            }
            Nodes = nodes.AsReadOnly();
            ParentIndices = parents.AsReadOnly();
            Depths = depths.AsReadOnly();
            BranchIndices = branches.AsReadOnly();
            Categories = categories.AsReadOnly();
            SourceSeriesIndices = sourceSeries.AsReadOnly();
            SourcePointIndices = sourcePoints.AsReadOnly();
        }

        /// <summary>Gets the immutable root.</summary>
        public ChartHierarchyNode Root { get; }

        /// <summary>Gets preorder nodes, including the root at index zero.</summary>
        public IReadOnlyList<ChartHierarchyNode> Nodes { get; }

        /// <summary>Gets parent indices, with -1 for the root.</summary>
        public IReadOnlyList<int> ParentIndices { get; }

        /// <summary>Gets zero-based node depths.</summary>
        public IReadOnlyList<int> Depths { get; }

        /// <summary>Gets the top-level branch ordinal used for consistent hierarchical colors.</summary>
        public IReadOnlyList<int> BranchIndices { get; }

        /// <summary>Gets labels aligned with the preorder node indices.</summary>
        public IReadOnlyList<string?> Categories { get; }

        /// <summary>Gets original series indices when created by FromChartData; otherwise zero.</summary>
        public IReadOnlyList<int> SourceSeriesIndices { get; }

        /// <summary>Gets original point indices for matrix leaves; matrix branch nodes use -1.</summary>
        public IReadOnlyList<int> SourcePointIndices { get; }

        /// <summary>Finds the preorder index of a stable node identifier.</summary>
        public bool TryGetNodeIndex(string id, out int index)
        {
            ArgumentNullException.ThrowIfNull(id);
            return _indices.TryGetValue(id, out index);
        }

        /// <summary>
        /// Projects ordinary chart series into a two-level hierarchy (series, then category).
        /// A single series projects directly to category leaves. Missing/non-finite values are
        /// omitted; negative finite values are rejected rather than silently changing their meaning.
        /// </summary>
        public static ChartHierarchySnapshot FromChartData(ChartDataSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Dictionary<ChartHierarchyNode, (int, int)> mapping = new();
            List<ChartHierarchyNode> seriesNodes = new(snapshot.Series.Count);
            for (int s = 0; s < snapshot.Series.Count; s++)
            {
                ChartSeriesSnapshot series = snapshot.Series[s];
                List<ChartHierarchyNode> leaves = new(series.Values.Count);
                string prefix = "series/" + s.ToString(CultureInfo.InvariantCulture);
                for (int p = 0; p < series.Values.Count; p++)
                {
                    if (series.Values[p] is not double value || !double.IsFinite(value)) continue;
                    if (value < 0)
                        throw new ArgumentException("Treemap and sunburst weights cannot be negative.", nameof(snapshot));
                    string id = prefix + "/" + p.ToString(CultureInfo.InvariantCulture);
                    string? label = p < snapshot.Categories.Count ? snapshot.Categories[p] : null;
                    ChartHierarchyNode leaf = new(id, label ?? (p + 1).ToString(CultureInfo.InvariantCulture), value);
                    leaves.Add(leaf);
                    mapping.Add(leaf, (s, p));
                }
                ChartHierarchyNode branch = new(prefix, series.Name, leaves);
                seriesNodes.Add(branch);
                mapping.Add(branch, (s, -1));
            }
            ChartHierarchyNode root = seriesNodes.Count == 1 ? seriesNodes[0] : new ChartHierarchyNode("matrix", null, seriesNodes);
            return new ChartHierarchySnapshot(root, mapping);
        }

        internal IReadOnlyList<ChartSeriesSnapshot> CreateSeries(ChartSeriesKind kind)
        {
            if (kind != ChartSeriesKind.Treemap && kind != ChartSeriesKind.Sunburst)
                throw new ArgumentOutOfRangeException(nameof(kind), "A hierarchy snapshot requires Treemap or Sunburst.");
            double?[] values = new double?[Nodes.Count];
            for (int i = 0; i < values.Length; i++) values[i] = Nodes[i].TotalValue;
            return Array.AsReadOnly(new[] { new ChartSeriesSnapshot(Root.Label, kind, Array.AsReadOnly(values)) });
        }
    }

    public sealed partial class ChartDataSnapshot
    {
        /// <summary>Creates a treemap or sunburst snapshot whose point indices are preorder node indices.</summary>
        public ChartDataSnapshot(ChartHierarchySnapshot hierarchy, ChartSeriesKind kind, int version = 0)
            : this((hierarchy ?? throw new ArgumentNullException(nameof(hierarchy))).Categories,
                hierarchy.CreateSeries(kind), version)
        {
            Hierarchy = hierarchy;
        }

        /// <summary>Gets the optional typed hierarchy. Existing snapshot constructors remain unchanged.</summary>
        public ChartHierarchySnapshot? Hierarchy { get; }
    }
}
