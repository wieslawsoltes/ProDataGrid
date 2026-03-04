# ProDiagnostics Remote Web Client Perf

Playwright suite for remote web client performance sanity checks against a real remote host.

## Run

```bash
cd tests/ProDiagnostics.Remote.WebClient.Perf
npm install
npm run install:browsers
npm test
```

The suite starts:
- a static server for `src/ProDiagnostics.Remote.WebClient`,
- a headless remote diagnostics host (`PerfHost`) on `ws://127.0.0.1:29414/attach`.

## What is measured

- connect handshake time,
- first tree snapshot render time,
- tree filter response time,
- expand/collapse response time.

Note:
- Expand/collapse assertions auto-skip when the active tree renderer mode does not expose explicit expander controls.
