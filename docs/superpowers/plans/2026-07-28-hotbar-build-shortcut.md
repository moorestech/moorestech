---
spec: docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md
---

# Plan C: ホットバーの建築ショートカット化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ホットバーを「アイテムを持つインベントリ末尾9スロット」から「設置対象IDへの参照9枠（Satisfactory型の建築ショートカット）」へ作り替える。数字キーで即建築モードに入り、同キーで抜ける。割当はプレイヤー単位でセーブに永続する。

**Architecture:** ①サーバに `HotbarAssignmentDatastore`（プレイヤー×9枠のGuid、前例 `PlayerInventoryDataStore` の `Dictionary<playerId, data>` パターン）を新設しセーブに永続 ②同期は3点セット（`HotbarUpdateEventPacket`＋`GetHotbarProtocol`＋クライアント購読）、操作は `HotbarProtocol` 1本のOperation分岐（前例 `BlueprintProtocol`）③クライアントは非MonoBehaviourの `ClientHotbarDatastore` が割当と選択枠を所有し、uGUI `HotBarView`/`HotBarItem` は削除 ④メインインベントリ末尾9スロットの特別扱い（優先挿入・ソート除外・Web契約）を完全撤廃 ⑤Webは新トピック `local_player.hotbar`＋ビルドメニューからのD&D割当。

**Tech Stack:** C#（Unity / MessagePack / UniRx）/ TypeScript+React+zod（moorestech_web/webui）

**前提:** plan A（設置対象ID統一。カタログ・`IPlacementTarget.Id`・`PlacementTargetFactory` を提供）と plan B（装備スロット。採掘ツールがホットバー手持ちに依存しなくなる・ホイールが装備切替へ移行済み）の完了後に着手する。

**確認すべきドキュメント（着手前に必読）:**
- spec: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md`（決定は `docs/adr/0002`）
- `/creating-server-protocol`・`/creating-server-tests`・`/csharp-event-pattern`・`/webui-design` の各スキル
- Unity固有ファイル（Prefab/シーン）の変更は `uloop execute-dynamic-code` 経由のみ（テキスト直編集禁止）

## Global Constraints

- サーバー可変状態のクライアント同期は3点セット。プロトコルは1ドメイン1本でリクエスト内Operation enum分岐
- 割当の永続はGuid（設置対象ID）のみ。向き（`PickedDirection`）は保存しない。表示名・アイコン等のマスタ由来値も保存しない
- ロード時、設置対象カタログで解決できない割当は削除する。**アンロック状態は参照しない**（未解放は削除しない。ロード順依存を作らないため）
- 数字キー: 押すたびに建築モードへの出入りを切り替える。空枠を押すと建築モードを抜ける。割当はキー長押し（建築モード中）とWeb D&Dの2経路
- `Func<>` 禁止・partial 禁止・try-catch 原則禁止・単純getter/setterプロパティ禁止・1ファイル200行以下・イベントはUniRx・コメントは日本語英語2行セット
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`
- テスト実行後の「Domain Reload in progress」エラーは45秒待ってリトライ
- 各タスク末尾で必ずコミットする

## File Structure（このplanで触るファイルの全体像）

新規:
- `moorestech_server/Assets/Scripts/Game.Hotbar/Game.Hotbar.asmdef`（references: `Game.PlacementTarget`, `UniRx`）
- `moorestech_server/Assets/Scripts/Game.Hotbar/HotbarAssignmentDatastore.cs`
- `moorestech_server/Assets/Scripts/Game.Hotbar/PlayerHotbarSaveJsonObject.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/HotbarProtocol.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetHotbarProtocol.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/HotbarUpdateEventPacket.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Hotbar/ClientHotbarDatastore.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Hotbar/HotbarNetworkEventHandler.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Hotbar/HotbarTopic.cs`
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Hotbar/HotbarActions.cs`（旧 `Inventory/HotbarActions.cs` は削除）
- `moorestech_web/webui/src/bridge/contract/schemas/hotbar.ts`
- `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx`（旧 `features/inventory/HotbarPanel` は削除）

削除:
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs` / `HotBarItem.cs`（＋Prefab `Assets/Asset/UI/Prefab/Inventory/HotBar.prefab` / `HotBarItem.prefab` はuloop経由で削除）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/HotbarActions.cs`
- `moorestech_web/webui/src/features/inventory/HotbarPanel/` / `hotbarLogic.ts` / `hotbarLogic.test.ts`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotBarSlotToInventorySlotTest.cs`
- `PlayerInventoryConst` のホットバー系API（`HotBarSlotCount`/`HotBarSlotToInventorySlot`/`GetHotBarSlots`/`IsHotBarSlot`）と `PlayerInventoryData.GetHotBarSlotIndex`

