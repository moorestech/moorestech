using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Service;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CraftChainer.BlockComponent.Crafter;
using Mooresmaster.Model.BlocksModule;

namespace Game.CraftChainer.BlockComponent.Template
{
    public class ChainerCrafterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var chest = blockMasterElement.BlockParam as CraftChainerCrafterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(chest.InventoryConnectors, blockPositionInfo);
            var inserter = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);
            
            var chestComponent = componentStates == null ? 
                new VanillaChestComponent(blockInstanceId, chest.ItemSlotCount, inserter) : 
                new VanillaChestComponent(componentStates, blockInstanceId, chest.ItemSlotCount, inserter);
            
            var chainerCrafter = componentStates == null ?
                new ChainerCrafterComponent() :
                new ChainerCrafterComponent(componentStates);
            
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
                chainerCrafter
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}