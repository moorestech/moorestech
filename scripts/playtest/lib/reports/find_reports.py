#!/usr/bin/env python3
"""報告所在の表示だけを担当する。
Display report locations without changing their state.
"""
import json
import stat
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'raw_store'))
from digest_reporter import tester_label
import raw_layout
import raw_reader


def read(path):
    # 外部JSONの破損を一覧とstderrの両方へ残す
    # Retain external JSON failures in the listing and stderr
    try:
        value = json.loads(path.read_text(encoding='utf-8'))
        if not isinstance(value, dict):
            raise ValueError('JSON object が必要')
        return value
    except (OSError, ValueError) as error:
        reason = f'{path}: {error}'
        print(reason, file=sys.stderr)
        return {'readError': reason}


def text(value):
    return ' '.join(str(value).split())


def newest(paths, limit):
    entries = []
    for path in paths:
        # 外部ディスク競合はその箱の所在・理由を残して隔離する
        # Isolate external disk races while retaining the affected location and reason
        try:
            info = path.stat()
            if stat.S_ISDIR(info.st_mode):
                entries.append((info.st_mtime_ns, str(path), path))
        except OSError as error:
            reason = f'読取失敗: {path}: {error}'
            print(reason, file=sys.stderr)
            print(reason)
    return [entry[2] for entry in sorted(entries, reverse=True)[:limit]]


def page(source, cursor_path):
    value = read(source)
    if 'readError' in value:
        raise ValueError(value['readError'])
    for item in value['items']:
        print(' '.join(text(item[key]) for key in ('kind', 'steamId', 'id', 'readyAt')))
    cursor = value.get('cursor')
    if cursor is not None and not isinstance(cursor, str):
        raise ValueError('cursor は文字列が必要')
    cursor_path.write_text(cursor or '', encoding='utf-8')


def local(logs, limit):
    reports = logs / 'harness/playtest/reports'
    raw_root = raw_layout.default_root()
    print('## 取り込み済み')
    if not reports.is_dir():
        print(f'報告ディレクトリが無い: {reports}')
    # 判定用schemaは使わず、壊れた箱も所在と欠損理由を表示する
    # Avoid decision schemas so broken boxes retain their location and error
    for box in newest((p for p in reports.glob('*/*') if not p.name.endswith('.partial')), limit):
        meta, manifest = read(box / 'ingest.json'), read(box / 'manifest.json')
        if 'steamPersonaName' in meta and not isinstance(meta['steamPersonaName'], str):
            reason = f'{box / "ingest.json"}: steamPersonaName: 文字列が必要'
            print(reason, file=sys.stderr)
            print(f'{box.name}\t読取失敗: {reason}\t{box}')
            reporter = '表示名不正'
        else:
            reporter = tester_label(meta) if meta.get('steamPersonaName') else meta.get('steamId', box.parent.name)
        queued = (box / 'AUTOFIX_QUEUED').exists()
        raw = raw_reader.raw_state(box, raw_root)
        print(f'{box.name}\t{text(reporter)}\t{text(manifest.get("kind", "missing"))}\t'
              f'{text(manifest.get("description", ""))[:40]}\tAUTOFIX_QUEUED={queued}\traw={raw["state"]}\t{box}')
        # 生データの期限切れ・欠落は一覧の利用者へ理由ごと見せる（enqueue-autofix はこの箱を投入しない）
        # Show expired or missing raw data with its reason (enqueue-autofix refuses such a box)
        if raw['state'] not in raw_reader.READABLE_STATES:
            print('生データ: ' + raw['reason'])
        for value in (meta, manifest):
            if 'readError' in value:
                print('読取失敗: ' + value['readError'])
    print('## 運搬中／未完了')
    for box in newest(reports.glob('*/*.partial'), sys.maxsize):
        print(f'{box.name}\t運搬中／未完了\t{box}')
    root = logs / 'harness/bug-report'
    print('## 自動修正')
    for name in ('inbox', 'runs'):
        directory = root / name
        print(f'### {name}')
        if not directory.is_dir():
            print(f'ディレクトリが無い: {directory}')
        held = {'.remote-exec-held': '保留', '.duplicate': '重複'}
        paths = (path for path in directory.glob('*') if name != 'inbox' or path.name not in held)
        # inboxは未処理を全件示し、runsだけ表示上限を適用する
        # Show every pending inbox box and limit only completed or active runs
        for box in newest(paths, limit if name == 'runs' else sys.maxsize):
            if name == 'inbox' and box.name.endswith('.partial'):
                print(f'{box.name}\t運搬中／未完了\t{box}')
                continue
            result = box / 'fix-result.json'
            status = read(result) if result.is_file() else '結果ファイルなし'
            print(f'{box.name}\t{text(status)}\t{box}')
        if name == 'inbox':
            for parent, state in held.items():
                for box in newest((directory / parent).glob('*'), sys.maxsize):
                    print(f'{box.name}\t{state}\t{box}')


if __name__ == '__main__':
    if sys.argv[1] == 'page':
        page(Path(sys.argv[2]), Path(sys.argv[3]))
    else:
        local(Path(sys.argv[2]), int(sys.argv[3]))
