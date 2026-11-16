# 空気清浄機フィルターの自動搬入上限 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 空気清浄機のフィルタースロットへのブロック経由の自動搬入を、使用中を含めて3個で止める（手動はスタック上限まで）。

**Architecture:** `CleanRoomAirFilterInventoryComponent` の `IBlockInventory` 側の2メソッド（`InsertItem(stack, context)` と `IBlockInventory.InsertionCheck` の明示実装）にだけ上限を掛ける。`IOpenableInventory` 側（プレイヤー操作）は従来どおり。上限値は機械の自動搬入倍率 `MachineInputSlotBinding.AutoInsertCraftCount`（現在3）を参照する。機械側 `VanillaMachineBlockInventoryComponent` の経路分けと同じ形。

**Tech Stack:** C# / Unity / NUnit（CombinedTest）/ プレイテストDSL

## Requirements

1. ベルト・チェスト直結・フィルター分岐器など `IBlockInventory.InsertItem(stack, context)` 経由の搬入は、スロットのフィルター数（使用中の1個を含む表示数）が3個になったら受け入れを止め、入りきらない分を残りとして返す。受け入れ基準: 空のスロットへ5個を context 付きで入れると残り2・スロット3。スロット3のとき1個入れると残り1
2. ベルトの振り分け判定 `IBlockInventory.InsertionCheck` は同じ上限で判定する。受け入れ基準: 空なら3個で true・4個で false、スロット2なら1個で true・2個で false
3. プレイヤー操作（`InsertItem(IItemStack)`・`InsertItem(ItemId,int)`・`InsertItem(List)`・`IOpenableInventory.InsertionCheck`・`SetItem`・`ReplaceItem`）は上限を掛けずスタック上限まで。受け入れ基準: 空のスロットへ context 無しで10個入れるとスロット10
4. フィルター以外を拒む既存挙動は変えない（既存 `CleanRoomAirFilterInsertionTest` が通り続ける）
5. 上限値は数値リテラルを置かず `MachineInputSlotBinding.AutoInsertCraftCount` を参照する
6. 実機（PlayMode）で、チェスト→ベルト→清浄機にフィルター5個を流すとスロットが3で止まり、残り2個が搬入元側に残る

やらないこと: 液体・他ブロックの上限変更／UIへの上限表示／マスタ・スキーマ変更／セーブ形式の変更（スロットの中身の形は変わらない。上限超えのセーブはそのままロードされ、以後の自動搬入が止まるだけ）

## Global Constraints

- AGENTS.md の規約（200行以下・日英2行コメント・try-catch禁止・デフォルト引数禁止・`Func<>`禁止・partial禁止）
- fail-closed で拒否する経路には `silent-ok: <理由> / <誰がどこで気づくか>` を同じ行か直前に書く（既存のフィルター以外拒否と同じ形）
- `.cs` を変えたら必ずコンパイルする。Mac mini で同時に動かせる Editor は1本まで。`moores-wt status` で他の Editor が動いていれば、push 済みブランチを `~/bin/winunity`（compile/test/play）で Windows 機に回す
- worktree: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/purifier-filter-auto-insert-cap`（ブランチ `feat/purifier-filter-auto-insert-cap`）。Editor 未起動（`--no-editor` で作成）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正1件 / 機械側の定数コメントから清浄機の語を除いた（注目点: 清浄機→MachineInputSlotBinding の定数依存が1本増える）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ0 / 弱: IsFilterOrEmpty と残数算出が InsertItem(ctx) と InsertionCheck の2か所に出る

---

### Task 1: 清浄機フィルターの自動搬入上限

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/MachineInputSlotBinding.cs`（`AutoInsertCraftCount` を `private const` → `public const`）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterInventoryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomAirFilterAutoInsertCapTest.cs`

**Interfaces:**
- Consumes: `MachineInputSlotBinding.AutoInsertCraftCount`（`public const int`、値3）
- Produces: なし（既存インターフェースの実装の挙動だけが変わる）

- [ ] **Step 1: 定数を公開する**

`MachineInputSlotBinding.cs` の定数を次に置き換える（クラスは `internal` のままでよい。清浄機は同じ Game.Block アセンブリ）:

```csharp
        // 自動搬入はレシピ3回分で止める。他ブロックの自動搬入上限もこの倍率を参照する
        // Automated inflow stops at three crafts' worth; other blocks' inflow caps reference this multiplier
        public const int AutoInsertCraftCount = 3;
