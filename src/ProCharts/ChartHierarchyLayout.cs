// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>A renderer-independent finite rectangle.</summary>
    public readonly struct ChartLayoutRect
    {
        /// <summary>Creates an ordered finite rectangle.</summary>
        public ChartLayoutRect(double left, double top, double right, double bottom)
        {
            if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) ||
                !double.IsFinite(bottom) || right < left || bottom < top ||
                !double.IsFinite(right - left) || !double.IsFinite(bottom - top))
                throw new ArgumentException("Rectangle coordinates must be finite and ordered.");
            Left = left; Top = top; Right = right; Bottom = bottom;
        }
        /// <summary>Gets the left edge.</summary>
        public double Left { get; }
        /// <summary>Gets the top edge.</summary>
        public double Top { get; }
        /// <summary>Gets the right edge.</summary>
        public double Right { get; }
        /// <summary>Gets the bottom edge.</summary>
        public double Bottom { get; }
        /// <summary>Gets the width.</summary>
        public double Width => Right - Left;
        /// <summary>Gets the height.</summary>
        public double Height => Bottom - Top;
        /// <summary>Tests half-open containment, excluding right and bottom edges.</summary>
        public bool Contains(double x, double y) => x >= Left && x < Right && y >= Top && y < Bottom;
    }

    /// <summary>A treemap rectangle whose node index is shared with ChartHierarchySnapshot.</summary>
    public readonly struct ChartTreemapCell
    {
        /// <summary>Creates a cell.</summary>
        public ChartTreemapCell(int nodeIndex, ChartLayoutRect bounds) { NodeIndex = nodeIndex; Bounds = bounds; }
        /// <summary>Gets the preorder node index.</summary>
        public int NodeIndex { get; }
        /// <summary>Gets the cell rectangle, including its branch header when present.</summary>
        public ChartLayoutRect Bounds { get; }
    }

    /// <summary>A sunburst sector in clockwise degrees, with radii normalized to the outer chart radius.</summary>
    public readonly struct ChartSunburstSector
    {
        /// <summary>Creates a sector.</summary>
        public ChartSunburstSector(int nodeIndex, double startAngle, double sweepAngle, double innerRadius, double outerRadius)
        { NodeIndex = nodeIndex; StartAngle = startAngle; SweepAngle = sweepAngle; InnerRadius = innerRadius; OuterRadius = outerRadius; }
        /// <summary>Gets the preorder node index.</summary>
        public int NodeIndex { get; }
        /// <summary>Gets the clockwise start angle in degrees (zero points right).</summary>
        public double StartAngle { get; }
        /// <summary>Gets the clockwise sweep in degrees.</summary>
        public double SweepAngle { get; }
        /// <summary>Gets the normalized inner radius.</summary>
        public double InnerRadius { get; }
        /// <summary>Gets the normalized outer radius.</summary>
        public double OuterRadius { get; }
    }

    /// <summary>Deterministic, renderer-independent squarified treemap and radial partition layout.</summary>
    public static class ChartHierarchyLayout
    {
        /// <summary>
        /// Lays out positive-weight nodes using squarified rows, preserving a header for branches.
        /// With zero gap/header the leaf areas partition the input rectangle proportionally to weight.
        /// Zero-area nodes are omitted; parents precede their children in drawing order.
        /// </summary>
        public static IReadOnlyList<ChartTreemapCell> CreateTreemap(ChartHierarchySnapshot hierarchy,
            double width, double height, double gap = 2, double headerHeight = 16)
        {
            ArgumentNullException.ThrowIfNull(hierarchy);
            ValidateNonnegative(width, nameof(width));
            ValidateNonnegative(height, nameof(height));
            ValidateNonnegative(gap, nameof(gap));
            ValidateNonnegative(headerHeight, nameof(headerHeight));
            if (width == 0 || height == 0 || hierarchy.Root.TotalValue == 0) return Array.Empty<ChartTreemapCell>();
            double scale = Math.Max(width, height);
            List<ChartTreemapCell> cells = new(hierarchy.Nodes.Count);
            ChartLayoutRect bounds = new(0, 0, width / scale, height / scale);
            if (hierarchy.Root.IsLeaf)
                cells.Add(new ChartTreemapCell(0, new ChartLayoutRect(0, 0, width, height)));
            else
                LayoutChildren(hierarchy, 0, bounds, gap / scale, headerHeight / scale, scale, width, height, cells);
            return cells.AsReadOnly();
        }

        /// <summary>Creates a weighted radial partition with one ring per hierarchy level.</summary>
        public static IReadOnlyList<ChartSunburstSector> CreateSunburst(ChartHierarchySnapshot hierarchy, double innerRadius = 0.18)
        {
            ArgumentNullException.ThrowIfNull(hierarchy);
            if (!double.IsFinite(innerRadius) || innerRadius < 0 || innerRadius >= 1)
                throw new ArgumentOutOfRangeException(nameof(innerRadius));
            if (hierarchy.Root.TotalValue == 0) return Array.Empty<ChartSunburstSector>();
            List<ChartSunburstSector> sectors = new(hierarchy.Nodes.Count);
            if (hierarchy.Root.IsLeaf)
                sectors.Add(new ChartSunburstSector(0, -90, 360, innerRadius, 1));
            else
                LayoutSectors(hierarchy, 0, -90, 360, innerRadius, (1 - innerRadius) / hierarchy.Root.Height, sectors);
            return sectors.AsReadOnly();
        }

        /// <summary>Returns the deepest painted treemap node at a point, or -1.</summary>
        public static int HitTestTreemap(IReadOnlyList<ChartTreemapCell> cells, double x, double y)
        {
            ArgumentNullException.ThrowIfNull(cells);
            for (int i = cells.Count - 1; i >= 0; i--)
                if (cells[i].Bounds.Contains(x, y)) return cells[i].NodeIndex;
            return -1;
        }

        /// <summary>Returns the sunburst node at coordinates relative to its center and normalized outer radius, or -1.</summary>
        public static int HitTestSunburst(IReadOnlyList<ChartSunburstSector> sectors, double x, double y)
        {
            ArgumentNullException.ThrowIfNull(sectors);
            if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 1 || Math.Abs(y) > 1) return -1;
            double radius = Math.Sqrt(x * x + y * y);
            double angle = Math.Atan2(y, x) * 180 / Math.PI;
            for (int i = sectors.Count - 1; i >= 0; i--)
            {
                ChartSunburstSector sector = sectors[i];
                double relative = ((angle - sector.StartAngle) % 360 + 360) % 360;
                bool withinOuter = radius < sector.OuterRadius || (sector.OuterRadius >= 1 - 1e-12 && radius <= 1);
                if (radius >= sector.InnerRadius && withinOuter && relative < sector.SweepAngle)
                    return sector.NodeIndex;
            }
            return -1;
        }

        private static void LayoutChildren(ChartHierarchySnapshot hierarchy, int parentIndex, ChartLayoutRect rect,
            double gap, double header, double scale, double maxWidth, double maxHeight, List<ChartTreemapCell> output)
        {
            ChartHierarchyNode parent = hierarchy.Nodes[parentIndex];
            if (rect.Width <= 0 || rect.Height <= 0 || parent.TotalValue <= 0) return;
            List<WeightedNode> entries = new(parent.Children.Count);
            double area = rect.Width * rect.Height;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                ChartHierarchyNode child = parent.Children[i];
                double childArea = child.TotalValue / parent.TotalValue * area;
                if (childArea <= 0 || !hierarchy.TryGetNodeIndex(child.Id, out int index)) continue;
                entries.Add(new WeightedNode(index, childArea));
            }
            entries.Sort(static (a, b) => a.Area == b.Area ? a.Index.CompareTo(b.Index) : b.Area.CompareTo(a.Area));
            int start = 0;
            ChartLayoutRect remaining = rect;
            while (start < entries.Count && remaining.Width > 0 && remaining.Height > 0)
            {
                int end = start + 1;
                double sum = entries[start].Area;
                double side = Math.Min(remaining.Width, remaining.Height);
                double worst = Worst(entries[start].Area, entries[start].Area, sum, side);
                while (end < entries.Count)
                {
                    double next = sum + entries[end].Area;
                    double candidate = Worst(entries[start].Area, entries[end].Area, next, side);
                    if (candidate > worst) break;
                    sum = next;
                    worst = candidate;
                    end++;
                }
                bool vertical = remaining.Width >= remaining.Height;
                double thickness = Math.Min(vertical ? remaining.Width : remaining.Height,
                    sum / (vertical ? remaining.Height : remaining.Width));
                if (thickness <= 0 || !double.IsFinite(thickness)) break;
                double cursor = vertical ? remaining.Top : remaining.Left;
                double limit = vertical ? remaining.Bottom : remaining.Right;
                for (int i = start; i < end; i++)
                {
                    double next = i == end - 1 ? limit : Math.Min(limit, cursor + entries[i].Area / thickness);
                    ChartLayoutRect childRect = vertical
                        ? new ChartLayoutRect(remaining.Left, cursor, remaining.Left + thickness, next)
                        : new ChartLayoutRect(cursor, remaining.Top, next, remaining.Top + thickness);
                    cursor = next;
                    double inset = Math.Min(gap / 2, Math.Min(childRect.Width, childRect.Height) / 2);
                    childRect = new ChartLayoutRect(childRect.Left + inset, childRect.Top + inset,
                        childRect.Right - inset, childRect.Bottom - inset);
                    if (childRect.Width <= 0 || childRect.Height <= 0) continue;
                    int index = entries[i].Index;
                    output.Add(new ChartTreemapCell(index, new ChartLayoutRect(
                        Math.Min(maxWidth, childRect.Left * scale), Math.Min(maxHeight, childRect.Top * scale),
                        Math.Min(maxWidth, childRect.Right * scale), Math.Min(maxHeight, childRect.Bottom * scale))));
                    if (!hierarchy.Nodes[index].IsLeaf)
                    {
                        double padding = Math.Min(gap / 2, Math.Min(childRect.Width, childRect.Height) / 2);
                        ChartLayoutRect content = new(childRect.Left + padding,
                            Math.Min(childRect.Bottom - padding, childRect.Top + padding + header),
                            childRect.Right - padding, childRect.Bottom - padding);
                        LayoutChildren(hierarchy, index, content, gap, header, scale, maxWidth, maxHeight, output);
                    }
                }
                remaining = vertical
                    ? new ChartLayoutRect(Math.Min(remaining.Right, remaining.Left + thickness), remaining.Top, remaining.Right, remaining.Bottom)
                    : new ChartLayoutRect(remaining.Left, Math.Min(remaining.Bottom, remaining.Top + thickness), remaining.Right, remaining.Bottom);
                start = end;
            }
        }

        private static double Worst(double maximum, double minimum, double sum, double side)
        {
            if (minimum <= 0 || sum <= 0 || side <= 0) return double.PositiveInfinity;
            double ratio = side / Math.Sqrt(sum);
            double square = ratio * ratio;
            return Math.Max(square * (maximum / sum), 1 / (square * (minimum / sum)));
        }

        private static void LayoutSectors(ChartHierarchySnapshot hierarchy, int parentIndex, double start, double sweep,
            double hole, double ringWidth, List<ChartSunburstSector> output)
        {
            ChartHierarchyNode parent = hierarchy.Nodes[parentIndex];
            int lastPositive = -1;
            for (int i = 0; i < parent.Children.Count; i++)
                if (parent.Children[i].TotalValue > 0) lastPositive = i;
            double cursor = start;
            for (int i = 0; i <= lastPositive; i++)
            {
                ChartHierarchyNode child = parent.Children[i];
                if (child.TotalValue <= 0 || !hierarchy.TryGetNodeIndex(child.Id, out int index)) continue;
                double childSweep = i == lastPositive ? Math.Max(0, start + sweep - cursor) : child.TotalValue / parent.TotalValue * sweep;
                if (childSweep <= 0) continue;
                int depth = hierarchy.Depths[index];
                output.Add(new ChartSunburstSector(index, cursor, childSweep,
                    hole + (depth - 1) * ringWidth, Math.Min(1, hole + depth * ringWidth)));
                if (!child.IsLeaf) LayoutSectors(hierarchy, index, cursor, childSweep, hole, ringWidth, output);
                cursor += childSweep;
            }
        }

        private static void ValidateNonnegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        }

        private readonly struct WeightedNode
        {
            public WeightedNode(int index, double area) { Index = index; Area = area; }
            public int Index { get; }
            public double Area { get; }
        }
    }
}
