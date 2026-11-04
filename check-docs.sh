#!/bin/bash
set -euo pipefail

export IsDocFx=true
dotnet tool restore
dotnet build src/ProDataGrid.SourceGenerators/ProDataGrid.SourceGenerators.csproj -c Release
dotnet docfx docfx/docfx.json
