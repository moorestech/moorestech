# 機械入力の自動搬入上限（レシピ3回分） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ベルト等のブロックからの自動搬入では、機械の入力スロットごとに「レシピ要求数×3」で受け入れを止める（手動投入は従来どおりスタック上限まで）。

**Architecture:** 手動か自動かは実行時に判定せず、呼ばれた入口で分ける。`IBlockInventory` の入口（`InsertItem(stack, InsertItemContext)` と `InsertionCheck`）は自動搬入専用なので上限あり、`IOpenableInventory` の入口と `SetItem` はプレイヤー操作用なので上限なしにする。上限の値は入力スロットの束縛器 `MachineInputSlotBinding` が持ち、挿入シミュレーション `VanillaMachineInputSimulationUtil` の第2パスで適用する。

**Tech Stack:** Unity / C#（moorestech_server）、NUnit、uloop

## Requirements

- R1: `IBlockInventory.InsertItem(stack, InsertItemContext)` で機械へ入れると、各入力スロットは `要求数×3` で止まり、超過分は remainder で返る。受け入れ基準: 要求数3のスロットへ100個流すと9個だけ入り91個返る
- R2: `IBlockInventory.InsertionCheck` も R1 と同じ上限で判定する。受け入れ基準: 上限まで入ったスロットへの追加は false。ベルトの振り分け（`VanillaBeltConveyorBlockInventoryInserter.cs:124,146`）が満杯の機械を飛ばすのはこの判定による
- R3: 消費しない素材（`IsRemain == true`）の自動搬入上限は要求数ちょうど。受け入れ基準: 要求数1・isRemain のスロットへ流すと1個で止まる
- R4: スタック上限が `要求数×3` より小さければスタック上限で止まる（既存の `AddItem` が担う）
- R5: 手動投入は従来どおりスタック上限まで入る。受け入れ基準: プレイヤー一括移動 `InventoryItemInsertService.Insert`（`IOpenableInventory.InsertItem(ItemId,int)` 経由）と `IOpenableInventory.InsertionCheck`、スロット指定移動の `SetItem` は上限なし
- R6: レシピ変更時の返却（`MachineRecipeRefundUtil` の `SimulateInsert`/`InsertItem`）は従来どおりスタック上限まで
- R7: 既にスロットに上限超の量がある（旧セーブ等）場合、自動搬入は受け入れないだけで中身は減らさない
- R8: 入口ごとの挙動をテストで固定する（ベルト側の入口は×3、`InsertSlot` 経路と `SetItem` はスタック上限）。将来のブロック間搬入が `IOpenableInventory.InsertItem` を直接呼ぶと上限が掛からない（既知の弱点）ため、その検知をこのテストに担わせる
- やらないこと: 液体タンクの受け入れ量は変えない／倍率をマスタデータ化しない／クライアントUIの変更なし／セーブ形式の変更なし

## Global Constraints

- 倍率は全機械共通の定数 `3`（`MachineInputSlotBinding.AutoInsertCraftCount`）
- 1ファイル200行以下、1ディレクトリのコードは10ファイルまで（AGENTS.md）
- デフォルト引数禁止、`Func<>` 禁止、partial 禁止、try-catch 禁止
- コメントは日本語1行＋英語1行のセット
- .meta は手で作らない（ディレクトリ・ファイルの移動は `git mv` で .meta ごと動かし、新規ファイル・新規ディレクトリの .meta は Unity の自動生成に任せてコミットする）
- 裁定: `.decisions/2026-10-18-機械入力の自動搬入はレシピ要求数の3倍で止める.md`、`.decisions/2026-10-18-手動と自動搬入は呼ばれた入口で区別する.md`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 上限値は前例RequiredCountAtと同じ束縛器、明示的インターフェース実装は前例1件（RailGraphDatastore.cs:545）の新規寄りパターンとして注目点、死活表（ベルト搬入=×3・手動SwapSlot/InsertSlot=従来・返却=従来・液体=従来）で死ぬ操作なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ0 / 弱=InsertItemFromBlockとInsertionCheckFromBlockが同じSimulateAutoInsertを呼ぶ（既存InsertItem/InsertionCheckの対と同形）

