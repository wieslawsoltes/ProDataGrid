#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

RUN_ID="$(date -u +%Y%m%d-%H%M%S)"
PROTOCOL_ARTIFACT_DIR="artifacts/remote-protocol-bench/nightly-${RUN_ID}"
WEB_METRICS_FILE="${ROOT_DIR}/artifacts/remote-webclient-perf/nightly-metrics-${RUN_ID}.json"
SUMMARY_JSON="artifacts/perf-gates/perf-nightly-summary.json"
SUMMARY_MD="artifacts/perf-gates/perf-nightly-summary.md"
HISTORY_FILE="${PERF_HISTORY_FILE:-.github/perf-history/remote-perf-history.json}"

mkdir -p "$PROTOCOL_ARTIFACT_DIR" artifacts/remote-webclient-perf artifacts/perf-gates artifacts/remote-hostload "$(dirname "$HISTORY_FILE")"

overall_status=0

run_step() {
  local label="$1"
  shift
  echo "[perf-nightly] ${label}"
  if ! "$@"; then
    echo "[perf-nightly] step failed: ${label}" >&2
    overall_status=1
  fi
}

run_step "running full protocol benchmark suite" \
  dotnet run -c Release --project tests/ProDiagnostics.Remote.Protocol.Benchmarks/ProDiagnostics.Remote.Protocol.Benchmarks.csproj -- \
    --job short \
    --artifacts "$PROTOCOL_ARTIFACT_DIR"

run_step "running medium host load scenario" \
  dotnet run -c Release --project tests/ProDiagnostics.Remote.HostLoad/ProDiagnostics.Remote.HostLoad.csproj -- \
    --profile medium \
    --duration-seconds 20

run_step "running perf integration suite (integration)" \
  dotnet test -c Release src/ProDiagnostics.IntegrationTests/ProDiagnostics.IntegrationTests.csproj --filter "Category=Perf"

run_step "running perf integration suite (managed devtools)" \
  dotnet test -c Release tests/ProDiagnostics.ManagedDevTools.PerfTests/ProDiagnostics.ManagedDevTools.PerfTests.csproj --filter "Category=Perf"

run_step "running web perf suite" \
  env PRODIAGNOSTICS_WEB_PERF_METRICS="$WEB_METRICS_FILE" bash tools/perf/run-remote-webclient-perf.sh

echo "[perf-nightly] evaluating thresholds and updating trend history"
if ! node tools/perf/evaluate-perf-gates.mjs \
  --config tools/perf/perf-gates.config.json \
  --protocol-dir "$PROTOCOL_ARTIFACT_DIR/results" \
  --hostload-dir artifacts/remote-hostload \
  --web-metrics "$WEB_METRICS_FILE" \
  --summary-json "$SUMMARY_JSON" \
  --summary-markdown "$SUMMARY_MD" \
  --history-file "$HISTORY_FILE" \
  --record-history; then
  overall_status=1
fi

echo "[perf-nightly] summary: $SUMMARY_MD"
exit "$overall_status"
