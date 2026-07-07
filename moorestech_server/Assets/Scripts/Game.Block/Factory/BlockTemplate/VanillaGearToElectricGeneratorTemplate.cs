using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearToElectricGeneratorTemplate : IBlockTemplate
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
            var param = blockMasterElement.BlockParam as GearToElectricGeneratorBlockParam;
            var gearConnects = param.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge>(gearConnects, gearConnects, blockPositionInfo);
            var generatorComponent = new GearToElectricGeneratorComponent(param, blockInstanceId, gearConnector);
            // 歯車→電気変換はGenerator役をワイヤー端点に渡す
            // Gear-to-electric passes the generator role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, generatorComponent, componentStates);


            var components = new List<IBlockComponent>
            {
                generatorComponent,
                gearConnector,
                wireConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