---

## File Structure

- Move: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/{MachineInputSlotBinding,VanillaMachineInputInventory,VanillaMachineInputSimulationUtil}.cs` → `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/`（Inventory 直下が既に10ファイルで、液体挿入の切り出し先を置けないため。namespace は `Game.Block.Blocks.Machine.Inventory.Input`）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/MachineInputFluidInsertUtil.cs` — `VanillaMachineInputInventory.InsertFluid` の本体（217行→200行以下にするための切り出し）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/MachineInputSlotBinding.cs` — `AutoInsertCraftCount` と `AutoInsertLimitAt`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/VanillaMachineInputSimulationUtil.cs` — `SimulateAutoInsert` を追加し第2パスの上限を切り替える
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/VanillaMachineInputInventory.cs` — `InsertItemFromBlock` / `InsertionCheckFromBlock`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineBlockInventoryComponent.cs` — `IBlockInventory` の入口を上限ありへ
- Modify: 移動した3型を参照する全ファイルに `using Game.Block.Blocks.Machine.Inventory.Input;` を足す（`grep -rln "MachineInputSlotBinding\|VanillaMachineInputInventory\|VanillaMachineInputSimulationUtil" moorestech_server/Assets/Scripts` で列挙。現在21ファイル）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Machine/MachineAutoInsertCapTest.cs`（同ディレクトリは現在 .cs 8本＋`Recipe/`。これで9本）

### Task 1: 入力系を Input/ サブディレクトリへ移し、液体挿入を切り出す（挙動不変）

**Files:**
- Move: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/MachineInputSlotBinding.cs` → `.../Inventory/Input/MachineInputSlotBinding.cs`
- Move: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs` → `.../Inventory/Input/VanillaMachineInputInventory.cs`
- Move: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputSimulationUtil.cs` → `.../Inventory/Input/VanillaMachineInputSimulationUtil.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/MachineInputFluidInsertUtil.cs`

**Interfaces:**
- Produces: namespace `Game.Block.Blocks.Machine.Inventory.Input`。`internal static class MachineInputFluidInsertUtil { public static FluidStack InsertFluid(FluidContainer[] tanks, MachineInputSlotBinding slotBinding, FluidStack fluidStack, int designatedTankIndex, out bool changed) }`

- [ ] **Step 1: ファイルを .meta ごと移動する**

```bash
cd moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory
mkdir Input
for f in MachineInputSlotBinding VanillaMachineInputInventory VanillaMachineInputSimulationUtil; do git mv $f.cs Input/$f.cs; git mv $f.cs.meta Input/$f.cs.meta; done
```

3ファイルの `namespace Game.Block.Blocks.Machine.Inventory` を `namespace Game.Block.Blocks.Machine.Inventory.Input` に変え、`VanillaMachineInputInventory.cs` に `using Game.Block.Blocks.Machine.Inventory;`（`IVanillaMachineSubInventory`・`MachineSlotPlacementCheck` 用）を足す。参照側全ファイルへ `using Game.Block.Blocks.Machine.Inventory.Input;` を足す。

- [ ] **Step 2: `InsertFluid` の本体を `MachineInputFluidInsertUtil` へ移す**

`VanillaMachineInputInventory.InsertFluid` の中身（`IsBound` 判定・`InsertIntoDesignatedTank`・`FillBoundTanks` を含むローカル関数ごと）を次の形で移し、`_slotBinding` → `slotBinding`、`FluidInputSlot`/`_fluidContainers` → `tanks` に置換する。コメントは移動元のものをそのまま持っていく。

