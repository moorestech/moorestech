---
spec: docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md
---

# Plan B: 装備スロット新設＋採掘のサーバ権威化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ツール（斧・石器）の置き場所として独立インベントリ「装備スロット」を新設し、採掘のダメージ算出と連打検証をサーバへ移す。このplan完了時点で、ホイールは装備切替に移り、旧ホットバーは数字キー選択のみ残る（ホットバー自体の作り替えはplan C）。

**Architecture:** ①`items.yml` トップレベルに `tools` 配列と `equipmentSlotCount` を新設し `ToolMaster` で読む ②`InventoryType.Equipment` の独立 `IOpenableInventory`（`EquipmentInventoryData`、前例 `GrabInventoryData`）を `PlayerInventoryData` に追加、受入制限は新機構 `IItemAcceptanceInventory` で宣言し移動サービスが尊重 ③装備状態の同期は3点セット（`EquipmentUpdateEventPacket`＋`PlayerInventoryResponseProtocol` 拡張＋クライアント購読）④採掘は `MapObjectAcquisitionProtocol` から `AttackDamage` を廃し、サーバが装備ツール×`miningTools` からダメージ算出＋`attackSpeed` クールダウン検証。

**Tech Stack:** C#（Unity / MessagePack / UniRx）/ Mooresmaster SourceGenerator / TypeScript+React+zod（moorestech_web/webui）

**前提:** plan A（設置対象ID統一）完了後に着手する（直接依存は薄いが、spec裁定の実装順 A→B→C に従う）。

**確認すべきドキュメント（着手前に必読）:**
- spec: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md`（決定は `docs/adr/0003`・`0004`）
- `/edit-schema`・`/validate-schema`・`/creating-server-protocol`・`/creating-server-tests`・`/csharp-event-pattern` の各スキル

## Global Constraints

- 後方互換は考慮しない。スキーマ追加は必須プロパティとし全JSONを一括更新（`optional: true`・フォールバック禁止）。ただし**セーブデータ（ユーザー生成データ）**の欠損フィールド（旧セーブに装備が無い等）は空初期化してよい（spec判断記録に裁定済み）
- サーバー可変状態のクライアント同期は3点セット（イベントパケット＋初期データ＋購読）。他プロトコル応答からの導出（Applier）は禁止
- 1プロトコル＝1 VanillaApiメソッド。enumはintに変換せずそのまま送る
- 採掘の距離検証は行わない（座標がクライアント申告値のため。ADR-0004裁定済み）
- 採掘進捗バーはクライアント予測のまま（演出は変えない）
- `Func<>` 禁止・partial 禁止・try-catch 原則禁止・単純getter/setterプロパティ禁止（Setは `SetHoge` メソッド）・1ファイル200行以下・イベントはUniRx・コメントは日本語英語2行セット
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`
- テスト実行後に「Unity is reloading (Domain Reload in progress)」が出たら45秒待ってリトライ
- 各タスク末尾で必ずコミットする

## File Structure（このplanで触るファイルの全体像）

新規:
- `moorestech_server/Assets/Scripts/Core.Master/ToolMaster.cs`
- `moorestech_server/Assets/Scripts/Core.Inventory/IItemAcceptanceInventory.cs`
- `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/Event/IEquipmentInventoryUpdateEvent.cs`（grabの同名前例に完全追随）
- `moorestech_server/Assets/Scripts/Game.PlayerInventory/Event/EquipmentInventoryUpdateEvent.cs`（同上）
- `moorestech_server/Assets/Scripts/Game.PlayerInventory/ItemManaged/EquipmentInventoryData.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/EquipmentUpdateEventPacket.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/EquipmentProtocol.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/Resolver/EquipmentInventoryIdentifierResolver.cs`
- `moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Equipment/LocalPlayerEquipment.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Equipment/EquipmentHeldItemModel.cs`（`HotBarHeldItemModel` の移設）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/EquipmentActions.cs`
- `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx`（＋必要ならロジックts）

変更（主要のみ）:
- `VanillaSchema/items.yml` … `tools` 配列＋`equipmentSlotCount` 追記
- テストJSON `Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`・実運用JSON `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`
- `Core.Master/MasterHolder.cs` … ToolMaster登録
- `Server.Util/MessagePack/InventoryType.cs` … `Equipment` 追加
- `Server.Util/MessagePack/InventoryIdentifierMessagePack.cs` … `CreateEquipmentMessage(playerId)` 追加
- `Game.PlayerInventory.Interface/PlayerInventoryData.cs` / `PlayerInventorySaveJsonObject.cs`
- `Game.PlayerInventory/PlayerInventoryDataStore.cs` … Equipment生成・セーブ・ロード
- `Server.Protocol/.../InventoryService/InventoryItemMoveService.cs` / `InventoryItemInsertService.cs` / `OpenableInventoryResolver`
- `Server.Protocol/PacketResponse/PlayerInventoryResponseProtocol.cs:27-79` … Equipment＋SelectedEquipmentIndex同梱
- `Server.Protocol/PacketResponse/MapObjectAcquisitionProtocol.cs` … AttackDamage廃止・サーバ算出
- `Common.Debug/DebugParameter.cs`（`DebugParameterKeys`）… `MapObjectSuperMine` キー追加
- `Client.Common/DebugConst.cs` … サーバ共有キーへの参照に変更
- `Client.Game/InGame/Mining/MapObjectMiningFocusState.cs` / `MapObjectMiningMiningState.cs` / `MapObjectMiningMiningCompleteState.cs` / `IMapObjectMiningState.cs`（Context）
- `Client.Network/API/VanillaApiSendOnly.cs:69-73`（`AttackMapObject`）＋ `SetSelectedEquipment` 追加
- `Client.Game/InGame/UI/Inventory/Main/NetworkEventInventoryUpdater.cs` … 装備イベント購読
- `Client.Game/InGame/UI/Inventory/HotBarView.cs` … ホイール切替の撤去（数字キーは残す）
- `Client.WebUiHost/Game/Topics/InventoryTopic.cs` … equipment/selectedEquipment 追加
- `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts` / `src/bridge/transport/actionContract.ts` / `src/features/inventory/HotbarPanel/index.tsx` / `hotbarLogic.ts`

---

### Task 1: `items.yml` に `tools` 配列と `equipmentSlotCount` を新設し、ToolMaster で読む

**Files:**
- Modify: `VanillaSchema/items.yml`（`playerInventorySlotLevels` の後）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`
- Create: `moorestech_server/Assets/Scripts/Core.Master/ToolMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/ToolMasterTest.cs`

