from pathlib import Path
import runpy

MODULE = Path(__file__).resolve().parents[2] / 'lib/reports/find_reports.py'


def test_partial_is_outside_completed_limit_and_inbox_is_labelled(tmp_path, capsys):
    module = runpy.run_path(str(MODULE))
    reports = tmp_path / 'harness/playtest/reports/tester'
    complete, partial = reports / 'complete', reports / 'moving.partial'
    for box in (complete, partial):
        box.mkdir(parents=True)
        (box / 'manifest.json').write_text('{"kind":"bug"}')
        (box / 'ingest.json').write_text('{"steamId":"123"}')
    inbox = tmp_path / 'harness/bug-report/inbox/moving.partial'
    inbox.mkdir(parents=True)
    module['local'](tmp_path, 1)
    text = capsys.readouterr().out
    completed, rest = text.split('## 運搬中／未完了')
    assert 'complete\t' in completed and 'moving.partial' not in completed
    assert 'moving.partial\t運搬中／未完了' in rest
    assert 'moving.partial\t結果ファイルなし' not in rest


def test_stat_race_keeps_other_locations_and_records_failure(tmp_path, monkeypatch, capsys):
    module = runpy.run_path(str(MODULE))
    vanished, intact = tmp_path / 'vanished', tmp_path / 'intact'
    vanished.mkdir()
    intact.mkdir()
    original = Path.stat

    def racing_stat(path, *args, **kwargs):
        if path == vanished:
            raise FileNotFoundError('moved during enumeration')
        return original(path, *args, **kwargs)

    monkeypatch.setattr(Path, 'stat', racing_stat)
    assert module['newest']([vanished, intact], 1) == [intact]
    captured = capsys.readouterr()
    assert 'vanished' in captured.out and 'vanished' in captured.err
