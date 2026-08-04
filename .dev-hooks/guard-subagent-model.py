#!/usr/bin/env python3
# サブエージェントのfable起動を仕組みで塞ぐ関所（PreToolUse: Agent/Task）
# Blocks subagent spawns that inherit or specify fable; model must be explicit (opus/sonnet/haiku).
# 例外: user-simulator判事のみfable許可（decisions.md #2 のユーザー裁定による明示的例外）
# Exception: the user-simulator judge (decisions.md #2) is the only sanctioned fable subagent.
import json
import sys

try:
    data = json.load(sys.stdin)
except Exception:
    sys.exit(0)

tool_input = data.get("tool_input", {}) or {}
model = (tool_input.get("model") or "").strip().lower()
prompt = tool_input.get("prompt") or ""

# 判事はプロンプトでagents/judge.mdを読ませる規約なのでそれを例外の判定に使う
# The judge protocol always references agents/judge.md in its prompt; use that as the exception key
if "agents/judge.md" in prompt:
    sys.exit(0)

if model in ("", "fable"):
    print(
        "BLOCKED: サブエージェントはmodelの明示が必須で、fableは禁止（省略はfable継承になるため不可）。"
        "opus / sonnet / haiku を指定して再実行すること。"
        "例外はuser-simulator判事（プロンプトにagents/judge.mdを含む）のみ。"
        "根拠: メモリ「サブエージェントのモデルコスト方針」・user-simulator decisions.md #2",
        file=sys.stderr,
    )
    sys.exit(2)

sys.exit(0)
