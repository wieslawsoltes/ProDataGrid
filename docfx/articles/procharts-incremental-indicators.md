# Incremental chart indicators

`ChartIndicators` analyzes complete batches. Persistent calculators instead consume observations through `IStreamingChartIndicator<TInput,TOutput>`: `Push`, `Current`, `SamplesProcessed` and `Reset`. Construction allocates state; Push/Reset do not allocate managed objects. Snapshot construction, application subscribers, labels and rendering remain separate costs.

## Available calculators

| Calculator | Result and warmup | Retained state |
| --- | --- | --- |
| StreamingExponentialMovingAverage | Full-period SMA seed, then alpha 2/(period+1) | Constant space |
| StreamingRelativeStrengthIndex | Wilder RSI after period price changes; flat/gain/loss conventions 50/100/0 | Constant space |
| StreamingMacd | SMA-seeded fast/slow EMA difference, signal and histogram | Constant space |
| StreamingAverageTrueRange | First high-low included in period seed; preceding close/Wilder updates afterward | Constant space |
| StreamingRollingStatistics | SMA, linear WMA, population/sample deviations after a full finite window | O(period) |
| StreamingSimpleMovingAverage | Arithmetic mean of the last period finite observations | O(period) |
| StreamingWeightedMovingAverage | Chronological weights 1 through period | O(period) |
| StreamingRollingStandardDeviation | Population divisor period; optional sample divisor period-1 | O(period) |
| StreamingBollingerBands | Rolling SMA plus/minus deviation multiplier | O(period) |
| StreamingDonchianChannels | Rolling low/high extrema and midpoint, including current bar | O(period) |

The scalar rolling adapters compose the same combined engine; when several statistics are needed together, use one `StreamingRollingStatistics` instead of allocating separate windows. Its `ChartRollingStatisticsValue` exposes Mean, WeightedMean, PopulationStandardDeviation and SampleStandardDeviation. Period one yields the input mean/weighted mean, zero population deviation and null sample deviation. Standalone sample-deviation and sample-Bollinger constructors require period at least two.

Bollinger and Donchian return immutable `ChartBandValue(Lower, Middle, Upper)`. Warmup/gap channels are null. Bollinger's default multiplier is two and population deviation is the default. Zero multiplier collapses all boundaries to the mean. Donchian requires finite high, low and close, even though close does not determine its extrema; this matches the batch channel convention. Both ATR and Donchian reject finite inverted high/low pairs before any state/counter change, including when close is missing.

## Compose calculation with a bounded display source

```csharp
var indicator = new StreamingBollingerBands(period: 32, standardDeviations: 2);
var display = new StreamingRangeChartDataSource(capacity: 10_000, name: "Bollinger 32");
var buffer = new ChartRangeSample[64];

// Execute on one serialized ingestion thread. Marshal source/model invalidation to the UI as required.
// Incoming prices must be chronological and valid for the destination source's increasing-X contract.
for (int i = 0; i < incomingBatch.Length; i++)
{
    ChartSample sample = incomingBatch[i];
    ChartBandValue band = indicator.Push(sample.Value);
    buffer[i] = new ChartRangeSample(sample.X, band.Lower, band.Upper, sample.Category);
}
display.AppendRange(buffer.AsSpan(0, incomingBatch.Length));
```

Here `incomingBatch` is at most 64 observations; size/reuse the buffer to the application's real batch bound. An application's input validation must happen before advancing calculators when the destination can reject data. Calculators and sources are separate components, not one atomic transaction. Display-ring eviction does not reset the calculator. The finite rolling price window and the retained chart-history capacity are independent.

Use numeric/date/log X positioning with a range source; configure any logarithmic source validity domain to match the renderer. Display decimation occurs **after** every price has been processed by the indicator, never on its input. Thus changing the display budget cannot change the calculated series. None restores full retained display resolution, not prices already discarded by the source. A lower/upper result that becomes a gap follows the source's ordinary paired-gap normalization.

For a scalar EMA, append `new ChartSample(sample.X, indicator.Push(sample.Value), sample.Category)` to `StreamingChartDataSource` instead. UI hosts should batch source notifications; a snapshot per scalar input can erase the benefit of cheap calculator updates.

## Input and lifecycle contracts

