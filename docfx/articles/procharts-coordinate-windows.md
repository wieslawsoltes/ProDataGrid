# Coordinate windows for synchronized streams

`StreamingMultiSeriesChartDataSource.BuildViewByX` captures a numeric or encoded-date interval directly from retained ring history. It does not require a full-history snapshot or a linear scan to discover ordinal row offsets.

```csharp
StreamingMultiSeriesChartView view = source.BuildViewByX(
    minimumX: 1_000,
    maximumX: 1_500,
    maxPoints: 256,
    downsampleMode: ChartDownsampleMode.MinMax,
    includeBoundaryNeighbors: true);

ChartDataSnapshot snapshot = view.Snapshot;
// Use only a hit obtained by rendering this exact snapshot:
long originalRow = view.SourceSampleIndices[hit.PointIndex];
double originalX = snapshot.Series[hit.SeriesIndex].XValues![hit.PointIndex];
```

`BuildSnapshotByX` is the convenience form when only numerical/category data is needed. Both APIs use the same snapshot construction, common-index reduction and cache as ordinal `BuildView`. No request object or full-history copy is allocated to resolve a coordinate range.

## Bounds and missing data

Bounds are **inclusive, finite and ordered**. Equal bounds select an exactly matching observation; no match means an empty view unless neighbors are enabled. Invalid bounds or an unknown reduction mode throw before changing the source/cache or raising events. Nonpositive/null budgets and `None` disable reduction; other budget conventions match the synchronized-stream API.

With neighbors enabled, the immediately preceding and following retained rows are added if the requested interval intersects the retained X extent. A range strictly between adjacent observations can therefore return those two rows. A range wholly before or after retained history stays empty: the API does not clamp a remote viewport onto an unrelated endpoint. Neighbors are chosen by row position, not by finding a nonmissing Y value. Independent gaps remain gaps, and nothing is interpolated or reconstructed from evicted history. The expanded window endpoints remain mandatory points during reduction.

`WindowStart` and `WindowCount` describe the resolved, neighbor-expanded ordinal window before reduction. `SourceSampleIndices` maps the selected output points to original accepted rows. The usual **per-series soft budget** still applies; the common union may exceed `maxPoints` to preserve each channel's selected features and gaps.

Coordinates use the same units as `Append`. For data already encoded with `DateTime.ToOADate()`, encode query bounds the same way. The source itself does not convert timestamps, calendar intervals or timezones.

## Atomic capture and presentation boundaries

Lower/upper binary searches, optional expansion, selection, array copying and identity/history metadata capture all execute under the same source lock. Appending or evicting rows cannot shift the resolved window between lookup and capture. Returned views remain owned and unchanged after subsequent append or Clear; numbering still restarts after Clear.

This is an explicit **source API**, not a new `ChartDataRequest` mode. It does not change `ChartModel.Request`, establish a live coordinate viewport, set axis limits, or supply numeric-X positioning for category Line/Area rendering. Use the existing numeric/date Scatter rendering for true X placement. A view's identity map must not be combined with the model's independently captured snapshot. See [synchronized streams](procharts-multi-series-streaming.md) for renderer, delivery and ownership contracts.

## Complexity and measured comparison

Lookup takes O(log retained rows), uses the logical ring directly, and allocates no temporary coordinate array or request. An uncached capture still costs O(window rows × series count), including reduction when requested, and holds the lock for that work. Equivalent normalized coordinate and ordinal windows reuse the same single-entry cache; a warm equivalent query allocates no managed memory in the source. New windows allocate owned output and reduction scratch as before. Large windows can still delay producers; no worst-case latency guarantee is implied.

The existing `ProCharts.Benchmarks` executable includes an **X-RANGE** section comparing 256 distinct 128-row captures, with three channels and wrapped retained capacities of 8,192, 65,536 and 262,144. Both paths capture the same static source with no reduction. The baseline linearly scans a previously captured, owned X array, then calls ordinal `BuildView`; the new path performs atomic binary ring lookup and capture. The baseline's initial full-X acquisition is deliberately excluded rather than charged to every query.

Construction, seeding, query preparation and complete coordinate/value/gap/identity validation are outside timing. Two paired warmups precede seven alternating-order measured pairs. The harness checks identical normalized cached views and every original output value before measuring, then compares captured-identity checksums during measured rounds. It reports median elapsed time and managed allocations in the existing `performance.txt` artifact. Timings are diagnostics, not fixed CI thresholds or universal speedups; this does not benchmark concurrent producers, rendering, physical GPU work or frame rate.

```sh
dotnet run --project tests/ProCharts.Benchmarks/ProCharts.Benchmarks.csproj -c Release
```
