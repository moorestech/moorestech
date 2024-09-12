using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var chest = blockMasterElement.BlockParam as ChestBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(chest.InventoryConnectors, blockPositionInfo);
            var chestComponent = new VanillaChestComponent(blockInstanceId, chest.ChestItemSlotCount, inputConnectorComponent);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var chest = blockMasterElement.BlockParam as ChestBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(chest.InventoryConnectors, blockPositionInfo);
            var chestComponent = new VanillaChestComponent(state, blockInstanceId, chest.ChestItemSlotCount, inputConnectorComponent);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}