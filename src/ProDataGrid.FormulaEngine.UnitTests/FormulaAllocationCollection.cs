// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    // Other classes deliberately compact the GC and stress shared pools. Keep exact
    // thread-allocation probes out of that cross-test activity. Explicit Parallel.For
    // scenarios inside these classes still exercise the real concurrent code paths.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class FormulaAllocationCollection
    {
        public const string Name = "Formula allocation-sensitive tests";
    }
}
