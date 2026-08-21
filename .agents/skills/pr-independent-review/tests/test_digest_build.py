# CLIの入出力と生成後検査を検証する
# Verify the CLI end-to-end and the post-generation checks
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "digest_build.py"
GOLDEN_MD = Path(__file__).resolve().parent / "golden" / "pr-1155-digest.md"
GOLDEN_PATCH = Path(__file__).resolve().parent / "golden" / "pr-1155-patch.diff"

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


def _write_patch(tmp_path, text=""):
    # コンバータは patch.diff を必須とするため、どのケースでも RUNDIR へ置く
    # The converter requires patch.diff, so every case puts one into the RUNDIR
    (tmp_path / "patch.diff").write_text(text, encoding="utf-8")

@pytest.mark.skipif(not GOLDEN_MD.is_file(), reason="golden/pr-1155-digest.md はTask 6で追加される")
def test_cli_writes_html_and_findings_from_golden(tmp_path):
    (tmp_path / "digest.md").write_text(GOLDEN_MD.read_text(encoding="utf-8"), encoding="utf-8")
    _write_patch(tmp_path, GOLDEN_PATCH.read_text(encoding="utf-8"))
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    html = (tmp_path / "digest.html").read_text(encoding="utf-8")
    findings = json.loads((tmp_path / "findings.json").read_text(encoding="utf-8"))
    assert html.count('data-finding-id="') == len(findings["findings"])
    assert findings["pr"] == 1155


def test_cli_writes_html_and_findings_from_minimal_digest(tmp_path):
    (tmp_path / "digest.md").write_text(MINIMAL_DIGEST, encoding="utf-8")
    _write_patch(tmp_path)
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    html = (tmp_path / "digest.html").read_text(encoding="utf-8")
    findings = json.loads((tmp_path / "findings.json").read_text(encoding="utf-8"))
    assert html.count('data-finding-id="') == len(findings["findings"])
    assert findings["pr"] == 1155


def test_cli_fails_loudly_on_broken_markdown(tmp_path):
    (tmp_path / "digest.md").write_text("# PR #1 x\n\n本文だけ\n", encoding="utf-8")
    _write_patch(tmp_path)
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 1
    assert r.stderr.strip()
    assert not (tmp_path / "digest.html").exists()


def test_inline_safe_js_neutralizes_script_terminating_sequences():
    # インラインscriptの本文に <!-- や <script が残るとHTMLパーサが </script> を見失う
    # A leftover <!-- or <script in the inline body makes the HTML parser lose the </script>
    sys.path.insert(0, str(SCRIPT.parent))
    from digest_build import inline_safe_js, load_assets
    for text in (load_assets()["hljs_js"], inline_safe_js("/<!--/ /<script/ '</script>'")):
        assert "<!--" not in text
        assert "<script" not in text
        assert "</script" not in text


def test_vendored_css_cannot_close_the_style_element():
    # CSSは<style>へ素で入るので、テーマ差し替え時に </style を持ち込んでいないか見張る
    # The themes are inlined into <style>, so guard against a swapped-in file carrying </style
    sys.path.insert(0, str(SCRIPT.parent))
    from digest_build import load_assets
    assets = load_assets()
    for key in ("hljs_css_light", "hljs_css_dark"):
        assert "</style" not in assets[key]


def test_missing_patch_diff_fails(tmp_path):
    # R4: patch.diff は Step 3 が必ず作る。無いまま生成させない
    # R4: patch.diff is always produced by Step 3; never build without it
    (tmp_path / "digest.md").write_text(MINIMAL_DIGEST, encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode != 0
    assert "patch.diff" in r.stderr


def test_missing_deletion_line_fails_the_build(tmp_path):
    # R3: 置換なのに削除行が無いカードはビルドごと落ちる（finding idを文言に含む）
    # R3: a card missing its deletion lines fails the whole build, naming the finding id
    card = "```code-card\n+10|            New(rate);\n```"
    (tmp_path / "digest.md").write_text(MINIMAL_DIGEST.replace("**PR側の主張:** なし", card), encoding="utf-8")
    _write_patch(tmp_path, "diff --git a/C.cs b/C.cs\n@@ -10,2 +10,2 @@\n-            Old(1f);\n+            New(rate);\n")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode != 0
    assert "F01" in r.stderr and "削除行" in r.stderr


def test_deletion_only_card_fails_with_an_empty_excerpt(tmp_path):
    # 削除行だけのカードは excerpt が空になる。pr-adjudicated-apply の入力契約が壊れるので落とす
    # A deletion-only card yields an empty excerpt, breaking pr-adjudicated-apply's input contract
    card = "```code-card\n-10|            Old();\n```"
    (tmp_path / "digest.md").write_text(MINIMAL_DIGEST.replace("**PR側の主張:** なし", card), encoding="utf-8")
    _write_patch(tmp_path)
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode != 0
    assert "excerpt" in r.stderr and "F01" in r.stderr
