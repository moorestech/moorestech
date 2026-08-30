using System;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Item;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using Mooresmaster.Model.ItemsModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     機械のモジュールスロット（第3レンジ）の挙動を検証するテスト
    ///     Tests verifying the behavior of machine module slots (the third slot range)
    /// </summary>
    public class MachineModuleSlotTest
    {
        // テスト用機械のスロット構成（blocks.jsonのTestElectricMachine / TestGearMachineに対応）
        // Slot layout of the test machines (matches TestElectricMachine / TestGearMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;
        private const int ModuleSlotCount = 4;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        // アイドル要求電力の期待値をproduction式から独立させるためのマスタ実値
        // Master value keeping the idle request-power expectation independent from the production formula
        private const float MachineIdlePowerRate = 0.25f;

        [Test]
        // モジュールスロットがインプット・アウトプットの後ろの第3レンジとして存在することを確認する
        // Verify module slots exist as the third range after input and output
        public void ThirdRangeExistsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 機械を設置してスロット数を確認
            // Place the machine and check the slot size
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, inventory.GetSlotSize());

            // モジュールアイテムを先頭のモジュールスロットにセットして取得できることを確認
            // Set a module item into the first module slot and verify it can be retrieved
            var moduleItem = CreateModuleItem(1);
            inventory.SetItem(ModuleRangeStart, moduleItem);
            Assert.AreEqual(moduleItem, inventory.GetItem(ModuleRangeStart));
            Assert.AreEqual(moduleItem, inventory.InventoryItems[ModuleRangeStart]);

            // 末尾のモジュールスロット（統合スロットの最終番号）にもアクセスできることを確認
            // Verify the last module slot (final unified slot index) is also accessible
            var lastModuleSlot = ModuleRangeStart + ModuleSlotCount - 1;
            var lastModuleItem = CreateModuleItem(3);
            inventory.SetItem(lastModuleSlot, lastModuleItem);
            Assert.AreEqual(lastModuleItem, inventory.GetItem(lastModuleSlot));
            Assert.AreEqual(lastModuleItem, inventory.InventoryItems[lastModuleSlot]);

            // 出力スロットは生産物数(1)だけが束縛され、束縛先スロットへのセットがモジュールレンジへ流れないことを確認する(ADR 0042)
            // Only as many output slots as recipe outputs (1) are bound; a set into the bound slot must not route into the module range (ADR 0042)
            var recipe = GetMachineRecipe();
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var boundOutputSlot = InputSlotCount;
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            var outputItem = ServerContext.ItemStackFactory.Create(outputItemId, 7);
            inventory.SetItem(boundOutputSlot, outputItem);
            Assert.AreEqual(outputItem, inventory.GetItem(boundOutputSlot));

            // リフレクションでアウトプットサブインベントリの実体に入っていることを確認
            // Verify via reflection that the item landed in the actual output sub-inventory
            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
            Assert.AreEqual(outputItem, outputInventory.OutputSlot[0]);

            // 束縛外の出力スロット（生産物数を超える枠）へのセットは拒否され、空のまま残る
            // A set into an unbound output slot (beyond the recipe's output count) is refused and stays empty
            var unboundOutputSlot = InputSlotCount + OutputSlotCount - 1;
            inventory.SetItem(unboundOutputSlot, ServerContext.ItemStackFactory.Create(outputItemId, 7));
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(unboundOutputSlot).Id);

            // 設定した2つのモジュールスロット以外のモジュールレンジは空のまま
            // The module range except the two configured slots stays empty
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(ModuleRangeStart + 1).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(ModuleRangeStart + 2).Id);
        }

        [Test]
        // 搬送由来のInsertItemはインプットレンジのみに入り、モジュールスロットには入らないことを確認する
        // Verify transport-driven InsertItem only fills the input range and never module slots
        public void TransportInsertDoesNotFillModuleSlotsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = GetMachineRecipe();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            // 束縛はレシピ選択前提のため、まず選択する(ADR 0042)。以降の搬送挿入は素材0だけが対象
            // Binding requires a selected recipe (ADR 0042); the transport inserts below target only input 0
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var input0Id = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);

            // 束縛済み素材を搬送経由で挿入してもインプットスロットに入ることを確認
            // A bound material inserted via transport lands in an input slot
            var inputStack = itemStackFactory.Create(input0Id, 1);
            var remainder = inventory.InsertItem(inputStack);
            Assert.AreEqual(0, remainder.Count);
            Assert.AreEqual(inputStack, inventory.GetItem(0));
            AssertModuleRangeIsEmpty(inventory);

            // 束縛先スロットを満杯にしてさらに挿入しても、モジュールスロットへ溢れないことを確認
            // Fill the bound slot completely; further inserts must not overflow into module slots
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(input0Id);
            inventory.SetItem(0, itemStackFactory.Create(input0Id, maxStack));

            var overflowRemainder = inventory.InsertItem(itemStackFactory.Create(input0Id, 5));
            Assert.AreEqual(5, overflowRemainder.Count);
            AssertModuleRangeIsEmpty(inventory);
        }

        [Test]
        // モジュールスロットがセーブ・ロードで維持され、moduleSlotキーの無い過去セーブも読めることを確認する
        // Verify module slots survive save/load and that old saves without the moduleSlot key still load
        public void ModuleSaveLoadRoundTripTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // モジュールスロットにアイテムを入れてセーブする
            // Put an item into a module slot and save
            var moduleItem = CreateModuleItem(2);
            inventory.SetItem(ModuleRangeStart, moduleItem);
            var saveState = block.GetSaveState();

            // ロード後もモジュールスロットの内容が維持されていることを確認
            // Verify the module slot content is retained after loading
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(100), saveState, block.BlockPositionInfo);
            var loadedInventory = loadedBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(moduleItem, loadedInventory.GetItem(ModuleRangeStart));

            // moduleSlotキーを取り除いた「過去セーブ」をロードしてもモジュールスロットが空で読めることを確認
            // Loading an "old save" with the moduleSlot key removed must yield empty module slots without errors
            var saveKey = typeof(VanillaMachineSaveComponent).FullName;
            var machineJson = JObject.Parse(saveState[saveKey]);
            machineJson.Remove("moduleSlot");
            saveState[saveKey] = machineJson.ToString();

            var oldSaveBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(101), saveState, block.BlockPositionInfo);
            var oldSaveInventory = oldSaveBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            AssertModuleRangeIsEmpty(oldSaveInventory);
        }

        [Test]
        // 旧仕様は入出力レンジをソートしモジュールだけ除外したが、ADR 0042で全スロットがレシピ束縛されソート自体が完全なno-opになった
        // The old spec sorted the input/output range and excluded only modules; ADR 0042 binds every slot to the recipe, making sorting a full no-op
        // インベントリ整理が機械スロットを一切動かさないことと、ギア機械にもモジュールスロットがあることを確認する
        // Verify inventory sorting never moves a machine slot and that gear machines also have module slots
        public void SortIsNoOpForBoundMachineSlotsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var recipe = GetMachineRecipe();

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);

            // 素材はスロット1にだけ置きスロット0は空のままにする。両方埋めると「ソートで空きへ寄せる」
            // 旧挙動と「全スロット束縛でno-op」の新挙動が同じ結果になり回帰を検知できない(C12・mutation testing実測)
            // Materials are placed only in slot 1, leaving slot 0 empty. Filling both would make the old
            // "sort compacts into the gap" behavior and the new "every slot is bound, no-op" behavior
            // indistinguishable, hiding the regression (C12, per mutation testing)
            var input1 = itemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[1].ItemGuid), 1);
            inventory.SetItem(1, input1);
            var moduleItem = CreateModuleItem(1);
            inventory.SetItem(ModuleRangeStart, moduleItem);

            InventorySortService.Sort(inventory, inventory.SortExcludedSlots);

            // 全スロットが束縛済みのため、ソートしてもスロット1の素材はスロット0へ寄らず、スロット0は空のまま
            // Every slot is bound, so sorting never pulls slot 1's material into slot 0, which stays empty
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(0).Id);
            Assert.AreEqual(input1, inventory.GetItem(1));
            Assert.AreEqual(moduleItem, inventory.GetItem(ModuleRangeStart));
            for (var i = ModuleRangeStart + 1; i < ModuleRangeStart + ModuleSlotCount; i++)
            {
                Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(i).Id);
            }

            // ギア機械にもモジュールスロットの第3レンジが存在することを確認
            // Verify the gear machine also has the third module slot range
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearMachine, new Vector3Int(5, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);
            var gearInventory = gearBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, gearInventory.GetSlotSize());
        }

        [Test]
        // 速度モジュール装着機が未装着機より短いtick数で加工を終えることを確認する
        // Verify a machine with a speed module finishes processing in fewer ticks than one without
        public void SpeedModuleShortensProcessingTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);

            // 速度モジュール装着機と未装着機を並べて設置する
            // Place a speed-boosted machine and a plain machine side by side
            var (boostedBlock, boostedInventory, boostedProcessor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var (plainBlock, plainInventory, plainProcessor) = PlaceMachine(new Vector3Int(5, 1, 1));
            boostedInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed, 1));
            InsertRecipeInputs(boostedBlock, boostedInventory, recipe);
            InsertRecipeInputs(plainBlock, plainInventory, recipe);

            // 開始tickでは進行しないため、開始直後の残りtickが短縮済み加工時間と一致することを確認
            // No progress occurs on the start tick, so the remaining ticks right after start equal the scaled processing time
            var speedModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            var expectedBoostedTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * (1f / (1f + speedModule.EffectValue))));
            AdvanceTicksWithFullPower(1, boostedProcessor, plainProcessor);
            Assert.AreEqual(expectedBoostedTicks, boostedProcessor.GetRemainingTicks());
            Assert.AreEqual(baseTicks, plainProcessor.GetRemainingTicks());

            // 短縮時間とベース時間の中間点まで進める（装着機の短縮時間は超え、ベース時間には届かない）
            // Advance to the midpoint of the boosted and base durations (past boosted, short of base)
            var advanceTicks = (int)((expectedBoostedTicks + baseTicks) / 2);
            AdvanceTicksWithFullPower(advanceTicks, boostedProcessor, plainProcessor);

            // 装着機は完了してIdle、未装着機はまだProcessingであることを確認
            // The boosted machine has finished (Idle) while the plain machine is still Processing
            Assert.AreEqual(ProcessState.Idle, boostedProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Processing, plainProcessor.CurrentState);

            // 装着機のアウトプットにレシピ通りの成果物が入っていることを確認
            // The boosted machine's output contains the recipe result
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.AreEqual(recipe.OutputItems[0].Count, CountOutputItem(boostedInventory, outputItemId));
        }

        [Test]
        // 省エネモジュール装着機がプロセス中のみ要求電力を下げることを確認する
        // Verify an efficiency module lowers the requested power only while processing
        public void EfficiencyModuleLowersRequestPowerTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Efficiency, 1));

            // Idle中はモジュールではなくidlePowerRate分だけ要求電力が下がる
            // While idle, idlePowerRate reduces demand instead of the module
            var electric = block.GetComponent<VanillaElectricMachineComponent>();
            Assert.AreEqual(processor.RequestPower * MachineIdlePowerRate, electric.RequestEnergy.AsPrimitive(), 0.0001f);

            // プロセス開始後は省エネ倍率分だけ要求電力が下がる
            // After processing starts, the requested power drops by the efficiency multiplier
            InsertRecipeInputs(block, inventory, recipe);
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            var efficiencyModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Efficiency);
            var expectedPower = processor.RequestPower / (1f + efficiencyModule.EffectValue);
            Assert.Less(electric.RequestEnergy.AsPrimitive(), processor.RequestPower);
            Assert.AreEqual(expectedPower, electric.RequestEnergy.AsPrimitive(), 0.01f);
        }

        [Test]
        // 生産性モジュール（確率1.0）で完了時に追加出力が1セット入ることを確認する
        // Verify a productivity module (chance 1.0) yields one extra output set on completion
        public void ProductivityExtraOutputTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Productivity, 1));
            InsertRecipeInputs(block, inventory, recipe);

            // 前提: 追加出力が確定（確率1.0）になるeffectValueであること。データ変更時はここで失敗させる
            // Precondition: effectValue must guarantee the extra output (chance 1.0); fail loudly on data drift
            var productivityModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Productivity);
            Assert.GreaterOrEqual(productivityModule.EffectValue, 1f);

            // 生産性トレードオフで時間が延びるため、余裕を持って完了まで進める
            // The productivity tradeoff stretches the time, so advance well past completion
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var scaledTicks = (int)Math.Round(baseTicks * (1f + productivityModule.TradeoffValue));
            AdvanceTicksWithFullPower(1 + scaledTicks + 3, processor);

            // 完了済みで、アウトプット合計がレシピ出力数の2倍になっていることを確認
            // Processing has finished and the total output equals double the recipe output count
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.AreEqual(recipe.OutputItems[0].Count * 2, CountOutputItem(inventory, outputItemId));
        }

        [Test]
        // 生産性モジュール装着機は追加出力分の空きが無いとプロセスを開始しないことを確認する
        // Verify a productivity-equipped machine does not start unless the extra output set also fits
        public void ProductivityReservesOutputCapacityTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = GetMachineRecipe();
            var (modBlock, modInventory, modProcessor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var (ctrlBlock, ctrlInventory, ctrlProcessor) = PlaceMachine(new Vector3Int(5, 1, 1));
            // 束縛(ADR 0042)は出力スロットを生産物数(1)に絞るため、先に選択してから配置する
            // Binding (ADR 0042) limits output slots to the recipe's output count (1), so select the recipe before placing anything
            MachineRecipeSelectTestUtil.SelectRecipe(modBlock, recipe);
            MachineRecipeSelectTestUtil.SelectRecipe(ctrlBlock, recipe);
            modInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Productivity, 1));

            // 唯一の出力スロットに「ベース1セット分は入るが2セット分(追加込み)は入らない」量まで埋める
            // Fill the sole output slot so one base set fits but two sets (base + extra) do not
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(outputItemId);
            var prefillCount = maxStack - recipe.OutputItems[0].Count;
            foreach (var inventory in new[] { modInventory, ctrlInventory })
            {
                inventory.SetItem(InputSlotCount, itemStackFactory.Create(outputItemId, prefillCount));
            }
            InsertRecipeInputs(modBlock, modInventory, recipe);
            InsertRecipeInputs(ctrlBlock, ctrlInventory, recipe);

            AdvanceTicksWithFullPower(2, modProcessor, ctrlProcessor);

            // 装着機は開始せずインプットが残り、未装着機はベース分の空きがあるため開始する
            // The equipped machine stays idle with inputs intact while the plain machine starts
            Assert.AreEqual(ProcessState.Idle, modProcessor.CurrentState);
            Assert.AreNotEqual(ItemMaster.EmptyItemId, modInventory.GetItem(0).Id);
            Assert.AreEqual(ProcessState.Processing, ctrlProcessor.CurrentState);
        }

        [Test]
        // プロセス途中でセーブ・ロードしても加工状態と残りtickが維持されることを確認する
        // Verify the processing state and remaining ticks survive a mid-process save and load
        public void MidProcessSaveLoadKeepsRemainingTicksTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed, 1));
            InsertRecipeInputs(block, inventory, recipe);

            // 開始＋数tick進めたプロセス途中の状態を作ってセーブする
            // Start the process, advance a few ticks mid-process, then save
            AdvanceTicksWithFullPower(6, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var remainingBeforeSave = processor.GetRemainingTicks();
            var saveState = block.GetSaveState();

            // ロード後も加工状態と残りtickが復元され、装着中モジュールの電力倍率が即時反映されることを確認
            // After loading, the state and remaining ticks are restored and the equipped module's power multiplier applies live
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(200), saveState, block.BlockPositionInfo);
            var loadedProcessor = loadedBlock.GetComponent<VanillaMachineProcessorComponent>();

            Assert.AreEqual(ProcessState.Processing, loadedProcessor.CurrentState);
            Assert.AreEqual(remainingBeforeSave, loadedProcessor.GetRemainingTicks());

            var speedModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            Assert.AreEqual(loadedProcessor.RequestPower * (1f + speedModule.TradeoffValue), loadedProcessor.EffectiveRequestPower, 0.0001f);
        }

        [Test]
        // プロセス中にモジュールを外すと、加工時間は開始時のまま・電力倍率は即時に中立へ戻ることを確認する
        // Verify removing the module mid-process keeps the start-time ticks while the power multiplier reverts immediately
        public void ModuleRemovalMidProcessAppliesImmediatelyTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed, 1));
            InsertRecipeInputs(block, inventory, recipe);

            // プロセス開始後にモジュールを取り外す（開始tickでは進行しないため残りtick＝短縮済み加工時間）
            // Remove the module after the process has started (no progress on the start tick, so remaining ticks = scaled time)
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var ticksAtStart = processor.GetRemainingTicks();
            Assert.Less(ticksAtStart, baseTicks);
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());

            AdvanceTicksWithFullPower(3, processor);

            // 加工時間は開始時に確定した値のまま進行する
            // The processing time stays at the value fixed at start
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(ticksAtStart - 3, processor.GetRemainingTicks());

            // 電力倍率は取り外しが即時反映され中立に戻る
            // The power multiplier reverts to neutral immediately after removal
            Assert.AreEqual(processor.RequestPower, processor.EffectiveRequestPower, 0.0001f);
        }

        // テスト用電動機械（MachineId）を設置して主要コンポーネントを返す
        // Place the test electric machine (MachineId) and return its key components
        private static (IBlock block, VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor) PlaceMachine(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            return (block, inventory, processor);
        }

        // テスト用電動機械のレシピ（blocks.jsonのTestElectricMachineに対応）を取得する
        // Get the recipe for the test electric machine (matches TestElectricMachine in blocks.json)
        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            var machineBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineBlockGuid);
        }

        // レシピの入力アイテム1セットをインプットへ投入する
        // Insert one set of the recipe's input items into the input range
        private static void InsertRecipeInputs(IBlock block, VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
        {
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            foreach (var inputItem in recipe.InputItems)
            {
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
        }

        // 毎tick有効要求電力ちょうどを供給して進める（電力比1.0で確率的丸めを排除し決定論化する）
        // Advance ticks supplying exactly the effective request power (ratio 1.0 removes probabilistic rounding)
        private static void AdvanceTicksWithFullPower(int ticks, params VanillaMachineProcessorComponent[] processors)
        {
            for (var i = 0; i < ticks; i++)
            {
                foreach (var processor in processors) processor.SupplyExternalPower(processor.EffectiveRequestPower);
                GameUpdater.UpdateOneTick();
            }
        }

        // 指定効果軸のモジュールアイテムを生成する
        // Create a module item of the specified effect axis
        private static IItemStack CreateModuleItemOfAxis(string effectAxis, int count)
        {
            var moduleItemElement = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == effectAxis);
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleItemElement.ItemGuid);
            return ServerContext.ItemStackFactory.Create(moduleItemId, count);
        }

        // アウトプットレンジ内の指定アイテムの合計数を数える
        // Count the total amount of the specified item in the output range
        private static int CountOutputItem(VanillaMachineBlockInventoryComponent inventory, ItemId itemId)
        {
            var total = 0;
            for (var i = InputSlotCount; i < InputSlotCount + OutputSlotCount; i++)
            {
                var stack = inventory.GetItem(i);
                if (stack.Id == itemId) total += stack.Count;
            }
            return total;
        }

        // テスト用モジュールアイテム（items.jsonのmoduleParam定義に対応）を生成する
        // Create a test module item (linked to a moduleParam definition in items.json)
        private static IItemStack CreateModuleItem(int count)
        {
            var moduleItemElement = MasterHolder.ItemMaster.Items.Modules.First();
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleItemElement.ItemGuid);
            return ServerContext.ItemStackFactory.Create(moduleItemId, count);
        }

        // モジュールレンジの全スロットが空であることを検証する
        // Assert that every slot in the module range is empty
        private static void AssertModuleRangeIsEmpty(VanillaMachineBlockInventoryComponent inventory)
        {
            for (var i = ModuleRangeStart; i < ModuleRangeStart + ModuleSlotCount; i++)
            {
                Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(i).Id);
            }
        }
    }
}
