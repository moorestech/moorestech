#!/usr/bin/env python3
# .claude/skills/pr-independent-review/scripts/git_query.py
# 新規性ゲートのgit問い合わせ層 — diff取得の安全設定と、ref時点のasmdef参照集合の読み取りを担う
# Git query layer for the novelty gate: safe diff invocation and reading asmdef references at a ref
import json
import subprocess

# 非ASCIIパスのクォート（core.quotepath既定on）は`+++ b/`マッチを外し誤帰属を生むため呼び出し毎に打ち消す
# core.quotepath defaults to on and would quote non-ASCII paths, breaking `+++ b/` and misattributing lines
GIT_SAFE_CONFIG = ["-c", "core.quotepath=false"]
# パーサ保護のdiff共通フラグ。ANSI・外部diff・textconv・rename圧縮はいずれも追加行を消して偽クリーンを作る
# Shared diff flags; color, external diff, textconv and rename compaction each hide added lines
DIFF_SAFE_FLAGS = ["--no-color", "--no-ext-diff", "--no-textconv", "--text", "--no-renames"]


def git(repo, *args):
    cmd = ["git", "-C", repo, *GIT_SAFE_CONFIG, *args]
    # --text指定でバイナリ差分も生で流れてくるため、非UTF-8バイトはデコード置換で受ける（クラッシュさせない）
    # --text dumps binary hunks verbatim, so replace non-UTF-8 bytes instead of crashing on decode
    return subprocess.run(cmd, check=True, capture_output=True, text=True, errors="replace").stdout


def changed_asmdef_paths(repo, base_ref):
    # diffで触れられたasmdefのパスだけを取る。中身の解釈はJSONで行うのでdiff本文は読まない
    # List asmdefs touched by the diff; their contents are read as JSON, never from the diff text
    out = git(repo, "diff", *DIFF_SAFE_FLAGS, "--name-only", f"{base_ref}...HEAD", "--", "*.asmdef")
    return [p for p in out.splitlines() if p]


def asmdef_references(repo, ref, path):
    # 指定ref時点のasmdefの references 集合。ref側に存在しない（新規追加）なら空集合
    # references set at the given ref; a file absent there (newly added) yields an empty set
    exists = subprocess.run(
        ["git", "-C", repo, *GIT_SAFE_CONFIG, "cat-file", "-e", f"{ref}:{path}"],
        capture_output=True, text=True,
    )
    if exists.returncode != 0:
        return set()
    raw = git(repo, "show", f"{ref}:{path}")
    # 外部入力（Unity生成JSON）のパース境界。壊れたJSONを空集合へ化けさせず失敗を見せる
    # External-input parse boundary: a broken JSON must fail loudly, never degrade to an empty set
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as e:
        raise RuntimeError(f"asmdef JSON parse failed ({ref}:{path}): {e}") from e
    refs = data.get("references", [])
    if not isinstance(refs, list):
        raise RuntimeError(f"asmdef references is not a list ({ref}:{path})")
    # GUID形式（"GUID:9a3d..."）も解決せず文字列のまま返す / GUID-style refs are kept verbatim
    return {r for r in refs if isinstance(r, str)}


def collect_asmdef_ref_additions(repo, base_ref):
    # base→HEADのreferences集合差（追加分）だけを出す。diff行の所属判定に依存しない
    # Report only the set difference of references; independent of diff-line membership guessing
    additions = []
    for path in changed_asmdef_paths(repo, base_ref):
        added_refs = asmdef_references(repo, "HEAD", path) - asmdef_references(repo, base_ref, path)
        additions.extend({"file": path, "ref": ref} for ref in sorted(added_refs))
    return additions
