using Core.Master;
using Game.Block.Interface;
using Game.Fluid;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineFluidOutputInventory
    {
        private readonly BlockId _blockId;
        private readonly BlockInstanceId _blockInstanceId;
        
        public FluidContainer[] FluidContainers;
        
        public VanillaMachineFluidOutputInventory(
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