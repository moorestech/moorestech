# .claude/skills/moores-code-review/tests/test_dead_member_gate.py
# DeadMemberAuditのreport.mdをrule別に取りこぼさず拾えることを固定する回帰テスト。
# リスト3/4/5を追加したとき、既存リスト1/2のパースを壊さないことも同時に見る。
# Regression tests: every report list must be parsed under the right rule, without
# breaking the list-1/2 parsing that predates lists 3-5.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import sys
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))
import dead_member_gate  # noqa: E402

CHANGED = "moorestech_client/Assets/Scripts/Client.Game/Sample.cs"
OTHER = "moorestech_client/Assets/Scripts/Client.Game/Untouched.cs"

REPORT = f"""# 死にpublicメンバー監査レポート

## サマリ

| 項目 | 件数 |
| --- | ---: |
| リスト1: 参照0 | 1 |

## リスト1: 参照0（どのアセンブリからも呼ばれていない）

| アセンブリ | メンバー | 宣言型 | 宣言場所 |
| --- | --- | --- | --- |
| Client.Game | `Sample.Dead` — `Dead()` | `Client.Game.Sample` | `{CHANGED}:10` |
| Client.Game | `Untouched.Dead` — `Dead()` | `Client.Game.Untouched` | `{OTHER}:10` |

## リスト2: テスト/デバッグ/エディタ/デフォルト参照のみ

| アセンブリ | メンバー | 宣言型 | 宣言場所 | 参照元 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.ForTest` — `ForTest()` | `Client.Game.Sample` | `{CHANGED}:20` | Client.Tests(Test):3 |

## リスト3-A: 公開範囲過剰（private候補・全参照が宣言型の中だけ）

| アセンブリ | メンバー | 宣言型 | 宣言場所 | 参照元 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.Helper` — `Helper(Int32)` | `Client.Game.Sample` | `{CHANGED}:30` | Client.Game(Production):1 |

## リスト3-B: 公開範囲過剰（internal候補・全参照が宣言アセンブリの中だけ）

### 宣言型ごとの集約（上位1型）

| 宣言型 | 候補数 |
| --- | ---: |
| `Client.Game.Sample` | 1 |

<details><summary>全1件</summary>

| アセンブリ | メンバー | 宣言型 | 宣言場所 | 参照元 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.Shared` — `Shared()` | `Client.Game.Sample` | `{CHANGED}:40` | Client.Game(Production):2 |

</details>

## リスト4-A: サーバー配置ミス（server宣言・client側からしか使われていない）

| アセンブリ | 型 | 宣言場所 | server実装 | client参照元 |
| --- | --- | --- | --- | --- |
| Game.Map | `Game.Map.Sample` | `{CHANGED}:50` | - | Client.Game:2 |

## リスト4-B: サーバー配置ミス（DI登録のみ・server側に解決者なし）

| アセンブリ | 型 | 宣言場所 | client参照元 | server実装 | server登録元 |
| --- | --- | --- | --- | --- | --- |
| Server.Event | `Server.Event.Sample` | `{CHANGED}:60` | - | - | Server.Boot:1 |

## リスト5-A: CancellationToken未伝搬（呼び出し元はトークンを持っている）

| アセンブリ | 呼び出し | 宣言型 | 宣言場所 | 形 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.Load → AddressableLoader.LoadAsync` | `Client.Game.Sample` | `{CHANGED}:70` | default(CancellationToken) / 呼び出し元: CT引数あり |

## リスト5-B: async void（Unityイベント関数は除外済み）

| アセンブリ | メソッド | 宣言型 | 宣言場所 | 形 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.FireAndForget` | `Client.Game.Sample` | `{CHANGED}:80` | async void（UniTaskVoid + .Forget() へ） |

## リスト5-C: CancellationTokenSource作りっぱなし（Cancel/Disposeが無い）

| アセンブリ | フィールド | 宣言型 | 宣言場所 | 形 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample._cts` | `Client.Game.Sample` | `{CHANGED}:90` | Cancel/Disposeがどのアセンブリにも無い |

## リスト6-A: 単一呼び出し元privateヘルパ（呼び出し元の `#region Internal` ローカル関数へ畳む候補）

| アセンブリ | メソッド | 宣言型 | 宣言場所 | 唯一の呼び出し元 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.BuildRow` — `BuildRow(Int32)` | `Client.Game.Sample` | `{CHANGED}:100` | `Sample.Render` |

## リスト6-B: 参照0privateメソッド（同一型内のどこからも呼ばれていない）

| アセンブリ | メソッド | 宣言型 | 宣言場所 | 呼び出し元 |
| --- | --- | --- | --- | --- |
| Client.Game | `Sample.Orphan` — `Orphan()` | `Client.Game.Sample` | `{CHANGED}:110` | IL上に呼び出し元なし |

## 注意

- UnityEventのシーン/Prefab配線（`m_MethodName`による文字列参照）
"""