```csharp
using System;
using Game.Fluid;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    // 束縛タンクへの液体挿入（VanillaMachineInputInventoryから切り出し。ADR 0042 R5）
    // Fluid insertion into bound tanks (split out of VanillaMachineInputInventory; ADR 0042 R5)
    internal static class MachineInputFluidInsertUtil
    {
        public static FluidStack InsertFluid(FluidContainer[] tanks, MachineInputSlotBinding slotBinding, FluidStack fluidStack, int designatedTankIndex, out bool changed)
        {
            // （移動元 InsertFluid の本体をここへ。ローカル関数は #region Internal に置く）
        }
    }
}
```

`VanillaMachineInputInventory` 側は次の1行委譲にする（既存コメント2行は残す）:

```csharp
public FluidStack InsertFluid(FluidStack fluidStack, int designatedTankIndex, out bool changed)
{
    return MachineInputFluidInsertUtil.InsertFluid(_fluidContainers, _slotBinding, fluidStack, designatedTankIndex, out changed);
}
```

（上のコードブロック中の括弧書きは「移動元の本体をそのまま貼る」指示であり、新しく書くコードは無い。）

- [ ] **Step 3: コンパイルし既存の機械テストが通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.CombinedTest\.Core\.Machine\."`
Expected: ErrorCount 0 / 全PASS。`wc -l` で `VanillaMachineInputInventory.cs` が170行前後になっていること。Unity が生成した `Input.meta` と `MachineInputFluidInsertUtil.cs.meta` を確認する

- [ ] **Step 4: コミットする**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "refactor: 機械の入力系をInput/へ移し液体挿入をMachineInputFluidInsertUtilへ切り出す"
```

### Task 2: 自動搬入の上限（要求数×3）を実装しテストする

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/MachineInputSlotBinding.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/VanillaMachineInputSimulationUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/Input/VanillaMachineInputInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineBlockInventoryComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Machine/MachineAutoInsertCapTest.cs`

**Interfaces:**
- Consumes: Task 1 の namespace `Game.Block.Blocks.Machine.Inventory.Input`
- Produces: `public const int MachineInputSlotBinding.AutoInsertCraftCount = 3`、`public int MachineInputSlotBinding.AutoInsertLimitAt(int slotIndex)`、`VanillaMachineInputSimulationUtil.SimulateAutoInsert(IReadOnlyList<IItemStack> currentSlots, IReadOnlyList<IItemStack> itemStacks, MachineInputSlotBinding slotBinding)`（戻り値は `SimulateInsert` と同じタプル）、`public IItemStack VanillaMachineInputInventory.InsertItemFromBlock(IItemStack itemStack)`、`public bool VanillaMachineInputInventory.InsertionCheckFromBlock(List<IItemStack> itemStacks)`

- [ ] **Step 1: `MachineInputSlotBinding` に上限を足す**（`RequiredCountAt` の直後）

```csharp
// 自動搬入はレシピ3回分で止める（マニホールド配置で高コスト素材を1機ごとに溜め込ませない）
// Automated inflow stops at three crafts' worth so manifolds do not hoard costly inputs per machine
public const int AutoInsertCraftCount = 3;

// スロットiへ自動搬入で満たしてよい数。消費しない素材は要求数ちょうど、束縛外は0
// How far automated inflow may fill slot i; non-consumed inputs stop at the requirement, unbound slots return 0
public int AutoInsertLimitAt(int slotIndex)
{
    if (_recipe == null || slotIndex < 0 || _recipe.InputItems.Length <= slotIndex) return 0;
    var input = _recipe.InputItems[slotIndex];
    var isRemain = input.IsRemain.HasValue && input.IsRemain.Value;
    return isRemain ? input.Count : input.Count * AutoInsertCraftCount;
}
```

- [ ] **Step 2: `VanillaMachineInputSimulationUtil` に `SimulateAutoInsert` を足す**

既存の `SimulateInsert` 本体を `private static ... Simulate(currentSlots, itemStacks, slotBinding, bool capAtAutoInsertLimit)` へ移し、公開口を2本にする:

