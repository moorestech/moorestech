# プラン5（破壊的クリーンアップ）着手用申し送り（2026-07-07）

状態: **プラン1〜4完了・本流マージ済み。プラン5は未着手**
計画書: `docs/superpowers/plans/2026-07-05-satisfactory-placement-plan5-destructive-cleanup.md`（7タスク）
経緯全体: `docs/superpowers/plans/2026-07-05-satisfactory-placement-handoff.md`

## 1. 出発点の状態

- 本流 `feature/replace-place-system` @ e93d63105（プラン4マージ 9470efb60 込み）。全回帰922/922 PASS
- moorestech_master: `plan2-master-migration` @ dce2ff9、**originへpush済み**。ピン（`.moorestech-external-revisions.json`）一致
- `../moorestech_master` はsymlink → `/Users/katsumi/moorestech-worktrees/moorestech_master`（実クローン）
- プラン4の進行台帳・レビューdiff・プレイテスト録画は `.superpowers/sdd-plan4/`（gitignore下）へ退避済み
- メインチェックアウトは他セッション共用。**プラン5も専用worktree＋専用Unity（`uloop launch ./moorestech_client`）での作業を推奨**

## 2. 計画書からのドリフト（着手時に読み替えること・2026-07-07検証済み）

1. **削除対象は69→81アイテム**: ベルト長尺バリアント12種（隠しアイテム）がプラン5計画確定後に追加されたため。現況: blocksのitemGuid 79件＋車両3件−木のシャフト1件＝81。Task 6スクリプトの`expected 69`表記を81へ更新すること（targets導出はblocksのitemGuid走査なので隠しアイテムは自動的に含まれる）
2. **Task 2 Step 2のシグネチャが旧形**: `SendPlaceBlockProtocolMessagePack(playerId, blockId, placePositions)`はbeltマージ後に不存在。現行は`(int playerId, List<PlaceInfo> placeInfos)`の1引数形で、**セルごとに`PlaceInfo.BlockId`を充填する**（`PlaceBlockProtocolTest`の準備コードが前例）
3. **Task 1 Step 1の前提は充足済み**: 本番マスタにrequiredItems未定義ブロックは0件（検証コマンド実行済み）。ForUnitTest/EditModeInPlayingTest側の棚卸しは着手時に実施

## 3. belt側変更との整合（再確認済みの要点）

- 垂直オーバーライド機構はスキーマ・コード・データとも完全削除済み。プラン5のitemGuid/usePlaceItems削除で参照する旧経路は残っていない
- ベルト隠しアイテム12種はTask 6で一括削除対象（上記ドリフト1）。隠しアイテムにcraftレシピは無いためdangling検証に引っかからない見込み
- `PlaceBlockProtocol`の未解放判定は`BeltConveyorPlaceFamilyUtil.TryGetFamily`でファミリー代表基準（プラン5では変更不要、Task 2のテスト移行時に触る場合のみ注意）

## 4. プラン5と独立した並行残件

- **電線接続ツール**: master側hermesコミット（electricWireItems）が未マージのため本番に`ElectricWireConnect`エントリ無し。**hermesマージ時に**エントリ＋electricWireItemsを追加し電線シナリオを再検証（プラン4 Task 13の唯一の未達）。電線アイテムは敷設素材として存続するためプラン5の削除対象と競合しない
- moorestech_masterのブランチ整理: local master(cd5fc11)がe5e144bから先行分岐したままplan2-master-migrationと未統合（プラン2からの継続事項）
- 車両nameフィールド追加（別件）: プラン5 Task 3の車両アイコン表示名は当面addressablePath末尾等で代替し、報告に含める（計画書どおり）

## 5. 環境メモ

- moorestech_master編集前に`pgrep -fl mooreseditor`で停止確認（プラン4 Task 6でimplementerが誤killした前例あり。**killせずユーザーに確認**）
- Domain Reloadエラーは45〜50秒待ちリトライ。Client.Tests実行後は特に長引く（プラン4実績: 50秒×数回）
- `UnityMcpSettings.json`が`.bak`へ勝手にリネームされる事象が既知（3回発生）。テスト失敗時はまず疑う
- スキーマ編集（Task 5）はedit-schemaスキル手順厳守。プロパティ削除時は全JSON配置先grep（Tests.Module ForUnitTest / Client.Tests EditModeInPlayingTest / ../moorestech_master / mooresmaster.SandBox）
