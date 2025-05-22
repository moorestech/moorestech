using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.FluidInventoryConnectsModule;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        public FluidContainer FluidContainer { get; }
        
        public static BlockConnectorComponent<IFluidInventory> CreateFluidInventoryConnector(FluidInventoryConnects fluidInventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IFluidInventory>(
                fluidInventoryConnects.InflowConnects,
                fluidInventoryConnects.OutflowConnects,
                blockPositionInfo
            );
        }
        
        //TODO: FluidContainerに更新用メソッドを追加した場合はそちらでOnNextを呼ぶ
        public void OnContainerChanged();
    }
}