変更（主要のみ）:
- `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs` / `WorldLoaderFromJson.cs` / `AssembleSaveJsonText.cs`
- `moorestech_server/Assets/Scripts/Game.PlayerInventory/ItemManaged/MainOpenableInventoryData.cs:68-73`（優先挿入撤廃）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SortInventoryProtocol.cs:39-40`（ソート除外撤廃）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs` / `PlaceBlockState.cs`（数字キー遷移・長押し割当）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs`（hotbarSlots/selectedHotbar削除）
- `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts` / `src/bridge/transport/actionContract.ts` / `src/features/inventory/InventoryPanel/`（ホットバー領域撤去）
- `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs` / `Operations/PlaytestItemOps.cs`（DSL作り替え）
- `.claude/skills/unity-playmode-recorded-playtest/references/hotbar-driven-systems.md`（全面書き換え）

---

### Task 1: サーバの割当ストア `HotbarAssignmentDatastore`（セーブ・ロード・無効割当削除）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Hotbar/Game.Hotbar.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.Hotbar/HotbarAssignmentDatastore.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Hotbar/PlayerHotbarSaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs` / `AssembleSaveJsonText.cs` / `WorldLoaderFromJson.cs`
- Modify: サーバDI登録（`MoorestechServerDIContainerGenerator`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotbarAssignmentDatastoreTest.cs`

**Interfaces:**
- Consumes: plan A の `PlacementTargetCatalog`
- Produces:
  - `class HotbarAssignmentDatastore`: `const int SlotCount = 9`、`IReadOnlyList<Guid> GetAssignments(int playerId)`（未割当は `Guid.Empty`）、`void SetAssignment(int playerId, int slot, Guid targetId)`、`void ClearAssignment(int playerId, int slot)`、`void SwapAssignments(int playerId, int slotA, int slotB)`、`IObservable<int> OnAssignmentChanged`（playerId を流すUniRx `Subject`）、`List<PlayerHotbarSaveJsonObject> GetSaveJsonObject()`、`void LoadHotbar(List<PlayerHotbarSaveJsonObject> saveData)`
  - `SetAssignment` はカタログで解決できないGuidを**無視**する（不正クライアント対策）。`LoadHotbar` は解決できない割当を `Guid.Empty` に落とす
  - セーブJSON: `PlayerHotbarSaveJsonObject { [JsonProperty("PlayerId")] int; [JsonProperty("Assignments")] List<string> }`（Guid文字列9個。空は `Guid.Empty` の文字列）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/UnitTest/Game/HotbarAssignmentDatastoreTest.cs`:

```csharp
[Test]
public void 割当はカタログ解決できるGuidのみ受け付けセーブロードで往復する()
{
    var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var datastore = serviceProvider.GetService<HotbarAssignmentDatastore>();
    var catalog = serviceProvider.GetService<PlacementTargetCatalog>();

    // カタログの実在エントリを割当→保持される
    // Assigning a real catalog entry is retained
    var validId = catalog.Entries[0].Id;
    datastore.SetAssignment(playerId: 1, slot: 3, validId);
    Assert.AreEqual(validId, datastore.GetAssignments(1)[3]);

    // 未知のGuidは無視される
    // Unknown GUIDs are ignored
    datastore.SetAssignment(1, 4, Guid.NewGuid());
    Assert.AreEqual(Guid.Empty, datastore.GetAssignments(1)[4]);

    // セーブ→ロード往復
    // Save and reload round-trips
    var saved = datastore.GetSaveJsonObject();
    var datastore2 = new HotbarAssignmentDatastore(catalog);
    datastore2.LoadHotbar(saved);
    Assert.AreEqual(validId, datastore2.GetAssignments(1)[3]);
}

[Test]
public void ロード時に解決できない割当は削除される()
{
    // Assignmentsに未知Guidを含むセーブをLoadHotbar→該当枠はGuid.Empty
    // Loading a save containing an unknown GUID clears that slot
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "HotbarAssignmentDatastoreTest"`
Expected: FAIL（型不在）

- [ ] **Step 2: 実装する**

asmdefは plan A の `Game.PlacementTarget.asmdef` と同形式（references: `"Game.PlacementTarget"`, `"UniRx"`）。データストア:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlacementTarget;
using UniRx;

namespace Game.Hotbar
{
    public class HotbarAssignmentDatastore
    {
        public const int SlotCount = 9;

        public IObservable<int> OnAssignmentChanged => _onAssignmentChanged;
        private readonly Subject<int> _onAssignmentChanged = new();

