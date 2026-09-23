using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Component.ConnectOverride;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Transport
{
    public class VanillaGearBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private static BlockSystem GetBlock(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltParam = blockMasterElement.BlockParam as GearBeltConveyorBlockParam;
            
            var gearEnergyTransformerConnector = new BlockConnectorComponent<IGearEnergyTransformer, GearConnectJudge>(
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
            var inventoryConnector = new BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>(
                gearBeltParam.InventoryConnectors.InputConnects,
                gearBeltParam.InventoryConnectors.OutputConnects, blockPositionInfo,
                new BeltConnectionOverride(blockPositionInfo, slopeType,
                    gearBeltParam.InventoryConnectors));
            var world = Game.Context.ServerContext.GetService<IBeltWorldMutation>();
            var belt = new SegmentBeltComponent(blockPositionInfo, slopeType, world, componentStates);
            var gearBeltConveyorComponent = new GearBeltConveyorComponent(blockInstanceId, gearBeltParam.GearConsumption, gearEnergyTransformerConnector);

            // 過負荷破壊コンポーネントを追加
            // Add overload breakage component
            var overloadParam = gearBeltParam as IGearOverloadParam;
            var overloadBreakageComponent = new GearOverloadBreakageComponent(blockInstanceId, gearBeltConveyorComponent, overloadParam);

            var blockComponents = new List<IBlockComponent>
            {
                gearBeltConveyorComponent,
                belt,
                gearEnergyTransformerConnector,
                inventoryConnector,
                overloadBreakageComponent
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, blockComponents, blockPositionInfo);
        }
    }
}