class CollectCandidatesTest(unittest.TestCase):
    def setUp(self):
        self.candidates = dead_member_gate.collect_candidates(REPORT, {CHANGED})

    def test_every_list_is_collected_under_its_own_rule(self):
        # リスト1〜6の全種が、それぞれのruleで1件ずつ拾えること
        # Each of lists 1-6 must yield exactly one candidate under its own rule
        by_rule = {}
        for candidate in self.candidates:
            by_rule.setdefault(candidate["rule"], []).append(candidate)
        self.assertEqual(sorted(by_rule), sorted([
            "dead-member-unused", "dead-member-nonproduction",
            "dead-member-overpublic-private", "dead-member-overpublic-internal",
            "placement-mismatch", "placement-registration-only",
            "ct-not-passed", "ct-async-void", "cts-not-released",
            "single-caller-helper", "dead-private-member"]))
        for rule, rows in by_rule.items():
            self.assertEqual(len(rows), 1, f"{rule} が1件ではない")

    def test_sublists_are_not_swallowed_by_the_parent_prefix(self):
        # 見出しは前方一致なので、6-A/6-Bのような枝分かれが片方に吸われないこと
        # Headings match by prefix, so a split like 6-A/6-B must not collapse into one rule
        by_rule = {}
        for candidate in self.candidates:
            by_rule.setdefault(candidate["rule"], []).append(candidate["member"])
        self.assertIn("Sample.Orphan", str(by_rule["dead-private-member"]))
        self.assertIn("Sample.BuildRow", str(by_rule["single-caller-helper"]))

    def test_patch_scope_filters_untouched_files(self):
        # patch外のファイルの行は拾わない（既存の絞り込みが壊れていないこと）
        # Rows from files outside the patch must be dropped
        self.assertTrue(all(candidate["file"] == CHANGED for candidate in self.candidates))

    def test_lines_and_members_are_extracted(self):
        by_rule = {candidate["rule"]: candidate for candidate in self.candidates}
        self.assertEqual(by_rule["dead-member-unused"]["line"], 10)
        self.assertEqual(by_rule["placement-mismatch"]["member"], "Game.Map.Sample")
        self.assertEqual(by_rule["cts-not-released"]["member"], "Sample._cts")

    def test_detail_carries_adjudication_context(self):
        # 最終列（参照元・形）がdetailとして裁定へ渡ること
        # The last cell (referencers or shape) must reach the verifier as detail
        by_rule = {candidate["rule"]: candidate for candidate in self.candidates}
        self.assertIn("CT引数あり", by_rule["ct-not-passed"]["detail"])
        self.assertIn("Server.Boot:1", by_rule["placement-registration-only"]["detail"])
        # 単一呼び出し元ヘルパは「唯一の呼び出し元」が裁定の文脈になる
        # For a single-caller helper the only caller is the adjudication context
        self.assertIn("Sample.Render", by_rule["single-caller-helper"]["detail"])
        self.assertEqual(by_rule["dead-member-nonproduction"]["referencers"], "Client.Tests(Test):3")
        # リスト1は最終列が宣言場所そのものなので、detailは空になる
        # List 1 ends at the location cell, so its detail stays empty
        self.assertEqual(by_rule["dead-member-unused"]["detail"], "")

    def test_aggregation_and_summary_rows_are_not_candidates(self):
        # 集約表・サマリ表の行は宣言場所を持たないので候補にならない
        # Aggregation and summary rows carry no location, so they never become candidates
        self.assertNotIn("Client.Game.Sample", [candidate["member"] for candidate in self.candidates])

    def test_sections_outside_the_lists_are_ignored(self):
        # 「注意」節など、リスト以外の見出しの下は拾わない
        # Headings outside the lists (such as the caveats) must be ignored
        report = REPORT.replace(f"| Client.Game | `Sample._cts` | `Client.Game.Sample` | `{CHANGED}:90` |",
                                f"| noise | `x` | `y` | `{CHANGED}:90` |")
        rules = {candidate["rule"] for candidate in dead_member_gate.collect_candidates(report, {CHANGED})}
        self.assertNotIn(None, rules)


if __name__ == "__main__":
    unittest.main()
