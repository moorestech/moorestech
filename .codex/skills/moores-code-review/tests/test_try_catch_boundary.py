# .claude/skills/moores-code-review/tests/test_try_catch_boundary.py
# try-catch 較正（PR1095 見逃し Q6）の回帰テスト
# Regression test for the try-catch calibration (PR1095 missed finding Q6)
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
# 標準ライブラリだけで動く（この環境には pytest が入っていない）
# Runs on the standard library alone (pytest is not installed in this environment)
import sys
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))

import checks_moores  # noqa: E402
import checks_static  # noqa: E402
from patch_util import parse_patch  # noqa: E402


def _patch(body: str, path: str = "moorestech_client/Assets/Scripts/Sample.cs") -> str:
    return (
        f"diff --git a/{path} b/{path}\n"
        f"--- a/{path}\n"
        f"+++ b/{path}\n"
        "@@ -1,0 +1,20 @@\n"
    ) + "".join(f"+{line}\n" for line in body.strip("\n").splitlines())


NO_COMMENT = _patch(
    """
        public void Load()
        {
            try
            {
                _value = _table[key];
            }
            catch (Exception e)
            {
                _value = null;
            }
        }
"""
)

# 根拠コメントはあるが、許可された境界3種のどれも主張していない
# Has a rationale comment, but claims none of the 3 permitted boundaries
UNRELATED_COMMENT = _patch(
    """
        public void Load()
        {
            // 念のため例外を握って安全側に倒す
            // Swallow exceptions just in case, to stay on the safe side
            try
            {
                _value = _table[key];
            }
            catch (Exception e)
            {
                _value = null;
            }
        }
"""
)

# 許可リストの境界（ネットワーク送受信）を主張している
# Claims one of the allowlisted boundaries (network send/receive)
NETWORK_COMMENT = _patch(
    """
        public void Receive()
        {
            // WebSocket の受信は外部境界なので隔離する
            // WebSocket receive is an external boundary, so isolate it
            try
            {
                _socket.Receive(buffer);
            }
            catch (Exception e)
            {
                return ReceiveResult.Disconnected;
            }
        }
"""
)


# 根拠コメントは try の直前にあり、try 本体が長い（catch 起点の窓では届かない）
# The rationale sits above `try` and the try body is long (out of reach for a catch-anchored window)
LONG_TRY_WITH_BOUNDARY_COMMENT = _patch(
    """
        public void Receive()
        {
            // ソケット受信は外部境界なので隔離する
            // Socket receive is an external boundary, so isolate it
            try
            {
                var a = 1;
                var b = 2;
                var c = 3;
                var d = 4;
                var e1 = 5;
                var f1 = 6;
                var g = 7;
                var h = 8;
                var i = 9;
                var j = 10;
                _socket.Receive(buffer);
            }
            catch (Exception e)
            {
                return ReceiveResult.Disconnected;
            }
        }
"""
)


def _confirmed_rules(patch_text: str) -> list[str]:
    files = parse_patch(patch_text)
    return [f["rule"] for f in checks_static.run(files, Path("/nonexistent"))]


def _candidates(patch_text: str) -> list[dict]:
    return checks_static.try_catch_boundary(parse_patch(patch_text))


class TryCatchBoundaryTest(unittest.TestCase):
    def test_no_comment_stays_confirmed(self):
        self.assertIn("try-catch-forbidden", _confirmed_rules(NO_COMMENT))
        self.assertEqual([], _candidates(NO_COMMENT))

    def test_comment_without_boundary_claim_stays_confirmed(self):
        # 回帰の芯: 根拠コメントが実在するだけでは免除されない（PR1095 の較正ミス）
        # Core regression: a rationale comment alone must never grant an exemption (PR1095 miscalibration)
        self.assertIn("try-catch-forbidden", _confirmed_rules(UNRELATED_COMMENT))
        self.assertEqual([], _candidates(UNRELATED_COMMENT))

    def test_boundary_claim_moves_to_candidate_not_exemption(self):
        # 許可リストを主張するものだけ candidate へ降り、confirmed からは外れる
        # Only allowlist-claiming ones drop to candidate and leave the confirmed set
        self.assertNotIn("try-catch-forbidden", _confirmed_rules(NETWORK_COMMENT))
        candidates = _candidates(NETWORK_COMMENT)
        self.assertEqual(1, len(candidates))
        self.assertEqual(["network-io"], candidates[0]["boundary_claim"])
        self.assertIn("WebSocket", candidates[0]["comment"])

    def test_rationale_above_a_long_try_is_still_found(self):
        # 根拠コメントの探索は catch ではなく try を起点にする（長い try で窓落ちしない）
        # The rationale search anchors on `try`, not `catch`, so a long body cannot push it out of the window
        candidates = _candidates(LONG_TRY_WITH_BOUNDARY_COMMENT)
        self.assertEqual(1, len(candidates))
        self.assertEqual(["network-io"], candidates[0]["boundary_claim"])

    def test_candidate_is_not_silently_dropped(self):
        # candidate は「消えた」ではなく「裁定行き」。件数が0なら verifier が起動されず免除と同義になる
        # A candidate means "goes to adjudication", not "gone"; zero candidates would silently equal an exemption
        self.assertTrue(_candidates(NETWORK_COMMENT))


