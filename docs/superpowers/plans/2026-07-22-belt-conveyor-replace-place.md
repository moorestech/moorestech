# ベルトコンベア リプレース設置 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ドラッグ起点が既存ベルト系ブロックのとき、パス上の既存ベルト系ブロックを選択中ブロックへアトミックに置き換え設置できるようにする（搬送中アイテムは進行率維持で引き継ぎ）。

**Architecture:** サーバーは `PlaceBlockProtocol` にセル単位 `IsReplace` フラグを追加し、`BlockReplaceService`（新設）が「事前検証→旧撤去→返却→新設置→消費→搬送品引き継ぎ」をロールバック不要の順序で実行する。クライアントは既存の設置計算機（Belt/Common）に「リプレースドラッグ」判定を追加し、プレビューにリプレース色を足す。新プロトコル・新UIステートは作らない。

**Tech Stack:** Unity C# / MessagePack / UniRx / NUnit（uloop実行）

**Spec:** `docs/superpowers/specs/2026-07-22-belt-conveyor-replace-place-design.md`

## Global Constraints

- partial禁止・1ファイル200行以下・単純getter/setter禁止（SetHogeメソッド）・try-catch原則禁止
- イベントはUniRx（Action禁止）
- コメントは日本語→英語の2行セット
- .metaファイル手動作成禁止
- コンパイル: `uloop compile --project-path ./moorestech_client`（tree3ルートから）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`
- ドメインリロードエラー時は45秒待ってリトライ

## 配置と前例（spec-architecture-review済み）

| 新規/変更 | 配置先 | 前例 |
|---|---|---|
| `BlockReplaceFamilyUtil`（新規） | `Game.Block.Interface/Extension/` | 同dir `BeltConveyorPlaceFamilyUtil.cs`（クライアントからも参照済み） |
| `BlockRemoveReason.Replace`（enum追加） | `Game.Block.Interface/BlockRemoveReason.cs` | 既存enum拡張 |
| `PlaceInfo.IsReplace` / `PlaceInfoMessagePack Key(5)` | `Server.Protocol/PacketResponse/PlacePacketDto.cs` | 既存DTO拡張（クライアントも同クラスを共用） |
| `BlockReplaceService`（新規） | `Server.Protocol/PacketResponse/Util/Construction/` | 同dir `ConstructionCostService.cs` |
| `TryInsertItemWithRemainingRate`（メソッド追加） | `VanillaBeltConveyorComponent` | コンポーネントが自インベントリを所有（`SetItem`前例） |
| リプレース判定拡張 | `BeltConveyorPlacePointCalculator` / `CommonBlockPlacePointCalculator` | 既存の`IsNotExistBlock`差し替え点 |
| リプレース色 | `MaterialConst` + `BlockPreviewObject` | `PlaceableColor`/`NotPlaceableColor`前例 |

---

### Task 1: サーバー基盤（ファミリー判定util・RemoveReason・DTOフラグ）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BlockReplaceFamilyUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/BlockRemoveReason.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs`

**Interfaces:**
- Produces: `BlockReplaceFamilyUtil.IsReplaceFamily(BlockId blockId): bool`／`BlockRemoveReason.Replace`／`PlaceInfo.IsReplace: bool`・`PlaceInfoMessagePack.IsReplace`（Key(5), bool）

- [ ] **Step 1: BlockReplaceFamilyUtil を作成**

```csharp
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// リプレース設置で相互置き換え可能なブロックファミリー判定
    /// Determines blocks mutually replaceable via replace-placement
    /// </summary>
    public static class BlockReplaceFamilyUtil
    {
        public static bool IsReplaceFamily(BlockId blockId)
        {
            // ベルト系3タイプのみ相互リプレース可能とする
            // Only the three belt-type blocks are mutually replaceable
            var blockType = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockType;
            return blockType == BlockMasterElement.BlockTypeConst.BeltConveyor ||
                   blockType == BlockMasterElement.BlockTypeConst.GearBeltConveyor ||
                   blockType == BlockMasterElement.BlockTypeConst.FilterSplitter;
        }
    }
}
```

- [ ] **Step 2: BlockRemoveReason に Replace を追加**（既存2値の後ろに `// リプレース設置による置き換え撤去` `// Removed by replace-placement` コメント付きで `Replace` を追加）

- [ ] **Step 3: PlacePacketDto に IsReplace を追加**

`PlaceInfo` クラスに `public bool IsReplace { get; set; }` を追加。`PlaceInfoMessagePack` に `[Key(5)] public bool IsReplace { get; set; }` を追加し、コンストラクタで `IsReplace = placeInfo.IsReplace;` をコピー。

- [ ] **Step 4: コンパイル** — `uloop compile --project-path ./moorestech_client` → エラー0を確認

