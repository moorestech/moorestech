using System.Linq;
using Core.Master;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using Tests.Module.TestMod;

namespace Tests.Util
{
    // 液体タンク指定のConnectedInfoを組むヘルパー。実際のブロックマスタが持つ流入コネクタをそのまま使う
    // Helper to build a tank-designated ConnectedInfo; reuses the block master's real inflow connector as-is
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
    }
}
