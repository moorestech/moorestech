using System;
using System.Collections.Generic;
using Core.Master;
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
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltParam = blockElement.BlockParam as GearBeltConveyorBlockParam;
            var blockName = blockElement.Name;
            
            var gearEnergyTransformerConnector = new BlockConnectorComponent<IGearEnergyTransformer>(
                gearBeltParam.GearConnects,
                gearBeltParam.GearConnects,
                blockPositionInfo
            );
            var inventoryConnector = BlockTemplateUtil.CreateInventoryConnector(gearBeltParam.InventoryConnectors, blockPositionInfo);
            var vanillaBeltConveyorComponent = new VanillaBeltConveyorComponent(
                gearBeltParam.BeltConveyorItemCount,
                0,
                inventoryConnector,
                blockName
            );
            
            var gearBeltConveyorComponent = new GearBeltConveyorComponent(vanillaBeltConveyorComponent, blockInstanceId, gearBeltParam.BeltConveyorSpeed, (Torque)gearBeltParam.RequiredTorque, gearEnergyTransformerConnector);
            
            var blockComponents = new List<IBlockComponent>
            {
                gearBeltConveyorComponent,
                vanillaBeltConveyorComponent,
                gearEnergyTransformerConnector,
                inventoryConnector,
            };
            return new BlockSystem(blockInstanceId, blockElement.BlockGuid, blockComponents, blockPositionInfo);
        }
        public IBlock Load(string state, BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            throw new NotImplementedException("さっさと実装しろ！");
        }
    }
}