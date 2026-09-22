#!/usr/bin/env python3
# =====================================================================
# ⚠ scripts変更後: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
# =====================================================================
"""Run independent workers for every original requirement."""

import argparse
import fcntl
import json
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from input_bundle import bundle, snapshot
from process_owner import ProcessOwner
from run_state import atomic_json, atomic_text
from worker_runs import VERDICTS, failure_result, launch


def _manifest(path, data):
    if not path.exists():
        atomic_json(path, data)
        return
    try:
        previous = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError(f"壊れたmanifestで入力同一性を証明できない: {error}") from error
    if previous != data:
        raise ValueError("入力/コード/modelが変更された: 新しいrun-dirで検証する")


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
        data = bundle(inputs[0].read_text(encoding="utf-8"), inputs[1].read_text(encoding="utf-8"),
                      procedure.read_text(encoding="utf-8"), repo, args.model)
        _manifest(target / "manifest.json", data)
        print(f'要求{len(data["units"])}件、model={args.model}、最大同時3件。全報告回収は全要求達成の意味ではない。', file=sys.stderr)
        owner = ProcessOwner(lock.fileno())
        owner.install_signals()
        try:
            with ThreadPoolExecutor(max_workers=3) as pool:
                futures = [pool.submit(launch, data, unit, target / unit["id"], owner)
                           for unit in data["units"]]
                results = []
                for unit, future in zip(data["units"], futures):
                    try:
                        result = future.result()
                    except (OSError, subprocess.SubprocessError) as error:
                        result = failure_result(unit["id"], target / unit["id"], str(error))
                        print(f'{unit["id"]}: worker failure: {error}', file=sys.stderr)
                    results.append(result)
                    print(f'{unit["id"]}: {result["verdict"]}', file=sys.stderr)
        finally:
            owner.stop_all()
            owner.restore_signals()
        if owner.failures and results:
            results[0] = {"id": results[0]["id"], "verdict": "MISSING",
                          "reason": "; ".join(owner.failures),
                          "reportPath": results[0].get("reportPath"),
                          "evidencePath": results[0].get("evidencePath", str(target.resolve()))}
        try:
            changed = snapshot(repo) != data["snapshot"]
        except (OSError, subprocess.SubprocessError) as error:
            print(f"snapshot failure: {error}", file=sys.stderr)
            changed = True
        missing = [row["id"] for row in results if row["verdict"] == "MISSING"]
        lines = ["# 原文要求の独立検査", "",
                 "以下はworkerの静的検査報告。実行試験・正しさの機械証明ではない。",
                 f'予定: {len(data["units"])}、回収: {len(results) - len(missing)}、欠員: {missing}',
                 f"検査中のコード変化: {changed}", ""]
        for row in results:
            lines.extend([f'## {row["id"]}: {VERDICTS.get(row["verdict"], "未完了")}',
                          f'個別報告先: {row.get("reportPath") or "なし"}',
                          f'失敗証拠先: {row.get("evidencePath") or "該当なし"}',
                          row.get("report", row.get("reason", "欠損")), ""])
        atomic_text(target / "summary.md", "\n".join(lines))
        atomic_json(target / "results.json", {"results": results, "codeChanged": changed,
                                               "missing": missing})
        if changed or missing:
            print(f"未完了: コード変化={changed}, 欠員={missing}", file=sys.stderr)
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