        // プレイヤーごとの9枠。未割当はGuid.Empty
        // 9 slots per player; Guid.Empty means unassigned
        private readonly Dictionary<int, Guid[]> _assignments = new();
        private readonly PlacementTargetCatalog _catalog;

        public HotbarAssignmentDatastore(PlacementTargetCatalog catalog)
        {
            _catalog = catalog;
        }

        public IReadOnlyList<Guid> GetAssignments(int playerId)
        {
            return GetOrCreate(playerId);
        }

        public void SetAssignment(int playerId, int slot, Guid targetId)
        {
            // カタログで解決できないIDは受け付けない
            // Reject ids the catalog cannot resolve
            if (!_catalog.TryGetEntry(targetId, out _)) return;
            GetOrCreate(playerId)[slot] = targetId;
            _onAssignmentChanged.OnNext(playerId);
        }

        public void ClearAssignment(int playerId, int slot) { /* Guid.Emptyを書きOnNext / write Guid.Empty and notify */ }
        public void SwapAssignments(int playerId, int slotA, int slotB) { /* 入替えてOnNext / swap and notify */ }

        public List<PlayerHotbarSaveJsonObject> GetSaveJsonObject() { /* 全playerを文字列化 / stringify all players */ }

        public void LoadHotbar(List<PlayerHotbarSaveJsonObject> saveData)
        {
            // 解決できない割当はロード時に削除する（アンロック状態は見ない）
            // Drop unresolvable assignments at load; unlock state is not consulted
        }

        private Guid[] GetOrCreate(int playerId) { /* 無ければ9枠生成 / create 9 empty slots on demand */ }
    }
}
```

セーブ配線: `WorldSaveAllInfoV1` に `[JsonProperty("hotbarAssignments")] List<PlayerHotbarSaveJsonObject> HotbarAssignments` をコンストラクタ引数ごと追加（`Blueprints` の追加前例に倣う。デフォルト引数禁止なので `AssembleSaveJsonText`・`WorldLoaderFromJson` の呼び出しを同時に直す）。ロード呼び出しは `WorldLoaderFromJson` 内で **`BlueprintDatastore.LoadBlueprints` の後**に置く（カタログがBPを解決できる状態にしてから割当検証するため。この順序を守るコメントを2行セットで残す）。DI登録は `AddSingleton<HotbarAssignmentDatastore>()`。

- [ ] **Step 3: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "HotbarAssignmentDatastoreTest"`
Expected: PASS

```bash
git add moorestech_server/Assets/Scripts/Game.Hotbar/ moorestech_server/Assets/Scripts/Game.SaveLoad/ moorestech_server/Assets/Scripts/Server.Boot/ moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotbarAssignmentDatastoreTest.cs
git commit -m "feat: ホットバー割当のサーバストアを新設(カタログ検証つき/セーブ永続)"
```

---

### Task 2: ホットバー同期の3点セット（HotbarProtocol・GetHotbarProtocol・イベントパケット）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/HotbarProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetHotbarProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/HotbarUpdateEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`／DI登録
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/HotbarProtocolTest.cs`

**Interfaces:**
- Consumes: Task 1 の `HotbarAssignmentDatastore`
- Produces:
  - ProtocolTag `"va:hotbar"`。`HotbarOperation { Assign, Clear, Swap }`。リクエスト（応答なし）: `[Key(2)] int PlayerId`, `[Key(3)] HotbarOperation Operation`, `[Key(4)] int Slot`, `[Key(5)] Guid TargetId`, `[Key(6)] int SlotB`。private ctor＋`CreateAssignRequest(playerId, slot, targetId)` / `CreateClearRequest(playerId, slot)` / `CreateSwapRequest(playerId, slotA, slotB)` のstatic factory
  - ProtocolTag `"va:getHotbar"`（前例 `GetGameUnlockStateProtocol`）。応答: `[Key(2)] Guid[] Assignments`（9個）
  - EventTag `"va:event:hotbarUpdate"`。MessagePack: `[Key(0)] Guid[] Assignments`（全量9個。割当変更は低頻度のため差分化しない）。`OnAssignmentChanged` 購読で該当playerへ `AddEvent`

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/HotbarProtocolTest.cs`（`/creating-server-tests` 参照。パケット送信の組み立ては `BlueprintProtocolTest` の形を丸写し）:

```csharp
[Test]
public void Assign_Clear_Swapが反映されGetHotbarで読める()
{
    // Assign(slot3, 実在ID) → GetHotbar応答[3]が一致
    // Assign then read back via GetHotbar
    // Swap(3, 5) → [5]に移動
    // Clear(5) → Guid.Empty
}

[Test]
public void 割当変更でイベントパケットが積まれる()
{
    // Assign後、EventProtocolProviderに"va:event:hotbarUpdate"が積まれ全量9個が入っている
    // Assign enqueues a hotbar update event carrying all 9 slots
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "HotbarProtocolTest"`
Expected: FAIL

- [ ] **Step 2: 実装する**

`/creating-server-protocol` の手順どおり3ファイルを作成し、`PacketResponseCreator` 登録＋`HotbarUpdateEventPacket` をDIにAddSingleton＋eager init（`MapObjectUpdateEventPacket` 等の既存登録行の隣）。`HotbarProtocol.GetResponse` はOperationでswitchし datastore の対応メソッドを呼び `return null`。クライアント送信は `VanillaApiSendOnly.SendHotbarRequest(HotbarProtocol.HotbarProtocolMessagePack request)` を1本、`VanillaApiWithResponse.GetHotbar(CancellationToken)` を1本追加（1プロトコル=1メソッド）。

- [ ] **Step 3: テスト実行とコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "HotbarProtocolTest"`
Expected: PASS

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/ moorestech_server/Assets/Scripts/Server.Event/ moorestech_server/Assets/Scripts/Server.Boot/ moorestech_client/Assets/Scripts/Client.Network/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/HotbarProtocolTest.cs
git commit -m "feat: ホットバー同期の3点セット(va:hotbar/va:getHotbar/イベントパケット)"
```

---

### Task 3: クライアントモデル `ClientHotbarDatastore` と購読

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Hotbar/ClientHotbarDatastore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Hotbar/HotbarNetworkEventHandler.cs`
- Modify: DI登録（`ILocalPlayerInventory` 登録箇所と同じコンテナ）・初期データ取得（`GetHotbar` を初期ハンドシェイク後のインベントリ取得と同じ場所で実行）

**Interfaces:**
- Consumes: Task 2 のイベント・プロトコル、plan A の `PlacementTargetCatalog`（クライアント側は `ClientBlueprintLibrary` 供給のカタログ）・`PlacementTargetFactory`
- Produces:
  - `class ClientHotbarDatastore`（非MonoBehaviour）:
    - `IReadOnlyList<Guid> Assignments`（9個）／`void ApplyAssignments(Guid[] assignments)`（購読・初期データから）
    - `int SelectedSlot`（クライアントのみの状態。ホットバー起点の建築モード中の枠。非建築時は `-1`）／`void SetSelectedSlot(int slot)`
    - `IObservable<Unit> OnChanged`（UniRx）
    - `void RequestAssign(int slot, Guid targetId)` / `RequestClear(int slot)` / `RequestSwap(int a, int b)` — `VanillaApi.SendOnly.SendHotbarRequest` を送る（ローカル書き換えはせずイベントエコーで反映）
    - `bool TryConsumeSelectRequest(out int slot)` — Web由来のキー/クリック選択をUIStateが消費する口（前例: `BuildMenuView.TryConsumeSelectedEntry`）／`void EnqueueSelectRequest(int slot)`
  - Task 4 のUIState・Task 5 のWebトピックが消費

- [ ] **Step 1: 実装する**

`HotbarNetworkEventHandler` は `NetworkEventInventoryUpdater.cs:21` と同じ形で `SubscribeEventResponse(HotbarUpdateEventPacket.EventTag, ...)` → `ApplyAssignments`。初期データは `GetHotbar` 応答を同じ初期化経路（メインインベントリの初期取得箇所）で適用する。

- [ ] **Step 2: コンパイルとコミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Hotbar/ moorestech_client/Assets/Scripts/Client.Starter/
git commit -m "feat: クライアントのホットバーモデルClientHotbarDatastoreと購読を追加"
```

---

### Task 4: 数字キーの建築モードトグルと長押し割当（UIState改修）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Hotbar/HotbarKeyInput.cs`（数字キー読み取りの共通ヘルパ）

**Interfaces:**
- Consumes: Task 3 の `ClientHotbarDatastore`、plan A の `PlacementTargetFactory`・`IPlacementTarget.Id`・カタログ
- Produces:
  - `static class HotbarKeyInput { static bool TryGetTappedSlot(out int slot); static bool TryGetLongPressedSlot(out int slot); }` — 既存の数字キー入力（`InputManager.UI.HotBar`。旧 `HotBarView.Update` の読み方を移植）を「タップ」と「長押し（0.5秒）」に判別する。長押し成立したキーはそのタップとして扱わない

