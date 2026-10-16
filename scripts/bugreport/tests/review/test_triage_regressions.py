import json
from pathlib import Path
import shutil
import subprocess
import sys
from types import SimpleNamespace

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from triage_lib import contact_sheet, log_scan


def report(box, out):
    result = subprocess.run([sys.executable, str(ROOT / 'triage.py'), str(box), '--out', str(out)],
                            capture_output=True, text=True, check=True)
    return result, (out / 'triage.md').read_text()


@pytest.mark.parametrize('manifest,snapshot,field', [
    ({'missing': [None]}, {'trainUnits': [], 'railSegments': []}, 'missing[0]'),
    ({}, {'trainUnits': [], 'railSegments': []}, 'missing'),
    ({'missing': []}, {}, 'trainUnits'),
    ({'missing': []}, {'trainUnits': [None], 'railSegments': []}, 'trainUnits[0]'),
    ({'missing': []}, {'trainUnits': [], 'railSegments': [None]}, 'railSegments[0]'),
    ({'missing': []}, {'trainUnits': [], 'railSegments': []}, None),
])
def test_external_shapes_keep_independent_sections(tmp_path, manifest, snapshot, field):
    box = tmp_path / 'box'
    box.mkdir()
    (box / 'manifest.json').write_text(json.dumps(manifest))
    (box / 'snapshots').mkdir()
    (box / 'snapshots/tick_1.json').write_text(json.dumps(snapshot))
    result, text = report(box, tmp_path / 'out')
    assert '## 動画' in text and '## 停滞診断' in text
    if field:
        assert field in text and field in result.stderr
        assert str(box) in result.stderr
    else:
        assert '列車時系列: 0 行' in text and 'レール: 0 件' in text


@pytest.mark.parametrize('field,value', [
    ('CurrentSpeedBits', 2 ** 63), ('AccumulatedDistanceBits', -(2 ** 63) - 1),
    ('Diagram', []), ('Diagram', {'Entries': [None], 'CurrentIndex': 0}),
    ('railPositionSaveData', {'RailSnapshot': [None]}),
])
def test_malformed_train_has_field_and_file(tmp_path, field, value):
    box = tmp_path / 'box'
    (box / 'snapshots').mkdir(parents=True)
    (box / 'manifest.json').write_text('{"missing":[]}')
    train = {'TrainUnitInstanceId': 'train', 'IsAutoRun': True, 'CurrentSpeedBits': 0,
             'AccumulatedDistanceBits': 0, 'Diagram': None,
             'railPositionSaveData': {'RailSnapshot': []}}
    train[field] = value
    path = box / 'snapshots/tick_1.json'
    path.write_text(json.dumps({'trainUnits': [train], 'railSegments': []}))
    result, text = report(box, tmp_path / 'out')
    assert str(path) in result.stderr and field in result.stderr
    assert '調査失敗' in text and 'レール: 0 件' in text


@pytest.mark.parametrize('mode', ['no_video', 'no_ffmpeg', 'failed', 'no_frames', 'success'])
def test_contact_sheet_never_reuses_previous_box(tmp_path, monkeypatch, mode):
    box, out = tmp_path / 'box', tmp_path / 'out'
    box.mkdir()
    out.mkdir()
    target = out / 'contact-sheet.png'
    target.write_bytes(b'OLD_BOX')
    if mode != 'no_video':
        (box / 'video.mp4').write_bytes(b'video')
    monkeypatch.setattr(contact_sheet.shutil, 'which', lambda name: None if mode == 'no_ffmpeg' else '/ffmpeg')
    calls = []

    def convert(args, **kwargs):
        calls.append(args)
        assert args[0] == '/ffmpeg' and str(box / 'video.mp4') in args
        assert Path(args[-1]) != target
        if mode == 'success':
            Path(args[-1]).write_bytes(b'NEW_BOX')
        return SimpleNamespace(returncode=1 if mode == 'failed' else 0, stderr='conversion failure')

    monkeypatch.setattr(contact_sheet.subprocess, 'run', convert)
    text = '\n'.join(contact_sheet.summarize(box, out))
    if mode == 'success':
        assert target.read_bytes() == b'NEW_BOX' and 'contact-sheet.png を生成' in text
    else:
        assert not target.exists() and 'contact-sheet.png を生成' not in text
    assert len(calls) == (0 if mode in ('no_video', 'no_ffmpeg') else 1)
    assert list(out.iterdir()) == ([target] if mode == 'success' else [])


