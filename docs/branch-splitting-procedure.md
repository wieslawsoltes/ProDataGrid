# Split Charting and Formula Engine Work Into Separate Branches

## Purpose
Create two clean branches that isolate charting work from formula engine work while keeping unrelated changes out of both branches.

## Current Working Tree Snapshot (analysis only)
- Base branch: `master` (tracking `origin/master`).
- Working tree is dirty with a mix of charting, formula engine, docs, and unrelated core/DataGrid changes.

### Charting-related paths currently present
- `src/ProCharts/`
- `src/ProCharts.Skia/`
- `src/ProCharts.Avalonia/`
- `src/ProDataGrid.Charting/`
- `src/Avalonia.Controls.DataGrid.UnitTests/Charting/`
- `src/DataGridSample/Pages/ChartingPage.axaml`
- `src/DataGridSample/Pages/ChartingPage.axaml.cs`
- `src/DataGridSample/Pages/PivotChartPage.axaml`
- `src/DataGridSample/ViewModels/ChartSampleViewModel.cs`
- `src/DataGridSample/ViewModels/ChartingSampleViewModel.cs`
- `src/DataGridSample/ViewModels/PivotChartViewModel.cs`
- `docfx/articles/procharts-*.md`
- `docfx/articles/chart-gallery.md`
- (Likely shared) `src/DataGridSample/MainWindow.axaml` and `docfx/articles/toc.yml`

### Formula-engine-related paths currently present
- `src/ProDataGrid.FormulaEngine/`
- `src/ProDataGrid.FormulaEngine.Excel/`
- `src/ProDataGrid.FormulaEngine.UnitTests/`
- `src/ProDataGrid.FormulaEngine.Benchmarks/`
- `src/Avalonia.Controls.DataGrid/Formulas/`
- `src/Avalonia.Controls.DataGrid/ColumnDefinitions/DataGridFormulaColumnDefinition.cs`
- `src/Avalonia.Controls.DataGrid/DataGridFormulaTextColumn.cs`
- `src/DataGridSample/Models/Formula*.cs`
- `src/DataGridSample/Pages/Formula*.axaml`
- `src/DataGridSample/Pages/Formula*.axaml.cs`
- `src/DataGridSample/ViewModels/Formula*.cs`
- `docfx/articles/formula-engine-*.md`
- `docs/formula-engine-*.md`
- (Likely shared) `src/DataGridSample/DataGridSample.csproj`, `src/Avalonia.Controls.DataGrid/Avalonia.Controls.DataGrid.csproj`, `docfx/docfx.json`

### Unrelated or mixed changes to keep out of both branches
- Many core DataGrid changes in `src/Avalonia.Controls.DataGrid/` (rows, state, templates, themes).
- Leak/pivot/unit test changes unrelated to charting/formulas.
- ProDiagnostics changes in `src/ProDiagnostics*/`.
- Any other modified files not listed above.

## Recommended Procedure (two worktrees, safest)
This avoids cross-contamination and keeps unrelated changes out.

1) Record the base commit for both branches:
   - `git rev-parse HEAD`

2) Create two branches at the current base commit:
   - `git branch charts-work <base-sha>`
   - `git branch formula-work <base-sha>`

3) Add two worktrees:
   - `git worktree add ../Avalonia.Controls.DataGrid-charts charts-work`
   - `git worktree add ../Avalonia.Controls.DataGrid-formula formula-work`

4) In the current worktree, create two targeted stashes (pathspec-based):
   - Charting stash (only charting paths):
     - `git stash push -u -m "charting-split" -- <chart-paths...>`
   - Formula stash (only formula paths):
     - `git stash push -u -m "formula-split" -- <formula-paths...>`
   - Leave unrelated changes untouched in the original worktree.

5) In the charts worktree:
   - `git stash apply stash^{/charting-split}`
   - Resolve any conflicts.
   - Remove any formula or unrelated files accidentally pulled in.
   - `git status` should show only charting-related paths.

6) In the formula worktree:
   - `git stash apply stash^{/formula-split}`
   - Resolve any conflicts.
   - Remove any charting or unrelated files accidentally pulled in.
   - `git status` should show only formula-related paths.

7) Handle shared files explicitly:
   - For `MainWindow.axaml`, `docfx/articles/toc.yml`, and `docfx/docfx.json`:
     - If changes are chart-only, keep on chart branch and revert on formula branch.
     - If changes are formula-only, keep on formula branch and revert on chart branch.
     - If they genuinely contain both, split them or keep a minimal common version on both branches.

8) Verify each branch cleanly builds the relevant scope (optional but recommended):
   - Chart branch: build ProCharts and sample pages.
   - Formula branch: build formula engine projects and formula samples.

9) Commit on each branch only after the file scopes are cleanly isolated.

## Alternative Procedure (single worktree, interactive adds)
Use this only if you want to avoid worktrees.

1) Create branches:
   - `git branch charts-work <base-sha>`
   - `git branch formula-work <base-sha>`

2) Checkout `charts-work` and stage only charting changes:
   - Use `git add -p` and/or `git add -- <chart-paths...>`.
   - Commit.

3) Reset working tree back to base:
   - `git reset --mixed <base-sha>` (keeps working tree, clears index).

4) Checkout `formula-work` and stage only formula changes:
   - Use `git add -p` and/or `git add -- <formula-paths...>`.
   - Commit.

5) Manually resolve shared files on each branch as in the worktree approach.

## Suggested Path Lists (copy/paste for pathspec)
### Charting
- `src/ProCharts/`
- `src/ProCharts.Skia/`
- `src/ProCharts.Avalonia/`
- `src/ProDataGrid.Charting/`
- `src/Avalonia.Controls.DataGrid.UnitTests/Charting/`
- `src/DataGridSample/Pages/ChartingPage.axaml`
- `src/DataGridSample/Pages/ChartingPage.axaml.cs`
- `src/DataGridSample/Pages/PivotChartPage.axaml`
- `src/DataGridSample/ViewModels/ChartSampleViewModel.cs`
- `src/DataGridSample/ViewModels/ChartingSampleViewModel.cs`
- `src/DataGridSample/ViewModels/PivotChartViewModel.cs`
- `docfx/articles/procharts-*.md`
- `docfx/articles/chart-gallery.md`

### Formula engine
- `src/ProDataGrid.FormulaEngine/`
- `src/ProDataGrid.FormulaEngine.Excel/`
- `src/ProDataGrid.FormulaEngine.UnitTests/`
- `src/ProDataGrid.FormulaEngine.Benchmarks/`
- `src/Avalonia.Controls.DataGrid/Formulas/`
- `src/Avalonia.Controls.DataGrid/ColumnDefinitions/DataGridFormulaColumnDefinition.cs`
- `src/Avalonia.Controls.DataGrid/DataGridFormulaTextColumn.cs`
- `src/DataGridSample/Models/Formula*.cs`
- `src/DataGridSample/Pages/Formula*.axaml`
- `src/DataGridSample/Pages/Formula*.axaml.cs`
- `src/DataGridSample/ViewModels/Formula*.cs`
- `docfx/articles/formula-engine-*.md`
- `docs/formula-engine-*.md`

## Final Check Before Committing
- `git status` on each branch should show only the intended scope.
- Shared files are either split or intentionally duplicated with scope-specific edits.
- Unrelated changes remain only in the original worktree or a separate branch.
