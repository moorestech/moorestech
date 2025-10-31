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
    public class CraftChainerCrafterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as CraftChainerCrafterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inserter = new CraftChainerCrafterInserter(inputConnectorComponent);
            
            var chestComponent = componentStates == null ? 
                new VanillaChestComponent(blockInstanceId, param.ItemSlotCount, inserter) : 
                new VanillaChestComponent(componentStates, blockInstanceId, param.ItemSlotCount, inserter);
            
            var chainerCrafter = componentStates == null ?
                new CraftCraftChainerCrafterComponent() :
                new CraftCraftChainerCrafterComponent(componentStates);
            
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