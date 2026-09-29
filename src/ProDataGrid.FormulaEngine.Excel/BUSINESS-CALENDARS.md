# Business-calendar formulas

`WORKDAY`, `NETWORKDAYS`, `WORKDAY.INTL` and `NETWORKDAYS.INTL` share a bounded calendar backend. The existing non-international functions use Saturday/Sunday weekends; the international variants accept all documented numeric weekend codes and Monday-first seven-character masks. A mask character 1 excludes that weekday. NETWORKDAYS.INTL permits `1111111` and returns zero; WORKDAY.INTL rejects an all-weekend calendar with #NUM!.

```text
WORKDAY.INTL(DATE(2012,1,1),90,11)
NETWORKDAYS.INTL(DATE(2024,1,1),DATE(2024,1,7),"0000011")
WORKDAY.INTL(A1,{1;5;10},"0000011",H1:H20)
```

Date serials and offsets are truncated before calculation. NETWORKDAYS counts inclusive endpoints, returning a negative count when the truncated endpoints are reversed. WORKDAY excludes the starting day for nonzero offsets and returns the truncated starting serial for offset zero. Holiday ordering and duplicates do not change results; fractional holiday dates are truncated, blank/masked-out entries are ignored, and holidays on weekends are not subtracted twice. Invalid holiday values are not silently ignored.

The start/end/offset and weekend arguments support matching arrays and singleton row/column broadcasting. The entire holiday argument is one exclusion set shared by the result cells, not a broadcast control. Syntactic omissions preserve the default weekend; an explicitly supplied blank weekend coerces to code zero and is invalid. Errors in scalar controls are returned per result cell; invalid holiday data rejects the call.

## Bounds and algorithms

Supported serials are 0 through 2,958,465 for Windows1900 and 0 through 2,957,003 for Mac1904 (through 9999-12-31). Nonfinite dates, out-of-domain results and impossible offsets return #NUM!, including Int32.MinValue rather than throwing or iterating indefinitely. Malformed weekend strings return #VALUE!; unsupported numeric codes return #NUM!. Numeric weekend codes must be integral.

Full weeks are counted arithmetically, with at most six remainder days. Holiday keys encode weekday and serial, sort once and deduplicate in private storage. Counting working holidays uses at most seven pairs of lower-bound searches rather than scanning every date or holiday. Offset dates use monotone bisection over the supported domain, at most 22 steps. For H holiday entries and date-domain size D, preprocessing is O(H log H), counting is O(7 log H + 7), and offset selection is O(log D * (7 log H + 7)). Small holiday workspaces use stack storage; larger buffers are rented, cleared and returned in finally. Inputs are not sorted or mutated in place.

MaximumArrayCellCount bounds output cells and the supplied holiday entries before workspace allocation. It is not a total workbook memory, expression-work or elapsed-time quota. Array calls build the holiday index once and reuse it across scalar results, including different weekend masks.

## Compatibility changes and qualification

The legacy per-day loops are removed, not retained behind duplicate function registrations. Legacy WORKDAY no longer preserves fractional start times, and impossible/overflowing offsets now return errors. Both calendar families use the uninterrupted Excel serial weekday cycle, including fictitious serial 60. This changes the earlier engine's special handling of serials 0 through 60; modern dates and the 1904 epoch remain aligned with the Gregorian weekday cycle. Early-1900, numeric-code coercion and every locale-specific text-date rule are not certified against a running native Excel application.

Tests exercise official examples, every weekend mask in both date systems against independent Gregorian loops for modern dates, all numeric codes, source ownership, duplicates, pooled error exits, date boundaries, scalar allocations, array errors and spill recalculation. The reusable data benchmark compares complete long-range results against a prior-style day loop and includes short-range controls. It is not a claim of uniform speedups, native Excel timing or general workbook/thread-safety qualification. Other date functions retain their existing implementation and limits.

References:
- https://support.microsoft.com/en-us/excel/functions/workday-intl-function
- https://support.microsoft.com/en-us/excel/functions/networkdays-intl-function
- https://learn.microsoft.com/en-us/office/troubleshoot/excel/wrongly-assumes-1900-is-leap-year
