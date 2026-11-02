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
using Mooresmaster.Model.ItemsModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine
{
    /// <summary>
    ///     品質モジュールが一般機械の出力を変えないことと、レベル変種の出力がセーブをまたいで残ることを確かめる
    ///     Verifies quality modules leave general machine output unchanged and level-variant outputs survive a save
    /// </summary>
    public class LevelVariantOutputTest
    {
        // テスト用機械のスロット構成（blocks.jsonのTestElectricMachineに対応）
        // Slot layout of the test machine (matches TestElectricMachine in blocks.json)
        private const int InputSlotCount = 2;
        private const int OutputSlotCount = 3;
        private const int ModuleRangeStart = InputSlotCount + OutputSlotCount;

        [Test]
        public void QualityModuleLeavesGeneralOutputAtBaseLevelTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var quality = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Quality);
            var productivity = GetModuleOfAxis(ModuleMasterElement.EffectAxisConst.Productivity);
            Assert.GreaterOrEqual(productivity.EffectValue, 1f, "生産性の追加産出が確定でない（前提が崩れている）");

            // 品質と生産性を付けて1サイクル回す（時間は両トレードオフの合算で延びる）
            // Run one cycle with quality and productivity (time stretches by both tradeoffs)
            inventory.SetItem(ModuleRangeStart, CreateModuleItem(quality));
            inventory.SetItem(ModuleRangeStart + 1, CreateModuleItem(productivity));
            InsertRecipeInputs(block, inventory, recipe);
            var ticks = (int)Math.Round(GameUpdater.SecondsToTicks(recipe.Time) * (1f + quality.TradeoffValue + productivity.TradeoffValue));
            AdvanceTicksWithFullPower(1 + ticks + 3, processor);

            // 出力は基準アイテム2セットで、上位変種は1つも無い
            // The output is two sets of the base item with no higher variant at all
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            var (baseItemId, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);
            Assert.AreEqual(recipe.OutputItems[0].Count * 2, CountOutputItem(inventory, baseItemId));
            Assert.AreEqual(0, CountOutputItem(inventory, lv2ItemId));

            #region Internal

            static void InsertRecipeInputs(IBlock block, VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
            {
                MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
                foreach (var inputItem in recipe.InputItems)
                {
                    inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
                }
            }

            // 毎tick有効要求電力ちょうどを供給して進める（電力比1.0で確率的丸めを排除する）
            // Advance ticks supplying exactly the effective request power (ratio 1.0 removes probabilistic rounding)
            static void AdvanceTicksWithFullPower(int ticks, VanillaMachineProcessorComponent processor)
            {
                for (var i = 0; i < ticks; i++)
                {
                    processor.SupplyExternalPower(processor.EffectiveRequestPower);
                    GameUpdater.UpdateOneTick();
                }
            }

            static ModuleMasterElement GetModuleOfAxis(string effectAxis)
            {
                return MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == effectAxis);
            }

            static IItemStack CreateModuleItem(ModuleMasterElement moduleElement)
            {
                return ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(moduleElement.ItemGuid), 1);
            }

            static int CountOutputItem(VanillaMachineBlockInventoryComponent inventory, ItemId itemId)
            {
                var total = 0;
                for (var i = InputSlotCount; i < InputSlotCount + OutputSlotCount; i++)
                {
                    var stack = inventory.GetItem(i);
                    if (stack.Id == itemId) total += stack.Count;
                }
                return total;
            }

            #endregion
        }

        [Test]
        public void VariantOutputSaveLoadTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = GetMachineRecipe();
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            var (_, lv2ItemId) = GetBaseAndLv2ItemIds(recipe);

            // 束縛(ADR 0042)のため、出力スロットへ置く前にレシピを選択してから変種を置いてセーブする
            // Binding (ADR 0042) needs the recipe selected first; then place the variant and save
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            inventory.SetItem(InputSlotCount, ServerContext.ItemStackFactory.Create(lv2ItemId, 7));
            var saveState = block.GetSaveState();

            // ロード後も変種ItemIdと数量が残る
            // The variant ItemId and count survive the load
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            var loadedBlock = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(301), saveState, block.BlockPositionInfo);
            var loadedInventory = loadedBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(lv2ItemId, loadedInventory.GetItem(InputSlotCount).Id);
            Assert.AreEqual(7, loadedInventory.GetItem(InputSlotCount).Count);
        }

        private static (IBlock block, VanillaMachineBlockInventoryComponent inventory, VanillaMachineProcessorComponent processor) PlaceMachine(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return (block, block.GetComponent<VanillaMachineBlockInventoryComponent>(), block.GetComponent<VanillaMachineProcessorComponent>());
        }

        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            var machineBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineBlockGuid);
        }

        // 出力の基準ItemIdとLv2変種ItemId（ファミリーがある前提も確かめる）
        // The output's base and Lv2 variant ItemIds (also asserts the family exists)
        private static (ItemId baseItemId, ItemId lv2ItemId) GetBaseAndLv2ItemIds(MachineRecipeMasterElement recipe)
        {
            var baseItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.IsTrue(MasterHolder.ItemMaster.HasLevelFamily(baseItemId));
            var lv2ItemId = MasterHolder.ItemMaster.GetLevelVariantItemId(baseItemId, 2);
            Assert.AreNotEqual(baseItemId, lv2ItemId);
            return (baseItemId, lv2ItemId);
        }

    }
}
