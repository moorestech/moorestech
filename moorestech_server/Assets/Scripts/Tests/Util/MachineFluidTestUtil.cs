using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using Tests.Module.TestMod;

namespace Tests.Util
{
    // 機械の液体テスト用ヘルパー。タンク指定のConnectedInfo組み立てと出力タンクの取り出しを担う
    // Machine fluid test helpers: builds tank-designated ConnectedInfo and fetches output tanks
    public static class MachineFluidTestUtil
    {
        // FluidMachineIdのタンクindexへ流入指定するConnectedInfoを返す
        // Returns a ConnectedInfo designating inflow to the given tank index on FluidMachineId
        public static ConnectedInfo ConnectedToTank(int tankIndex)
        {
            var machineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FluidMachineId).BlockParam as ElectricMachineBlockParam;
            var connector = machineParam.FluidInventoryConnectors.InflowConnects
                .First(c => (c as IFluidConnector).Option.ConnectTankIndex == tankIndex);
            return new ConnectedInfo(connector, connector, null);
        }

        // 機械の出力タンク列を取り出す（MachineFluidIOTest と同じ private フィールド経路）
        // Fetch the machine's output tank list (same private-field route as MachineFluidIOTest)
        public static IReadOnlyList<FluidContainer> GetOutputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            return outputInventory.FluidOutputSlot;
        }
    }
}
