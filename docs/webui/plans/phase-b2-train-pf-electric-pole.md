# Phase B2 実行計画: 列車PFインベントリ・電柱ネットワーク情報

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-BLK-9 / 電柱情報相当。依存なし。列車 HUD 本体は Phase C3（ここでは PF のみ）。

## タスク1: 列車プラットフォーム（item / fluid）

- uGUI 参照実装: `InGame/UI/Inventory/Block/TrainPlatformBlockInventoryViewBase.cs` 派生の
  `TrainItemPlatformBlockInventoryView.cs` / `TrainFluidPlatformBlockInventoryView.cs`（fluid は容量表示付き）
- 表示: PF スロット + `TrainPlatformTransferStateDetail`（積込/卸しモード状態）
- 操作: モード切替（uGUI は `SetTrainPlatformTransferMode`）→ Action 化
- 手順（順序規約どおり）:
  1. uGUI 側の表示項目・操作を実コードで確定（モードの enum 値・スロット構成・エラー状態）
  2. `BlockDetailDtoBuilder` に PF capability を追加（モード状態含む）+ 切替 Action
  3. `protocol.ts`/WireFixtures 契約 → Web ビュー（`src/features/blockInventory/` に PF セクション）
  4. blockComponents レジストリへ TrainStation / TrainItemPlatform / TrainFluidPlatform 系
     blockType を登録（v8 blocks.json で対象 blockType を再列挙して確定）
- e2e: モードトグルとスロット表示のジェスチャテストを blockInventory e2e 群に追加

## タスク2: 電柱ネットワーク情報

- uGUI 参照実装: `InGame/UI/Inventory/Block/ElectricPoleNetworkInfoUIView.cs` +
  `ElectricNetworkInfoView.cs`
- 表示: 所属電力ネットワークの集約情報（発電量・消費量・充足率等。表示項目は uGUI 実コードで確定）
- データ源: 既存 `Client.WebUiHost/Game/Topics/BlockDetail/BlockNetworkInfoCache.cs` が近縁。
  電力ネットワーク値も連続変動のため**固定間隔サンプリング**で publish（B1 と同方針）
- レジストリへ ElectricPole 系 blockType（v8 で3ブロック）を登録

## 完了条件

- PF 2種 + 電柱が専用 UI で開き、PF モード切替が操作できる
- B1 のレジストリ網羅テストに本 Phase の blockType も通る

## 検証

vitest / blockInventory e2e / `uloop compile` / 完了時コミット + `../TODO.md` 更新
