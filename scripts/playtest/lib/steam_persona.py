#!/usr/bin/env python3
"""Steam 応答から公開表示名を取り出し、失敗理由も同じ形で書く。
Extract a public Steam persona from a response and write failures in the same shape.
"""
import json
import re
import sys

# 制御文字・改行を空白1つへ畳む。表示名の改行は貼り付けコマンドの退避（PASTEABLE_COMMAND_RE）を偽装できる（C12）
# Folds control characters and newlines to a single space; a newline in the display name can otherwise forge the pasteable-command stash (PASTEABLE_COMMAND_RE, C12)
_CONTROL_CHARS_RE = re.compile(r"[\x00-\x1f\x7f]+")


def _normalize(value: str) -> str:
    return _CONTROL_CHARS_RE.sub(" ", value).strip()


def unresolved(reason: str) -> dict:
    return {"steamPersonaName": "", "steamProfileUrl": "", "steamPersonaMissing": reason}


def extract(response_path: str, steam_id: str) -> dict:
    # 外部応答の解析失敗は、取り込みを止めずに理由へ変換する
    # Convert malformed external responses to a reason without stopping ingestion
    try:
        with open(response_path, encoding="utf-8") as source:
            players = json.load(source)["response"]["players"]
    except (OSError, ValueError, KeyError, TypeError) as error:
        return unresolved(f"応答の解析に失敗: {type(error).__name__}")

    # 対象 SteamID と文字列の表示情報が揃った応答だけを採用する
    # Accept only a matching SteamID with string-valued display fields
    for player in players if isinstance(players, list) else []:
        if isinstance(player, dict) and player.get("steamid") == steam_id:
            name = player.get("personaname")
            url = player.get("profileurl")
            if isinstance(name, str) and isinstance(url, str):
                return {"steamPersonaName": _normalize(name), "steamProfileUrl": _normalize(url), "steamPersonaMissing": ""}
    return unresolved("応答に該当 SteamID が無い")


if __name__ == "__main__":
    mode, output = sys.argv[1], sys.argv[-1]
    result = extract(sys.argv[2], sys.argv[3]) if mode == "extract" else unresolved(sys.argv[2])
    with open(output, "w", encoding="utf-8") as target:
        json.dump(result, target, ensure_ascii=False, separators=(",", ":"))