- [ ] **Step 5: Commit** — `feat(server): リプレース設置の基盤（ファミリー判定・RemoveReason・DTOフラグ）`

---

### Task 2: VanillaBeltConveyorComponent に進行率指定挿入を追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/VanillaBeltConveyorComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorReplaceInsertTest.cs`（新規）

**Interfaces:**
- Produces: `VanillaBeltConveyorComponent.TryInsertItemWithRemainingRate(ItemId itemId, double remainingRate): bool`
  - remainingRate: 1.0=入口直後、0.0=出口。`RemainingTicks = ticksOfItemEnterToExit * remainingRate` を設定
  - スロットは `slotIndex = Clamp(CeilToInt(remainingRate * slotCount) - 1, 0, slotCount - 1)` で選び、埋まっていれば入口側（インデックス大）へ順にずらす。全部埋まっていれば false

- [ ] **Step 1: 失敗するテストを書く**（creating-server-testsスキル参照。`ForUnitTestModBlockId.BeltConveyorId` でベルト生成 → `TryInsertItemWithRemainingRate(itemId, 0.5)` → `BeltConveyorItems` の該当スロットに入り `RemainingTicks ≒ Total*0.5` を検証。満杯時falseも検証）
- [ ] **Step 2: テスト失敗を確認** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorReplaceInsertTest"` → コンパイルエラー（メソッド未定義）
- [ ] **Step 3: 実装**

```csharp
/// <summary>
/// 進行率を維持したままアイテムを挿入する（リプレース設置の搬送品引き継ぎ用）
/// Insert an item preserving its progress rate (for replace-placement transit handover)
/// </summary>
public bool TryInsertItemWithRemainingRate(ItemId itemId, double remainingRate)
{
    BlockException.CheckDestroy(this);

    // 進行率に対応するスロットから入口側へ空きを探す
    // Search for a free slot from the rate-matched slot toward the entry side
    var idealIndex = Math.Clamp((int)Math.Ceiling(remainingRate * _inventoryItemNum) - 1, 0, _inventoryItemNum - 1);
    for (var i = idealIndex; i < _inventoryItems.Length; i++)
    {
        if (_inventoryItems[i] != null) continue;

        var goalConnector = _blockInventoryInserter.GetNextGoalConnector(new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, 1) });
        _inventoryItems[i] = new VanillaBeltConveyorInventoryItem(itemId, ItemInstanceId.Create(), null, goalConnector, _ticksOfItemEnterToExit)
        {
            RemainingTicks = (uint)Math.Ceiling(_ticksOfItemEnterToExit * remainingRate),
        };
        NotifyItemsChanged();
        return true;
    }

    return false;
}
```

- [ ] **Step 4: テスト成功を確認**（同コマンド → PASS）
- [ ] **Step 5: Commit** — `feat(server): ベルトへの進行率指定アイテム挿入を追加`

---

### Task 3: BlockReplaceService と PlaceBlockProtocol の分岐

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BlockReplaceService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`

**Interfaces:**
- Consumes: Task 1の`BlockReplaceFamilyUtil`・`BlockRemoveReason.Replace`・`PlaceInfoMessagePack.IsReplace`、Task 2の`TryInsertItemWithRemainingRate`
- Produces: `BlockReplaceService.TryReplaceBlock(PlaceInfoMessagePack placeInfo, IOpenableInventory playerInventory, bool isFreePlacement): bool`

**処理順序（ロールバック不要の順序で厳守）:**
1. 対象座標にブロックが存在し、既存・新規の両方が `BlockReplaceFamilyUtil.IsReplaceFamily` であることを検証（違えば false）
2. 同一BlockId・同一Directionなら no-op で true（冪等・サーバー側防御）
3. 新ブロックのunlock検証（`PlaceBlockProtocol.IsUnlocked` 相当のロジックを再利用。ベルトファミリーは直線ブロックで判定）
4. 旧ブロックの搬送品を `(ItemId, remainingRate)` リストとして収集：`block.ComponentManager.TryGetComponent<IItemCollectableBeltConveyor>` の `BeltConveyorItems` から `remainingRate = item.RemainingTicks / (double)item.TotalTicks`（TotalTicks==0/uint.MaxValueなら1.0扱い）。IItemCollectableBeltConveyorが無いブロック（FilterSplitter）は `IBlockInventory` の全スロットを rate=1.0 として収集
5. 旧建設コスト返却リストを作成（`RemoveBlockProtocol.GetRefundItems` と同じ計算のうち建設コスト部分のみ。搬送品は4で別収集済みなのでIBlockInventory走査は含めない）
6. **事前検証**: `playerInventory.InsertionCheck(旧コスト返却 + 搬送品全部)` が false なら失敗（最悪ケースで全搬送品がプレイヤー行きでも溢れないことを保証）。さらに新コストが「現在インベントリ＋旧コスト返却」で賄えるか検証（`ConstructionCostService.HasRequiredItems` を返却リスト連結で評価）。isFreePlacement時はコスト検証・消費・返却をすべてスキップ
7. 旧ブロック撤去 `RemoveBlock(pos, BlockRemoveReason.Replace)` → 旧コスト返却を挿入
8. `TryAddBlock` で新設置（失敗時は搬送品をプレイヤーへ挿入して終了＝ベストエフォート。6の検証済みなので必ず入る）
9. `ConstructionCostService.ConsumeRequiredItems` で新コスト消費
10. 搬送品引き継ぎ: 新ブロックの `ComponentManager.TryGetComponent<VanillaBeltConveyorComponent>` が取れれば各アイテムを `TryInsertItemWithRemainingRate`、false戻り・コンポーネント無しの分はプレイヤーへ挿入（6で検証済み）

- [ ] **Step 1: BlockReplaceService を上記順序で実装**（200行以内。`#region Internal`＋ローカル関数でフロー明示。GearBeltConveyorは`VanillaBeltConveyorComponent`がComponentManagerに登録されているか確認し、されていなければ`GearBeltConveyorComponent`経由の取得も試す）
- [ ] **Step 2: PlaceBlockProtocol に分岐を追加**

