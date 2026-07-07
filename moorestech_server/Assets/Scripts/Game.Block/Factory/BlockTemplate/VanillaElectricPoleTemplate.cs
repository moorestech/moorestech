using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricPole;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricPoleTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Create(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (ElectricPoleBlockParam)blockMasterElement.BlockParam;
            var transformer = new VanillaElectricPoleComponent(blockInstanceId);
            // 電柱はTransformer役のみをワイヤー端点に渡す
            // Pole passes only the transformer role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, transformer, componentStates);
            var components = new List<IBlockComponent>
            {
                transformer,
                wireConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
