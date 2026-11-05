#!/usr/bin/env python3
"""Run the published review harness in isolated, exact-revision worktrees."""
import argparse
import json
import os
from pathlib import Path
import shutil
import statistics
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='Fresh output directory; never reuse recorded samples')
    parser.add_argument('--base', required=True)
    parser.add_argument('--candidate', required=True)
    parser.add_argument('--scenario', action='append', required=True)
    parser.add_argument('--baseline-scenario', help='Override A workload for same-revision mode comparisons')
    parser.add_argument('--supplemental', action='store_true')
    parser.add_argument('--tracked-candidate', action='store_true')
    parser.add_argument('--order', choices=['ABBABAAB', 'BAABABBA'], default='ABBABAAB')
    args = parser.parse_args()
    repo, output = args.repo.resolve(), args.output.resolve()
    if output.exists():
        parser.error('--output must not already exist; preserve previous samples')
    output.mkdir(parents=True)
    source = Path(__file__).resolve().parent
    name = 'SupplementalReviewPerformanceTests' if args.supplemental else 'ReviewPerformanceTests'
    revisions = {}
    worktrees = {}
    # Build both revisions before measuring either one. Do not run unrelated builds/tests concurrently.
    for side, revision in [('A', args.base), ('B', args.candidate)]:
        sha = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', revision + '^{commit}'], text=True).strip()
        revisions[side] = sha
        worktree = output / side
        subprocess.run(['git', '-C', str(repo), 'worktree', 'add', '--detach', str(worktree), sha], check=True)
        worktrees[side] = worktree
        shutil.copy2(source / (name + '.cs'), worktree / 'src/Avalonia.Controls.DataGrid.UnitTests' / (name + '.cs'))
        command = ['dotnet', 'build', 'src/Avalonia.Controls.DataGrid.UnitTests', '-c', 'Release', '-p:CollectCoverage=false', '--verbosity', 'quiet']
        if side == 'B' and args.tracked_candidate:
            command.append('-p:DefineConstants=REVIEW_TRACKED_CELLS')
        with (output / (side + '-build.log')).open('w') as log:
            subprocess.run(command, cwd=worktree, stdout=log, stderr=subprocess.STDOUT, check=True)
    (output / 'revisions.json').write_text(json.dumps(revisions, indent=2))
    with (output / 'dotnet-info.txt').open('w') as log:
        subprocess.run(['dotnet', '--info'], cwd=repo, stdout=log, check=True)
    for scenario in args.scenario:
        if not scenario.replace('-', '').isalnum():
            parser.error('scenario must be an alphanumeric harness scenario, optionally containing hyphens')
        for index, side in enumerate(args.order):
            actual_scenario = args.baseline_scenario if side == 'A' and args.baseline_scenario else scenario
            result = output / f'{scenario}-{side}.jsonl'
            env = dict(os.environ, DOTNET_TieredCompilation='0', REVIEW_SCENARIO=actual_scenario, REVIEW_OUTPUT=str(result))
            assembly = worktrees[side] / 'src/Avalonia.Controls.DataGrid.UnitTests/bin/Release/net10.0/Avalonia.Controls.DataGrid.UnitTests.dll'
            command = ['dotnet', str(assembly), '-class', '*'+name, '-parallel', 'none']
            with (output / f'{scenario}-{index}-{side}.log').open('w') as log:
                subprocess.run(command, cwd=worktrees[side], env=env, stdout=log, stderr=subprocess.STDOUT, timeout=240, check=True)
        summary = {'scenario': scenario, 'revisions': revisions, 'order': args.order}
        for side in 'AB':
            samples = [json.loads(line) for line in (output / f'{scenario}-{side}.jsonl').read_text().splitlines()]
            if len(samples) != 4:
                raise RuntimeError(f'Expected four successful processes for {scenario}/{side}, got {len(samples)}')
            summary[side] = {'process_mean_ms': [statistics.mean(s['times']) for s in samples],
                             'process_mean_bytes': [statistics.mean(s['bytes']) for s in samples]}
        with (output / 'summary.jsonl').open('a') as log:
            log.write(json.dumps(summary) + '\n')
        print(json.dumps(summary), flush=True)
    print('Worktrees and logs retained under', output)
    print('When no longer needed, remove only these generated worktrees with git worktree remove --force, then remove the output directory.')


if __name__ == '__main__':
    main()
