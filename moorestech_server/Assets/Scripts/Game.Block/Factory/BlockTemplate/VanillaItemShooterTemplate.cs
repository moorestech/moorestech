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
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = blockElement.BlockParam as ItemShooterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(itemShooter.InventoryConnectors, blockPositionInfo);
            
            var direction = blockPositionInfo.BlockDirection;
            var chestComponent = new ItemShooterComponent(inputConnectorComponent, itemShooter);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockElement.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = blockElement.BlockParam as ItemShooterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(itemShooter.InventoryConnectors, blockPositionInfo);
            
            var direction = blockPositionInfo.BlockDirection;
            var chestComponent = new ItemShooterComponent(state, inputConnectorComponent, itemShooter);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockElement.BlockId, components, blockPositionInfo);
        }
    }
}