```

- [ ] **Step 2: 清浄機のブロック経路に上限を掛ける**

`CleanRoomAirFilterInventoryComponent.cs` に `using Game.Block.Blocks.Machine.Inventory.Input;` と `using System;` を足し、`InsertItem(IItemStack itemStack, InsertItemContext context)` を置き換え、`IBlockInventory.InsertionCheck` の明示実装と上限算出を足す:

```csharp
        // 自動搬入は使用中を含めスロット表示で3個まで（ADR 0101）
        // Automated inflow stops at three in the slot, the one in use included (ADR 0101)
        private const int AutoInsertFilterLimit = MachineInputSlotBinding.AutoInsertCraftCount;

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            CheckDestroy(this);

            // silent-ok: フィルター以外は受け取らず全量を差し戻す / ベルトの出口に品が留まって詰まるのをプレイヤーが見て気づく
            // silent-ok: non-filters are handed back whole / the player sees the item parked and clogging at the belt exit
            if (!IsFilterOrEmpty(itemStack) || itemStack.Count == 0) return itemStack;

            // 上限までの分だけスロットへ渡し、残りは搬入元へ返す
            // Hand the slot only what fits under the cap and return the rest to the source
            var acceptable = Math.Min(itemStack.Count, AutoInsertAcceptableCount());
            // silent-ok: 3個に達したら搬入を止める / 品がベルト出口・チェストに留まり、プレイヤーがスロットの3個と並べて見て気づく
            // silent-ok: inflow stops at three / the item parks at the belt exit or chest and the player sees it next to the slot's three
            if (acceptable <= 0) return itemStack;

            // 前例（VanillaMachineInputSimulationUtil）と同じく SubItem で分け、品のメタデータを保つ
            // Split with SubItem like the precedent (VanillaMachineInputSimulationUtil) to keep item metadata
            var portion = acceptable == itemStack.Count ? itemStack : itemStack.SubItem(itemStack.Count - acceptable);
            var slotRemainder = _filterSlot.InsertItem(portion);
            return itemStack.SubItem(acceptable - slotRemainder.Count);
        }

        // ベルトの振り分け判定は自動搬入と同じ上限を参照する
        // Belt routing checks the same cap as automated insertion
        bool IBlockInventory.InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            var total = 0;
            foreach (var itemStack in itemStacks)
            {
                if (!IsFilterOrEmpty(itemStack)) return false;
                total += itemStack.Count;
            }

            return total <= AutoInsertAcceptableCount() && _filterSlot.InsertionCheck(itemStacks);
        }
```

クラス末尾の `IsFilterOrEmpty` の下に足す:

```csharp
        // 上限までに残っている受け入れ数。空スロットの Count は0
        // How many more filters fit under the cap; an empty slot counts 0
        private int AutoInsertAcceptableCount()
        {
            return AutoInsertFilterLimit - _filterSlot.GetItem(0).Count;
        }
