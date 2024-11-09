using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CraftChainer.BlockComponent.ProviderChest;
using Mooresmaster.Model.BlocksModule;

namespace Game.CraftChainer.BlockComponent.Template
{
    public class ChainerProviderChestTemplate : IBlockTemplate
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
            var chest = blockMasterElement.BlockParam as CraftChainerProviderChestBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(chest.InventoryConnectors, blockPositionInfo);
            
            var inserter = new ProviderChestBlockInventoryInserter(inputConnectorComponent);
            
            var chestComponent = componentStates == null ? new VanillaChestComponent(blockInstanceId, chest.ItemSlotCount, inserter) : new VanillaChestComponent(componentStates, blockInstanceId, chest.ItemSlotCount, inserter);
            
            var chainerProviderChestComponent = new ChainerProviderChestComponent(inserter);
            
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
                chainerProviderChestComponent
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}