Null or non-finite input consumes one observation, produces a gap and restarts warmup. Reset also clears the counter, which otherwise saturates at long.MaxValue. Previous returned values never change. `IsReady` on rolling calculators means a complete finite window exists; a genuinely unrepresentable result channel can still be null. The rolling engine retains only the last period consecutive finite inputs, not the entire finite run used by EMA-style recurrences.

Instances are **single-writer and not thread-safe**. Process source order and serialize access. Correcting/deleting an earlier value or replacing a still-forming bar requires Reset/replay; these calculators do not implicitly revise their previous input. Separate series require separate instances. No dispatcher, timer, network feed, storage archive or multi-series transaction is created.

## Numerical behavior and latency tradeoffs

EMA/RSI/MACD/ATR retain a local origin with a growing scale and transform constant-size recurrence state when the range grows. Their previous input history is not rescanned. Very different magnitudes can still lose small details.

Rolling moments use a two-stack aggregate queue in **one capacity-sized array**. Back prefixes and front suffixes each store centered, offset/scaled summaries. Eviction selects the remaining suffix rather than subtracting large nearly equal moments. Once an outlier expires it cannot remain in the current summaries or their scale. The two active summaries are combined in chronological order; weighted ranks shift by the preceding summary's count. Local centered differences preserve small variance around large common offsets, and fused reconstruction reduces intermediate rounding/overflow. This uses binary64, not arbitrary precision, and is not a promise of bitwise equivalence to batch algorithms with different rounding histories.

Each input enters and transfers at most once: rolling updates are amortized O(1), with O(period) retained numerical storage. **A transfer update can take O(period).** This is not a worst-case constant-latency algorithm. Donchian uses two monotonic queues with the same amortized/worst-case distinction; bounded ring-slot expiry is independent of its lifetime counter. Reset logically clears state without reallocating or clearing numeric-only backing arrays; old numeric storage is not secure-erased.

The paired centered-moment background is described in [Pébay's Sandia report](https://www.sandia.gov/research/publications/details/formulas-for-robust-one-pass-parallel-computation-of-covariances-and-arbitr-2008-09-01/). The queue, chronological weighted composition and normalized implementation are local code. [Math.FusedMultiplyAdd](https://learn.microsoft.com/dotnet/api/system.math.fusedmultiplyadd) reconstructs a product-plus-offset with one final rounding. Extreme mixtures may still lose small details, and genuinely unrepresentable floating-point channels become null. No investment or vendor-complete initialization claim is implied.

## Sample and reproducible validation

Open **ProCharts Range Area → Open bounded live interval demo**, then enable **Calculate rolling Bollinger bands**. Each synthetic price is processed once with period 32; only the resulting intervals enter the bounded display source. Append remains batched, and display-reduction toggling does not replay calculation. Changing calculation mode or deviation width deliberately resets/reseeds the demonstration. Its original supplied-interval mode remains available. All view state/commands remain in the view model with compiled bindings and no added code-behind logic.

```sh
dotnet test src/Avalonia.Controls.DataGrid.UnitTests/Avalonia.Controls.DataGrid.UnitTests.csproj -c Release -p:CollectCoverage=false --filter FullyQualifiedName~StreamingRollingIndicatorTests
dotnet run --project tests/ProCharts.Streaming.Benchmarks/ProCharts.Streaming.Benchmarks.csproj -c Release
```

Regression tests use independent finite-window scans, seeded batch comparisons, transfer boundaries, gap restart, large offsets, expired extreme outliers, subnormal/overflow cases, duplicate extrema, atomic inverted-bar rejection and zero managed allocations. The sample checks full input processing before reduction, bounded-history mapping, single-snapshot batch delivery, reset and real headless toggle routing/native exports.

The existing benchmark retains its MACD fixtures and adds paired rolling-statistics/Bollinger/Donchian comparisons for periods 32/512. Both paths include initial seeding; the batch alternative recomputes the prefix after every append. This is not a claim that hosts previously used that schedule. Separate diagnostics report constructor allocation and transfer versus following-update latency for large windows. Two warmups and medians of seven runs are diagnostics, not UI latency percentiles or fixed CI thresholds. See the benchmark README for exact inclusion/exclusion rules. No renderer, physical-GPU, presentation or FPS improvement is implied.
