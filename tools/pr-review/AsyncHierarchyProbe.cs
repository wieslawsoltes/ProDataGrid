using System.Diagnostics;
using System.Text.Json;
using ProDataGrid.Hierarchy.Benchmarks;
foreach (HierarchyShape shape in new[] { HierarchyShape.Wide2555Depth3, HierarchyShape.Binary4094Depth11, HierarchyShape.VeryDeep512Depth128 })
{
    var benchmark = new AsyncHierarchyExpansionBenchmarks { Shape = shape, Strategy = AsyncExpansionStrategy.Batched };
    benchmark.GlobalSetup();
    for (int i = 0; i < 8; i++) { benchmark.IterationSetup(); await benchmark.ExpandAllAsync(); }
    var times = new double[32]; var bytes = new long[32];
    for (int i = 0; i < times.Length; i++)
    {
        benchmark.IterationSetup();
        long before = GC.GetTotalAllocatedBytes(true), start = Stopwatch.GetTimestamp();
        await benchmark.ExpandAllAsync();
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes[i] = GC.GetTotalAllocatedBytes(true) - before;
    }
    Console.WriteLine(JsonSerializer.Serialize(new { shape = shape.ToString(), times, bytes }));
}
