using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public List<FluidStack> GetFluidInventory();

        // 接続経由で流体を受け取り、受け取れなかった残量を返す。
        // connectedInfoは送り手側connectorのエントリ（SelfConnector=送り手側、TargetConnector=受け手側）。受け手は自分側のオプション（ConnectTankIndex等）をTargetConnectorから読む
        // Receive fluid via a connection and return the unaccepted remainder.
        // connectedInfo is the sender-side connector entry (SelfConnector = sender, TargetConnector = receiver); receivers read their own options (e.g. ConnectTankIndex) from TargetConnector
        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo);

        public static BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> CreateFluidInventoryConnector(FluidInventoryConnects fluidInventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>(
                fluidInventoryConnects.InflowConnects,
                fluidInventoryConnects.OutflowConnects,
                blockPositionInfo
            );
        }
    }
}
