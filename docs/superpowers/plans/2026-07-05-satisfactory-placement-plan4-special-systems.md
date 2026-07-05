# Satisfactory式設置システム プラン4: 特殊システム縦切り 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 特殊設置5プロトコル（列車車両設置×2・橋脚付きレール接続・電柱延長・歯車チェーンポール延長）を「スロット指定アイテム消費」から「Guid/BlockId指定＋requiredItemsインベントリ横断消費＋アンロックゲート」へ移行し、クライアント特殊システムをビルドメニュー選択駆動（`PlacementSelection`）に切り替え、本番マスタの除外9ブロック＋車両3種へrequiredItemsを投入する。

**Architecture:** サーバーは`ConstructionCostService`を`(ItemId,int)`タプル基盤に一般化して車両マスタ（`TrainCarRequiredItemElement`）も扱えるようにし、5プロトコルへ`PlaceBlockProtocol`と同型の「アンロック検証→コスト事前検証→設置→消費」を移植する。クライアントは`BlockPlacementSelection`を3種別（ブロック/車両/接続ツール）対応の`PlacementSelection`へ拡張し、`placeSystem.yml`を「接続ツールのビルドメニューエントリ定義」へ転換、`PlaceSystemSelector`のUsePlaceItems/HoldingItemIdマッチングを全廃する。

**Tech Stack:** Unity C# / MessagePack / VContainer / UniRx / uloop CLI / Python3（マスタ追補スクリプト）

**Spec:** `docs/superpowers/specs/2026-07-03-satisfactory-style-placement-design.md`
**申し送り:** `docs/superpowers/plans/2026-07-05-satisfactory-placement-handoff.md`（必読: 基盤API・技術的な罠・プラン4への申し送り）

## 合意済み・本プランでの設計判断

- **MessagePackペイロードは互換なしで置換する**（クライアント・サーバー同時更新のため許容。既存Key番号の「意味の維持」は不要だが、番号の並びは既存のまま流用し追加enum値は末尾に足す）
- **`ConstructionCostService`は`IReadOnlyList<(ItemId itemId, int count)>`を正準形にする**。blocks用`ConstructionRequiredItemElement[]`と車両用`TrainCarRequiredItemElement[]`は`ToItemCounts`変換で吸収。この変換結果は`ElectricWireAutoConnectService.EvaluateAutoConnect`の`reservedItems`引数と同型なので、電線予約リストとしてそのまま渡す（申し送りの「requiredItems→reservedItems変換helper」に相当）
- **接続ツールエントリは`placeSystem.json`がマスタソース**。`usePlaceItems`/`priority`を廃止し、`name`/`iconItemGuid`/`placeBlockGuid`(optional)/`sortPriority`を新設。`placeBlockGuid`は「空きスペースへ延長するとき自動設置するブロック」（レール接続→橋脚、電線接続→電柱）。歯車チェーン接続ツールは接続専用のため`placeBlockGuid`なし（ポール設置はポールブロック選択で行う。現行の「ポール保持＝設置延長/チェーン保持＝接続のみ」の分離を踏襲）
- **敷設素材（電線・チェーン・レール）の選定はクライアント側自動選択**: 電線=`Blocks.ElectricWireItems`マスタ定義順で最初に所持しているもの（サーバーの`ElectricWireAutoConnectService`と同ルール）、レール=`TrainUnitMaster.GetRailItems()`定義順、チェーン=既存`GearChainPoleItemFinder.FindOwnedChainItemId`（既存実装を踏襲）
- **歯車ポール・レール橋脚のブロック選択は専用システムへルーティング**: `blockType == GearChainPole`→`GearChainPoleConnectSystem`（連続延長つき設置）、`blockType == TrainRail`→`TrainRailPlaceSystem`。電柱は通常の`CommonBlockPlaceSystem`（サーバー側自動接続が現行どおり効く）
- **`RemoveTrainCarProtocol`の全額返却化は本プランに含める**（車両プロトコルの縦切りの一部。requiredItems未定義マスタ向けのitemGuidフォールバックは`RemoveBlockProtocol`と同型で残し、プラン5で削除）
- **ビルドメニューはカテゴリタブなしの単一グリッド続行**（プラン3の合意を踏襲）。表示順は ブロック→車両→接続ツール のセクション順、各セクション内はsortPriority順
- **GearChainPoleExtendResponseの失敗理由はstringのまま**（enum化はスコープ外。既存定数を踏襲）
- **旧経路との共存期間の増殖について**: requiredItems投入（Task 12）時点で対象9ブロックの素材レシピを同時削除するため、旧ホットバー経路が残っていても「素材→アイテム→設置→破壊→素材」は往復中立。プラン2と同じ受容ライン

## Global Constraints

- 作業開始時に必ず`pwd`確認。作業ディレクトリは `/Users/katsumi/moorestech`
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client`（エラー0件）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reloadエラーは45秒待ってリトライ。一括180秒超はクラス分割
- 新規サーバー.csファイルはUnity再起動が必要な場合がある（申し送り「テスト・コンパイルの罠」参照）。.metaは手動作成禁止（サーバー側は2行形式がUnity正規出力）
- partial絶対禁止 / 1ファイル200行以下 / 1ディレクトリ10ファイルまで / 単純getter/setter禁止（値のSetは`SetHoge`メソッド） / デフォルト引数禁止（追加引数は全呼び出し側を更新） / try-catch禁止 / イベントはUniRx
- 日英2行セットコメント（各1行厳守）を約3〜10行ごと。自明コメント禁止
- `#region Internal`はメソッド内ローカル関数まとめ用途のみ
- スキーマ編集はedit-schemaスキルの手順に従う（`_CompileRequester.cs`のdummyText変更でSourceGenerator起動、foreignKey追加時はvalidate-schellスキル相当のC#バリデーション追加必須、プロパティ削除時は全JSON配置先のgrep確認）
- moorestech_master編集時はmooreseditor.appを終了（`pgrep -fl mooreseditor`で確認）。コミット後は`.moorestech-external-revisions.json`のピン更新
- テストコードでの`Core.Inventory`等の参照は`global::`修飾が必要な場合がある
- 各タスク完了時にコミット

## 配置と前例（spec-architecture-review）

| 配置決定 | 層 | 前例 |
|---|---|---|
| `ConstructionCostService`のタプル正準化＋車両変換 | Server.Protocol/Util/Construction | 自身（プラン1）＋`ElectricWireSystemUtil.ConsumeItem`再利用構造を維持 |
| 5プロトコルのアンロック検証 | 各プロトコル内 | `PlaceBlockProtocol.cs:42-43`（`IGameUnlockStateDataController`をDI取得し`BlockUnlockStateInfos[guid].IsUnlocked`） |
| `PlacementSelection`（3種別選択） | Client.Game/.../PlaceSystem | `BlockPlacementSelection`（プラン3）を拡張・改名 |
| `BuildMenuEntryCatalog` | Client.Game/.../UI/BuildMenu | `BuildMenuView.RebuildBlockList`のエントリ列挙を分離（200行制約対応） |
| 電線・レール素材の自動選択util | 各Connectシステムのディレクトリ | `GearChainPoleItemFinder.FindOwnedChainItemId`（クライアント側自動選択の既存前例） |
| placeSystem.ymlの転換 | VanillaSchema | スペック§3。foreignKeyバリデーションは`PlaceSystemMasterUtil`（既存）を更新 |
| 車両全額返却 | `RemoveTrainCarProtocol` | `RemoveBlockProtocol`のrequiredItems返却＋フォールバック構造 |

新規パターン（ユーザーレビュー注目点）: **placeSystem.ymlの`placeBlockGuid`**（接続ツール→自動設置ブロックの関連付け）。従来は「インベントリ内のアイテム検索」で暗黙決定していたものをマスタで明示する。

---

### Task 1: ConstructionCostServiceのItemCount正準化と車両対応

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionCostService.cs`（全面改修・67行→約110行）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`（76-80行のインライン変換と62-92行の呼び出し）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`（`CreateRefundItems`呼び出し箇所。grepで特定）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ConstructionCostServiceTest.cs`（既存4テスト更新＋車両変換テスト追加）

**Interfaces:**
- Consumes: `Mooresmaster.Model.BlocksModule.ConstructionRequiredItemElement`（`.ItemGuid`/`.Count`）、`Mooresmaster.Model.TrainModule.TrainCarRequiredItemElement`（同形・別型）
- Produces（後続タスク全部が使う。シグネチャ厳守）:
  - `static (ItemId itemId, int count)[] ConstructionCostService.ToItemCounts(ConstructionRequiredItemElement[] requiredItems)`（null/空→空配列）
  - `static (ItemId itemId, int count)[] ConstructionCostService.ToItemCounts(TrainCarRequiredItemElement[] requiredItems)`（同上）
  - `static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IReadOnlyList<IItemStack> inventoryItems)`
  - `static void ConsumeRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IOpenableInventory inventory)`
  - `static List<IItemStack> CreateRefundItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts)`
  - ※変換結果は`ElectricWireAutoConnectService.EvaluateAutoConnect`の`reservedItems: IReadOnlyList<(ItemId itemId, int count)>`へそのまま渡せる

- [ ] **Step 1: 失敗するテストを書く**

`ConstructionCostServiceTest.cs`の既存テストを新シグネチャ（`ToItemCounts`経由）に書き換え、車両変換テストを追加する。既存テストの資材準備・アサーションはそのまま流用し、`ConstructionCostService.HasRequiredItems(requiredItems, ...)`を`ConstructionCostService.HasRequiredItems(ConstructionCostService.ToItemCounts(requiredItems), ...)`の形に変える。追加テスト:

```csharp
        [Test]
        public void 車両requiredItemsをItemCountsへ変換できる()
        {
            CreateServer();

            // 車両用の生成型はblocks用と別型のため、変換オーバーロードで正準形に揃える
            // Train-car generated type differs from the blocks one, so the overload normalizes it
            var trainCar = MasterHolder.TrainUnitMaster.Train.TrainCars[0];
            var itemCounts = ConstructionCostService.ToItemCounts(trainCar.RequiredItems);

            Assert.IsTrue(itemCounts.Length > 0);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(trainCar.RequiredItems[0].ItemGuid), itemCounts[0].itemId);
            Assert.AreEqual(trainCar.RequiredItems[0].Count, itemCounts[0].count);
        }

        [Test]
        public void requiredItemsがnullなら空のItemCountsを返す()
        {
            CreateServer();
            Assert.AreEqual(0, ConstructionCostService.ToItemCounts((ConstructionRequiredItemElement[])null).Length);
        }
```

（`CreateServer`は既存テストのサーバー生成ヘルパーに合わせる。車両テストはTask 2 Step 1でForUnitTestのtrain.jsonにrequiredItemsを投入してから通る — 先にTask 2 Step 1のマスタ投入だけ前倒しで行ってもよい）

