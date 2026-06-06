# アップグレードシステム フェーズA2（ネットワーク同期＋クライアントUI）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **API整合の前提:** 本プランのAPI名（`IModuleSlotInventoryComponent` / `MachineModuleSlotComponent.TryInsertModule` / `RemoveModule` / `GetEquippedModules` / `SlotCount` / `MachineModuleEffect`）は Phase A 計画（`2026-06-05-upgrade-system-phase-a.md`）に追従する。Phase A 実装でシグネチャが変わったら本プランも同期すること。

**Goal:** Phase A で作ったサーバー側モジュールスロットを、クライアントへ同期し、機械インベントリ画面でプレイヤーがモジュールを装着/取り外しできるようにする。

**Architecture:** モジュールスロットを既存のアイテム移動インフラに載せる。`MachineModuleSlotComponent` に `IOpenableInventory` を実装し、新しい `InventoryType.ModuleSlot` と専用リゾルバを追加することで、既存の `InventoryItemMoveProtocol`（drag&drop移動）・クライアントの `SubInventory` / `ItemSlotView` スタックをそのまま再利用する。挿入制約（モジュールのみ・Count=1・装着済み上書き拒否）は `InsertItem` オーバーライドに集約してサーバー側で再検証する。取得は `GetFluidInventoryProtocol` 型の専用プロトコルを新設。

**Tech Stack:** Unity / C# / NUnit / MessagePack プロトコル / UniRx / Addressables / uloop CLI

**設計仕様:** `docs/superpowers/specs/2026-06-05-upgrade-system-design.md`（§6 機械への組み込み）
**前提プラン:** `docs/superpowers/plans/2026-06-05-upgrade-system-phase-a.md`（完了済みであること）

---

## 設計判断（このプランで確定）

**Q: モジュール装着は既存の `InventoryItemMoveProtocol` に乗せるか、専用プロトコルを新設するか？**

**A: 既存の移動プロトコルに乗せる（`InventoryType.ModuleSlot` を追加）。** 理由:
- 既存の移動スタック（`OpenableInventoryResolver` → `IOpenableInventory` → `InventoryItemMoveService` / クライアントの drag&drop・`ItemSlotView`・`SubInventory`）を丸ごと再利用でき、クライアント実装が最小になる。
- Phase A の挿入制約は `MachineModuleSlotComponent.InsertItem`（`IOpenableInventory` 実装）に集約して保持する。サーバーは常にこの制約で再検証するため、クライアント送信値を信用しない安全性は維持される。
- 調査で確認した実infra: `Server.Protocol/PacketResponse/InventoryItemMoveProtocol.cs`（`OpenableInventoryResolver.Resolve(InventoryIdentifierMessagePack)` で `InventoryType` 別にリゾルバ委譲）、`Server.Util.MessagePack/InventoryIdentifierMessagePack.cs`（`InventoryType` + 座標/各種ID）。

**トレードオフ:** `MachineModuleSlotComponent` が `IOpenableInventory`（汎用インベントリ抽象）も実装することになり、Phase A の「独立インターフェース」の純度は少し下がる。ただし block-level の `[DisallowMultiple]` 制約は `IBlockInventory`/`IOpenableBlockInventoryComponent`（コンポーネント属性）に対するもので、`IOpenableInventory`（インベントリ抽象）の追加実装とは衝突しない。

> Phase A の `IModuleSlotInventoryComponent` API（`TryInsertModule` 等）はサーバー内ロジック用に残す。`IOpenableInventory.InsertItem` はそれらに委譲する薄いアダプタにする。

---

## ファイル構成

**サーバー新規:**
- `Server.Protocol/PacketResponse/GetModuleSlotInventoryProtocol.cs` — モジュールスロット内容の取得（`GetFluidInventoryProtocol` テンプレート）
- `Server.Protocol/PacketResponse/Util/InventoryService/Resolver/ModuleSlotInventoryIdentifierResolver.cs` — `InventoryType.ModuleSlot` 用リゾルバ

