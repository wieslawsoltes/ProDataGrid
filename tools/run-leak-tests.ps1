param(
    [string]$Configuration = "Release",
    [string]$Filter = "FullyQualifiedName~LeakTests",
    [string]$ResultsDir = "artifacts/test",
    [string]$TestVerbosity = "detailed",
    [string]$TestLogger = "console;verbosity=detailed",
    [string]$TrxLogger = "trx;LogFileName=leak-tests.trx",
    [int]$HeartbeatSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$dotnet = (Get-Command dotnet).Source
if (-not $dotnet) {
    throw "dotnet not found in PATH."
}

$testProject = Join-Path $repoRoot "src\\Avalonia.Controls.DataGrid.LeakTests\\Avalonia.Controls.DataGrid.LeakTests.csproj"
if (-not (Test-Path $testProject)) {
    throw "Test project not found at $testProject"
}

$resultsDirFull = Join-Path $repoRoot $ResultsDir
New-Item -ItemType Directory -Path $resultsDirFull -Force | Out-Null

$arguments = @(
    "test"
    $testProject
    "-c"
    $Configuration
    "-v"
    $TestVerbosity
    "--logger"
    $TestLogger
    "--logger"
    $TrxLogger
    "--results-directory"
    $resultsDirFull
    "--filter"
    $Filter
)

Write-Host "dotnet: $dotnet"
Write-Host "Test project: $testProject"
Write-Host "Results dir: $resultsDirFull"

$process = Start-Process -FilePath $dotnet -ArgumentList $arguments -NoNewWindow -PassThru
$startTime = Get-Date

if ($HeartbeatSeconds -gt 0) {
    while (-not $process.WaitForExit($HeartbeatSeconds * 1000)) {
        $elapsed = (Get-Date) - $startTime
        Write-Host ("Leak tests still running... elapsed {0:hh\:mm\:ss}" -f $elapsed)
    }
} else {
    $process.WaitForExit()
}

$process.Refresh()
if ($process.ExitCode -ne 0) {
    throw "Leak tests exited with code $($process.ExitCode)."
}