- [ ] **Step 2: コンパイルエラー（RED）を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ToItemCounts`未定義エラー

- [ ] **Step 3: ConstructionCostServiceを改修**

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 建設コスト(requiredItems)の検証・消費・返却スタック生成を行う
    /// Validates, consumes, and creates refund stacks for construction costs (requiredItems)
    /// </summary>
    public static class ConstructionCostService
    {
        // ブロック用requiredItemsを正準形(ItemId,個数)へ変換する。電線予約リストと同型
        // Convert block requiredItems to the canonical (ItemId,count) form, shared with wire reservations
        public static (ItemId itemId, int count)[] ToItemCounts(ConstructionRequiredItemElement[] requiredItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return Array.Empty<(ItemId, int)>();

            var result = new (ItemId, int)[requiredItems.Length];
            for (var i = 0; i < requiredItems.Length; i++)
            {
                result[i] = (MasterHolder.ItemMaster.GetItemId(requiredItems[i].ItemGuid), requiredItems[i].Count);
            }
            return result;
        }

        // 車両用requiredItemsの変換。生成型が別なだけで内容は同じ
        // Conversion for train-car requiredItems; a distinct generated type with the same shape
        public static (ItemId itemId, int count)[] ToItemCounts(TrainCarRequiredItemElement[] requiredItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return Array.Empty<(ItemId, int)>();

            var result = new (ItemId, int)[requiredItems.Length];
            for (var i = 0; i < requiredItems.Length; i++)
            {
                result[i] = (MasterHolder.ItemMaster.GetItemId(requiredItems[i].ItemGuid), requiredItems[i].Count);
            }
            return result;
        }

        public static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IReadOnlyList<IItemStack> inventoryItems)
        {
            if (itemCounts == null || itemCounts.Count == 0) return true;

            // 全スロットの所持数を合算
            // Sum held counts across all inventory slots per material
            foreach (var (itemId, count) in itemCounts)
            {
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                if (total < count) return false;
            }

            return true;
        }

        public static void ConsumeRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IOpenableInventory inventory)
        {
            if (itemCounts == null || itemCounts.Count == 0) return;

            // 先頭スロットから順に減算する共通処理（電線消費と同一実装）を再利用する
            // Reuse the shared first-slot-onward consumption logic used by wire consumption
            foreach (var (itemId, count) in itemCounts)
            {
                ElectricWireSystemUtil.ConsumeItem(inventory, itemId, count);
            }
        }

        public static List<IItemStack> CreateRefundItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts)
        {
            var result = new List<IItemStack>();
            if (itemCounts == null) return result;

            // コスト全額分のスタック生成
            // Create refund stacks matching the full cost definition
            foreach (var (itemId, count) in itemCounts)
            {
                result.Add(ServerContext.ItemStackFactory.Create(itemId, count));
            }

            return result;
        }
    }
}
```

- [ ] **Step 4: 既存呼び出し側を更新**

Run: `grep -rn "ConstructionCostService\." moorestech_server moorestech_client --include='*.cs' | grep -v Tests`

- `PlaceBlockProtocol.cs`: セル処理内で一度だけ`var costItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);`を作り、(a)`HasRequiredItems(costItemCounts, inventory.InventoryItems)`、(b)`EvaluateAutoConnect(..., costItemCounts, ...)`（76-80行の`.Select(...)`インライン変換を削除）、(c)`ConsumeRequiredItems(costItemCounts, inventory)`に差し替える
- `RemoveBlockProtocol.cs`: `CreateRefundItems(blockMaster.RequiredItems)`→`CreateRefundItems(ConstructionCostService.ToItemCounts(blockMaster.RequiredItems))`の形へ（現物の呼び出し形を確認して合わせる）
- 他にヒットした呼び出し（プラン3クライアントの`ConstructionCostPreviewCalculator`は`ConstructionRequiredItemElement[]`を直接受ける独自実装のため対象外）

- [ ] **Step 5: テストPASS確認＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConstructionCostServiceTest|PlaceBlockProtocol|RemoveBlockProtocol"`
Expected: 全PASS（車両変換テストはTask 2のマスタ投入前ならスキップ理由を報告）

```bash
git add moorestech_server/ moorestech_client/
git commit -m "refactor: 建設コストサービスをItemCount正準形に一般化し車両マスタへ対応"
```

---

### Task 2: 列車車両プロトコル2本のGuid化とRemoveTrainCar全額返却

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceTrainCarOnRailProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/AttachTrainCarToUnitProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs`（`BuildRefundItems`: 121-139行）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/train.json`（テストデータ投入）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（99-127行）、`VanillaApiSendOnly.cs`（137行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`（暫定: 保持アイテム→Guid解決）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceTrainCarOnRailProtocolTest.cs`（更新）、`AttachTrainCarToUnitProtocolTest.cs`（新設）、`RemoveTrainCarProtocolTest.cs`（更新）

**Interfaces:**
- Consumes: Task 1の`ToItemCounts(TrainCarRequiredItemElement[])`/`HasRequiredItems`/`ConsumeRequiredItems`/`CreateRefundItems`、`MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(Guid, out TrainCarMasterElement)`、`IGameUnlockStateDataController.TrainCarUnlockStateInfos`
- Produces:
  - `PlaceTrainOnRailRequestMessagePack(RailPositionSnapshotMessagePack railPosition, Guid trainCarGuid, int playerId)`（`[Key(3)] Guid TrainCarGuid`。`HotBarSlot`/`InventorySlot`削除）
  - `AttachTrainCarToUnitRequestMessagePack(..., Guid trainCarGuid, ...)`（`[Key(4)] Guid TrainCarGuid`）
  - enum末尾追加: `PlaceTrainCarFailureType.NotUnlocked = 5, InsufficientItems = 6` / `AttachTrainCarFailureType.NotUnlocked = 6, InsufficientItems = 7`
  - クライアントAPI: `PlaceTrainOnRail(RailPosition railPosition, Guid trainCarGuid, CancellationToken ct)` / `AttachTrainCarToUnit(TrainUnitInstanceId, RailPosition, Guid trainCarGuid, bool, bool, CancellationToken)`

- [ ] **Step 1: テストマスタへ車両requiredItems/initialUnlockedを投入**

`Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/train.json`のtrainCars 1両目（itemGuid `20000000-...`）へ以下を追加（キー位置はitemGuidの直後）:

```json
"requiredItems": [
  { "itemGuid": "00000000-0000-0000-1234-000000000003", "count": 3 },
  { "itemGuid": "00000000-0000-0000-1234-000000000004", "count": 2 }
],
"initialUnlocked": true,
```

2両目はrequiredItems/initialUnlockedを付けない（未解放拒否テスト用。requiredItems未定義の車両はコスト0で設置できる仕様とし、未解放のみで拒否を検証する）。同様の変更を`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/train.json`にも適用する（クライアント統合テストの整合。1両目のみでよい）。

- [ ] **Step 2: 失敗するテストを書く**

`PlaceTrainCarOnRailProtocolTest.cs`の既存テスト`PlaceTrainOnRail_ValidRailAndItem_CreatesTrainUnit`を改修し、新テストを追加する。要点:

```csharp
        // 既存テストの「ホットバーへ車両アイテムを置く」準備を「素材をインベントリへ入れる」に差し替える
        // Replace the hot-bar item setup with construction materials in the inventory
        // 送信は new PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, trainCarGuid, playerId)

        [Test]
        public void 車両設置でrequiredItemsがインベントリ横断で消費される()
        {
            // 素材: Test3(1234-...0003)x3 + Test4(1234-...0004)x2 を複数スロットに分割投入
            // 設置成功後、両素材の合計所持数が0になることをAssert
        }

        [Test]
        public void 素材不足なら設置されずInsufficientItemsを返す()
        {
            // Test3x2のみ所持 → Success=false, FailureType=InsufficientItems, 列車未生成, 素材非消費
        }

        [Test]
        public void 未解放車両はNotUnlockedで拒否される()
        {
            // 2両目のtrainCarGuid（initialUnlocked無し）で送信 → FailureType=NotUnlocked
        }

        [Test]
        public void 存在しない車両GuidはItemNotFoundで拒否される()
        {
            // Guid.NewGuid()で送信 → FailureType=ItemNotFound
        }
```

（レール敷設・スナップショット生成の準備コードは既存テストのものを流用する。車両guidはtrain.jsonの`trainCarGuid`を直接参照）

`AttachTrainCarToUnitProtocolTest.cs`を新設し、creating-server-testsスキルの雛形に従って「連結成功でrequiredItems消費」「素材不足でInsufficientItems・編成不変」の2件を書く（編成の事前準備はPlaceTrainCarOnRailを成功させて作る）。

`RemoveTrainCarProtocolTest.cs`の`RemoveTrainCar_RefundsCarBlockAndContents_ToPlayerInventory`を「車両アイテム1個」→「requiredItems全額（Test3x3+Test4x2）＋コンテナ中身」の検証へ更新する。

- [ ] **Step 3: REDを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceTrainCarOnRail|AttachTrainCarToUnit|RemoveTrainCar"`
Expected: 新シグネチャ未実装によるコンパイルエラー（またはFAIL）

- [ ] **Step 4: PlaceTrainCarOnRailProtocolを改修**

(a) ペイロード: `[Key(3)] public int HotBarSlot`と`[IgnoreMember] InventorySlot`を削除し、以下へ置換。コンストラクタも`(RailPositionSnapshotMessagePack railPosition, Guid trainCarGuid, int playerId)`に変更:

```csharp
            [Key(3)] public Guid TrainCarGuid { get; set; }
```

(b) コンストラクタで`IGameUnlockStateDataController`を取得（`PlaceBlockProtocol.cs`と同じ`serviceProvider.GetService<IGameUnlockStateDataController>()`。using `Game.UnlockState`）。

(c) `ExecuteRequest`の「手持ちアイテムを取得し検証する」ブロック（55-63行）と「アイテムを消費する」（72-74行）を以下の流れに置換:

```csharp
                // 車両マスタとアンロック状態を検証する
                // Validate the train car master and its unlock state
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(data.TrainCarGuid, out var trainCarMaster))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.ItemNotFound);
                }
                if (!_gameUnlockStateDataController.TrainCarUnlockStateInfos[data.TrainCarGuid].IsUnlocked)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.NotUnlocked);
                }

                // 建設コストの充足をインベントリ横断で検証する
                // Validate construction cost across the whole inventory
                var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
                var mainInventory = inventoryData.MainOpenableInventory;
                var costItemCounts = ConstructionCostService.ToItemCounts(trainCarMaster.RequiredItems);
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, mainInventory.InventoryItems))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InsufficientItems);
                }
```

`TryCreateTrainUnit`は`ItemId trainItemId`引数を`TrainCarMasterElement trainCarMaster`に変更し、内部の`TryGetTrainCarMaster(itemId, ...)`を削除（マスタは検証済みのものを渡す）。ユニット生成成功後の消費は:

```csharp
                // 建設コストを消費する
                // Consume the construction cost
                ConstructionCostService.ConsumeRequiredItems(costItemCounts, mainInventory);
```

(d) enum末尾に`NotUnlocked = 5, InsufficientItems = 6,`を追加。

- [ ] **Step 5: AttachTrainCarToUnitProtocolを同型で改修**

`[Key(4)] int HotBarSlot`→`[Key(4)] Guid TrainCarGuid`（`InventorySlot`削除、コンストラクタ引数`int hotBarSlot`→`Guid trainCarGuid`）。`ExecuteRequest`の49-57行（手持ちアイテム検証）をStep 4(c)と同じ「マスタ解決→アンロック→コスト検証」に、82行の`SetItem`消費を`ConsumeRequiredItems`に置換。`TryCreateCarAndRailPosition`の`ItemId trainItemId`引数を`TrainCarMasterElement trainCarMaster`へ。enum末尾に`NotUnlocked = 6, InsufficientItems = 7,`を追加。

