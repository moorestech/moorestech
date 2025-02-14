using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.InventoryConnectsModule;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public FluidContainer FluidContainer { get; }
        
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