**サーバー修正:**
- `Game.Block/Blocks/Machine/Module/MachineModuleSlotComponent.cs` — `IOpenableInventory` 実装を追加（`InsertItem` で挿入制約、`GetItem`/`SetItem`/`GetSlotSize`）
- `Game.PlayerInventory.Interface/.../InventoryType.cs`（`InventoryType` enum定義箇所）— `ModuleSlot` を追加
- `Server.Util.MessagePack/InventoryIdentifierMessagePack.cs` — `CreateModuleSlotMessage(Vector3Int)` ファクトリ追加
- `Server.Protocol/PacketResponse/Util/InventoryService/OpenableInventoryResolver.cs` — `ModuleSlotInventoryIdentifierResolver` を `AddResolver` に登録
- `Server.Protocol/PacketResponse/PacketResponseCreator.cs` — `GetModuleSlotInventoryProtocol` を登録

**クライアント新規:**
- `Client.Game/InGame/UI/UIState/State/SubInventory/ModuleSlotSubInventorySource.cs` — モジュールスロットの sub-inventory source（`BlockSubInventorySource` テンプレート）

**クライアント修正:**
- `Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs` / `GearMachineBlockInventoryView.cs` — `moduleSlotCount > 0` ならモジュールスロット列を描画
- 機械インベントリ prefab（Unity YAML。**手編集禁止**。`uloop execute-dynamic-code` 経由 or ユーザー依頼でスロットUI要素を追加）

**テスト:**
- `Tests/CombinedTest/Core/MachineModuleSlotSyncTest.cs`（新規）— 取得プロトコル＋移動プロトコル経由の装着/拒否のサーバー統合テスト
- PlayMode: 既存 `EditModeInPlayingTest` 系（`TestElectricToGearGeneratorUI` ブロックが前例）にモジュール装着UI往復を追加

---

# タスク群A2-1: モジュールスロットの `IOpenableInventory` アダプタ

ゴール: `MachineModuleSlotComponent` が `IOpenableInventory` として振る舞い、移動インフラから挿入/取得でき、挿入制約を保持する。

### Task A2-1-1: `IOpenableInventory` の契約を確認

**Files:**
- Read: `moorestech_server/Assets/Scripts/Core.Inventory/IOpenableInventory.cs`（実パスは grep で確認）

- [ ] **Step 1: インターフェース定義を読む**

Run: `grep -rl "interface IOpenableInventory" moorestech_server/Assets/Scripts --include=*.cs`
そのファイルを Read し、必須メンバ（`InsertItem` / `SetItem` / `GetItem` / `GetSlotSize` / `InventoryItems` 等）の正確なシグネチャを書き出す。`VanillaChestComponent` か `VanillaMachineBlockInventoryComponent` の `IOpenableInventory` 実装例も1つ Read して、戻り値・空アイテムの扱いを確認する。

- [ ] **Step 2: メモを残す（実装前の契約確認のみ。コミット不要）**

### Task A2-1-2: 失敗するテストを書く（移動プロトコル経由の装着）

**Files:**
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotSyncTest.cs`

- [ ] **Step 1: テストを書く**

プレイヤーのメインインベントリにモジュールアイテムを置き、`InventoryItemMoveProtocol` 相当のサーバー処理でモジュールスロットへ移動 → 装着されることを検証。非モジュールは移動しても装着されないことを検証。

```csharp
using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineModuleSlotSyncTest
    {
        // メインインベントリ→モジュールスロットへの移動プロトコルでモジュールが装着されることを検証
        // Verify a module is equipped via the move protocol from main inventory to the module slot
        [Test]
        public void MoveModuleIntoSlotViaProtocolTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorGenerators().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            ServerContext.WorldBlockDatastore.TryAddBlock(
                ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1, 1), BlockDirection.North,
                Array.Empty<BlockCreateParam>(), out var block);

            // モジュールアイテムをプレイヤーのメインスロット0へ
            // Put a module item into player main slot 0
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data[0];
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            playerInventory.MainOpenableInventory.SetItem(0, itemStackFactory.Create(moduleItemId, 1));

            // 移動プロトコルを送る（メイン slot0 → モジュールスロット slot0）
            // Send the move protocol (main slot0 -> module slot0)
            var move = new InventoryItemMoveProtocol.InventoryItemMoveProtocolMessagePack(
                1, ItemMoveType.SwapSlot,
                InventoryIdentifierMessagePack.CreateMainMessage(0), 0,
                InventoryIdentifierMessagePack.CreateModuleSlotMessage(new Vector3Int(1, 1, 1)), 0);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(move).ToList());

            var moduleSlot = block.GetComponent<IModuleSlotInventoryComponent>();
            Assert.AreEqual(1, moduleSlot.GetEquippedModules().Count);
        }
    }
}
```

> `PacketResponseCreatorGenerators` / `packet.GetPacketResponse` の正確な型・呼び出しは、既存の移動プロトコルのテスト（`grep -rl "InventoryItemMoveProtocol" moorestech_server/Assets/Scripts/Tests`）を Read して合わせること。`MainOpenableInventory` のアクセサ名も既存テストで確認。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleSlotSyncTest"`
Expected: FAIL（`CreateModuleSlotMessage` 未定義 or `InventoryType.ModuleSlot` 未定義でコンパイルエラー）。

