using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent
    {
        private BlockPositionInfo _blockPositionInfo;
        private BlockConnectorComponent<IFluidInventory> _connectorComponent;
        
        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectorComponent = connectorComponent;
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
        }
    }
}