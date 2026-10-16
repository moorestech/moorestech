"""取り込み済みの箱を変更せず要約する。
Summarize ingested boxes without modifying them.
"""
import json


def read_json(path):
    # 外部ファイルの欠損・破損を調査結果に残す
    # Surface missing or malformed external files in the investigation
    try:
        value = json.loads(path.read_text(encoding='utf-8'))
        if not isinstance(value, dict):
            raise ValueError('JSON object が必要')
        return value
    except (OSError, ValueError) as error:
        raise ValueError(f'{path}: {error}') from error


def cell(value):
    return json.dumps(value, ensure_ascii=False).replace('|', '\\|').replace('\n', ' ')
