using Game.Block.Interface.Component;
using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using static Tests.CombinedTest.Core.Machine.MachineInventoryTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    internal static class MachineStateDetailsTestHelper
    {
        // レシピが要求する液体を入力タンクへ1回分投入する（液体を使わないレシピでは何もしない）
        // Insert one recipe's worth of required fluids into the input tanks (no-op for recipes without fluids)
        public static void InsertRecipeFluids(Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe, VanillaMachineBlockInventoryComponent inventoryComponent)
        {
            if (recipe.InputFluids == null || recipe.InputFluids.Length == 0) return;
            var containers = GetInputFluidContainers(inventoryComponent);
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var inputFluid = recipe.InputFluids[i];
                containers[i].AddLiquid(new FluidStack(inputFluid.Amount, MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid)));
            }
        }
        public static void ValidateMachineBlockStateDetails(BlockStateDetail[] details, string expectedState,
            float expectedCurrentPower, float expectedRequestedPower, float expectedProcessingRate)
        {
            Assert.AreEqual(2, details.Length);
            var detail = details.FirstOrDefault(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey);
            Assert.IsNotNull(detail, "CommonMachineBlockStateDetailが含まれていません");
            var stateData = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(detail.Value);
            Assert.AreEqual(expectedState, stateData.CurrentStateType, "状態が期待値と一致しません");
            Assert.AreEqual(expectedCurrentPower, stateData.CurrentPower, 0.01f, "現在の電力が期待値と一致しません");
            Assert.AreEqual(expectedRequestedPower, stateData.RequestPower, 0.01f, "要求電力が期待値と一致しません");
            Assert.AreEqual(expectedProcessingRate, stateData.ProcessingRate, 0.01f, "処理率が期待値と一致しません");
        }
        public static (string state, float currentPower, float requestedPower, float processingRate) ExtractMachineDetails(BlockStateDetail[] details)
        {
            Assert.AreEqual(2, details.Length);
            var detail = details.FirstOrDefault(d => d.Key == CommonMachineBlockStateDetail.BlockStateDetailKey);
            var stateData = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(detail.Value);
            return (stateData.CurrentStateType, stateData.CurrentPower, stateData.RequestPower, stateData.ProcessingRate);
        }
    }
}
