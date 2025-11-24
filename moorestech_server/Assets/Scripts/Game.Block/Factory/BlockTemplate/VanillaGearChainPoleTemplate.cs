using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearChainPoleTemplate : IBlockTemplate
    {
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // セーブデータ付きでブロックを生成する
            // Create block with saved states
            return Create(blockMasterElement, blockInstanceId, blockPositionInfo, componentStates);
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            // 新規生成用のブロックを構築する
            // Build block for fresh creation
            return Create(blockMasterElement, blockInstanceId, blockPositionInfo, null);
        }

        private static IBlock Create(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, Dictionary<string, string> componentStates)
        {
            // マスターからパラメータを抽出する
            // Extract parameters from master data
            var param = blockMasterElement.BlockParam as GearChainPoleBlockParam;
            var gearConnects = param.Gear.GearConnects;

            // ギア接続コンポーネントとチェーンポールを初期化する
            // Initialize gear connector and chain pole component
            var connectorComponent = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnects, gearConnects, blockPositionInfo);
            var chainPoleComponent = new GearChainPoleComponent(param.MaxConnectionDistance, blockInstanceId, connectorComponent, componentStates);

            var components = new List<IBlockComponent>
            {
                chainPoleComponent,
                connectorComponent,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
