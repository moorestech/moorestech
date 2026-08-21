# .claude/skills/moores-code-review/tests/test_check_all.py
# 統一窓口check_all.pyの回帰テスト。verifier起動計画の4種全網羅と、
# 窓口出力が単体スクリプト出力と一致すること（取りこぼしゼロ）を固定する。
# Regression tests for the unified gateway: full verifier-plan coverage and
# equivalence with standalone script outputs.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))
import check_all  # noqa: E402

TRIGGER_BODY = """
using System;

namespace Game.SomeDomain
{
    public class GatewayTrigger
    {
        public void Run(int a, int b)
        {
            if (a>=b) return;

            var start = DateTime.Now;
            var elapsed = (DateTime.Now - start).TotalSeconds;

            // WebSocket受信の隔離のため / Isolate WebSocket receive boundary
            try
            {
                Receive();
            }
            catch (Exception e)
            {
                Log(e);
            }
        }
    }
}
""".strip("\n")


def trigger_patch() -> str:
    path = "moorestech_server/Assets/Scripts/Game.SomeDomain/GatewayTrigger.cs"
    lines = TRIGGER_BODY.splitlines()
    return (f"diff --git a/{path} b/{path}\nnew file mode 100644\n--- /dev/null\n+++ b/{path}\n"
            f"@@ -0,0 +1,{len(lines)} @@\n") + "".join(f"+{l}\n" for l in lines)


class VerifierPlanTest(unittest.TestCase):
    def test_all_four_kinds_are_planned(self):
        # 全種の候補がverifier起動計画に載ること / Every candidate kind must be planned
        result = {
            "deterministic": {"candidates": {
                "comparison_operator": [{}], "try_catch_boundary": [{}],
                "server_elapsed_time": [{}, {}]}},
            "dead_member": {"status": "ok", "candidates": [{}]},
            "ts_dead_code": {"status": "ok", "candidates": [{}, {}]},
        }
        plans = {p["candidate_kind"]: p["count"] for p in check_all.plan_verifiers(result)}
        self.assertEqual(plans, {"comparison_operator": 1, "try_catch_boundary": 1,
                                 "server_elapsed_time": 2, "dead_member": 1,
                                 "ts_dead_code": 2})

    def test_zero_candidates_launch_nothing(self):
        result = {"deterministic": {"candidates": {}},
                  "dead_member": {"status": "skipped", "candidates": []}}
        self.assertEqual(check_all.plan_verifiers(result), [])


class GatewayEquivalenceTest(unittest.TestCase):
    def test_gateway_matches_standalone_outputs(self):
        # 窓口のdeterministic節が単体実行と完全一致すること（取りこぼし検知）
        # The gateway's deterministic node must equal the standalone run
        with tempfile.NamedTemporaryFile("w", suffix=".diff", delete=False) as f:
            f.write(trigger_patch())
            patch = f.name
        gw = json.loads(subprocess.run(
            [sys.executable, str(SCRIPTS / "check_all.py"), patch,
             "--repo-root", ".", "--skip-dead-member"],
            capture_output=True, text=True, timeout=300).stdout)
        det = json.loads(subprocess.run(
            [sys.executable, str(SCRIPTS / "deterministic_checks.py"), patch,
             "--repo-root", "."],
            capture_output=True, text=True, timeout=300).stdout)
        self.assertEqual(gw["deterministic"], det)
        self.assertEqual(gw["summary"]["errors"], [])
        planned = {p["candidate_kind"] for p in gw["verifiers_to_launch"]}
        self.assertEqual(planned, {"comparison_operator", "try_catch_boundary",
                                   "server_elapsed_time"})


if __name__ == "__main__":
    unittest.main()
