using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MachineRecipesModule;
using Mooresmaster.Model.ModulesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     品質モジュールによる出力のレベル変種差し替え・容量予約・セーブ整合を検証するテスト
    ///     Tests verifying quality module level-variant output, capacity reservation, and save consistency
    /// </summary>
    public class QualityModuleOutputTest
    {
        // テスト用機械のスロット構成（blocks.jsonのTestElectricMachineに対応）
        // Slot layout of the test machine (matches TestElectricMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        [Test]
        // 品質シフト1.0の品質モジュールで、完了時に基準アイテムでなくレベル2変種が出力されることを確認する
        // Verify a quality module with shift 1.0 outputs the level-2 variant instead of the base item on completion
        public void QualityModuleProducesUpgradedOutputTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality, 1));
            InsertRecipeInputs(inventory, recipe);

            // 前提: 整数部1段が確定（effectValue=1.0）であること。データ変更時はここで失敗させる
            // Precondition: one guaranteed level-up (effectValue = 1.0); fail loudly on data drift
            var qualityModule = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Quality);
            Assert.AreEqual(1f, qualityModule.EffectValue, 0.0001f);

            // 品質トレードオフで時間が延びるため、余裕を持って完了まで進める
            // The quality tradeoff stretches the time, so advance well past completion
            AdvanceTicksWithFullPower(1 + ScaledTicks(recipe, 1f + qualityModule.TradeoffValue) + 3, processor);

            // 完了済みで、出力がすべてレベル2変種であり基準アイテムが存在しないことを確認
            // Processing finished; the output is entirely the level-2 variant with no base items
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            var (baseItemId, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);
            Assert.AreEqual(recipe.OutputItems[0].Count, CountOutputItem(inventory, lv2ItemId));
            Assert.AreEqual(0, CountOutputItem(inventory, baseItemId));
        }

        [Test]
        // 品質＋生産性の同時装着で、ベースセットと追加セットの両方がレベル2変種になることを確認する
        // Verify quality + productivity together upgrade both the base set and the extra set to the level-2 variant
        public void QualityWithProductivityBothUpgradedTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality, 1));
            inventory.SetItem(ModuleRangeStart + 1, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Productivity, 1));
            InsertRecipeInputs(inventory, recipe);

            // 前提: 追加出力が確定（確率1.0）であること
            // Precondition: the extra output is guaranteed (chance 1.0)
            var qualityModule = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Quality);
            var productivityModule = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Productivity);
            Assert.GreaterOrEqual(productivityModule.EffectValue, 1f);

            // 時間は両トレードオフの合算で延びる（(1+0.5+0.5)倍）ため、余裕を持って進める
            // Time stretches by both tradeoffs combined ((1+0.5+0.5)x), so advance well past completion
            AdvanceTicksWithFullPower(1 + ScaledTicks(recipe, 1f + qualityModule.TradeoffValue + productivityModule.TradeoffValue) + 3, processor);

            // 完了済みで、レシピ出力数の2倍がすべてレベル2変種になっていることを確認
            // Processing finished; double the recipe output count is present, all as the level-2 variant
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            var (baseItemId, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);
            Assert.AreEqual(recipe.OutputItems[0].Count * 2, CountOutputItem(inventory, lv2ItemId));
            Assert.AreEqual(0, CountOutputItem(inventory, baseItemId));
        }

        [Test]
        // 容量予約が変種IDで効くこと（基準は入るが変種が入らない場合は開始しない／変種が入る場合は開始する）を確認する
        // Verify reservation uses the variant id (no start when only the base fits; start when the variant fits)
        public void QualityReservationUsesVariantTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = GetMachineRecipe();
            var (baseItemId, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);
            var maxStack = MasterHolder.ItemMaster.GetItemMaster(baseItemId).MaxStack;

            // 品質装着機・未装着機・変種空きあり装着機の3台を設置する
            // Place three machines: quality-equipped, plain, and quality-equipped with variant space
            var (qualityBlock, qualityInventory, qualityProcessor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var (plainBlock, plainInventory, plainProcessor) = PlaceMachine(new Vector3Int(5, 1, 1));
            var (variantFitBlock, variantFitInventory, variantFitProcessor) = PlaceMachine(new Vector3Int(9, 1, 1));
            qualityInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality, 1));
            variantFitInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality, 1));

            // 出力を「基準アイテム1セット分の空きはあるが、別スタックの変種は入らない」状態まで埋める
            // Fill outputs so one base set fits but the separately-stacking variant cannot
            foreach (var inventory in new[] { qualityInventory, plainInventory })
            {
                inventory.SetItem(InputSlotCount, itemStackFactory.Create(baseItemId, maxStack));
                inventory.SetItem(InputSlotCount + 1, itemStackFactory.Create(baseItemId, maxStack));
                inventory.SetItem(InputSlotCount + 2, itemStackFactory.Create(baseItemId, maxStack - recipe.OutputItems[0].Count));
            }

            // 変種空きあり装着機は最終スロットに変種の部分スタックを置き、変種が積み増しできる状態にする
            // The variant-fit machine holds a partial variant stack in its last slot so the variant can stack up
            variantFitInventory.SetItem(InputSlotCount, itemStackFactory.Create(baseItemId, maxStack));
            variantFitInventory.SetItem(InputSlotCount + 1, itemStackFactory.Create(baseItemId, maxStack));
            variantFitInventory.SetItem(InputSlotCount + 2, itemStackFactory.Create(lv2ItemId, 1));

            InsertRecipeInputs(qualityInventory, recipe);
            InsertRecipeInputs(plainInventory, recipe);
            InsertRecipeInputs(variantFitInventory, recipe);

            AdvanceTicksWithFullPower(2, qualityProcessor, plainProcessor, variantFitProcessor);

            // 品質装着機は変種の空きが無いため開始せず、インプットも消費されない
            // The quality machine does not start (no space for the variant) and keeps its inputs
            Assert.AreEqual(ProcessState.Idle, qualityProcessor.CurrentState);
            Assert.AreNotEqual(ItemMaster.EmptyItemId, qualityInventory.GetItem(0).Id);

            // 未装着機は基準アイテムの空きがあるため開始し、変種空きあり装着機も開始する
            // The plain machine starts (base space available) and the variant-fit machine starts as well
            Assert.AreEqual(ProcessState.Processing, plainProcessor.CurrentState);
            Assert.AreEqual(ProcessState.Processing, variantFitProcessor.CurrentState);
        }

        [Test]
        // プロセス途中でセーブ・ロードしても品質シフトのスナップショットが維持され、変種が出力されることを確認する
        // Verify the quality shift snapshot survives a mid-process save/load and the variant is still produced
        public void QualityShiftSurvivesMidProcessSaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var position = new Vector3Int(1, 1, 1);
            var (block, inventory, processor) = PlaceMachine(position);
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality, 1));
            InsertRecipeInputs(inventory, recipe);

            // 数tick進めたプロセス途中の状態を作り、ワールド全体をセーブする
            // Advance a few ticks into the process, then save the entire world
            AdvanceTicksWithFullPower(3, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 新しいDIコンテナでワールドをロードし、品質シフトのスナップショットが復元されていることを確認
            // Load the world in a fresh DI container and verify the quality shift snapshot is restored
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)loadServiceProvider.GetService<IWorldSaveDataLoader>()).Load(saveJson);

            var loadedBlock = ServerContext.WorldBlockDatastore.GetBlock(position);
            var loadedProcessor = loadedBlock.GetComponent<VanillaMachineProcessorComponent>();
            var loadedInventory = loadedBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(ProcessState.Processing, loadedProcessor.CurrentState);
            var qualityModule = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Quality);
            Assert.AreEqual(qualityModule.EffectValue, loadedProcessor.CurrentQualityShift, 0.0001f);

            // ロード後のワールドで完了まで進め、出力がレベル2変種のみであることを確認（スナップショット復元の実効確認）
            // Advance the loaded world to completion; the output is only the level-2 variant (proves the snapshot restoration works)
            AdvanceTicksWithFullPower((int)loadedProcessor.RemainingTicks + 3, loadedProcessor);
            Assert.AreEqual(ProcessState.Idle, loadedProcessor.CurrentState);
            var (baseItemId, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);
            Assert.AreEqual(recipe.OutputItems[0].Count, CountOutputItem(loadedInventory, lv2ItemId));
            Assert.AreEqual(0, CountOutputItem(loadedInventory, baseItemId));
        }

        [Test]
        // レベル変種アイテムが出力スロットに入った状態でセーブ・ロードしても変種IDが保持されることを確認する
        // Verify a level variant item in the output slot retains its ItemId across a save/load round-trip
        public void VariantOutputSaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var (baseItemId, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);

            // 変種アイテムを出力スロットへ直接置いてセーブする
            // Place the variant item directly into an output slot and save
            inventory.SetItem(InputSlotCount, ServerContext.ItemStackFactory.Create(lv2ItemId, 7));
            var saveState = block.GetSaveState();

            // ロード後も変種ItemIdと数量が保持されていることを確認（独立ItemIdの保存経路）
            // The variant ItemId and count survive the load (independent-ItemId save path)
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(301), saveState, block.BlockPositionInfo);
            var loadedInventory = loadedBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(lv2ItemId, loadedInventory.GetItem(InputSlotCount).Id);
            Assert.AreEqual(7, loadedInventory.GetItem(InputSlotCount).Count);
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

        // テスト用電動機械のレシピを取得する
        // Get the recipe for the test electric machine
        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            var machineBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineBlockGuid);
        }

        // レシピ出力の基準ItemIdとレベル2変種ItemIdを取得する（ファミリー登録の前提も検証）
        // Get the base and level-2 variant ItemIds of the recipe output (also asserts the family exists)
        private static (ItemId baseItemId, ItemId lv2ItemId) GetBaseAndLv2ItemIds(MachineRecipeMasterElement recipe)
        {
            var baseItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.IsTrue(MasterHolder.LevelFamilyMaster.HasFamily(baseItemId));
            var lv2ItemId = MasterHolder.LevelFamilyMaster.GetVariantItemId(baseItemId, 2);
            Assert.AreNotEqual(baseItemId, lv2ItemId);
            return (baseItemId, lv2ItemId);
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

        // トレードオフ倍率を適用した加工tick数を計算する
        // Compute the processing ticks scaled by the tradeoff multiplier
        private static int ScaledTicks(MachineRecipeMasterElement recipe, float timeMultiplier)
        {
            var baseTicks = GameUpdater.SecondsToTicks(recipe.Time);
            return (int)Math.Round(baseTicks * timeMultiplier);
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

        // 指定効果軸のモジュール定義を取得する
        // Get the module definition of the specified effect axis
        private static ModuleMasterElement GetModuleOfAxis(string effectAxis)
        {
            return MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == effectAxis);
        }

        // 指定効果軸のモジュールアイテムを生成する
        // Create a module item of the specified effect axis
        private static IItemStack CreateModuleItemOfAxis(string effectAxis, int count)
        {
            var moduleElement = GetModuleOfAxis(effectAxis);
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
    }
}