- [ ] **Step 6: RemoveTrainCarProtocolの返却を全額化**

`BuildRefundItems`の車両アイテム返却（127-128行）を置換:

```csharp
                // 建設コスト全額を返却する。コスト未定義マスタは旧仕様の車両アイテム1個を返す（プラン5で削除予定のフォールバック）
                // Refund the full construction cost; masters without cost fall back to one car item (removed in plan 5)
                var costItemCounts = ConstructionCostService.ToItemCounts(car.TrainCarMasterElement.RequiredItems);
                if (costItemCounts.Length > 0)
                {
                    result.AddRange(ConstructionCostService.CreateRefundItems(costItemCounts));
                }
                else
                {
                    var carItemId = MasterHolder.ItemMaster.GetItemId(car.TrainCarMasterElement.ItemGuid);
                    result.Add(ServerContext.ItemStackFactory.Create(carItemId, 1));
                }
```

- [ ] **Step 7: クライアント呼び出しを暫定更新（コンパイル維持）**

- `VanillaApiWithResponse.PlaceTrainOnRail`: 引数`int hotBarSlot`→`Guid trainCarGuid`、リクエスト生成を新コンストラクタへ
- `VanillaApiWithResponse.AttachTrainCarToUnit`: 同様
- `VanillaApiSendOnly.PlaceTrainOnRail(RailPosition, int hotBarSlot)`: 同様（未使用ならこの機会に削除してよい。grepで使用箇所確認）
- `TrainCarPlaceSystem.ManualUpdate`: クリック送信部の冒頭で暫定解決を行い、`RequestPlacementAsync(hit, carMaster.TrainCarGuid)`へ引数変更（Task 9で選択駆動に置換するまでの暫定）:

```csharp
            // 暫定: 保持アイテムから車両Guidを解決する（Task 9で選択駆動へ置換）
            // Interim: resolve the car guid from the held item (replaced by selection-driven flow in Task 9)
            if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(context.HoldingItemId, out var carMaster)) return;
```

- [ ] **Step 8: コンパイル＋テストPASS＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceTrainCarOnRail|AttachTrainCarToUnit|RemoveTrainCar|ConstructionCostService"`
Expected: 全PASS

```bash
git add moorestech_server/ moorestech_client/
git commit -m "feat: 列車車両の設置・連結・撤去をGuid指定と建設コスト消費に移行"
```

---

### Task 3: ElectricWireExtendのBlockId化とrequiredItems消費

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireExtendProtocol.cs`（ペイロードKey(6)）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs`（全面）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（電柱ブロックへrequiredItems投入）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（344行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireExtendRequestSender.cs`（`Extend`引数）、`ElectricWireExtendMode.cs`（暫定: スロット→BlockId解決）
- Test: `ElectricWireExtendProtocolTest.cs`（4件更新）、`ElectricWireExtendProtocolFailureTest.cs`（3件更新）

**Interfaces:**
- Consumes: Task 1のAPI、`PlaceBlockProtocol`のアンロック検証パターン
- Produces:
  - `ElectricWireExtendRequest`: `[Key(6)] public int PoleBlockIdInt`＋`[IgnoreMember] public BlockId PoleBlockId => new(PoleBlockIdInt)`（`PoleInventorySlot`削除）。`CreateExtendRequest(int playerId, Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId wireItemId)` / `CreateIsolatedPlaceRequest(int playerId, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId wireItemId)`
  - `ElectricWireExtendService.Execute(bool hasFromConnector, Vector3Int fromPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, ItemId wireItemId)`
  - クライアントAPI: `ExtendElectricWire(Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId wireItemId, CancellationToken ct)`
  - `ElectricWirePlacementFailureReason`末尾に`NotUnlocked`と`InsufficientItems`を追加（既存値の順序変更禁止）

- [ ] **Step 1: テストマスタの電柱ブロックへrequiredItemsを投入**

ForUnitTestのblocks.jsonから`"blockType": "ElectricPole"`のブロックを特定し（`grep -n 'ElectricPole' .../blocks.json`）、既存テストが使う電柱ブロックへ以下を追加する（既存キーの重複に注意 — 申し送り「休眠キーの罠」参照。`initialUnlocked`が未設定なら`true`も付ける）:

```json
"requiredItems": [ { "itemGuid": "00000000-0000-0000-1234-000000000003", "count": 2 } ],
"initialUnlocked": true,
```

既存の電線系テスト（`ElectricWirePlacement*`/`PlaceBlockProtocolTest`等）がこの電柱を使っている場合はコスト消費の影響を受けるため、`grep -rn "ElectricPole" moorestech_server/Assets/Scripts/Tests`で影響テストを棚卸しして期待値を更新する。

- [ ] **Step 2: テストを新仕様へ更新（RED）**

`ElectricWireExtendProtocolTest.cs`/`FailureTest.cs`の全7テストを更新する。変更パターン:
- 準備: 「電柱アイテムをスロットへ置く」→「電柱の建設素材（Test3x2）＋電線アイテムをインベントリへ入れる」
- 送信: `CreateExtendRequest(playerId, fromPos, poleSlot, placeInfo, wireItemId)`→`CreateExtendRequest(playerId, fromPos, poleBlockId, placeInfo, wireItemId)`（`poleBlockId`はForUnitTestModBlockIdの電柱ID）
- 検証: 電柱アイテム1個消費→素材Test3x2消費に変更。電線消費の検証は既存のまま
- `範囲外スロット指定は例外を出さず失敗応答を返す`は「未解放ブロック指定は失敗応答を返す」に書き換える（initialUnlocked無しの電柱を1つテストマスタへ追加してそのBlockIdを送る。応答は`NotUnlocked`）
- 追加: `素材不足なら設置されずInsufficientItemsで拒否される`（電線は足りるが建設素材が無いケース）

- [ ] **Step 3: サーバーを改修**

`ElectricWireExtendProtocol.cs`: ペイロードをInterfaces欄のとおり変更し、`Execute`呼び出しへ`request.PoleBlockId`を渡す。`CreateExtendRequest`/`CreateIsolatedPlaceRequest`とも引数`int poleInventorySlot`→`BlockId poleBlockId`に変更（他の引数は現行維持）。

`ElectricWireExtendService.cs`の`Execute`冒頭（27-45行）を置換:

```csharp
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 設置先が既に埋まっていないか確認する
            // Ensure the target position is not already occupied
            if (ServerContext.WorldBlockDatastore.Exists(polePlaceInfo.Position))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

            // ブロックの解放状態を検証する（解放判定は基底ブロック）
            // Validate the unlock state (judged on the base block)
            var baseBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(poleBlockId).BlockGuid;
            if (!ServerContext.GetService<IGameUnlockStateDataController>().BlockUnlockStateInfos[baseBlockGuid].IsUnlocked)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.NotUnlocked);

            // 縦向きオーバーライドを適用し電柱パラメータを解決する
            // Apply vertical override and resolve the pole parameter
            var blockId = poleBlockId.GetVerticalOverrideBlockId(polePlaceInfo.VerticalDirection);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not ElectricPoleBlockParam poleParam)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);

            // 建設コストの充足を検証する
            // Validate the construction cost
            var costItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.InsufficientItems);
```

（`Game.UnlockState`と`Server.Protocol.PacketResponse.Util.Construction`のusing追加。`ExecuteExtendWithOrigin`/`ExecuteIsolatedPlace`の引数から`poleInventorySlot`/`poleItem`を外し、`costItemCounts`を渡す）

`ExecuteExtendWithOrigin`の電線所持判定（97-98行）を「建設コスト予約込み」に変更:

```csharp
            // 電線合計＋建設コスト中の同一アイテム分を合算で判定する
            // Judge by total wires plus the same-item amount reserved by the construction cost
            var reservedWire = 0;
            foreach (var (itemId, count) in costItemCounts)
            {
                if (itemId == wireItemId) reservedWire += count;
            }
            if (ElectricWireSystemUtil.CountItem(inventory, wireItemId) < totalWire + reservedWire)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);
```

消費部（121-122行）を置換:

```csharp
            // 建設コストと張れた電線分を消費してから連結成分を再構築する
            // Consume the construction cost and successfully-placed wires, then rebuild connected components
            ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);
            ElectricWireSystemUtil.ConsumeItem(inventory, wireItemId, consumedWire);
```

`ExecuteIsolatedPlace`の`EvaluateAutoConnect`予約（134行）と消費（143行）を置換（**申し送りの必須事項: requiredItems由来の予約リストを渡す**）:

```csharp
            var plan = ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, polePlaceInfo.Position, polePlaceInfo.Direction, costItemCounts, inventory.InventoryItems);
            ...
            ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);
            ElectricWireAutoConnectService.ExecuteAutoConnect(plan, ServerContext.WorldBlockDatastore.GetBlock(selfConnector.BlockInstanceId), inventory);
```

`ElectricWirePlacementFailureReason`のenum定義ファイルを探し（`grep -rn "enum ElectricWirePlacementFailureReason"`）、末尾に`NotUnlocked, InsufficientItems,`を追加。`NoPoleItem`は「BlockId不正」用として残す（`IsBlock`検証は不要になったため、GetBlockMasterが解決できない不正IDのガードが必要なら`MasterHolder.BlockMaster`の存在チェックAPIの有無を確認し、無ければ`PlaceBlockProtocol`と同じく無ガードで統一する）。

- [ ] **Step 4: クライアント呼び出しを暫定更新**

- `VanillaApiWithResponse.ExtendElectricWire`: `int poleInventorySlot`→`BlockId poleBlockId`
- `ElectricWireExtendRequestSender.Extend(Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId wireItemId, ...)`へ引数変更
- `ElectricWireExtendMode.ExtendToEmptySpace`: `TryFindPoleSlot`成功後に`var poleBlockId = MasterHolder.BlockMaster.GetBlockId(poleItemId);`で暫定解決し、`Extend(fromPos, poleBlockId, placeInfo, wireItemId, ...)`を呼ぶ（Task 9で選択駆動へ置換）

- [ ] **Step 5: コンパイル＋テストPASS＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire"`
Expected: 全PASS（Step 1の影響棚卸しテスト含む）

```bash
git add moorestech_server/ moorestech_client/
git commit -m "feat: 電柱延長をBlockId指定と建設コスト消費に移行"
```

---

### Task 4: GearChainPoleExtendのBlockId化とrequiredItems消費

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GearChainPoleExtendProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/GearChain/GearChainPlacementEvaluator.cs`（ポール同一アイテム判定の一般化）
- Modify: ForUnitTest `blocks.json`（GearChainPoleブロックへrequiredItems投入）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（359-378行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/GearChainPoleConnect/Modes/GearChainPoleFrameInputCollector.cs`（50行付近・暫定）、`Parts/GearChainPoleExtendRequestSender.cs`（77-78行）、`GearChainPoleExtendSendCommand`定義（`PoleSlot`→`PoleBlockId`）
- Test: `GearChainPoleExtendProtocolTest.cs`（7件更新）

