#!/usr/bin/env bash
set -euo pipefail

dotnet run -c Release --project tests/ProDiagnostics.Remote.Protocol.Benchmarks/ProDiagnostics.Remote.Protocol.Benchmarks.csproj "$@"

