// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    // Sliding-window aggregation with two stacks sharing a single capacity-sized array.
    // Back prefixes grow from index zero; front suffixes occupy the other end. Transferring
    // back to front computes suffix summaries in place before expired entries are discarded.
    // No moment is subtracted on eviction, and no expired outlier remains in either summary.
    internal sealed class RollingMomentWindow
    {
        private readonly Entry[] _entries;
        private int _frontCount, _backCount;

        public RollingMomentWindow(int period) { _entries = new Entry[period]; }
        public int Count => _frontCount + _backCount;
        public void Clear() { _frontCount = _backCount = 0; }

        public RollingMoments Add(double value)
        {
            if (Count == _entries.Length)
            {
                if (_frontCount == 0)
                {
                    RollingMoments suffix = default;
                    for (int i = _backCount - 1; i >= 0; i--)
                    {
                        double previous = _entries[i].Value;
                        suffix = RollingMoments.Combine(RollingMoments.Single(previous), suffix);
                        _entries[i] = new Entry(previous, suffix);
                    }
                    _frontCount = _backCount;
                    _backCount = 0;
                }
                _frontCount--;
            }
            RollingMoments prefix = _backCount == 0 ? default : _entries[_backCount - 1].Moments;
            prefix = RollingMoments.Combine(prefix, RollingMoments.Single(value));
            _entries[_backCount++] = new Entry(value, prefix);
            return _frontCount == 0 ? prefix : RollingMoments.Combine(_entries[_entries.Length - _frontCount].Moments, prefix);
        }

        private readonly record struct Entry(double Value, RollingMoments Moments);
    }

    // Pairwise centered moments in local offset/scaled coordinates. Scaling bounds intermediate
    // squared differences; keeping offsets avoids losing small variance around a large common value.
    // WeightedSum is the normalized sum of chronological weights 1..Count, not raw price squares.
    internal readonly record struct RollingMoments(int Count, double Origin, double Scale,
        double Mean, double M2, double WeightedSum)
    {
        public static RollingMoments Single(double value) => new(1, value, 0, 0, 0, 0);

        public static RollingMoments Combine(RollingMoments a, RollingMoments b)
        {
            if (a.Count == 0) return b;
            if (b.Count == 0) return a;
            double origin = a.Origin;
            double distance = Math.Abs(b.Origin - origin);
            double scale;
            if (double.IsFinite(distance)) scale = Math.Max(Math.Max(a.Scale, b.Scale), distance);
            else
            {
                origin = 0;
                scale = Math.Max(Math.Max(a.Scale, b.Scale), Math.Max(Math.Abs(a.Origin), Math.Abs(b.Origin)));
            }
            int count = a.Count + b.Count;
            if (scale == 0) return new RollingMoments(count, origin, 0, 0, 0, 0);
            double fa = a.Scale / scale, fb = b.Scale / scale;
            double sa = (a.Origin - origin) / scale, sb = (b.Origin - origin) / scale;
            double ma = Math.FusedMultiplyAdd(a.Mean, fa, sa), mb = Math.FusedMultiplyAdd(b.Mean, fb, sb);
            double delta = mb - ma;
            double mean = Math.FusedMultiplyAdd(delta, (double)b.Count / count, ma);
            double m2 = (a.M2 * fa) * fa + (b.M2 * fb) * fb + delta * delta * ((double)a.Count / count) * b.Count;
            double wa = a.Count * (a.Count + 1d) / 2, wb = b.Count * (b.Count + 1d) / 2;
            double weighted = Math.FusedMultiplyAdd(a.WeightedSum, fa, sa * wa) +
                Math.FusedMultiplyAdd(b.WeightedSum, fb, sb * wb) + a.Count * (b.Count * mb);
            return new RollingMoments(count, origin, scale, mean, m2, weighted);
        }

        public double? Decode(double value) => Finite(Math.FusedMultiplyAdd(value, Scale, Origin));
        public double Deviation(bool sample) => Math.Sqrt(Math.Max(0, M2) / (sample ? Count - 1d : Count));

        public ChartRollingStatisticsValue Statistics() => new(Decode(Mean),
            Decode(WeightedSum / (Count * (Count + 1d) / 2)),
            Finite(Deviation(false) * Scale), Count > 1 ? Finite(Deviation(true) * Scale) : null);

        public ChartBandValue Bands(double multiplier, bool sample)
        {
            double? middle = Decode(Mean);
            if (multiplier == 0 || Scale == 0) return new ChartBandValue(middle, middle, middle);
            double deviation = Deviation(sample);
            double width = deviation * multiplier;
            if (double.IsFinite(width)) return new ChartBandValue(Decode(Mean - width), middle, Decode(Mean + width));
            // A huge multiplier can overflow normalized units even with a tiny physical scale.
            // Change multiplication order before treating the result as unrepresentable. Never
            // replace a missing center with an invented numeric zero.
            double physicalWidth = deviation * (multiplier * Scale);
            return middle.HasValue ? new ChartBandValue(Finite(middle.Value - physicalWidth), middle,
                Finite(middle.Value + physicalWidth)) : default;
        }

        private static double? Finite(double value) => double.IsFinite(value) ? value : null;
    }
}