**Interfaces:**
- Produces:
  - `GearChainPoleExtendRequest`: `[Key(6)] public int PoleBlockIdInt`＋`[IgnoreMember] public BlockId PoleBlockId => new(PoleBlockIdInt)`。`CreateExtendRequest(int playerId, Vector3Int fromPolePos, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId chainItemId)` / `CreateIsolatedPlaceRequest(int playerId, BlockId poleBlockId, PlaceInfo polePlaceInfo)`
  - `GearChainPlacementEvaluator.EvaluatePlacement(..., ItemId chainItemId, IReadOnlyList<IItemStack> inventoryItems, IReadOnlyList<(ItemId itemId, int count)> reservedItemCounts)`（最終引数`ItemId placingPoleItemId`を予約リストへ一般化）
  - 失敗理由定数の追加: `GearChainPlacementEvaluator.NotUnlockedError = "NotUnlocked"` / `InsufficientItemsError = "InsufficientItems"`
  - クライアントAPI: `ExtendGearChainPole(Vector3Int fromPolePos, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId chainItemId, CancellationToken)` / `PlaceIsolatedGearChainPole(BlockId poleBlockId, PlaceInfo polePlaceInfo, CancellationToken)`

- [ ] **Step 1: テストマスタのGearChainPoleブロックへrequiredItems/initialUnlockedを投入**

Task 3 Step 1と同じ要領。既存テストが使うポールブロックに`requiredItems: [Test3 x2]`と`initialUnlocked: true`を追加。影響テストを`grep -rn "GearChainPole" moorestech_server/Assets/Scripts/Tests`で棚卸し。

- [ ] **Step 2: テストを新仕様へ更新（RED）**

7テストの変更パターン: 準備を素材投入へ、送信を`poleBlockId`指定へ、検証を「ポールアイテム1個消費→素材消費」へ。`ExtendFailsWithoutPoleItem`は「素材無しはInsufficientItemsで失敗し状態不変」に書き換え、「未解放ポールはNotUnlockedで失敗」を追加（未解放ポールブロックをテストマスタへ1つ追加）。チェーン距離比例消費の検証（`ExtendPlacesConnectsAndConsumesItems`）は既存のまま素材消費の検証を加える。

- [ ] **Step 3: サーバーを改修**

`GearChainPoleExtendProtocol.GetResponse`の47-55行（スロット検証〜ポールパラメータ解決）を置換:

```csharp
            // ブロックの解放状態を検証する（解放判定は基底ブロック）
            // Validate the unlock state (judged on the base block)
            var baseBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(request.PoleBlockId).BlockGuid;
            if (!_gameUnlockStateDataController.BlockUnlockStateInfos[baseBlockGuid].IsUnlocked) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.NotUnlockedError);

            // 縦向きオーバーライドを適用しポールパラメータを解決する
            // Apply vertical override and resolve the pole parameter
            var blockId = request.PoleBlockId.GetVerticalOverrideBlockId(request.PolePlaceInfo.VerticalDirection);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not GearChainPoleBlockParam poleParam) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.NoPoleItemError);

            // 建設コストの充足を検証する
            // Validate the construction cost
            var costItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems)) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.InsufficientItemsError);
```

（`IGameUnlockStateDataController`をコンストラクタでDI取得。67行の`EvaluatePlacement(..., poleItemStack.Id)`は`(..., costItemCounts)`へ。88-89行のポール1個消費を`ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);`へ）

`GearChainPlacementEvaluator`: 最終引数`ItemId placingPoleItemId`（チェーン=ポール同一アイテムの+1予約に使用）を`IReadOnlyList<(ItemId itemId, int count)> reservedItemCounts`へ一般化し、チェーン所持判定を「チェーンコスト＋予約リスト中の同一アイテム数」で行う（Task 3の`reservedWire`と同じ形）。定数`NotUnlockedError`/`InsufficientItemsError`を既存定数群（18-24行）に追加。

- [ ] **Step 4: クライアント呼び出しを暫定更新**

- `VanillaApiWithResponse.ExtendGearChainPole`/`PlaceIsolatedGearChainPole`: `int poleInventorySlot`→`BlockId poleBlockId`
- `GearChainPoleExtendSendCommand`（定義ファイルをgrepで特定）: `PoleSlot`（int）→`PoleBlockId`（BlockId）
- `GearChainPoleExtendRequestSender.Send`: `command.PoleSlot`→`command.PoleBlockId`
- `GearChainPoleFrameInputCollector.CollectPlaceExtend`（50行）: `PoleInventorySlot = HotBarSlotToInventorySlot(...)`→`PoleBlockId = MasterHolder.BlockMaster.GetBlockId(poleBlockMaster.BlockGuid)`（`GearChainPolePlaceExtendInput`のフィールドも改名）。`Decide`側（`GearChainPolePlaceExtendMode`）の`PoleSlot`参照も追随

- [ ] **Step 5: コンパイル＋テストPASS＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearChain"`
Expected: 全PASS（クライアントのGearChainPoleテストも含めて回す）

```bash
git add moorestech_server/ moorestech_client/
git commit -m "feat: 歯車チェーンポール延長をBlockId指定と建設コスト消費に移行"
```

---

### Task 5: RailConnectWithPlacePierのBlockId化・事前検証化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectWithPlacePierProtocol.cs`（全面改修）
- Modify: ForUnitTest `blocks.json`（レール橋脚ブロック=blockType TrainRail へrequiredItems投入）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（332行付近）、`VanillaApiSendOnly.cs`（131行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs`（100-111行・166-177行、暫定）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RailConnectWithPlacePierProtocolTest.cs`（新設）

**Interfaces:**
- Produces:
  - `RailConnectWithPlacePierRequest`: `[Key(6)] public int PierBlockIdInt`＋`[IgnoreMember] public BlockId PierBlockId => new(PierBlockIdInt)`。`Create(int playerId, int fromNodeId, Guid fromGuid, BlockId pierBlockId, PlaceInfo placeInfo, Guid railTypeGuid)`
  - クライアントAPI: `PlaceRailWithPier(int fromNodeId, Guid fromGuid, BlockId pierBlockId, PlaceInfo pierPlaceInfo, Guid railTypeGuid, CancellationToken)`
  - 処理順の変更: 「解放→橋脚コスト＋レール距離比例コストの合算事前検証→設置→接続→消費」（現行の「設置後にレール検証で失敗応答」というブロック残留バグを解消する）

- [ ] **Step 1: テストマスタのレール橋脚へrequiredItems/initialUnlockedを投入**

ForUnitTestのblocks.jsonの`"blockType": "TrainRail"`ブロックに`requiredItems: [Test3 x2]`と`initialUnlocked: true`を追加。影響テスト棚卸し（`grep -rn "TrainRail" moorestech_server/Assets/Scripts/Tests | grep -i "pier\|rail"`）。

- [ ] **Step 2: 失敗するテストを書く（新設）**

creating-server-testsスキルの雛形で`RailConnectWithPlacePierProtocolTest.cs`を新設。レールノードの準備は既存の`RailConnectionEditProtocol`系テスト（あれば）や`TrainRail`系CombinedTestの準備コードを参考にする。テスト4件:

```csharp
        [Test] public void 橋脚設置と接続で橋脚コストとレールが距離比例で消費される() { }
        [Test] public void 橋脚素材不足なら設置されず失敗応答を返す() { }
        [Test] public void レール素材不足なら設置されず失敗応答を返す() { }   // 現行は設置後に失敗しブロックが残る。新実装で残らないことを検証
        [Test] public void 未解放橋脚は設置されず失敗応答を返す() { }
```

- [ ] **Step 3: サーバーを改修**

`GetResponse`の43-83行を以下の流れへ書き換える:

```csharp
            // 解放状態と橋脚コストを検証する（解放判定は基底ブロック）
            // Validate unlock state and pier cost (unlock judged on the base block)
            var baseBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(request.PierBlockId).BlockGuid;
            if (!_gameUnlockStateDataController.BlockUnlockStateInfos[baseBlockGuid].IsUnlocked) return RailConnectWithPlacePierResponse.CreateFailedResponse();

            var blockId = request.PierBlockId.GetVerticalOverrideBlockId(request.PierPlaceInfo.VerticalDirection);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var pierItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);

            // レール素材の距離比例コストを橋脚コストと合算して事前検証する
            // Pre-validate the distance-based rail cost combined with the pier cost
            // NOTE: toNodeは設置後にしか取れないため、距離は設置予定位置から暫定ノードで算出できない。
            //       既存実装と同じくRailComponentのBackNode位置が設置位置に一致する前提で、fromNode→PierPlaceInfo.Positionの
            //       レール長を求めるユーティリティが無い場合は、TryResolveRailItemForPlacementの距離引数に
            //       既存のGetRailLength(fromNode, toNode)を使うため設置後検証→失敗時ロールバック（RemoveBlock）方式にする。
            //       （GearChainPoleExtendProtocol.cs:78-84の「事前検証済みだが失敗時はブロックを取り消す」前例に従う）
```

実装方針（上記NOTEの確定形）: レール長は設置後の`toNode`からしか正確に取れないため、**「橋脚コスト事前検証→設置→レールコスト解決→不足なら`RemoveBlock`でロールバックし失敗応答→接続→消費」**とする。消費部:

```csharp
            // 橋脚コストと接続に使ったレールを消費する
            // Consume the pier cost and the rails used by the connection
            ConstructionCostService.ConsumeRequiredItems(pierItemCounts, inventoryData.MainOpenableInventory);
            if (connectResult && request.RailTypeGuid != Guid.Empty)
            {
                var railItemId = MasterHolder.ItemMaster.GetItemId(railItem.ItemGuid);
                ElectricWireSystemUtil.ConsumeItem(inventoryData.MainOpenableInventory, railItemId, requiredCount);
            }
```

（67-78行の手書きスロット走査ループと78行のthrowを`ElectricWireSystemUtil.ConsumeItem`へ置換 — レール消費のコピペ重複解消。81-83行の橋脚アイテム1個消費は削除。`IGameUnlockStateDataController`をコンストラクタでDI取得）

- [ ] **Step 4: クライアント呼び出しを暫定更新**

- `VanillaApiWithResponse.PlaceRailWithPier`/`VanillaApiSendOnly.PlaceRailWithPier`: `int pierInventorySlot`→`BlockId pierBlockId`
- `TrainRailConnectSystem`: 100-101行で得た`pierBlockId`を111行の`SendConnectRailWithPlacePierProtocol(placeInfo, previewData.RailTypeGuid, pierBlockId)`へ渡す（引数型を`BlockId`へ変更。インベントリ検索はTask 9で除去するまで暫定維持）

- [ ] **Step 5: コンパイル＋テストPASS＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RailConnect"`
Expected: 全PASS

```bash
git add moorestech_server/ moorestech_client/
git commit -m "feat: 橋脚付きレール接続をBlockId指定と建設コスト消費に移行"
```

---

### Task 6: placeSystem.ymlの接続ツールエントリ化（スキーマ転換）

**Files:**
- Modify: `VanillaSchema/placeSystem.yml`（全面書き換え）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（dummyText変更でSourceGenerator起動）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/PlaceSystemMasterUtil.cs`（バリデーション書き換え）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/placeSystem.json`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/placeSystem.json`（別リポジトリ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs`（UsePlaceItemsマッチング削除）