### Task A2-1-3: `InventoryType.ModuleSlot` と識別子ファクトリを追加

**Files:**
- Modify: `InventoryType` enum 定義ファイル（`grep -rl "enum InventoryType" moorestech_server/Assets/Scripts` で特定）
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/InventoryIdentifierMessagePack.cs`

- [ ] **Step 1: enum に値を追加**

```csharp
// モジュールスロット用のインベントリ種別を追加
// Add an inventory type for module slots
ModuleSlot,
```

- [ ] **Step 2: 識別子ファクトリを追加**

`CreateBlockMessage` の隣に追加（座標ベース。ブロックと同じく位置で解決）:

```csharp
public static InventoryIdentifierMessagePack CreateModuleSlotMessage(Vector3Int position)
{
    return new InventoryIdentifierMessagePack
    {
        InventoryType = InventoryType.ModuleSlot,
        BlockPosition = new Vector3IntMessagePack(position),
    };
}
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

### Task A2-1-4: `MachineModuleSlotComponent` に `IOpenableInventory` を実装

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleSlotComponent.cs`

- [ ] **Step 1: クラス宣言に `IOpenableInventory` を追加し、メンバを実装**

Task A2-1-1 で確認した契約に合わせる。`InsertItem` は Phase A の制約を再利用（`TryInsertModule` に委譲）。`SetItem`（移動が使う場合）も同制約でガードする。

```csharp
// IOpenableInventory 実装（移動インフラ用アダプタ）。挿入制約は TryInsertModule に集約
// IOpenableInventory adapter for the move infra; insert restriction delegated to TryInsertModule
public int GetSlotSize() => SlotCount;

public IItemStack GetItem(int slot)
{
    BlockException.CheckDestroy(this);
    return _slots[slot];
}

public void SetItem(int slot, IItemStack itemStack)
{
    BlockException.CheckDestroy(this);
    // 空への差し替え（取り外し）は許可、それ以外はモジュール制約で検証
    // Allow clearing (removal); otherwise validate with module restriction
    if (itemStack == null || itemStack.Id == ItemMaster.EmptyItemId) { _slots[slot] = ServerContext.ItemStackFactory.CreatEmpty(); return; }
    if (slot < 0 || slot >= SlotCount) return;
    if (itemStack.Count != 1) return;
    if (ResolveModuleOrNull(itemStack) == null) return;
    _slots[slot] = itemStack;
}

public IItemStack InsertItem(IItemStack itemStack)
{
    BlockException.CheckDestroy(this);
    // 空きスロットを探して1枚だけ装着。残りは戻す
    // Find an empty slot and equip exactly one; return the remainder
    if (itemStack == null || itemStack.Count < 1 || ResolveModuleOrNull(itemStack) == null) return itemStack;
    for (var i = 0; i < SlotCount; i++)
    {
        if (_slots[i].Id != ItemMaster.EmptyItemId) continue;
        _slots[i] = itemStack.SubItem(itemStack.Count - 1); // 1枚だけ装着（Countを1にする）
        return itemStack.SubItem(1); // 残り（Count-1）を返す
    }
    return itemStack;
}
```

> `IItemStack` の「1枚だけ取り出す」API（`SubItem` 等の正確な名前）は `ItemStack.cs` を Read して合わせる。`InventoryItems`/`OnInventoryUpdate` 等 `IOpenableInventory` が要求する他メンバも契約に従い実装（更新通知が必須なら `Subject<...>` を足す。既存 `VanillaChestComponent` の実装を踏襲）。

- [ ] **Step 2: Unity再起動 → コンパイル**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Expected: 成功。

### Task A2-1-5: `ModuleSlotInventoryIdentifierResolver` を追加・登録

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/Resolver/ModuleSlotInventoryIdentifierResolver.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/OpenableInventoryResolver.cs`

