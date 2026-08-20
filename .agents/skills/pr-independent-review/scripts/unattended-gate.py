#!/usr/bin/env python3
"""無人レビュー/無人applyセッションの関所。両スキルのfrontmatter hooksから呼ばれ、スキル発動中だけ有効になる。
Gate for unattended review/apply sessions; wired from both skills' frontmatter hooks so it is active only while the skill runs.

stop  … 成果物が無いままターンを終えようとしたらblockし、正しい終わり方を再注入する
ask   … 無人実行中の AskUserQuestion をdenyし、判断の行き先（裁定カード / abort.json）を再注入する

なぜhookが要るか: cmuxフォアグラウンド化でターン終了してもプロセスが死ななくなり、止まっても誰も気付かない。
pollerはtranscriptが1200秒止まって初めて自壊と判定し、唯一のRESUME予算を1回消費する（実質20分の空転）。
Why: after the cmux foreground move, ending a turn no longer kills the process, so a premature stop is silent —
the poller only notices after 1200s of transcript silence and then spends its single RESUME budget.

全失敗経路はexit 0・無出力（fail open）。関所の実装バグでレビューが止まる方が、たまに素通しするより高くつく。
Every failure path exits 0 silently: a gate bug stalling reviews costs far more than an occasional miss.
"""
import json
import os
import re
import sys

# 無人起動プロンプトの目印。人が対話で起動した場合は関所を立てない（質問も停止も自由）
# Marker of the unattended launch prompt; attended manual runs are left untouched
UNATTENDED_MARK = "【無人起動】"
MAX_BLOCKS = 2
RUNDIR_BASE = os.environ.get(
    "PR_REVIEW_RUNDIR_BASE",
    "/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs",
)
REVIEW_PROMPT_RE = re.compile(r"/pr-independent-review\D+(\d+)")
APPLY_PROMPT_RE = re.compile(r"/pr-adjudicated-apply\s+(\d+)")


def first_user_text(transcript_path: str) -> str:
    """transcriptの先頭userメッセージ本文。起動プロンプトはここにしか無い。
    Text of the first user message; the launch prompt lives only there."""
    # 外部境界（外部が書いたjsonlの読み取り・パース）のためtry許容
    # External boundary: reading and parsing a jsonl written by another process
    try:
        with open(transcript_path, "r", encoding="utf-8") as f:
            for line in f:
                try:
                    rec = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if rec.get("type") != "user":
                    continue
                content = rec.get("message", {}).get("content")
                if isinstance(content, str):
                    return content
                if isinstance(content, list):
                    return "".join(b.get("text", "") for b in content if isinstance(b, dict))
        return ""
    except OSError:
        return ""


def resolve_rundir(number: str) -> str:
    """再レビューは runs/pr-<N>-r2/ 以降に積まれるため最新を解く（poller.rundir と同一規則）。
    Re-reviews land in runs/pr-<N>-r2/ onward, so resolve the newest (same rule as poller.rundir)."""
    best, best_rev = os.path.join(RUNDIR_BASE, f"pr-{number}"), 1
    prefix = f"pr-{number}-r"
    try:
        for name in os.listdir(RUNDIR_BASE):
            if not name.startswith(prefix) or not name[len(prefix):].isdigit():
                continue
            rev = int(name[len(prefix):])
            if rev > best_rev:
                best, best_rev = os.path.join(RUNDIR_BASE, name), rev
    except OSError:
        pass
    return best


def block_count_exceeded(session_id: str, tag: str) -> bool:
    """自前のブロック上限カウンタ。ハーネス側のstop_hook_activeだけでは2回目以降を抑えられない。
    Own block counter; the harness stop_hook_active flag alone cannot cap repeats (shadow-gate precedent)."""
    state_dir = os.path.join(os.environ.get("TMPDIR", "/tmp"), "claude-unattended-gate")
    path = os.path.join(state_dir, f"{session_id}.{tag}")
    try:
        os.makedirs(state_dir, exist_ok=True)
        count = int(open(path, encoding="utf-8").read().strip()) if os.path.exists(path) else 0
        if count >= MAX_BLOCKS:
            return True
        with open(path, "w", encoding="utf-8") as f:
            f.write(str(count + 1))
        return False
    except (OSError, ValueError):
        # 書けない＝カウントできない。無限ブロックを避けるため通す / Cannot count, so let it through
        return True


def main() -> int:
    # 外部境界（ハーネスがstdinへ渡すJSON）/ External boundary: JSON handed in by the harness on stdin
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    job = sys.argv[2] if len(sys.argv) > 2 else ""
    session_id = payload.get("session_id", "")
    prompt = first_user_text(payload.get("transcript_path", ""))
    if not session_id or UNATTENDED_MARK not in prompt:
        return 0

    if mode == "ask":
        sys.stderr.write(
            "無人実行中のため AskUserQuestion は使えません。判断が要る指摘は"
            "レビューならダイジェストの裁定カード（設計判断）へ、applyなら実装せず"
            "apply-result.json の summary へ落としてください。"
            "続行不能な場合のみ $RUNDIR/abort.json に理由を書いて終えること。\n"
        )
        return 2

    if mode != "stop":
        return 0

    pattern = APPLY_PROMPT_RE if job == "apply" else REVIEW_PROMPT_RE
    match = pattern.search(prompt)
    if not match:
        return 0
    run = resolve_rundir(match.group(1))
    goal = "apply-result.json" if job == "apply" else "session-done.marker"
    if os.path.exists(os.path.join(run, goal)) or os.path.exists(os.path.join(run, "abort.json")):
        return 0
    if block_count_exceeded(session_id, f"stop-{job or 'review'}"):
        return 0

    tail = (
        "SKILL.md の手順を最後まで走り切って apply-result.json を書く"
        if job == "apply"
        else "SKILL.md の Step 7.5（findings.json）から Step 8 まで走り切って session-done.marker を書く"
    )
    sys.stderr.write(
        f"無人実行の成果物がまだありません（{run}/ に {goal} も abort.json も無い）。"
        "ここでターンを終えても対話モードなのでプロセスは死なず、pollerはtranscript停止として扱います。"
        "最大1200秒空転したうえで唯一のRESUME予算を1回消費するだけで、誰にも気付かれません。"
        f"{tail}か、続行不能なら $RUNDIR/abort.json に理由を書いてください。"
        "サブエージェントの完了待ちなら、ターンを閉じずに同一ターン内でブロッキングして待つこと。"
        f"（このブロックは{MAX_BLOCKS}回でフェイルオープンします）\n"
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