```csharp
public static (List<IItemStack> slots, List<int> touchedSlots, List<IItemStack> remainders) SimulateInsert(
    IReadOnlyList<IItemStack> currentSlots, IReadOnlyList<IItemStack> itemStacks, MachineInputSlotBinding slotBinding)
{
    return Simulate(currentSlots, itemStacks, slotBinding, false);
}

// ブロックからの自動搬入用。第二パスをスタック上限ではなく自動搬入上限で止める
// For automated inflow from blocks; the second pass stops at the auto-insert cap instead of the stack limit
public static (List<IItemStack> slots, List<int> touchedSlots, List<IItemStack> remainders) SimulateAutoInsert(
    IReadOnlyList<IItemStack> currentSlots, IReadOnlyList<IItemStack> itemStacks, MachineInputSlotBinding slotBinding)
{
    return Simulate(currentSlots, itemStacks, slotBinding, true);
}
```

`Simulate` 内の `FillToStackLimit` を次に置き換える（呼び出し側も `FillToSecondPassLimit(remaining)` に）:

```csharp
// 第二パス: 自動搬入なら上限まで、それ以外はスタック上限まで詰める。既に上限超のスロットは減らさず飛ばす
// Second pass: up to the auto-insert cap for automated inflow, else to the stack limit; slots already over the cap are skipped, never reduced
IItemStack FillToSecondPassLimit(IItemStack remaining)
{
    foreach (var slot in slotBinding.ResolveBoundSlots(remaining.Id))
    {
        if (remaining.Count == 0) break;

        var portion = remaining;
        if (capAtAutoInsertLimit)
        {
            var room = slotBinding.AutoInsertLimitAt(slot) - slots[slot].Count;
            if (room <= 0) continue;
            if (room < remaining.Count) portion = ServerContext.ItemStackFactory.Create(remaining.Id, room);
        }

        var result = slots[slot].AddItem(portion);
        var added = portion.Count - result.RemainderItemStack.Count;
        if (added == 0) continue;

        slots[slot] = result.ProcessResultItemStack;
        remaining = remaining.SubItem(added);
        if (!touchedSlots.Contains(slot)) touchedSlots.Add(slot);
    }
    return remaining;
}
```

- [ ] **Step 3: `VanillaMachineInputInventory` に自動搬入の口を足す**（`InsertionCheck` の直後）

```csharp
// ブロックからの自動搬入。各スロットはAutoInsertLimitAtで止まる（手動投入はInsertItem側でスタック上限まで）
// Automated inflow from blocks; each slot stops at AutoInsertLimitAt (manual input goes through InsertItem up to the stack limit)
public IItemStack InsertItemFromBlock(IItemStack itemStack)
{
    var (slots, touchedSlots, remainders) = VanillaMachineInputSimulationUtil.SimulateAutoInsert(InputSlot, new List<IItemStack> { itemStack }, _slotBinding);
    foreach (var slot in touchedSlots) _itemDataStoreService.SetItem(slot, slots[slot]);
    return remainders[0];
}

public bool InsertionCheckFromBlock(List<IItemStack> itemStacks)
{
    foreach (var remainder in VanillaMachineInputSimulationUtil.SimulateAutoInsert(InputSlot, itemStacks, _slotBinding).remainders)
    {
        if (remainder.Count != 0) return false;
    }
    return true;
}
```

- [ ] **Step 4: `VanillaMachineBlockInventoryComponent` の `IBlockInventory` 入口を差し替える**

`InsertItem(IItemStack, InsertItemContext)` を次に置き換える:

```csharp
// ブロックからの搬入だけレシピ3回分で止める。手動投入はIOpenableInventoryの口からスタック上限まで入る
// Only inflow from blocks stops at three crafts' worth; manual input enters via the IOpenableInventory entry up to the stack limit
public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
{
    BlockException.CheckDestroy(this);
    return _vanillaMachineInputInventory.InsertItemFromBlock(itemStack);
}
```

`InsertionCheck` は `IBlockInventory` と `IOpenableInventory` で同シグネチャなので、既存の public 版（IOpenableInventory 用・上限なし）は残し、その直後に明示的実装を足す:

