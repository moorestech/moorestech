# プレイヤーインベントリ スロット増量アップグレード 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** プレイヤーインベントリのスロット数を「スロットレベル」で管理し、研究のclearedActions（gameAction）でレベルN解放（冪等）できるようにする。

**Architecture:** items.ymlルートに`playerInventorySlotLevels`配列（レベル=インデックス）を追加。グローバルな`PlayerInventorySlotLevelDataStore`がレベルを保持し、レベル上昇時に全プレイヤーのメインインベントリを`ExpandSlots`で末尾拡張。クライアントはプロトコルレスポンスのMain配列長からサイズを知り、UIスロットを動的生成する。ホットバーは常に「最後の9スロット」。

**Tech Stack:** Unity / C# / Mooresmaster SourceGenerator / MessagePack(通信) / Newtonsoft.Json(セーブ) / NUnit / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-05-player-inventory-slot-upgrade-design.md`

## Global Constraints

- 作業ディレクトリ: `/Users/katsumi/moorestech-worktrees/tree1`（git worktree。必ず最初に`pwd`確認）
- ブランチ: `feature/item-stack-upgrade`（作成済み・checkout済み）
- コンパイル: `uloop compile --project-path ./moorestech_client`（.cs変更後は必ず実行）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（サーバーテストもクライアントプロジェクトから実行する）
- **新規サーバー側.csファイルを追加したら、そのファイルをテスト実行/コンパイルする前にUnity Editorの再起動が必要**（immutable package扱いのため。Refresh/Resolveでは不可）。再起動はユーザーに依頼すること
- uloopで「Unity is reloading (Domain Reload in progress)」が出たら45秒待ってリトライ
- partial禁止・try-catch禁止・デフォルト引数禁止・1ファイル200行以下・単純getter/setterプロパティ禁止（値SetはSetHogeメソッド）
- コメントは日本語・英語の2行セット（各1行厳守）を主要処理に付ける。自明なコメントは書かない
- `Mooresmaster.Model.*`クラスの手動作成禁止（SourceGenerator生成物）
- Prefab/シーンのテキスト直接編集禁止。変更は`uloop execute-dynamic-code`（Unity Editor経由）のみ
- 各タスク完了ごとにコミットする

---

### Task 1: スキーマ追加とテスト用マスタデータ

**Files:**
- Modify: `VanillaSchema/items.yml`（末尾、levelFamiliesの後）
- Modify: `VanillaSchema/ref/gameAction.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/research.json`

**Interfaces:**
- Produces: 生成コード `Items.PlayerInventorySlotLevels`（`PlayerInventorySlotLevelElement[]`、optionalなのでJSONにキーが無い場合null）、`PlayerInventorySlotLevelElement.SlotCount`（int）、`GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel`（string定数）、`UnlockPlayerInventorySlotLevelGameActionParam.Level`（int）
- テストマスタ: レベル0=45スロット、レベル1=54スロット。研究ノード `aaaaaaaa-bbbb-cccc-dddd-000000000001`（Test1アイテム1個消費、クリアでレベル1解放、前提なし）

- [ ] **Step 1: items.yml のルート properties 末尾（levelFamiles ブロックの後）に追加**

```yaml
- key: playerInventorySlotLevels
  type: array
  optional: true
  overrideCodeGeneratePropertyName: PlayerInventorySlotLevelElement
  items:
    type: object
    properties:
    - key: slotCount
      type: integer
```

（既存の`levelFamilies`と同じ「optionalなルート配列」パターン。csc.rspはitems.yml/gameAction.yml登録済みのため変更不要）

- [ ] **Step 2: gameAction.yml に enum オプションと case を追加**

`gameActionType` の options 末尾に `- unlockPlayerInventorySlotLevel` を追加し、cases 末尾（playSkit の後）に追加:

```yaml
    - when: unlockPlayerInventorySlotLevel
      type: object
      isDefaultOpen: true
      properties:
      - key: level
        type: integer
```

- [ ] **Step 3: _CompileRequester.cs の dummyText を任意の新しい値に変更**（SourceGeneratorトリガー）

- [ ] **Step 4: ForUnitTest の items.json にルートキーを追加**

`"levelFamilies"` と同じ階層に:

```json
"playerInventorySlotLevels": [
  { "slotCount": 45 },
  { "slotCount": 54 }
]
```

レベル0=45（既存テストの前提サイズを維持）、レベル1=54。

- [ ] **Step 5: ForUnitTest の research.json の data 配列末尾にノード追加**

```json
{
  "researchNodeGuid": "aaaaaaaa-bbbb-cccc-dddd-000000000001",
  "researchNodeName": "Inventory Slot Upgrade Research",
  "researchNodeDescription": "unlock inventory slot level 1",
  "prevResearchNodeGuid": "",
  "clearedActions": [
    {
      "gameActionType": "unlockPlayerInventorySlotLevel",
      "gameActionParam": { "level": 1 }
    }
  ],
  "graphViewSettings": { "UIPosition": [0, 0], "UIScale": [1, 1, 1], "IconItem": "" },
  "consumeItems": [
    { "itemGuid": "00000000-0000-0000-1234-000000000001", "itemCount": 1 }
  ],
  "prevResearchNodeGuids": []
}
```

- [ ] **Step 6: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: SUCCESS（`Mooresmaster could not be found`系エラーが出たらスキーマ記述ミス。yaml_spec.md参照で修正）

- [ ] **Step 7: コミット**

```bash
git add VanillaSchema moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/
git commit -m "feat: add playerInventorySlotLevels schema and unlockPlayerInventorySlotLevel gameAction"
```

---

### Task 2: スロットレベル解決ユーティリティとPlayerInventoryConstの新API（追加のみ）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/PlayerInventorySlotLevelMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/PlayerInventoryConst.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/HotBarSlotToInventorySlotTest.cs`（新signatureのテスト追記）