- [ ] **Step 1: リゾルバを実装**

`BlockInventoryIdentifierResolver`（同ディレクトリ）を Read してテンプレートにする。座標からブロックを引き、`IModuleSlotInventoryComponent`（=`IOpenableInventory`）を返す。

```csharp
using Core.Inventory;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.Util.InventoryService.Resolver
{
    // モジュールスロットを IOpenableInventory として解決する
    // Resolve a machine module slot as an IOpenableInventory
    public class ModuleSlotInventoryIdentifierResolver : IInventoryIdentifierResolver
    {
        public InventoryType InventoryType => InventoryType.ModuleSlot;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public ModuleSlotInventoryIdentifierResolver(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public IOpenableInventory Resolve(InventoryIdentifierMessagePack identifier)
        {
            var pos = identifier.BlockPosition.Vector3Int;
            if (!_worldBlockDatastore.ExistsComponent<IModuleSlotInventoryComponent>(pos)) return null;
            return _worldBlockDatastore.GetBlock<IModuleSlotInventoryComponent>(pos) as IOpenableInventory;
        }
    }
}
```

> `IInventoryIdentifierResolver` の正確なメンバ名（`InventoryType` / `Resolve`）と `BlockInventoryIdentifierResolver` の座標取得方法は同ディレクトリの実装で確認。`IModuleSlotInventoryComponent : IOpenableInventory` を継承させると `as` が不要になる（A2-1-4 で `IOpenableInventory` を実装するので、インターフェース側にも継承を足すか検討）。

- [ ] **Step 2: `OpenableInventoryResolver` に登録**

`AddResolver(new BlockInventoryIdentifierResolver(...))` の隣に追加。コンストラクタ引数に `ServerContext.WorldBlockDatastore` を渡す:

```csharp
AddResolver(new ModuleSlotInventoryIdentifierResolver(ServerContext.WorldBlockDatastore));
```

- [ ] **Step 3: コンパイル → テストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineModuleSlotSyncTest"`
Expected: PASS。

- [ ] **Step 4: 非モジュール拒否テストを追加**

A2-1-2 のテストに、非モジュールアイテムを移動しても装着されない（スロットが空のまま）ケースを追加し、PASS を確認。

```csharp
        // 非モジュールアイテムは移動してもスロットに入らない
        // A non-module item does not enter the slot even if moved
        [Test]
        public void NonModuleItemRejectedByMoveTest()
        {
            // ... 上と同じセットアップ。ForUnitTestItemId.ItemId1 をメインに置いて移動 ...
            // moduleSlot.GetEquippedModules().Count == 0 を検証
        }
```

- [ ] **Step 5: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Module/MachineModuleSlotComponent.cs \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/ \
  moorestech_server/Assets/Scripts/Server.Util/MessagePack/InventoryIdentifierMessagePack.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotSyncTest.cs
git commit -m "feat(server): equip modules via existing move protocol (ModuleSlot inventory type)"
```

---

# タスク群A2-2: モジュールスロット取得プロトコル

ゴール: クライアントが座標から「装着中モジュールのアイテム配列」を取得できる。

### Task A2-2-1: 失敗するテストを書く

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotSyncTest.cs`

- [ ] **Step 1: 取得プロトコルのテストを追加**

装着済みの機械に取得プロトコルを送り、装着アイテムIDが返ることを検証。

```csharp
        // 取得プロトコルが装着中モジュールのアイテム配列を返すことを検証
        // Verify the get protocol returns the equipped module item array
        [Test]
        public void GetModuleSlotInventoryProtocolTest()
        {
            // ... block 生成 + moduleSlot.TryInsertModule(0, moduleItem) ...
            var request = new GetModuleSlotInventoryProtocol.RequestMessagePack(new Vector3Int(1, 1, 1));
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request).ToList())[0];
            var response = MessagePackSerializer.Deserialize<GetModuleSlotInventoryProtocol.ResponseMessagePack>(responseBytes);
            Assert.AreEqual(4, response.Items.Length);          // SlotCount
            Assert.AreEqual((int)moduleItemId, response.Items[0].Id);
        }
```

