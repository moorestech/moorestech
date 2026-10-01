using Game.Block.Blocks.BeltConveyor.Connection;
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Transport;
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
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private static BlockSystem GetBlock(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
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

            
            
            var vanillaBeltConveyorComponent = new VanillaBeltConveyorComponent(blockInstanceId, blockPositionInfo, gearBeltParam.TimeOfItemEnterToExit,
                true, BeltTransportSpeedProfile.Gear(gearBeltParam.TimeOfItemEnterToExit, gearBeltParam.GearConsumption.BaseRpm, gearBeltParam.GearConsumption.MinimumRpm), slopeType, gearBeltParam.InventoryConnectors, componentStates);
            
            var gearBeltConveyorComponent = new GearBeltConveyorComponent(vanillaBeltConveyorComponent, blockInstanceId, gearBeltParam.TimeOfItemEnterToExit, gearBeltParam.GearConsumption, gearEnergyTransformerConnector);
            
            // 過負荷破壊コンポーネントを追加
            // Add overload breakage component
            var overloadParam = gearBeltParam as IGearOverloadParam;
            var overloadBreakageComponent = new GearOverloadBreakageComponent(blockInstanceId, gearBeltConveyorComponent, overloadParam);

            var blockComponents = new List<IBlockComponent>
            {
                gearBeltConveyorComponent,
                vanillaBeltConveyorComponent,
                gearEnergyTransformerConnector,
                inventoryConnector,
                overloadBreakageComponent
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, blockComponents, blockPositionInfo);
        }
    }
}