**Interfaces:**
- Produces: `PlayerInventorySlotLevelMasterUtil.GetSlotCount(int level)` → int（未定義/空配列なら45、levelは[0, 最大]にクランプ）、`PlayerInventorySlotLevelMasterUtil.GetMaxLevel()` → int（未定義なら0）
- Produces: `PlayerInventoryConst.HotBarSlotCount`（const 9）、`PlayerInventoryConst.HotBarSlotToInventorySlot(int hotBarSlot, int mainInventorySize)`、`PlayerInventoryConst.GetHotBarSlots(int mainInventorySize)` → int[]、`PlayerInventoryConst.IsHotBarSlot(int slot, int mainInventorySize)`
- **Core.Master（ItemMaster等）は変更禁止。** プレイヤーインベントリのドメインロジックはGame層に置く。マスタ生成物へは既存の`MasterHolder.ItemMaster.Items`（public readonlyフィールド）経由で読むだけ
- **旧API（MainInventorySize等）はこのタスクでは削除しない**（Task 11で削除）

**⚠️ 新規サーバー.csファイル追加のため、作成後にUnity Editor再起動をユーザーに依頼してからコンパイル・テストを実行すること。**

- [ ] **Step 1: PlayerInventorySlotLevelMasterUtil.cs を新規作成**（Game.PlayerInventory.Interface。Core.Master参照はasmdefに既存）

```csharp
using System;
using Core.Master;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     items.jsonのplayerInventorySlotLevelsからレベル→スロット数を解決する
    ///     Resolves level → slot count from playerInventorySlotLevels in items.json
    /// </summary>
    public static class PlayerInventorySlotLevelMasterUtil
    {
        // スロットレベル未定義時の従来スロット数
        // Fallback slot count when slot levels are undefined in master data
        private const int FallbackMainInventorySlotCount = 45;

        public static int GetSlotCount(int level)
        {
            var levels = MasterHolder.ItemMaster.Items.PlayerInventorySlotLevels;
            if (levels == null || levels.Length == 0) return FallbackMainInventorySlotCount;

            // 範囲外レベルは[0, 最大]へクランプする
            // Out-of-range levels are clamped into [0, max level]
            var index = Math.Clamp(level, 0, levels.Length - 1);
            return levels[index].SlotCount;
        }

        public static int GetMaxLevel()
        {
            var levels = MasterHolder.ItemMaster.Items.PlayerInventorySlotLevels;
            if (levels == null || levels.Length == 0) return 0;
            return levels.Length - 1;
        }
    }
}
```

（生成プロパティ名が`PlayerInventorySlotLevels`/`SlotCount`と異なる場合はコンパイルエラーで判明するので生成コードに合わせる）

- [ ] **Step 2: PlayerInventoryConst.cs に新APIを追加**（既存メンバーは残す）

```csharp
        public const int HotBarSlotCount = 9;

        // ホットバーは常にメインインベントリの最後の9スロット
        // The hotbar is always the last nine slots of the main inventory
        public static int HotBarSlotToInventorySlot(int hotBarSlot, int mainInventorySize)
        {
            if (hotBarSlot < 0 || HotBarSlotCount <= hotBarSlot)
                throw new Exception("ホットバーは0～8までです");
            return mainInventorySize - HotBarSlotCount + hotBarSlot;
        }

        public static int[] GetHotBarSlots(int mainInventorySize)
        {
            var slots = new int[HotBarSlotCount];
            for (var i = 0; i < HotBarSlotCount; i++) slots[i] = mainInventorySize - HotBarSlotCount + i;
            return slots;
        }

        public static bool IsHotBarSlot(int slot, int mainInventorySize)
        {
            return mainInventorySize - HotBarSlotCount <= slot && slot < mainInventorySize;
        }
```

- [ ] **Step 3: HotBarSlotToInventorySlotTest.cs にテスト追加**

```csharp
        [Test]
        public void DynamicSizeHotBarSlotTest()
        {
            // 45スロット時は従来と同じ36〜44がホットバー
            // With 45 slots the hotbar is the classic 36..44 range
            Assert.AreEqual(36, PlayerInventoryConst.HotBarSlotToInventorySlot(0, 45));
            Assert.AreEqual(44, PlayerInventoryConst.HotBarSlotToInventorySlot(8, 45));

            // 54スロット時は45〜53がホットバー
            // With 54 slots the hotbar shifts to 45..53
            Assert.AreEqual(45, PlayerInventoryConst.HotBarSlotToInventorySlot(0, 54));
            Assert.AreEqual(53, PlayerInventoryConst.HotBarSlotToInventorySlot(8, 54));

            Assert.IsTrue(PlayerInventoryConst.IsHotBarSlot(36, 45));
            Assert.IsFalse(PlayerInventoryConst.IsHotBarSlot(35, 45));
            Assert.IsFalse(PlayerInventoryConst.IsHotBarSlot(36, 54));
            Assert.AreEqual(new[] { 36, 37, 38, 39, 40, 41, 42, 43, 44 }, PlayerInventoryConst.GetHotBarSlots(45));
        }
```

- [ ] **Step 4: Unity再起動をユーザーに依頼 → コンパイル＆テスト実行**