```

既存の `public bool InsertionCheck(List<IItemStack>)` はそのまま残す（C# では明示実装がある `IBlockInventory` 経由の呼び出しだけが新しい方へ行き、`IOpenableInventory` 経由は既存の上限無しのまま）。ファイルが200行を超えないことを `wc -l` で確かめる。

- [ ] **Step 3: テストを書く**

`CleanRoomAirFilterAutoInsertCapTest.cs`:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomAirFilterAutoInsertCapTest
    {
        [Test]
        public void BlockInsertStopsAtThreeTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;

            // 空のスロットへ5個入れると3個だけ入り2個返る
            // Inserting five into an empty slot takes three and returns two
            var remainder = inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 5), InsertItemContext.Empty);
            Assert.AreEqual(2, remainder.Count);
            Assert.AreEqual(ForUnitTestItemId.TestCleanRoomFilter, remainder.Id);
            Assert.AreEqual(3, inventory.GetItem(0).Count);

            // 3個に達した後は全量返る
            // Once at three, everything comes back
            Assert.AreEqual(1, inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 1), InsertItemContext.Empty).Count);
            Assert.AreEqual(3, inventory.GetItem(0).Count);
        }

        [Test]
        public void BlockInsertionCheckUsesSameCapTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;
            var blockInventory = (IBlockInventory)inventory;

            // 空なら3個まで可、4個は不可
            // Empty: three fit, four do not
            Assert.IsTrue(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 3) }));
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 4) }));

            // 2個入っていれば残り1個分だけ
            // With two loaded, only one more fits
            inventory.SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 2);
            Assert.IsTrue(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 1) }));
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 2) }));
        }

        [Test]
        public void ManualInsertIgnoresCapTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;

            // プレイヤー操作の経路は3個を超えて入る
            // The player path goes past three
            Assert.AreEqual(0, inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 10)).Count);
            Assert.AreEqual(0, inventory.InsertItem(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 2) }).Count);
            Assert.AreEqual(12, inventory.GetItem(0).Count);
            Assert.IsTrue(((global::Core.Inventory.IOpenableInventory)inventory).InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 5) }));

            // 手で3個を超えて入れた後は、ブロックからの搬入を全量返す
            // After a manual overfill, block inflow comes back whole
            Assert.AreEqual(1, inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 1), InsertItemContext.Empty).Count);
        }

        [Test]
        public void BeltStopsAtThreeTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 北向きにチェスト→ベルト→清浄機。5個流す
            // Chest, belt and purifier northbound; send five
            var source = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1)).GetComponent<VanillaChestComponent>();
            CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 1));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestCleanRoomFilter, 5));

            // 1マスのベルトは60tick周期で1個渡すので、600tickあれば5個分は流れ切る
            // A one-cell belt hands one per 60 ticks, so 600 ticks covers five
            GameUpdater.RunFrames(600);
            Assert.AreEqual(3, filter.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);

            // 残り2個はベルト上とチェストに留まる
            // The other two stay on the belt and in the chest
            var onBelt = BeltWorldTestUtil.ItemsOnSegmentAt(Vector3Int.zero).Length;
            Assert.AreEqual(2, onBelt + source.GetItem(0).Count);
        }
    }
}
```

`IOpenableInventory` の名前空間は `Core.Inventory`。テストの名前空間 `Tests.CombinedTest.Core...` と衝突するので `global::` を付けている。テストのフィルターのスタック上限が12未満で `ManualInsertIgnoresCapTest` が落ちた場合は、items.json の `stackLevelTableGuid` が指す表の上限を確かめ、その上限以下で3を超える数（例: 上限が10なら8と2）に置き換える。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomAirFilter"`
Expected: ErrorCount 0 / 新規4件と既存 `CleanRoomAirFilterInsertionTest`・`CleanRoomAirFilterTest` が全 PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/MachineInputSlotBinding.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterInventoryComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomAirFilterAutoInsertCapTest.cs docs/adr/0101-purifier-filter-auto-insert-capped-at-three.md ".decisions/2026-11-16-空気清浄機のフィルターの自動搬入は3個で止める.md" docs/superpowers/plans/2026-11-16-purifier-filter-auto-insert-cap.md
git commit -m "空気清浄機のフィルターのブロック経由の自動搬入を3個で止める"
```
（テストの .meta は Unity が生成したものをコミットする。手で作らない）

### Task 2: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-filter-auto-insert-cap.cs`

**Interfaces:**
- Consumes: Task 1 の挙動

- [ ] **Step 1: シナリオを書く**

