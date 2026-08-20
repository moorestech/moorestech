#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# =====================================================================
"""Codex外部監査の結論を確実に回収する（.out.md が途中で切れていても失敗と断定しない）。
Recover the final answer of a codex audit even when its stdout .out.md was truncated.

背景: 2026-08-18、codex exec 3本が task_complete まで完走したのに、リダイレクト先の
.out.md には最終回答が入らず（ツール実行ログの途中で終端）、integrator が「欠員」と誤判定した。
codex はセッションを $CODEX_HOME/sessions/YYYY/MM/DD/rollout-*.jsonl に必ず残すので、そこが正本。

Background: on 2026-08-18 three codex runs completed (task_complete) but their redirected
.out.md ended mid tool-log, so the integrator wrongly reported them as missing systems.
The rollout jsonl under $CODEX_HOME/sessions is the authoritative record, so read it instead.

usage:
  codex_recover.py --prompt <prompt.md> --out <stdout .out.md> [--final <final.md>] [--since-min N]

exit: 0=結論あり(ok/recovered) / 3=セッションはあるが未完走 / 4=セッションが見つからない / 5=認証失効(401)
"""
import argparse
import json
import os
import re
import sys
import time
from pathlib import Path


# stdout に残る認証失効の痕跡。codex 自身の ERROR 行にアンカーする（監査対象本文の引用で誤マッチさせない）
# Footprints of an expired login, anchored to codex's own ERROR lines (never the audited text)
AUTH_FAILURE_RE = re.compile(
    r"^\S*\s*ERROR\s+codex_login.*(401 Unauthorized|refresh_token_invalidated|Please log in again)", re.M)
AUTH_SCAN_TAIL_BYTES = 8192


def auth_failure_in_stdout(out_path: Path) -> bool:
    # 認証エラーは起動直後（先頭）か終了間際（末尾）に出る。監査本文の巻き込みを避けるため両端だけ読む
    # Auth errors appear right after launch (head) or at exit (tail); read only both ends, never the body
    if not out_path.is_file():
        return False
    with out_path.open("rb") as handle:
        head = handle.read(AUTH_SCAN_TAIL_BYTES).decode("utf-8", errors="replace")
        handle.seek(0, 2)
        size = handle.tell()
        handle.seek(max(0, size - AUTH_SCAN_TAIL_BYTES))
        tail = handle.read().decode("utf-8", errors="replace")
    return bool(AUTH_FAILURE_RE.search(head) or AUTH_FAILURE_RE.search(tail))


def codex_sessions_root() -> Path:
    return Path(os.environ.get("CODEX_HOME", str(Path.home() / ".codex"))) / "sessions"


def normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def scan_session(path: Path):
    """rollout jsonl から (先頭user_message, 最終結論, 完走フラグ) を取り出す。"""
    first_user = None
    last_answer = None
    completed = False
    with path.open(encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            # 壊れた行が1つあっても走査は止めない / one broken line must not abort the scan
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                continue
            payload = record.get("payload")
            if not isinstance(payload, dict):
                continue
            kind = payload.get("type")
            if kind == "user_message" and first_user is None:
                first_user = payload.get("message") or ""
            elif kind == "agent_message":
                last_answer = payload.get("message") or last_answer
            elif kind == "task_complete":
                completed = True
                last_answer = payload.get("last_agent_message") or last_answer
    return first_user, last_answer, completed


def find_session(prompt_text: str, since_min: int):
    """プロンプト本文の先頭一致で該当セッションを引き当てる（最新優先）。"""
    needle = normalize(prompt_text)[:200]
    if not needle:
        return None
    cutoff = time.time() - since_min * 60
    root = codex_sessions_root()
    if not root.is_dir():
        return None
    candidates = [p for p in root.rglob("rollout-*.jsonl") if p.stat().st_mtime >= cutoff]
    for path in sorted(candidates, key=lambda p: p.stat().st_mtime, reverse=True):
        first_user, last_answer, completed = scan_session(path)
        if first_user and needle in normalize(first_user):
            return path, last_answer, completed
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--prompt", required=True, help="codex exec に渡したプロンプトファイル")
    parser.add_argument("--out", required=True, help="stdout をリダイレクトした .out.md")
    parser.add_argument("--final", help="結論の書き出し先（既定: <out>.final.md）")
    parser.add_argument("--since-min", type=int, default=360)
    args = parser.parse_args()

    out_path = Path(args.out)
    final_path = Path(args.final) if args.final else out_path.with_suffix(".final.md")
    result = {"prompt": args.prompt, "out": str(out_path), "final": str(final_path)}

    # codex -o が正常に書けていればそれが最優先 / the -o artifact wins when present
    if final_path.is_file() and final_path.read_text(encoding="utf-8", errors="replace").strip():
        result.update(status="ok", source="output-last-message")
        print(json.dumps(result, ensure_ascii=False))
        return 0

    prompt_text = Path(args.prompt).read_text(encoding="utf-8", errors="replace")
    found = find_session(prompt_text, args.since_min)
    if not found:
        # rollout に結論が無いときだけ stdout の認証失効痕跡を見る（監査対象の引用で誤マッチしないよう末尾8KB・codexのERROR行に限定）
        # Only when the rollout has no answer do we look for an auth failure in stdout (tail 8KB, codex ERROR lines only)
        if auth_failure_in_stdout(out_path):
            result.update(status="auth_expired", source="stdout",
                          hint="codex の認証が失効（401）。この CODEX_HOME で `codex login` を再実行する必要がある")
            print(json.dumps(result, ensure_ascii=False))
            return 5
        result.update(status="missing", source="none",
                      hint="$CODEX_HOME/sessions に該当セッションなし。起動自体が失敗した可能性")
        print(json.dumps(result, ensure_ascii=False))
        return 4

    session_path, answer, completed = found
    result["session"] = str(session_path)
    if not answer:
        result.update(status="incomplete", source="rollout",
                      completed=completed, hint="セッションはあるが結論が無い。再実行が必要")
        print(json.dumps(result, ensure_ascii=False))
        return 3

    final_path.write_text(answer, encoding="utf-8")
    result.update(status="recovered", source="rollout", completed=completed,
                  chars=len(answer))
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