`PlaceBlock(placeInfo)` 冒頭の `Exists` チェックを以下に変更:

```csharp
// リプレース指定セルはリプレースサービスへ委譲、それ以外は既存ブロックがあれば何もしない
// Delegate replace-flagged cells to the replace service; otherwise skip occupied cells
if (placeInfo.IsReplace)
{
    _blockReplaceService.TryReplaceBlock(placeInfo, inventoryData.MainOpenableInventory, isFreePlacement);
    return;
}
if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;
```

（`_blockReplaceService`はコンストラクタでnew。unlock判定ロジックはBlockReplaceService側と共通化し、PlaceBlockProtocolが200行を超えるなら`IsUnlocked`をBlockReplaceServiceかutilへ移す）

- [ ] **Step 3: コンパイル** → エラー0
- [ ] **Step 4: Commit** — `feat(server): PlaceBlockProtocolにリプレース設置を追加`

---

### Task 4: サーバーCombinedTest

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ReplaceBlockPlaceTest.cs`

**Interfaces:**
- Consumes: Task 1-3の全成果物。既存の`PlaceBlockProtocol`テスト（`Tests/CombinedTest/Server/PacketTest/`配下の既存設置テストを先に読み、初期化・パケット送信パターンを踏襲する。creating-server-testsスキル参照）

テストケース（各ケース、プレイヤーインベントリに必要素材を入れてから`va:placeBlock`をIsReplace付きで送る）:

- [ ] **Step 1: 同型向き変え** — ベルト設置(North)→同IDでIsReplace+East送信 → 位置のブロックがEast向き・インベントリ素材数が増減なし
- [ ] **Step 2: 搬送品の進行率維持** — ベルトにアイテム挿入→数tick進行→リプレース → 新ブロックの`BeltConveyorItems`に同ItemIdが存在し進行率が誤差1スロット以内で一致
- [ ] **Step 3: 異種置き換えの差額精算** — ベルトA(コストX)をベルトB(コストY)へリプレース → インベントリが -Y+X 変化、ブロックIDがBになっている
- [ ] **Step 4: 非ファミリー拒否** — 機械ブロックの座標へIsReplace送信 → ブロックが変化しない・素材消費なし
- [ ] **Step 5: インベントリ満杯時の失敗** — プレイヤーインベントリを満杯にしてリプレース → 旧ブロックがそのまま残る・搬送品ロストなし
- [ ] **Step 6: 通常設置の挙動不変** — IsReplace=falseで既存ブロック座標へ送信 → 従来通りスキップ
- [ ] **Step 7: テスト実行** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ReplaceBlockPlaceTest"` → 全PASS
- [ ] **Step 8: Commit** — `test(server): リプレース設置のCombinedTest`

---

