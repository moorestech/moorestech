# digest.md のパースと必須キー検査を検証する
# Verify digest.md parsing and required-key validation
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.parse import DigestError, parse_document, parse_yaml_block

MINIMAL = """# PR #1 テストPR

```yaml
pr: 1
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


def test_parse_yaml_block_subset():
    got = parse_yaml_block("pr: 1\nfiles: [a.cs:1, b.cs:2]\nopts:\n  - x\n  - y\n")
    assert got == {"pr": "1", "files": ["a.cs:1", "b.cs:2"], "opts": ["x", "y"]}


def test_parse_document_reads_meta_and_findings():
    doc = parse_document(MINIMAL)
    assert doc.meta["pr"] == "1"
    assert doc.meta["verdict"] == "reject"
    assert len(doc.findings) == 1
    f = doc.findings[0]
    assert f.slug == "first"
    assert f.category == "critical"
    assert f.files == ["a/b/C.cs:10"]
    assert f.options == ["直す"]
    assert "PR側の主張" in f.body_md
    assert doc.notes["criticals"] == "1件ある。"
    assert "ユーザー裁定なし" in doc.ledger_md
    assert "Warning全件" in doc.appendix_md


def test_missing_reserved_section_is_error():
    text = MINIMAL.replace("# 判断台帳\n\n- ユーザー裁定なし\n", "")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "判断台帳" in str(e.value)


def test_missing_required_key_is_error():
    text = MINIMAL.replace("severity: critical\n", "")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "severity" in str(e.value)


def test_unknown_verdict_is_error():
    text = MINIMAL.replace("verdict: reject", "verdict: maybe")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "verdict" in str(e.value)


def test_recommended_key_is_rejected():
    text = MINIMAL.replace("options:\n  - 直す", "recommended: true\noptions:\n  - 直す")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "recommended" in str(e.value)
