using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaFilterSplitterTemplate : IBlockTemplate
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
            var param = blockMasterElement.BlockParam as FilterSplitterBlockParam;

            var connectorComponent = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);

            // マスタの outputConnects の順序が方向インデックスとなる
            // The order of outputConnects in master data defines the direction index
            BlockConnectInfoElement[] outputElements = param.InventoryConnectors.OutputConnects.items;

            var splitter = componentStates == null
                ? new VanillaFilterSplitterComponent(blockInstanceId, connectorComponent, outputElements, param.FilterSlotCountPerDirection)
                : new VanillaFilterSplitterComponent(componentStates, blockInstanceId, connectorComponent, outputElements, param.FilterSlotCountPerDirection);

            var components = new List<IBlockComponent>
            {
                splitter,
                connectorComponent,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