**Interfaces:**
- Produces: `MasterHolder.ToolMaster`（`int EquipmentSlotCount`, `bool IsTool(ItemId itemId)`, `IReadOnlyList<ToolMasterElement> All`）。Task 2 の受入制限、Task 6 のサーバ採掘、Task 8 のWeb表示が参照する

- [ ] **Step 1: スキーマを追記する**

`/edit-schema` スキルを読んでから、`VanillaSchema/items.yml` のトップレベル `properties`（`playerInventorySlotLevels` の後）に追記。foreignKey記法は `map.yml` の `miningTools.toolItemGuid`（64-77行）と同じ:

```yaml
- key: equipmentSlotCount
  type: integer
- key: tools
  type: array
  overrideCodeGeneratePropertyName: ToolMasterElement
  items:
    type: object
    properties:
    - key: toolItemGuid
      type: uuid
      foreignKey:
        schemaId: items
        foreignKeyIdPath: /data/[*]/itemGuid
        displayElementPath: /data/[*]/name
```

`optional: true` は付けない。

- [ ] **Step 2: テスト用・実運用のitems.jsonを一括更新する**

両JSONのトップレベルに追加:
- テスト用: 既存アイテムのうち、`map.json` の `MiningType: Mining` な mapObject の `miningTools[].toolItemGuid` に使われているGUIDを1つ探して `tools` に載せる（＝テストで「装備すると掘れるアイテム」になる）。`equipmentSlotCount` は `3`
- 実運用: 石の斧・石器の2アイテムのGUID（`items.json` の `data` から `name` で検索）を `tools` に載せる。`equipmentSlotCount` は `3`

```json
"equipmentSlotCount": 3,
"tools": [
  { "toolItemGuid": "<石の斧のitemGuid>" },
  { "toolItemGuid": "<石器のitemGuid>" }
]
```

- [ ] **Step 3: コンパイルして生成を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`Mooresmaster.Model.ItemsModule` に `Tools`/`EquipmentSlotCount` が生成される）

- [ ] **Step 4: 失敗するテストを書く**

`Tests/UnitTest/Core/ToolMasterTest.cs`:

```csharp
using System.Linq;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core
{
    public class ToolMasterTest
    {
        [Test]
        public void Toolsと装備スロット数をロードできる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(3, MasterHolder.ToolMaster.EquipmentSlotCount);
            Assert.IsTrue(MasterHolder.ToolMaster.All.Count >= 1);

            // tools記載のアイテムはIsTool=true、未記載はfalse
            // Listed items are tools; unlisted items are not
            var toolGuid = MasterHolder.ToolMaster.All[0].ToolItemGuid;
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolGuid);
            Assert.IsTrue(MasterHolder.ToolMaster.IsTool(toolItemId));

            var nonTool = MasterHolder.ItemMaster.GetItemAllIds().First(id => id != toolItemId && !MasterHolder.ToolMaster.All.Any(t => MasterHolder.ItemMaster.GetItemId(t.ToolItemGuid) == id));
            Assert.IsFalse(MasterHolder.ToolMaster.IsTool(nonTool));
        }
    }
}
```

（`ItemMaster.GetItemId(Guid)`・`GetItemAllIds()` の実名は `Core.Master/ItemMaster.cs` を開いて確認し、実名に合わせる）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ToolMasterTest"`
Expected: FAIL（`MasterHolder.ToolMaster` 不在のコンパイルエラー）

- [ ] **Step 5: ToolMaster を実装する**

`Core.Master/ToolMaster.cs`（前例: plan Aの `BuildToolMaster` と同じ「同一JSONの自分の配列だけ読む」形。`ItemsLoader.Load(itemJToken)` から `.Tools` と `.EquipmentSlotCount` を読む。`IsTool` は `Initialize()` で `HashSet<ItemId>` を構築して判定）。`MasterHolder.cs` へは `ItemMaster` 初期化（`JsonFileName("items")`）の後に同じJTokenで登録する。

- [ ] **Step 6: バリデーション追加を確認する**

`/validate-schema` スキルを読み、`toolItemGuid` のforeignKeyに対応するC#バリデーション（存在しないitemGuidを検出）を既存の同型バリデーション（`ConnectToolMaster` の `RequiredItems` 等）と同じ場所・同じ形式で追加する。

- [ ] **Step 7: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ToolMasterTest"`
Expected: PASS

```bash
git add VanillaSchema/items.yml moorestech_server/Assets/Scripts/Core.Master/ moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json moorestech_server/Assets/Scripts/Tests/UnitTest/Core/ToolMasterTest.cs
git commit -m "feat: items.ymlにtools/equipmentSlotCountを新設しToolMasterで読む"
cd ../moorestech_master && git add server_v8/mods/moorestechAlphaMod_8/master/items.json && git commit -m "feat: 石の斧・石器をtoolsに登録" && cd -
```

---

### Task 2: 受入制限機構 `IItemAcceptanceInventory` を新設し移動サービスが尊重する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Inventory/IItemAcceptanceInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/InventoryItemMoveService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/InventoryItemInsertService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ItemAcceptanceInventoryTest.cs`

**Interfaces:**
- Produces:
  - `interface IItemAcceptanceInventory { bool CanAccept(ItemId itemId); int GetMaxCountPerSlot(ItemId itemId); }`
  - 移動サービス2種は、移動先が `IItemAcceptanceInventory` を実装する場合 `CanAccept` がfalseなら**何もしない**、trueなら `GetMaxCountPerSlot` を超えない個数だけ移す（超過分は移動元に残す）
- 消費: Task 3 の `EquipmentInventoryData` が実装する

**新機構の根拠（spec判断記録に裁定済み）:** 「特定のアイテムしか受け付けないインベントリ」は既存に無い（機械のモジュールスロット `VanillaMachineModuleInventory` は無制限で前例にならない — `Core.Inventory/OpenableInventoryItemDataStoreService.cs:32-40` の `InsertionCheck` は容量のみ検査、と裏取り済み）。

