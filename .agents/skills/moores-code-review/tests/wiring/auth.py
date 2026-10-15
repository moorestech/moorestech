import re
import unittest
from pathlib import Path
from wiring.context import SKILL_DIR, SKILL_MD, REPO_ROOT, WORKFLOW_SRC

class CodexRecoverAuthOrderTest(unittest.TestCase):
    """認証失効(exit 5)は rollout に結論が無いときだけ・codex 自身の ERROR 行だけで判定する（2026-08-20 レビュー C1）。
    exit 5 only when the rollout has no answer, and only from codex's own ERROR lines."""

    def _run(self, out_text: str) -> int:
        import os, subprocess, sys, tempfile
        with tempfile.TemporaryDirectory() as td:
            prompt = Path(td) / "codex-x.md"
            out = Path(td) / "codex-x.out.md"
            prompt.write_text("unique-prompt-that-matches-no-session-9f3a", encoding="utf-8")
            out.write_text(out_text, encoding="utf-8")
            env = dict(os.environ, CODEX_HOME=td)  # sessions/ 不在 → rollout に結論なし
            run = subprocess.run([sys.executable, str(SKILL_DIR / "scripts/codex_recover.py"),
                                  "--prompt", str(prompt), "--out", str(out)],
                                 capture_output=True, text=True, env=env)
            return run.returncode

    def test_real_auth_error_line_is_exit_5(self):
        self.assertEqual(self._run(
            "2026-08-19T20:37:49Z ERROR codex_login::auth::manager: Failed to refresh token: 401 Unauthorized: {\n"), 5)

    def test_audited_text_mentioning_401_is_not_auth_failure(self):
        # 監査対象の本文（プロンプト echo）に 401 の文字があっても認証失効とは判定しない
        # The audited text echoed to stdout may mention 401; that must not read as an auth failure
        self.assertEqual(self._run(
            "user\n認証失効(401 Unauthorized / Please log in again) を exit 5 で区別する設計をレビューせよ\n"), 4)
