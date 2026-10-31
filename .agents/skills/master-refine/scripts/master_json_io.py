#!/usr/bin/env python3
"""マスタ JSON の読み書きと、書き戻し前の検証（操作 A）。
- 重複キーは読み込みで止める（後勝ちで黙って値が消えるため）
- 書き戻す原文は、indent 2・非 ASCII そのまま・元の末尾改行の有無で無変更往復できなければ止める
- 書き戻す前に前後を Decimal で読み、sortPriority と配列順以外が変わっていないことを照合する

Master JSON IO and pre-write checks for operation A: stop on duplicate keys, stop unless the original text round-trips
unchanged (indent 2, raw non-ASCII, original trailing newline), and compare before/after with Decimal ignoring only
sortPriority and the array order.
"""
import json
from decimal import Decimal
from pathlib import Path


class MasterJsonError(Exception):
    pass


class MasterJsonFile:
    """読み込んだ原文と解析結果。 / Original text and parsed document."""

    def __init__(self, path, text, doc):
        self.path = path
        self.text = text
        self.doc = doc
        self.trailing_newline = text.endswith("\n")


def _reject_duplicate_keys(path):
    def hook(pairs):
        keys = [key for key, _ in pairs]
        duplicates = sorted({key for key in keys if keys.count(key) > 1})
        if duplicates:
            raise MasterJsonError(f"{path}: duplicate key {duplicates}; nothing was written")
        return dict(pairs)
    return hook


def _parse(path, text, parse_float):
    return json.loads(text, object_pairs_hook=_reject_duplicate_keys(path), parse_float=parse_float)


def render(doc, trailing_newline):
    return json.dumps(doc, indent=2, ensure_ascii=False) + ("\n" if trailing_newline else "")


def read_master_json(path, require_round_trip):
    path = Path(path)
    text = path.read_text(encoding="utf-8")
    loaded = MasterJsonFile(path, text, _parse(path, text, float))
    # 書き戻すファイルは、原文がこの書式で再現できるときだけ扱う（書式差分で無関係な行が変わるのを防ぐ）
    # Only handle files to be written when this format reproduces the original (so unrelated lines never change)
    if require_round_trip and render(loaded.doc, loaded.trailing_newline) != text:
        raise MasterJsonError(f"{path}: original text does not round-trip with indent 2 / ensure_ascii=False; nothing was written")
    return loaded


def _rows_without_sort(rows, guid_key):
    # 並びを無視して比べるため、sortPriority を除いた行を GUID 順の正規化文字列にする（Decimal は文字列で表す）
    # Order-insensitive form: rows without sortPriority as canonical strings sorted by GUID (Decimal written as text)
    return sorted((row[guid_key], json.dumps({k: v for k, v in row.items() if k != "sortPriority"}, default=str, sort_keys=True))
                  for row in rows)


def verify_only_sort_changed(loaded, new_text, guid_key):
    # 前後を Decimal で読み、data の各行の sortPriority と data の並び以外がすべて同じことを確かめる
    # Read both sides with Decimal and require everything to match except each data row's sortPriority and the data order
    before = _parse(loaded.path, loaded.text, Decimal)
    after = _parse(loaded.path, new_text, Decimal)
    if {k: v for k, v in before.items() if k != "data"} != {k: v for k, v in after.items() if k != "data"}:
        raise MasterJsonError(f"{loaded.path}: top-level values other than data changed; nothing was written")
    if _rows_without_sort(before["data"], guid_key) != _rows_without_sort(after["data"], guid_key):
        raise MasterJsonError(f"{loaded.path}: data rows changed beyond sortPriority and order; nothing was written")
    if new_text.endswith("\n") != loaded.trailing_newline:
        raise MasterJsonError(f"{loaded.path}: trailing newline would change; nothing was written")


def write_if_changed(loaded, new_text):
    if new_text != loaded.text:
        loaded.path.write_text(new_text, encoding="utf-8")
        return True
    return False