- [ ] **Step 1: インターフェースを書く**

`Core.Inventory/IItemAcceptanceInventory.cs`:

```csharp
using Core.Master;

namespace Core.Inventory
{
    // 受け入れ可能なアイテムを制限したいインベントリが宣言する。移動サービスがこれを尊重する
    // Declared by inventories that restrict acceptable items; move services honor it
    public interface IItemAcceptanceInventory
    {
        bool CanAccept(ItemId itemId);
        int GetMaxCountPerSlot(ItemId itemId);
    }
}
```

- [ ] **Step 2: 失敗するテストを書く**

`Tests/UnitTest/Game/ItemAcceptanceInventoryTest.cs`。Task 3 の装備インベントリ完成前なので、このタスクではテスト内フェイク（`IOpenableInventory`＋`IItemAcceptanceInventory` を実装した小さなテスト用クラス。実装は `OpenableInventoryItemDataStoreService` 委譲で `GrabInventoryData.cs` の形を丸写し）を移動先に使い、`InventoryItemMoveService`/`InventoryItemInsertService` を直接呼んで検証する:

```csharp
[Test]
public void 受入不可アイテムの移動は何も起きない()
{
    // fake: CanAccept=false固定。移動を試みても両インベントリ不変
    // fake with CanAccept=false; both inventories unchanged after the move
}

[Test]
public void スロット上限1のインベントリへスタック移動すると1個だけ入り残りは元に残る()
{
    // fake: CanAccept=true, GetMaxCountPerSlot=1。5個移動→先に1個・元に4個
    // fake with per-slot cap 1; moving 5 puts 1 and leaves 4
}
```

移動サービスの呼び出しシグネチャは `InventoryItemMoveService.cs`/`InventoryItemInsertService.cs` を開いて確認し、既存の `InventoryItemMoveProtocol` 経由テスト（あれば）の形に合わせる。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemAcceptanceInventoryTest"`
Expected: FAIL（制限が尊重されず全量移動してしまう）

- [ ] **Step 3: 移動サービス2種に受入検査を実装する**

両サービスの「移動先へ入れる」直前に:

```csharp
// 移動先が受入制限を宣言していれば尊重する
// Honor acceptance restrictions declared by the destination inventory
if (destination is IItemAcceptanceInventory acceptance)
{
    if (!acceptance.CanAccept(itemId)) return;
    moveCount = Math.Min(moveCount, acceptance.GetMaxCountPerSlot(itemId) - destinationSlotCurrentCount);
    if (moveCount <= 0) return;
}
```

（変数名は各サービスの実コードに合わせる。Swap系は「入れ替え後の両スロットが制約を満たす場合のみ実行」とする）

- [ ] **Step 4: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemAcceptanceInventoryTest|InventoryItemMove"`
Expected: PASS（既存の移動テストもリグレッションなし）

```bash
git add moorestech_server/Assets/Scripts/Core.Inventory/IItemAcceptanceInventory.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/ moorestech_server/Assets/Scripts/Tests/UnitTest/Game/ItemAcceptanceInventoryTest.cs
git commit -m "feat: インベントリ受入制限IItemAcceptanceInventoryを新設し移動サービスが尊重"
```

---

### Task 3: 装備インベントリ `EquipmentInventoryData` の新設（InventoryType.Equipment・セーブ・ロード）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/Event/IEquipmentInventoryUpdateEvent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory/Event/EquipmentInventoryUpdateEvent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory/ItemManaged/EquipmentInventoryData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/PlayerInventoryData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/PlayerInventorySaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory/PlayerInventoryDataStore.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/InventoryType.cs` / `InventoryIdentifierMessagePack.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/Resolver/EquipmentInventoryIdentifierResolver.cs`
- Modify: 同 `Resolver` を束ねる `OpenableInventoryResolver`（実ファイル名はディレクトリ内を確認）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/EquipmentInventoryTest.cs`

**Interfaces:**
- Consumes: Task 1 `ToolMaster`、Task 2 `IItemAcceptanceInventory`
- Produces:
  - `PlayerInventoryData.EquipmentInventory: EquipmentInventoryData`（**具象型で持つ**。選択インデックスは `EquipmentInventoryData.SelectedEquipmentIndex` が所有し、`-1`＝素手。変更は `SetSelectedEquipmentIndex(int)`、選択中アイテムは `GetSelectedItem()`（`-1` なら空スタック））
  - `InventoryType.Equipment`、`InventoryIdentifierMessagePack.CreateEquipmentMessage(int playerId)`
  - セーブJSON: `PlayerInventorySaveJsonObject` に `"EquipmentInventoryItems"`（List）と `"SelectedEquipmentIndex"`（int）
  - `IEquipmentInventoryUpdateEvent`（grab前例と同形。Task 4 のイベントパケットが購読）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/EquipmentInventoryTest.cs`（`/creating-server-tests` 参照。初期化は既存PacketTestと同じ）:

```csharp
[Test]
public void ツールだけが装備スロットへ移動でき1枠1個まで()
{
    var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

    // メインにツール5個と非ツール1個を置く
    // Put 5 tools and 1 non-tool into main
    var toolItemId = MasterHolder.ItemMaster.GetItemId(MasterHolder.ToolMaster.All[0].ToolItemGuid);
    playerInventory.MainOpenableInventory.SetItem(0, toolItemId, 5);

    // InventoryItemMoveProtocol（InsertSlot）でメイン0→装備0へ5個要求
    // Request moving 5 items from main slot 0 to equipment slot 0
    // → 装備0に1個・メイン0に4個
    Assert.AreEqual(1, playerInventory.EquipmentInventory.GetItem(0).Count);
    Assert.AreEqual(4, playerInventory.MainOpenableInventory.GetItem(0).Count);

    // 非ツールは移動されない
    // Non-tools do not move
}

[Test]
public void 装備と選択インデックスがセーブロードで往復する()
{
    // SetItem＋SetSelectedEquipmentIndex(2)→GetSaveJsonObject→別コンテナでLoad→一致
    // Save with an equipped item and index 2, reload, verify both round-trip
}

[Test]
public void 装備フィールドの無い旧セーブは空装備で開始する()
{
    // EquipmentInventoryItems=null のPlayerInventorySaveJsonObjectをLoad→空スロット3・index 0
    // Loading a legacy save without equipment yields 3 empty slots and index 0
}
```

