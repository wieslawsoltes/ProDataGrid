#!/usr/bin/env bash
set -euo pipefail

dotnet test src/ProDiagnostics.IntegrationTests/ProDiagnostics.IntegrationTests.csproj --filter "Category=Perf" "$@"