**Interfaces:**
- Produces: `PlaceSystemMasterElement`の新形: `PlaceMode`（enum 3種: TrainRailConnect/GearChainPoleConnect/ElectricWireConnect）/`Name`（string）/`IconItemGuid`（Guid）/`PlaceBlockGuid`（Guid?、optional）/`SortPriority`（int）。`UsePlaceItems`/`Priority`は消滅
- Consumes: edit-schemaスキルの手順（yaml_spec.md確認、JSONデータ全配置先更新、手動`new PlaceSystemMasterElement(`箇所のgrep）

- [ ] **Step 1: スキーマを書き換える**

`VanillaSchema/placeSystem.yml`全体を以下に置換（編集前にedit-schemaスキルのyaml_spec.mdを確認）:

```yaml
# NOTE このyamlに記述されているスキーマのコード、JSONローダーはSourceGeneratorによって自動生成されます。そのため、このスキーマのクラスやローダーを実装する必要はありません。
# NOTE The schema code and JSON loader described in this YAML are automatically generated by the SourceGenerator. Therefore, you do not need to implement the classes or loaders for this schema.

# ビルドメニューの接続ツールエントリ定義。placeBlockGuidは空きスペース延長時に自動設置するブロック（レール接続→橋脚、電線接続→電柱）。
# Build-menu connect tool entry definitions. placeBlockGuid is the block auto-placed when extending into empty space (rail connect -> pier, wire connect -> pole).

id: placeSystem
type: object
isDefaultOpen: true
properties:
- key: data
  type: array
  openedByDefault: true
  overrideCodeGeneratePropertyName: PlaceSystemMasterElement
  items:
    type: object
    properties:
    - key: placeMode
      type: enum
      options:
      - TrainRailConnect
      - GearChainPoleConnect
      - ElectricWireConnect
    - key: name
      type: string
    - key: iconItemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
    - key: placeBlockGuid
      type: uuid
      optional: true
      foreignKey:
        schemaId: blocks
        foreignKeyIdPath: /data/[*]/blockGuid
        displayElementPath: /data/[*]/name
    - key: sortPriority
      type: integer
```

`_CompileRequester.cs`の`dummyText`を新しい値に変更してSourceGeneratorを起動する。

- [ ] **Step 2: 全JSONデータを新形式へ更新**

旧プロパティ残存確認: `grep -rn '"usePlaceItems"\|"priority"' --include='placeSystem.json' . ../moorestech_master mooresmaster 2>/dev/null`（mooresmaster/mooresmaster.SandBoxにplaceSystem.jsonがあればそれも更新）

(a) ForUnitTest `placeSystem.json`（iconItemGuid/placeBlockGuidはテストマスタの実在guidを使う。電柱・ポールのblockGuidは`grep '"blockType"' .../blocks.json`で特定）:

```json
{
  "data": [
    { "placeMode": "TrainRailConnect", "name": "レール敷設", "iconItemGuid": "<テストのレール素材itemGuid>", "placeBlockGuid": "<テストのTrainRail橋脚blockGuid>", "sortPriority": 100 },
    { "placeMode": "ElectricWireConnect", "name": "電線接続", "iconItemGuid": "00000000-0000-0000-4649-000000000001", "placeBlockGuid": "<テストのElectricPole blockGuid>", "sortPriority": 110 },
    { "placeMode": "GearChainPoleConnect", "name": "歯車チェーン接続", "iconItemGuid": "b15c8f72-<現物の続き>", "sortPriority": 120 }
  ]
}
```

(b) EditModeInPlayingTestの`placeSystem.json`は`{"data": []}`のまま（変更不要）。

(c) 本番 `/Users/katsumi/moorestech_master/.../placeSystem.json`（mooreseditor停止を`pgrep -fl mooreseditor`で確認してから）:

```json
{
  "data": [
    { "placeMode": "TrainRailConnect", "name": "レール敷設", "iconItemGuid": "5be3a22c-129f-4d3c-a29e-bab3150ca2f4", "placeBlockGuid": "fbefeea3-0a66-4064-b1ff-a22ebf92126d", "sortPriority": 100 },
    { "placeMode": "ElectricWireConnect", "name": "電線接続", "iconItemGuid": "<blocks.json先頭のelectricWireItemsのitemGuid>", "placeBlockGuid": "019e158c-bf90-739a-bec1-ad9728f5b0d0", "sortPriority": 110 },
    { "placeMode": "GearChainPoleConnect", "name": "歯車チェーン接続", "iconItemGuid": "8412fa32-186d-41a9-9627-69874f341c6e", "sortPriority": 120 }
  ]
}
```

（電線のiconItemGuidは`python3 -c "import json;print(json.load(open('...blocks.json'))['electricWireItems'][0]['itemGuid'])"`等で確認。旧データのTrainRail/TrainCarエントリは削除される — 役割はブロック/車両エントリへ移行済み）

- [ ] **Step 3: バリデータとC#参照を更新**

- `PlaceSystemMasterUtil.cs`: `UsePlaceItems`のitems実在チェックを「`IconItemGuid`のitems実在チェック＋`PlaceBlockGuid`（非null時）のblocks実在チェック」へ書き換える（既存のチェック実装形式に合わせる。foreignKey追加のためバリデーション必須 — edit-schemaスキルのCRITICAL事項）
- `PlaceSystemSelector.cs`: `GetPlaceSystemElement`ローカル関数と46-67行のマッチング分岐（UsePlaceItems検索＋GearChainPoleItemFinderフォールバック）を削除する。特殊システムは一時的に選択不能になる（Task 8で選択駆動として復活）。`SelectedBlockId`分岐と`EmptyPlaceSystem`は維持。不要になったusing（`System.Linq`/`Mooresmaster.Model.PlaceSystemModule`/`GearChainPoleConnect.Parts`等）を整理
- `grep -rn "UsePlaceItems\|new PlaceSystemMasterElement(" moorestech_server moorestech_client --include='*.cs'`で残存参照を洗い、すべて更新する

- [ ] **Step 4: コンパイル＋回帰＋コミット（moorestech_master側も）**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|Master"`
Expected: エラー0件・全PASS

```bash
git -C /Users/katsumi/moorestech_master add server_v8/mods/moorestechAlphaMod_8/master/placeSystem.json
git -C /Users/katsumi/moorestech_master commit -m "feat: placeSystemを接続ツールエントリ定義へ転換"
# .moorestech-external-revisions.json のピンを新コミットハッシュへ更新してから
git add VanillaSchema/ moorestech_server/ moorestech_client/ .moorestech-external-revisions.json
git commit -m "feat: placeSystemスキーマを接続ツールエントリ定義へ転換"
```

---

### Task 7: PlacementSelection拡張とビルドメニュー3種エントリ化

**Files:**
- Rename+Modify: `.../PlaceSystem/BlockPlacementSelection.cs` → `.../PlaceSystem/PlacementSelection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Modify: `.../PlaceSystem/IPlaceSystem.cs`（Contextへ新フィールド追加。旧フィールドはまだ残す）
- Modify: `.../PlaceSystem/PlaceSystemStateController.cs`（選択変化検知の追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（`BlockPlacementSelection`→`PlacementSelection`のDI名変更）

**Interfaces:**
- Produces（Task 8/9が使う）:
  - `enum PlacementSelectionType { None, Block, TrainCar, ConnectTool }`
  - `PlacementSelection`: `PlacementSelectionType SelectionType` / `BlockId? SelectedBlockId` / `Guid SelectedTrainCarGuid` / `string SelectedConnectPlaceMode`（`PlaceSystemMasterElement.PlaceModeConst`値）/ `SetSelectedBlock(BlockId)` / `SetSelectedTrainCar(Guid)` / `SetSelectedConnectTool(string)` / `ClearSelection()`
  - `PlaceSystemUpdateContext`追加フィールド: `PlacementSelectionType SelectionType` / `Guid SelectedTrainCarGuid` / `string SelectedConnectPlaceMode` / `bool IsSelectionChanged`
  - `BuildMenuView.TryConsumeSelectedEntry(out BuildMenuEntry entry)`
- Consumes: Task 6の新`PlaceSystemMasterElement`、プラン3の`ItemSlotView`/`ClientContext.ItemImageContainer`、`IGameUnlockStateData.BlockUnlockStateInfos/TrainCarUnlockStateInfos`、`MasterHolder.TrainUnitMaster.Train.TrainCars`

- [ ] **Step 1: PlacementSelectionを作成（旧BlockPlacementSelectionを置換）**

```csharp
using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public enum PlacementSelectionType
    {
        None,
        Block,
        TrainCar,
        ConnectTool,
    }

    /// <summary>
    /// ビルドメニューで選択中の設置対象（ブロック・車両・接続ツール）
    /// The build-menu selection: a block, a train car, or a connect tool
    /// </summary>
    public class PlacementSelection
    {
        public PlacementSelectionType SelectionType { get; private set; } = PlacementSelectionType.None;
        public BlockId? SelectedBlockId { get; private set; }
        public Guid SelectedTrainCarGuid { get; private set; }
        public string SelectedConnectPlaceMode { get; private set; }

        public void SetSelectedBlock(BlockId blockId)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.Block;
            SelectedBlockId = blockId;
        }

        public void SetSelectedTrainCar(Guid trainCarGuid)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.TrainCar;
            SelectedTrainCarGuid = trainCarGuid;
        }

        public void SetSelectedConnectTool(string placeMode)
        {
            ClearSelection();
            SelectionType = PlacementSelectionType.ConnectTool;
            SelectedConnectPlaceMode = placeMode;
        }

        public void ClearSelection()
        {
            SelectionType = PlacementSelectionType.None;
            SelectedBlockId = null;
            SelectedTrainCarGuid = Guid.Empty;
            SelectedConnectPlaceMode = null;
        }
    }
}
```

`grep -rn "BlockPlacementSelection" moorestech_client --include='*.cs'`で全参照（MainGameStarter/BuildMenuState/PlaceSystemStateController/テスト）を`PlacementSelection`へ改名する。旧ファイルは削除（.metaはUnityに任せる — ファイル名だけ変えて中身を置換する方式なら.meta移動もUnityが処理するため、`git mv`でリネームしてから中身を書き換えるのが安全）。

- [ ] **Step 2: BuildMenuEntryとBuildMenuEntryCatalogを作成**

`BuildMenuEntry.cs`:

```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの1エントリ（ブロック・車両・接続ツールのいずれか）
    /// One build-menu entry: a block, a train car, or a connect tool
    /// </summary>
    public readonly struct BuildMenuEntry
    {
        public readonly PlacementSelectionType EntryType;
        public readonly BlockId BlockId;
        public readonly Guid TrainCarGuid;
        public readonly string ConnectPlaceMode;
        public readonly ItemId IconItemId;
        public readonly string ToolTipText;

        public BuildMenuEntry(PlacementSelectionType entryType, BlockId blockId, Guid trainCarGuid, string connectPlaceMode, ItemId iconItemId, string toolTipText)
        {
            EntryType = entryType;
            BlockId = blockId;
            TrainCarGuid = trainCarGuid;
            ConnectPlaceMode = connectPlaceMode;
            IconItemId = iconItemId;
            ToolTipText = toolTipText;
        }
    }
}
```

`BuildMenuEntryCatalog.cs`（エントリ列挙の静的ロジック。現行`BuildMenuView.RebuildBlockList`のブロック列挙・`IsUnlocked`・`CreateToolTipText`をここへ移設して一般化）:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.TrainModule;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニューの表示エントリ一覧を組み立てる（ブロック→車両→接続ツールの順）
    /// Builds the list of build-menu entries: blocks, then train cars, then connect tools
    /// </summary>
    public static class BuildMenuEntryCatalog
    {
        public static List<BuildMenuEntry> CreateEntries(IGameUnlockStateData unlockState)
        {
            var entries = new List<BuildMenuEntry>();

            // 解放済みブロックをソート順に列挙する
            // Enumerate unlocked blocks in sort order
            var unlockedBlocks = MasterHolder.BlockMaster.Blocks.Data
                .Where(b => IsBlockUnlocked(unlockState, b))
                .OrderBy(b => b.SortPriority ?? 0)
                .ThenBy(b => b.Name);
            foreach (var blockMaster in unlockedBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
                var iconItemId = MasterHolder.BlockMaster.GetItemId(blockId);
                entries.Add(new BuildMenuEntry(PlacementSelectionType.Block, blockId, default, null, iconItemId, CreateBlockToolTip(blockMaster)));
            }

            // 解放済み車両を列挙する
            // Enumerate unlocked train cars
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (!unlockState.TrainCarUnlockStateInfos.TryGetValue(trainCar.TrainCarGuid, out var state) || !state.IsUnlocked) continue;
                var iconItemId = MasterHolder.ItemMaster.GetItemId(trainCar.ItemGuid);
                entries.Add(new BuildMenuEntry(PlacementSelectionType.TrainCar, default, trainCar.TrainCarGuid, null, iconItemId, CreateTrainCarToolTip(trainCar)));
            }

            // 接続ツールは常時表示する
            // Connect tools are always visible
            var connectTools = MasterHolder.PlaceSystemMaster.PlaceSystem.Data.OrderBy(e => e.SortPriority);
            foreach (var tool in connectTools)
            {
                var iconItemId = MasterHolder.ItemMaster.GetItemId(tool.IconItemGuid);
                entries.Add(new BuildMenuEntry(PlacementSelectionType.ConnectTool, default, default, tool.PlaceMode, iconItemId, tool.Name));
            }

            return entries;

            #region Internal

            bool IsBlockUnlocked(IGameUnlockStateData state, BlockMasterElement blockMaster)
            {
                return state.BlockUnlockStateInfos.TryGetValue(blockMaster.BlockGuid, out var info) && info.IsUnlocked;
            }

            string CreateBlockToolTip(BlockMasterElement blockMaster)
            {
                var builder = new StringBuilder(blockMaster.Name);
                AppendRequiredItems(builder, ConstructionCostTexts(blockMaster.RequiredItems?.Select(r => (r.ItemGuid, r.Count))));
                return builder.ToString();
            }

            string CreateTrainCarToolTip(TrainCarMasterElement trainCar)
            {
                var builder = new StringBuilder(MasterHolder.ItemMaster.GetItemMaster(trainCar.ItemGuid).Name);
                AppendRequiredItems(builder, ConstructionCostTexts(trainCar.RequiredItems?.Select(r => (r.ItemGuid, r.Count))));
                return builder.ToString();
            }

            IEnumerable<string> ConstructionCostTexts(IEnumerable<(System.Guid itemGuid, int count)> requiredItems)
            {
                if (requiredItems == null) yield break;
                foreach (var (itemGuid, count) in requiredItems)
                {
                    yield return $"{MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name} x{count}";
                }
            }

            void AppendRequiredItems(StringBuilder builder, IEnumerable<string> costTexts)
            {
                foreach (var text in costTexts) builder.Append('\n').Append(text);
            }

            #endregion
        }
    }
}
```