プロトコル送信の組み立ては既存の `InventoryItemMoveProtocol` を使うPacketTest（`Tests/CombinedTest/Server/PacketTest/` 内をgrep）の形を丸写しし、`InventoryIdentifierMessagePack.CreateEquipmentMessage(playerId)` を移動先に使う。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EquipmentInventoryTest"`
Expected: FAIL（型不在のコンパイルエラー）

- [ ] **Step 2: イベントと装備インベントリを実装する**

- `IEquipmentInventoryUpdateEvent`/`EquipmentInventoryUpdateEvent` — `IGrabInventoryUpdateEvent`/`GrabInventoryUpdateEvent`（`Game.PlayerInventory.Interface/Event/`・`Game.PlayerInventory/Event/`）を開き、**同じ構造・同じDI登録**でEquipment版を作る（イベント引数にplayerId・slot・itemStackを含める。grab版がUniRxでなければgrab版の機構のまま合わせ、UniRx化はしない — 前例一致優先）
- `EquipmentInventoryData.cs`（前例 `GrabInventoryData.cs` の委譲構造を丸写しし、以下を追加）:

```csharp
public class EquipmentInventoryData : IOpenableInventory, IItemAcceptanceInventory
{
    public int SelectedEquipmentIndex { get; private set; }
    // OpenableInventoryItemDataStoreService委譲フィールドはGrabInventoryDataと同形
    // Delegation to OpenableInventoryItemDataStoreService mirrors GrabInventoryData

    public EquipmentInventoryData(int playerId, IEquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent)
    {
        // スロット数はマスタ固定値
        // Slot count is fixed by master data
        // → OpenableInventoryItemDataStoreServiceをMasterHolder.ToolMaster.EquipmentSlotCountで生成
    }

    public bool CanAccept(ItemId itemId)
    {
        return MasterHolder.ToolMaster.IsTool(itemId);
    }

    public int GetMaxCountPerSlot(ItemId itemId)
    {
        return 1;
    }

    public void SetSelectedEquipmentIndex(int index)
    {
        // -1(素手)..スロット数-1へクランプし、変更時のみ選択イベントを発火
        // Clamp to -1 (bare hands)..slotCount-1; fire the selection event only on change
    }

    public IItemStack GetSelectedItem()
    {
        // -1(素手)なら空スタックを返す
        // Return an empty stack when -1 (bare hands) is selected
        return SelectedEquipmentIndex < 0 ? ServerContext.ItemStackFactory.CreatEmpty() : GetItem(SelectedEquipmentIndex);
    }
}
```

（空スタック生成の実名は `GrabInventoryData` 等の既存コードで使われている形に合わせる）

選択変更の通知は `IEquipmentInventoryUpdateEvent` に `SubscribeSelection` 系を足すのではなく、**同インターフェースに選択変更用のイベントを1本追加**する（イベント2種を1インターフェースで束ねる。Task 4 のパケットが両方購読する）。
- `PlayerInventoryData` に `public readonly EquipmentInventoryData EquipmentInventory;` を追加（具象型。`SelectedEquipmentIndex`/`GetSelectedItem` へアクセスするため）（コンストラクタ引数追加、呼び出し側を全部直す — デフォルト引数禁止）
- `PlayerInventoryDataStore` — プレイヤー生成箇所で `EquipmentInventoryData` を生成。`GetSaveJsonObject`/`LoadPlayerInventory` に装備アイテムリストと選択インデックスを追加（`PlayerInventorySaveJsonObject` に `[JsonProperty("EquipmentInventoryItems")] List<ItemStackSaveJsonObject>` と `[JsonProperty("SelectedEquipmentIndex")] int`。ロードでnullなら空初期化）
- `InventoryType.cs` に `Equipment` を追加、`InventoryIdentifierMessagePack` に `CreateEquipmentMessage(int playerId)`（`CreateGrabMessage` と同形）
- `EquipmentInventoryIdentifierResolver.cs`（前例 `GrabInventoryIdentifierResolver.cs` を丸写しして装備に差し替え）を作り、Resolver登録箇所（`OpenableInventoryResolver` 相当）に追加

- [ ] **Step 3: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EquipmentInventoryTest|PlayerInventory"`
Expected: PASS（既存PlayerInventory系もリグレッションなし）

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/ moorestech_server/Assets/Scripts/Game.PlayerInventory/ moorestech_server/Assets/Scripts/Server.Util/MessagePack/ moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/EquipmentInventoryTest.cs
git commit -m "feat: 装備インベントリEquipmentInventoryDataを新設(InventoryType.Equipment/セーブ対応)"
```

---

### Task 4: 装備の同期3点セット（イベントパケット・初期データ・選択プロトコル）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/EquipmentUpdateEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlayerInventoryResponseProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/EquipmentProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（登録）
- Modify: `MoorestechServerDIContainerGenerator`（`EquipmentUpdateEventPacket` のAddSingleton＋eager init。`GrabInventoryUpdateEventPacket` の登録行の隣）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EquipmentUpdateEventTest.cs`

**Interfaces:**
- Consumes: Task 3 の `IEquipmentInventoryUpdateEvent`・`PlayerInventoryData.EquipmentInventory`
- Produces:
  - EventTag `"va:event:equipmentUpdate"`。MessagePack: `[Key(0)] string EventType`（`"slot"` / `"selected"`）, `[Key(1)] int Slot`, `[Key(2)] ItemMessagePack Item`, `[Key(3)] int SelectedIndex`（EventType分岐は `MapObjectUpdateEventPacket` のEventType文字列前例）
  - `PlayerInventoryResponseProtocolMessagePack` に `[Key(5)] ItemMessagePack[] Equipment` と `[Key(6)] int SelectedEquipmentIndex`
  - ProtocolTag `"va:equipment"`。`EquipmentOperation { SetSelectedIndex }` のenum＋static factory（応答なし＝SendOnly運用）
  - Task 5 のクライアント購読、Task 8 のInventoryTopicが消費

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/Event/EquipmentUpdateEventTest.cs`（イベント検証の形は同ディレクトリの既存イベントテスト、例: Grab系かMainInventory系のイベントテストを開いて丸写し）:

```csharp
[Test]
public void 装備変更と選択変更がイベントで飛ぶ()
{
    // 装備スロットへSetItem → EventProtocolProviderに"va:event:equipmentUpdate"(EventType=slot)が積まれる
    // Setting an equipment slot enqueues a slot-type equipment update event
    // EquipmentProtocolでSetSelectedIndex(2) → EventType=selectedのイベント＋サーバ状態のindex=2
    // Selecting index 2 via protocol enqueues a selected-type event and updates server state
}

