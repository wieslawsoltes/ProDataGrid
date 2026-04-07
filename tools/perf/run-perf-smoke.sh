#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

RUN_ID="$(date -u +%Y%m%d-%H%M%S)"
PROTOCOL_ARTIFACT_DIR="artifacts/remote-protocol-bench/${RUN_ID}"
WEB_METRICS_FILE="${ROOT_DIR}/artifacts/remote-webclient-perf/metrics-${RUN_ID}.json"
SUMMARY_JSON="artifacts/perf-gates/perf-smoke-summary.json"
SUMMARY_MD="artifacts/perf-gates/perf-smoke-summary.md"

mkdir -p "$PROTOCOL_ARTIFACT_DIR" artifacts/remote-webclient-perf artifacts/perf-gates artifacts/remote-hostload

overall_status=0

run_step() {
  local label="$1"
  shift
  echo "[perf-smoke] ${label}"
  if ! "$@"; then
    echo "[perf-smoke] step failed: ${label}" >&2
    overall_status=1
  fi
}

run_step "running protocol microbenchmarks" \
  dotnet run -c Release --project tests/ProDiagnostics.Remote.Protocol.Benchmarks/ProDiagnostics.Remote.Protocol.Benchmarks.csproj -- \
    --job short \
    --filter \
    "*RemoteMessageSerializerBenchmarks.Deserialize_Request*" \
    "*RemoteReadOnlyRouterBenchmarks.Handle_TreeSnapshot*" \
    "*RemoteStreamSessionHubBenchmarks.Publish_FanoutBurst*" \
    --artifacts "$PROTOCOL_ARTIFACT_DIR"

run_step "running host load sanity scenario" \
  dotnet run -c Release --project tests/ProDiagnostics.Remote.HostLoad/ProDiagnostics.Remote.HostLoad.csproj -- \
    --profile small \
    --duration-seconds 8

run_step "running managed perf tests (integration)" \
  dotnet test -c Release src/ProDiagnostics.IntegrationTests/ProDiagnostics.IntegrationTests.csproj --filter "Category=Perf"

run_step "running managed perf tests (devtools)" \
  dotnet test -c Release tests/ProDiagnostics.ManagedDevTools.PerfTests/ProDiagnostics.ManagedDevTools.PerfTests.csproj --filter "Category=Perf"

run_step "running web perf smoke tests" \
  env PRODIAGNOSTICS_WEB_PERF_METRICS="$WEB_METRICS_FILE" bash tools/perf/run-remote-webclient-perf.sh

echo "[perf-smoke] evaluating perf thresholds"
if ! node tools/perf/evaluate-perf-gates.mjs \
  --config tools/perf/perf-gates.config.json \
  --protocol-dir "$PROTOCOL_ARTIFACT_DIR/results" \
  --hostload-dir artifacts/remote-hostload \
  --web-metrics "$WEB_METRICS_FILE" \
  --summary-json "$SUMMARY_JSON" \
  --summary-markdown "$SUMMARY_MD"; then
  overall_status=1
fi

echo "[perf-smoke] summary: $SUMMARY_MD"
exit "$overall_status"
