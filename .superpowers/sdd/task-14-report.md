# Task 14 レポート: ADR-0007 vein手掘り最終レビュー是正

## ステータス

レビュー確定の8ブロッカー群を是正済み。コミット `c86a72a4a`（`fix: vein手掘り最終レビュー指摘を是正する`）で、最終ゲート前のfocused検証はGREENになった。広域・EditMode・ライブsmoke・最終再レビューは未実行のため、完了ゲートは保留する。

## レビュー対象

| 項目 | 値 |
|---|---|
| branch | `feature/vein-hand-mining` |
| review base | `f1adc3486`（`origin/feature/vein-hand-mining`） |
| review head | `003edfd77` |
| fix head | `c86a72a4a` |

## 是正したブロッカー（8群）

1. 採掘FSMは開始対象の参照を保持し、参照が変わればFocusへ戻す。完了送信も開始対象へ固定し、開始装備ID引数を廃止して `MiningToolCandidate.ToolItemId` を使用する。
2. `MapVeinMasterUtil` は `attackSpeed <= 0` と同一vein内の重複 `ToolItemGuid` を拒否する。
3. `OutcropGameObjectDatastore` と `VeinPin` の `[Inject] Construct` を `Initialize` に改名する。
4. `vein-hand-mining-smoke.cs` の3比較を定数・基準値左辺の比較へ統一する。
5. `MapObjectGameObject` と `OutcropGameObject` の不要なfloatキャストを除去する。
6. `MapObjectPin` と `VeinPin` の単一呼び出し `EnsureDesiredActiveInitialized` を呼び出し元の `#region Internal` ローカル関数へ移す。
7. `MapObjectGameObject.OnFocus` を廃止して `SetFocused` にインライン化する。
8. レビュー指定の日本語・英語コメント対を機械的に短縮し、根拠コメントの例外は維持する。

## TDD・検証

引き継ぎ時点のfocusedテストは **10/14 RED** で、対象切替1件、非正attackSpeed 2件、重複ToolItemGuid 1件が意図した失敗だった。是正後の最終結果は次のとおり。

| コマンド | 結果 |
|---|---|
| `uloop compile --project-path ./moorestech_client` | Error 0、Warning 8 |
| `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectMiningEquipmentSwitchTest|MapVeinMasterTest"` | 14/14 PASS、FAIL 0、SKIP 0 |

## 変更領域

- Client mining FSM、mapObject/outcrop採掘対象、tutorial pinのDI・可視性ヘルパー。
- Server veinマスタ検証とREDテスト。
- 録画smokeの比較規約、レビュー対象のコメント短縮、Task 14進捗記録。

## 既知の警告・劣化

- コンパイルWarning 8は既存の非網羅switch、obsolete Object検索、未使用fieldであり、この是正の新規警告ではない。
- `UserSettings/UnityMcpSettings.json` はこのworktreeで実行後に`.bak`へ戻るため、各`uloop`実行前に既存`.bak`から復元した。追跡外でコミット対象外。
- Task 14レビュー後の必須広域検証はまだ行っていない。focused GREENだけでライブ挙動まで保証しない。

## Beads

`bd create` は共有埋め込みDolt DBに `issue_prefix` が無いため失敗した。bootstrapはDB名衝突、再初期化はhookにより承認待ちとなるため、タスク追跡DBを変更していない。

## 残る必須検証

1. mining/map広域テストを再実行する。
2. `EditModeInPlayingTest` を再実行する。
3. ライブv8録画smokeを再実行する。
4. 最終再レビューでブロッカー・コメント規約・公開面を確認する。