**挙動仕様（spec裁定済み）:**
- 通常モード（GameScreen）で数字キー: 割当あり→その設置対象で建築モードへ（`UIStateEnum.PlaceBlock` へ `UITransitContextContainer.Create<IPlacementTarget>` を渡す。前例 `BuildMenuState.cs:32`）。割当なし→何もしない
- 建築モード（PlaceBlock）で数字キー（タップ）: 現在の選択枠と同じ→GameScreenへ。別の割当枠→`_placeSystemStateController.SetTarget` で持ち替え。空枠→GameScreenへ
- 建築モードで数字キー**長押し**: 現在の設置対象（`PlaceSystemStateController.CurrentTarget.Id`。スポイト直後・ビルドメニュー選択後を含む）をその枠へ `RequestAssign`
- Web由来の選択（`TryConsumeSelectRequest`。**WebのHUDクリック起点のみ** — 数字キーはUnity側一本化）もキー入力と同じ分岐で処理する
- `PlaceBlockState.OnExit` で `ClientHotbarDatastore.SetSelectedSlot(-1)`

- [ ] **Step 1: HotbarKeyInputを実装する**

旧 `HotBarView.Update()` の数字キー読み取り（`InputManager.UI.HotBar`＋`HotBarKeyBoardComposite`）を開いて読み方を移植し、押下時刻を記録して長押し判別を実装する（`#region Internal`＋ローカル関数を活用）。

- [ ] **Step 2: GameScreenState / PlaceBlockState に分岐を実装する**

`GameScreenState.GetNextUpdate()` に追加:

```csharp
// 数字キーで割当済み設置対象を持って建築モードへ入る
// A digit key enters build mode holding the assigned placement target
var selectRequested = HotbarKeyInput.TryGetTappedSlot(out var slot) || _clientHotbarDatastore.TryConsumeSelectRequest(out slot);
if (selectRequested)
{
    var targetId = _clientHotbarDatastore.Assignments[slot];
    if (targetId != Guid.Empty && _placementTargetCatalog.TryGetEntry(targetId, out var entry) && PlacementTargetFactory.TryCreate(entry, out var target))
    {
        _clientHotbarDatastore.SetSelectedSlot(slot);
        return UIStateEnum.PlaceBlock(UITransitContextContainer.Create<IPlacementTarget>(target)); // 実際の遷移記法はBuildMenuState.cs:32の形に合わせる
    }
}
```

`PlaceBlockState.GetNextUpdate()` に、上記仕様のタップ3分岐＋長押し割当を追加（長押し: `_placeSystemStateController.CurrentTarget` がnullでなければ `RequestAssign(slot, CurrentTarget.Id)`）。DI・コンストラクタ注入は両Stateの既存フィールドの形（`[Inject]` かコンストラクタか）に合わせる。

- [ ] **Step 3: コンパイルとコミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0

```bash
git add moorestech_client/Assets/Scripts/Client.Game/
git commit -m "feat: 数字キーの建築モードトグルと長押し割当を実装"
```

---

### Task 5: Web — 新トピック `local_player.hotbar`・HUD作り替え・D&D割当

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Hotbar/HotbarTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Hotbar/HotbarActions.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/HotbarActions.cs`
- Create: `moorestech_web/webui/src/bridge/contract/schemas/hotbar.ts`
- Create: `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx`（＋`hotbarDnd.ts` 等の純ロジック）
- Delete: `moorestech_web/webui/src/features/inventory/HotbarPanel/` / `hotbarLogic.ts` / `hotbarLogic.test.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts` / `src/features/buildMenu/BuildMenuSlot.tsx`（ドラッグ元）/ HUDマウント先

**Interfaces:**
- Consumes: Task 3 の `ClientHotbarDatastore`、plan A のWeb契約（`id`+`kind`・iconUrl解決）
- Produces:
  - トピック `local_player.hotbar`: `{ slots: ({ id: string; kind: string; label: string; iconUrl?: string } | null)[9], selectedSlot: number }`（表示情報はカタログ＋アイコン解決から毎回構築。`ClientHotbarDatastore.OnChanged` とビルドメニュー変更で再配信、PostLateUpdateデバウンス — 前例 `InventoryTopic`/`BuildMenuTopic`）
  - アクション: `"hotbar.select": { index }`（**クリックのみ**→`EnqueueSelectRequest`）、`"hotbar.assign": { slot, id }`、`"hotbar.clear": { slot }`、`"hotbar.swap": { from, to }`
  - HUD左側にホットバー9枠（数字キー表示・選択中ハイライト・空枠表示）。ビルドメニューのエントリをHUD枠へドラッグ&ドロップで割当。枠同士のドラッグでswap、枠外へドラッグでclear
  - **数字キーはUnity側 `HotbarKeyInput`（Task 4）に一本化する。新HotbarPanelはキーを一切listenしない**（旧HotbarPanelの `useGameLayerKeydown` による1-9監視は移植しない）。二重経路にすると1押下でWeb由来selectとUnityタップが両方成立し、建築モードに入って即抜ける往復が起きる（シミュレーター指摘 2026-07-28）

**注意:** `/webui-design` スキル必読。D&Dは既存流用元なし（ビルドメニューはクリック選択方式）。HTML5 DnDではなくポインタイベントで実装し、`InventoryPanel` の `splitDrag.ts` のような「純ロジックts＋薄いコンポーネント」構成に分離する。

- [ ] **Step 1: 失敗するテストを書く（D&D純ロジック）**

```typescript
// src/features/hotbar/hotbarDnd.test.ts
import { describe, expect, it } from "vitest";
import { resolveDropAction } from "./hotbarDnd";

