#!/usr/bin/env python3
"""正典tree `$CANON`（origin/master のSHAへピンした測定器worktree）を用意し、散文で守らせていたガードを終了コードで裁く。
Prepare the canonical tree `$CANON` (a worktree pinned to origin/master) and enforce the former prose guards via exit codes.

使い方 / Usage:
    canon_setup.py --origin <起動元repo> --parent <worktree親ディレクトリ> [--allow-skew]

stdout に JSON: {"canon", "sha8", "origin_master_sha", "skew", "origin_head_sha", "cleaned", "warnings"}
exit 0  … 用意完了（--allow-skew 付きで skew:true の場合を含む）
exit 10 … origin/master の fetch / rev-parse 失敗
exit 11 … worktree add 失敗
exit 12 … $CANON に novelty_gate.py が無い（誤決定した$CANONで走らせない）
exit 13 … SKILL.md 同一性ガードで差分（$ORIGIN に未マージのskill改修がある。--allow-skew で続行可）
exit 14 … $ORIGIN が git 作業ツリーでない / $CANON が $ORIGIN と同一パスになる（物差しが実行中に動く経路）
"""
import argparse
import json
import os
import subprocess
import sys
import time

SKILL_REL = os.path.join(".agents", "skills", "pr-independent-review")
NOVELTY_REL = os.path.join(SKILL_REL, "scripts", "novelty_gate.py")
SKILL_MD_REL = os.path.join(SKILL_REL, "SKILL.md")
STALE_SECONDS = 24 * 3600


def git(repo: str, *args: str) -> subprocess.CompletedProcess:
    # 外部境界（git プロセス起動）/ External boundary: spawning git
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True)


def fail(code: int, message: str) -> int:
    sys.stderr.write(f"canon_setup: {message}\n")
    return code


def resolve_pin(origin: str) -> tuple[str, str] | None:
    # origin/master を更新してピンSHAを確定する / Refresh origin/master and settle the pin SHA
    fetched = git(origin, "fetch", "origin", "+refs/heads/master:refs/remotes/origin/master")
    if fetched.returncode != 0:
        return None
    short = git(origin, "rev-parse", "--short=8", "refs/remotes/origin/master")
    full = git(origin, "rev-parse", "refs/remotes/origin/master")
    if short.returncode != 0 or full.returncode != 0:
        return None
    return short.stdout.strip(), full.stdout.strip()


def clean_stale_pins(origin: str, parent: str, keep: str) -> tuple[list[str], list[str]]:
    # 24時間使われていない古ピンを消す。失敗は警告に留める（衛生であって測定の前提ではない）
    # Remove pins unused for 24h; failures are warnings only (hygiene, not a measurement prerequisite)
    cleaned, warnings = [], []
    now = time.time()
    for name in sorted(os.listdir(parent)):
        path = os.path.join(parent, name)
        if not name.startswith("skills-canon") or path == keep or not os.path.isdir(path):
            continue
        marker = os.path.join(path, ".last-used")
        if os.path.exists(marker) and now - os.path.getmtime(marker) < STALE_SECONDS:
            continue
        removed = git(origin, "worktree", "remove", "--force", path)
        if removed.returncode == 0:
            cleaned.append(path)
        else:
            warnings.append(f"古ピンの掃除に失敗: {path}: {removed.stderr.strip()}")
    return cleaned, warnings


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--origin", required=True)
    ap.add_argument("--parent", required=True)
    ap.add_argument("--allow-skew", action="store_true")
    args = ap.parse_args()
    origin = os.path.realpath(args.origin)
    parent = os.path.realpath(args.parent)

    # $ORIGIN の妥当性 / Validate $ORIGIN
    toplevel = git(origin, "rev-parse", "--show-toplevel")
    if toplevel.returncode != 0 or os.path.realpath(toplevel.stdout.strip()) != origin:
        return fail(14, f"$ORIGIN が git 作業ツリーのルートではない: {origin}")

    pin = resolve_pin(origin)
    if pin is None:
        return fail(10, "origin/master の fetch または rev-parse に失敗")
    sha8, full_sha = pin
    canon = os.path.join(parent, f"skills-canon-{sha8}")
    if canon == origin:
        return fail(14, f"$CANON が $ORIGIN と同一パスになる: {canon}")

    # 無ければ作る。既存はSHA固定なので触らない / Create if missing; an existing pin is immutable
    if not os.path.isdir(canon):
        os.makedirs(parent, exist_ok=True)
        added = git(origin, "worktree", "add", canon, "--detach", full_sha)
        if added.returncode != 0:
            return fail(11, f"worktree add 失敗: {added.stderr.strip()}")
    with open(os.path.join(canon, ".last-used"), "w", encoding="utf-8"):
        pass
    cleaned, warnings = clean_stale_pins(origin, parent, canon)

    # 実在確認は novelty_gate.py で行う（moores-code-review/SKILL.md は $PRWT 側にもあり弁別にならない）
    # Existence check uses novelty_gate.py (moores-code-review/SKILL.md also exists in $PRWT, so it cannot discriminate)
    if not os.path.isfile(os.path.join(canon, NOVELTY_REL)):
        return fail(12, f"$CANON に novelty_gate.py が無い: {canon}")

    # SKILL.md 同一性ガード。本体は harness が $ORIGIN から読むため、差分＝「新しい指示 × 古いreviewer」の版ズレ
    # SKILL.md identity guard: the harness reads the body from $ORIGIN, so a diff means new instructions on old reviewers
    origin_md = os.path.join(origin, SKILL_MD_REL)
    canon_md = os.path.join(canon, SKILL_MD_REL)
    with open(origin_md, "rb") as a, open(canon_md, "rb") as b:
        skew = a.read() != b.read()
    origin_head = git(origin, "rev-parse", "HEAD").stdout.strip()
    result = {
        "canon": canon,
        "sha8": sha8,
        "origin_master_sha": full_sha,
        "skew": skew,
        "origin_head_sha": origin_head,
        "cleaned": cleaned,
        "warnings": warnings,
    }
    print(json.dumps(result, ensure_ascii=False))
    if skew and not args.allow_skew:
        return fail(13, f"SKILL.md 同一性ガードで差分: $ORIGIN={origin_head} $CANON={full_sha}。"
                        "ユーザーが続行を選んだ場合のみ --allow-skew で再実行し records の canonical: に skew を明記する")
    return 0


if __name__ == "__main__":
    sys.exit(main())
