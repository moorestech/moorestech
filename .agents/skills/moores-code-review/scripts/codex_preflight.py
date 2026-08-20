#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""codex_preflight.py — Codex外部監査の起動前検査（バイナリ解決＋認証ファイルの実在）。

背景: 無人レビュー環境（封じ込めPATH）で `which codex` が失敗し、本当は
~/.local/bin に実体があるのに「codex不在」として3本とも起動前縮退した run が
2026-08-16〜20 に10本連続した。PATH依存の `which` をやめ、既知の実体パスまで
辿って解決し、認証ファイル（$CODEX_HOME/auth.json）の有無と鮮度も併せて返す。

Background: under the containment PATH `which codex` fails although the binary
lives in ~/.local/bin; ten unattended reviews in a row degraded "codex missing".
Resolve through known install paths and report auth.json presence/age as well.

usage:
  codex_preflight.py            # JSONを標準出力へ / JSON to stdout
exit: 0=ok / 2=バイナリ不在 / 3=認証ファイル不在
"""
import json
import os
import shutil
import sys
import time
from pathlib import Path

# 既知のインストール先（PATHに無い環境の救済順）/ Known install locations, tried in order
CANDIDATE_BINARIES = (
    "~/.local/bin/codex",
    "~/.codex/packages/standalone/current/bin/codex",
    "/opt/homebrew/bin/codex",
    "/usr/local/bin/codex",
)


def resolve_binary() -> str | None:
    found = shutil.which("codex")
    if found:
        return found
    for cand in CANDIDATE_BINARIES:
        p = Path(os.path.expanduser(cand))
        if p.is_file() and os.access(p, os.X_OK):
            return str(p)
    return None


def codex_home() -> Path:
    return Path(os.environ.get("CODEX_HOME", str(Path.home() / ".codex")))


def main() -> int:
    home = codex_home()
    auth = home / "auth.json"
    binary = resolve_binary()
    result = {
        "codex": binary,
        "codex_home": str(home),
        "auth_json": str(auth),
        "auth_exists": auth.is_file(),
        "auth_age_days": None,
        "status": "ok",
        "hint": "",
    }
    if auth.is_file():
        result["auth_age_days"] = round((time.time() - auth.stat().st_mtime) / 86400, 1)

    if binary is None:
        result.update(status="missing_binary",
                      hint="codex バイナリが見つからない。PATH か CANDIDATE_BINARIES を確認")
        print(json.dumps(result, ensure_ascii=False))
        return 2
    if not auth.is_file():
        result.update(status="missing_auth",
                      hint=f"{auth} が無い。この CODEX_HOME で `codex login` が必要")
        print(json.dumps(result, ensure_ascii=False))
        return 3
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