describe("resolveDropAction", () => {
  it("ビルドメニューエントリを枠へ落とすとassign", () => {
    expect(resolveDropAction({ kind: "buildMenuEntry", id: "guid-a" }, { kind: "hotbarSlot", index: 2 }))
      .toEqual({ type: "hotbar.assign", payload: { slot: 2, id: "guid-a" } });
  });
  it("枠から枠へ落とすとswap", () => {
    expect(resolveDropAction({ kind: "hotbarSlot", index: 1 }, { kind: "hotbarSlot", index: 4 }))
      .toEqual({ type: "hotbar.swap", payload: { from: 1, to: 4 } });
  });
  it("枠から枠外へ落とすとclear", () => {
    expect(resolveDropAction({ kind: "hotbarSlot", index: 1 }, { kind: "outside" }))
      .toEqual({ type: "hotbar.clear", payload: { slot: 1 } });
  });
});
```

Run: `cd moorestech_web/webui && npm test`
Expected: FAIL

- [ ] **Step 2: webuiを実装する**

`hotbar.ts` スキーマ・`HotbarPanel`（`Topics.hotbar` 購読・**クリックのみ**で `dispatchAction("hotbar.select", {index})`・D&Dハンドラ。数字キーはlistenしない — Interfaces欄の一本化注記参照）・`actionContract.ts` に4アクション追加・`BuildMenuSlot.tsx` にドラッグ開始ハンドラ追加。旧 `features/inventory/HotbarPanel`・`hotbarLogic.*` を削除し、HUDマウント先を新パネルへ差し替え。

Run: `cd moorestech_web/webui && npm test && npm run build`
Expected: PASS（旧 `hotbarSlots` 参照が残っていればtscが検出する — Task 6で契約から消すため、このタスク時点では `inventory.ts` は未変更でよい）

- [ ] **Step 3: Unity側トピック・アクションを実装する**

`HotbarTopic.cs`（前例 `BuildMenuTopic.cs:19` のトピック名const・`BuildJson`・デバウンス構造）: `ClientHotbarDatastore.Assignments` を `PlacementTargetCatalog` で解決し、アイコンは `WebBuildMenuEntryCatalog`/`BuildMenuEntryDtoFactory` のアイコンURL解決をそのまま使う。`HotbarActions.cs` は4ハンドラ（select→`EnqueueSelectRequest`、assign/clear/swap→`ClientHotbarDatastore.RequestXxx`。assignのidはGuidパース失敗で無視）。トピック・アクションの登録箇所は既存の登録行（`BuildMenuTopic`/`SelectHotbarActionHandler` をgrep）と同じ場所。

- [ ] **Step 4: コンパイル・確認・コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `cd moorestech_web/webui && npm run build && npm test` → PASS

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/ moorestech_web/webui/src/
git commit -m "feat: Webホットバーを新トピック+D&D割当に作り替え"
```

---

