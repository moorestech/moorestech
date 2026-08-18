# CLIの入出力と生成後検査を検証する
# Verify the CLI end-to-end and the post-generation checks
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "digest_build.py"
GOLDEN_MD = Path(__file__).resolve().parent / "golden" / "pr-1155-digest.md"

# Task 6 が golden/pr-1155-digest.md を作るまでは、goldenに依存しない最小digestで代替する
# Until Task 6 adds golden/pr-1155-digest.md, fall back to a golden-independent minimal digest
MINIMAL_DIGEST = """# PR #1155 テストPR

```yaml
pr: 1155
head: 0123456789012345678901234567890123456789
verdict: reject
verdict_line: Critical 1件
date: 2026-08-18
generated_at: 2026-08-18T02:44:00+09:00
```

## 最初の指摘

```yaml
slug: first
category: critical
severity: critical
summary: 壊れている。
files: [a/b/C.cs:10]
options:
  - 直す
```

**PR側の主張:** なし

# 注記

## must-read

必読は0件。

## other-rulings

残りも0件。

## suppressed

該当なし（0件）。

## new-shape

該当なし（0件）。

## criticals

1件ある。

# 判断台帳

- ユーザー裁定なし

# 折りたたみ参考

## Warning全件

0件。
"""


@pytest.mark.skipif(not GOLDEN_MD.is_file(), reason="golden/pr-1155-digest.md はTask 6で追加される")
def test_cli_writes_html_and_findings_from_golden(tmp_path):
    (tmp_path / "digest.md").write_text(GOLDEN_MD.read_text(encoding="utf-8"), encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    html = (tmp_path / "digest.html").read_text(encoding="utf-8")
    findings = json.loads((tmp_path / "findings.json").read_text(encoding="utf-8"))
    assert html.count('data-finding-id="') == len(findings["findings"])
    assert findings["pr"] == 1155


def test_cli_writes_html_and_findings_from_minimal_digest(tmp_path):
    (tmp_path / "digest.md").write_text(MINIMAL_DIGEST, encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    html = (tmp_path / "digest.html").read_text(encoding="utf-8")
    findings = json.loads((tmp_path / "findings.json").read_text(encoding="utf-8"))
    assert html.count('data-finding-id="') == len(findings["findings"])
    assert findings["pr"] == 1155


def test_cli_fails_loudly_on_broken_markdown(tmp_path):
    (tmp_path / "digest.md").write_text("# PR #1 x\n\n本文だけ\n", encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 1
    assert r.stderr.strip()
    assert not (tmp_path / "digest.html").exists()
