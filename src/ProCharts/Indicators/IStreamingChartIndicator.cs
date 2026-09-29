// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProCharts
{
    /// <summary>A reusable, single-writer indicator that consumes observations without retaining their history.</summary>
    /// <typeparam name="TInput">One observation, including any required channels.</typeparam>
    /// <typeparam name="TOutput">The aligned result for the most recently consumed observation.</typeparam>
    /// <remarks>
    /// Implementations are not thread-safe. Push observations in source order and serialize access.
    /// A gap consumes one observation and restarts warmup; Reset also clears the lifetime counter.
    /// Correcting an earlier observation requires Reset and replay, not another Push at that position.
    /// </remarks>
    public interface IStreamingChartIndicator<in TInput, out TOutput>
    {
        /// <summary>Gets the result of the most recent Push, including warmup/missing output.</summary>
        TOutput Current { get; }

        /// <summary>Gets observations consumed since Reset, including gaps; saturates at long.MaxValue.</summary>
        long SamplesProcessed { get; }

        /// <summary>Consumes the next observation and returns its aligned output.</summary>
        TOutput Push(TInput observation);

        /// <summary>Clears the result, warmup, recurrence state and observation count.</summary>
        void Reset();
    }

    /// <summary>Aligned high, low and close channels for one observation. Open is not required by ATR.</summary>
    /// <param name="High">The observation's high value, or a gap.</param>
    /// <param name="Low">The observation's low value, or a gap.</param>
    /// <param name="Close">The observation's closing value, or a gap.</param>
    public readonly record struct ChartHighLowClose(double? High, double? Low, double? Close);

    /// <summary>An aligned MACD observation. Individual channels remain null until their warmup completes.</summary>
    /// <param name="Line">Fast EMA minus slow EMA.</param>
    /// <param name="Signal">EMA of the MACD line.</param>
    /// <param name="Histogram">MACD line minus signal.</param>
    public readonly record struct ChartMacdValue(double? Line, double? Signal, double? Histogram);
}