Run: `uloop compile --project-path ./moorestech_client`（SUCCESS）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "HotBarSlotToInventorySlotTest"`
Expected: PASS

- [ ] **Step 5: コミット** `git add -A && git commit -m "feat: add dynamic-size hotbar helpers and inventory slot level master util"`

---

### Task 3: PlayerInventorySlotLevelDataStore（新規store）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/IPlayerInventorySlotLevelDataStore.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerInventory/PlayerInventorySlotLevelDataStore.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:137`付近
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/PlayerInventorySlotLevelTest.cs`（新規）

**Interfaces:**
- Produces:

```csharp
public interface IPlayerInventorySlotLevelDataStore
{
    int CurrentLevel { get; }
    int CurrentSlotCount { get; }
    IObservable<int> OnSlotCountChanged { get; } // UniRx。流れる値は新しいスロット数
    void UnlockLevel(int level);
    void LoadLevel(int level);
    int GetSaveLevel();
}
```

- イベントはC# eventではなく**UniRx**を使う（Game.UnlockStateと同様のスタイル）。`Game.PlayerInventory.asmdef`のreferencesに`"UniRx"`を追加する（Game.UnlockState.asmdefに前例あり）。インターフェース宣言は`System.IObservable`なのでGame.PlayerInventory.Interface側にUniRx参照は不要

**⚠️ 新規サーバー.csファイル追加のため、作成後にUnity Editor再起動をユーザーに依頼してからコンパイル・テストを実行すること。**

- [ ] **Step 1: 失敗するテストを書く**（`Tests/CombinedTest/Game/PlayerInventorySlotLevelTest.cs`）

```csharp
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class PlayerInventorySlotLevelTest
    {
        // レベル解放でスロット数が上がり、冪等に動作する
        // Unlocking a level raises the slot count and behaves idempotently
        [Test]
        public void UnlockLevelIsIdempotentTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();

            var eventCount = 0;
            store.OnSlotCountChanged.Subscribe(_ => eventCount++);

            Assert.AreEqual(0, store.CurrentLevel);
            Assert.AreEqual(45, store.CurrentSlotCount);

            store.UnlockLevel(1);
            Assert.AreEqual(1, store.CurrentLevel);
            Assert.AreEqual(54, store.CurrentSlotCount);
            Assert.AreEqual(1, eventCount);

            // 同一・下位レベルの再発火では何も起きない
            // Re-firing the same or a lower level changes nothing
            store.UnlockLevel(1);
            store.UnlockLevel(0);
            Assert.AreEqual(1, store.CurrentLevel);
            Assert.AreEqual(1, eventCount);

            // マスタ範囲外は最大レベルにクランプ
            // Levels beyond master definition clamp to the max level
            store.UnlockLevel(99);
            Assert.AreEqual(1, store.CurrentLevel);
        }
    }
}
```

- [ ] **Step 2: インターフェースを作成**（`Game.PlayerInventory.Interface/IPlayerInventorySlotLevelDataStore.cs`）

```csharp
using System;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     プレイヤーインベントリのスロット数レベルをワールド共通で保持する
    ///     Holds the world-global player inventory slot level
    /// </summary>
    public interface IPlayerInventorySlotLevelDataStore
    {
        int CurrentLevel { get; }
        int CurrentSlotCount { get; }

        /// <summary>スロット数が実際に増えたときのみ発行。流れる値は新しいスロット数</summary>
        IObservable<int> OnSlotCountChanged { get; }

        void UnlockLevel(int level);
        void LoadLevel(int level);
        int GetSaveLevel();
    }
}
```

- [ ] **Step 3: 実装を作成**（`Game.PlayerInventory/PlayerInventorySlotLevelDataStore.cs`）

```csharp
using System;
using Game.PlayerInventory.Interface;
using UniRx;

namespace Game.PlayerInventory
{
    public class PlayerInventorySlotLevelDataStore : IPlayerInventorySlotLevelDataStore
    {
        public int CurrentLevel => _currentLevel;
        public int CurrentSlotCount => PlayerInventorySlotLevelMasterUtil.GetSlotCount(_currentLevel);
        public IObservable<int> OnSlotCountChanged => _onSlotCountChanged;

        private readonly Subject<int> _onSlotCountChanged = new();
        private int _currentLevel;

        public void UnlockLevel(int level)
        {
            // レベルは下がらない冪等操作。範囲外は最大レベルへクランプ
            // Idempotent unlock; the level never decreases and clamps to the max defined level
            var clamped = Math.Clamp(level, 0, PlayerInventorySlotLevelMasterUtil.GetMaxLevel());
            if (clamped <= _currentLevel) return;

            SetLevel(clamped);
        }

        public void LoadLevel(int level)
        {
            var clamped = Math.Clamp(level, 0, PlayerInventorySlotLevelMasterUtil.GetMaxLevel());
            if (clamped == _currentLevel) return;

            SetLevel(clamped);
        }

        public int GetSaveLevel()
        {
            return _currentLevel;
        }

        private void SetLevel(int level)
        {
            // スロット数が実際に変わったときだけイベント発行する
            // Publish the event only when the slot count actually changes
            var beforeSlotCount = CurrentSlotCount;
            _currentLevel = level;
            if (CurrentSlotCount != beforeSlotCount) _onSlotCountChanged.OnNext(CurrentSlotCount);
        }
    }
}
```

- [ ] **Step 4: asmdefにUniRx参照を追加＆DI登録**

`moorestech_server/Assets/Scripts/Game.PlayerInventory/Game.PlayerInventory.asmdef` の `references` 配列に `"UniRx"` を追加（Game.UnlockState.asmdefと同形式）。

`MoorestechServerDIContainerGenerator.cs` の `IPlayerInventoryDataStore` 登録行の直前に追加:

```csharp
            services.AddSingleton<IPlayerInventorySlotLevelDataStore, PlayerInventorySlotLevelDataStore>();
```

- [ ] **Step 5: Unity再起動をユーザーに依頼** → 再起動後 `uloop compile --project-path ./moorestech_client`（SUCCESS）

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerInventorySlotLevelTest"`
Expected: PASS

- [ ] **Step 7: コミット** `git commit -am "feat: add PlayerInventorySlotLevelDataStore with idempotent level unlock"`（新規ファイルは`git add`）

---

### Task 4: OpenableInventoryItemDataStoreService.ExpandSlots

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Inventory/OpenableInventoryItemDataStoreService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/PlayerInventorySlotLevelTest.cs`（追記）

**Interfaces:**
- Produces: `OpenableInventoryItemDataStoreService.ExpandSlots(int newSlotCount)` — 末尾に空スロット追加のみ。縮小要求は無視。追加スロットごとに既存の`_onInventoryUpdate`イベントを発火

- [ ] **Step 1: テスト追記**（PlayerInventorySlotLevelTest.cs内。usingに`Core.Inventory`,`Core.Item.Interface`,`Game.Context`,`Core.Master`を追加）

```csharp
        // 拡張しても既存アイテムはスロット位置ごと保持される
        // Expansion preserves existing items at their slot indices
        [Test]
        public void ExpandSlotsKeepsExistingItemsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var updatedSlots = new System.Collections.Generic.List<int>();
            var service = new OpenableInventoryItemDataStoreService((slot, _) => updatedSlots.Add(slot), ServerContext.ItemStackFactory, 45);
            service.SetItem(0, new ItemId(1), 5);
            service.SetItem(44, new ItemId(2), 7);
            updatedSlots.Clear();

            service.ExpandSlots(54);

            Assert.AreEqual(54, service.GetSlotSize());
            Assert.AreEqual(5, service.GetItem(0).Count);
            Assert.AreEqual(7, service.GetItem(44).Count);
            Assert.AreEqual(new[] { 45, 46, 47, 48, 49, 50, 51, 52, 53 }, updatedSlots);

            // 縮小要求は無視される
            // Shrink requests are ignored
            service.ExpandSlots(10);
            Assert.AreEqual(54, service.GetSlotSize());
        }