```csharp
// ベルトの振り分け判定はこちらを通るため、満杯（レシピ3回分）の機械は振り分け先から外れる
// Belt routing asks through this entry, so a machine full to three crafts drops out of the routing targets
bool IBlockInventory.InsertionCheck(List<IItemStack> itemStacks)
{
    BlockException.CheckDestroy(this);
    return _vanillaMachineInputInventory.InsertionCheckFromBlock(itemStacks);
}
```

200行以下に収めるため、同ファイルで次も行う（挙動不変）:
- `InsertItem(ItemId itemId, int count)` の本体を `return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));` に畳む
- `InsertItem(IItemStack)` と `InsertItem(List<IItemStack>)` にある「プロセス開始できるなら開始」コメント（実際には開始しない誤記）を削除する
- `CreateCopiedItems` の本体を `return new ReadOnlyCollection<IItemStack>(InventoryItems.ToList());` に畳む

- [ ] **Step 5: テストを書く** — `Tests/CombinedTest/Core/Machine/MachineAutoInsertCapTest.cs`

テスト用レシピ: `MachineRecipes.Data[0]`（素材0=要求3・素材1=要求1）と `machineRecipeGuid f79c7ee0-1ebe-4fcc-b1ec-5bcb296d9c1a`（素材0=要求2・素材1=要求1 isRemain）。

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine
{
    // ブロックからの自動搬入はレシピ3回分で止まり、手動投入はスタック上限まで入る
    // Automated inflow from blocks stops at three crafts' worth; manual input still fills to the stack limit
    public class MachineAutoInsertCapTest
    {
        private static readonly Guid BasicRecipeGuid = new("bd3d4d7d-9c3b-4ae1-875b-950327eedd9d");
        private static readonly Guid RemainRecipeGuid = new("f79c7ee0-1ebe-4fcc-b1ec-5bcb296d9c1a");

        [Test]
        public void BlockInsertStopsAtThreeCrafts()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var blockInventory = (IBlockInventory)inventory;
            var input0 = ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 100);

            var remainder = blockInventory.InsertItem(input0, InsertItemContext.Empty);

            // 要求3×3=9個で止まり、残りは返る
            // Stops at 3x3=9 and the rest comes back
            Assert.AreEqual(9, inventory.GetItem(0).Count);
            Assert.AreEqual(91, remainder.Count);
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 1) }));
        }

        [Test]
        public void BlockInsertStopsAtRequirementForRemainInput()
        {
            var (inventory, recipe) = Setup(RemainRecipeGuid);
            var blockInventory = (IBlockInventory)inventory;

            blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 100), InsertItemContext.Empty);
            blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(recipe.InputItems[1].ItemGuid, 100), InsertItemContext.Empty);

            // 消費素材は2×3=6個、消費しない素材は要求数1個で止まる
            // The consumed input stops at 2x3=6, the non-consumed one at its requirement of 1
            Assert.AreEqual(6, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(1).Count);
        }

        [Test]
        public void ManualInsertFillsToStackLimit()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var input0 = ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 100);
            var stackLimit = ItemStackLevelDataStore.Instance.GetMaxStack(input0.Id);

            // プレイヤー一括移動（InsertSlot）と同じ経路で入れる
            // Insert through the same path as the player's bulk move (InsertSlot)
            var player = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 1);
            player.SetItem(0, input0);
            Assert.IsTrue(inventory.InsertionCheck(new List<IItemStack> { ServerContext.ItemStackFactory.Create(input0.Id, 10) }));
            InventoryItemInsertService.Insert(player, 0, inventory, 100);

            Assert.AreEqual(Math.Min(100, stackLimit), inventory.GetItem(0).Count);
        }

        [Test]
        public void BlockInsertDoesNotReduceSlotAlreadyOverCap()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var input0Id = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(input0Id, 50));

            var remainder = ((IBlockInventory)inventory).InsertItem(ServerContext.ItemStackFactory.Create(input0Id, 5), InsertItemContext.Empty);

            // 旧セーブ等で上限超のスロットは減らさず、追加も受けない
            // A slot already over the cap (e.g. an old save) is neither reduced nor topped up
            Assert.AreEqual(50, inventory.GetItem(0).Count);
            Assert.AreEqual(5, remainder.Count);
        }

        private static (VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe) Setup(Guid machineRecipeGuid)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(machineRecipeGuid);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            return (block.GetComponent<VanillaMachineBlockInventoryComponent>(), recipe);
        }
    }
}
```

`InsertItemContext.Empty` は `Game.Block.Interface/Component/InsertItemContext.cs:36` の既存値、スタック上限は `Core.Item/ItemStackLevelDataStore.cs:31` の `GetMaxStack`（`using Core.Item;` が要る）。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Core.Machine.MachineAutoInsertCapTest"`
Expected: ErrorCount 0 / 4件PASS

