# TEXTSPLIT

`TEXTSPLIT(text, column_delimiter, [row_delimiter], [ignore_empty], [match_mode], [pad_with])` splits text into an independently owned rectangular array. The bounded two-pass implementation replaces the earlier nested-list implementation; existing scalar source and option intersection is retained. It composes with LET, higher-order functions and worksheet spill/dependency recalculation.

```text
TEXTSPLIT("1,2,3;4,5,6",",",";")
TEXTSPLIT("a.b-c",{".","-"})
TEXTSPLIT("a;b;c",,";")
TEXTSPLIT("a,b;c",",",";",,,"missing")
```

Delimiter arrays are read in full in row-major order. Matches are literal, not wildcards. Case-sensitive ordinal comparison is the default; match mode 1 selects ordinal case-insensitive matching. Same-position matches prefer the first listed delimiter and do not overlap. Row delimiters are applied before column delimiters. Missing columns are padded with #N/A or the supplied scalar pad value. Source values, delimiter arrays and masks are never modified.

Empty fields are retained as empty text by default. `ignore_empty=TRUE` removes empty segments separately at each axis; a retained row with no column fields is padded to the other rows' width. An empty source or a result with no rows/columns returns #CALC!, rather than manufacturing a one-cell result. Empty delimiter entries are ignored; having no nonempty delimiter on either axis returns #VALUE!. These explicit edge contracts are regression-tested implementation behavior, not exhaustive native-Excel qualification.

Array-valued source text and scalar options use the existing address-aware implicit-intersection rule. This is not broadcasting independent source texts into nested split arrays. Input expressions are evaluated once. Syntactically omitted arguments retain omission metadata; direct eager invocation treats only absent arguments as omitted. Error-valued padding is stored only where padding is needed. Numeric-looking fields remain text.

## Allocation and complexity

A count-only pass checks the final rectangle, including padding, against MaximumArrayCellCount and worksheet dimension ceilings before allocating output strings or the result array. A second pass creates the owned result and only its final field strings. No split list, substring row copy, per-row token array or per-cell argument buffer is built. Delimiter-array storage is separately bounded by the same cell policy. Each produced field uses the existing 32,767 UTF-16-code-unit text budget; the entire source may be larger if its resulting fields fit.

A single delimiter uses runtime span search. Multiple delimiters advance through candidate positions once per pass and compare prefixes in list order; absent delimiters do not rescan the entire suffix for every output token. Worst-case work is proportional to source length times delimiter count and compared prefix length. This is not a total CPU/time or workbook-memory quota, and allocating many final strings still has a cost.

Tests compare both evaluators and formatter round trips, seeded independent String.Split results, delimiter ownership, bounds, allocation-free rejection, errors and real spill resizing. The reusable benchmark/native-smoke executable records validated outputs and allocation/time samples. This is not CSV parsing: quoting, escaping and culture-specific delimiter inference are not added. Ordinal matching, empty/fallback interactions, overlapping delimiter priorities and array-source behavior are not native Excel differential certified.

Official syntax: https://support.microsoft.com/en-us/excel/functions/textsplit-function
