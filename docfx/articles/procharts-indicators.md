# Statistical and technical indicators

`ProCharts.ChartIndicators` transforms existing `ChartSeriesSnapshot` data into independently renderable series. It has no Avalonia or Skia dependency and does not modify the source. Calculations use observation order, not an assumed calendar spacing.

```csharp
ChartSeriesSnapshot price = snapshot.Series[0];
ChartSeriesSnapshot average = ChartIndicators.ExponentialMovingAverage(price, 20);
ChartBandSeries bands = ChartIndicators.BollingerBands(price, 20, standardDeviations: 2);
ChartMacdSeries macd = ChartIndicators.MovingAverageConvergenceDivergence(price, 12, 26, 9);
ChartSeriesSnapshot rsi = ChartIndicators.RelativeStrengthIndex(price, 14);

var pricePane = new ChartDataSnapshot(snapshot.Categories,
    new[] { price, average, bands.Lower, bands.Middle, bands.Upper });
var oscillatorPane = new ChartDataSnapshot(snapshot.Categories,
    new[] { macd.Line, macd.Signal, macd.Histogram });
```

The existing chart model and renderer accept the returned line/column series. RSI and other differently scaled results should use their own pane or an explicitly configured secondary axis. Each result inherits the source value-axis assignment, but not its formatter, styling, error bars, OHLC channels, or trendline configuration. `ChartBandSeries` contains three boundary lines; it does not imply an area-fill-between-series renderer.

## Available transforms and conventions

| Transform | Definition and warmup |
| --- | --- |
| SimpleMovingAverage | Arithmetic mean of the last `period` consecutive finite observations. |
| ExponentialMovingAverage | Seed with the first complete-window SMA, then alpha `2 / (period + 1)`. |
| WeightedMovingAverage | Linear weights from 1 for the oldest observation to `period` for the newest. |
| RollingStandardDeviation | Population divisor `period` by default; optional sample divisor `period - 1`. |
| BollingerBands | SMA plus/minus a configurable multiple of rolling standard deviation; population variance by default. |
| RelativeStrengthIndex | Wilder-smoothed average gains/losses after `period` price changes, requiring `period + 1` consecutive prices. Flat data returns 50, gains-only returns 100, losses-only returns zero. |
| MovingAverageConvergenceDivergence | Fast SMA-seeded EMA minus slow SMA-seeded EMA, an EMA of that difference as signal, and difference-minus-signal as a column histogram. |
| AverageTrueRange | Maximum of high-low and distances to preceding close, smoothed with alpha `1 / period` after an SMA seed. The first bar of a finite run uses high-low and counts toward the seed. |
| DonchianChannels | Rolling high maximum, low minimum and their midpoint; includes the current bar. |

Different products can use different initial seeds, flat-RSI conventions, or first-ATR-bar rules. These conventions are explicit and regression tested; vendor-by-vendor equivalence is not claimed.

## Gaps, ownership, and channels

Output values always have the same count and index alignment as the input. Warmup positions are null. Null and non-finite observations reset both state and warmup, rather than being treated as zero or joined across a missing-data interval. EMA/RSI/MACD/ATR therefore restart after a gap. No look-ahead observations are used by their recurrence formulas.

ATR and Donchian channels require high/low lists aligned with every close/value. A missing or non-finite high, low or close resets the calculation. Finite high values below the corresponding low values are rejected. Open values are not required. Optional X coordinates must have the same count as values; they are copied without reordering, resampling, or generating replacement dates. The caller must keep source collections stable while a transform is running.

Results own read-only arrays. Multi-series results share one owned X array. Changing a caller-owned input array after a call cannot change its already returned result. Periods must be positive; sample deviation requires a period of at least two. A period longer than the input produces aligned null output without allocating a period-sized rolling window. Invalid periods, incompatible channel lengths and non-finite band multipliers fail explicitly.

## Numerical and performance behavior

Calculations use locally offset, scaled coordinates to avoid overflowing sums of large finite numbers and to retain variance in data with a large common offset. An opposite-sign extreme range falls back to magnitude scaling. Final coordinate reconstruction uses fused multiply-add. Results that are genuinely outside the representable finite range become null, not infinity or clamped invented values. This is floating-point arithmetic, not arbitrary precision; extreme mixtures of vastly different magnitudes can still lose smaller details.

Rolling moments use removable Welford updates and periodic full-window rebasing. Weighted averages use rolling sums with periodic rebasing. Donchian extrema use monotonic queues. Rebuilding once per period keeps total work linear rather than rescanning a full window for every output. The transforms are batch operations; normalized input and output arrays require O(n) memory, with O(period) working windows. They are not an incremental streaming indicator state machine, and no renderer/GPU performance gain is implied.

The regression suite compares rolling statistics, weighted means, EMAs, MACD, and extrema against independent reference calculations. It also exercises gap restart, known Wilder values, source ownership, warmup alignment, large-offset variance, extreme finite values, missing channels and oversized periods.
