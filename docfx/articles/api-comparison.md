# API Comparison with WPF DataGrid

## Public API Comparison with WPF DataGrid

- Build ProDataGrid: `dotnet build src/Avalonia.Controls.DataGrid/Avalonia.Controls.DataGrid.csproj -c Release -f net8.0`
- Run the comparer: `dotnet run --project tools/api-compare -- --pro src/Avalonia.Controls.DataGrid/bin/Release/net8.0/Avalonia.Controls.DataGrid.dll --wpf ~/.nuget/packages/microsoft.windowsdesktop.app.ref/8.0.22/ref/net8.0/PresentationFramework.dll --out artifacts/api-diff`
- Results: `artifacts/api-diff/{wpf-api.json, prodatagrid-api.json, api-diff.json}` and a curated write-up in `docs/wpf-api-comparison.md`.
