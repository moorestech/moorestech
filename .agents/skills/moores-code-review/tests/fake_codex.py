#!/usr/bin/env python3
# .claude/skills/moores-code-review/tests/fake_codex.py
# codex_workflow_runner のテスト用の偽 codex。`codex exec ... --output-schema S -o F -` の形だけを受け、
# スキーマの required を workflow_sim.py と同じ既定値で埋めた JSON を F に書く（null 許容のキーは null）。
# 起動ごとに FAKE_CODEX_LOG へ1行（label・effort・model・schema 有無）を追記する。
# FAKE_CODEX_OVERRIDES（JSON: 安全化した label 接頭辞→上書き）で既定値では通らない分岐を踏ませる。
# Fake codex for codex_workflow_runner tests: fills schema-required keys with workflow_sim.py defaults and logs each launch.
import json
import os
import re
import sys
from pathlib import Path


def fill(schema):
    if not schema:
        return "ok"
    types = schema.get("type")
    if isinstance(types, list):
        # 元で任意だったキー（null 許容）は workflow_sim と同じく「無い」扱いの null にする
        # Originally optional (nullable) keys become null, matching their absence in workflow_sim
        return None
    if schema.get("enum"):
        return schema["enum"][0]
    if types == "object":
        return {k: fill(schema["properties"][k]) for k in schema.get("required", [])}
    if types == "array":
        return []
    if types in ("integer", "number"):
        return 0
    if types == "boolean":
        return True
    return "ok"


def main(argv):
    opts = {"-o": None, "--output-schema": None, "-m": None, "-c": None}
    i = 0
    while i < len(argv):
        if argv[i] in opts:
            opts[argv[i]] = argv[i + 1]
            i += 2
        else:
            i += 1
    sys.stdin.read()
    final = Path(opts["-o"])
    label = re.sub(r"^\d+-", "", final.name.split(".final")[0])
    with open(os.environ["FAKE_CODEX_LOG"], "a", encoding="utf-8") as log:
        log.write(json.dumps({"label": label, "effort": opts["-c"], "model": opts["-m"], "schema": bool(opts["--output-schema"])}) + "\n")
    if not opts["--output-schema"]:
        final.write_text("ok\n", encoding="utf-8")
        return 0
    answer = fill(json.loads(Path(opts["--output-schema"]).read_text(encoding="utf-8")))
    for prefix, patch in json.loads(os.environ.get("FAKE_CODEX_OVERRIDES", "{}")).items():
        if label.startswith(prefix):
            answer.update(patch)
    final.write_text(json.dumps(answer), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[2:]))
