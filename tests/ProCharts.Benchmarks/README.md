# ProCharts performance diagnostics

Run with the repository's .NET SDK:

```sh
dotnet run --project tests/ProCharts.Benchmarks/ProCharts.Benchmarks.csproj -c Release
```

The program records runtime, operating system and processor count, then reports median elapsed time and managed allocations across seven measured runs after two warmups. Input construction is outside the timed regions. Indicator normalization, working windows, result ownership and output arrays are included. The charting CI workflow retains the complete output with its regression-test artifact.

## Scenarios

- 100,000 appends into a 4,096-element shifting list and a bounded streaming ring, with retained-tail equivalence checked.
- 100,000 unchanged cached-window reads.
- MinMax, LTTB and Bucket selection of one million samples to a 2,000-point budget, with endpoint/order/budget checks.
- SMA, WMA, standard deviation and Donchian channels over 200,000 observations with periods 20 and 2,000.
- Bollinger bands, RSI, ATR and MACD over 200,000 observations, checking finite aligned outputs.
- An independent full-window SMA scan compared with the rolling implementation over 20,000 observations and period 2,000. Every output is compared before timing.

The list and window-scan cases are deliberately simple reference algorithms, **not** measurements of a previous production renderer or adapter. Shared CI runners are variable, so there are no fixed elapsed-time pass/fail thresholds. Semantic contracts and steady-state append allocation regressions are tested separately. These benchmarks do not measure GPU throughput, browser interaction latency, rendering frame rate or incremental indicator updates.
