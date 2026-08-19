using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using Mooresmaster.Model.ItemsModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // stateへ公開される要求電力（充足率の分母）が、分子と同じ状態基準でラッチされ続けることを検証する
    // Verifies the request power published to the state (the satisfaction-rate denominator) stays latched on the same state basis as the numerator
    public class MachinePublishedRequestPowerTest
    {
        // 期待値をproduction式から独立させるためのマスタ実値（forUnitTest mod の TestElectricMachine）
        // Master values kept independent from the production formula (TestElectricMachine in the forUnitTest mod)
        private const float MachineIdlePowerRate = 0.25f;
        private const int ModuleRangeStart = 5;

        [Test]
        // 加工中はモジュール倍率込みの実効要求電力がstateへ載ることを確認する
        // Verify the state carries the effective request power including the module multiplier while processing
        public void ProcessingPublishesEffectiveRequestPowerTest()
        {
            var (block, processor, _) = StartProcessingWithSpeedModule();

            // 遷移の次tickで分子分母がそろって加工基準になる
            // One tick after the transition both numerator and denominator sit on the processing basis
            AdvanceOneTickWithFullPower(processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(processor.RequestPower * SpeedModulePowerMultiplier(), GetStateRequestPower(block), 0.01f);
        }

        [Test]
        // レシピ解除でIdleへ戻した直後、通知前に分母がIdle基準へ取り直されることを確認する
        // Verify the denominator is re-latched onto the idle basis before notifying, right after clearing the recipe returns the machine to idle
        public void ClearingRecipeRelatchesPublishedRequestPowerTest()
        {
            var (block, processor, selector) = StartProcessingWithSpeedModule();
            AdvanceOneTickWithFullPower(processor);
            Assert.AreEqual(processor.RequestPower * SpeedModulePowerMultiplier(), GetStateRequestPower(block), 0.01f);

            // Updateを挟まずにレシピを解除する。ここで取り直さないと加工基準の分母が居残る
            // Clear the recipe without an intervening Update; without the re-latch the processing-basis denominator would linger
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.ClearSelectedRecipe(overflow));
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(processor.RequestPower * MachineIdlePowerRate, GetStateRequestPower(block), 0.01f);
        }

        // 速度モジュールを装着した機械を加工状態まで進める
        // Bring a machine equipped with a speed module up to the processing state
        private static (IBlock block, VanillaMachineProcessorComponent processor, IMachineRecipeSelectorComponent selector) StartProcessingWithSpeedModule()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);

            var speedModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            inventory.SetItem(ModuleRangeStart, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(speedModule.ItemGuid), 1));

            var recipe = GetMachineRecipe();
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            foreach (var inputItem in recipe.InputItems)
            {
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            AdvanceOneTickWithFullPower(processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            return (block, processor, selector);
        }

        private static float SpeedModulePowerMultiplier()
        {
            var speedModule = MasterHolder.ItemMaster.Items.Modules.First(m => m.EffectAxis == ModuleMasterElement.EffectAxisConst.Speed);
            return 1f + speedModule.TradeoffValue;
        }

        private static MachineRecipeMasterElement GetMachineRecipe()
        {
            var machineBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            return MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machineBlockGuid);
        }

        private static void AdvanceOneTickWithFullPower(VanillaMachineProcessorComponent processor)
        {
            processor.SupplyExternalPower(processor.EffectiveRequestPower);
            GameUpdater.UpdateOneTick();
        }

        private static float GetStateRequestPower(IBlock block)
        {
            var details = block.GetComponent<IBlockStateObservable>().GetBlockStateDetails();
            var detail = details.First(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey);
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(detail.Value).RequestPower;
        }
    }
}
