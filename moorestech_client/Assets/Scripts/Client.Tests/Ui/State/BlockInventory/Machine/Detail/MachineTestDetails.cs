using System;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.FluidInventoryConnectsModule;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の状態詳細と param の試験用の組み立て
    /// Test builders for the machine's state details and param
    /// </summary>
    public static class MachineTestDetails
    {
        public static CommonMachineBlockStateDetail Common(string stateType, float currentPower, float requestPower)
        {
            return new CommonMachineBlockStateDetail(currentPower, requestPower, 0.25f, stateType, VanillaMachineBlockStateConst.IdleState);
        }

        public static MachineBlockStateDetail Machine(Guid runningRecipe, Guid selectedRecipe, bool outputSpaceShort)
        {
            return new MachineBlockStateDetail(0.5f, runningRecipe, selectedRecipe, outputSpaceShort);
        }

        // 構成の数だけを持つ IMachineParam の偽物
        // A fake IMachineParam that only carries the layout counts
        public sealed class FakeMachineParam : IMachineParam
        {
            public int InputSlotCount { get; }
            public int OutputSlotCount { get; }
            public int InputTankCount { get; }
            public int OutputTankCount { get; }
            public float InnerTankCapacity => 0;
            public int ModuleSlotCount { get; }
            public bool AllowQualityModule => false;
            public FluidInventoryConnects FluidInventoryConnectors => null;

            public FakeMachineParam(int inputSlotCount, int outputSlotCount, int inputTankCount, int outputTankCount, int moduleSlotCount)
            {
                InputSlotCount = inputSlotCount;
                OutputSlotCount = outputSlotCount;
                InputTankCount = inputTankCount;
                OutputTankCount = outputTankCount;
                ModuleSlotCount = moduleSlotCount;
            }
        }
    }
}
