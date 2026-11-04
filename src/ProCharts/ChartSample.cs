// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

namespace ProCharts
{
    /// <summary>A numeric chart sample. A null or non-finite value represents a gap.</summary>
    public readonly struct ChartSample
    {
        /// <summary>Creates a sample without allocating a category label.</summary>
        public ChartSample(double x, double? value, string? category = null)
        {
            X = x;
            Value = value;
            Category = category;
        }

        /// <summary>Gets the numeric or OLE Automation date coordinate.</summary>
        public double X { get; }

        /// <summary>Gets the value, or a gap.</summary>
        public double? Value { get; }

        /// <summary>Gets the optional category label.</summary>
        public string? Category { get; }
    }
}
