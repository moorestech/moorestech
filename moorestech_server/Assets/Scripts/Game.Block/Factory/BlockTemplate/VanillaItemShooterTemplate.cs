using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaItemShooterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = blockMasterElement.BlockParam as ItemShooterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(itemShooter.InventoryConnectors, blockPositionInfo);
            
            var direction = blockPositionInfo.BlockDirection;
            var chestComponent = new ItemShooterComponent(inputConnectorComponent, itemShooter);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = blockMasterElement.BlockParam as ItemShooterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(itemShooter.InventoryConnectors, blockPositionInfo);
            
            var direction = blockPositionInfo.BlockDirection;
            var chestComponent = new ItemShooterComponent(state, inputConnectorComponent, itemShooter);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}