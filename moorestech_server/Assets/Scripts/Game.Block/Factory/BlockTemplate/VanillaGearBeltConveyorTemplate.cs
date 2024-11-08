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
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(state, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private static BlockSystem GetBlock(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltParam = blockMasterElement.BlockParam as GearBeltConveyorBlockParam;
            
            var gearEnergyTransformerConnector = new BlockConnectorComponent<IGearEnergyTransformer>(
                gearBeltParam.Gear.GearConnects,
                gearBeltParam.Gear.GearConnects,
                blockPositionInfo
            );
            var inventoryConnector = BlockTemplateUtil.CreateInventoryConnector(gearBeltParam.InventoryConnectors, blockPositionInfo);
            var slopeType = gearBeltParam.SlopeType switch
            {
                ItemShooterBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                ItemShooterBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var itemFactory = new CommonBeltConveyorInventoryItemFactory();
            var vanillaBeltConveyorComponent = 
                state == null ? 
                    new VanillaBeltConveyorComponent(gearBeltParam.BeltConveyorItemCount, 0, inventoryConnector, slopeType, itemFactory) :
                    new VanillaBeltConveyorComponent(state, gearBeltParam.BeltConveyorItemCount, 0, inventoryConnector,slopeType, itemFactory);
            
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