### Task 5: クライアント リプレースドラッグ判定

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlacePointCalculator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ReplacePlacementJudge.cs`

**Interfaces:**
- Consumes: `BlockReplaceFamilyUtil.IsReplaceFamily`（Task 1、クライアントから参照可能な`Game.Block.Interface.Extension`）、`BlockGameObjectDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)`
- Produces: `ReplacePlacementJudge`（static）:
  - `IsReplaceDragStart(BlockGameObjectDataStore store, BlockId holdingBlockId, Vector3Int startCell): bool` — 手持ちと起点セル既存の両方がファミリーならtrue
  - `TryMarkReplace(BlockGameObjectDataStore store, PlaceInfo info): bool` — セルの既存ブロックがファミリーなら `info.IsReplace = true; info.Placeable = true` にして true。同BlockId・同Directionなら `Placeable = false`（no-op）。非ファミリーなら false（Placeable据え置き=false）

- [ ] **Step 1: ReplacePlacementJudge を実装**（既存ブロック取得は`TryGetBlockGameObject`、BlockIdは`BlockGameObject`の保持データから。向きは`BlockPosInfo.BlockDirection`）
- [ ] **Step 2: BeltConveyorPlaceSystem に組み込み** — `ScreenLeftClick.GetKeyDown`時に`_isReplaceDrag = ReplacePlacementJudge.IsReplaceDragStart(...)`を保持（`Disable`と選択ブロック変更時にfalseへリセット）。`SetCurrentPlaceInfo`後、`_isReplaceDrag`なら`BeltConveyorCellBlockResolver.Resolve`済みの各`PlaceInfo`のうち`Placeable==false`のセルへ`TryMarkReplace`を適用（重なり以外の理由で不可のセルを誤って復活させないよう、適用は「既存ブロック重なりが原因のセル」に限定する: TryMarkReplace内で既存ブロック有無を自分で判定するため、ブロックが無いセルは触らない）
- [ ] **Step 3: CommonBlockPlaceSystem にも同様の組み込み**（FilterSplitter等の単セルブロック用。ドラッグ開始時判定＋計算後の`TryMarkReplace`適用。1x1ブロックのみ対象: `BlockSize != Vector3Int.one`なら適用しない）
- [ ] **Step 4: コスト先読みとの整合** — `BeltConveyorCostPreviewMarker`/`ConstructionCostPreviewCalculator`はPlaceable=trueセルを従来通り「新コスト要求」として数える（変更不要なことを確認）
- [ ] **Step 5: コンパイル** → エラー0
- [ ] **Step 6: Commit** — `feat(client): リプレースドラッグ判定を設置システムへ追加`

---

### Task 6: プレビューのリプレース色

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Common/`配下の`MaterialConst`（`PlaceableColor`定義箇所をGrepで特定）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs`

**Interfaces:**
- Produces: `MaterialConst.ReplacePreviewColor`（シアン系: `new Color(0.3f, 0.8f, 1f, α)`、αは既存Placeable色と同値）、`BlockPreviewObject.SetPreviewColor(PlaceInfo placeInfo)`（Placeable×IsReplaceで3色分岐）

- [ ] **Step 1: MaterialConst に ReplacePreviewColor を追加**
- [ ] **Step 2: BlockPreviewObject に `SetPreviewColor(PlaceInfo)` を追加**（IsReplace&&Placeable→Replace色 / Placeable→従来色 / それ以外→NotPlaceable色。既存`SetPlaceableColor(bool)`は他呼び出し元があるため残す）
- [ ] **Step 3: PlacementPreviewBlockGameObjectController の2箇所の`SetPlaceableColor`呼び出しを`SetPreviewColor(placeInfo)`へ変更**（L52は地面接触時にPlaceable=false相当の扱いを維持）
- [ ] **Step 4: コンパイル** → エラー0
- [ ] **Step 5: Commit** — `feat(client): リプレースセルのプレビュー色を追加`

---

### Task 7: 統合検証（コンパイル・回帰テスト）

- [ ] **Step 1: フルコンパイル** — `uloop compile --project-path ./moorestech_client` → エラー0・新規警告0
- [ ] **Step 2: 関連回帰テスト** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(ReplaceBlockPlaceTest|BeltConveyorReplaceInsertTest|PlaceBlock|BeltConveyor)"` → 全PASS（既存のPlaceBlock系・BeltConveyor系が壊れていないこと）
- [ ] **Step 3: Commit**（未コミット差分があれば）

---

### Task 8: プレイテストE2E（unity-playmode-recorded-playtest）

- [ ] **Step 1:** unity-playmode-recorded-playtestスキルのDSLシナリオで検証: ベルト5本を東向きに設置 → 起点ベルトからリプレースドラッグ相当の`SendOnly.PlaceBlock`（IsReplace付きPlaceInfo）を送信 → ブロックの向きが変わったことをサーバー状態で確認。可能ならUI経路（ビルドメニュー→ドラッグ）でも実施
- [ ] **Step 2:** 録画とresult.jsonを確認し、Errorログ0を確認
- [ ] **Step 3: Commit**（シナリオファイル追加分）

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 通常ドラッグ設置（起点空セル） | 生存 | IsReplace=false経路は`Exists`スキップ含め不変（Task 4 Step 6で検証） |
| 削除モード(G)・スポイト・Blueprint | 生存 | 触らない。BlueprintはIsReplace未設定=false |
| RemoveBlockの返却 | 生存 | RemoveBlockProtocolは無変更 |
| ベルト上アイテムの既存挙動 | 生存 | `TryInsertItemWithRemainingRate`は追加メソッドで既存経路無変更 |
