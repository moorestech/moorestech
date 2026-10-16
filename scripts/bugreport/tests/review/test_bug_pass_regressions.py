import hashlib
import importlib.util
import json
from pathlib import Path
import sys

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from triage_lib import log_scan, train_timeline, rail_edges

spec = importlib.util.spec_from_file_location('restore_transfer', ROOT / 'lib/restore-commit-transfer.py')
transfer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(transfer)


def test_assert_is_selected_ahead_of_two_hundred_recent_warnings(tmp_path):
    (tmp_path / 'logs').mkdir()
    assertion = '12:00:00.000\t100\tAssert\tstation connection failed\n at Station.cs:10'
    warnings = [f'12:01:00.000\t{n}\tWarning\twarning {n}' for n in range(250)]
    (tmp_path / 'logs/unity.log').write_text('\n'.join([assertion, *warnings]))
    text = '\n'.join(log_scan.summarize(tmp_path))
    assert 'station connection failed' in text and 'at Station.cs:10' in text
    assert '重要ログ省略: 51' in text


def test_malformed_snapshot_name_does_not_hide_valid_train_or_rail(tmp_path, capsys):
    (tmp_path / 'snapshots').mkdir()
    (tmp_path / 'snapshots/tick_100.json').write_text(json.dumps({'trainUnits': [], 'railSegments': []}))
    bad = tmp_path / 'snapshots/tick_bad.json'
    bad.write_text('{}')
    trains = '\n'.join(train_timeline.summarize(tmp_path))
    rails = '\n'.join(rail_edges.summarize(tmp_path))
    assert '列車時系列: 0 行' in trains and 'tick=100 / レール: 0 件' in rails
    assert str(bad) in trains and str(bad) in rails and str(bad) in capsys.readouterr().err


def write_plan(box):
    (box / 'repo').mkdir()
    plan = []
    for number, name in enumerate(('commits.bundle', 'master-commits.bundle')):
        parts = []
        for index, value in enumerate((b'first', b'second')):
            path = f'__attachments/{number * 2 + index:06d}/chunk'
            part = box / path
            part.parent.mkdir(parents=True)
            part.write_bytes(value)
            parts.append(dict(path=path, bytes=len(value), sha256=hashlib.sha256(value).hexdigest()))
        plan.append(dict(path=f'repo/{name}', bytes=11, parts=parts))
    (box / 'repo/commit-transfer.json').write_text(json.dumps(plan))
    return plan


def test_transfer_restores_both_bundles_and_retries_exactly(tmp_path):
    write_plan(tmp_path)
    transfer.restore(tmp_path)
    transfer.restore(tmp_path)
    assert (tmp_path / 'repo/commits.bundle').read_bytes() == b'firstsecond'
    assert (tmp_path / 'repo/master-commits.bundle').read_bytes() == b'firstsecond'


@pytest.mark.parametrize('corruption', ['missing', 'hash', 'length', 'path', 'total'])
def test_transfer_refuses_corruption_without_replacing_original(tmp_path, corruption):
    plan = write_plan(tmp_path)
    destination = tmp_path / 'repo/commits.bundle'
    destination.write_bytes(b'original')
    part = tmp_path / plan[0]['parts'][0]['path']
    if corruption == 'missing':
        part.unlink()
    elif corruption == 'hash':
        part.write_bytes(b'other')
    elif corruption == 'length':
        part.write_bytes(b'longer')
    elif corruption == 'path':
        plan[0]['parts'][0]['path'] = '../escape'
    else:
        plan[0]['bytes'] += 1
    (tmp_path / 'repo/commit-transfer.json').write_text(json.dumps(plan))
    with pytest.raises((OSError, ValueError)):
        transfer.restore(tmp_path)
    assert destination.read_bytes() == b'original'
