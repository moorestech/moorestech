import re
from itertools import groupby
from . import cell, read_json


def summarize(box):
    path = box / 'logs/unity.log'
    lines = ['## ログ']
    if not path.is_file():
        return lines + ['unity.log が無い']
    raw = path.read_text(encoding='utf-8', errors='replace').splitlines()
    # 元ログの連続同文だけを集約してから重要行を絞る
    # Collapse consecutive identical original lines before selecting important ones
    selected = [(text, len(list(group))) for text, group in groupby(raw)
                if re.search(r'\b(error|exception|warning)\b', text, re.I)]
    lines.extend(f'{text} ×{count}' for text, count in selected[:200])
    if len(selected) > 200:
        lines.append(f'重要ログ省略: {len(selected) - 200} 行')
    lines.append('### TrainSynchronization（全件）')
    lines.extend(text for text in raw if '[TrainSynchronization]' in text)
    return lines


def stalls(box):
    lines = ['## 停滞診断']
    files = sorted((box / 'logs/train-sync').glob('*.json'))
    for path in files:
        value = read_json(path)
        lines.append(f'- {path.name}: {cell(value.get("reason", "reasonが無い"))}')
    return lines if files else lines + ['停滞診断ファイルが無い']
