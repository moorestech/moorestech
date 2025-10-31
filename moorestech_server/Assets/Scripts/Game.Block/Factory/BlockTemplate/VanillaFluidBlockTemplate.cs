using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaFluidBlockTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private BlockSystem GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var fluidPipeParam = (blockMasterElement.BlockParam as FluidPipeBlockParam)!;
            
            var inventoryConnects = fluidPipeParam.FluidInventoryConnectors;
            BlockConnectorComponent<IFluidInventory> connectorComponent = IFluidInventory.CreateFluidInventoryConnector(inventoryConnects, blockPositionInfo);
            
            var fluidPipeComponent = new FluidPipeComponent(blockPositionInfo, connectorComponent, fluidPipeParam.Capacity, componentStates);
            var saveComponent = new FluidPipeSaveComponent(fluidPipeComponent);
            var components = new List<IBlockComponent>
            {
                fluidPipeComponent,
                connectorComponent,
                saveComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}