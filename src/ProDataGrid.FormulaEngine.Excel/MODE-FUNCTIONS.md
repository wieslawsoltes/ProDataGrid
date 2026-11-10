# Mode functions

`MODE.SNGL` and the compatibility name `MODE` return a single most-frequent numeric value; `MODE.MULT` returns all equally frequent modes in an owned vertical array. With no repeated numeric value, all three return `#N/A`.

```text
MODE.MULT({1;2;3;4;3;2;1;2;3;5;6;1})
MODE.SNGL({5.6;4;4;3;2;4})
TRANSPOSE(MODE.MULT(A1:A1000))
```

The previous engine's single-mode tie policy is retained: first appearance, not smallest numeric value or earliest second occurrence. Multiple modes follow that same first-appearance order across arguments and row-major array traversal. This explicit deterministic ordering is an engine compatibility contract; Microsoft documents the modal values and vertical result, not every native tie-order scenario. Sorting the result remains possible with `SORT`.

Blank, text and logical entries inside arrays/direct references are ignored; numeric zero is included. Direct scalar numeric text and logical arguments use the existing numerical coercion rules. Original error metadata propagates. Precision normalization occurs before grouping, signed zeros share a count, and nonfinite keys are rejected. The lazy interface streams direct references and preserves single-cell provenance without reparsing or substituting the caller's resolver. Named expressions returning scalar values still do not preserve all reference provenance; custom eager invocation cannot infer how scalar arguments originated. Directly nested arrays/callables and unresolved reference payloads are rejected.

## Performance and ownership

The mode counter is an invocation-local open-addressed numeric table. Small sets stay on the stack; larger sets use cleared pooled numeric-entry arrays. Storage follows the number of distinct accepted values, not the observation count. It grows only when a new distinct key needs space. Neither a numeric sample list nor a managed dictionary is allocated. Inputs are read once, remain unmodified, and no cross-call index is retained. For `MODE.MULT`, only winning entries are ordered at publication and copied to a separate result.

Expected counting work is O(n), with O(d) workspace for d distinct values; multi-result ordering adds O(m log m) work for m modes. Hash collisions can increase worst-case probing work; this is not a universal linear-time or elapsed-time guarantee. Warm pool reuse can eliminate managed workspace allocations but retains buffers and can allocate when cold. Full clearing and result creation are included in the benchmark.

`MaximumArrayCellCount` bounds all visited scalar/physical array entries across arguments, including ignored entries and masked holes. Direct range enumeration is bounded by values supplied by the resolver. This is a per-call resource policy, not Excel's data-size limit or a workbook-wide quota. The existing dimension/cell limit applies to the multi-result array. At most 255 arguments are accepted. Hosts must keep source arrays and registries stable during evaluation.

The maintained aggregate executable compares scalar/multi-mode counting with an independent prior-style list/dictionary algorithm on 100,000 observations with 32, 4,096 and 100,000 distinct values. All result elements are checked before timing, with weighted checksums during timing. This is not a linked older release or native Excel benchmark. The unit tests cover independent sorted-map oracles, provenance, precision, growth, error recovery, concurrency, allocations and real spill resizing. NativeAOT smoke includes both evaluators. No native-Excel differential certification is claimed.

Primary references: [MODE.SNGL](https://support.microsoft.com/en-us/excel/functions/mode-sngl-function), [MODE.MULT](https://support.microsoft.com/en-us/excel/functions/mode-mult-function), [MODE](https://support.microsoft.com/en-us/excel/functions/mode-function).