- [ ] **Step 7: 回帰を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.(CombinedTest|UnitTest)\."`（`--timeout-seconds 1500`）
Expected: 失敗が master と同じ集合（新規失敗0）。ベルト経由で機械へ「要求数×3」を超えて入る前提の既存テストが落ちた場合は、その期待値を新仕様（R1）へ直し、どのテストをなぜ直したかをコミットメッセージに書く。master で既に落ちているテスト（bd の P0 `moorestech-szeu` 等）は対象外として報告に列挙する

- [ ] **Step 8: コミットする**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "feat: ブロックからの自動搬入で機械入力をレシピ3回分で止める"
```

### Task 3: moores-code-review で全ブランチレビューを実行する

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。Codex ホストでは同スキルの `references/codex-host.md` の runner で実行する

### Task 4: PR を作成しセッション終了可能状態にする

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PR本文に Discord 要望リンク https://discord.com/channels/888315641709330433/1560698281200324702 を入れる

## 判断記録（ADR）

- 機能裁定: `.decisions/2026-10-18-機械入力の自動搬入はレシピ要求数の3倍で止める.md`（出所: ユーザー裁定 2026-10-18）
- 手動/自動の区別: `.decisions/2026-10-18-手動と自動搬入は呼ばれた入口で区別する.md`（出所: ユーザー裁定 2026-10-18「それで問題なさそう？」への回答として agent が全呼び出し元を洗った結果を提示し、ユーザーが「ok」）
- 上限値を `MachineInputSlotBinding` に置く: 出所: agent前提（同クラスが既に第1パスの上限 `RequiredCountAt` を持つ前例。束縛器はレシピを持つ唯一の場所）
- `SimulateInsert` に bool 引数を足さず `SimulateAutoInsert` を別口で公開: 出所: agent前提（返却経路と搬入経路が呼ぶ口を名前で分け、呼び出し側でフラグを選ばせない）
- Input/ サブディレクトリへの移動と液体挿入の切り出し: 出所: agent前提（`VanillaMachineInputInventory.cs` が既に217行で200行規約超過、Inventory 直下が10ファイルで新規ファイルを置けない）
- unityプレイ録画テストは含めない: 出所: agent前提（サーバー内の挿入数の変更で、表示・入力・UI に触れない。ベルト振り分けは `InsertionCheck` の単体判定で担保され、CombinedTest で入口ごとに検証できる）
- grill §1.7 の Codex 独立質問: generate 4問、うち2問（倍率・液体）は最初の裁定で既答、残り2問（搬入経路・消費しない素材）をユーザーに聞いて裁定済み。filter は手作業で消し込んだ（実行ログ: 本セッション）
- Phase 2.6 弱1件（`InsertItemFromBlock` と `InsertionCheckFromBlock` がそれぞれ `SimulateAutoInsert` を呼ぶ）: このままにする。出所: agent前提（拒否権つき。既存の `InsertItem`/`InsertionCheck` の対と同形で、計算本体は1本の関数に集約済み）
- user-simulator review: 適用推奨0・要裁定0。Warning のうち件数誤記の修正と既知の弱点の Requirements（R8）への転記を適用した
