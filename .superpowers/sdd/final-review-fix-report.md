# 最終レビュー機械的修正 適用レポート

作業ディレクトリ: /Users/katsumi/moorestech-worktrees/tree1
適用順序: B（デッドコード削除）→ A（残った比較演算子）→ C → D → E

## A. 比較演算子
- A-1/A-2: **スキップ（正当）**。B-3/B-4で `HasMaterials`（`Materials.Count > 0` を含む行）ごと削除したため対象行が消失。矛盾解消。
- A-3: RailConnectionEditProtocol.cs:140 `refundStacks.Count > 0` → `0 < refundStacks.Count` 適用。
- A-4: ElectricWireSystemUtil.cs:143 同上 適用。

## B. デッドコード削除
- B-1: BuildMenuEntryDtoFactory.cs 未使用 `using ...PlaceSystem.ConnectTool;` 削除。`ConnectToolPlacementTarget` は `.Targets`、`ConnectToolIconEndpoint` は親名前空間 `Client.WebUiHost.Game` 解決で、当該usingは真に未使用と確認。
- B-2: ConnectToolMasterUtil.Initialize（呼び出し元0件をgrep確認）削除。Validate は残置（E参照）。
- B-3: ElectricWireConnectionCost.HasMaterials 削除。Empty は ElectricWirePlacementEvaluator から参照ありのため残置。
- B-4: GearChainConnectionCost.HasMaterials 削除。併せて Empty も production参照0件のため削除、未使用となった `using System;` も削除。
- B-5: GearChainConnectionCost.Empty 削除に伴い GearChainPoleSaveLoadTest.cs の2箇所を `new GearChainConnectionCost(Array.Empty<ConnectToolMaterialCost>())` へ置換、`using System;` を追加。

## C. cardinality（解放フィルタ欠如）
- ConnectToolCatalog.ResolveDefaultConnectToolGuid に `IGameUnlockStateData unlockState` 引数を追加し、`.Where(解放済み)` を挿入（BuildMenuEntryCatalog の `ConnectToolUnlockStateInfos.TryGetValue(...).IsUnlocked` 前例に一致）。0件時は既存どおり Guid.Empty 返却。
- 呼び出し元追従:
  - PlacementTargetPickService.cs:44 → 既存フィールド `_gameUnlockStateData` を供給。
  - GearChainPoleConnectSystem.cs → コンストラクタに `IGameUnlockStateData` を追加（VContainer登録済み: MainGameStarter.cs:251 Singleton）しフィールド保持、呼び出しへ供給。
- 全呼び出し元をgrepで洗い出し2箇所のみと確認、両方更新済み。

## D. region-internal規約
- D-1: RailConnectWithPlacePierProtocolTest.cs のクラス直下 `#region Internal`/`#endregion` のみ削除（privateメソッドは複数[Test]共用のため据え置き）。
- D-2: ElectricWireExtendService.Execute の `ExecuteExtendWithOrigin`/`ExecuteIsolatedPlace` をExecute末尾の単一 `#region Internal` 内ローカル関数化（inventory/fromPos/polePlaceInfo/blockId/poleParam/connectToolGuid/costItemCounts をキャプチャ）。TryPlacePole は両者から呼ばれる共有ヘルパーのためクラス直下privateのまま。未使用化した `using Core.Inventory;` を削除。
- D-3: ElectricWirePlacementEvaluator の `SumReserved`/`HasEnoughItem` を EvaluateWireConnection 末尾のローカル関数化（reservedMaterials/items をキャプチャ、引数削減）。TryCalculateWireCost は別メソッドのため不変。
- D-4: GearChainPlacementEvaluator の `SumReserved` を CountItem と同じ `#region Internal` へ統合しローカル関数化（reservedMaterials キャプチャ）。
- D-5: ElectricWireAutoConnectPreview の `TrySelectConnectTool`（内部 TrySumCost 込み）を ApplyAutoConnect の既存 `#region Internal` へローカル関数化。virtualInventory をキャプチャし、outパラメータ `totalCost` は外側ローカルとの衝突回避のため `selectedCost` へ改名。
- D-6: ConnectToolTextureLoader の `LoadViewData` を GetConnectToolTexture 末尾の `#region Internal` ローカル関数化。
- D-7: PlacementModeTopic の `GetSelectedName` を BuildJson 末尾の `#region Internal` ローカル関数化（_controller.CurrentTarget をキャプチャし引数ゼロ化）。

## E. マスタバリデーション追加
- ConnectToolMasterUtil.Validate に `ValidateLengthPerUnit`（`element.LengthPerUnit <= 0` を検出しログ追記）を既存の itemGuid 実在検証と同じlogsパターンで追加。ConnectToolCostCalculator の `CeilToInt(distance/LengthPerUnit)` 0除算防止。フォールバックではなく不正検出ログ。B-2のInitialize削除と両立。

## 永続化
ConnectionCost型（GearChain/ElectricWire）を触ったが、シリアライズ構造は不変。削除したのは未使用の HasMaterials / Empty のみで、保存はItemGuid・ロード時GetItemId解決を維持。B-5はテストのみの変更。

## 検証結果
- コンパイル: `uloop compile --project-path ./moorestech_client` → Success:true, ErrorCount:0
- テスト: `--filter-value "ElectricWire|Rail|GearChain|ConnectTool"` → 172 tests, 172 passed, 0 failed, 0 skipped

## 懸念
- 特になし。D-2は90行メソッドのローカル関数化で最も規模が大きいが、キャプチャ変数の定義済み性（poleParamはis-notチェック後に確定）とTryPlacePole共有を保ち、テスト172全PASSで挙動不変を確認。
