using Game.Block.Blocks.BeltConveyor.Connection;
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Transport
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private BlockSystem GetBlock(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = blockMasterElement.BlockParam as BeltConveyorBlockParam;

            var slopeType = beltParam.SlopeType switch
            {
                BeltConveyorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                BeltConveyorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                BeltConveyorBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };

            // 搬送の実体はワールド全体の組が持つ。blockは接続と機械からの搬入口だけを持つ
            // Transport lives in the world-wide assembly; the block holds only its connections and the inlet for machines
            var connectorComponent = BeltInventoryConnectionContext.Create(beltParam.InventoryConnectors, blockPositionInfo, slopeType);
            var components = new List<IBlockComponent>
            {
                new BeltConveyorInventoryComponent(blockInstanceId),
                connectorComponent
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
