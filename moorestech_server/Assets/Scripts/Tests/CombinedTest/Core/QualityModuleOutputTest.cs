using System;
using System.Linq;
using Core.Item.Interface;
using Core.Item;
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
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
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
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality));
            InsertRecipeInputs(block, inventory, recipe);

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
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality));
            inventory.SetItem(ModuleRangeStart + 1, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Productivity));
            InsertRecipeInputs(block, inventory, recipe);

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
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(baseItemId);

            // 品質装着機・未装着機・変種空きあり装着機の3台を設置する
            // Place three machines: quality-equipped, plain, and quality-equipped with variant space
            var (qualityBlock, qualityInventory, qualityProcessor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var (plainBlock, plainInventory, plainProcessor) = PlaceMachine(new Vector3Int(5, 1, 1));
            var (variantFitBlock, variantFitInventory, variantFitProcessor) = PlaceMachine(new Vector3Int(9, 1, 1));
            qualityInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality));
            variantFitInventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality));

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

            InsertRecipeInputs(qualityBlock, qualityInventory, recipe);
            InsertRecipeInputs(plainBlock, plainInventory, recipe);
            InsertRecipeInputs(variantFitBlock, variantFitInventory, recipe);

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
        // 小数シフト＋生産性で混在レベル（lv2+lv3）が実現し得る状況でも、混在ペアでは開始せず、完了した機械は必ず全量格納される（消失ゼロ）ことを確認する
        // Verify that with fractional shift + productivity a mixed-level pair (lv2+lv3) never starts, and every completed machine stores everything (no silent loss)
        public void MixedLevelRealizationReservesExactlyTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = GetMachineRecipe();
            var baseItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            var lv2ItemId = MasterHolder.ItemMaster.GetLevelVariantItemId(baseItemId, 2);
            var lv3ItemId = MasterHolder.ItemMaster.GetLevelVariantItemId(baseItemId, 3);
            Assert.AreNotEqual(lv2ItemId, lv3ItemId);
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(baseItemId);

            // 前提: 品質1.0+0.4でシフト1.4（確定1段＋40%でもう1段）、生産性1.0で追加セット確定
            // Precondition: quality 1.0 + 0.4 gives shift 1.4 (one guaranteed + 40% one more); productivity 1.0 guarantees the extra set
            var qualityModules = MasterHolder.ItemMaster.Items.Modules.Where(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Quality).ToArray();
            var quality10 = qualityModules.First(m => Math.Abs(m.EffectValue - 1.0f) < 0.0001f);
            var quality04 = qualityModules.First(m => Math.Abs(m.EffectValue - 0.4f) < 0.0001f);
            var productivity = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Productivity);

            // 出力空きスロットを1つだけ残した機械を多数並べる（混在ペアは2スロット必要なため開始できず、同レベルペアを引いたtickにのみ開始する）
            // Place many machines with only one free output slot (a mixed pair needs two slots so it cannot start; a machine starts only on a tick that rolls a same-level pair)
            const int machineCount = 20;
            var machines = new (VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor)[machineCount];
            for (var i = 0; i < machineCount; i++)
            {
                var (block, inventory, processor) = PlaceMachine(new Vector3Int(1 + i * 3, 1, 1));
                inventory.SetItem(ModuleRangeStart, CreateModuleItem(quality10));
                inventory.SetItem(ModuleRangeStart + 1, CreateModuleItem(quality04));
                inventory.SetItem(ModuleRangeStart + 2, CreateModuleItem(productivity));

                // 基準アイテム満杯スロット2つ＋空きスロット1つ（同レベル2個は1スロットに収まるが、混在は2スロット必要）
                // Two full base-item slots plus one free slot (two same-level items fit one slot; a mixed pair needs two)
                inventory.SetItem(InputSlotCount, itemStackFactory.Create(baseItemId, maxStack));
                inventory.SetItem(InputSlotCount + 1, itemStackFactory.Create(baseItemId, maxStack));
                InsertRecipeInputs(block, inventory, recipe);
                machines[i] = (inventory, processor);
            }

            // 時間はトレードオフ合算（1+0.5+0+0.5=2.0倍）で延びるため、余裕を持って完了まで進める
            // Time stretches by the combined tradeoffs (1+0.5+0+0.5 = 2.0x), so advance well past completion
            var allProcessors = machines.Select(m => m.processor).ToArray();
            AdvanceTicksWithFullPower(1 + ScaledTicks(recipe, 2.0f) + 3, allProcessors);

            // 各機械で「未開始・進行中（出力増加なし）」か「完了して同レベル2個全量格納」のどちらかであることを確認（消失ゼロ・混在ペアでの開始なし）
            // Each machine either has not produced yet (not started / in progress) or completed storing a same-level pair (zero loss, no mixed-pair start)
            var completedCount = 0;
            foreach (var (inventory, processor) in machines)
            {
                var lv2Count = CountOutputItem(inventory, lv2ItemId);
                var lv3Count = CountOutputItem(inventory, lv3ItemId);
                var newItemCount = lv2Count + lv3Count;
                var inputConsumed = inventory.GetItem(0).Id == ItemMaster.EmptyItemId;

                if (inputConsumed && processor.CurrentState == ProcessState.Idle)
                {
                    // 完了済みは同レベルペアが消失なく格納される（混在ペアは空き1スロットに入らないため開始時に弾かれる）
                    // A completed machine stores a same-level pair with no loss (a mixed pair cannot fit the single free slot and is rejected at start)
                    Assert.AreEqual(2, newItemCount);
                    Assert.IsTrue(lv2Count == 2 || lv3Count == 2);
                    completedCount++;
                }
                else
                {
                    // 未開始または進行中の機械は出力が増えない
                    // A machine that has not started or is still processing gains no outputs
                    Assert.AreEqual(0, newItemCount);
                }
            }

            // 同レベルペア（確率0.52）を引いて完了する機械が20台中に十分現れること
            // Enough machines roll a same-level pair (probability 0.52) and complete among the 20
            Assert.GreaterOrEqual(completedCount, 1);
        }

        [Test]
        // プロセス途中でセーブ・ロードしても開始時に確定した産出予定が引き継がれ、ロード後にモジュールを外しても変種が出力されることを確認する
        // Verify the pending outputs fixed at start survive a mid-process save/load and the variant is produced even if the module is removed after loading
        public void QualityShiftSurvivesMidProcessSaveLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var position = new Vector3Int(1, 1, 1);
            var (block, inventory, processor) = PlaceMachine(position);
            inventory.SetItem(ModuleRangeStart, CreateModuleItemOfAxis(ModuleMasterElement.EffectAxisConst.Quality));
            InsertRecipeInputs(block, inventory, recipe);

            // 数tick進めたプロセス途中の状態を作り、ワールド全体をセーブする
            // Advance a few ticks into the process, then save the entire world
            AdvanceTicksWithFullPower(3, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 新しいDIコンテナでワールドをロードし、加工途中の状態が復元されていることを確認
            // Load the world in a fresh DI container and verify the mid-process state is restored
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)loadServiceProvider.GetService<IWorldSaveDataLoader>()).Load(saveJson);

            var loadedBlock = ServerContext.WorldBlockDatastore.GetBlock(position);
            var loadedProcessor = loadedBlock.GetComponent<VanillaMachineProcessorComponent>();
            var loadedInventory = loadedBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(ProcessState.Processing, loadedProcessor.CurrentState);

            // ロード直後にモジュールを取り外し、保存済みの産出予定が使われることを確実に検証する
            // Remove the module right after loading to prove the saved pending outputs are used
            loadedInventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.CreatEmpty());

            // ロード後のワールドで完了まで進め、出力がレベル2変種のみであることを確認（セーブされた産出予定スタックの実効確認）
            // Advance the loaded world to completion; the output is only the level-2 variant (proves the saved pending output stacks take effect)
            AdvanceTicksWithFullPower((int)loadedProcessor.GetRemainingTicks() + 3, loadedProcessor);
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
            Assert.IsTrue(MasterHolder.ItemMaster.HasLevelFamily(baseItemId));
            var lv2ItemId = MasterHolder.ItemMaster.GetLevelVariantItemId(baseItemId, 2);
            Assert.AreNotEqual(baseItemId, lv2ItemId);
            return (baseItemId, lv2ItemId);
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

        // 指定効果軸のモジュールアイテム定義を取得する
        // Get the module item definition of the specified effect axis
        private static ModuleMasterElement GetModuleOfAxis(string effectAxis)
        {
            return MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == effectAxis);
        }

        // 指定効果軸のモジュールアイテムを生成する
        // Create a module item of the specified effect axis
        private static IItemStack CreateModuleItemOfAxis(string effectAxis)
        {
            return CreateModuleItem(GetModuleOfAxis(effectAxis));
        }

        // 指定モジュール定義のアイテムを1個生成する
        // Create one item of the specified module definition
        private static IItemStack CreateModuleItem(ModuleMasterElement moduleElement)
        {
            var moduleItemId = MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid);
            return ServerContext.ItemStackFactory.Create(moduleItemId, 1);
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
