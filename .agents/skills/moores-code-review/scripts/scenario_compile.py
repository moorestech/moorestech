#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""scenario_compile.py — 録画シナリオ全件のコンパイル診断を記録し、反映前後の差集合を取る（applied_diff_checks.py の部品）。

録画シナリオ（unity-playmode-recorded-playtest 配下の .cs）はレビュー diff からも uloop compile からも外れる。
反映が壊したかは「どの名前を変えたか」からは決まらない（複数行シグネチャ・名前を含まない型不一致・別型への移動）。
そこで全件を本体を呼ばないローカル関数へ包んで execute-dynamic-code でコンパイルし、反映前と反映後の診断を
(シナリオ, エラーコード, メッセージ, 指す行のソース文字列) で正規化して比べ、反映後にだけ増えた診断をこの反映の破壊とする。
行番号・列は追従編集で揺れるので鍵に入れず、代わりに指す行の文字列で呼び出し箇所を区別する（別の呼び出しの
同文面 CS1503 と相殺しない。2026-10-07 Codex 再監査）。行が取れず対応を確認できない一致は既存扱いせず未確認にする。

Records compile diagnostics of every playtest scenario and diffs before/after an applied change. Name-based
targeting misses multi-line signatures, name-less type errors and moves; comparing normalized diagnostics does not.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import time
from collections import Counter
from pathlib import Path

SCENARIO_MARK = "unity-playmode-recorded-playtest"
USING_DIRECTIVE_RE = re.compile(r"^using\s+(?:static\s+)?[\w.]+(?:\s*=\s*[\w.<>, ]+)?\s*;\s*$")
EDC_TIMEOUT_SEC = 180


def list_scenarios(repo_root: Path) -> list[Path]:
    return sorted((repo_root / ".agents" / "skills").glob(f"{SCENARIO_MARK}/**/*.cs"))


def compile_snippet(scenario: Path) -> str:
    # 先頭の using 指令とコメントは残し、本体を呼ばないローカル関数へ包む（PlaytestRunner.Run を実行させない）
    # Keep leading using directives and comments; wrap the body in a never-called local function so nothing runs
    lines = scenario.read_text(encoding="utf-8").splitlines()
    split = 0
    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped and not stripped.startswith("//") and not USING_DIRECTIVE_RE.match(stripped):
            split = i
            break
    header, body = lines[:split], lines[split:]
    return "\n".join([*header, "object ScenarioCompileOnly()", "{", *body, "}",
                      f'return "compile-only: {scenario.name}";', ""])


def _parse_json(text: str) -> dict | None:
    start, end = text.find("{"), text.rfind("}")
    if start < 0 or end <= start:
        return None
    try:
        return json.loads(text[start:end + 1])
    except json.JSONDecodeError:
        return None


def run_edc(project: Path, snippet: Path) -> dict:
    # 戻り: {"status": "compiled", "diagnostics": [...]} / {"status": "unknown", "reason": ...}
    # Returns compiled diagnostics, or unknown with a reason (no Editor, runtime failure, rejection)
    cmd = [os.environ.get("MOORES_REVIEW_ULOOP", "uloop"), "execute-dynamic-code",
           "--project-path", str(project), "--code-file", str(snippet)]
    reason = ""
    for attempt in range(2):
        try:
            run = subprocess.run(cmd, capture_output=True, text=True, timeout=EDC_TIMEOUT_SEC)
        except (OSError, subprocess.TimeoutExpired) as e:
            return {"status": "unknown", "reason": f"uloop を実行できない: {e}", "no_response": True}
        data = _parse_json(run.stdout) or _parse_json(run.stderr)
        # 接続失敗は stderr に {"Error": {"ErrorCode": "UNITY_NOT_REACHABLE", "Phase": "connection"}} で返る（2026-10-07 実測）
        # Connection failures come back on stderr as an Error object with Phase "connection" (observed 2026-10-07)
        error = data.get("Error") if data else None
        if data is None or (isinstance(error, dict) and error.get("Phase") == "connection"):
            detail = error.get("Message") if isinstance(error, dict) else run.stderr.strip()[:200]
            return {"status": "unknown", "reason": f"Unity に届かない（Editor 不在）: {detail}", "no_response": True}
        errors = data.get("CompilationErrors") or []
        if data.get("Success") or errors:
            # Line は実際にコンパイルされたコード（UpdatedCode。無ければスニペット）の行 / Line indexes the compiled code
            code_lines = str(data.get("UpdatedCode") or snippet.read_text(encoding="utf-8")).splitlines()
            return {"status": "compiled", "diagnostics": [normalize(e, code_lines) for e in errors]}
        # コンパイル診断なしの失敗は実行拒否・例外。1回だけ取り直す / no diagnostics: rejection or runtime error, retry once
        reason = str(data.get("ErrorMessage") or data.get("Error") or "Success=false・CompilationErrors なし")[:200]
        if attempt == 0:
            time.sleep(3)
    return {"status": "unknown", "reason": reason}