```

- [ ] **Step 2: 実装**（`GetSlotSize()`の下に追加）

```csharp
        public void ExpandSlots(int newSlotCount)
        {
            // 末尾に空スロットを追加する。縮小は非対応で無視する
            // Append empty slots at the tail; shrinking is not supported and ignored
            while (_inventory.Count < newSlotCount)
            {
                _inventory.Add(_itemStackFactory.CreatEmpty());
                InvokeEvent(_inventory.Count - 1);
            }
        }
```

- [ ] **Step 3: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerInventorySlotLevelTest"` → PASS

- [ ] **Step 4: コミット** `git commit -am "feat: add ExpandSlots to OpenableInventoryItemDataStoreService"`

---

### Task 5: メインインベントリの動的サイズ化（サーバーコア）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory/ItemManaged/MainOpenableInventoryData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory/PlayerInventoryDataStore.cs`
- Test: `PlayerInventorySlotLevelTest.cs`（追記）

**Interfaces:**
- Consumes: `IPlayerInventorySlotLevelDataStore`（Task 3）、`ExpandSlots`（Task 4）
- Produces: `MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent ev, int slotCount)` / `MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent ev, int slotCount, List<IItemStack> itemStacks)` / `MainOpenableInventoryData.ExpandSlots(int newSlotCount)`
- PlayerInventoryDataStoreはレベル変更イベント購読で全プレイヤー拡張、生成・ロード時は現在レベルのスロット数を使用

- [ ] **Step 1: テスト追記**

```csharp
        // レベル解放で既存プレイヤーのインベントリが拡張され、アイテムが保持される
        // Level unlock expands existing player inventories while preserving items
        [Test]
        public void UnlockLevelExpandsPlayerInventoryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();

            var inventory = inventoryDataStore.GetInventoryData(0);
            Assert.AreEqual(45, inventory.MainOpenableInventory.GetSlotSize());
            inventory.MainOpenableInventory.SetItem(44, new ItemId(1), 9);

            store.UnlockLevel(1);

            Assert.AreEqual(54, inventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(9, inventory.MainOpenableInventory.GetItem(44).Count);

            // レベル解放後に取得した新規プレイヤーも54スロット
            // A newly created player after the unlock also gets 54 slots
            var newPlayerInventory = inventoryDataStore.GetInventoryData(1);
            Assert.AreEqual(54, newPlayerInventory.MainOpenableInventory.GetSlotSize());
        }
```

- [ ] **Step 2: MainOpenableInventoryData のコンストラクタ変更と ExpandSlots 追加**

```csharp
        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent, int slotCount)
        {
            _playerId = playerId;
            _mainInventoryUpdateEvent = mainInventoryUpdateEvent;
            _openableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotCount);
        }

        public MainOpenableInventoryData(int playerId, MainInventoryUpdateEvent mainInventoryUpdateEvent, int slotCount, List<IItemStack> itemStacks) : this(playerId, mainInventoryUpdateEvent, slotCount)
        {
            for (var i = 0; i < itemStacks.Count; i++) _openableInventoryService.SetItemWithoutEvent(i, itemStacks[i]);
        }

        public void ExpandSlots(int newSlotCount)
        {
            _openableInventoryService.ExpandSlots(newSlotCount);
        }
```

InsertItemのホットバー優先スロットを動的化:

```csharp
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _openableInventoryService.InsertItemWithPrioritySlot(itemStack, PlayerInventoryConst.GetHotBarSlots(GetSlotSize()));
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return _openableInventoryService.InsertItemWithPrioritySlot(itemId, count, PlayerInventoryConst.GetHotBarSlots(GetSlotSize()));
        }
```

- [ ] **Step 3: PlayerInventoryDataStore を書き換え**（ファイル先頭に `using UniRx;` を追加）

```csharp
        private readonly IPlayerInventorySlotLevelDataStore _slotLevelDataStore;

        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent, IGrabInventoryUpdateEvent grabInventoryUpdateEvent, IPlayerInventorySlotLevelDataStore slotLevelDataStore)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent)mainInventoryUpdateEvent;
            _grabInventoryUpdateEvent = (GrabInventoryUpdateEvent)grabInventoryUpdateEvent;
            _slotLevelDataStore = slotLevelDataStore;

            // レベル上昇で全プレイヤーのメインインベントリを拡張する
            // Expand every player's main inventory when the slot level rises
            _slotLevelDataStore.OnSlotCountChanged.Subscribe(slotCount =>
            {
                foreach (var inventory in _playerInventoryData.Values)
                    ((MainOpenableInventoryData)inventory.MainOpenableInventory).ExpandSlots(slotCount);
            });
        }
```

`GetInventoryData`内: `new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _slotLevelDataStore.CurrentSlotCount)`

`LoadPlayerInventory`内（セーブ済みアイテム数がスロット数を超える場合の安全弁）:

```csharp
                // セーブ済みアイテム数が現レベルのスロット数を超える場合はアイテム数まで拡張する
                // Expand to the saved item count when it exceeds the current level's slot count
                var slotCount = System.Math.Max(_slotLevelDataStore.CurrentSlotCount, mainItems.Count);
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, slotCount, mainItems);
```

- [ ] **Step 4: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerInventorySlotLevelTest"` → PASS

- [ ] **Step 5: 既存テストのリグレッション確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ResearchDataStoreTest|InventoryItemInsertServiceTest|PlayerInventoryProtocolTest"`
Expected: PASS（ForUnitTestのレベル0=45なので既存挙動不変）

- [ ] **Step 6: コミット** `git commit -am "feat: make player main inventory slot count level-driven and expandable"`

