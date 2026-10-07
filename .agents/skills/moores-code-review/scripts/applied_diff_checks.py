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
"""applied_diff_checks.py — 反映 diff の機械的動作確認の対象を決める（orchestrator-steps.md「反映 diff の機械的動作確認」）。

1. 録画シナリオ: 反映 diff が改名・削除・シグネチャ変更した C# の公開宣言名を取り、
   unity-playmode-recorded-playtest 配下の .cs（レビュー diff から除外され、uloop compile の対象でもない）を
   語境界で grep する。参照したシナリオごとに、本体を呼ばないローカル関数へ包んだコンパイル確認用スニペットを
   --out-dir へ書く（uloop execute-dynamic-code --code-file で流すと型・メンバー解決だけを確かめられる）。
2. セーブ/ロード: 反映 diff が Save/Load・DataStore・Json 系に触れたら、既存のセーブ往復テストを選ぶ regex を返す。
   既存テストで覆えない経路は unverified として返す（新しいテスト基盤は作らない）。

Finds what the mechanical checks must cover for an applied diff: playtest scenarios referencing renamed or
removed public C# declarations (with compile-only snippets), and save/load round-trip tests to run.

usage: applied_diff_checks.py <diff> --repo-root <REPO> --out-dir <DIR>  → stdout に JSON
exit: 0=ok / 2=diff が読めない
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from refix_snapshot import is_non_source_path  # noqa: E402

SCENARIO_MARK = "unity-playmode-recorded-playtest"
TYPE_DECL_RE = re.compile(r"\b(?:class|struct|interface|enum|record)\s+([A-Z]\w*)")
MEMBER_DECL_RE = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|protected)\b[^=;{(]*?\b([A-Z]\w*)\s*(?:<[^>]*>)?\s*(?:\(|\{|=>|;)")
USING_DIRECTIVE_RE = re.compile(r"^using\s+(?:static\s+)?[\w.]+(?:\s*=\s*[\w.<>, ]+)?\s*;\s*$")
SAVE_LOAD_PATH_RE = re.compile(r"Save|Load|DataStore|Datastore|Json")
SAVE_LOAD_LINE_RE = re.compile(r"\b\w*(?:Save|Load)\w*\s*\(|Json|DataStore|Datastore")
# 既存のセーブ往復テスト（サーバー側・EditMode）。ブロック・電線・チェーン・接続を含むワールドのセーブ→再ロードを覆う
# Existing save/load round-trip tests (server, EditMode) covering blocks, wires, chains and connections
SAVE_LOAD_TEST_REGEX = r"Tests\.(UnitTest|CombinedTest)\..*(SaveLoad|SaveJsonFile)"
SAVE_LOAD_UNVERIFIED = ["クライアントが線・チェーンを含むセーブから起動する経路（クライアント起動を通す既存の自動テストが無い）"]


def parse_diff(text: str) -> dict[str, dict[str, list[str]]]:
    files: dict[str, dict[str, list[str]]] = {}
    current = None
    for line in text.splitlines():
        m = re.match(r"^diff --git a/(.*) b/(.*)$", line)
        if m:
            current = files.setdefault(m.group(2), {"added": [], "removed": []})
            continue
        if current is None or line.startswith(("+++", "---")):
            continue
        if line.startswith("+"):
            current["added"].append(line[1:])
        elif line.startswith("-"):
            current["removed"].append(line[1:])
    return files


def declared_names(line: str) -> list[str]:
    names = TYPE_DECL_RE.findall(line)
    m = MEMBER_DECL_RE.match(line)
    if m:
        names.append(m.group(1))
    return names


def changed_api(files: dict) -> list[str]:
    # 削除行の宣言のうち、同じ宣言行が追加側に無いもの＝改名・削除・シグネチャ変更（移動だけなら同じ行が残る）。
    # テストの宣言はシナリオから参照されないので数えない（Fake 等の名前で無関係なシナリオを拾わない）
    # Declarations on removed lines whose exact line is absent from the added side: rename, removal or signature change.
    # Test declarations are never referenced by scenarios, so they are skipped
    code = {p: f for p, f in files.items()
            if p.endswith(".cs") and SCENARIO_MARK not in p and not is_non_source_path(p)}
    added = {" ".join(l.split()) for f in code.values() for l in f["added"]}
    names: set[str] = set()
    for f in code.values():
        for line in f["removed"]:
            if " ".join(line.split()) not in added:
                names.update(declared_names(line))
    return sorted(names)


def scenario_hits(repo_root: Path, names: list[str]) -> dict[str, list[str]]:
    scenarios = sorted((repo_root / ".agents" / "skills").glob(f"{SCENARIO_MARK}/**/*.cs"))
    hits: dict[str, list[str]] = {}
    for path in scenarios:
        text = path.read_text(encoding="utf-8", errors="replace")
        for name in names:
            if re.search(rf"\b{re.escape(name)}\b", text):
                hits.setdefault(str(path.relative_to(repo_root)), []).append(name)
    return hits


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


def save_load(files: dict) -> dict:
    touched = sorted(p for p, f in files.items() if p.endswith(".cs") and (
        SAVE_LOAD_PATH_RE.search(p) or any(SAVE_LOAD_LINE_RE.search(l) for l in f["added"] + f["removed"])))
    return {"touched": bool(touched), "files": touched,
            "test_regex": SAVE_LOAD_TEST_REGEX if touched else None,
            "unverified": SAVE_LOAD_UNVERIFIED if touched else []}


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("diff")
    ap.add_argument("--repo-root", required=True)
    ap.add_argument("--out-dir", required=True)
    args = ap.parse_args(argv)
    diff_path, repo_root, out_dir = Path(args.diff), Path(args.repo_root).resolve(), Path(args.out_dir)
    if not diff_path.is_file():
        print(f"diff が無い: {diff_path}", file=sys.stderr)
        return 2
    files = parse_diff(diff_path.read_text(encoding="utf-8", errors="replace"))
    names = changed_api(files)
    hits = scenario_hits(repo_root, names) if names else {}
    snippets = []
    if hits:
        out_dir.mkdir(parents=True, exist_ok=True)
        for rel in hits:
            target = out_dir / (rel.split(f"{SCENARIO_MARK}/", 1)[-1].replace("/", "__") + ".compile.cs")
            target.write_text(compile_snippet(repo_root / rel), encoding="utf-8")
            snippets.append(str(target.resolve()))
    print(json.dumps({"changed_api": names, "scenario_files": sorted(hits), "scenario_hits": hits,
                      "compile_snippets": snippets, "save_load": save_load(files)}, ensure_ascii=False, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
