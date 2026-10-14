name: ProCharts templated controls
on:
  push:
    branches: [feat/procharts-templated-browser]
  pull_request:
    paths: ['src/ProCharts*/**', 'samples/ProCharts.*/**', 'tests/ProCharts.Controls.Tests/**', '.github/workflows/procharts-controls.yml', 'Directory.*.props']
  workflow_dispatch:
permissions:
  contents: read
concurrency:
  group: procharts-controls-${{ github.ref }}
  cancel-in-progress: true
jobs:
  managed:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Build both library targets and desktop sample
        shell: bash
        run: |
          set -o pipefail
          mkdir -p artifacts
          dotnet build src/ProCharts.Controls/ProCharts.Controls.csproj -c Release 2>&1 | tee artifacts/controls-build.log
          dotnet publish samples/ProCharts.Sample.Desktop/ProCharts.Sample.Desktop.csproj -c Release -o artifacts/desktop 2>&1 | tee artifacts/desktop-build.log
      - name: Headless control and data tests
        shell: bash
        run: |
          set -o pipefail
          dotnet test tests/ProCharts.Controls.Tests/ProCharts.Controls.Tests.csproj -c Release --logger trx --results-directory artifacts/tests 2>&1 | tee artifacts/control-tests.log
      - name: Existing chart regression suite
        shell: bash
        run: |
          set -o pipefail
          dotnet test src/Avalonia.Controls.DataGrid.UnitTests/Avalonia.Controls.DataGrid.UnitTests.csproj -c Release -p:CollectCoverage=false --filter FullyQualifiedName~Charting --logger trx --results-directory artifacts/regressions 2>&1 | tee artifacts/regressions.log
      - name: Pack reusable libraries
        shell: bash
        run: |
          for project in ProCharts ProCharts.Skia ProCharts.Avalonia ProCharts.Controls; do
            dotnet pack src/$project/$project.csproj -c Release -o artifacts/packages
          done
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: procharts-managed
          path: artifacts
          retention-days: 14
  browser:
    runs-on: ubuntu-latest
    timeout-minutes: 25
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Install WebAssembly toolchain
        run: dotnet workload install wasm-tools
      - name: Publish actual Avalonia browser application
        shell: bash
        run: |
          set -o pipefail
          mkdir -p artifacts/source
          git archive HEAD -o artifacts/source/source.zip
          git rev-parse HEAD > artifacts/source/commit.txt
          git rev-parse HEAD^{tree} > artifacts/source/tree.txt
          (cd artifacts/source && sha256sum source.zip > SHA256SUMS.txt)
          dotnet publish samples/ProCharts.Sample.Browser/ProCharts.Sample.Browser.csproj -c Release -o artifacts/browser 2>&1 | tee artifacts/browser-build.log
      - name: Install browser test dependencies
        run: |
          python -m pip install playwright==1.55.0 Pillow==11.3.0
          python -m playwright install --with-deps chromium
      - name: Exercise the published WebAssembly application in Chromium
        shell: bash
        run: |
          set -o pipefail
          python tests/ProCharts.Browser/validate.py --root artifacts/browser/wwwroot --output artifacts/browser-tests 2>&1 | tee artifacts/browser-tests.log
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: procharts-browser
          path: artifacts
          retention-days: 14