[Test]
public void プレイヤーインベントリ応答に装備と選択インデックスが同梱される()
{
    // PlayerInventoryResponseProtocolの応答に Equipment(3枠) と SelectedEquipmentIndex が入る
    // The inventory response carries 3 equipment slots and the selected index
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EquipmentUpdateEventTest"`
Expected: FAIL

- [ ] **Step 2: 実装する**

- `EquipmentUpdateEventPacket.cs` — `/creating-server-protocol` のEvent型手順どおり。`GrabInventoryUpdateEventPacket` を丸写しし、`IEquipmentInventoryUpdateEvent` のスロット更新・選択変更の両方を購読して `EventType` を分けて `AddEvent(playerId, ...)`
- `PlayerInventoryResponseProtocol.cs` — 応答構築（29-43行）にEquipmentスロットのループとSelectedEquipmentIndexを追加
- `EquipmentProtocol.cs` — `/creating-server-protocol` のRequest-Response型手順どおり（`GetResponse` で `playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(...)` を呼び `return null`）。リクエスト:

```csharp
[MessagePackObject]
public class EquipmentProtocolMessagePack : ProtocolMessagePackBase
{
    [Key(2)] public int PlayerId { get; set; }
    [Key(3)] public EquipmentOperation Operation { get; set; }
    [Key(4)] public int SelectedIndex { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public EquipmentProtocolMessagePack() { Tag = ProtocolTag; }

    private EquipmentProtocolMessagePack(int playerId, EquipmentOperation operation, int selectedIndex)
    {
        Tag = ProtocolTag;
        PlayerId = playerId;
        Operation = operation;
        SelectedIndex = selectedIndex;
    }

    public static EquipmentProtocolMessagePack CreateSetSelectedIndexRequest(int playerId, int selectedIndex)
        => new(playerId, EquipmentOperation.SetSelectedIndex, selectedIndex);
}
```

- `PacketResponseCreator.cs` に登録行を追加。DIコンテナに `EquipmentUpdateEventPacket` をAddSingleton＋eager init（grab版の登録行の隣に同形で）

- [ ] **Step 3: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EquipmentUpdateEventTest|EquipmentInventoryTest"`
Expected: PASS

```bash
git add moorestech_server/Assets/Scripts/Server.Event/ moorestech_server/Assets/Scripts/Server.Protocol/ moorestech_server/Assets/Scripts/Server.Boot/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EquipmentUpdateEventTest.cs
git commit -m "feat: 装備同期の3点セット(イベントパケット/初期データ同梱/選択プロトコル)"
```

---

### Task 5: クライアント装備モデル `LocalPlayerEquipment` と購読・手持ちモデル移設

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Equipment/LocalPlayerEquipment.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/NetworkEventInventoryUpdater.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Equipment/EquipmentHeldItemModel.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarHeldItemModel.cs`（移設）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs`（ホイール切替と手持ちモデル駆動の撤去）

**Interfaces:**
- Consumes: Task 4 のイベント・初期データ・プロトコル
- Produces:
  - `class LocalPlayerEquipment`（非MonoBehaviour）: `IReadOnlyList<IItemStack> Slots`、`int SelectedIndex`、`IItemStack SelectedItem`（空なら空スタック）、`IObservable<Unit> OnChanged`（UniRx `Subject`）、`void SetSelectedIndex(int)`（ローカル反映＋`VanillaApi.SendOnly.SetSelectedEquipment` 送信）、`void ApplySlotUpdate(int slot, ...)` / `void ApplySelected(int index)` / `void ApplyInitial(...)`（購読側から呼ぶ）
  - Task 6/7 の採掘判定、Task 8 のWebトピック、plan Cが消費
  - DI登録: `LocalPlayerInventory` が登録されているコンテナ（`Client.Starter` 側。`ILocalPlayerInventory` の登録箇所をgrepして同じ場所）に登録

- [ ] **Step 1: LocalPlayerEquipmentを実装する**

構造は `LocalPlayerInventory.cs`（`Client.Game/InGame/UI/Inventory/Main/`）を参考に、UniRx `Subject<Unit>` で変更通知（`event Action` は禁止）。

- [ ] **Step 2: 購読と初期データ適用を配線する**

`NetworkEventInventoryUpdater.cs:21` の `MainInventoryUpdateEventPacket.EventTag` 購読の隣に:

```csharp
ClientContext.VanillaApi.Event.SubscribeEventResponse(EquipmentUpdateEventPacket.EventTag, OnEquipmentUpdateEvent);
```

を追加し、`EventType`（`"slot"`/`"selected"`）で `LocalPlayerEquipment.ApplySlotUpdate`/`ApplySelected` に振り分ける。同クラス（または同クラスが使う初期化経路）で `PlayerInventoryResponseProtocol` の応答を適用している箇所を探し、`Equipment`・`SelectedEquipmentIndex` を `ApplyInitial` で適用する。

`VanillaApiSendOnly.cs` に追加（1プロトコル=1メソッド）:

```csharp
public void SetSelectedEquipment(int selectedIndex)
{
    var request = EquipmentProtocol.EquipmentProtocolMessagePack.CreateSetSelectedIndexRequest(_playerConnectionSetting.PlayerId, selectedIndex);
    _packetSender.Send(request);
}
```

（フィールド名・送信メソッド名は同ファイルの `AttackMapObject`（69-73行）の実装に合わせる）

- [ ] **Step 3: 手持ち3Dモデルを装備駆動へ移設する**

`HotBarHeldItemModel.cs` を `Equipment/EquipmentHeldItemModel.cs` へ移設し、参照元を `ILocalPlayerInventory`+ホットバー選択から `LocalPlayerEquipment.SelectedItem` に変更（`OnChanged` 購読で `UpdateAsync` 相当を駆動。AddressableロードとPlayerGrabItemManager連携のロジックは不変）。`HotBarView` から手持ちモデルの生成・駆動コードを外し、`EquipmentHeldItemModel` の生成は `HotBarView.Start()` から `LocalPlayerEquipment` をDIできる場所（`MainGameStarter` 等、`HotBarView` を生成している場所の隣）へ移す。

`HotBarView.Update()` からホイール切替（`InputManager.UI.SwitchHotBar` 読み取りの `SelectedHotBar()` ローカル関数のスクロール部分）を削除する（数字キー選択は残す）。

- [ ] **Step 4: コンパイルとコミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add moorestech_client/Assets/Scripts/Client.Game/ moorestech_client/Assets/Scripts/Client.Network/
git commit -m "feat: クライアント装備モデルLocalPlayerEquipmentと手持ちモデルの装備駆動化"
```

---

### Task 6: 採掘のサーバ権威化（サーバ側）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapObjectAcquisitionProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Common.Debug/DebugParameter.cs`（`DebugParameterKeys` に `MapObjectSuperMine` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:69-73`（`AttackMapObject` の `attackDamage` 引数削除。**このタスクに含めないと `Client.Network` がコンパイル不能になり `uloop run-tests` が完了できない** — シミュレーター指摘 2026-07-28）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningMiningCompleteState.cs:37`（呼び出しの引数削除のみの最小追随。フロー全体の書き換えはTask 7）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MapObjectAcquisitionProtocolTest.cs`（既存があれば修正・なければ新規）

**Interfaces:**
- Consumes: Task 3 の `PlayerInventoryData.EquipmentInventory`/`SelectedEquipmentIndex`、Task 1 の `ToolMaster`
- Produces:
  - `MapObjectMiningService`: `bool TryAttack(int playerId, IMapObject mapObject, IItemStack equippedItem, out List<IItemStack> earnedItems)` — ダメージ解決＋クールダウン検証を担う。DI登録（AddSingleton）
  - `GetMapObjectProtocolProtocolMessagePack` から `AttackDamage` を**削除**（`[Key(2)] PlayerId`, `[Key(3)] InstanceId` のみ）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test]
public void 対応ツール装備時のみサーバがダメージを算出して掘れる()
{
    // 装備0にツールを置き選択→AttackMapObject(instanceId)→HPがminingToolsのdamage分減る
    // With the tool equipped, one attack reduces HP by the master-defined damage
    // ツール未装備→attack→HP不変
    // With nothing equipped, HP is unchanged
}

[Test]
public void attackSpeed未満の連打は無視される()
{
    // 1打目適用→即2打目→HPは1打分のみ。attackSpeed秒(テストマスタの値)待って3打目→適用
    // A second hit within attackSpeed is ignored; after waiting, the next hit applies
    // クールダウン閾値はattackSpeed×0.9(ジッタ余裕)であることに注意して待ち時間を選ぶ
    // Note the threshold is attackSpeed*0.9 (jitter margin) when choosing the wait time
    // 待機は Thread.Sleep((int)(attackSpeed * 1000) + 100) でよい（テストマスタのattackSpeedは小さい値にしておく）
}

[Test]
public void PickUpはツール不要で一撃取得()
{
    // MiningType=PickUpのmapObjectはツールなしで1回のattackで破壊されアイテムが入る
    // PickUp objects are destroyed by a single attack without any tool
}
```

対象mapObjectのinstanceIdはテストマスタ `map.json` から取得（既存のMapObject系テストの取得方法を丸写し）。テスト用 `map.json` の対象miningToolsの `attackSpeed` が1秒以上なら `0.2` 程度へ更新してよい（テスト専用データ）。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectAcquisition"`
Expected: FAIL

- [ ] **Step 2: 実装する**

`Game.Map/MapObjectMiningService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Map.Interface.MapObject;

namespace Game.Map
{
    public class MapObjectMiningService
    {
        // クールダウン判定の許容率。クライアントはattackSpeed間隔ちょうどで送るためジッタ余裕を持たせる
        // Cooldown tolerance; clients send at exactly attackSpeed intervals, so allow jitter
        private const double CooldownMarginRate = 0.9;

        // プレイヤー×mapObjectごとの最終打撃時刻。クールダウン検証に使う
        // Last hit time per (player, mapObject) for cooldown validation
        private readonly Dictionary<(int playerId, int instanceId), DateTime> _lastAttackTimes = new();

        public bool TryAttack(int playerId, IMapObject mapObject, IItemStack equippedItem, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // デバッグ高速採掘はダメージ最大・クールダウン無視
            // Debug super-mine: max damage, no cooldown
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.MapObjectSuperMine))
            {
                earnedItems = mapObject.Attack(int.MaxValue);
                return true;
            }

            var master = MasterHolder.MapObjectMaster.GetMapObjectMaster(mapObject.MapObjectGuid);

            // PickUpはツール不要の一撃取得
            // PickUp requires no tool and destroys in one hit
            if (master.MiningType == "PickUp")
            {
                earnedItems = mapObject.Attack(int.MaxValue);
                return true;
            }

            // 装備中ツールとminingToolsを照合しダメージを解決
            // Resolve damage by matching the equipped tool against miningTools
            var miningTools = ((MiningMiningParam)master.MiningParam).MiningTools;
            var equippedGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItem.Id).ItemGuid;
            foreach (var tool in miningTools)
            {
                if (tool.ToolItemGuid != equippedGuid) continue;

                // クールダウン: 前回打撃からattackSpeed×0.9秒未満は無視（ジッタで正当打を捨てない余裕）
                // Cooldown: ignore hits within attackSpeed*0.9s; the margin tolerates network jitter
                var key = (playerId, mapObject.InstanceId);
                var now = DateTime.UtcNow;
                if (_lastAttackTimes.TryGetValue(key, out var last) && (now - last).TotalSeconds < tool.AttackSpeed * CooldownMarginRate) return false;
                _lastAttackTimes[key] = now;

                earnedItems = mapObject.Attack(tool.Damage);
                return true;
            }
            return false;
        }
    }
}
```

（`MiningType` の生成型がenumならenum比較にする。`GetMapObjectMaster` の実名は `MapObjectMaster.cs` を開いて確認。空装備の `equippedItem` は空スタックが来る前提で、`ItemMaster.GetItemMaster` が空IDで例外になるなら先に空チェックでreturn false）

`DebugParameterKeys` に追加:

```csharp
public const string MapObjectSuperMine = "MapObjectSuperMine";
```

`MapObjectAcquisitionProtocol.cs` — `AttackDamage` フィールドを削除し、`GetResponse` を:

```csharp
var mapObject = ServerContext.MapObjectDatastore.Get(data.InstanceId);
var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();

// サーバがダメージ算出とクールダウン検証を行う（ADR-0004）
// The server resolves damage and validates the cooldown (ADR-0004)
if (!_mapObjectMiningService.TryAttack(data.PlayerId, mapObject, equippedItem, out var earnedItems)) return null;

if (!mapObject.IsDestroyed) _mapObjectUpdateEventPacket.SendHpUpdateEvent(mapObject);
foreach (var earnItem in earnedItems) playerInventory.MainOpenableInventory.InsertItem(earnItem);
return null;
```

`MapObjectMiningService` をDIにAddSingletonし、プロトコルのコンストラクタで取得する。

あわせて `VanillaApiSendOnly.AttackMapObject` の引数から `attackDamage` を削除し、呼び出し元 `MapObjectMiningMiningCompleteState.cs:37` は**引数を減らすだけの最小変更**を行う（採掘フロー全体の装備参照化はTask 7。この2点を先に直さないとクライアントを含む全asmdefのコンパイルが通らずStep 3のテストが実行できない）。

- [ ] **Step 3: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectAcquisition|MapObject"`
Expected: PASS。**注意**: テスト後に `DebugParameters` の `MapObjectSuperMine` を残置しない（テストで触った場合は `RemoveBool` でクリーンアップ。cache残置はPlaceBlock系テストを無言死させる前科がある）

```bash
git add moorestech_server/Assets/Scripts/Game.Map/ moorestech_server/Assets/Scripts/Server.Protocol/ moorestech_server/Assets/Scripts/Common.Debug/ moorestech_server/Assets/Scripts/Tests/
git commit -m "feat: 採掘をサーバ権威化(装備ツール×miningToolsのダメージ算出+attackSpeedクールダウン)"
```

---

### Task 7: 採掘のサーバ権威化（クライアント側追随）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMapObjectMiningState.cs`（Contextへ `LocalPlayerEquipment` 追加・`HotBarView` 依存の除去）
- Modify: 同 `MapObjectMiningFocusState.cs` / `MapObjectMiningMiningState.cs` / `MapObjectMiningMiningCompleteState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs`（キーを `DebugParameterKeys.MapObjectSuperMine` 参照へ）

**Interfaces:**
- Consumes: Task 5 の `LocalPlayerEquipment.SelectedItem`、Task 6 のプロトコル新シグネチャと `VanillaApiSendOnly.AttackMapObject(int mapObjectInstanceId)`（Task 6で変更済み）

- [ ] **Step 1: クライアント採掘フローを装備参照に書き換える**

- `MapObjectMiningControllerContext` — `HotBarView`/`ILocalPlayerInventory` への採掘用参照を `LocalPlayerEquipment` に差し替え（Contextの生成箇所も追随）
- `MapObjectMiningFocusState.MiningProcess`（57-107行）— 「ホットバー選択スロットのアイテム」を `context.LocalPlayerEquipment.SelectedItem` に置換。ツール照合・`ShowRecommendMiningTools`（「このアイテムが必要です」表示）・ツールチップのロジックは不変
- `MapObjectMiningMiningCompleteState` — `attackDamage` フィールドと `int.MaxValue` 上書き（31-34行）を削除し `ClientContext.VanillaApi.SendOnly.AttackMapObject(instanceId)` のみ送る。PickUp側（FocusState 48行）の `int.MaxValue` 引数も削除
- `MapObjectMiningMiningState` — 進捗計算（クライアント予測）は不変。SuperMine時の `dt *= 1000`（59-62行）も不変（サーバ側がクールダウンを無視するため成立する）

- [ ] **Step 2: コンパイル・全採掘テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject|Mining"`
Expected: PASS

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/
git commit -m "feat: クライアント採掘を装備スロット参照に変更しattackDamage送信を廃止"
```

---

### Task 8: Web — inventoryトピックへの装備追加・装備HUD・ホイールの移行

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/EquipmentActions.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx`
- Modify: `moorestech_web/webui/src/features/inventory/HotbarPanel/index.tsx` / `hotbarLogic.ts`（ホイール撤去）
- Modify: HUDレイアウトの組み込み先（`HotbarPanel` をマウントしている画面コンポーネント。grepで特定）

**Interfaces:**
- Consumes: Task 5 の `LocalPlayerEquipment`
- Produces:
  - `local_player.inventory` トピックに `equipment: SlotData[]` と `selectedEquipment: number` を追加（`hotbarSlots`/`selectedHotbar` は**plan Cまで残す**）
  - アクション `"inventory.select_equipment": { index: number }`
  - HUD右端の装備3枠 `EquipmentPanel`（選択中ハイライト・ホイールで循環・空枠も選択可）

**注意（webui-designスキル必読）:** 装飾はCSS/DOM/インラインSVG限定。画像アセットの新規追加は禁止。

- [ ] **Step 1: webuiの失敗するテストを書く**

装備の循環ロジックを `hotbarLogic.ts` から流用する形で `EquipmentPanel` 用の純ロジック `equipmentLogic.ts` を作り、先にテスト:

```typescript
// src/features/inventory/equipmentLogic.test.ts
import { describe, expect, it } from "vitest";
import { cycleEquipment } from "./equipmentLogic";

describe("cycleEquipment", () => {
  it("下方向で0→1→2→空(-1)→0と循環する", () => {
    expect(cycleEquipment(0, 1, 3)).toBe(1);
    expect(cycleEquipment(2, 1, 3)).toBe(-1); // -1 = 素手（空選択）
    expect(cycleEquipment(-1, 1, 3)).toBe(0);
  });
  it("上方向で逆順に循環する", () => {
    expect(cycleEquipment(0, -1, 3)).toBe(-1);
    expect(cycleEquipment(-1, -1, 3)).toBe(2);
  });
});
```

Run: `cd moorestech_web/webui && npm test`
Expected: FAIL

**設計注（サーバ表現との対応）:** 素手（空選択）はwebui内部で `-1` とし、サーバへは「装備スロット数」（=3、範囲外インデックス）を送る…のではなく、**サーバ側 `SetSelectedEquipmentIndex` も `-1`＝素手を正式に受け付ける**。Task 3 の実装時に `-1..スロット数-1` のクランプとし、`SelectedItem` は `-1` で空スタックを返す（Task 3/5 の実装者はこの注記に従うこと）。

- [ ] **Step 2: webuiを実装する**

- `equipmentLogic.ts` 実装でテストPASS
- `inventory.ts` スキーマ: `PlayerInventoryDataSchema` に `equipment: z.array(SlotDataSchema)` と `selectedEquipment: z.number()` を追加
- `actionContract.ts`: `"inventory.select_equipment": { index: number }` を追加（`ACTION_TYPES` 配列にも）
- `EquipmentPanel/index.tsx`（`HotbarPanel/index.tsx` の構造を踏襲）: `Topics.inventory` 購読で3枠表示・`selectedEquipment` ハイライト・GameScreen中のホイールで `dispatchAction("inventory.select_equipment", {index})`。HUD右端に配置（マウント先は `HotbarPanel` と同じ画面コンポーネント）
- `HotbarPanel/index.tsx` — ホイール処理（`accumulateHotbarWheel` 呼び出し）を削除（数字キーとクリックは残す）

Run: `cd moorestech_web/webui && npm test && npm run build`
Expected: PASS

- [ ] **Step 3: Unity側を実装する**

- `InventoryTopic.cs` — `LocalPlayerEquipment` を注入し、DTOに `Equipment`/`SelectedEquipment` を追加（`BuildJson` で装備3枠を出力。既存の `HotbarSlots` 切り出しはそのまま）
- `EquipmentActions.cs` — `SelectEquipmentActionHandler : IActionHandler`（`ActionType => "inventory.select_equipment"`、前例 `HotbarActions.cs:11`）。payloadの `index`（-1..2）を検証し `LocalPlayerEquipment.SetSelectedIndex(index)` を呼ぶ（そこからサーバ送信される。Task 5参照）。アクションハンドラの登録箇所は `SelectHotbarActionHandler` の登録行をgrepして同じ場所に追加

- [ ] **Step 4: コンパイル・確認・コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `cd moorestech_web/webui && npm run build && npm test` → PASS

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/ moorestech_web/webui/src/
git commit -m "feat: Webに装備HUD(右端3枠)とinventory.select_equipmentを追加しホイールを装備切替へ"
```

---

### Task 9: 最終確認 — moores-code-review

- [ ] **Step 1: 全テスト＋コンパイル＋webuiの最終確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Equipment|Tool|MapObject|Inventory"`
Run: `cd moorestech_web/webui && npm run build && npm test`
Expected: すべてPASS・エラー0

- [ ] **Step 2: 動作確認（プレイテスト・任意だが推奨）**

`unity-playmode-recorded-playtest` スキルのDSLで「装備スロットへ斧を移動→木を採掘→アイテム獲得」を確認する（採掘系DSLコマンドは未整備のため、`PlaytestDriver` に採掘用操作を足す必要が出たらplan Cのタスク6と合流させてよい）。

- [ ] **Step 3: 未コミット作業が無いことを確認してコミットする**

```bash
git status --short   # 空であること
```

- [ ] **Step 4: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

specの台帳: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md` の「判断記録（ADR）」を参照（装備の独立インベントリ・tools配列・1枠1個・サーバ権威採掘・クールダウン・SuperMineサーバ移設は裁定済み）。

planning中に新たに生じた判断:
- **装備同期は3点セット標準で新設**: `EquipmentUpdateEventPacket`（EventType文字列で slot/selected を分岐 — `MapObjectUpdateEventPacket` 前例）＋`PlayerInventoryResponseProtocol` 同梱＋`NetworkEventInventoryUpdater` 購読（出所: `.claude/rules/server-protocol.md` の標準に従う。agent前提（拒否権つき））
- **選択インデックスは `EquipmentInventoryData` が所有し `-1`＝素手を正式な値とする**（出所: agent前提（拒否権つき））。「空も選べる」裁定の表現として範囲を `-1..slotCount-1` にクランプ
- **クールダウンの時刻は `DateTime.UtcNow`・保持は `MapObjectMiningService` 内Dictionary**（出所: agent前提（拒否権つき））。サーバtick連動は不要（攻撃間隔は秒指定のため実時間でよい）。セーブしない（揮発でよい）
- **`MapObjectSuperMine` キーは `Common.Debug.DebugParameterKeys` へ移設しサーバが参照**（出所: ADR-0004裁定の具体化）。クライアントの `dt*=1000` 進捗加速は演出として残す
- **受入制限の形は `CanAccept(ItemId)`＋`GetMaxCountPerSlot(ItemId)` の2メソッド**（出所: agent前提（拒否権つき））。「ツール限定」と「1枠1個」を同一機構で表現し、移動サービス側の分岐を1箇所にする
- **チュートリアル `itemViewHighLight` は現状調査の結果、レシピパネルアンカー（`recipe.item-`）解決でありホットバー非依存**（出所: 現状調査 2026-07-28）。specのQA観点「装備HUDへ移ること」は「変更不要であることの確認」に読み替える
- **クールダウン閾値は `attackSpeed × 0.9`（ジッタ余裕）**（出所: シミュレーター予測 2026-07-28 → 適用。クライアントはattackSpeed間隔ちょうどで送るため厳密判定だと正当打が捨てられる）
- **`VanillaApiSendOnly.AttackMapObject` のシグネチャ変更はTask 6に含める**（出所: シミュレーター予測 2026-07-28 → 適用。Client.NetworkがServer.Protocolをソース参照するため、分離するとタスク単体でコンパイル不能になる）
