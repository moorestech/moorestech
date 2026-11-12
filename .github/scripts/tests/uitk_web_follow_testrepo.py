"""Temporary git repositories and ledger text shared by the UITK Web follow gate tests."""

import os
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

GIT_ENV = {
    **os.environ,
    "GIT_AUTHOR_NAME": "gate-test",
    "GIT_AUTHOR_EMAIL": "gate-test",
    "GIT_COMMITTER_NAME": "gate-test",
    "GIT_COMMITTER_EMAIL": "gate-test",
}
BUNDLE_NAMES = {1: "命綱", 2: "常駐表示", 3: "建築", 4: "持ち物", 5: "最後"}


def ledger_text(states: dict[int, tuple[str, str]], rows: list[tuple[str, str, str, str]], records: list[tuple[int, str, str, str]]) -> str:
    # states: {束: (状態, 起点)}、rows: (行, 束, 所有者, Web パス)、records: (束, コミット, 判定, 理由)
    # states: {bundle: (state, start)}, rows: (name, bundles, owner, paths), records: (bundle, commit, verdict, note)
    lines = ["# 台帳", "", "## 束", "", "| 束 | 中身 | 状態 | 暗転の起点 | 物差し |", "|---|---|---|---|---|"]
    for bundle_id in (1, 2, 3, 4, 5):
        state, start = states.get(bundle_id, ("Web", "—"))
        lines.append(f"| {bundle_id} | {BUNDLE_NAMES[bundle_id]} | {state} | {start} | — |")
    lines += ["", "## 行", "", "| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |", "|---|---|---|---|---|---|---|---|"]
    lines += [f"| {name} | 機能 | {bundles} | {owner} | {paths} | — | — | — |" for name, bundles, owner, paths in rows]
    lines += ["", "## 追従記録", "", "| 束 | コミット | 判定 | 写し先・理由 |", "|---|---|---|---|"]
    lines += [f"| {bundle_id} | `{commit}` | {verdict} | {note} |" for bundle_id, commit, verdict, note in records]
    return "\n".join(lines) + "\n"


class TestRepo:
    def __init__(self):
        self._workspace = tempfile.TemporaryDirectory()
        self.root = Path(self._workspace.name)
        self.git("init", "-q", "-b", "master")

    def cleanup(self):
        self._workspace.cleanup()

    def git(self, *args: str) -> str:
        return subprocess.run(["git", *args], cwd=self.root, env=GIT_ENV, check=True, capture_output=True, text=True).stdout.strip()

    def commit(self, message: str, files: dict[str, str | None]) -> str:
        # 値が None のファイルは削除し、それ以外は書いて add する
        # Remove files mapped to None; write and stage the rest
        for path, content in files.items():
            if content is None:
                self.git("rm", "-q", path)
                continue
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8")
            self.git("add", path)
        self.git("commit", "-q", "-m", message)
        return self.git("rev-parse", "HEAD")
