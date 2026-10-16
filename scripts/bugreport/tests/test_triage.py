import json
from pathlib import Path
import struct
import subprocess
import sys

SCRIPT = Path(__file__).resolve().parents[1] / 'triage.py'


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding='utf-8')


def test_triage(tmp_path):
    box, out = tmp_path / 'box', tmp_path / 'out'
    write(box / 'manifest.json', {'kind': 'bug', 'missing': [{'item': 'steamId', 'reason': 'Editor'}]})
    (box / 'logs').mkdir()
    (box / 'logs/unity.log').write_text('Error test\nError test\nWarning test\n'
                                        '[TrainSynchronization] Permanently waiting\n')
    bits = struct.unpack('<q', struct.pack('<d', 1.25))[0]
    train = {'TrainUnitInstanceId': 'train1', 'IsAutoRun': True,
             'CurrentSpeedBits': bits, 'AccumulatedDistanceBits': bits,
             'Diagram': {'Entries': [{}, {}], 'CurrentIndex': 1},
             'railPositionSaveData': {'RailSnapshot': [{'node': 1}]}}
    edge = {'A': 'a', 'B': 'b', 'Length': 123, 'RailTypeGuid': '12345678-rest', 'IsDrawable': True}
    for tick in (200, 100):
        train['IsAutoRun'] = tick == 100
        write(box / f'snapshots/tick_{tick}.json', {'trainUnits': [train], 'railSegments': [edge, edge]})
    write(box / 'logs/train-sync/stall-test.json', {'reason': 'waiting'})
    before = {p.relative_to(box): p.read_bytes() for p in box.rglob('*') if p.is_file()}
    result = subprocess.run([sys.executable, str(SCRIPT), str(box), '--out', str(out)],
                            capture_output=True, text=True, check=True)
    report = (out / 'triage.md').read_text()
    assert result.stdout == report
    headings = ['## manifest', '## ログ', '## 列車', '## レール', '## 停滞診断', '## 動画']
    assert [report.index(h) for h in headings] == sorted(report.index(h) for h in headings)
    assert 'Error test ×2' in report and 'Warning test ×1' in report
    assert '[TrainSynchronization] Permanently waiting' in report
    rows = [line for line in report.splitlines() if line.startswith('| "train1"')]
    assert len(rows) == 2 and '| 100 |' in rows[0] and '| 200 |' in rows[1]
    assert '1.25' in rows[0] and '登録2 / index=1' in rows[0]
    assert 'レール: 2 件' in report and 'video.mp4 が無い' in report
    assert 'stall-test.json' in report and 'Editor' in report
    assert before == {p.relative_to(box): p.read_bytes() for p in box.rglob('*') if p.is_file()}


def test_output_inside_box_is_rejected(tmp_path):
    result = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path), '--out', str(tmp_path / 'out')],
                            capture_output=True, text=True)
    assert result.returncode != 0
    assert not (tmp_path / 'out').exists()
