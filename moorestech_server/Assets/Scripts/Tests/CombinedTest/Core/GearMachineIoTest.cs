using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearMachineIoTest
    {
        public int GearMachineRecipeIndex = 3;
        
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[GearMachineRecipeIndex];

            // ギアマシンブロックの配置
            // Place the gear machine block
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var gearMachineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockParam as GearMachineBlockParam;
            var machineProcessor = block.GetComponent<VanillaMachineProcessorComponent>();

            // 満額の歯車電力（baseTorque×baseRpm）を機械へ直接供給する。gearの現在値は導出のみで外部注入できないため機械の電力経路を使う
            // Supply full gear power (baseTorque×baseRpm) directly to the machine; gear values are derived-only, so use the machine's power path
            var suppliedPower = (float)(gearMachineParam.GearConsumption.BaseTorque * gearMachineParam.GearConsumption.BaseRpm);

            // クラフト時間をtick単位で計算（マージン付き）
            // Calculate craft time in ticks with margin
            var craftTicks = GameUpdater.SecondsToTicks(recipe.Time) + 10;

            for (uint tick = 0; tick < craftTicks; tick++)
            {
                // tick数を先に設定してから処理を行う
                // Set tick count before processing
                GameUpdater.RunFrames(1);

                machineProcessor.SupplyExternalPower(suppliedPower);
                machineProcessor.Update();
            }

            //検証
            AssertInventory(blockInventory, recipe);
        }
        
        
        [Test]
        // RPM、トルクが足りないときに処理に時間がかかるテスト
        // Test that processing takes longer when RPM or torque is insufficient
        public void NotEnoughTorqueOrRpmTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[GearMachineRecipeIndex];

            // ギアマシンブロックの配置
            // Place the gear machine blocks
            var recipeBlockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(recipeBlockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lackRpmBlock);
            ServerContext.WorldBlockDatastore.TryAddBlock(recipeBlockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var lackTorqueBlock);
            MachineRecipeSelectTestUtil.SelectRecipe(lackRpmBlock, recipe);
            MachineRecipeSelectTestUtil.SelectRecipe(lackTorqueBlock, recipe);

            var lackRpmInventory = lackRpmBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            var lackTorqueInventory = lackTorqueBlock.GetComponent<VanillaMachineBlockInventoryComponent>();

            foreach (var inputItem in recipe.InputItems)
            {
                lackRpmInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
                lackTorqueInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var gearMachineParam = lackRpmBlock.BlockMasterElement.BlockParam as GearMachineBlockParam;

            var lackRpmProcessor = lackRpmBlock.GetComponent<VanillaMachineProcessorComponent>();
            var lackTorqueProcessor = lackTorqueBlock.GetComponent<VanillaMachineProcessorComponent>();

            // 満額の半分の電力を供給し、加工に約2倍の時間がかかることを検証する。gear networkはall-or-nothing（不足は全停止）で部分供給しないため機械へ直接供給する
            // Supply half the full power to verify ~2x processing time; gear network is all-or-nothing (deficit stops entirely), so feed the machine directly
            var halfPower = (float)(gearMachineParam.GearConsumption.BaseTorque * gearMachineParam.GearConsumption.BaseRpm) * 0.5f;

            // 半分電力なので2倍の時間が必要（確率的丸めによる変動を考慮した大きめのマージン）
            // Half power means 2x time needed (large margin to account for probabilistic rounding variance)
            var craftTicks = GameUpdater.SecondsToTicks(recipe.Time * 3);

            for (uint tick = 0; tick < craftTicks; tick++)
            {
                // tick数を先に設定してから処理を行う
                // Set tick count before processing
                GameUpdater.RunFrames(1);

                lackRpmProcessor.SupplyExternalPower(halfPower);
                lackTorqueProcessor.SupplyExternalPower(halfPower);

                lackRpmProcessor.Update();
                lackTorqueProcessor.Update();
            }

            //検証
            AssertInventory(lackRpmInventory, recipe);
            AssertInventory(lackTorqueInventory, recipe);
        }
        
        private void AssertInventory(VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
        {
            (List<IItemStack> input, List<IItemStack> output) = GetInputOutputSlot(inventory);
            
            Assert.AreEqual(0, input.Count);
            foreach (var inputItem in input) Assert.AreEqual(ItemMaster.EmptyItemId, inputItem.Id);
            
            Assert.AreNotEqual(0, output.Count);
            for (var i = 0; i < output.Count; i++)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(outputItemId, output[i].Id);
                Assert.AreEqual(recipe.OutputItems[i].Count, output[i].Count);
            }
        }
        
        private (List<IItemStack>, List<IItemStack>) GetInputOutputSlot(VanillaMachineBlockInventoryComponent vanillaMachineInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            
            var inputSlot = vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
            inputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            var outputSlot = vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            return (inputSlot, outputSlot);
        }
    }
}