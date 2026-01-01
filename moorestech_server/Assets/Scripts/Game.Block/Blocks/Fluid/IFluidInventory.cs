using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.FluidInventoryConnectsModule;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public List<FluidStack> GetFluidInventory();
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source);
        
        public static BlockConnectorComponent<IFluidInventory> CreateFluidInventoryConnector(FluidInventoryConnects fluidInventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            var inflowConnectors = BlockConnectorInfoFactory.FromConnectors(fluidInventoryConnects.InflowConnects);
            var outflowConnectors = BlockConnectorInfoFactory.FromConnectors(fluidInventoryConnects.OutflowConnects);
            return new BlockConnectorComponent<IFluidInventory>(
                inflowConnectors,
                outflowConnectors,
                blockPositionInfo
            );
        }
    }
}
