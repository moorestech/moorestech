using System;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using Mooresmaster.Model.ModulesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
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

            // 最終アウトプットスロットへのセットがモジュールレンジへ流れないことを確認
            // Verify a set to the last output slot does not route into the module range
            var lastOutputSlot = InputSlotCount + OutputSlotCount - 1;
            var outputItem = ServerContext.ItemStackFactory.Create(new ItemId(3), 7);
            inventory.SetItem(lastOutputSlot, outputItem);
            Assert.AreEqual(outputItem, inventory.GetItem(lastOutputSlot));

            // リフレクションでアウトプットサブインベントリの実体に入っていることを確認
            // Verify via reflection that the item landed in the actual output sub-inventory
            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
            Assert.AreEqual(outputItem, outputInventory.OutputSlot[OutputSlotCount - 1]);

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

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // モジュールアイテムを搬送経由で挿入してもインプットスロットに入ることを確認
            // Inserting a module item via transport lands in an input slot
            var moduleItem = CreateModuleItem(1);
            var remainder = inventory.InsertItem(moduleItem);
            Assert.AreEqual(0, remainder.Count);
            Assert.AreEqual(moduleItem, inventory.GetItem(0));
            AssertModuleRangeIsEmpty(inventory);

            // インプットを満杯にしてさらに挿入しても、モジュールスロットへ溢れないことを確認
            // Fill the input range completely; further inserts must not overflow into module slots
            var item1MaxStack = MasterHolder.ItemMaster.GetItemMaster(new ItemId(1)).MaxStack;
            var item2MaxStack = MasterHolder.ItemMaster.GetItemMaster(new ItemId(2)).MaxStack;
            inventory.SetItem(0, itemStackFactory.Create(new ItemId(1), item1MaxStack));
            inventory.SetItem(1, itemStackFactory.Create(new ItemId(2), item2MaxStack));

            var overflowRemainder = inventory.InsertItem(itemStackFactory.Create(new ItemId(3), 5));
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
        // インベントリ整理でモジュールスロットが除外されることと、ギア機械にもモジュールスロットがあることを確認する
        // Verify inventory sorting excludes module slots and that gear machines also have module slots
        public void SortExcludesModuleSlotsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // インプットをItemId降順で配置し、モジュールスロットにモジュールアイテムを装着する
            // Place input items in descending ItemId order and equip a module item into a module slot
            inventory.SetItem(0, itemStackFactory.Create(new ItemId(5), 3));
            inventory.SetItem(1, itemStackFactory.Create(new ItemId(2), 4));
            var moduleItem = CreateModuleItem(1);
            inventory.SetItem(ModuleRangeStart, moduleItem);

            InventorySortService.Sort(inventory, inventory.GetSortExcludedSlots());

            // インプット・アウトプットレンジはソートされ、モジュールスロットはそのまま残ることを確認
            // Input/output ranges are sorted while module slots stay untouched
            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 4), inventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(5), 3), inventory.GetItem(1));
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
            InsertRecipeInputs(boostedInventory, recipe);
            InsertRecipeInputs(plainInventory, recipe);

            // 短縮時間とベース時間の中間点まで進める（装着機の短縮時間は超え、ベース時間には届かない）
            // Advance to the midpoint of the boosted and base durations (past boosted, short of base)
            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            var expectedBoostedTicks = (uint)Math.Max(1, (long)Math.Round(baseTicks * (1f / (1f + speedModule.EffectValue))));
            var advanceTicks = (int)(1 + (expectedBoostedTicks + baseTicks) / 2);
            AdvanceTicksWithFullPower(advanceTicks, boostedProcessor, plainProcessor);

            // 装着機は完了してIdle、未装着機はまだProcessingであることを確認
            // The boosted machine has finished (Idle) while the plain machine is still Processing
            Assert.AreEqual(ProcessState.Idle, boostedProcessor.CurrentState);
            Assert.AreEqual(expectedBoostedTicks, boostedProcessor.ProcessingRecipeTicks);
            Assert.AreEqual(ProcessState.Processing, plainProcessor.CurrentState);
            Assert.AreEqual(baseTicks, plainProcessor.ProcessingRecipeTicks);

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

            // Idle中はモジュールがあっても要求電力は変わらない
            // While idle, the requested power is unchanged even with the module equipped
            var electric = block.GetComponent<VanillaElectricMachineComponent>();
            Assert.AreEqual(processor.RequestPower, electric.RequestEnergy.AsPrimitive(), 0.0001f);

            // プロセス開始後は省エネ倍率分だけ要求電力が下がる
            // After processing starts, the requested power drops by the efficiency multiplier
            InsertRecipeInputs(inventory, recipe);
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            var efficiencyModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Efficiency);
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
            InsertRecipeInputs(inventory, recipe);

            // 前提: 追加出力が確定（確率1.0）になるeffectValueであること。データ変更時はここで失敗させる
            // Precondition: effectValue must guarantee the extra output (chance 1.0); fail loudly on data drift
            var productivityModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Productivity);
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
            modInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Productivity, 1));

            // アウトプットを「ベース1セットは入るが追加セットは入らない」量まで埋める
            // Fill outputs so one base set fits but the extra set does not
            var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(outputItemId).MaxStack;
            foreach (var inventory in new[] { modInventory, ctrlInventory })
            {
                inventory.SetItem(InputSlotCount, itemStackFactory.Create(outputItemId, maxStack));
                inventory.SetItem(InputSlotCount + 1, itemStackFactory.Create(outputItemId, maxStack));
                inventory.SetItem(InputSlotCount + 2, itemStackFactory.Create(outputItemId, maxStack - recipe.OutputItems[0].Count));
            }
            InsertRecipeInputs(modInventory, recipe);
            InsertRecipeInputs(ctrlInventory, recipe);

            AdvanceTicksWithFullPower(2, modProcessor, ctrlProcessor);

            // 装着機は開始せずインプットが残り、未装着機はベース分の空きがあるため開始する
            // The equipped machine stays idle with inputs intact while the plain machine starts
            Assert.AreEqual(ProcessState.Idle, modProcessor.CurrentState);
            Assert.AreNotEqual(ItemMaster.EmptyItemId, modInventory.GetItem(0).Id);
            Assert.AreEqual(ProcessState.Processing, ctrlProcessor.CurrentState);
        }

        [Test]
        // プロセス途中でセーブ・ロードしても短縮済みの加工時間が維持されることを確認する
        // Verify the speed-scaled processing time survives a mid-process save and load
        public void EffectSurvivesMidProcessSaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed, 1));
            InsertRecipeInputs(inventory, recipe);

            // 開始＋数tick進めたプロセス途中の状態を作ってセーブする
            // Start the process, advance a few ticks mid-process, then save
            AdvanceTicksWithFullPower(6, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var ticksBeforeSave = processor.ProcessingRecipeTicks;
            var remainingBeforeSave = processor.RemainingTicks;
            Assert.Less(ticksBeforeSave, baseTicks);
            var saveState = block.GetSaveState();

            // ロード後も短縮済みtick数・残りtick・電力倍率が復元されることを確認
            // After loading, the scaled ticks, remaining ticks, and power multiplier are restored
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(200), saveState, block.BlockPositionInfo);
            var loadedProcessor = loadedBlock.GetComponent<VanillaMachineProcessorComponent>();

            Assert.AreEqual(ProcessState.Processing, loadedProcessor.CurrentState);
            Assert.AreEqual(ticksBeforeSave, loadedProcessor.ProcessingRecipeTicks);
            Assert.Less(loadedProcessor.ProcessingRecipeTicks, baseTicks);
            Assert.AreEqual(remainingBeforeSave, loadedProcessor.RemainingTicks);

            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            Assert.AreEqual(1f + speedModule.TradeoffValue, loadedProcessor.CurrentPowerMultiplier, 0.0001f);
        }

        [Test]
        // 効果系キーの無い旧セーブはレシピ時間の再計算と中立効果でロードされることを確認する
        // Verify an old save lacking the effect keys loads with recipe-time recomputation and neutral effects
        public void OldSaveWithoutEffectKeysLoadsNeutralTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed, 1));
            InsertRecipeInputs(inventory, recipe);

            // 短縮済みプロセス途中のセーブから効果系4キーを取り除き、旧セーブを再現する
            // Strip the four effect keys from a mid-process save to emulate an old save
            AdvanceTicksWithFullPower(6, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var saveState = block.GetSaveState();
            var saveKey = typeof(VanillaMachineSaveComponent).FullName;
            var machineJson = JObject.Parse(saveState[saveKey]);
            machineJson.Remove("processingTotalSeconds");
            machineJson.Remove("effectPowerMultiplier");
            machineJson.Remove("effectExtraOutputChance");
            machineJson.Remove("processedCycleCount");
            saveState[saveKey] = machineJson.ToString();

            // 加工時間はレシピ定義から再計算され、要求電力は中立（等倍）でロードされることを確認
            // Ticks are recomputed from the recipe definition and the request power loads as neutral
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(201), saveState, block.BlockPositionInfo);
            var loadedProcessor = loadedBlock.GetComponent<VanillaMachineProcessorComponent>();

            Assert.AreEqual(ProcessState.Processing, loadedProcessor.CurrentState);
            Assert.AreEqual(baseTicks, loadedProcessor.ProcessingRecipeTicks);
            Assert.AreEqual(loadedProcessor.RequestPower, loadedProcessor.EffectiveRequestPower, 0.0001f);
        }

        [Test]
        // プロセス中にモジュールを外しても開始時のスナップショット効果が維持されることを確認する
        // Verify the snapshot taken at process start persists even if the module is removed mid-process
        public void EffectSnapshotPersistsAfterModuleRemovalTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Speed, 1));
            InsertRecipeInputs(inventory, recipe);

            // プロセス開始後にモジュールを取り外す
            // Remove the module after the process has started
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var ticksAtStart = processor.ProcessingRecipeTicks;
            Assert.Less(ticksAtStart, baseTicks);
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());

            AdvanceTicksWithFullPower(3, processor);

            // 加工時間・電力倍率ともスナップショットのまま進行していることを確認
            // Both the processing time and the power multiplier still follow the snapshot
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(ticksAtStart, processor.ProcessingRecipeTicks);
            Assert.AreEqual(ticksAtStart - 3, processor.RemainingTicks);

            var speedModule = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            Assert.AreEqual(processor.RequestPower * (1f + speedModule.TradeoffValue), processor.EffectiveRequestPower, 0.0001f);
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
        private static void InsertRecipeInputs(VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
        {
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
                foreach (var processor in processors) processor.SupplyPower(processor.EffectiveRequestPower);
                GameUpdater.UpdateOneTick();
            }
        }

        // 指定効果軸のモジュールアイテムを生成する
        // Create a module item of the specified effect axis
        private static IItemStack CreateModuleItemOfAxis(string effectAxis, int count)
        {
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == effectAxis);
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
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

        // テスト用モジュールアイテム（modules.jsonの定義に対応するアイテム）を生成する
        // Create a test module item (the item linked to a modules.json definition)
        private static IItemStack CreateModuleItem(int count)
        {
            var moduleElement = MasterHolder.ModuleMaster.Modules.Data[0];
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
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