（注: 車両名は車両マスタにnameフィールドが無いためアイテム名で代用 — 申し送りのMinor「車両name追加は別件」と整合。プラン5でアイテム削除時に再考する）

- [ ] **Step 3: BuildMenuViewをエントリ駆動へ書き換える**

`RebuildBlockList`→`RebuildEntryList`とし、`BuildMenuEntryCatalog.CreateEntries(_gameUnlockStateData)`の結果でスロット生成、クリック購読は`_clickedEntry = entry`（`BuildMenuEntry?`フィールド）。`TryConsumeSelectedBlock`→`TryConsumeSelectedEntry(out BuildMenuEntry entry)`へ改名。既存の`IsUnlocked`/`CreateToolTipText`はカタログへ移設したので削除（200行制約内に収まる）。

- [ ] **Step 4: BuildMenuStateの選択反映を3種対応にする**

```csharp
            // 選択が確定したら種別に応じて選択状態を設定し設置モードへ遷移する
            // On selection, set the placement selection by entry type and transition to placement mode
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
            {
                switch (entry.EntryType)
                {
                    case PlacementSelectionType.Block:
                        _placementSelection.SetSelectedBlock(entry.BlockId);
                        break;
                    case PlacementSelectionType.TrainCar:
                        _placementSelection.SetSelectedTrainCar(entry.TrainCarGuid);
                        break;
                    case PlacementSelectionType.ConnectTool:
                        _placementSelection.SetSelectedConnectTool(entry.ConnectPlaceMode);
                        break;
                }
                return new UITransitContext(UIStateEnum.PlaceBlock);
            }
```

（フィールド名`_blockPlacementSelection`→`_placementSelection`。`ClearSelection`が呼ばれている既存箇所をgrepし、遷移設計（PlaceBlockState退出時のクリア等）が壊れないことを確認）

- [ ] **Step 5: Contextへ新フィールドを追加（旧フィールド残置）**

`PlaceSystemUpdateContext`に`SelectionType`/`SelectedTrainCarGuid`/`SelectedConnectPlaceMode`/`IsSelectionChanged`のreadonlyフィールドとコンストラクタ末尾引数を追加（デフォルト引数禁止 — `new PlaceSystemUpdateContext(`の全呼び出し側をgrepして引数追加。テストは`PlacementSelectionType.None, Guid.Empty, null, false`相当を渡す）。

`PlaceSystemStateController.CreateContext`で`_placementSelection`から各値を渡し、選択変化検知を追加:

```csharp
            // 選択内容の変化を検知する（車両プレビューのリセット等に使う）
            // Detect selection changes (used to reset previews such as the train car preview)
            var isSelectionChanged = _lastSelectionType != _placementSelection.SelectionType
                                     || _lastSelectedBlockId != _placementSelection.SelectedBlockId
                                     || _lastSelectedTrainCarGuid != _placementSelection.SelectedTrainCarGuid
                                     || _lastSelectedConnectPlaceMode != _placementSelection.SelectedConnectPlaceMode;
```

（`_last...`フィールド4本を追加し`CreateContext`末尾で更新）

- [ ] **Step 6: コンパイル＋回帰＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|BuildMenu|GearChainPole"`
Expected: エラー0件・全PASS

```bash
git add moorestech_client/
git commit -m "feat: ビルドメニューをブロック・車両・接続ツールの3種エントリ化"
```

---

### Task 8: Selector新分岐とTrainRail/TrainCarの選択駆動化

**Files:**
- Modify: `.../PlaceSystem/PlaceSystemSelector.cs`（`GetCurrentPlaceSystem`全面書き換え）
- Modify: `.../PlaceSystem/TrainRail/TrainRailPlaceSystem.cs`＋`TrainRailPlaceSystemService.cs`（ItemId→BlockId駆動）
- Modify: `.../PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`、`TrainCarPlacementDetector.cs`（`TryDetect(ItemId)`→`TryDetect(Guid)`）、`TrainCarPreviewController.cs`（`ShowPreview(ItemId, ...)`→`ShowPreview(Guid, ...)`）

**Interfaces:**
- Consumes: Task 7の`context.SelectionType`/`SelectedBlockId`/`SelectedTrainCarGuid`/`IsSelectionChanged`、`MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(Guid, ...)`、プラン3の`PlaceSystemUtil.SendPlaceBlockProtocol(List<PlaceInfo>, BlockId)`
- Produces: 選択種別→設置システムの確定ルーティング（Task 9の接続3システムはConnectTool分岐で選ばれる）

- [ ] **Step 1: PlaceSystemSelectorを選択駆動へ書き換える**

`GetCurrentPlaceSystem`を以下へ置換（Task 6でマッチング削除済みの状態から）:

```csharp
        public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context)
        {
            switch (context.SelectionType)
            {
                case PlacementSelectionType.Block:
                {
                    // レール橋脚と歯車ポールはblockTypeで専用システムへ振り分ける
                    // Route rail piers and gear chain poles to their dedicated systems by blockType
                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(context.SelectedBlockId.Value);
                    if (blockMaster.BlockType == BlockMasterElement.BlockTypeConst.TrainRail) return _trainRailPlaceSystem;
                    if (blockMaster.BlockType == BlockMasterElement.BlockTypeConst.GearChainPole) return _gearChainPoleConnectSystem;
                    return _commonBlockPlaceSystem;
                }
                case PlacementSelectionType.TrainCar:
                    return _trainCarPlaceSystem;
                case PlacementSelectionType.ConnectTool:
                    return context.SelectedConnectPlaceMode switch
                    {
                        PlaceSystemMasterElement.PlaceModeConst.TrainRailConnect => _trainRailConnectSystem,
                        PlaceSystemMasterElement.PlaceModeConst.GearChainPoleConnect => _gearChainPoleConnectSystem,
                        PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect => _electricWireConnectSystem,
                        _ => EmptyPlaceSystem,
                    };
                default:
                    return EmptyPlaceSystem;
            }
        }
```

- [ ] **Step 2: TrainRailPlaceSystemを選択駆動＋新プロトコルへ**

`ManualUpdate`の30-37行を置換:

```csharp
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            var blockId = context.SelectedBlockId.Value;
            var placeInfo = _trainRailPlaceSystemService.ManualUpdate(blockId);
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

            PlaceSystemUtil.SendPlaceBlockProtocol(new List<PlaceInfo> { placeInfo }, blockId);
        }
```

`TrainRailPlaceSystemService.ManualUpdate(ItemId itemId)`のシグネチャを`ManualUpdate(BlockId blockId)`へ変更し、内部の`GetBlockId(itemId)`/`GetBlockMaster(itemId)`相当をBlockId直参照に単純化（現物を読んで追随。他の呼び出し元は`TrainRailConnectSystem`のみ — Task 9で更新するため、このタスクでは`TrainRailConnectSystem`の104行も`ManualUpdate(pierBlockId)`へ機械的に追随させる）。不要になった`ILocalPlayerInventory`/`PlayerInventoryConst`依存を`TrainRailPlaceSystem`から削除。

- [ ] **Step 3: TrainCarPlaceSystemをGuid駆動へ**

