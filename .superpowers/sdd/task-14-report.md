# Task 14 レポート: ADR-0007 vein手掘り最終レビュー是正

## ステータス

レビュー確定の8ブロッカー群を `c86a72a4a` で是正し、再レビュー4指摘を `59eb691b9fa5f352c817e2df1ba8719f575cd1c1` で解消した。広域・EditMode・ライブsmoke・最終再レビューは未実行のため、完了ゲートは保留する。

## レビュー対象

| 項目 | 値 |
|---|---|
| branch | `feature/vein-hand-mining` |
| master merge-base | `a32bd94687d50b3ba4e4c6d084b6276978e96b91` |
| review head | `003edfd77` |
| code-fix commit | `c86a72a4a` |
| report-correction head | `426466d0f` |
| re-review fix head | `59eb691b9fa5f352c817e2df1ba8719f575cd1c1` |
| final small fix head | `da3f13a9c93b3310feab9d8b619e8c9d2062ff3c` |

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

### 再レビュー是正の追加検証

- mutation RED: 完了送信を現在フォーカスへ変えた状態で `MapObjectMiningEquipmentSwitchTest` は2/3 PASS・1 FAIL。開始対象の攻撃回数が `Expected: 1 / But was: 0` となり、追加テストが誤送信を検出した。
- GREEN compile: Error 0、Warning 124。生成コード・既存コード由来で、変更ファイルの新規警告はない。
- GREEN focused: `MapObjectMiningEquipmentSwitchTest|MapVeinMasterTest` は14/14 PASS、FAIL 0、SKIP 0。

### 最終小規模是正の検証

- `InvokePrivate` をconstructor末尾の `#region Internal` ローカル関数へ移し、ForUnitTest用GUIDの根拠コメントを復元した。生成済み `.meta` は変更なし。
- compile: Error 0、Warning 0。
- focused `MapObjectMiningEquipmentSwitchTest`: 初回はDomain Reloadのため規定どおり45秒待機し、再実行で3/3 PASS、FAIL 0、SKIP 0。

## 変更領域

- Client mining FSM、mapObject/outcrop採掘対象、tutorial pinのDI・可視性ヘルパー。
- Server veinマスタ検証とREDテスト。
- 録画smokeの比較規約、レビュー対象のコメント短縮、Task 14進捗記録。

## 既知の警告・劣化

- 初回是正のコンパイルWarning 8と再レビュー是正時のWarning 124は、既存・生成コード由来であり、この是正の新規警告ではない。
- `UserSettings/UnityMcpSettings.json` はこのworktreeで実行後に`.bak`へ戻るため、各`uloop`実行前に既存`.bak`から復元した。追跡外でコミット対象外。
- Task 14レビュー後の必須広域検証はまだ行っていない。focused GREENだけでライブ挙動まで保証しない。

## Beads

`bd create` は共有埋め込みDolt DBに `issue_prefix` が無いため失敗した。bootstrapはDB名衝突、再初期化はhookにより承認待ちとなるため、タスク追跡DBを変更していない。

## 残る必須検証

1. mining/map広域テストを再実行する。
2. `EditModeInPlayingTest` を再実行する。
3. ライブv8録画smokeを再実行する。
4. 最終再レビューでブロッカー・コメント規約・公開面を確認する。
