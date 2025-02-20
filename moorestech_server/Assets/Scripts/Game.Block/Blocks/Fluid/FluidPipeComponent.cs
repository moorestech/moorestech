using System;
using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent, IFluidInventory
    {
        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private BlockPositionInfo _blockPositionInfo;
        
        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectorComponent = connectorComponent;
            FluidContainer = new FluidContainer(capacity, Guid.Empty);
        }
        
        public FluidContainer FluidContainer { get; }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public void Update()
        {
            foreach (KeyValuePair<IFluidInventory, ConnectedInfo> kvp in _connectorComponent.ConnectedTargets)
            {
                var selfOption = kvp.Value.SelfOption as FluidConnectOption;
                var targetOption = kvp.Value.TargetOption as FluidConnectOption;
                var target = kvp.Value.TargetBlock.GetComponent<IFluidInventory>();
                var fluidInventory = kvp.Key;
                
                if (selfOption == null || targetOption == null || target == null) throw new Exception();
            }
        }
    }
}