import json
import subprocess
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "review_workflow" / "25_refix_history.js"

# 台本の断片を node で読み、trackRefix の再発の扱いだけを確かめる
# Load the workflow fragment in node and check only how trackRefix handles recurrences
HARNESS = """
const fs = require('fs')
const logs = []
const log = (m) => logs.push(m)
const REPORT_SCHEMA = { properties: {}, required: [] }
eval(fs.readFileSync(process.argv[1], 'utf8') + '\\nglobalThis.trackRefix = trackRefix')
const input = JSON.parse(process.argv[2])
try {
  const out = trackRefix(input.result, input.round, input.history)
  console.log(JSON.stringify({ ok: true, out, result: input.result, history: input.history, logs }))
} catch (e) {
  console.log(JSON.stringify({ ok: false, error: e.message, logs }))
}
"""


def run(result, round_, history):
    arg = json.dumps({"result": result, "round": round_, "history": history})
    out = subprocess.run(["node", "-e", HARNESS, str(SCRIPT), arg], capture_output=True, text=True, check=True)
    return json.loads(out.stdout)


def finding(id_, severity, recurs_from):
    return {"id": id_, "severity": severity, "summary": "s", "recurs_from": recurs_from}


class TrackRefixRecurrenceTest(unittest.TestCase):
    def test_warning_recurring_from_prior_warning_is_logged_not_promoted(self):
        out = run({"critical_count": 0, "findings": [finding("w1", "Warning", "r1")]}, 2, [])
        self.assertTrue(out["ok"], out)
        self.assertEqual(out["out"], {"stuck": False, "promoted": 0})
        self.assertEqual(out["result"]["findings"][0]["severity"], "Warning")
        self.assertTrue(any("Warning→Warning" in line for line in out["logs"]))

    def test_critical_with_missing_reference_still_fails(self):
        out = run({"critical_count": 1, "findings": [finding("c1", "Critical", "r1")]}, 2, [])
        self.assertFalse(out["ok"])
        self.assertIn("再発の参照先が無い", out["error"])

    def test_warning_recurring_from_prior_critical_is_promoted_and_stuck(self):
        history = [{"round": "r1", **finding("c1", "Critical", None)}]
        out = run({"critical_count": 0, "findings": [finding("c1", "Warning", "r1")]}, 2, history)
        self.assertTrue(out["ok"], out)
        self.assertEqual(out["out"], {"stuck": True, "promoted": 1})
        self.assertEqual(out["result"]["findings"][0]["severity"], "Critical")


if __name__ == "__main__":
    unittest.main()
