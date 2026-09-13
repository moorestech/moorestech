#!/usr/bin/env python3
"""`gh pr checkout` を PR専用worktree（pr-<番号>）以外で実行させない PreToolUse(Bash) hook。
PreToolUse(Bash) hook that refuses `gh pr checkout` unless it runs inside the PR-dedicated worktree pr-<number>.

`gh pr checkout` は cwd の worktree のブランチを切り替える。$ORIGIN（多くはメインworktree）で叩くと、他セッションの
作業ブランチから引き剥がす（2026-08-05 実事故）。許す形は「同一コマンド内で `cd <…>/pr-<番号>` してから叩く」だけ。
無人・対話を問わず常に効く（事故は対話モードで起きた）。判定不能な入力は通す（fail open）。
Allowed only as `cd <...>/pr-<number> && gh pr checkout ...` in the same command; unparsable input passes (fail open).
"""
import json
import re
import sys

CHECKOUT_RE = re.compile(r"\bgh\s+pr\s+checkout\b")
CD_PRWT_RE = re.compile(r"\bcd\s+(?:\"[^\"]*\"|'[^']*'|\S+)")
PRWT_DIR_RE = re.compile(r"/pr-\d+/?['\"]?$")


def cd_targets_prwt(command: str, checkout_pos: int) -> bool:
    # checkout より前にある cd の行き先が pr-<番号> で終わっていればよい / A cd before the checkout must end in pr-<number>
    for m in CD_PRWT_RE.finditer(command[:checkout_pos]):
        target = m.group(0).split(None, 1)[1]
        if PRWT_DIR_RE.search(target):
            return True
    return False


def main() -> int:
    # 外部境界（ハーネスが stdin へ渡す JSON）/ External boundary: JSON handed in by the harness on stdin
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0
    command = payload.get("tool_input", {}).get("command", "")
    if not isinstance(command, str):
        return 0
    match = CHECKOUT_RE.search(command)
    if not match or cd_targets_prwt(command, match.start()):
        return 0
    sys.stderr.write(
        "checkout_guard: `gh pr checkout` は PR専用worktree の外では実行できません。"
        "cwd の worktree のブランチを切り替えるため、$ORIGIN で叩くと他セッションの作業ブランチを引き剥がします。"
        "`cd <$PRWTの実値> && gh pr checkout <番号>` の形（$PRWT は pr-<番号>）で叩き直してください。"
        "PRブランチの取得自体は `git -C <$ORIGINの実値> fetch` + `worktree add` で行います。\n"
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