```csharp
// シナリオ: チェスト→ベルト→空気清浄機にフィルター5個を流し、スロットが3で止まり残り2個が搬入元側に残ることを確かめる
// Scenario: feed five filters chest -> belt -> purifier and check the slot stops at three with two left upstream
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using UnityEngine;
return PlaytestRunner.Run("cleanroom-filter-auto-insert-cap", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");
    await p.SetupFlatGround();
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);

    // 北向きにチェスト→ベルト→清浄機
    // Chest, belt and purifier northbound
    var chestBlock = await p.PlaceBlockDirect("チェスト", new Vector3Int(3, 33, 2), BlockDirection.North);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(3, 33, 3), BlockDirection.North);
    var filterBlock = await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(3, 33, 4), BlockDirection.North);
    var filterItemId = PlaytestItemOps.ResolveItemId("クリーンルームフィルター");
    IOpenableInventory chestInventory = chestBlock.GetComponent<IOpenableBlockInventoryComponent>();
    IOpenableInventory filterInventory = filterBlock.GetComponent<IOpenableBlockInventoryComponent>();
    await p.RunOnServer("チェストにフィルター5個を入れる", () => chestInventory.SetItem(0, filterItemId, 5));

    // スロットが3になり、その後も増えない
    // The slot reaches three and stays there
    await p.Until(() => filterInventory.GetItem(0).Count == 3, 20f, "清浄機のスロットが3個になった");
    await p.WaitSeconds(8f);
    p.Assert(filterInventory.GetItem(0).Count == 3, $"8秒後もスロットは3個のまま(実際={filterInventory.GetItem(0).Count})");
    p.Assert(chestInventory.InventoryItems.Sum(i => i.Id == filterItemId ? i.Count : 0) <= 2, "チェストに残るのは2個以下（残りはベルト上で待つ）");
    await p.Screenshot("01-filter-capped-at-three");
});
```

DSL の API 名・ブロック名・アイテム名は実在を確認済み（user-simulator review）。

- [ ] **Step 2: 新シナリオと既存の手動投入シナリオを実行する**

unity-playmode-recorded-playtest スキルの `references/run-scenario.md` に従い `scripts/run-scenario.sh` で次の2本を回す:
1. `scenarios/misc/cleanroom-filter-auto-insert-cap.cs`（自動搬入が3で止まる）
2. `scenarios/misc/cleanroom-room-usability.cs`（UI の手動投入で5個入る＝手動は上限無し、の回帰確認）

Expected: 両方 result.json が success。録画・スクショを確認し、スロット表示が3（1本目）・5（2本目）であることを目で見る。終わったら `list-work-saves.sh` で作ったワールドを消す。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-filter-auto-insert-cap.cs
git commit -m "清浄機のフィルター自動搬入上限の録画シナリオを追加"
```

### Task 3: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の反映が判定経路・条件式に触れたら、Task 2 の録画テストを反映後のバイナリで再実施する

### Task 4: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR本文の先頭行は報告ページ `https://review.moores.tech/pr/<PR番号>/report/` の h1（CLAUDE.local.md「PR の成果物の載せ先」）

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0101-purifier-filter-auto-insert-capped-at-three.md`／裁定: `.decisions/2026-11-16-空気清浄機のフィルターの自動搬入は3個で止める.md`
- 上限を掛ける経路は `IBlockInventory` の2メソッドだけ（`InsertItem(stack, context)`・`IBlockInventory.InsertionCheck` 明示実装）。出所: agent前提（`VanillaMachineBlockInventoryComponent.cs` の同じ経路分け）
- 定数は `MachineInputSlotBinding.AutoInsertCraftCount` を `public const` にして参照する。清浄機が機械の入力束縛クラスを参照する依存が1本増える。出所: agent前提（既存定数と同じ意味の閾値は既存を参照する規約）
- 上限超えの拒否は `silent-ok` で書く（機械側 `MachineInputBlockInflow.Insert` の同種拒否と同じ扱い。品が搬入元に留まり目で見える）。出所: agent前提
- ランタイム挙動（ベルト搬入）に触れるので unityプレイ録画テストを Task 2 に入れる。手動投入の回帰は既存シナリオ `cleanroom-room-usability.cs` で確認する
- 分割は既存スタックを `SubItem` で行い、`ItemStackFactory.Create` で作り直さない。出所: agent前提（user-simulator review 指摘・前例 `VanillaMachineInputSimulationUtil.cs`）
- Phase 2.6 弱「IsFilterOrEmpty と残数算出が2メソッドに出る」は、同じクラス内の private ヘルパーの呼び出しで判定本体は1か所なのでこのままとする。出所: agent前提（拒否権つき）
- 1タスク（Task 1）にコード・テストをまとめた。定数公開と上限実装は片方だけ承認する意味が無いため