- [ ] **Step 2: 失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GetModuleSlotInventoryProtocolTest"`
Expected: FAIL（`GetModuleSlotInventoryProtocol` 未定義）。

### Task A2-2-2: `GetModuleSlotInventoryProtocol` を実装

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetModuleSlotInventoryProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PacketResponseCreator.cs`

- [ ] **Step 1: プロトコルを実装（`GetFluidInventoryProtocol` テンプレート）**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.Component;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class GetModuleSlotInventoryProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getModuleSlotInv";

        public GetModuleSlotInventoryProtocol(ServiceProvider serviceProvider) { }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestMessagePack>(payload);
            var pos = data.Pos.Vector3Int;
            var datastore = ServerContext.WorldBlockDatastore;
            if (!datastore.ExistsComponent<IModuleSlotInventoryComponent>(pos))
                return new ResponseMessagePack(pos, Array.Empty<ItemMessagePack>());

            var slot = datastore.GetBlock<IModuleSlotInventoryComponent>(pos);
            // 全スロットをアイテム配列で返す（空スロット含む）
            // Return all slots as an item array (including empty slots)
            var items = Enumerable.Range(0, slot.SlotCount)
                .Select(i => new ItemMessagePack(slot.GetModule(i)))
                .ToArray();
            return new ResponseMessagePack(pos, items);
        }

        [MessagePackObject]
        public class RequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Pos { get; set; }
            [Obsolete("デシリアライズ用")] public RequestMessagePack() { }
            public RequestMessagePack(Vector3Int pos) { Tag = ProtocolTag; Pos = new Vector3IntMessagePack(pos); }
        }

        [MessagePackObject]
        public class ResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Pos { get; set; }
            [Key(3)] public ItemMessagePack[] Items { get; set; }
            [Obsolete("デシリアライズ用")] public ResponseMessagePack() { }
            public ResponseMessagePack(Vector3Int pos, ItemMessagePack[] items) { Tag = ProtocolTag; Pos = new Vector3IntMessagePack(pos); Items = items; }
        }
    }
}
```

> `ItemMessagePack`（`Server.Util.MessagePack`）が `IItemStack` を受けるコンストラクタを持つか確認（`ItemMessagePack.cs` を Read。`Id`+`Count` 形式）。無ければ `new ItemMessagePack(stack.Id, stack.Count)` 等に合わせる。

- [ ] **Step 2: `PacketResponseCreator` に登録**

`PacketResponseCreator.cs` を Read し、`GetFluidInventoryProtocol` を登録している箇所（`ProtocolTag` → 生成のマッピング）と同じ書式で `GetModuleSlotInventoryProtocol` を1行追加する。

- [ ] **Step 3: コンパイル → テストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GetModuleSlotInventoryProtocolTest"`
Expected: PASS。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetModuleSlotInventoryProtocol.cs \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PacketResponseCreator.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotSyncTest.cs
git commit -m "feat(server): add GetModuleSlotInventoryProtocol"
```

---

# タスク群A2-3: クライアントUI（モジュールスロット列の表示と装着）

ゴール: 機械インベントリ画面にモジュールスロットが表示され、プレイヤーがメインインベントリからdrag&dropで装着できる。

> **注（Unity固有ファイル）:** prefab/シーンの編集は **手編集禁止**。スロットUI要素の追加は `uloop execute-dynamic-code` 経由か、ユーザーへ手順を指示する。本タスク群のコードは C# のみで、prefab配線は別途行う。

### Task A2-3-1: クライアントのモジュールスロット sub-inventory source

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/ModuleSlotSubInventorySource.cs`

- [ ] **Step 1: 既存パターンを確認**

Read: `BlockSubInventorySource.cs` / `ISubInventorySource.cs`。ブロックインベントリがどう取得プロトコルを叩き、`ISubInventoryView` にスロットを供給するかを把握する。`InventoryType.Block` を使う識別子生成箇所を確認。

- [ ] **Step 2: モジュールスロット用 source を実装**

`BlockSubInventorySource` を踏襲し、取得は `GetModuleSlotInventoryProtocol`、識別子は `InventoryIdentifierMessagePack.CreateModuleSlotMessage(pos)` を使う。装着移動は既存のdrag&dropが `InventoryItemMoveProtocol` を `InventoryType.ModuleSlot` 宛に送ることで成立する（A2-1で対応済み）。