### Task 6: 旧ホットバーの完全撤廃（uGUI削除・特別扱い撤廃・Web契約掃除）

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs` / `HotBarItem.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/PlayerInventoryConst.cs` / `PlayerInventoryData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory/ItemManaged/MainOpenableInventoryData.cs:68-73`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SortInventoryProtocol.cs:39-40`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts` / `src/features/inventory/InventoryPanel/`ほか `hotbarSlots`/`selectedHotbar` 参照全箇所
- Delete: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotBarSlotToInventorySlotTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` ほか `HotBarView` 参照全箇所（`ILocalPlayerInventory.GetHotBarInventorySlot` 拡張含む）
- Modify: ホットバー系APIを使う既存テスト（Grep実測16ファイル40箇所。主要: `Tests/CombinedTest/Server/PacketTest/OneClickCraftProtocolTest.cs:30,53,82`・`SendCommandProtocolTest.cs:35,42`・`GetMapObjectProtocolTest.cs:31`・`SortInventoryProtocolTest.cs:44`・`Tests/UnitTest/.../InventoryItemInsertServiceTest.cs:27,33` — 着手時に `grep -rn "HotBarSlot\|GetHotBarSlots\|IsHotBarSlot" moorestech_server/Assets/Scripts/Tests/` で全量を再確認し、末尾9スロット前提を通常スロット指定に書き換える）

- [ ] **Step 1: サーバ側の特別扱いを撤廃する**

- `PlayerInventoryConst` から `HotBarSlotCount`/`HotBarSlotToInventorySlot`/`GetHotBarSlots`/`IsHotBarSlot` を削除（`MainInventoryColumns` は残す）。`PlayerInventoryData.GetHotBarSlotIndex` も削除
- `MainOpenableInventoryData.InsertItem` の優先挿入（68-73行）を素の `InsertItem` に戻す
- `SortInventoryProtocol` のホットバー除外（39-40行）を削除（`ISortExcludedSlots` 合流は残す）
- `PlayerInventorySlotLevelMasterUtil` の「ホットバー数未満なら例外」検証は「1未満なら例外」に変更
- 削除でコンパイルエラーになった参照は、このタスクの後続Stepの削除対象（`HotBarView` 等）以外に残っていないことを確認しながら潰す

- [ ] **Step 2: uGUIを削除する**

- `HotBarView.cs`/`HotBarItem.cs` を削除し、参照元（`MainGameStarter`・`InventoryTopic`・旧 `HotbarActions`（Task 5で削除済み）・Playtest（Task 7で対応するため一時的にコンパイルが通る最小修正でよい））を整理
- シーン/Prefab上の `HotBar` オブジェクト削除は `uloop execute-dynamic-code` で行う（`GameObject.Find`→`Object.DestroyImmediate`→シーン保存、Prefabは `AssetDatabase.DeleteAsset("Assets/Asset/UI/Prefab/Inventory/HotBar.prefab")` 等）。テキスト直編集は禁止

- [ ] **Step 3: Web契約から旧ホットバーを消す**

- `InventoryTopic.cs` — `HotbarSlots`/`SelectedHotbar` フィールドと切り出しロジック（94-98行）を削除
- `inventory.ts` — `hotbarSlots`/`selectedHotbar` を削除。`actionContract.ts` から `"inventory.select_hotbar"` を削除
- `npm run build` の型エラーを潰しながら、`InventoryPanel` のホットバー領域・Shift配分のホットバー分岐・`selectedHotbar` 依存表示をすべて撤去

- [ ] **Step 4: コンパイル・全テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Inventory|Sort|Hotbar|OneClickCraft|SendCommand|GetMapObject"` → PASS（削除したテスト以外。フィルタはホットバーAPI波及テストを含むこと — シミュレーター指摘 2026-07-28）
Run: `cd moorestech_web/webui && npm run build && npm test` → PASS

```bash
git add -A
git commit -m "feat: 旧ホットバー(末尾9スロット特別扱い/uGUI/Web契約)を完全撤廃"
```

---

### Task 7: プレイテストDSLとスキル文書の更新

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs` / `Operations/PlaytestItemOps.cs`
- Modify: `GiveItemToHotbar`/`SelectHotbar` を使う全シナリオ（`grep -rn "GiveItemToHotbar\|SelectHotbar" moorestech_client/Assets/Scripts/Client.Playtest/ .claude/skills/` で洗い出す）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/references/hotbar-driven-systems.md`

- [ ] **Step 1: DSLを作り替える**

- `PlaytestItemOps.GiveItemToHotbar` を削除（アイテム付与は既存の `GiveItem`（メインインベントリ）系に一本化。無ければ同ファイル内のメイン版を使う）
- `PlaytestDriver.SelectHotbar(int slot)` は現行どおりキー入力エミュレート（`SemanticInput.TapKey(Key.Digit1 + slot)`）のまま残す — 意味が「持ち替え」から「建築モードトグル」に変わることをXMLコメントで明記
- `PlaytestDriver.AssignHotbar(int slot, string targetName)` を新設: カタログから表示名一致のエントリを探し `ClientHotbarDatastore.RequestAssign(slot, entry.Id)` → イベントエコー反映を `WaitUntil` で待つ（`_reporter.Act` ラップ・既存メソッドの形に合わせる）
- 旧DSL使用シナリオを新セマンティクス（`AssignHotbar`→`SelectHotbar`→設置）へ書き換える

