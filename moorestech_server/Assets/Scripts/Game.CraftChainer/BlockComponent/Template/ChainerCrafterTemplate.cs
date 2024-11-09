using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
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
            var chestComponent = componentStates == null ? 
                new VanillaChestComponent(blockInstanceId, chest.ItemSlotCount, inputConnectorComponent) : 
                new VanillaChestComponent(componentStates, blockInstanceId, chest.ItemSlotCount, inputConnectorComponent);
            
            var chainerCrafter = new ChainerCrafterComponent();
            
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