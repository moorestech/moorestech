# 置換なのに削除行を書いていないカードを patch.diff との照合で検出する
# Detect cards that describe a replacement without writing the deleted lines, by cross-checking patch.diff
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.patch_guard import missing_deletion_problems
from digest_md.models import Document, Finding

PATCH_WITH_DELETION = """diff --git a/Foo.cs b/Foo.cs
--- a/Foo.cs
+++ b/Foo.cs
@@ -36,3 +36,3 @@
     void A()
-        Old(1f);
+        New(rate);
"""

PATCH_PURE_ADDITION = """diff --git a/Foo.cs b/Foo.cs
--- a/Foo.cs
+++ b/Foo.cs
@@ -36,2 +36,3 @@
     void A()
+        New(rate);
"""


def _doc(body: str) -> Document:
    f = Finding(slug="s", title="t", category="critical", severity="critical",
                summary="s", files=["Foo.cs:37"], body_md=body)
    f.id = "F01"
    return Document(meta={}, notes={}, ledger_md="", appendix_md="", findings=[f])


def test_replacement_without_deletion_line_is_reported():
    doc = _doc("```code-card\n+37|        New(rate);\n```")
    problems = missing_deletion_problems(doc, PATCH_WITH_DELETION)
    assert len(problems) == 1 and "F01" in problems[0]


def test_replacement_with_deletion_line_passes():
    doc = _doc("```code-card\n-37|        Old(1f);\n+37|        New(rate);\n```")
    assert missing_deletion_problems(doc, PATCH_WITH_DELETION) == []


def test_pure_addition_passes():
    doc = _doc("```code-card\n+37|        New(rate);\n```")
    assert missing_deletion_problems(doc, PATCH_PURE_ADDITION) == []


def test_card_without_added_lines_is_skipped():
    doc = _doc("```code-card\n 36|    void A()\n```")
    assert missing_deletion_problems(doc, PATCH_WITH_DELETION) == []


def test_unmatched_card_is_skipped():
    # patchのどのhunkとも一致しない抜粋は検証しようがないので落とさない（誤検知を作らない）
    # An excerpt matching no hunk cannot be verified, so it is never failed (no false positives)
    doc = _doc("```code-card\n+99|        Unrelated();\n```")
    assert missing_deletion_problems(doc, PATCH_WITH_DELETION) == []