SERVER_GAME = "moorestech_server/Assets/Scripts/Game.Map/MiningService.cs"
SERVER_GAME_TEST = "moorestech_server/Assets/Scripts/Game.Map/Tests/MiningServiceTest.cs"
CLIENT = "moorestech_client/Assets/Scripts/Client.Game/View.cs"

REALTIME_BODY = """
        public void Attack()
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = now - _last;
        }
"""

TICK_BODY = """
        public void Attack()
        {
            _elapsedTicks += 1;
            if (_elapsedTicks < GameUpdater.SecondsToTicks(AttackSpeed)) return;
        }
"""


def _moores_rules(patch_text: str) -> list[str]:
    return [f["rule"] for f in checks_moores.run_confirmed(parse_patch(patch_text))]


class ServerRealtimeApiTest(unittest.TestCase):
    def test_realtime_api_in_server_game_is_confirmed(self):
        self.assertIn("server-realtime-api", _moores_rules(_patch(REALTIME_BODY, SERVER_GAME)))

    def test_tick_accumulation_is_clean(self):
        self.assertNotIn("server-realtime-api", _moores_rules(_patch(TICK_BODY, SERVER_GAME)))

    def test_server_test_code_is_exempt(self):
        self.assertNotIn("server-realtime-api", _moores_rules(_patch(REALTIME_BODY, SERVER_GAME_TEST)))

    def test_client_code_is_out_of_scope(self):
        # クライアント表示は実時間で動いてよい。規約はサーバのゲームロジック限定
        # Client-side presentation may use real time; the rule is scoped to server game logic
        self.assertNotIn("server-realtime-api", _moores_rules(_patch(REALTIME_BODY, CLIENT)))

    def test_datetime_is_never_confirmed(self):
        # DateTimeは用途で正否が割れるのでconfirmedにしない（セーブの実世界時刻記録は正当）
        # DateTime is never confirmed: recording real-world timestamps in save data is legitimate
        body = ("\n        _worldCreationDateTime = DateTime.UtcNow;\n"
                "        var played = DateTime.UtcNow - _sessionStart;\n"
                "        return played.TotalSeconds;\n")
        self.assertNotIn("server-realtime-api", _moores_rules(_patch(body, SERVER_GAME)))

    def test_datetime_elapsed_gating_becomes_candidate(self):
        # PR1095当時のクールダウン実装（DateTime差分でゲート）が候補として拾われること
        # The PR1095-era cooldown (gating on a DateTime delta) must be picked up as a candidate
        body = ("\n        private readonly Dictionary<int, DateTime> _lastAttackTimes = new();\n"
                "        public bool TryAttack(int playerId, float attackSpeed)\n"
                "        {\n"
                "            var now = DateTime.UtcNow;\n"
                "            if (_lastAttackTimes.TryGetValue(playerId, out var last)"
                " && (now - last).TotalSeconds < attackSpeed) return false;\n"
                "            _lastAttackTimes[playerId] = now;\n"
                "            return true;\n"
                "        }\n")
        candidates = checks_moores.server_elapsed_time(parse_patch(_patch(body, SERVER_GAME)))
        self.assertEqual(1, len(candidates))
        self.assertEqual("server-elapsed-time", candidates[0]["rule"])

    def test_datetime_without_elapsed_marker_is_not_a_candidate(self):
        # 経過計測の痕跡が無い純粋な日時記録は候補にもしない（verifier起動を無駄に増やさない）
        # A pure timestamp record with no elapsed marker is not even a candidate
        body = "\n        CreatedAt = DateTime.UtcNow.ToString(\"O\");\n"
        self.assertEqual([], checks_moores.server_elapsed_time(parse_patch(_patch(body, SERVER_GAME))))

    def test_client_datetime_is_out_of_candidate_scope(self):
        body = ("\n        var now = DateTime.UtcNow;\n"
                "        var d = (now - _start).TotalSeconds;\n")
        self.assertEqual([], checks_moores.server_elapsed_time(parse_patch(_patch(body, CLIENT))))

    def test_comment_mentioning_stopwatch_is_not_a_violation(self):
        # コメント内の言及で誤検知しない（cs_lex がコメントを潰す）
        # A mention inside a comment must not trip the check (cs_lex blanks comments)
        body = "\n        // Stopwatch は使わずティック加算にする\n        _elapsedTicks += 1;\n"
        self.assertNotIn("server-realtime-api", _moores_rules(_patch(body, SERVER_GAME)))


if __name__ == "__main__":
    unittest.main()
