using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
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
            var beltConveyorConnector = new VanillaBeltConveyorConnector(inventoryConnector); 
            
            var slopeType = gearBeltParam.SlopeType switch
            {
                ItemShooterBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                ItemShooterBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var itemCount = gearBeltParam.BeltConveyorItemCount;
            
            // 歯車ベルトコンベアはRPMによって速度が変わるため、デフォルトは0となる
            // Gear belt conveyors have different speeds depending on the RPM, so the default is 0
            var time = 0;
            
            var vanillaBeltConveyorComponent = componentStates == null ? 
                    new VanillaBeltConveyorComponent(itemCount, time, beltConveyorConnector, slopeType) :
                    new VanillaBeltConveyorComponent(componentStates, itemCount, time, beltConveyorConnector,slopeType);
            
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