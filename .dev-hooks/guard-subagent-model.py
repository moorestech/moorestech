#!/usr/bin/env python3
# サブエージェントのfable起動を仕組みで塞ぐ関所（PreToolUse: Agent/Task）
# Blocks subagent spawns that inherit or specify fable; model must be explicit (opus/sonnet/haiku).
import json
import sys

try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)

tool_input = data.get("tool_input", {}) or {}
model = (tool_input.get("model") or "").strip().lower()

if model in ("", "fable"):
    print(
        "BLOCKED: サブエージェントはmodelの明示が必須で、fableは禁止（省略はfable継承になるため不可）。"
        "opus / sonnet / haiku を指定して再実行すること。"
        "根拠: メモリ「サブエージェントのモデルコスト方針」",
        file=sys.stderr,
    )
    sys.exit(2)

sys.exit(0)
