import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts" / "requirements"))
from input_bundle import bundle, prompt, snapshot, units


class RequirementInputTests(unittest.TestCase):
    def test_free_text_is_not_summarized(self):
        for raw in ("表示を直す。\n\n説明も残す。\n", "# 依頼\n一般見出しも全文の一部\n"):
            self.assertEqual(units(raw)[0]["text"], raw)

    def test_every_goal_alias_splits_and_excludes_non_goal(self):
        for heading in ("## 目指す", "## 目指す（ゴール）", "## ゴール"):
            raw = f"{heading}\n- A [agent前提]\n  継続\n- B\n## 非目標\n- C\n"
            rows = units(raw)
            self.assertEqual(len(rows), 2)
            self.assertEqual("".join(row["text"] for row in rows), "- A [agent前提]\n  継続\n- B\n")
            self.assertEqual((rows[0]["start"], rows[0]["end"]), (2, 3))

    def test_empty_ambiguous_and_missing_goals_fail(self):
        invalid = ("", "## ゴール\n\n## 目指さない\n- C",
                   *(f"{heading}\n- C" for heading in ("## 目指さない", "## 非目標", "## 制約", "## トレードオフ")),
                   "## 目指す\n- A\n## ゴール\n- B\n")
        for raw in invalid:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                units(raw)

    def test_fences_do_not_create_boundaries(self):
        raw = "## ゴール\n- A\n````py\n## 非目標\n- inside\n```\nstill fenced\n````\n  continued\n- B\n## 制約\n- C\n"
        rows = units(raw)
        self.assertEqual(len(rows), 2)
        self.assertIn("## 非目標\n- inside", rows[0]["text"])
        self.assertTrue(rows[1]["text"].startswith("- B\n"))
        tilde = "## ゴール\n- A\n~~~~ text\n- inside\n~~~\nstill fenced\n~~~~\n- B\n"
        self.assertEqual(len(units(tilde)), 2)

    def test_prompts_share_complete_prefix_and_preserve_units(self):
        raw = "## ゴール\n- A\n  続き\n- B\n"
        rows = units(raw)
        data = {"repo": "/tmp/source", "procedure": "手順全文", "context": raw, "patch": "差分全文"}
        texts = [prompt(data, row, Path(f"/tmp/result-{i}.md")) for i, row in enumerate(rows)]
        marker = "\n\n担当要求:"
        self.assertEqual(texts[0].split(marker)[0], texts[1].split(marker)[0])
        self.assertIn("context行2–3)\n- A\n  続き\n", texts[0])
        for value in ("手順全文", raw, "差分全文", "/tmp/source"):
            self.assertIn(value, texts[0])

    def test_snapshot_and_bundle_fingerprint_cover_repository_state(self):
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory).resolve()
            subprocess.run(["git", "init", "-q", str(repo)], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.email", "test@example.com"], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.name", "Test"], check=True)
            tracked = repo / "consumer.txt"
            tracked.write_text("one\n", encoding="utf-8")
            subprocess.run(["git", "-C", str(repo), "add", "consumer.txt"], check=True)
            subprocess.run(["git", "-C", str(repo), "commit", "-qm", "initial"], check=True)
            clean = snapshot(repo)
            tracked.write_text("two\n", encoding="utf-8")
            dirty = snapshot(repo)
            (repo / "new.txt").write_text("new\n", encoding="utf-8")
            untracked = snapshot(repo)
            self.assertEqual(len({clean, dirty, untracked}), 3)
            first = bundle("request", "patch", "procedure", repo, "sonnet")
            (repo / "new.txt").write_text("changed\n", encoding="utf-8")
            second = bundle("request", "patch", "procedure", repo, "sonnet")
            self.assertNotEqual(first["fingerprint"], second["fingerprint"])
            with self.assertRaises(ValueError):
                snapshot(Path("relative"))
