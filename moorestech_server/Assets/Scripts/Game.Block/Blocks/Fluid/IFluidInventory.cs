using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.FluidInventoryConnectsModule;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source);
        
        public static BlockConnectorComponent<IFluidInventory> CreateFluidInventoryConnector(FluidInventoryConnects fluidInventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IFluidInventory>(
                fluidInventoryConnects.InflowConnects,
                fluidInventoryConnects.OutflowConnects,
                blockPositionInfo
            );
        }
    }
}