import subprocess

def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()

conflicts = git('diff', '--name-only', '--diff-filter=U').splitlines()
allowed = {'tools/formula-benchmarks/Program.cs', 'tools/formula-benchmarks/README.md'}
assert conflicts and set(conflicts).issubset(allowed), conflicts
for path in conflicts:
    # Only resolve add/add conflicts against the exact already-reviewed PR #355 files.
    expected = git('rev-parse', f'd64efcb07798334d2d742d1ee538ebe955fad5be:{path}')
    actual = git('rev-parse', f':3:{path}')
    assert expected == actual, (path, expected, actual)
    subprocess.run(['git', 'checkout', '--ours', '--', path], check=True)
    subprocess.run(['git', 'add', path], check=True)
assert not git('diff', '--name-only', '--diff-filter=U')
