# Incremental chart indicators

The existing `ChartIndicators` methods are batch transforms. For a live feed, `StreamingExponentialMovingAverage`, `StreamingRelativeStrengthIndex`, `StreamingMacd` and `StreamingAverageTrueRange` instead keep a fixed amount of recurrence state and consume one observation at a time. Push and Reset do not allocate managed memory. No period-sized window or lifetime history is retained.

```csharp
var indicator = new StreamingExponentialMovingAverage(period: 20);
var display = new StreamingChartDataSource(capacity: 10_000, name: "EMA 20");

// Execute on one serialized ingestion thread; marshal chart invalidation to the UI as required.
foreach (ChartSample sample in incomingSamples)
    display.Append(new ChartSample(sample.X, indicator.Push(sample.Value), sample.Category));
```

The indicator's history survives display-ring eviction. It represents the entire finite input run, not an EMA restarted over only the last visible points. For higher-throughput UI delivery, accumulate output ChartSample values in a reused buffer and call the display source's AppendRange once per batch. Snapshot construction and notification dispatch are separate costs from indicator calculation.

## API and conventions

All four classes implement the small `IStreamingChartIndicator<TInput,TOutput>` contract: `Push`, `Current`, `SamplesProcessed` and `Reset`. Scalar indicators consume nullable doubles; ATR consumes `ChartHighLowClose` or its three-scalar overload. MACD returns a value-type `ChartMacdValue` whose line, signal and histogram each have their own warmup.

EMA uses a complete-period SMA seed and alpha 2/(period+1). RSI uses Wilder averages after period price changes, requiring period+1 prices; flat/gain-only/loss-only results are 50/100/0. MACD subtracts independently SMA-seeded fast/slow EMAs and smooths that difference for its signal. ATR counts the first bar's high-low toward its SMA seed, then uses the preceding close and Wilder alpha 1/period. These match the documented batch conventions, within floating-point tolerance, rather than claiming every vendor's initialization policy.

Null or non-finite input consumes one observation, produces a gap and restarts warmup. ATR requires finite high, low and close. Inverted finite high/low pairs throw before changing any state, even when close is missing. Reset also clears the observation count. The counter saturates at long.MaxValue rather than wrapping.

Instances are **single-writer and not thread-safe**. Call Push in source order. They are append-only calculators: correcting or deleting an earlier price requires Reset and replay. They do not implicitly replace the most recent still-forming bar. Use separate instances for independent series. Current is a value, not a mutable buffer; previously returned results cannot change.

## Numerical behavior

The first finite price establishes a local origin. Increasing ranges rescale the fixed recurrence state, avoiding rescans of input history. Opposite-sign subtraction overflow switches to origin-zero magnitude scaling. Price-level and difference-level state are transformed separately, preserving small MACD differences on large common offsets. Output reconstruction uses fused multiply-add. Genuinely unrepresentable results become null while finite normalized recurrence state is retained.

This is double-precision arithmetic, not arbitrary precision. A stream mixing extreme and vastly smaller magnitudes can lose smaller details. Incremental and batch calculations need not be bit-identical because their scaling and rounding histories differ. Neither implementation is an investment recommendation.

## Reproduce validation and performance

```sh
dotnet test src/Avalonia.Controls.DataGrid.UnitTests/Avalonia.Controls.DataGrid.UnitTests.csproj -c Release -p:CollectCoverage=false --filter FullyQualifiedName~StreamingIndicatorTests
dotnet run --project tests/ProCharts.Streaming.Benchmarks/ProCharts.Streaming.Benchmarks.csproj -c Release
```

Tests compare every output against the batch API across seeded random data, gaps and multiple periods, and cover known Wilder values, resets, invalid-range atomicity, extreme and subnormal inputs, large-offset MACD, source composition and zero-allocation steady state. The benchmark compares recomputing the existing batch MACD after each new observation with persistent incremental state, checking every measured output. It includes initial history seeding for both, reports two warmups and medians of seven measurements, and records allocations. This comparison does not assert that an existing host previously recomputed after every append or that the chart's GPU/frame rate changed.
