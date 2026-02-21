using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private static BlockSystem GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltParam = blockMasterElement.BlockParam as GearBeltConveyorBlockParam;
            
            var gearEnergyTransformerConnector = new BlockConnectorComponent<IGearEnergyTransformer>(
                gearBeltParam.Gear.GearConnects,
                gearBeltParam.Gear.GearConnects,
                blockPositionInfo
            );
            var inventoryConnector = BlockTemplateUtil.CreateInventoryConnector(gearBeltParam.InventoryConnectors, blockPositionInfo);
            var beltConveyorConnector = new VanillaBeltConveyorBlockInventoryInserter(blockInstanceId, inventoryConnector); 
            
            var slopeType = gearBeltParam.SlopeType switch
            {
                GearBeltConveyorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                GearBeltConveyorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                GearBeltConveyorBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var itemCount = gearBeltParam.BeltConveyorItemCount;
            
            // RPM供給前は搬送を停止させるため、無限大の時間を設定する
            // Use infinite time to stop transport before RPM is supplied
            var time = float.PositiveInfinity;
            
            var vanillaBeltConveyorComponent = componentStates == null ? 
                    new VanillaBeltConveyorComponent(itemCount, time, beltConveyorConnector, slopeType) :
                    new VanillaBeltConveyorComponent(componentStates, itemCount, time, beltConveyorConnector,slopeType, gearBeltParam.InventoryConnectors);
            
            var gearBeltConveyorComponent = new GearBeltConveyorComponent(vanillaBeltConveyorComponent, blockInstanceId, gearBeltParam.BeltConveyorSpeed, (Torque)gearBeltParam.RequireTorque, gearEnergyTransformerConnector);
            
            var blockComponents = new List<IBlockComponent>
            {
                gearBeltConveyorComponent,
                vanillaBeltConveyorComponent,
                gearEnergyTransformerConnector,
                inventoryConnector
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, blockComponents, blockPositionInfo);
        }
    }
}
