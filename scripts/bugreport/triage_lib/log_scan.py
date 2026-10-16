import re
import sys
from . import cell, read_json

MAX_IMPORTANT_LOG_ROWS = 200


def summarize(box):
    path = box / 'logs/unity.log'
    lines = ['## ログ']
    if not path.is_file():
        return lines + ['unity.log が無い']
    raw = path.read_text(encoding='utf-8', errors='replace').splitlines()
    # 時刻・tickを比較から外し、スタックを含む連続レコードだけ集約する
    # Ignore time and tick when grouping consecutive records including their stack
    records = []
    for number, text in enumerate(raw, 1):
        match = re.match(r'^(\d{2}:\d{2}:\d{2}\.\d+)\t(-?\d+)\t([^\t]+)\t(.*)$', text)
        if match:
            records.append([number, number, text, match[3] + '\t' + match[4]])
        elif records and '\t' in records[-1][2]:
            records[-1][1] = number
            records[-1][2] += '\n' + text
            records[-1][3] += '\n' + text
        else:
            records.append([number, number, text, text])
    groups = []
    for first, last, text, key in records:
        if groups and groups[-1]['key'] == key:
            groups[-1].update(last=last, end=text, count=groups[-1]['count'] + 1)
        else:
            groups.append(dict(first=first, last=last, text=text, end=text, key=key, count=1))
    important = [g for g in groups if re.search(r'\b(error|exception|assert|warning)\b', g['key'], re.I)]
    # エラーを優先し、同じ優先度では末尾を残して元位置順に表示する
    # Prefer errors and recent entries, then display the selection in source order
    selected = sorted(important, key=lambda g: (bool(re.search(r'\b(error|exception|assert)\b', g['key'], re.I)), g['last']))[-MAX_IMPORTANT_LOG_ROWS:]
    for group in sorted(selected, key=lambda g: g['first']):
        lines.append(f"{group['text']} ×{group['count']} [unity.log:{group['first']}-{group['last']}]")
        if group['count'] > 1:
            lines.append(f"反復末尾: {group['end'].splitlines()[0]}")
    if len(selected) < len(important):
        lines.append(f'重要ログ省略: {len(important) - len(selected)} 集約件（元ログ: logs/unity.log）')
    lines.append('### TrainSynchronization（全件）')
    lines.extend(text for text in raw if '[TrainSynchronization]' in text)
    return lines


def stalls(box):
    lines = ['## 停滞診断']
    files = sorted((box / 'logs/train-sync').glob('*.json'))
    for path in files:
        # 個別の外部JSON破損を記録し、次の診断を読む
        # Record individual external JSON corruption and continue with the next diagnostic
        try:
            value = read_json(path)
            reasons = ' / '.join(f'{key}={cell(value[key]) if key in value else "欠損"}'
                                 for key in ('WaitingReason', 'WaitingReasonAtCapture', 'CaptureReason'))
            lines.append(f'- {path.name}: {reasons}')
        except (OSError, ValueError) as error:
            reason = f'停滞診断調査失敗: {path}: {error}'
            print(reason, file=sys.stderr)
            lines.append(reason)

    return lines if files else lines + ['停滞診断ファイルが無い']
