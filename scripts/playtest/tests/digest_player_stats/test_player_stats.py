"""人数・時間・表示の境界を検証する。
Tests player, time and rendering boundaries."""
import unittest

import digest_collect as collect
from digest_support.progress import aggregate_progress
from digest_support.sections import format_progress


def row(sid, seconds):
    return dict(steamId=sid, tester=f"Tester（SteamID {sid}）", playSeconds=seconds,
                research=0, reached=0, lastEvent="none", lastUiState="x", endReason="quit")


class PlayerStatsTest(unittest.TestCase):
    def test_empty_zero_repeated_and_multiple_players(self):
        # 人数と平均の分母をセッション数から区別する
        # Distinguish the player denominator from the session count.
        cases = [([], 0, 0, None), ([("A", 0)], 1, 0, 0),
                 ([("A", 600), ("A", 1200)], 1, 1800, 1800),
                 ([("A", 600), ("A", 1200), ("B", 3600)], 2, 5400, 2700)]
        for values, players, total, mean in cases:
            with self.subTest(values=values):
                records = [row(sid, seconds) for sid, seconds in values]
                agg = aggregate_progress(records)
                self.assertEqual((agg["testers"], agg["sessions"]), (players, len(values)))
                self.assertEqual(agg["playTime"], dict(knownSeconds=total,
                                 knownSessions=len(values), missingSessions=0))
                self.assertEqual(agg["meanPlaySeconds"], mean)
                self.assertEqual(agg["unknownIdSessions"], 0)
                lines = format_progress(agg, {}, records)
                self.assertIn(f"- 合計プレイ時間 {total / 60:.1f} 分", lines)
                value = "対象なし" if mean is None else f"{mean / 60:.1f} 分"
                self.assertIn("- 1人あたり平均プレイ時間 " + value, lines)
        agg = aggregate_progress([row("A", 600), row("A", 1200), row("B", 3600)])
        self.assertEqual(agg["players"]["A"]["playTime"]["knownSeconds"], 1800)
        self.assertEqual(agg["players"]["B"]["playTime"]["knownSeconds"], 3600)

    def test_missing_time_and_unknown_ids(self):
        # 既知分・全欠損・未帰属を本文まで区別する
        # Distinguish partial, wholly missing and unattributed time in rendered text.
        cases = [
            ([("A", 600), ("A", None), ("B", 1200)], 2, 1800, 2, 1, 0,
             "既知分 30.0 分（一部不明）", "時間欠損",
             ["- Tester（SteamID A）: 既知分 10.0 分（一部不明）", "- Tester（SteamID B）: 20.0 分"]),
            ([("A", None), ("B", None)], 2, 0, 0, 2, 0, "不明", "時間欠損",
             ["- Tester（SteamID A）: 不明", "- Tester（SteamID B）: 不明"]),
            ([("", 60), ("A", 120)], 1, 180, 2, 0, 1, "3.0 分", "ID不明",
             ["- Tester（SteamID A）: 2.0 分", "- ID不明分: 1.0 分"]),
            ([("", 60)], 0, 60, 1, 0, 1, "1.0 分", "ID不明", ["- ID不明分: 1.0 分"]),
            ([("", None)], 0, 0, 0, 1, 1, "不明", "時間欠損/ID不明", ["- ID不明分: 不明"]),
            ([("A", None)], 1, 0, 0, 1, 0, "不明", "時間欠損", ["- Tester（SteamID A）: 不明"]),
        ]
        for values, count, seconds, known, missing, unknown, total, reason, detail in cases:
            with self.subTest(values=values):
                records = [row(sid, value) for sid, value in values]
                agg = aggregate_progress(records)
                self.assertEqual((agg["testers"], agg["sessions"]), (count, len(values)))
                self.assertEqual(agg["playTime"], dict(knownSeconds=seconds,
                                 knownSessions=known, missingSessions=missing))
                self.assertEqual(agg["playSecondsMissing"], missing)
                self.assertEqual(agg["unknownIdSessions"], unknown)
                self.assertIsNone(agg["meanPlaySeconds"])
                self.assertNotIn("", agg["players"])
                unassigned = [value for sid, value in values if not sid]
                self.assertEqual(agg["unattributedPlayTime"], {
                    "knownSeconds": sum(value for value in unassigned if value is not None),
                    "knownSessions": sum(value is not None for value in unassigned),
                    "missingSessions": sum(value is None for value in unassigned)})
                lines = format_progress(agg, {}, records)
                self.assertIn("- 合計プレイ時間 " + total, lines)
                self.assertIn(f"- 1人あたり平均プレイ時間 不明（{reason}）", lines)
                for expected in detail:
                    self.assertIn(expected, lines)
                self.assertNotIn("Tester（SteamID ）", "\n".join(lines))
                if missing:
                    warning = f"- ⚠ playSeconds が欠落したセッション {missing}件（欠損を含むため平均は不明）"
                    self.assertIn(warning, lines)
                    self.assertLess(lines.index(warning), lines.index("## 進行記録"))
                if unknown:
                    warning = f"- ⚠ ID不明のセッション {unknown}件（帰属不明のため平均は不明）"
                    self.assertIn(warning, lines)
                    self.assertLess(lines.index(warning), lines.index("## 進行記録"))
                if not known:
                    self.assertNotIn("既知分", "\n".join(lines))

    def test_time_over_twenty_four_hours(self):
        records = [row("A", 90000)]
        lines = format_progress(aggregate_progress(records), {}, records)
        for expected in ("- 合計プレイ時間 1500.0 分", "- 1人あたり平均プレイ時間 1500.0 分",
                         "- Tester（SteamID A）: 1500.0 分"):
            self.assertIn(expected, lines)

    def test_same_name_keeps_ids_separate_and_last_label_wins(self):
        records = [row("A", 600), row("B", 1200), dict(row("A", 600), tester="Updated（SteamID A）")]
        agg = aggregate_progress(records)
        self.assertEqual(list(agg["players"]), ["A", "B"])
        self.assertEqual(agg["testers"], 2)
        self.assertEqual(agg["meanPlaySeconds"], 1200)
        lines = format_progress(agg, {}, records)
        self.assertIn("- Updated（SteamID A）: 20.0 分", lines)
        self.assertNotIn("- Tester（SteamID A）: 10.0 分", lines)
        self.assertIn("- Tester（SteamID B）: 20.0 分", lines)

    def test_resolved_id_is_shared_by_label_and_aggregation(self):
        record = dict(steamId="B", playSeconds=60, endReason="quit", reachedChallenges=[],
                      completedResearch=[], lastUiState="x", events=[])
        for meta_id, record_id, expected in (("A", "B", "A"), ("", "B", "B"), ("", "", "")):
            with self.subTest(meta=meta_id, record=record_id):
                value = collect.flatten_progress_record(dict(record, steamId=record_id),
                            {"meta": {"steamId": meta_id, "steamPersonaName": "Tester"}})
                self.assertEqual(value["steamId"], expected)
                self.assertEqual(value["tester"], f"Tester（SteamID {expected}）")
                agg = aggregate_progress([value])
                self.assertEqual(agg["testers"], int(bool(expected)))
                self.assertEqual(agg["unknownIdSessions"], int(not expected))