---

### Task 6: GameActionExecutorへのアクション追加＋研究統合

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Action/GameActionExecutor.cs`
- Test: `PlayerInventorySlotLevelTest.cs`（追記）

**Interfaces:**
- Consumes: `GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel` / `UnlockPlayerInventorySlotLevelGameActionParam.Level`（Task 1生成）、`IPlayerInventorySlotLevelDataStore.UnlockLevel`（Task 3）
- 研究ノード `aaaaaaaa-bbbb-cccc-dddd-000000000001`（Task 1）

- [ ] **Step 1: テスト追記**（ResearchDataStoreTest.CompleteResearchForTest を流用）

```csharp
        // 研究完了のclearedActionsでスロットレベルが解放される
        // Completing research unlocks the slot level via clearedActions
        [Test]
        public void ResearchClearedActionUnlocksSlotLevelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            Assert.AreEqual(45, store.CurrentSlotCount);

            ResearchDataStoreTest.CompleteResearchForTest(serviceProvider, System.Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-000000000001"));

            Assert.AreEqual(1, store.CurrentLevel);
            Assert.AreEqual(54, store.CurrentSlotCount);
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(ResearchDataStoreTest.PlayerId);
            Assert.AreEqual(54, inventory.MainOpenableInventory.GetSlotSize());
        }
```

- [ ] **Step 2: GameActionExecutor 修正**

コンストラクタに`IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore`を追加しフィールド保持。

`ExecuteUnlockActions`のswitch whitelistに追加（ロード時の再実行対象にする）:

```csharp
                    case GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel:
```

`ExecuteAction`のswitchにcase追加＋`#region Internal`にローカル関数追加:

```csharp
                case GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel:
                    UnlockPlayerInventorySlotLevel();
                    break;
```

```csharp
            void UnlockPlayerInventorySlotLevel()
            {
                var level = ((UnlockPlayerInventorySlotLevelGameActionParam)action.GameActionParam).Level;
                _playerInventorySlotLevelDataStore.UnlockLevel(level);
            }
```

- [ ] **Step 3: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerInventorySlotLevelTest|ResearchDataStoreTest"` → PASS

- [ ] **Step 4: コミット** `git commit -am "feat: execute unlockPlayerInventorySlotLevel gameAction"`

---

### Task 7: セーブ・ロード

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Test: `PlayerInventorySlotLevelTest.cs`（追記）

**Interfaces:**
- Produces: セーブJSONに `"inventorySlotLevel": <int>`（旧セーブに無い場合はデフォルト0）
- ロード順: unlockState → **slotLevel** → world → rail → playerInventory（既存順に挿入）

- [ ] **Step 1: テスト追記**

```csharp
        // セーブ→ロードでレベルとインベントリサイズが復元される
        // Save then load restores the level and the inventory size
        [Test]
        public void SaveLoadRestoresSlotLevelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            store.UnlockLevel(1);
            inventory.MainOpenableInventory.SetItem(50, new ItemId(1), 3);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            var loadedStore = loadServiceProvider.GetService<IPlayerInventorySlotLevelDataStore>();
            Assert.AreEqual(1, loadedStore.CurrentLevel);

            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            Assert.AreEqual(54, loadedInventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(3, loadedInventory.MainOpenableInventory.GetItem(50).Count);
        }

        // レベル情報のない旧セーブ（アイテム数45）はレベル0でもアイテムを失わない
        // Legacy saves without level info keep all 45 items even at level 0
        [Test]
        public void LoadLegacySaveWithoutLevelKeepsItemsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            inventory.MainOpenableInventory.SetItem(44, new ItemId(1), 8);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // inventorySlotLevelキーを消して旧フォーマットを再現する
            // Strip the inventorySlotLevel key to emulate the legacy format
            var legacyJson = Newtonsoft.Json.Linq.JObject.Parse(saveJson);
            legacyJson.Remove("inventorySlotLevel");

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(legacyJson.ToString());

            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            Assert.AreEqual(45, loadedInventory.MainOpenableInventory.GetSlotSize());
            Assert.AreEqual(8, loadedInventory.MainOpenableInventory.GetItem(44).Count);
        }
```

usingに `Game.SaveLoad.Interface`, `Game.SaveLoad.Json` を追加。

- [ ] **Step 2: WorldSaveAllInfoV1 にフィールド追加**

コンストラクタ末尾パラメータに `int inventorySlotLevel` を追加し `InventorySlotLevel = inventorySlotLevel;`、プロパティ追加:

```csharp
        [JsonProperty("inventorySlotLevel")] public int InventorySlotLevel { get; }
```

- [ ] **Step 3: AssembleSaveJsonText 修正**

コンストラクタに`IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore`追加・フィールド保持。`new WorldSaveAllInfoV1(...)`の末尾引数に `_playerInventorySlotLevelDataStore.GetSaveLevel()` を追加。

- [ ] **Step 4: WorldLoaderFromJson 修正**

コンストラクタに`IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore`追加・フィールド保持。`Load()`内、`_gameUnlockStateDataController.LoadUnlockState(...)`の直後に:

```csharp
            // スロットレベルはプレイヤーインベントリより先にロードする
            // Load the slot level before player inventories
            _playerInventorySlotLevelDataStore.LoadLevel(load.InventorySlotLevel);
```

- [ ] **Step 5: コンパイル＆テスト**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerInventorySlotLevelTest|SaveJsonFileTest|AssemblePlayerInventorySaveJsonTextTest"` → PASS

- [ ] **Step 6: コミット** `git commit -am "feat: persist inventory slot level in world save"`

---

### Task 8: サーバープロトコルの動的サイズ対応

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlayerInventoryResponseProtocol.cs:29`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SortInventoryProtocol.cs:40`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockFromHotBarProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceTrainCarOnRailProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/AttachTrainCarToUnitProtocol.cs`

**Interfaces:**
- Consumes: `PlayerInventoryConst.HotBarSlotToInventorySlot(hotBar, size)` / `GetHotBarSlots(size)`（Task 2）
- Produces: playerInvRequestレスポンスのMain配列長 = 現在のスロット数（クライアントはこれでサイズを知る）

- [ ] **Step 1: PlayerInventoryResponseProtocol のループ上限を動的化**

```csharp
            //メインインベントリのアイテムを設定
            var slotSize = playerInventory.MainOpenableInventory.GetSlotSize();
            var mainItems = new List<ItemMessagePack>();
            for (var i = 0; i < slotSize; i++)
