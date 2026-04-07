#!/usr/bin/env bash
set -euo pipefail

dotnet run -c Release --project tests/ProDiagnostics.Remote.HostLoad/ProDiagnostics.Remote.HostLoad.csproj "$@"