> 実コードの `BlockSubInventorySource` のメンバ構成（コンストラクタ・更新購読・スロット数取得）に正確に合わせる。クライアントの識別子生成ヘルパー（サーバーの `InventoryIdentifierMessagePack` に対応するクライアント側）が別にある場合はそれを使う。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/ModuleSlotSubInventorySource.cs
git commit -m "feat(client): add ModuleSlotSubInventorySource"
```

### Task A2-3-2: 機械インベントリViewにモジュールスロット列を描画

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/GearMachineBlockInventoryView.cs`
- Modify: 機械インベントリ prefab（**手編集禁止**。`execute-dynamic-code` or ユーザー依頼）

- [ ] **Step 1: View に表示処理を足す**

Read: `MachineBlockInventoryView.cs` / `CommonBlockInventoryViewBase.cs` / `GearEnergyTransformerUIView.cs`（機械画面に副パネルを足す前例）。`moduleSlotCount`（マスタ or 取得レスポンスのスロット数）に応じて `ItemSlotView` を並べる。スロットは `ModuleSlotSubInventorySource` に紐付ける。

> `moduleSlotCount` をクライアントがどこから知るか: (a) `GetModuleSlotInventoryProtocol` の `Items.Length`、(b) ブロックマスタの `ModuleSlotCount`（クライアント側マスタからも引ける）。(a) で十分（0なら列を出さない）。

- [ ] **Step 2: prefab にスロットUIコンテナを追加**

`uloop execute-dynamic-code` で機械インベントリ prefab にモジュールスロット用の `GridLayoutGroup` + `ItemSlotView` テンプレートを追加し、View にバインド。**手でYAML編集しない。** 手順が固まらない場合はユーザーに「prefabのどこにスロット列を置くか」を確認。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/GearMachineBlockInventoryView.cs
git commit -m "feat(client): render module slot row in machine inventory view"
```

---

# タスク群A2-4: PlayMode UI往復テスト

ゴール: 実機UIで機械を開き、モジュールを装着→閉じ開きで保持される往復を検証。

> **注:** EnterPlayMode はドメインリロードを起こす。memory「EditMode Test with EnterPlayMode」の手順（SessionStateフラグ・50秒待機・TestResults.xml読取）に従う。`TestElectricToGearGeneratorUI` ブロック（最近のコミット `5033f6d1b`/`694650fb3`）が機械UIのPlayModeテストの前例。

### Task A2-4-1: PlayMode 往復テストを追加

**Files:**
- Modify: 既存の `EditModeInPlayingTest` 系テスト（`grep -rl "TestElectricToGearGeneratorUI" moorestech_client/Assets/Scripts` で前例特定）

- [ ] **Step 1: テストを追加**

機械を配置→インベントリを開く→モジュールアイテムをメインに用意→スロットへ移動UI操作→`GetModuleSlotInventoryProtocol` 相当で装着確認→閉じて開き直し→装着保持を確認。

- [ ] **Step 2: 実行（ドメインリロード待機手順）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<追加テスト名>"`
ドメインリロードで失敗したら45秒待機して再試行、または50秒待機後 `~/Library/Application Support/sakastudio/moorestech/TestResults.xml` を読む。
Expected: PASS。

- [ ] **Step 3: コミット**

```bash
cd ~/moorestech
git add moorestech_client/Assets/Scripts/<追加テストファイル>
git commit -m "test(client): PlayMode module equip round-trip in machine UI"
```

---

## フェーズA2完了条件

- [ ] サーバー: モジュールスロットが `IOpenableInventory` として移動インフラに乗り、挿入制約（モジュールのみ・Count=1・上書き拒否）をサーバー側で再検証する
- [ ] サーバー: `GetModuleSlotInventoryProtocol` で装着内容を取得できる
- [ ] クライアント: 機械インベントリ画面にモジュールスロット列が表示される
- [ ] クライアント: メインインベントリからdrag&dropでモジュールを装着でき、画面とセーブに反映される
- [ ] PlayMode: 機械UIでの装着往復が保持される

## 残課題（A2範囲外）

- **GearMachine の省エネ消費適用** → `2026-06-06-upgrade-system-phase-a3-gear-efficiency.md`
- **品質軸（レベルファミリー）** → `2026-06-06-upgrade-system-phase-b-quality.md`
