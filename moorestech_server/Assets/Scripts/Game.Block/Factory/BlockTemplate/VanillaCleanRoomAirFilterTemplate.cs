using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    /// <summary>
    ///     クリーンルーム清浄機（電力消費・フィルタースロット付き）のテンプレート
    ///     Template for the clean-room air purifier with power consumption and a filter slot
    /// </summary>
    public class VanillaCleanRoomAirFilterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static IBlock Create(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (CleanRoomAirFilterBlockParam)blockMasterElement.BlockParam;

            var filterComponent = componentStates == null
                ? new CleanRoomAirFilterComponent(blockInstanceId, param)
                : new CleanRoomAirFilterComponent(componentStates, blockInstanceId, param);

            // 清浄機はConsumer役をワイヤー端点に渡す
            // The purifier passes the consumer role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, filterComponent, componentStates);

            var components = new List<IBlockComponent>
            {
                filterComponent,
                new CleanRoomAirFilterInventoryComponent(filterComponent.FilterSlot),
                wireConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
