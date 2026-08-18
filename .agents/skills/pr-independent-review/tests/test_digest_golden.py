# goldenのmdから生成したHTMLが固定版と一致することを検証する（見た目の回帰検知）
# Verify the HTML built from the golden md matches the frozen copy (visual regression guard)
import re
import subprocess
import sys
from pathlib import Path

GOLDEN = Path(__file__).resolve().parent / "golden"
SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "digest_build.py"

# 素のfinding id（[F:slug]を経ない直書き参照）を検出する正規表現
# Detects a bare finding id written outside the [F:slug] cross-reference syntax
_BARE_FINDING_ID = re.compile(r"\bF(0[1-9]|1[01])\b")

# 127KBのバンドル本体はgoldenへ持たない。存在と規模だけ検査し、比較時は差し替える
# The 127KB bundle never lands in the golden file; presence and size are checked, then it is swapped out
_BUNDLE = re.compile(r'(?s)<script id="hljs-bundle">.*?</script>')


def _normalize(html: str) -> str:
    m = _BUNDLE.search(html)
    assert m, "hljsバンドルが出力にありません"
    assert len(m.group(0)) > 100000, "hljsバンドルが小さすぎます（取得失敗の疑い）"
    return _BUNDLE.sub('<script id="hljs-bundle">[BUNDLE]</script>', html)


def test_golden_html_is_reproduced(tmp_path):
    (tmp_path / "digest.md").write_text((GOLDEN / "pr-1155-digest.md").read_text(encoding="utf-8"), encoding="utf-8")
    (tmp_path / "patch.diff").write_text((GOLDEN / "pr-1155-patch.diff").read_text(encoding="utf-8"), encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    got = _normalize((tmp_path / "digest.html").read_text(encoding="utf-8"))
    want = (GOLDEN / "pr-1155-digest.expected.html").read_text(encoding="utf-8")
    assert got == want


def test_golden_md_has_no_bare_finding_id():
    # R7受け入れ基準: 相互参照は[F:slug]のみで書き、素のF0xをmdに残さない
    # R7 acceptance: cross-references use [F:slug] only, never a bare F0x in the md
    text = (GOLDEN / "pr-1155-digest.md").read_text(encoding="utf-8")
    hits = _BARE_FINDING_ID.findall(text)
    assert not hits, f"golden md に素のfinding id参照が残っています: {hits}"
