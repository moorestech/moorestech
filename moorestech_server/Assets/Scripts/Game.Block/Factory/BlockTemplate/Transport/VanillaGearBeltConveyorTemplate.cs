using Game.Block.Blocks.BeltConveyor.Connection;
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Transport
{
    public class VanillaGearBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static BlockSystem GetBlock(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltParam = blockMasterElement.BlockParam as GearBeltConveyorBlockParam;

            var gearEnergyTransformerConnector = new BlockConnectorComponent<IGearEnergyTransformer, GearContext>(
                gearBeltParam.Gear.GearConnects,
                gearBeltParam.Gear.GearConnects,
                blockPositionInfo
            );
            var slopeType = gearBeltParam.SlopeType switch
            {
                GearBeltConveyorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                GearBeltConveyorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                GearBeltConveyorBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var inventoryConnector = BeltInventoryConnectionContext.Create(gearBeltParam.InventoryConnectors, blockPositionInfo, slopeType);

            // 搬送の実体はワールド全体の組が持ち、速度はマスタで固定。歯車は回転数の受け手と過負荷破壊だけを担う
            // Transport lives in the world-wide assembly at the fixed master speed; the gear side only receives rotation and handles overload breakage
            var gearEnergyTransformer = new GearEnergyTransformer(gearBeltParam.GearConsumption, blockInstanceId, gearEnergyTransformerConnector);
            var overloadParam = gearBeltParam as IGearOverloadParam;
            var overloadBreakageComponent = new GearOverloadBreakageComponent(blockInstanceId, gearEnergyTransformer, overloadParam);

            var blockComponents = new List<IBlockComponent>
            {
                gearEnergyTransformer,
                new BeltConveyorInventoryComponent(blockInstanceId),
                gearEnergyTransformerConnector,
                inventoryConnector,
                overloadBreakageComponent
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, blockComponents, blockPositionInfo);
        }
    }
}