- [ ] **Step 2: スキル文書を書き換える**

`hotbar-driven-systems.md` — 「ホットバー手持ち駆動（usePlaceItems）」の陳腐化した説明を全面削除し、新仕様（ホットバー=設置対象ショートカット・`AssignHotbar`/`SelectHotbar` の使い方・数字キートグルの注意点）に書き換える。

- [ ] **Step 3: プレイテストで通し確認する**

`unity-playmode-recorded-playtest` スキルのDSL一発実行で「AssignHotbar→SelectHotbar→ClickPlace→ブロックが置ける→同キーで建築モードを抜ける」を確認する。

Run: `.claude/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh <シナリオ>`（スキルのルーティングに従う）
Expected: result.json が成功

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest/ .claude/skills/unity-playmode-recorded-playtest/
git commit -m "feat: プレイテストDSLを新ホットバー仕様(AssignHotbar/トグル)に更新"
```

---

### Task 8: 最終確認 — moores-code-review

- [ ] **Step 1: 全テスト＋コンパイル＋webuiの最終確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Hotbar|Inventory|PlacementTarget|Blueprint"`
Run: `cd moorestech_web/webui && npm run build && npm test`
Expected: すべてPASS・エラー0

- [ ] **Step 2: QA観点の確認（specの検証・QA観点を1項目ずつ潰す）**

- 未解放ブロックの割当がロードで消えないこと（アンロック非参照をテストで確認済みだが、実セーブでも確認）
- ビルドメニューに出ないブロック（ベルトの坂等）のIDを `hotbar.assign` に投げても割当されないこと
- 旧セーブ（`hotbarAssignments` 無し）が空割当で起動すること

- [ ] **Step 3: 未コミット作業が無いことを確認してコミットする**

```bash
git status --short   # 空であること（.meta削除漏れに注意）
```

- [ ] **Step 4: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

specの台帳: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md` の「判断記録（ADR）」を参照（設置対象参照のみ保持・数字キートグル・サーバ永続・割当2経路・無効割当削除・特別扱い撤廃・uGUI削除・新トピックは裁定済み）。

planning中に新たに生じた判断:
- **`HotbarAssignmentDatastore` は新asmdef `Game.Hotbar` に置く**（出所: agent前提（拒否権つき））。ホットバーはインベントリでも設置対象定義でもない独立ドメインのため、`Game.PlayerInventory`・`Game.PlacementTarget` のどちらにも混ぜない（小さな単一責務asmdefの前例: `Game.UnlockState`）
- **同期はイベント全量9個＋`GetHotbarProtocol`＋購読の3点セット**（出所: `.claude/rules/server-protocol.md` 標準。全量送信は割当変更が低頻度のため差分化しない — agent前提（拒否権つき））
- **操作プロトコルは `va:hotbar` 1本のOperation分岐（Assign/Clear/Swap）＋static factory**（出所: 1ドメイン1本のプロトコル方針＋`FilterSplitterStateRequest` 前例。specの「プロトコルは1本にモード分岐」裁定の具体化）
- **`SetAssignment` はサーバ側でもカタログ検証し未知IDを無視する**（出所: agent前提（拒否権つき））。ロード時削除と同じ有効性定義（カタログ）を書き込み時にも適用
- **選択中の枠はクライアントのみの状態（サーバ非保持・非セーブ）**（出所: ADR-0002「割当と選択中の枠は非MonoBehaviourのクライアントモデルが所有」の具体化。装備の選択インデックス（サーバ保持）とは対称でないが、ホットバー選択は建築モードという純クライアント状態の一部のため）
- **Web由来の選択は `TryConsumeSelectRequest` でUIStateが消費する**（出所: agent前提（拒否権つき）。前例 `BuildMenuView.TryConsumeSelectedEntry` — Web→UIState橋渡しの既存パターン）
- **長押し閾値は0.5秒**（出所: agent前提（拒否権つき））
- **数字キーはUnity `HotbarKeyInput` に一本化し、WebのHotbarPanelはキーをlistenしない**（出所: シミュレーター予測 2026-07-28 → 適用。二重経路だと1押下で建築モードが往復する。UIStateの他キー遷移がInputManager駆動である前例に整合。Web一本化へ方針を変えたい場合のみ裁定を求める）
- **`SelectHotbar` DSLはキー入力エミュレートのまま維持・`GiveItemToHotbar` は削除し `AssignHotbar` を新設**（出所: ADR-0002の帰結の具体化）
