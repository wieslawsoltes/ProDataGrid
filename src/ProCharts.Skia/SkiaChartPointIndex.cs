// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using SkiaSharp;

namespace ProCharts.Skia
{
    internal readonly record struct SkiaChartIndexedPoint(SKPoint Position, int SeriesIndex,
        int PointIndex, float RadiusSquared, bool HasX);

    /// <summary>A bounded, renderer-owned uniform grid over screen-space point centers.</summary>
    internal sealed class SkiaChartPointIndex
    {
        private readonly SKRect _plot;
        private readonly int _dimension;
        private readonly int[] _heads;
        private readonly int[] _next;
        private readonly SkiaChartIndexedPoint[] _points;
        private int _count;
        private float _maximumRadius;

        public SkiaChartPointIndex(SKRect plot, int capacity)
        {
            _plot = plot;
            _dimension = Math.Clamp((int)Math.Sqrt(capacity / 8d), 1, 128);
            _heads = new int[_dimension * _dimension];
            Array.Fill(_heads, -1);
            _next = new int[capacity];
            _points = new SkiaChartIndexedPoint[capacity];
        }

        public void Add(SkiaChartIndexedPoint point, float radius)
        {
            SKPoint position = point.Position;
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
                position.X < (double)_plot.Left - radius || position.X > (double)_plot.Right + radius ||
                position.Y < (double)_plot.Top - radius || position.Y > (double)_plot.Bottom + radius) return;
            int cell = CellY(position.Y) * _dimension + CellX(position.X);
            _points[_count] = point;
            _next[_count] = _heads[cell];
            _heads[cell] = _count++;
            _maximumRadius = Math.Max(_maximumRadius, radius);
        }

        public bool TryFind(SKPoint point, out SkiaChartIndexedPoint found)
        {
            found = default;
            if (_count == 0) return false;
            // Round outwards to avoid losing points exactly at a float-rounded radius/cell boundary.
            double radius = _maximumRadius + Math.Max(0.00001, _maximumRadius * 0.000001);
            int left = CellX(point.X - radius), right = CellX(point.X + radius);
            int top = CellY(point.Y - radius), bottom = CellY(point.Y + radius);
            float bestDistance = float.MaxValue;
            int best = -1;
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                    for (int i = _heads[y * _dimension + x]; i >= 0; i = _next[i])
                    {
                        SkiaChartIndexedPoint candidate = _points[i];
                        float dx = point.X - candidate.Position.X, dy = point.Y - candidate.Position.Y;
                        float distance = dx * dx + dy * dy;
                        if (distance > candidate.RadiusSquared || !float.IsFinite(distance) ||
                            distance > bestDistance || (distance == bestDistance && (best < 0 || i >= best))) continue;
                        best = i;
                        bestDistance = distance;
                    }
            if (best < 0) return false;
            found = _points[best];
            return true;
        }

        private int CellX(double x) => (int)Math.Clamp((x - _plot.Left) / _plot.Width * _dimension, 0, _dimension - 1);
        private int CellY(double y) => (int)Math.Clamp((y - _plot.Top) / _plot.Height * _dimension, 0, _dimension - 1);
    }
}
