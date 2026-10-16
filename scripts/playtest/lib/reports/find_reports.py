#!/usr/bin/env python3
"""報告所在の表示だけを担当する。
Display report locations without changing their state.
"""
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from digest_reporter import tester_label


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
    return sorted((p for p in paths if p.is_dir()),
                  key=lambda path: (path.stat().st_mtime_ns, str(path)), reverse=True)[:limit]


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
    print('## 取り込み済み')
    if not reports.is_dir():
        print(f'報告ディレクトリが無い: {reports}')
    # 判定用schemaは使わず、壊れた箱も所在と欠損理由を表示する
    # Avoid decision schemas so broken boxes retain their location and error
    for box in newest(reports.glob('*/*'), limit):
        meta, manifest = read(box / 'ingest.json'), read(box / 'manifest.json')
        reporter = tester_label(meta) if meta.get('steamPersonaName') else meta.get('steamId', box.parent.name)
        queued = (box / 'AUTOFIX_QUEUED').exists()
        print(f'{box.name}\t{text(reporter)}\t{text(manifest.get("kind", "missing"))}\t'
              f'{text(manifest.get("description", ""))[:40]}\tAUTOFIX_QUEUED={queued}\t{box}')
        for value in (meta, manifest):
            if 'readError' in value:
                print('読取失敗: ' + value['readError'])
    root = logs / 'harness/bug-report'
    print('## 自動修正')
    for name in ('inbox', 'runs'):
        directory = root / name
        print(f'### {name}')
        if not directory.is_dir():
            print(f'ディレクトリが無い: {directory}')
        paths = directory.glob('*')
        # inboxは未処理を全件示し、runsだけ表示上限を適用する
        # Show every pending inbox box and limit only completed or active runs
        for box in newest(paths, limit if name == 'runs' else sys.maxsize):
            result = box / 'fix-result.json'
            status = read(result) if result.is_file() else '結果ファイルなし'
            print(f'{box.name}\t{text(status)}\t{box}')


if __name__ == '__main__':
    if sys.argv[1] == 'page':
        page(Path(sys.argv[2]), Path(sys.argv[3]))
    else:
        local(Path(sys.argv[2]), int(sys.argv[3]))