def test_short_real_video_cannot_publish_stale_sheet(tmp_path):
    ffmpeg = shutil.which('ffmpeg')
    if ffmpeg is None:
        pytest.skip('ffmpeg が無いため短い実動画の検証は未確認')
    box, out = tmp_path / 'box', tmp_path / 'out'
    box.mkdir()
    out.mkdir()
    subprocess.run([ffmpeg, '-v', 'error', '-f', 'lavfi', '-i', 'color=size=32x32:rate=1',
                    '-t', '0.01', str(box / 'video.mp4')], check=True, capture_output=True)
    target = out / 'contact-sheet.png'
    target.write_bytes(b'OLD_BOX')
    text = '\n'.join(contact_sheet.summarize(box, out))
    assert not target.exists() or target.read_bytes() != b'OLD_BOX'
    assert ('contact-sheet.png を生成' in text) == target.exists()


def test_stall_missing_fields_are_named(tmp_path):
    (tmp_path / 'logs/train-sync').mkdir(parents=True)
    (tmp_path / 'logs/train-sync/stall.json').write_text('{}')
    text = '\n'.join(log_scan.stalls(tmp_path))
    for field in ('WaitingReason', 'WaitingReasonAtCapture', 'CaptureReason'):
        assert field + '=欠損' in text


def test_cli_video_conversion_is_connected(tmp_path, monkeypatch):
    box, out, binary = tmp_path / 'box', tmp_path / 'out', tmp_path / 'bin'
    box.mkdir()
    binary.mkdir()
    (box / 'manifest.json').write_text('{"missing":[]}')
    (box / 'video.mp4').write_bytes(b'video')
    ffmpeg = binary / 'ffmpeg'
    marker = tmp_path / 'called'
    ffmpeg.write_text(f'#!{sys.executable}\nimport sys\nfrom pathlib import Path\nPath({str(marker)!r}).write_text(" ".join(sys.argv))\nPath(sys.argv[-1]).write_bytes(b"NEW_BOX")\n')
    ffmpeg.chmod(0o755)
    monkeypatch.setenv('PATH', str(binary))
    result, text = report(box, out)
    assert str(box / 'video.mp4') in marker.read_text()
    assert (out / 'contact-sheet.png').read_bytes() == b'NEW_BOX'
    assert 'contact-sheet.png を生成' in text and result.stdout == text


@pytest.mark.skipif(sys.platform != 'darwin', reason='macOS immutable file regression')
def test_cli_locked_previous_sheet_keeps_report(tmp_path):
    import os
    import stat
    box, out = tmp_path / 'box', tmp_path / 'out'
    box.mkdir()
    out.mkdir()
    (box / 'manifest.json').write_text('{"missing":[]}')
    target = out / 'contact-sheet.png'
    target.write_bytes(b'OLD_BOX')
    os.chflags(target, stat.UF_IMMUTABLE)
    try:
        result, text = report(box, out)
        assert '動画調査失敗' in text and '今回の成果物ではありません' in text
        assert '動画調査失敗' in result.stderr and '## manifest' in text
        assert 'contact-sheet.png を生成' not in text
    finally:
        os.chflags(target, 0)


def test_real_log_repeats_keep_unique_late_error_and_locations(tmp_path):
    (tmp_path / 'logs').mkdir()
    rows = [f'12:00:00.{i:03d}\t{i}\tWarning\tRepeated\nStack()' for i in range(201)]
    rows += ['12:00:01.000\t202\tError\tUnique cause']
    (tmp_path / 'logs/unity.log').write_text('\n'.join(rows))
    text = '\n'.join(log_scan.summarize(tmp_path))
    assert '×201 [unity.log:1-402]' in text
    assert 'Unique cause ×1 [unity.log:403-403]' in text
    assert '反復末尾: 12:00:00.200' in text


def test_log_limit_prefers_errors_and_recent_warnings(tmp_path):
    (tmp_path / 'logs').mkdir()
    rows = ['Error original cause'] + [f'Warning {i}' for i in range(250)]
    (tmp_path / 'logs/unity.log').write_text('\n'.join(rows))
    text = '\n'.join(log_scan.summarize(tmp_path))
    assert text.count('×1 [unity.log:') == 200
    assert 'Error original cause' in text and 'Warning 249' in text
    assert '重要ログ省略: 51 集約件' in text