def normalize(diag: dict, code_lines: list[str]) -> dict:
    # 行番号は揺れるので、指す行のソース文字列（前後空白除去）で呼び出し箇所を識別する。取れなければ None
    # Line numbers drift, so the stripped source text of the pointed line identifies the call site; None if unknown
    line = diag.get("Line")
    source = code_lines[line - 1].strip() if isinstance(line, int) and 1 <= line <= len(code_lines) else None
    return {"code": str(diag.get("ErrorCode") or "").strip(),
            "message": " ".join(str(diag.get("Message") or "").split()), "source": source or None}


def record(repo_root: Path, out_json: Path) -> dict:
    # 作業ツリーの現状で全シナリオをコンパイルし out_json に書く。最初の1本が無応答なら Editor 不在として打ち切る
    # Compile every scenario in the current worktree; if the very first call gets no response, treat the Editor as absent
    snippet_dir = out_json.with_suffix("")
    snippet_dir.mkdir(parents=True, exist_ok=True)
    project = repo_root / "moorestech_client"
    scenarios: dict[str, dict] = {}
    status, reason = "ok", ""
    for path in list_scenarios(repo_root):
        rel = str(path.relative_to(repo_root))
        snippet = snippet_dir / (rel.split(f"{SCENARIO_MARK}/", 1)[-1].replace("/", "__") + ".compile.cs")
        snippet.write_text(compile_snippet(path), encoding="utf-8")
        result = run_edc(project, snippet)
        if not scenarios and result.pop("no_response", False):
            status, reason = "unavailable", result["reason"]
            break
        result.pop("no_response", None)
        scenarios[rel] = result
    if status == "ok" and any(s["status"] != "compiled" for s in scenarios.values()):
        status = "partial"
    data = {"status": status, "reason": reason, "scenarios": scenarios}
    out_json.parent.mkdir(parents=True, exist_ok=True)
    out_json.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    return data


def _diff_scenario(rel: str, prev: dict | None, entry: dict) -> tuple[list[dict], int, list[str]]:
    # 同じ (コード, メッセージ, 指す行の文字列) だけを既存とみなす（別の呼び出しの同文面 CS1503 と相殺しない）。
    # 行の文字列が取れない診断は、同文面が反映前にあれば対応を確認できないので未確認、無ければ増分
    # Only identical (code, message, source line) counts as existing; a source-less diagnostic is unverified when
    # the same text existed before (call site cannot be matched), otherwise new
    before = Counter((d["code"], d["message"], d.get("source")) for d in (prev or {}).get("diagnostics", [])
                     if d.get("source"))
    loose = Counter((d["code"], d["message"]) for d in (prev or {}).get("diagnostics", []))
    new, unverified, existing = [], [], 0
    for d in entry.get("diagnostics", []):
        key, item = (d["code"], d["message"], d.get("source")), {"scenario": rel, "code": d["code"],
                                                               "message": d["message"], "source": d.get("source")}
        if d.get("source") and before[key] > 0:
            before[key] -= 1
            existing += 1
        elif not d.get("source") and loose[(d["code"], d["message"])] > 0:
            unverified.append(f"{rel}: {d['code']} {d['message']}（指す行が取れず反映前の同文面と対応を確認できない）")
        else:
            new.append(item)
    return new, existing, unverified


def compare(before: dict | None, after: dict) -> dict:
    # 反映後にだけ増えた診断 = この反映の破壊。反映前が取れていなければ既存扱いせず未確認にする
    # Diagnostics present only after = broken by this change. Without a usable "before", report unverified, never "existing"
    unverified: list[str] = []
    if before is None:
        unverified.append("反映前の診断記録が無い（反映前に record を取っていない）")
    elif before.get("status") == "unavailable":
        unverified.append(f"反映前の診断が取れていない（{before.get('reason')}）")
    if after.get("status") == "unavailable":
        unverified.append(f"反映後の診断が取れない（{after.get('reason')}）")
    new: list[dict] = []
    existing = 0
    before_s = (before or {}).get("scenarios", {})
    for rel, entry in after.get("scenarios", {}).items():
        if entry["status"] != "compiled":
            unverified.append(f"{rel}: 反映後 {entry.get('reason')}")
            continue
        prev = before_s.get(rel)
        if before is not None and prev is not None and prev["status"] != "compiled":
            unverified.append(f"{rel}: 反映前 {prev.get('reason')}")
            continue
        if before is None or before.get("status") == "unavailable":
            continue
        # 反映前に無いシナリオ（今回追加）は全診断が増分 / a scenario absent before counts all its diagnostics as new
        added, kept, unsure = _diff_scenario(rel, prev, entry)
        new += added
        existing += kept
        unverified += unsure
    status = "new_errors" if new else ("unverified" if unverified else "ok")
    return {"required": True, "status": status, "new": new, "existing": existing,
            "unverified": unverified, "compiled": len(after.get("scenarios", {})),
            "after_errors": sum(len(e.get("diagnostics", [])) for e in after.get("scenarios", {}).values())}
