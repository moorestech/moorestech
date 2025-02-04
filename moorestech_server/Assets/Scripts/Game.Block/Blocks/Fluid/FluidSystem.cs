using Game.Block.Component;
using Game.Block.Interface;
using Mooresmaster.Model.InventoryConnectsModule;

namespace Game.Block.Blocks.Fluid
{
    public static class FluidSystem
    {
        public static BlockConnectorComponent<IFluidInventory> CreateFluidInventoryConnector(InventoryConnects inventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IFluidInventory>(
                inventoryConnects.InputConnects,
                inventoryConnects.OutputConnects,
                blockPositionInfo
            );
        }
    }
}