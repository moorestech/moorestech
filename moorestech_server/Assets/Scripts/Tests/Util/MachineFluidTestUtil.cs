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
    // 機械液体テスト用のConnectedInfo組立と出力タンク取得
    // Machine fluid test helpers for ConnectedInfo and output tanks
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

        // 出力タンク列取得（private field経由）
        // Fetch the machine's output tank list via the private field
        public static IReadOnlyList<FluidContainer> GetOutputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            return outputInventory.FluidOutputSlot;
        }
    }
}