```

- [ ] **Step 2: SortInventoryProtocol のホットバー除外を動的化**

```csharp
            IEnumerable<int> excludeSlots = data.Target.InventoryType == InventoryType.Main
                ? PlayerInventoryConst.GetHotBarSlots(inventory.GetSlotSize())
                : Array.Empty<int>();
```

- [ ] **Step 3: ホットバー→インベントリスロット変換を3プロトコルでハンドラ側に移動**

各MessagePackクラスの `[IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);` を削除し、ハンドラでインベントリ取得後に変換する。例（PlaceBlockFromHotBarProtocol）:

```csharp
            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(data.HotBarSlot, inventoryData.MainOpenableInventory.GetSlotSize());
            var item = inventoryData.MainOpenableInventory.GetItem(inventorySlot);
```

以降の `data.InventorySlot` 参照をすべて `inventorySlot` に置換（PlaceTrainCarOnRail/AttachTrainCarToUnitは`mainInventory.GetSlotSize()`を使用）。

- [ ] **Step 4: コンパイル＆該当テスト**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerInventoryProtocolTest|SortInventoryProtocolTest|PlaceHotBarBlockProtocolTest|PlaceTrainCarOnRailProtocolTest|AttachTrainCar|RemoveTrainCar"` → PASS

- [ ] **Step 5: コミット** `git commit -am "feat: make inventory protocols slot-count dynamic"`

---

### Task 9: クライアントモデルの動的サイズ対応

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/LocalPlayerInventory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/LocalPlayerInventoryController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/ILocalPlayerInventoryExtension.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs`
- Modify: 5つのホットバー参照箇所（下記Step 4）

**Interfaces:**
- Produces: `ILocalPlayerInventory.MainSlotCount`（int getter）、`LocalPlayerInventory.EnsureMainSlotCount(int slotCount)`
- Produces（拡張メソッド）: `GetHotBarInventorySlot(this ILocalPlayerInventory, int hotBarSlot)` / `IsHotBarSlot(this ILocalPlayerInventory, int slot)`

- [ ] **Step 1: LocalPlayerInventory 修正**

インターフェースに `public int MainSlotCount { get; }` を追加。実装クラス:

```csharp
        public int MainSlotCount => _mainInventory.Count;
```

コンストラクタの初期化を master 駆動に変更:

```csharp
            // 初期サイズはレベル0のスロット数。実サイズはサーバーレスポンスで確定する
            // Initial size is the level-0 slot count; the real size comes from the server response
            var initialSlotCount = PlayerInventorySlotLevelMasterUtil.GetSlotCount(0);
            for (var i = 0; i < initialSlotCount; i++) _mainInventory.Add(itemStackFactory.CreatEmpty());
```

拡張メソッド用の成長メソッドを追加:

```csharp
        public void EnsureMainSlotCount(int slotCount)
        {
            // レベルアップ通知（範囲外スロットのイベント）で末尾に空スロットを足す
            // Grow with empty tail slots when an out-of-range slot event arrives after level up
            var itemStackFactory = ServerContext.ItemStackFactory;
            while (_mainInventory.Count < slotCount)
            {
                _mainInventory.Add(itemStackFactory.CreatEmpty());
                _onItemChange.OnNext(_mainInventory.Count - 1);
            }
        }
```

- [ ] **Step 2: LocalPlayerInventoryController 修正**

`SetMainItem` で範囲外スロットを受けたら成長させる:

```csharp
        public void SetMainItem(int slot, IItemStack itemStack)
        {
            if (slot >= _localPlayerInventory.MainSlotCount) _localPlayerInventory.EnsureMainSlotCount(slot + 1);
            _localPlayerInventory[slot] = itemStack;
        }
```

`GetServerInventoryIdentifier` / `GetServerInventorySlot` の `PlayerInventoryConst.MainInventorySize` を `_localPlayerInventory.MainSlotCount` に置換（ローカル関数からは`_localPlayerInventory`フィールドを参照）。

- [ ] **Step 3: ILocalPlayerInventoryExtension 修正**

```csharp
        public static int GetMainInventoryItemCount(this ILocalPlayerInventory localPlayerInventory, ItemId itemId)
        {
            var count = 0;
            for (var i = 0; i < localPlayerInventory.MainSlotCount; i++)
            {
                if (localPlayerInventory[i].Id == itemId) count += localPlayerInventory[i].Count;
            }
            return count;
        }

        // ホットバーは常にメインインベントリの最後の9スロット
        // The hotbar is always the last nine slots of the main inventory
        public static int GetHotBarInventorySlot(this ILocalPlayerInventory localPlayerInventory, int hotBarSlot)
        {
            return PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot, localPlayerInventory.MainSlotCount);
        }

        public static bool IsHotBarSlot(this ILocalPlayerInventory localPlayerInventory, int slot)
        {
            return PlayerInventoryConst.IsHotBarSlot(slot, localPlayerInventory.MainSlotCount);
        }
