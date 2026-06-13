using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // エアフィルターブロックを組み立てる。item / 本体 / inventoryコネクタ の3コンポーネント。
    // Builds the air filter block: item inventory / core / inventory-connector components.
    public class VanillaCleanRoomAirFilterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Build(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Build(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as CleanRoomAirFilterBlockParam;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(param.FilterItemGuid);

            // フィルタースロット（Load時はstateを復元）。
            // Filter slots; restore saved state on Load.
            var itemComponent = componentStates == null
                ? new CleanRoomAirFilterItemComponent(param.FilterItemSlotCount, filterItemId, blockInstanceId)
                : new CleanRoomAirFilterItemComponent(componentStates, param.FilterItemSlotCount, filterItemId, blockInstanceId);

            // 本体（電力/実効q/摩耗）。number型はfloatで生成されるのでdoubleにキャスト。
            // Core component (power / effective q / wear); number props are float, cast to double.
            var filterComponent = componentStates == null
                ? new CleanRoomAirFilterComponent(blockInstanceId, (double)param.RemovalVolumePerSecond, param.RequiredPower, (double)param.FilterCapacity, itemComponent)
                : new CleanRoomAirFilterComponent(componentStates, blockInstanceId, (double)param.RemovalVolumePerSecond, param.RequiredPower, (double)param.FilterCapacity, itemComponent);

            // ベルト等からフィルターを搬入できるよう inventory コネクタを付ける。
            // Inventory connector so belts can feed filters into the item inventory.
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);

            var components = new List<IBlockComponent>
            {
                itemComponent,
                filterComponent,
                connector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