- `ITrainCarPlacementDetector.TryDetect(ItemId holdingItemId, ...)`→`TryDetect(Guid trainCarGuid, ...)`。実装（`TrainCarPlacementDetector.cs:105`）内の`TryGetTrainCarMaster(itemId, ...)`を`TryGetTrainCarMaster(trainCarGuid, ...)`へ
- `TrainCarPreviewController.ShowPreview(ItemId itemId, ...)`→`ShowPreview(Guid trainCarGuid, ...)`（内部のマスタ解決を同様に変更）
- `TrainCarPlaceSystem.ManualUpdate`: `context.IsSelectSlotChanged`→`context.IsSelectionChanged`、`TryDetect(context.HoldingItemId, ...)`→`TryDetect(context.SelectedTrainCarGuid, ...)`、`ShowPreview(context.HoldingItemId, ...)`→`ShowPreview(context.SelectedTrainCarGuid, ...)`、Task 2 Step 7の暫定解決コードを削除し`RequestPlacementAsync(hit, context.SelectedTrainCarGuid)`へ

- [ ] **Step 4: コンパイル＋回帰＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|TrainCar|TrainRail"`
Expected: エラー0件・全PASS

```bash
git add moorestech_client/
git commit -m "feat: レール橋脚と列車車両の設置をビルドメニュー選択駆動に切替"
```

---

### Task 9: 接続3システムの選択駆動化と素材自動選択

**Files:**
- Create: `.../PlaceSystem/ElectricWireConnect/ElectricWireItemAutoSelector.cs`
- Create: `.../PlaceSystem/TrainRailConnect/TrainRailItemAutoSelector.cs`
- Create: `.../PlaceSystem/Util/ConnectToolMasterUtil.cs`
- Modify: `ElectricWireExtendMode.cs`（38行・81行）、`TrainRailConnectSystem.cs`（81-112行・93/105/134行）、`GearChainPoleConnectSystem.cs`（50-59行）、`GearChainPoleFrameInputCollector.cs`（76/88/101行）

**Interfaces:**
- Produces:
  - `static ItemId ElectricWireItemAutoSelector.FindOwnedWireItemId(ILocalPlayerInventory inventory)`（`Blocks.ElectricWireItems`マスタ定義順・最初に所持しているもの。未所持は`ItemMaster.EmptyItemId`）
  - `static ItemId TrainRailItemAutoSelector.FindOwnedRailItemId(ILocalPlayerInventory inventory)`（`TrainUnitMaster.GetRailItems()`定義順。同上）
  - `static bool ConnectToolMasterUtil.TryGetPlaceBlock(string placeMode, out BlockId blockId, out BlockMasterElement blockMaster)`（placeSystemマスタの該当エントリの`PlaceBlockGuid`を解決。未定義はfalse）
- Consumes: Task 6の`PlaceSystemMasterElement.PlaceBlockGuid`、既存`GearChainPoleItemFinder.FindOwnedChainItemId`

- [ ] **Step 1: 自動選択ユーティリティ3つを作成**

`ElectricWireItemAutoSelector.cs`:

```csharp
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 敷設に使う電線アイテムをマスタ定義順で自動選択する（サーバーの自動接続と同一ルール）
    /// Auto-select the wire item in master definition order, matching the server auto-connect rule
    /// </summary>
    public static class ElectricWireItemAutoSelector
    {
        public static ItemId FindOwnedWireItemId(ILocalPlayerInventory inventory)
        {
            foreach (var wireItem in MasterHolder.BlockMaster.Blocks.ElectricWireItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(wireItem.ItemGuid);
                for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                {
                    if (inventory[i].Id == itemId && inventory[i].Count > 0) return itemId;
                }
            }

            return ItemMaster.EmptyItemId;
        }
    }
}
```

`TrainRailItemAutoSelector.cs`（同形。`MasterHolder.TrainUnitMaster.GetRailItems()`を列挙し`railItem.ItemGuid`で判定。namespace `...TrainRailConnect`）。

`ConnectToolMasterUtil.cs`:

```csharp
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 接続ツールエントリに紐づく自動設置ブロック（橋脚・電柱）を解決する
    /// Resolves the auto-placed block (pier / pole) bound to a connect tool entry
    /// </summary>
    public static class ConnectToolMasterUtil
    {
        public static bool TryGetPlaceBlock(string placeMode, out BlockId blockId, out BlockMasterElement blockMaster)
        {
            blockId = default;
            blockMaster = null;

            foreach (var element in MasterHolder.PlaceSystemMaster.PlaceSystem.Data)
            {
                if (element.PlaceMode != placeMode || element.PlaceBlockGuid == null) continue;
                blockId = MasterHolder.BlockMaster.GetBlockId(element.PlaceBlockGuid.Value);
                blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                return true;
            }

            return false;
        }
    }
}
```

（`PlaceBlockGuid`の生成型がGuid?でない場合は現物に合わせる）

- [ ] **Step 2: ElectricWireConnectSystemの選択駆動化**

`ElectricWireExtendMode.Update`:
- 38行 `var wireItemId = ctx.HoldingItemId;` → `var wireItemId = ElectricWireItemAutoSelector.FindOwnedWireItemId(_context.Inventory);`
- `ExtendToEmptySpace`の`TryFindPoleSlot`（81行）を`ConnectToolMasterUtil.TryGetPlaceBlock(PlaceSystemMasterElement.PlaceModeConst.ElectricWireConnect, out var poleBlockId, out var poleMaster)`へ置換（`poleItemId`が下流の`EvaluateNewPole`引数に必要な場合は、電柱=選択ブロックの所持アイテム前提が消えたため、`EvaluateNewPole`のpoleItemId引数の用途を現物確認し「電柱アイテム所持判定」なら建設コスト充足判定（プラン3の`ConstructionCostPreviewCalculator.CalculateAffordableCellCount(poleMaster.RequiredItems, ...) >= 1`）に置き換える）
- Task 3 Step 4の暫定`GetBlockId(poleItemId)`解決を削除し`Extend(fromPos, poleBlockId, ...)`
- `ElectricWireExtendRequestSender.TryFindPoleSlot`が未参照になったら削除

- [ ] **Step 3: TrainRailConnectSystemの選択駆動化**

- 81-103行の`pierSlots`インベントリ検索を削除し、`ConnectToolMasterUtil.TryGetPlaceBlock(PlaceModeConst.TrainRailConnect, out var pierBlockId, out var pierBlockMaster)`で橋脚を解決（失敗時は現行の「pierなし」分岐=デフォルト最大長プレビューへ）
- レール素材: 93/105/134行の`context.HoldingItemId`→`TrainRailItemAutoSelector.FindOwnedRailItemId(_playerInventory)`（フレーム冒頭で1回解決し変数で渡す）
- 104行 `_trainRailPlaceSystemService.ManualUpdate(itemStack.Id)`→`ManualUpdate(pierBlockId)`（Task 8で変更済みのシグネチャ）
- `SendConnectRailWithPlacePierProtocol(placeInfo, previewData.RailTypeGuid, pierBlockId)`（Task 5の暫定引数をそのまま選択解決値に）

- [ ] **Step 4: GearChainPoleConnectSystemの選択駆動化**

- `ManualUpdate`のモード分岐（50行）を置換:

```csharp
            // ブロック選択（歯車ポール）なら設置延長、接続ツール選択ならチェーン接続モード
            // Pole-block selection runs place-extend; connect-tool selection runs chain-connect
            GearChainPoleFrameResult result;
            if (context.SelectionType == PlacementSelectionType.Block)
            {
                var poleBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(context.SelectedBlockId.Value);
                var input = _inputCollector.CollectPlaceExtend(context, _sourcePole, poleBlockMaster, _requestSender.IsAwaitingResponse);
                result = GearChainPolePlaceExtendMode.Decide(input);
            }
            else
            {
                var input = _inputCollector.CollectChainConnect(context, _sourcePole);
                result = GearChainPoleChainConnectMode.Decide(input);
            }
```

- `GearChainPoleFrameInputCollector`:
  - `CollectPlaceExtend`: `PoleBlockId = MasterHolder.BlockMaster.GetBlockId(poleBlockMaster.BlockGuid)`（Task 4の暫定と同じ値になるが、HotBarSlot由来コードを完全排除）、76行の`context.HoldingItemId`→`input.OwnedChainItemId`
  - `CollectChainConnect`: 88行`HoldingChainItemId = context.HoldingItemId`→`GearChainPoleItemFinder.FindOwnedChainItemId(_playerInventory)`、101行の`context.HoldingItemId`→同値
- `GearChainPoleItemFinder.TryGetPoleBlockMaster`が未参照になったら削除（`FindOwnedChainItemId`は存続）

- [ ] **Step 5: コンパイル＋回帰＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|GearChain|ElectricWire|TrainRail"`
Expected: エラー0件・全PASS

```bash
git add moorestech_client/
git commit -m "feat: 接続ツール3種をビルドメニュー選択駆動と素材自動選択に切替"
```

---

### Task 10: ホットバー依存の最終排除

**Files:**
- Modify: `.../PlaceSystem/IPlaceSystem.cs`（`HoldingItemId`/`IsSelectSlotChanged`/`PreviousSelectHotbarSlotIndex`/`CurrentSelectHotbarSlotIndex`削除）
- Modify: `.../PlaceSystem/PlaceSystemStateController.cs`（`HotBarView`依存除去）
- Modify: `new PlaceSystemUpdateContext(`の全呼び出し側（テスト含む）
- Modify: `.../PlaceSystem/Util/PlaceSystemUtil.cs`（`SendPlaceProtocol`が未参照なら削除）

**Interfaces:**
- Produces: `PlaceSystemUpdateContext(PlacementSelectionType selectionType, BlockId? selectedBlockId, Guid selectedTrainCarGuid, string selectedConnectPlaceMode, bool isSelectionChanged)`（最終形）
- Consumes: Task 8/9完了時点で`HoldingItemId`等の参照が特殊システムから消えていること

- [ ] **Step 1: 残存参照ゼロを確認してから削除**

Run: `grep -rn "HoldingItemId\|CurrentSelectHotbarSlotIndex\|PreviousSelectHotbarSlotIndex\|IsSelectSlotChanged" moorestech_client/Assets/Scripts --include='*.cs'`

ヒットが`IPlaceSystem.cs`（定義）と`PlaceSystemStateController.cs`（充填）のみであることを確認。他に残っていればTask 8/9の漏れとして先に潰す。

- [ ] **Step 2: Contextとコントローラを最終形へ**

- `PlaceSystemUpdateContext`から旧4フィールドとコンストラクタ引数を削除
- `PlaceSystemStateController`: `HotBarView`のコンストラクタ注入・`_hotBarView`・`_lastSelectHotBarSlot`を削除し、`CreateContext`を選択値のみで構成。DI解決のため`MainGameStarter`側の登録変更は不要（コンストラクタインジェクション）だが、`new PlaceSystemStateController(`を手書きしている箇所があればgrepで追随
- `new PlaceSystemUpdateContext(`の全呼び出し側（テスト等）を新5引数に更新

- [ ] **Step 3: 旧送信ユーティリティの掃除**

Run: `grep -rn "SendPlaceProtocol\|PlaceHotBarBlock" moorestech_client/Assets/Scripts --include='*.cs'`

`PlaceSystemUtil.SendPlaceProtocol`と`VanillaApiSendOnly.PlaceHotBarBlock`がクライアント内で未参照になっていれば、この2つは**プラン5（プロトコル本体削除）まで残す**。参照が残っていれば（＝移行漏れ）Task 8/9の漏れとして修正する。結果を報告に含める。