```

- [ ] **Step 4: 呼び出し箇所の置換**（すべて機械的な置換。各ファイルでILocalPlayerInventoryインスタンスが利用可能）

| ファイル | 旧 | 新 |
|---|---|---|
| `HotBarView.cs:29` | `_localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(SelectIndex)]` | `_localPlayerInventory[_localPlayerInventory.GetHotBarInventorySlot(SelectIndex)]` |
| `HotBarView.cs:74-78` | `var c = ...Columns; var r = ...Rows; var startHotBarSlot = c * (r - 1); if (slot < startHotBarSlot \|\| ...MainInventorySize <= slot) return;` | `var startHotBarSlot = _localPlayerInventory.MainSlotCount - PlayerInventoryConst.HotBarSlotCount; if (slot < startHotBarSlot \|\| _localPlayerInventory.MainSlotCount <= slot) return;` |
| `HotBarView.cs:140` | `PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)` | `_localPlayerInventory.GetHotBarInventorySlot(selectIndex)` |
| `MapObjectMiningFocusState.cs:60` | `PlayerInventoryConst.HotBarSlotToInventorySlot(context.HotBarView.SelectIndex)` | `context.LocalPlayerInventory.GetHotBarInventorySlot(context.HotBarView.SelectIndex)` |
| `CommonBlockPlaceSystem.cs:204` | `PlayerInventoryConst.HotBarSlotToInventorySlot(context.CurrentSelectHotbarSlotIndex)` | `_localPlayerInventory.GetHotBarInventorySlot(context.CurrentSelectHotbarSlotIndex)` |
| `GearChainPoleFrameInputCollector.cs:50` | `PlayerInventoryConst.HotBarSlotToInventorySlot(context.CurrentSelectHotbarSlotIndex)` | `_playerInventory.GetHotBarInventorySlot(context.CurrentSelectHotbarSlotIndex)` |
| `TrainRailPlaceSystem.cs:31` | `PlayerInventoryConst.HotBarSlotToInventorySlot(slotIndex)` | `_localPlayerInventory.GetHotBarInventorySlot(slotIndex)` |
| `DisplayEnergizedRange.cs:114` | `PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot)` | `_localPlayerInventory.GetHotBarInventorySlot(hotBarSlot)` |

必要に応じ `using Client.Game.InGame.UI.Inventory.Main;` を追加。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS

- [ ] **Step 6: コミット** `git commit -am "feat: make client local inventory slot count dynamic"`

---

### Task 10: クライアントUIのスロット動的生成

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/PlayerInventoryMainSlotsView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/PlayerInventoryViewController.cs`
- Prefab: `moorestech_client/Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab`（uloop経由でのみ変更）

**Interfaces:**
- Produces: `PlayerInventoryMainSlotsView.SetSlotCount(int slotCount)`（不足分をInstantiate、縮小なし）、`PlayerInventoryMainSlotsView.SlotViews`（`IReadOnlyList<ItemSlotView>`）、`PlayerInventoryMainSlotsView.OnSlotViewCreated`（`IObservable<ItemSlotView>`）
- Prefab: 45個の静的ItemSlotViewを削除し、動的生成に置き換え

