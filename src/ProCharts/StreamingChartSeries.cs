// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Fixed scalar-series metadata for a synchronized stream.</summary>
    /// <remarks>Styles and formatter delegates keep their normal shared-reference semantics; numerical storage is owned by the source.</remarks>
    public sealed class StreamingChartSeries
    {
        /// <summary>Defines a line, area or scatter series and its input validity domain.</summary>
        /// <remarks>Configure the assigned renderer value axis to match ValueAxisKind. Financial, range and stacked channels are not supplied.</remarks>
        public StreamingChartSeries(string? name = null, ChartSeriesKind kind = ChartSeriesKind.Line,
            ChartValueAxisAssignment valueAxisAssignment = ChartValueAxisAssignment.Primary,
            ChartSeriesStyle? style = null, Func<double, string>? dataLabelFormatter = null,
            ChartAxisKind valueAxisKind = ChartAxisKind.Value)
        {
            if (kind is not (ChartSeriesKind.Line or ChartSeriesKind.Area or ChartSeriesKind.Scatter))
                throw new ArgumentOutOfRangeException(nameof(kind), "Synchronized scalar streams support Line, Area and Scatter.");
            if (valueAxisAssignment is not (ChartValueAxisAssignment.Primary or ChartValueAxisAssignment.Secondary))
                throw new ArgumentOutOfRangeException(nameof(valueAxisAssignment));
            if (valueAxisKind is not (ChartAxisKind.Value or ChartAxisKind.Logarithmic))
                throw new ArgumentOutOfRangeException(nameof(valueAxisKind));
            Name = name; Kind = kind; ValueAxisAssignment = valueAxisAssignment;
            Style = style; DataLabelFormatter = dataLabelFormatter; ValueAxisKind = valueAxisKind;
        }

        /// <summary>Gets the optional display name.</summary>
        public string? Name { get; }
        /// <summary>Gets the scalar Cartesian presentation kind.</summary>
        public ChartSeriesKind Kind { get; }
        /// <summary>Gets the destination value axis.</summary>
        public ChartValueAxisAssignment ValueAxisAssignment { get; }
        /// <summary>Gets the input validity domain, applied before reduction.</summary>
        public ChartAxisKind ValueAxisKind { get; }
        /// <summary>Gets the shared presentation style.</summary>
        public ChartSeriesStyle? Style { get; }
        /// <summary>Gets the shared data-label formatter.</summary>
        public Func<double, string>? DataLabelFormatter { get; }
    }
}