- [ ] **Step 4: コンパイル＋広域回帰＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.CombinedTest.Server"`
Expected: 全PASS（180秒超は分割）

```bash
git add moorestech_client/
git commit -m "refactor: 設置システムからホットバー・保持アイテム依存を排除"
```

---

### Task 11: シーン確認（ビルドメニューの実表示）

**Files:** なし（確認のみ。必要が出た場合のみ`uloop execute-dynamic-code`でシーン修正）

- [ ] **Step 1: エントリ3種の表示をエディタ上で確認**

プラン3で配線済みの`BuildMenuView`パネル（MainGameシーン）はグリッド1本のままで3種エントリを流し込む設計のため、シーン変更は原則不要。uloopでPlayModeを起動し（uloop-control-play-modeスキル）、Bキー相当の遷移でメニューを開いて以下を確認する:

1. ブロックエントリの後に接続ツール3件（レール敷設・電線接続・歯車チェーン接続）が並ぶ
2. ツールチップに名称（車両はコスト付き）が出る
3. 解放済み車両がある場合は車両エントリが出る（新規セーブでは研究未達のため出ないのが正常）

表示崩れ（グリッドセル不足等）があれば`uloop execute-dynamic-code`で調整し、変更内容を報告する。

- [ ] **Step 2: コミット（シーン変更があった場合のみ）**

```bash
git status --short moorestech_client/Assets/Scenes/
# 変更があれば
git add moorestech_client/Assets/Scenes/ && git commit -m "feat: ビルドメニューの3種エントリ表示を調整"
```

---

### Task 12: 本番マスタ追補（moorestech_master・プラン4スクリプト）

**Files:**
- Create: `/Users/katsumi/moorestech_master/tools/plan4_migration/migrate_plan4.py`
- Modify（スクリプト実行で）: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` / `train.json` / `craftRecipes.json` / `research.json`
- Modify: `/Users/katsumi/moorestech/.moorestech-external-revisions.json`（ピン更新）

**Interfaces:**
- Consumes: Task 2-5完了（プロトコルがrequiredItems消費に移行済みであること — 増殖経路防止の順序制約）
- Produces: 除外9ブロック＋車両3種にrequiredItems投入、素材レシピ12件（ブロック9＋車両3）削除、research.jsonの`unlockItemRecipeView`から該当12アイテムのguid除去

- [ ] **Step 1: mooreseditor停止確認とブランチ確認**

Run: `pgrep -fl mooreseditor || echo "not running"`
Run: `git -C /Users/katsumi/moorestech_master status --short && git -C /Users/katsumi/moorestech_master branch --show-current`
Expected: `not running` / working tree clean / ブランチ`plan2-master-migration`（異なる場合はユーザーへ報告して指示を仰ぐ — moorestech_masterのブランチ整理は別件の残タスク）

- [ ] **Step 2: 追補スクリプトを作成**

`migrate_plan4.py`（migrate.pyは適用済みデータ上で再実行不可のため新規。dry-run既定・`--apply`で書き込み）:

```python
#!/usr/bin/env python3
"""プラン4追補: 除外9ブロック+車両3種へrequiredItems投入、素材レシピ削除、recipeView参照除去。
Plan4 follow-up: invest requiredItems into the 9 excluded blocks + 3 train cars,
delete their material recipes, and strip their guids from unlockItemRecipeView."""
import json
import sys
from pathlib import Path

MASTER = Path(__file__).resolve().parent.parent.parent / 'server_v8/mods/moorestechAlphaMod_8/master'
TARGET_BLOCK_TYPES = {'TrainRail', 'TrainStation', 'TrainItemPlatform', 'TrainFluidPlatform', 'ElectricPole', 'GearChainPole'}
APPLY = '--apply' in sys.argv


def load(name):
    return json.loads((MASTER / name).read_text(encoding='utf-8'))


def save(name, data):
    # 既存ファイルのインデントを踏襲する（実行前にheadで確認し必要なら調整）
    # Preserve the existing indentation (verify with head before running)
    (MASTER / name).write_text(json.dumps(data, ensure_ascii=False, indent=4) + '\n', encoding='utf-8')


blocks = load('blocks.json')
train = load('train.json')
craft = load('craftRecipes.json')
research = load('research.json')

# 素材のみレシピを結果アイテムguidで索引化する
# Index material-only recipes by their result item guid
recipe_by_result = {}
for recipe in craft['data']:
    if recipe.get('hasBlockIngredient'):
        continue
    recipe_by_result.setdefault(recipe['craftResultItemGuid'], []).append(recipe)

invested_blocks, invested_cars, removed_recipe_guids, removed_item_guids = [], [], set(), set()

# 対象9ブロックへrequiredItemsを投入する（レシピ材料をそのまま複製。craftResultCount==1前提を検証）
# Invest requiredItems into the 9 target blocks by copying recipe materials (asserting craftResultCount == 1)
for block in blocks['data']:
    if block.get('blockType') not in TARGET_BLOCK_TYPES or block.get('requiredItems'):
        continue
    recipes = recipe_by_result.get(block['itemGuid'], [])
    assert len(recipes) == 1, f"recipe not unique for block {block['name']}: {len(recipes)}"
    recipe = recipes[0]
    assert recipe['craftResultCount'] == 1, f"unexpected craftResultCount for {block['name']}"
    block['requiredItems'] = [{'itemGuid': m['itemGuid'], 'count': m['count']} for m in recipe['requiredItems']]
    invested_blocks.append(block['name'])
    removed_recipe_guids.add(recipe['recipeGuid'])
    removed_item_guids.add(block['itemGuid'])

# 車両3種へrequiredItemsを投入する
# Invest requiredItems into the 3 train cars
for car in train['trainCars']:
    if car.get('requiredItems'):
        continue
    recipes = recipe_by_result.get(car['itemGuid'], [])
    assert len(recipes) == 1, f"recipe not unique for car {car['itemGuid']}: {len(recipes)}"
    recipe = recipes[0]
    assert recipe['craftResultCount'] == 1
    car['requiredItems'] = [{'itemGuid': m['itemGuid'], 'count': m['count']} for m in recipe['requiredItems']]
    invested_cars.append(car['itemGuid'])
    removed_recipe_guids.add(recipe['recipeGuid'])
    removed_item_guids.add(car['itemGuid'])

# 対象レシピを削除する
# Delete the target recipes
before = len(craft['data'])
craft['data'] = [r for r in craft['data'] if r['recipeGuid'] not in removed_recipe_guids]
deleted_recipes = before - len(craft['data'])

# unlockItemRecipeView から削除アイテムのguidを除去し、空になったアクションは落とす
# Strip deleted item guids from unlockItemRecipeView and drop actions that become empty
stripped = 0
for node in research['data']:
    actions = node.get('clearedActions') or []
    kept_actions = []
    for action in actions:
        if action.get('gameActionType') == 'unlockItemRecipeView':
            guids = action['gameActionParam']['unlockItemGuids']
            new_guids = [g for g in guids if g not in removed_item_guids]
            stripped += len(guids) - len(new_guids)
            if not new_guids:
                continue
            action['gameActionParam']['unlockItemGuids'] = new_guids
        kept_actions.append(action)
    node['clearedActions'] = kept_actions

# 検証: 投入数と削除数が期待どおりで、対象アイテムを結果とするレシピが残っていないこと
# Validate expected counts and that no recipe still produces a target item
assert len(invested_blocks) == 9, f"invested blocks = {invested_blocks}"
assert len(invested_cars) == 3, f"invested cars = {invested_cars}"
assert deleted_recipes == 12, f"deleted recipes = {deleted_recipes}"
for recipe in craft['data']:
    assert recipe['craftResultItemGuid'] not in removed_item_guids

print(f"blocks: {invested_blocks}")
print(f"cars: {invested_cars}")
print(f"recipes deleted: {deleted_recipes}, recipeView guids stripped: {stripped}")

if APPLY:
    save('blocks.json', blocks)
    save('train.json', train)
    save('craftRecipes.json', craft)
    save('research.json', research)
    print('APPLIED')
else:
    print('DRY RUN (use --apply to write)')
```

実行前に実ファイルのJSONキー名（`recipeGuid`/`craftResultItemGuid`/`requiredItems`/`clearedActions`/`gameActionParam`等）とインデントを`head -40`とplan2の`migrate.py`で照合し、スクリプトを現物へ合わせて修正すること。

- [ ] **Step 3: dry-run→適用→差分確認**

```bash
python3 /Users/katsumi/moorestech_master/tools/plan4_migration/migrate_plan4.py
python3 /Users/katsumi/moorestech_master/tools/plan4_migration/migrate_plan4.py --apply
git -C /Users/katsumi/moorestech_master diff --stat
```

Expected: dry-runの出力が「9ブロック・3車両・レシピ12件」。差分が4ファイルのみ。

- [ ] **Step 4: 本番マスタでの起動確認**

uloopでゲームを起動（v8 mod読み込み）し、マスタバリデーションエラー（requiredItemsのitems実在・基底/オーバーライド一致等）が出ないことを`uloop get-logs --project-path ./moorestech_client --log-type Error`で確認する。

- [ ] **Step 5: コミットとピン更新**

```bash
git -C /Users/katsumi/moorestech_master add tools/plan4_migration/ server_v8/
git -C /Users/katsumi/moorestech_master commit -m "feat: プラン4追補 特殊ブロック9種と車両3種へ建設コストを投入しレシピを削除"
```

moorestechリポジトリ側の`.moorestech-external-revisions.json`のピンを新コミットハッシュへ更新し、コミットする（RepositorySyncの巻き戻り対策）:

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: moorestech_masterのピンをプラン4追補へ更新"
```

---

### Task 13: 全体回帰とPlayMode実機検証

**Files:** なし（検証のみ）

- [ ] **Step 1: 全回帰**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.CombinedTest"`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.UnitTest"`
Expected: 全PASS（180秒超はクラス分割）

- [ ] **Step 2: PlayMode実機検証（録画付き）**

`unity-playmode-recorded-playtest`スキルで以下を検証する（legacy Input制約で自動化不可の操作はUIStateControl直接遷移で代替し報告）:

1. **ビルドメニュー**: 3種エントリ表示（Task 11の再確認を本番マスタで）
2. **橋脚単体設置**: レール橋脚をブロック選択→設置で素材が減る（新PlaceBlockProtocol経由）
3. **電線接続ツール**: 電柱間の接続で電線が減る／空きスペース延長で電柱素材＋電線が減る
4. **歯車ポール**: ポールブロック選択で設置延長（素材消費）、チェーン接続ツールで接続
5. **破壊返却**: 追補済みブロック（電柱等）を破壊すると素材全額が返る
6. **車両**: unlockTrainCar済みセーブ（または研究をデバッグ実行）で車両エントリ→レール上設置→素材消費、撤去→素材全額返却

- [ ] **Step 3: 未コミット差分の最終確認**

```bash
git status --short
# 残差分があれば内容を確認してコミット（環境ドリフトファイルは除外）
```

検証結果（成功シナリオ・自動化できなかった操作・発見したバグ）を報告し、申し送りドキュメント`2026-07-05-satisfactory-placement-handoff.md`へプラン4完了の追記を行う。
