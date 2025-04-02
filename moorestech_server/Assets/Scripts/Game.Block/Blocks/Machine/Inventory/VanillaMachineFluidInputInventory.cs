using Core.Master;
using Game.Block.Interface;
using Game.Fluid;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineFluidInputInventory
    {
        private readonly BlockId _blockId;
        private readonly BlockInstanceId _blockInstanceId;
        
        public FluidContainer[] FluidContainers;
        
        public VanillaMachineFluidInputInventory(
            BlockId blockId,
            BlockInstanceId blockInstanceId,
            int inputSlot
        )
        {
            _blockId = blockId;
            _blockInstanceId = blockInstanceId;
            FluidContainers = new FluidContainer[inputSlot];
        }
    }
}