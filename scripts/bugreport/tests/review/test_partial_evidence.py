import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from triage_lib import log_scan, rail_edges, train_timeline


def test_corrupt_older_snapshot_keeps_latest_rails_and_intact_trains(tmp_path, capsys):
    snapshots = tmp_path / 'snapshots'
    snapshots.mkdir()
    (snapshots / 'tick_100.json').write_text('{broken')
    train = {'TrainUnitInstanceId': 'intact', 'IsAutoRun': True, 'CurrentSpeedBits': 0,
             'AccumulatedDistanceBits': 0, 'Diagram': None, 'railPositionSaveData': {'RailSnapshot': []}}
    latest = snapshots / 'tick_200.json'
    latest.write_text(json.dumps({'trainUnits': [train], 'railSegments': []}))
    assert 'tick=200 / レール: 0 件' in '\n'.join(rail_edges.summarize(tmp_path))
    text = '\n'.join(train_timeline.summarize(tmp_path))
    assert 'intact' in text and '列車時系列: 1 行' in text and 'tick_100.json' in text
    assert 'tick_100.json' in capsys.readouterr().err
    latest.write_text('{broken-latest')
    import pytest
    with pytest.raises(ValueError, match='tick_200.json'):
        rail_edges.summarize(tmp_path)


def test_corrupt_stall_keeps_later_diagnostics(tmp_path, capsys):
    directory = tmp_path / 'logs/train-sync'
    directory.mkdir(parents=True)
    (directory / 'a.json').write_text('{broken')
    (directory / 'b.json').write_text('{"WaitingReason":"intact"}')
    text = '\n'.join(log_scan.stalls(tmp_path))
    assert 'a.json' in text and 'intact' in text
    assert 'a.json' in capsys.readouterr().err
