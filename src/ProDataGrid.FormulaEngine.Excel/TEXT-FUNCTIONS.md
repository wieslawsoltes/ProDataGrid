# Text functions and Unicode indexing

The registry adds `FIND`, `SEARCH`, `TEXTBEFORE`, `TEXTAFTER`, `EXACT`, `CLEAN`, `PROPER`, `REPT`, `REPLACE`, `SUBSTITUTE`, `UNICODE` and `UNICHAR`.

These functions share an elementwise implementation supporting scalar inputs, equally shaped arrays, and singleton row/column broadcasting. Input expressions are evaluated once. Each output cell uses a readonly argument view instead of allocating a parameter array or capturing delegate. Sparse holes are blank values. Invalid shapes return `#VALUE!`, and `MaximumArrayCellCount` is checked before allocating a result.

```text
TEXTBEFORE("Ada,Lovelace",",")
TEXTAFTER("path/to/file","/",-1)
SEARCH("b?d","xxBADyy")
SUBSTITUTE("a-b-a","a","x",2)
REPLACE("abcdefghijk",6,5,"*")
EXACT({"a";"b"},{"a","b"})
```

`FIND` is case-sensitive and literal; `SEARCH` ignores case and supports `?`, `*` and tilde escaping. Searches return one-based positions and honor an optional start position. `TEXTBEFORE` and `TEXTAFTER` support positive/negative occurrences, case mode, a virtual end delimiter and a not-found fallback. Delimiters are literal rather than wildcard expressions. Omitted optional arguments are distinguished from explicitly supplied blank cells by the lazy evaluation interface. Direct eager function invocation treats only absent arguments as omitted.

`SUBSTITUTE` uses case-sensitive, nonoverlapping occurrences and can replace one specified occurrence or all occurrences. `REPT`, `REPLACE` and `SUBSTITUTE` check expanded output size with widened arithmetic before constructing the final string. `CLEAN` removes ASCII controls 0 through 31 only. `PROPER` lowercases input and capitalizes letters following nonletters; it uses workbook culture for BMP letter casing, not acronym-preserving title-case conventions. Unicode conversion rejects incomplete surrogate sequences; `UNICHAR` rejects zero/out-of-range values and returns `#N/A` for a surrogate code point.

## Compatibility version

Existing workbooks retain the original UTF-16 indexing behavior by default:

```csharp
workbook.Settings.TextCompatibilityVersion = FormulaTextCompatibilityVersion.Version1;
// Opt in to treating a valid surrogate pair as one character:
workbook.Settings.TextCompatibilityVersion = FormulaTextCompatibilityVersion.Version2;
```

This affects `LEN`, `LEFT`, `RIGHT`, `MID`, `FIND`, `SEARCH` and `REPLACE`. For example, `LEN("A😀B")` is 4 in version 1 and 3 in version 2. Combining marks and variation selectors remain separate characters; this is not grapheme-cluster indexing. Invalid lone surrogates are not normalized by slicing. `MID` now clamps very large counts before index arithmetic so a request such as `MID("abcd",2,2147483647)` returns the remaining substring instead of overflowing.

## Performance and limits

Literal searches use ordinal runtime search without token allocation. Short wildcard patterns use stack storage; long patterns rent numeric token storage, returned in `finally`. Star-separated segments are processed without recursive backtracking; worst-case comparison work can still be proportional to text length times pattern length. This is not a global execution quota.

Repetition fills one final string by copying doubled spans. Replacement uses span concatenation; substitution counts matches then fills one correctly sized result, without a split list or repeated concatenation. New text-producing functions enforce a 32,767 UTF-16-code-unit output budget; this is a conservative allocation contract even under character-index version 2. It is not a new global limit on all existing string functions, host values or whole-workbook memory.

Tests cover both evaluators, formatter round trips, omissions, case and wildcard escapes, surrogate/combining-mark indexing, source ownership and array limits, exhaustive small wildcard inputs against an independent matcher, and seeded substitution/slicing comparisons. These checks do not constitute native Microsoft Excel differential qualification. Every locale-specific comparison/casing edge, unusual shape/error interaction, empty/fallback interaction and workbook-format behavior is not certified. Case-insensitive search uses ordinal Unicode folding, not every Excel locale collation rule. Legacy byte-oriented `*B` functions and system-code-page `CHAR`/`CODE` are not implemented here.

Official syntax and compatibility references: [SEARCH](https://support.microsoft.com/en-us/excel/functions/search-function), [REPLACE](https://support.microsoft.com/en-us/excel/functions/replace-function), [TEXTBEFORE](https://support.microsoft.com/en-us/excel/functions/textbefore-function), [TEXTAFTER](https://support.microsoft.com/en-us/excel/functions/textafter-function), [REPT](https://support.microsoft.com/en-us/excel/functions/rept-function), [SUBSTITUTE](https://support.microsoft.com/en-us/excel/functions/substitute-function), [UNICODE](https://support.microsoft.com/en-us/excel/functions/unicode-function), [UNICHAR](https://support.microsoft.com/en-us/excel/functions/unichar-function).