- [ ] **Step 1: PlayerInventoryMainSlotsView を新規作成**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Common;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    ///     メインインベントリのスロットビューをスロット数に応じて動的生成する
    ///     Dynamically creates main inventory slot views to match the slot count
    /// </summary>
    public class PlayerInventoryMainSlotsView : MonoBehaviour
    {
        public IReadOnlyList<ItemSlotView> SlotViews => _slotViews;
        public IObservable<ItemSlotView> OnSlotViewCreated => _onSlotViewCreated;

        [SerializeField] private ItemSlotView itemSlotViewPrefab;
        [SerializeField] private Transform slotsParent;

        private readonly List<ItemSlotView> _slotViews = new();
        private readonly Subject<ItemSlotView> _onSlotViewCreated = new();

        public void SetSlotCount(int slotCount)
        {
            // 不足分だけ生成する。縮小（レベルダウン）は仕様上発生しない
            // Create only the missing views; shrinking never happens by design
            while (_slotViews.Count < slotCount)
            {
                var slotView = Instantiate(itemSlotViewPrefab, slotsParent);
                _slotViews.Add(slotView);
                _onSlotViewCreated.OnNext(slotView);
            }
        }
    }
}
```

（ItemSlotViewの実際の名前空間はコンパイルエラーで確認して合わせる）

- [ ] **Step 2: PlayerInventoryViewController を動的スロット対応に書き換え**

- `[SerializeField] private List<ItemSlotView> mainInventorySlotObjects;` を `[SerializeField] private PlayerInventoryMainSlotsView mainSlotsView;` に置換
- `Awake`: 静的listのforeach購読を削除し `mainSlotsView.OnSlotViewCreated.Subscribe(slotView => slotView.OnPointerEvent.Subscribe(ItemSlotUIEvent));` に変更
- `ItemSlotUIEvent`: `mainInventorySlotObjects.IndexOf(slotObject)` → `mainSlotsView.SlotViews` ベースのIndexOf（`((List<ItemSlotView>)... は不可なので手動ループか `.ToList().IndexOf`。手動ループ推奨）
- `InventoryViewUpdate` 冒頭に追加:

```csharp
            // スロット数の変化を検知してビューを生成する
            // Detect slot count changes and build missing slot views
            mainSlotsView.SetSlotCount(_playerInventory.LocalPlayerInventory.MainSlotCount);
```

- `InventoryViewUpdate` 内の `mainInventorySlotObjects` を `mainSlotsView.SlotViews` に置換
- `DirectMove` の Internal 関数を動的値へ置換:

```csharp
            InventoryType GetInventoryType(int index, bool hasSub)
            {
                var mainSlotCount = _playerInventory.LocalPlayerInventory.MainSlotCount;
                if (hasSub && index >= mainSlotCount) return InventoryType.SubInventory;
                if (_playerInventory.LocalPlayerInventory.IsHotBarSlot(index)) return InventoryType.HotBar;
                return InventoryType.MainInventory;
            }

            (int start, int end) GetTargetRange(InventoryType source, bool hasSub)
            {
                var mainSlotCount = _playerInventory.LocalPlayerInventory.MainSlotCount;
                var hotBarStart = mainSlotCount - PlayerInventoryConst.HotBarSlotCount;
                switch (source)
                {
                    case InventoryType.MainInventory:
                        return hasSub ? (mainSlotCount, mainSlotCount + _subInventory.Count) : (hotBarStart, mainSlotCount);
                    case InventoryType.HotBar:
                        return hasSub ? (mainSlotCount, mainSlotCount + _subInventory.Count) : (0, hotBarStart);
                    case InventoryType.SubInventory:
                        return (0, hotBarStart);
                    default:
                        return (0, 0);
                }
            }
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS

- [ ] **Step 4: プレハブ改修（uloop execute-dynamic-code、Unity Editor必須）**

1. まず構造を調査: `InventoryItems.prefab` をロードし、`PlayerInventoryViewController` の `mainInventorySlotObjects` 45個の親Transform（GridLayoutGroupの有無）とスロットのプレハブ元を確認するコードを実行
2. 改修内容:
   - スロット親オブジェクトに `PlayerInventoryMainSlotsView` を AddComponent
   - `itemSlotViewPrefab` に `Assets/AddressableResources/UI/ItemSlotView.prefab` の ItemSlotView を、`slotsParent` にスロット親Transformを割り当て
   - `PlayerInventoryViewController.mainSlotsView` に上記コンポーネントを割り当て
   - 45個の静的スロットGameObjectを削除
   - 親にGridLayoutGroupが無い場合はAddComponent（cellSize等は既存スロットのRectTransformから読み取った値を設定）
   - `PrefabUtility.SavePrefabAsset` で保存
3. 調査結果が想定と異なる場合（スロットが複数階層に分かれている等）は、その構造に合わせて slotsParent の付け方を調整する

- [ ] **Step 5: コンパイル＆クライアント起動確認**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop get-logs --project-path ./moorestech_client --log-type Error` → プレハブ関連エラーなし

- [ ] **Step 6: コミット**（.metaはUnity生成のものをそのままコミット）

```bash
git add -A && git commit -m "feat: dynamically generate main inventory slot views"
```

---

### Task 11: 旧APIの削除とテスト移行

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerInventory.Interface/PlayerInventoryConst.cs`（旧メンバー削除）
- Modify: 旧APIを参照する残存テスト（下記）

**Interfaces:**
- Produces: PlayerInventoryConst の最終形 = `MainInventoryColumns`、`HotBarSlotCount`、`HotBarSlotToInventorySlot(slot, size)`、`GetHotBarSlots(size)`、`IsHotBarSlot(slot, size)` のみ

- [ ] **Step 1: PlayerInventoryConst から削除**: `MainInventoryRows`、`MainInventorySize`、`HotBarSlots`配列、単一引数の`HotBarSlotToInventorySlot(int)`、単一引数の`IsHotBarSlot(int)`

- [ ] **Step 2: コンパイルエラーになったテストを移行**（すべて機械的置換）

対象と置換方針:
- `OneClickCraftProtocolTest.cs` / `GetMapObjectProtocolTest.cs` / `SendCommandProtocolTest.cs` / `PlaceHotBarBlockProtocolTest.cs` / `SortInventoryProtocolTest.cs` / `InventoryItemInsertServiceTest.cs`: 対象インベントリ取得後に `PlayerInventoryConst.HotBarSlotToInventorySlot(n, inventory.GetSlotSize())` / `PlayerInventoryConst.GetHotBarSlots(inventory.GetSlotSize())[0]` へ
- `PlaceTrainCarOnRailProtocolTest.cs` / `RemoveTrainCarProtocolTest.cs`: static プロパティでのスロット計算をテストメソッド内で `mainInventory.GetSlotSize()` を使う形に変更
- `PlayerInventoryProtocolTest.cs` / `InventoryItemInsertServiceTest.cs` / `AssemblePlayerInventorySaveJsonTextTest.cs` のループ上限 `PlayerInventoryConst.MainInventorySize` → `inventory.GetSlotSize()`（インベントリ未取得の箇所は取得してから）
- `HotBarSlotToInventorySlotTest.cs`: 旧signatureテストを削除（Task 2で新テスト追加済み）

- [ ] **Step 3: 残存参照ゼロ確認**

Run: `grep -rn "MainInventorySize\|MainInventoryRows\|HotBarSlots\b" --include="*.cs" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts | grep -v "GetHotBarSlots"`
Expected: ヒット0件

- [ ] **Step 4: コンパイル＆サーバー全テスト**

Run: `uloop compile --project-path ./moorestech_client` → SUCCESS
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.(UnitTest|CombinedTest)"` → PASS

- [ ] **Step 5: コミット** `git commit -am "refactor: remove fixed-size PlayerInventoryConst members"`

---

### Task 12: 最終検証

- [ ] **Step 1: クライアントプロジェクト全テスト実行**（edit-schemaスキルのCRITICAL要件。EditModeInPlayingTestがドメインリロードを起こすため、uloop切断時は45秒待機→リトライ、必要ならTestResults.xml直読）

Run: `uloop run-tests --project-path ./moorestech_client`
Expected: 全PASS

- [ ] **Step 2: 動作確認（PlayMode・任意）**: ワールド新規作成→インベントリを開き45スロット表示→研究またはデバッグでレベル解放→スロットが増えホットバーが最終行に追従することを確認。worktreeの場合 `../moorestech_master` シンボリックリンクの存在を確認（メモリ: worktree-master-symlink）

- [ ] **Step 3: 実データ側の作業をユーザーに引き継ぎ**: moorestech_master の items.json への `playerInventorySlotLevels` 実データ（初期半分〜45以上への段階）と研究ノードへの `unlockPlayerInventorySlotLevel` アクション設定は mooreseditor で実施（コード側スコープ外）

- [ ] **Step 4: 全作業コミット確認** `git status` がクリーンであること

---

## 補足メモ

- **通知経路**: レベルアップ時、サーバーは`ExpandSlots`内の`InvokeEvent`で新スロット分の`MainInventoryUpdateEvent`（空アイテム）を発火。クライアントは`NetworkEventInventoryUpdater.OnMainInventoryUpdateEvent`→`SetMainItem`が範囲外スロットで`EnsureMainSlotCount`を呼び自動成長する（Task 9 Step 2）。専用パケットは作らない
- **クライアントの初期サイズ**: `LocalPlayerInventory`はレベル0サイズで生成→ハンドシェイクの`SetMainInventory`（Clear+AddRange）でサーバーの実サイズに置き換わる（既存実装のまま）
- **研究ロード時の再実行**: `ResearchDataStore.LoadResearchData`は`ExecuteUnlockActions`を再実行する。`unlockPlayerInventorySlotLevel`をwhitelistに入れているため再適用されるが、`UnlockLevel`が冪等なので二重適用は無害（セーブ値ロードと研究再実行のどちらが先でも同じ結果）
- **grabやサブインベントリは無変更**。ホットバーHUD（HotBarItem 9個）も固定のまま
