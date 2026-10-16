from pathlib import Path
import subprocess

SCRIPT = Path(__file__).resolve().parents[2] / 'lib/prepare-master.sh'


def git(*args):
    return subprocess.check_output(['git', *map(str, args)], text=True, stderr=subprocess.PIPE).strip()


def commit(repo, value):
    (repo / 'data').write_text(value)
    git('-C', repo, 'add', 'data')
    git('-C', repo, 'commit', '-qm', value)
    return git('-C', repo, 'rev-parse', 'HEAD')


def test_stale_master_fetches_bundle_prerequisite(tmp_path):
    origin, sender, receiver = (tmp_path / name for name in ('origin.git', 'sender', 'receiver'))
    git('-c', 'init.defaultBranch=master', 'init', '-q', '--bare', origin)
    git('clone', '-q', origin, sender)
    git('-C', sender, 'config', 'user.email', 'test@example.invalid')
    git('-C', sender, 'config', 'user.name', 'test')
    commit(sender, 'A')
    git('-C', sender, 'push', '-q', 'origin', 'master')
    git('clone', '-q', origin, receiver)
    prerequisite = commit(sender, 'B')
    git('-C', sender, 'push', '-q', 'origin', 'master')
    wanted = commit(sender, 'C')
    run = tmp_path / 'run'
    (run / 'repo').mkdir(parents=True)
    git('-C', sender, 'update-ref', 'refs/bugreport/stale', wanted)
    git('-C', sender, 'bundle', 'create', run / 'repo/master-commits.bundle',
        'origin/master..refs/bugreport/stale')
    assert subprocess.run(['git', '-C', str(receiver), 'cat-file', '-e', prerequisite],
                          capture_output=True).returncode != 0
    # 本番のマスタ復元口を一時repoだけで実行する
    # Exercise the production master restoration entry using only temporary repositories
    program = '''set -euo pipefail
MASTER="$1"; RUN="$2"; MASTER_COMMIT="$3"; MASTER_WORKTREES="$4"; ID=stale
MASTER_FAILED=0; MASTER_DIFF_ABSENT=0; MASTER_DIFF_APPLY_FAILED=0; MASTER_UNTRACKED_FAILED=0
log() { echo "$*" >&2; }
source "$5"
test "$MASTER_FAILED" = 0
test "$(git -C "$MASTER_WORKTREE" rev-parse HEAD)" = "$MASTER_COMMIT"
'''
    subprocess.run(['bash', '-c', program, 'test', str(receiver), str(run), wanted,
                    str(tmp_path / 'worktrees'), str(SCRIPT)], check=True, capture_output=True)
    assert (tmp_path / 'worktrees/bugfix-stale/data').read_text() == 'C'
