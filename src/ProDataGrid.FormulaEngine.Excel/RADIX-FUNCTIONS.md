# Number-base conversions

The registry provides `BIN2DEC`, `OCT2DEC`, `HEX2DEC`, `DEC2BIN`, `DEC2OCT`, `DEC2HEX`, all six cross-conversions between binary/octal/hexadecimal, and `BASE`/`DECIMAL`. These use the shared scalar/array evaluator, not a separate expression engine.

## Fixed-width signed encodings

Binary inputs use ten bits, octal thirty bits and hexadecimal forty bits. The highest bit is a two's-complement sign bit. Inputs accept at most ten ASCII digits, case-insensitively. Cross-conversions check the signed target range before formatting; negative output always occupies ten characters and is sign-extended for the target width.

```text
BIN2DEC("1111111111")     // -1
HEX2BIN("FFFFFFFE00")    // 1000000000 (-512)
DEC2HEX(-54)             // FFFFFFFFCA
DEC2HEX(100,4)           // 0064
BIN2OCT(1001,3)          // 011
```

Decimal numbers and places are truncated before range validation. Supplied places must be from 1 through 10, including for a negative value; a valid supplied width is then ignored for negative formatting. Positive values fail if their representation does not fit the requested places. An absent or syntactically omitted places argument uses the minimum width; an explicitly supplied blank coerces to zero and fails. Invalid places for negative inputs is an explicit contract requiring native-Excel edge qualification, not a claim that every undocumented native interaction matches.

## General unsigned bases

`BASE(number,radix,[minimum_length])` supports radices 2 through 36, numbers from zero through 2^53-1, and minimum lengths from zero through 255. It produces uppercase digits and leading zero padding. Fractional controls are truncated.

`DECIMAL(text,radix)` parses up to 255 ASCII digits, case-insensitively. It keeps the integer exact in UInt64 for common inputs and promotes to a bounded stack-limb accumulator when necessary. The final magnitude is rounded once to binary64 using round-to-nearest/ties-to-even, before the host's configured significant-digit policy. Unrepresentable values return `#NUM!`. This avoids cumulative floating-point multiply/add rounding; it does not introduce arbitrary-precision formula values.

```text
BASE(15,2,10)       // 0000001111
DECIMAL("zap",36)  // 45745
```

A zero-length input is zero. Whitespace, signs, radix prefixes and characters outside the selected alphabet return `#NUM!`; supplied formula errors preserve their metadata. Numeric/text coercion follows the shared engine. Existing precision settings can intentionally alter numeric inputs/results.

## Ownership, resources and validation

Functions support equally sized arrays and singleton row/column broadcasting. Sparse positions are blank values; input arrays/masks remain unchanged. The shared per-result allocation cap applies before output allocation. Formatting uses a stack buffer and a final owned string. General parsing uses at most 42 UInt32 stack limbs and no mutable shared workspace. This bounds one conversion, not total workbook memory, nested formulas or elapsed execution time.

Regression coverage includes all ten-bit encodings, seeded signed conversions, all 35 radices, exact BigInteger construction plus independent invariant-decimal parser rounding oracles, explicit halfway bits, overflow boundaries, invalid domains, omission forwarding, masks, concurrent calls, allocation checks and actual spill recalculation. Test or benchmark counts must come from executed CI, not the number of authored cases.

This is not native Excel differential certification. Unusual coercion/error precedence, invalid places on negative numbers, empty-input semantics and every locale/shape interaction remain separately qualified. No production BigInteger, reflection, unsafe code, external package or new permission is required.

Primary references: [BASE](https://support.microsoft.com/en-us/excel/functions/base-function), [DECIMAL](https://support.microsoft.com/en-us/excel/functions/decimal-function), [DEC2HEX](https://support.microsoft.com/en-us/excel/functions/dec2hex-function), [HEX2BIN](https://support.microsoft.com/en-us/excel/functions/hex2bin-function).
