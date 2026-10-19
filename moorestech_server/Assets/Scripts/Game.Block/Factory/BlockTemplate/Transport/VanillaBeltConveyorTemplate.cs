using Game.Block.Blocks.BeltConveyor.Connection;
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Transport
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private BlockSystem GetBlock(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = blockMasterElement.BlockParam as BeltConveyorBlockParam;

            var slopeType = beltParam.SlopeType switch
            {
                BeltConveyorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                BeltConveyorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                BeltConveyorBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };

            // 搬送の実体はワールド全体の組が持つ。blockは接続・機械からの搬入口・セーブ入口だけを持つ
            // Transport lives in the world-wide assembly; the block holds only its connections, the inlet for machines and the save entry
            var connectorComponent = BeltInventoryConnectionContext.Create(beltParam.InventoryConnectors, blockPositionInfo, slopeType);
            var saveStateComponent = componentStates == null
                ? new BeltConveyorSaveStateComponent(blockInstanceId)
                : new BeltConveyorSaveStateComponent(componentStates, blockInstanceId);
            var components = new List<IBlockComponent>
            {
                new BeltConveyorInventoryComponent(blockInstanceId),
                saveStateComponent,
                connectorComponent
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
