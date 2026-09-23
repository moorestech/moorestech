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
"""Run independent workers for every original requirement."""

import argparse
import fcntl
import json
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor, wait
from pathlib import Path

from input_bundle import bundle, snapshot
from process_owner import ProcessOwner
from run_state import atomic_json, atomic_text
from worker_runs import VERDICTS, failure_result, launch


def _manifest(path, data):
    if not path.exists():
        if any(path.parent.glob("R*/attempt-*")):
            raise ValueError("manifestが無い既存attemptは入力同一性を証明できない")
        atomic_json(path, data)
        return
    try:
        previous = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"壊れたmanifestで入力同一性を証明できない: {error}") from error
    if previous != data:
        raise ValueError("入力/コード/modelが変更された: 新しいrun-dirで検証する")


def _run_workers(data, target, owner):
    # シグナルは停止要求の記録だけ。停止はこの制御経路で1回だけ行う
    # Signals only record a stop request; this control flow performs the stop exactly once
    with ThreadPoolExecutor(max_workers=3) as pool:
        futures = [pool.submit(launch, data, unit, target / unit["id"], owner)
                   for unit in data["units"]]
        try:
            pending = set(futures)
            while pending and not owner.stop_requested:
                _done, pending = wait(pending, timeout=0.2)
        finally:
            owner.stop_all()
        results = []
        for unit, future in zip(data["units"], futures):
            try:
                result = future.result()
            except (OSError, subprocess.SubprocessError) as error:
                result = failure_result(unit["id"], target / unit["id"], str(error))
                print(f'{unit["id"]}: worker failure: {error}', file=sys.stderr)
            results.append(result)
            print(f'{unit["id"]}: {result["verdict"]}', file=sys.stderr)
    return results


def execute(args):
    repo = Path(args.repo_root).resolve()
    target = Path(args.run_dir).resolve()
    inputs = [Path(args.context).resolve(), Path(args.patch).resolve()]
    procedure = Path(__file__).resolve().parents[2] / "references" / "requirement-proof.md"
    inputs.append(procedure.resolve())
    if target == repo or repo in target.parents:
        raise ValueError("run-dirはコードrepo外のログ置場を指定する")
    if any(path == target or target in path.parents or path in target.parents for path in inputs):
        raise ValueError("run-dirは要求入力と別のディレクトリを指定する")
    target.mkdir(parents=True, exist_ok=True)
    with (target / "run.lock").open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if (target / "invalidated.json").exists():
            raise ValueError("このrun-dirは検査中のコード変化/判定不能で無効化済み: 新しいrun-dirで検証する")
        data = bundle(inputs[0].read_text(encoding="utf-8"), inputs[1].read_text(encoding="utf-8"),
                      procedure.read_text(encoding="utf-8"), repo, args.model)
        _manifest(target / "manifest.json", data)
        print(f'要求{len(data["units"])}件、model={args.model}、最大同時3件。全報告回収は全要求達成の意味ではない。', file=sys.stderr)
        owner = ProcessOwner(lock.fileno())
        owner.install_signals()
        try:
            results = _run_workers(data, target, owner)
        finally:
            owner.restore_signals()
        # 停止要求は個別判定と独立したrun単位の結果。全件SUPPORTEDでも中断runは成功にしない
        # A stop request is a run-level outcome; a cancelled run never succeeds even if all units passed
        stop_requested = owner.stop_requested
        # コード変化は三値: True/False/None(判定不能)。False以外はrun-dirを恒久無効化する
        # Code change is tri-state; anything but False permanently invalidates this run-dir
        change_error = None
        try:
            changed = snapshot(repo) != data["snapshot"]
        except (OSError, subprocess.SubprocessError) as error:
            print(f"snapshot failure: {error}", file=sys.stderr)
            changed, change_error = None, str(error)
        if changed is not False:
            atomic_json(target / "invalidated.json",
                        {"codeChanged": changed, "codeChangeError": change_error})
        missing = [row["id"] for row in results if row["verdict"] == "MISSING"]
        change_line = (f"判定不能（snapshot failure: {change_error}）" if changed is None else changed)
        lines = ["# 原文要求の独立検査", "",
                 "以下はworkerの静的検査報告。実行試験・正しさの機械証明ではない。",
                 f'予定: {len(data["units"])}、回収: {len(results) - len(missing)}、欠員: {missing}',
                 f"検査中のコード変化: {change_line}",
                 f"後始末失敗: {owner.failures}",
                 f"停止要求（SIGTERM/SIGINT）: {'あり（中断run・成功扱いにしない）' if stop_requested else 'なし'}",
                 ""]
        for row in results:
            lines.extend([f'## {row["id"]}: {VERDICTS.get(row["verdict"], "未完了")}',
                          f'個別報告先: {row.get("reportPath") or "なし"}',
                          f'失敗証拠先: {row.get("evidencePath") or "該当なし"}',
                          row.get("report", row.get("reason", "欠損")), ""])
        atomic_text(target / "summary.md", "\n".join(lines))
        atomic_json(target / "results.json", {"results": results, "codeChanged": changed,
                                               "codeChangeError": change_error,
                                               "runnerFailures": owner.failures,
                                               "stopRequested": stop_requested,
                                               "missing": missing})
        if changed is not False or missing or owner.failures or stop_requested:
            print(f"未完了: コード変化={change_line}, 欠員={missing}, 後始末失敗={owner.failures}, "
                  f"停止要求={stop_requested}", file=sys.stderr)
            return 2
        print(target / "summary.md")
        return 0


def main():
    parser = argparse.ArgumentParser()
    for name in ("repo-root", "context", "patch", "run-dir"):
        parser.add_argument("--" + name, required=True)
    parser.add_argument("--model", required=True, choices=["sonnet"])
    args = parser.parse_args()
    try:
        return execute(args)
    except (OSError, ValueError, json.JSONDecodeError, subprocess.SubprocessError) as error:
        print(f"要求検査未完了: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
