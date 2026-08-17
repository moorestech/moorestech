# Task 14 レポート: ADR-0007 vein手掘り最終レビュー

## ステータス

Task 14 の必須 `moores-code-review` と全是正を完了した。最終判定は **Approved（Critical 0 / Important 0 / Minor 0）**。設計判断の保留と免責で消した指摘は0件である。

## レビュー対象

| 項目 | 値 |
|---|---|
| branch | `feature/vein-hand-mining` |
| master merge-base | `a32bd94687d50b3ba4e4c6d084b6276978e96b91` |
| 最終production修正 | `da3f13a9c93b3310feab9d8b619e8c9d2062ff3c` |
| 証跡可視化 | `26bf2b4845fd1a7a63c79b90b83cd928a77ee110` と本Task 14最終コミット |
| 外部master head | `094d242be9509565393efc5aad5b467bda247222` |

## 適用した是正

1. 採掘FSMに開始対象を保持し、照準変更でFocusへ戻す。完了送信も開始対象へ固定する。
2. veinマスタの非正 `attackSpeed` と同一vein内の重複 `ToolItemGuid` を拒否する。
3. `[Inject] Construct` を `Initialize` に統一し、単一呼び出しhelperをローカル関数へ移す。
4. 比較方向、不要なfloat cast、コメント長、未使用メンバーを規約へ揃える。
5. mutation testで完了時の誤送信を実際にRED化し、開始対象だけが攻撃される契約を固定する。
6. recorded smokeに本番focus一致後だけ表示するCollider輪郭を追加し、失敗時も入力・GameObject・Materialを `finally` で解除する。

## レビュー結果

- 12レンズ、17 reviewer、Fable、比較演算子verifier、DeadMemberAudit、18分割investigatorを統合した。
- 初回8ブロッカー群と再レビュー4指摘を是正し、最終再レビューは Approved（Critical 0 / Important 0 / Minor 0）。
- 外部Codex監査は10分でタイムアウトし、確定した追加指摘は無い。未実行として扱い、他の独立系統で補完した。
- DeadMemberAudit全体実行はMono.Cecilのstack overflowで縮退した。Client.Tests除外実行の33候補は個別照合し、残存blockerは0件だった。
- コメント候補18件はすべてload-bearing rationaleとして維持し、最新smokeコメント1件だけを機械短縮した。
- suppressed: 0件。

### Warning

- `MainGameStarter.cs` は既存374行で、今回差分による増加ではない。
- `ChallengeMasterUtil.cs` は既存395行で、今回差分による増加ではない。
- `MoorestechServerDIContainerGenerator.cs` は既存309行で、今回差分による増加ではない。
- `Server.Protocol/PacketResponse` はmasterと同じ51ファイルで、branch-neutralな既存配置である。

### Info

- `VanillaSchema/map.yml` の `optional: true` は、`none` unionで値が存在しないこと自体を表す裁定済みの正当なabsenceである。
- Unity起動時のBush 2ログは既存BrokenPrefabAsset、小石5ログは生成完了前にpinが検索する既存の一時ログで、ADR-0007差分由来ではない。scenario計測区間の `ErrorLogs` は0件。

## 検証

| 対象 | 結果 |
|---|---|
| 最終compile | Error 0 / Warning 0 |
| `MapObjectMiningEquipmentSwitchTest\|MapVeinMasterTest` | 14/14 PASS |
| mining/map広域regex | 137/137 PASS |
| `EditModeInPlayingTest` | 16/16 PASS |
| mutation RED | 2/3 PASS・開始対象assert 1件が意図どおりFAIL |
| mutation復元後GREEN | 14/14 PASS |
| cleanup修正後 recorded smoke | `PlaytestResults/20260805_024138/vein-hand-mining-smoke`、28/28 PASS、Addressables 11、露頭1772、ErrorLogs 0 |

recorded smokeでは実際のLMB保持と進捗、正面・45度の本番focus、`va:mining`応答による石x1増加を録画した。最終コミット後にも同一シナリオを再収録し、公開証跡はその新しい実行を使う。

## Beads

`bd create` は共有埋め込みDolt DBに `issue_prefix` が無いため失敗した。bootstrapはDB名衝突、再初期化はhookにより承認待ちとなるため、DBを変更せずSDD台帳へ記録した。
