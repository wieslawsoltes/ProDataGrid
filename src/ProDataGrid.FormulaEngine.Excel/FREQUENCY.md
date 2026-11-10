# Frequency distributions

`FREQUENCY(data_array,bins_array)` returns a vertical array of interval counts and one extra overflow count. Values equal to a boundary belong to that boundary's interval; the overflow row counts observations above every boundary.

```text
FREQUENCY({79;85;78;85;50;81;95;88;97},{70;79;89})
// 1;2;4;2
SUM(FREQUENCY(A1:A1000,{10;20;30}))
```

The function reads physical arrays row-major. Blank, text and Boolean elements are ignored, including masked-out positions; numeric zero is counted. Numeric scalar inputs are one-element distributions. Errors are propagated with their messages; nonfinite numeric inputs return `#NUM!`. The existing significant-digit settings apply to numeric observations and boundaries before comparisons.

## Ordering and ownership

Unsorted numeric boundaries are indexed privately and their interval counts returned in original numeric-boundary order. Equal boundaries use the first occurrence, with zero for later duplicates. Nonnumeric bin entries are ignored rather than retained as result rows. With no numeric bins, the one-row result is the count of numeric observations; with no numeric observations, all result rows are zero.

The function never sorts or changes a source array or mask. It has no retained cross-call cache. An eager invocation receives already evaluated arguments; the normal evaluator evaluates each argument once. Hosts must keep those values stable for the duration of the call.

## Resource and performance contract

Physical input positions and final result cells are bounded by `MaximumArrayCellCount`. The output also respects the worksheet row ceiling. Validate these bounds before allocating a result. Small bin/count workspaces use stack storage; larger ones use per-invocation pooled arrays cleared on every exit, including error paths. No copied observation array, per-bin observation rescans, per-observation objects or shared mutable state are introduced.

For n observations and b numeric bins, work is O(b log b + n log b), with O(b) workspace and O(b) owned output. A one-bin query is O(n); no-bin data counting is O(n). Eager argument evaluation or worksheet reference materialization can allocate before the function runs. These bounds are not a global memory, execution-time or concurrent-workbook quota. Warm zero-workspace allocation does not mean zero cold pool allocation or zero retained pool capacity.

## Qualification

The suite checks the documented example, equality/overflow intervals, unsorted/repeated bins, ignored inputs, masks, independent linear-assignment comparisons, error metadata, limits, source ownership, concurrent calls and real spill recalculation. Native Microsoft Excel differential qualification is not claimed: unusual unsorted/duplicate/nonnumeric-bin combinations, scalar logical coercion, locale behavior and every error-precedence interaction remain explicit qualification boundaries.

Primary syntax and empty-input reference: [Microsoft FREQUENCY](https://support.microsoft.com/en-us/excel/functions/frequency-function). The maintained aggregate benchmark records an independent linear-bin implementation alongside this implementation; that is an algorithm comparison, not a linked older release or native Excel speed claim.
