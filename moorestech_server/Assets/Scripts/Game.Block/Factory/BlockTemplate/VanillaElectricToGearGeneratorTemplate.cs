using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricToGearGeneratorTemplate : IBlockTemplate
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
            var param = blockMasterElement.BlockParam as ElectricToGearGeneratorBlockParam;
            var gearConnects = param.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnects, gearConnects, blockPositionInfo);

            // セーブデータがあれば復元コンストラクタを使用し、なければ初期化コンストラクタを使用する
            // Use the restore constructor if save data exists, otherwise use the initializing constructor
            var component = componentStates == null
                ? new ElectricToGearGeneratorComponent(param, blockInstanceId, gearConnector)
                : new ElectricToGearGeneratorComponent(componentStates, param, blockInstanceId, gearConnector);

            var components = new List<IBlockComponent>
            {
                component,
                gearConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
