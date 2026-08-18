# goldenのmdから生成したHTMLが固定版と一致することを検証する（見た目の回帰検知）
# Verify the HTML built from the golden md matches the frozen copy (visual regression guard)
import subprocess
import sys
from pathlib import Path

GOLDEN = Path(__file__).resolve().parent / "golden"
SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "digest_build.py"


def test_golden_html_is_reproduced(tmp_path):
    (tmp_path / "digest.md").write_text((GOLDEN / "pr-1155-digest.md").read_text(encoding="utf-8"), encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    got = (tmp_path / "digest.html").read_text(encoding="utf-8")
    want = (GOLDEN / "pr-1155-digest.expected.html").read_text(encoding="utf-8")
    assert got == want
