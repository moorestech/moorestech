using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.GearElectric;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    /// <summary>
    /// 歯車発電機のテンプレートクラス
    /// </summary>
    public class VanillaGearElectricGeneratorTemplate : IBlockTemplate
    {
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGearElectricGenerator(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGearElectricGenerator(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock CreateGearElectricGenerator(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as GearElectricGeneratorBlockParam;

            // ギア接続の設定
            var gearConnectSetting = configParam.Gear.GearConnects;
            var gearConnectorComponent = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);

            // GearElectricGeneratorコンポーネントの作成
            var gearElectricGeneratorComponent = componentStates == null
                ? new GearElectricGeneratorComponent(
                    configParam,
                    blockInstanceId,
                    blockPositionInfo,
                    gearConnectorComponent
                )
                : new GearElectricGeneratorComponent(
                    componentStates,
                    configParam,
                    blockInstanceId,
                    blockPositionInfo,
                    gearConnectorComponent
                );

            // コンポーネントリストの作成
            var components = new List<IBlockComponent>
            {
                gearElectricGeneratorComponent,
                gearConnectorComponent
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}