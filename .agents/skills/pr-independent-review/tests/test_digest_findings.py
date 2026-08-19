# id採番規則と、先頭optionが必ず推奨になることを検証する
# Verify the id numbering rule and that the first option is always the recommended one
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.errors import DigestError
from digest_md.findings import assign_ids, build_findings
from digest_md.parse import Document, Finding


def _doc(findings):
    meta = {"pr": "1", "head": "0" * 40, "verdict": "reject", "verdict_line": "x",
            "date": "2026-08-18", "generated_at": "2026-08-18T00:00:00+09:00", "title": "t"}
    return Document(meta=meta, notes={}, ledger_md="", appendix_md="", findings=findings)


def _f(slug, severity, path, options=("直す",), category="critical"):
    return Finding(slug=slug, title=slug, category=category, severity=severity,
                   summary="s", files=[path], body_md="", options=list(options))


def test_assign_ids_orders_by_severity_then_path_then_line():
    doc = _doc([
        _f("b", "medium", "z/A.cs:5"),
        _f("a", "critical", "b/B.cs:20"),
        _f("c", "critical", "b/B.cs:3"),
        _f("d", "critical", "a/C.cs:1"),
    ])
    refs = assign_ids(doc)
    assert refs == {"d": "F01", "c": "F02", "a": "F03", "b": "F04"}


def test_build_findings_makes_first_option_recommended():
    doc = _doc([_f("a", "critical", "a/A.cs:1", options=("直す", "戻す", "消す"))])
    assign_ids(doc)
    out = build_findings(doc)
    opts = out["findings"][0]["options"]
    assert [o["key"] for o in opts] == ["A", "B", "C"]
    assert opts[0]["recommended"] is True
    assert all("recommended" not in o for o in opts[1:])


def test_build_findings_every_non_suppressed_has_exactly_one_recommended():
    doc = _doc([_f("a", "critical", "a/A.cs:1"), _f("b", "low", "b/B.cs:2", options=("x", "y"))])
    assign_ids(doc)
    out = build_findings(doc)
    for f in out["findings"]:
        assert len([o for o in f["options"] if o.get("recommended")]) == 1


def test_build_findings_suppressed_has_no_options():
    f = _f("s", "high", "a/A.cs:1", options=())
    f.suppressed = True
    f.suppress_reason = "ADRで免責"
    doc = _doc([f])
    assign_ids(doc)
    out = build_findings(doc)
    assert out["findings"][0]["options"] == []
    assert out["findings"][0]["suppress_reason"] == "ADRで免責"


def _finding_with(body_md: str) -> Finding:
    f = Finding(slug="s", title="t", category="critical", severity="critical",
                summary="s", files=["a/B.cs:1"], body_md=body_md, options=["直す"])
    f.id = "F01"
    return f


def test_excerpt_drops_deleted_lines():
    # R9: excerptはPR後の現行コードだけを持つ（pr-adjudicated-applyの入力契約）
    # R9: the excerpt carries only post-PR code, which is pr-adjudicated-apply's input contract
    from digest_md.findings import _excerpt
    assert _excerpt(_finding_with("```code-card\n-37|old();\n+38|new();\n```")) == "new();"


def test_excerpt_rejects_a_deletion_only_card():
    # 削除行だけだと excerpt が空になり、applyが修正対象を突き止められなくなる
    # A deletion-only card empties the excerpt and leaves apply with no anchor on the code
    from digest_md.findings import _excerpt
    with pytest.raises(DigestError):
        _excerpt(_finding_with("```code-card\n-37|old();\n```"))
