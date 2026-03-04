# Preview and Elements 3D

Remote diagnostics includes two high-value visual channels:

- live app preview (`diagnostics.preview.*`)
- Elements 3D structural projection (`diagnostics.elements3d.*`)

## Preview

Read methods:

- `diagnostics.preview.capabilities.get`
- `diagnostics.preview.snapshot.get`

Mutation methods:

- `diagnostics.preview.paused.set`
- `diagnostics.preview.settings.set`
- `diagnostics.preview.input.inject`

Stream topic:

- `diagnostics.stream.preview`

Supported preview transports:

- `svg` (default)
- `png`

Preview settings include target FPS, max dimensions, scale, diff mode, and frame payload inclusion.

Input injection supports pointer, wheel, and keyboard-style events using `RemotePreviewInputRequest`.

## Elements 3D

Read method:

- `diagnostics.elements3d.snapshot.get`

Mutation methods:

- `diagnostics.elements3d.root.set`
- `diagnostics.elements3d.root.reset`
- `diagnostics.elements3d.filters.set`

Snapshot payload includes:

- node bounds/depth/z-index/visibility/opacity
- selected/scoped root ids
- depth range and layer filter state
- exploded 3D and 2D grid mode metadata
- optional SVG projection (`SvgSnapshot`, `SvgViewBox`)

## SVG Pipeline

Elements 3D SVG snapshots are generated from Avalonia rendering through Skia (`SKSvgCanvas`) using immediate rendering infrastructure. This keeps the exported vector projection aligned with managed diagnostics rendering.

## Practical Guidance

- Start with preview/metrics/profiler paused in remote sessions.
- Refresh/snapshot on demand for heavy trees.
- Use SVG for interactive pan/zoom inspection; switch to PNG only when needed for compatibility.
