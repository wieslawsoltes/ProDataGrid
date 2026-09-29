# Bitwise formulas

`BITAND`, `BITOR`, `BITXOR`, `BITLSHIFT` and `BITRSHIFT` use the existing scalar/array function infrastructure. Operands are validated before machine operations; invalid shift counts never wrap under C# shift-count masking. Inputs are never modified.

```text
BITAND(13,25)             // 9
BITOR(9,12)              // 13
BITXOR(5,6)              // 3
BITLSHIFT(4,2)           // 16
BITRSHIFT(13,2)          // 3
BITAND({1;2;3},{1,2})    // 3-by-2 result
```

## Implemented contracts

Numeric operands must be finite integers from zero through 2^48-1. Shift amounts must be integers from -53 through 53. A negative amount reverses direction. Right shifts discard low bits and become zero when the magnitude exceeds the input width. Left shifts reject a result above 2^48-1 with `#NUM!`; zero can be shifted by any permitted amount. The result check precedes the operation and cannot silently overflow.

The shared engine coercion policy applies to scalar values, including text numbers and blank cells. Invalid nonnumeric values return `#VALUE!`, invalid numeric domains return `#NUM!`, and existing formula error metadata is propagated. Configured significant-digit normalization applies before integer validation and to numeric output.

Equal-sized arrays and singleton row/column broadcasting use readonly argument views. Sparse holes act as blanks; output dimensions are checked against `MaximumArrayCellCount` before allocation. This setting is a per-result policy, not a workbook-wide memory or execution-time limit.

## Validation and qualification

The regression suite checks compiled/interpreted and formatter paths with precision on/off, every byte pair against independent bit expansion, every allowed shift using exact-integer arithmetic, source masks, invalid domains, concurrent independent calls, scalar allocation and dependency/spill recalculation. Executed counts and evidence belong in the PR rather than being inferred from authored tests.

This is not native Microsoft Excel differential certification. The integer-only shift rejection and 48-bit shifted-result ceiling are explicit implementation contracts; every undocumented native error/coercion interaction has not been verified against Excel. Thread safety of mutable host inputs, workbooks and registries remains the host's responsibility.

Primary syntax references: [BITAND](https://support.microsoft.com/en-us/excel/functions/bitand-function), [BITOR](https://support.microsoft.com/en-us/excel/functions/bitor-function), [BITXOR](https://support.microsoft.com/en-us/excel/functions/bitxor-function), [BITLSHIFT](https://support.microsoft.com/en-us/excel/bitlshift-function), [BITRSHIFT](https://support.microsoft.com/en-us/excel/functions/bitrshift-function).
